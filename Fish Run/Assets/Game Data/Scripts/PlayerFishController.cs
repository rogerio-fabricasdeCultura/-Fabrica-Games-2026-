using FishAlive;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Mouse-controlled swimming. Space is read directly from the Input System keyboard.
/// PlayerInput provides the mouse action and FishMotion animates the fish.
/// </summary>
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(FishMotion), typeof(PlayerInput), typeof(CharacterController))]
public class PlayerFishController : MonoBehaviour
{
    [Header("Input")]
    public InputActionAsset InputActions;
    public bool LockCursor = true;

    [Header("Camera")]
    public Camera GameCamera;
    [Min(0.01f)] public float MouseSensitivity = 0.12f;

    [Header("Fish Movement Speed")]
    [Min(0.01f)] public float CruiseSpeed = 0.35f;
    [Min(0.01f)] public float MaxSpeed = 5f;
    [Min(0.01f)] public float Acceleration = 1.5f;
    [Min(0.01f)] public float Deceleration = 1.5f;

    [Header("Fish Direction")]
    [Min(1f)] public float TurnSpeed = 180f;
    [Range(1f, 89f)] public float MaximumPitch = 85f;
    public bool InvertY;

    [Header("Swim Animation Speed")]
    [Min(0.01f)] public float CruiseAnimationSpeed = 1f;
    [Min(0.01f)] public float FastAnimationSpeed = 1.8f;

    [Header("Movement Bounds")]
    public bool UseMovementBounds;
    public Vector3 MovementBoundsMin = new Vector3(-6f, -3.5f, -1000f);
    public Vector3 MovementBoundsMax = new Vector3(6f, 3.5f, 1000f);

    [Header("Water Surface Limit")]
    public bool KeepBelowWaterSurface = true;
    [Tooltip("Optional. When empty, use Water Surface / Water Level from the camera's Underwater Camera Effect.")]
    public Transform WaterSurface;
    [Tooltip("Fallback height when neither a water transform nor camera water settings are available.")]
    public float WaterLevel = 4f;
    [Tooltip("Minimum distance between the fish root and the surface. The animated body bounds also provide clearance.")]
    [Min(0f)] public float SurfaceClearance = 0.25f;

    [Header("Terrain Collision")]
    [Tooltip("Radius of the collision sphere around the fish root, in local units.")]
    [Min(0.01f)] public float CollisionRadius = 0.18f;
    [Tooltip("Small tolerance used by the Character Controller to slide along solid surfaces.")]
    [Min(0.001f)] public float CollisionSkinWidth = 0.01f;

    [Header("Power Up")]
    [Tooltip("Seconds of power granted by each collectible. Another pickup restarts this timer.")]
    [Min(0.1f)] public float PowerDuration = 10f;
    public UnityEvent OnPowerStarted = new UnityEvent();
    public UnityEvent OnPowerEnded = new UnityEvent();

    [Header("Runtime Power")]
    [SerializeField] private float _powerTimeRemaining;

    public bool IsPoweredUp => _powerTimeRemaining > 0f;
    public bool CanDefeatEnemies => IsPoweredUp;
    public float PowerTimeRemaining => _powerTimeRemaining;

    [Header("Runtime Input / Speed")]
    [SerializeField] private Vector2 _lookInput;
    [SerializeField] private bool _boostHeld;
    [SerializeField] private float _currentSpeed;
    [SerializeField] private bool _keyboardDetected;
    [SerializeField] private bool _mouseBound;

    public float CurrentSpeed => _currentSpeed;

    private FishMotion _fishMotion;
    private PlayerInput _playerInput;
    private InputAction _lookAction;
    private Vector3 _controlledPosition;
    private Quaternion _controlledRotation;
    private float _yaw;
    private float _pitch;
    private bool _isReady;
    private bool _cursorCaptured;
    private bool _lookPaused;
    private Keyboard _pairedKeyboard;
    private Mouse _pairedMouse;
    private UnderwaterCameraEffect _waterEffect;
    private SkinnedMeshRenderer[] _bodyRenderers;
    private CharacterController _characterController;

    private void Awake()
    {
        _powerTimeRemaining = 0f;
        _fishMotion = GetComponent<FishMotion>();
        _characterController = GetComponent<CharacterController>();
        if (_characterController == null)
        {
            // Support existing scene objects created before collisions were added.
            _characterController = gameObject.AddComponent<CharacterController>();
        }

        _playerInput = GetComponent<PlayerInput>();
        if (_playerInput == null)
        {
            // Existing scene instances may predate the RequireComponent attribute.
            _playerInput = gameObject.AddComponent<PlayerInput>();
        }

        GetComponent<Animator>().applyRootMotion = false;
        _bodyRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();

        if (GameCamera == null)
        {
            GameCamera = Camera.main;
        }
    }

    private void OnEnable()
    {
        _isReady = false;
        ConfigureCollision();
        _playerInput.enabled = true;
        _playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
        _playerInput.defaultActionMap = "Player";
        _playerInput.neverAutoSwitchControlSchemes = true;

        if (_playerInput.actions == null)
        {
            _playerInput.actions = InputActions;
        }

        if (_playerInput.actions == null)
        {
            Debug.LogError("Player Fish: assign RiverPlayerInput to Player Input > Actions.", this);
            enabled = false;
            return;
        }

        InputActionMap playerMap = _playerInput.actions.FindActionMap("Player");
        _lookAction = playerMap?.FindAction("Look");
        if (_lookAction == null)
        {
            Debug.LogError("Player Fish: RiverPlayerInput needs Player/Look.", this);
            enabled = false;
            return;
        }

        _playerInput.ActivateInput();
        BindKeyboardAndMouse();
        if (!enabled)
        {
            return;
        }

        _playerInput.SwitchCurrentActionMap("Player");

        _controlledPosition = transform.position;
        _controlledRotation = transform.rotation;
        Vector3 forward = transform.forward;
        _yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        _pitch = -Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg;
        _currentSpeed = Mathf.Max(0.01f, CruiseSpeed);
        _lookPaused = false;
        _fishMotion.SetExternalMotion(true);
        ApplyWaterSurfaceLimit();
        _isReady = true;
        if (LockCursor && Application.isFocused)
        {
            CaptureCursor();
        }
    }

    private void OnDisable()
    {
        EndPower();
        _isReady = false;
        ReleaseCursor();
        _lookInput = Vector2.zero;
        _boostHeld = false;
        _keyboardDetected = false;
        _mouseBound = false;
        _pairedKeyboard = null;
        _pairedMouse = null;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            ReleaseCursor();
        }
    }

    private void Update()
    {
        UpdatePower();
        if (!_isReady)
        {
            return;
        }

        BindKeyboardAndMouse();
        if (!enabled)
        {
            return;
        }

        if (LockCursor)
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ReleaseCursor();
                _lookPaused = true;
            }
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                CaptureCursor();
                _lookPaused = false;
            }
        }

        // Unity routes input to the Game view. Cursor lock is presentation, not an input gate.
        _lookInput = LockCursor && _lookPaused ? Vector2.zero : _lookAction.ReadValue<Vector2>();
        // Single-player Space input must not depend on PlayerInput device pairing or binding masks.
        Keyboard keyboard = Keyboard.current;
        _keyboardDetected = keyboard != null && keyboard.enabled;
        _boostHeld = _keyboardDetected && keyboard.spaceKey.isPressed;
        _mouseBound = _lookAction.enabled && _lookAction.controls.Count > 0;

        // Mouse delta is already a displacement; do not multiply it by deltaTime.
        // Keep the desired angles so TurnSpeed never discards part of a mouse movement.
        _yaw = Mathf.Repeat(_yaw + _lookInput.x * MouseSensitivity, 360f);
        _pitch += _lookInput.y * MouseSensitivity * (InvertY ? 1f : -1f);
        _pitch = Mathf.Clamp(_pitch, -MaximumPitch, MaximumPitch);
        Quaternion desiredRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        _controlledRotation = Quaternion.RotateTowards(
            _controlledRotation, desiredRotation, Mathf.Max(1f, TurnSpeed) * Time.deltaTime);

        float slowSpeed = Mathf.Max(0.01f, CruiseSpeed);
        float targetSpeed = _boostHeld ? Mathf.Max(slowSpeed, MaxSpeed) : slowSpeed;
        float speedChange = _boostHeld ? Acceleration : Deceleration;
        _currentSpeed = Mathf.MoveTowards(
            _currentSpeed, targetSpeed, Mathf.Max(0.01f, speedChange) * Time.deltaTime);

        _controlledPosition += (_controlledRotation * Vector3.forward) * _currentSpeed * Time.deltaTime;
        ApplyMovementBounds();
        ApplyWaterSurfaceLimit();

        float speedPercent = Mathf.InverseLerp(slowSpeed, Mathf.Max(slowSpeed, MaxSpeed), _currentSpeed);
        float animationSpeed = Mathf.Lerp(CruiseAnimationSpeed, FastAnimationSpeed, speedPercent);
        _fishMotion.SetExternalSwimSpeed(_currentSpeed, animationSpeed);
    }

    /// <summary>Refresh the power timer without stacking duration or changing swim speed.</summary>
    public bool TryActivatePower()
    {
        if (!isActiveAndEnabled || !_isReady)
        {
            return false;
        }

        bool wasPoweredUp = IsPoweredUp;
        _powerTimeRemaining = Mathf.Max(0.1f, PowerDuration);
        if (!wasPoweredUp)
        {
            OnPowerStarted.Invoke();
        }

        return true;
    }

    private void UpdatePower()
    {
        if (!IsPoweredUp)
        {
            return;
        }

        // Scaled time pauses the power along with the game.
        if (_powerTimeRemaining <= Time.deltaTime)
        {
            EndPower();
        }
        else
        {
            _powerTimeRemaining -= Time.deltaTime;
        }
    }

    private void EndPower()
    {
        if (!IsPoweredUp)
        {
            return;
        }

        _powerTimeRemaining = 0f;
        OnPowerEnded.Invoke();
    }

    private void LateUpdate()
    {
        if (_isReady)
        {
            // Move after animation evaluation, using a swept collider rather than teleporting.
            Vector3 previousPosition = transform.position;
            transform.rotation = _controlledRotation;
            _characterController.Move(_controlledPosition - previousPosition);
            _controlledPosition = transform.position;

            // Sliding against a bank must not push the fish through the water surface.
            if (KeepBelowWaterSurface && _controlledPosition.y > GetMaximumSwimHeight() + 0.001f)
            {
                _characterController.enabled = false;
                transform.position = previousPosition;
                _characterController.enabled = true;
                _controlledPosition = previousPosition;
            }
        }
    }

    private void ConfigureCollision()
    {
        float radius = Mathf.Max(0.01f, CollisionRadius);
        _characterController.center = Vector3.zero;
        _characterController.stepOffset = 0f;
        _characterController.radius = radius;
        _characterController.height = radius * 2f;
        // The fish swims; it should not walk up slopes or step onto the river banks.
        _characterController.slopeLimit = 0f;
        _characterController.skinWidth = Mathf.Clamp(CollisionSkinWidth, 0.001f, radius * 0.5f);
        _characterController.minMoveDistance = 0f;
        _characterController.detectCollisions = true;
        _characterController.enableOverlapRecovery = true;
    }

    private void CaptureCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        _cursorCaptured = true;
    }

    private void BindKeyboardAndMouse()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard == null || mouse == null ||
            (_pairedKeyboard == keyboard && _pairedMouse == mouse))
        {
            return;
        }

        // This single-player game always uses the current keyboard and mouse.
        if (!_playerInput.SwitchCurrentControlScheme(keyboard, mouse))
        {
            Debug.LogError("Player Fish: add the KeyboardMouse control scheme to RiverPlayerInput.", this);
            enabled = false;
            return;
        }

        _pairedKeyboard = keyboard;
        _pairedMouse = mouse;
    }

    private void ReleaseCursor()
    {
        if (!_cursorCaptured)
        {
            return;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _cursorCaptured = false;
    }

    private void ApplyWaterSurfaceLimit()
    {
        if (!KeepBelowWaterSurface)
        {
            return;
        }

        float maximumHeight = GetMaximumSwimHeight();
        if (_controlledPosition.y <= maximumHeight)
        {
            return;
        }

        _controlledPosition.y = maximumHeight;
        Vector3 forward = _controlledRotation * Vector3.forward;
        if (forward.y > 0f)
        {
            // Slide along the surface instead of repeatedly trying to swim out of it.
            Vector3 horizontalForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (horizontalForward.sqrMagnitude < 0.001f)
            {
                horizontalForward = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
            }

            _controlledRotation = Quaternion.LookRotation(horizontalForward.normalized, Vector3.up);
            _pitch = Mathf.Max(0f, _pitch);
        }
    }

    private float GetMaximumSwimHeight()
    {
        float clearance = Mathf.Max(0f, SurfaceClearance);
        foreach (SkinnedMeshRenderer bodyRenderer in _bodyRenderers)
        {
            if (bodyRenderer == null || !bodyRenderer.enabled || !bodyRenderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            // An enclosing sphere keeps the head and tail submerged while turning.
            Bounds bodyBounds = bodyRenderer.bounds;
            float bodyRadius = Vector3.Distance(bodyBounds.center, transform.position) + bodyBounds.extents.magnitude;
            clearance = Mathf.Max(clearance, bodyRadius);
        }

        return GetWaterSurfaceHeight() - clearance;
    }

    private float GetWaterSurfaceHeight()
    {
        if (WaterSurface != null)
        {
            return WaterSurface.position.y;
        }

        if (_waterEffect == null && GameCamera != null)
        {
            _waterEffect = GameCamera.GetComponent<UnderwaterCameraEffect>();
        }

        return _waterEffect != null ? _waterEffect.SurfaceHeight : WaterLevel;
    }

    private void ApplyMovementBounds()
    {
        if (!UseMovementBounds)
        {
            return;
        }

        _controlledPosition.x = Mathf.Clamp(_controlledPosition.x, MovementBoundsMin.x, MovementBoundsMax.x);
        _controlledPosition.y = Mathf.Clamp(_controlledPosition.y, MovementBoundsMin.y, MovementBoundsMax.y);
        _controlledPosition.z = Mathf.Clamp(_controlledPosition.z, MovementBoundsMin.z, MovementBoundsMax.z);
    }
}

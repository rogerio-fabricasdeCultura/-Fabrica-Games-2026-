using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Follows the player fish from behind and keeps it framed like a simple third-person game.
/// </summary>
[ExecuteAlways]
[DefaultExecutionOrder(200)]
public class FishThirdPersonCamera : MonoBehaviour
{
    public float CurrentZoomDistance =>
        Application.isPlaying && EnableZoom && _zoomDistance > 0f
            ? _zoomDistance
            : DefaultZoomDistance;

    public float DefaultZoomDistance => Mathf.Clamp(
        FollowOffset.magnitude,
        Mathf.Max(0.1f, MinZoomDistance),
        Mathf.Max(Mathf.Max(0.1f, MinZoomDistance), MaxZoomDistance));

    [Header("Target")]
    public Transform Target;

    [Header("Follow")]
    public Vector3 FollowOffset = new Vector3(0f, 2.5f, -8f);
    [Min(0.01f)] public float FollowSmoothTime = 0.04f;
    [Min(0f)] public float RotationSmoothSpeed = 30f;
    [Tooltip("Use zero to keep the fish root centered on screen.")]
    public float LookHeight;

    [Header("Mouse Wheel Zoom")]
    public bool EnableZoom = true;
    [Min(0.1f)] public float MinZoomDistance = 1f;
    [Min(0.1f)] public float MaxZoomDistance = 12f;
    [Tooltip("Distance changed per standard mouse wheel notch. Positive scroll zooms in.")]
    [Min(0.01f)] public float ZoomStep = 0.5f;

    private Vector3 _followVelocity;
    private Vector3 _smoothedOffset;
    private Quaternion _followRotation;
    private Transform _previousTarget;
    private float _zoomDistance;

    private void OnEnable()
    {
        ResolveTarget();
        UpdateEditorPose();
    }

    private void OnValidate()
    {
        FollowSmoothTime = Mathf.Max(0.01f, FollowSmoothTime);
        RotationSmoothSpeed = Mathf.Max(0f, RotationSmoothSpeed);
        MinZoomDistance = Mathf.Max(0.1f, MinZoomDistance);
        MaxZoomDistance = Mathf.Max(MinZoomDistance, MaxZoomDistance);
        ZoomStep = Mathf.Max(0.01f, ZoomStep);
    }

    private void Start()
    {
        ResolveTarget();

        if (Target != null)
        {
            SnapToTarget();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            ResolveTarget();
            UpdateEditorPose();
        }
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (Target == null)
        {
            ResolveTarget();
            if (Target == null)
            {
                return;
            }
        }

        if (_previousTarget != Target)
        {
            SnapToTarget();
        }

        UpdateZoom();

        float rotationStep = RotationSmoothSpeed <= 0f
            ? 1f
            : 1f - Mathf.Exp(-RotationSmoothSpeed * Time.deltaTime);
        _followRotation = Quaternion.Slerp(_followRotation, Target.rotation, rotationStep);

        // Smooth only the offset. Follow translation immediately so accelerating does not
        // make the camera lag behind the fish or change its apparent distance.
        _smoothedOffset = Vector3.SmoothDamp(
            _smoothedOffset,
            _followRotation * GetFollowOffset(),
            ref _followVelocity,
            Mathf.Max(0.01f, FollowSmoothTime));

        transform.position = Target.position + _smoothedOffset;
        // Aim directly at the fish every frame. Smoothing this rotation would move it
        // away from the center while the camera catches up with a mouse turn.
        transform.rotation = GetDesiredRotation();
    }

    private void UpdateEditorPose()
    {
        if (Application.isPlaying || Target == null)
        {
            return;
        }

        SnapToTarget();
    }

    private void SnapToTarget()
    {
        _previousTarget = Target;
        float minimumDistance = Mathf.Max(0.1f, MinZoomDistance);
        _zoomDistance = Mathf.Clamp(FollowOffset.magnitude, minimumDistance, Mathf.Max(minimumDistance, MaxZoomDistance));
        _followRotation = Target.rotation;
        _smoothedOffset = _followRotation * GetFollowOffset();
        _followVelocity = Vector3.zero;
        transform.position = GetDesiredPosition();
        transform.rotation = GetDesiredRotation();
    }

    private void ResolveTarget()
    {
        if (Target != null)
        {
            return;
        }

        PlayerFishController playerFishController = FindObjectOfType<PlayerFishController>();
        if (playerFishController != null)
        {
            Target = playerFishController.transform;
        }
    }

    private Vector3 GetDesiredPosition()
    {
        return Target.position + Target.rotation * GetFollowOffset();
    }

    private void UpdateZoom()
    {
        if (!EnableZoom || Mouse.current == null)
        {
            return;
        }

        float wheelDelta = Mouse.current.scroll.ReadValue().y;
        float minimumDistance = Mathf.Max(0.1f, MinZoomDistance);
        // Standard Windows wheel input is 120 units per notch. Do not multiply by deltaTime.
        _zoomDistance = Mathf.Clamp(
            _zoomDistance - wheelDelta / 120f * Mathf.Max(0.01f, ZoomStep),
            minimumDistance,
            Mathf.Max(minimumDistance, MaxZoomDistance));
    }

    private Vector3 GetFollowOffset()
    {
        if (!Application.isPlaying || !EnableZoom)
        {
            return FollowOffset;
        }

        Vector3 offsetDirection = FollowOffset.sqrMagnitude > 0.001f ? FollowOffset.normalized : Vector3.back;
        return offsetDirection * _zoomDistance;
    }

    private Quaternion GetDesiredRotation()
    {
        Vector3 lookTarget = Target.position + Vector3.up * LookHeight;
        Vector3 lookDirection = lookTarget - transform.position;

        if (lookDirection.sqrMagnitude <= 0.001f)
        {
            return transform.rotation;
        }

        return Quaternion.LookRotation(lookDirection.normalized, _followRotation * Vector3.up);
    }

    private void OnDrawGizmosSelected()
    {
        if (Target == null)
        {
            return;
        }

        Vector3 desiredPosition = GetDesiredPosition();
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(Target.position, desiredPosition);
        Gizmos.DrawWireSphere(desiredPosition, 0.2f);
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>Builds a circular, terrain-rendered minimap entirely at runtime.</summary>
[DefaultExecutionOrder(300)]
[DisallowMultipleComponent]
public class TerrainMinimapRuntime : MonoBehaviour
{
    private PlayerFishController _player;
    private Terrain _terrain;
    private FishThirdPersonCamera _fishCamera;
    private Camera _mapCamera;
    private GameObject _cameraObject;
    private Canvas _canvas;
    private RectTransform _markerTransform;
    private RectTransform _collectibleMarkerRoot;
    private RenderTexture _mapTexture;
    private Sprite _circleSprite;
    private Sprite _triangleSprite;
    private Texture2D _circleTexture;
    private Texture2D _triangleTexture;
    private float _diameter;
    private float _orthographicSize;
    private float _cameraHeight;
    private int _resolution;
    private bool _rotateWithPlayer;
    private bool _isInitialized;
    private float _nextCollectibleRefreshTime;
    private readonly Dictionary<PowerCollectible, Image> _collectibleMarkers =
        new Dictionary<PowerCollectible, Image>();

    public void Initialize(
        PlayerFishController player,
        Terrain terrain,
        int resolution,
        float diameter,
        float orthographicSize,
        float cameraHeight,
        bool rotateWithPlayer)
    {
        if (_isInitialized || player == null || terrain == null || terrain.terrainData == null)
        {
            return;
        }

        _player = player;
        _terrain = terrain;
        _fishCamera = player.GameCamera != null
            ? player.GameCamera.GetComponent<FishThirdPersonCamera>()
            : null;
        if (_fishCamera == null)
        {
            FishThirdPersonCamera[] fishCameras = FindObjectsOfType<FishThirdPersonCamera>();
            for (int cameraIndex = 0; cameraIndex < fishCameras.Length; cameraIndex++)
            {
                if (fishCameras[cameraIndex].Target == player.transform)
                {
                    _fishCamera = fishCameras[cameraIndex];
                    break;
                }
            }
        }
        _resolution = Mathf.Clamp(resolution, 128, 1024);
        _diameter = Mathf.Max(120f, diameter);
        _orthographicSize = Mathf.Max(5f, orthographicSize);
        _cameraHeight = Mathf.Max(10f, cameraHeight);
        _rotateWithPlayer = rotateWithPlayer;

        CreateMapCamera();
        CreateMapInterface();
        _isInitialized = true;
        UpdateMapCamera();
    }

    private void LateUpdate()
    {
        if (_isInitialized)
        {
            UpdateMapCamera();
            UpdateCollectibleMarkerPositions();
            if (Time.unscaledTime >= _nextCollectibleRefreshTime)
            {
                RefreshCollectibleMarkers();
                _nextCollectibleRefreshTime = Time.unscaledTime + 0.5f;
            }
        }
    }

    private void CreateMapCamera()
    {
        _mapTexture = new RenderTexture(_resolution, _resolution, 16, RenderTextureFormat.ARGB32)
        {
            name = "Terrain Minimap Render Texture",
            filterMode = FilterMode.Bilinear,
            useMipMap = false,
            autoGenerateMips = false,
            antiAliasing = 4
        };
        _mapTexture.Create();

        _cameraObject = new GameObject("Terrain Minimap Camera");
        _mapCamera = _cameraObject.AddComponent<Camera>();
        _mapCamera.orthographic = true;
        _mapCamera.orthographicSize = _orthographicSize;
        _mapCamera.aspect = 1f;
        _mapCamera.clearFlags = CameraClearFlags.SolidColor;
        _mapCamera.backgroundColor = new Color(0.025f, 0.09f, 0.12f, 1f);
        _mapCamera.nearClipPlane = 0.1f;
        _mapCamera.farClipPlane = Mathf.Max(500f, _terrain.terrainData.size.y + _cameraHeight + 50f);
        _mapCamera.depth = -10f;
        _mapCamera.allowHDR = false;
        _mapCamera.allowMSAA = true;
        _mapCamera.useOcclusionCulling = false;
        _mapCamera.targetTexture = _mapTexture;

        UniversalAdditionalCameraData additionalCameraData =
            _cameraObject.AddComponent<UniversalAdditionalCameraData>();
        additionalCameraData.renderPostProcessing = false;
        additionalCameraData.renderShadows = false;
        additionalCameraData.volumeLayerMask = 0;
    }

    private void CreateMapInterface()
    {
        _circleTexture = CreateCircleTexture(512);
        _triangleTexture = CreateTriangleTexture(64);
        _circleSprite = Sprite.Create(
            _circleTexture,
            new Rect(0f, 0f, _circleTexture.width, _circleTexture.height),
            new Vector2(0.5f, 0.5f),
            _circleTexture.width);
        _triangleSprite = Sprite.Create(
            _triangleTexture,
            new Rect(0f, 0f, _triangleTexture.width, _triangleTexture.height),
            new Vector2(0.5f, 0.5f),
            _triangleTexture.width);

        GameObject canvasObject = new GameObject(
            "Terrain Minimap Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        _canvas = canvasObject.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Image frame = CreateImage("Minimap Frame", canvasObject.transform, _circleSprite);
        frame.color = new Color(0.015f, 0.035f, 0.045f, 0.94f);
        RectTransform frameTransform = frame.rectTransform;
        frameTransform.anchorMin = Vector2.one;
        frameTransform.anchorMax = Vector2.one;
        frameTransform.pivot = Vector2.one;
        frameTransform.anchoredPosition = new Vector2(-26f, -26f);
        frameTransform.sizeDelta = new Vector2(_diameter, _diameter);

        GameObject maskObject = new GameObject(
            "Minimap Circular Viewport",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Mask));
        maskObject.transform.SetParent(frame.transform, false);

        RectTransform maskTransform = maskObject.GetComponent<RectTransform>();
        maskTransform.anchorMin = new Vector2(0.5f, 0.5f);
        maskTransform.anchorMax = new Vector2(0.5f, 0.5f);
        maskTransform.pivot = new Vector2(0.5f, 0.5f);
        maskTransform.anchoredPosition = Vector2.zero;
        maskTransform.sizeDelta = new Vector2(_diameter - 14f, _diameter - 14f);

        Image maskImage = maskObject.GetComponent<Image>();
        maskImage.sprite = _circleSprite;
        maskImage.color = Color.white;
        maskImage.raycastTarget = false;
        maskObject.GetComponent<Mask>().showMaskGraphic = false;

        GameObject mapObject = new GameObject(
            "Terrain Map Image",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(RawImage));
        mapObject.transform.SetParent(maskObject.transform, false);
        RectTransform mapTransform = mapObject.GetComponent<RectTransform>();
        mapTransform.anchorMin = Vector2.zero;
        mapTransform.anchorMax = Vector2.one;
        mapTransform.offsetMin = Vector2.zero;
        mapTransform.offsetMax = Vector2.zero;

        RawImage mapImage = mapObject.GetComponent<RawImage>();
        mapImage.texture = _mapTexture;
        mapImage.raycastTarget = false;
        if (SystemInfo.graphicsUVStartsAtTop)
        {
            mapImage.uvRect = new Rect(0f, 1f, 1f, -1f);
        }

        GameObject collectibleMarkerRootObject = new GameObject("Collectible Markers", typeof(RectTransform));
        collectibleMarkerRootObject.transform.SetParent(maskObject.transform, false);
        _collectibleMarkerRoot = collectibleMarkerRootObject.GetComponent<RectTransform>();
        _collectibleMarkerRoot.anchorMin = Vector2.zero;
        _collectibleMarkerRoot.anchorMax = Vector2.one;
        _collectibleMarkerRoot.offsetMin = Vector2.zero;
        _collectibleMarkerRoot.offsetMax = Vector2.zero;

        Sprite playerIcon = _player.MinimapPlayerIcon != null ? _player.MinimapPlayerIcon : _triangleSprite;
        if (_player.MinimapPlayerIcon == null)
        {
            CreatePlayerMarker(maskObject.transform, 30f, 38f, new Color(0.02f, 0.07f, 0.08f, 1f));
        }

        Image playerMarker = CreateImage("Player Direction Marker", maskObject.transform, playerIcon);
        playerMarker.color = new Color(0.95f, 1f, 0.95f, 1f);
        playerMarker.raycastTarget = false;
        _markerTransform = playerMarker.rectTransform;
        _markerTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _markerTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _markerTransform.pivot = new Vector2(0.5f, 0.5f);
        _markerTransform.anchoredPosition = Vector2.zero;
        float markerSize = Mathf.Max(8f, _player.MinimapIconSize);
        _markerTransform.sizeDelta = new Vector2(markerSize, markerSize);
    }

    private void RefreshCollectibleMarkers()
    {
        PowerCollectible[] collectibles = FindObjectsOfType<PowerCollectible>();
        HashSet<PowerCollectible> activeCollectibles = new HashSet<PowerCollectible>();

        for (int collectibleIndex = 0; collectibleIndex < collectibles.Length; collectibleIndex++)
        {
            PowerCollectible collectible = collectibles[collectibleIndex];
            if (collectible == null || !collectible.isActiveAndEnabled)
            {
                continue;
            }

            activeCollectibles.Add(collectible);
            if (!_collectibleMarkers.TryGetValue(collectible, out Image marker))
            {
                marker = CreateImage(
                    collectible.GetDisplayLabel() + " Minimap Marker",
                    _collectibleMarkerRoot,
                    GetCollectibleIcon(collectible.Type));
                marker.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                marker.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                marker.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                marker.raycastTarget = false;
                _collectibleMarkers.Add(collectible, marker);
            }

            UpdateCollectibleMarkerAppearance(collectible, marker);
        }

        List<PowerCollectible> removedCollectibles = new List<PowerCollectible>();
        foreach (KeyValuePair<PowerCollectible, Image> markerEntry in _collectibleMarkers)
        {
            if (markerEntry.Key == null || !activeCollectibles.Contains(markerEntry.Key))
            {
                if (markerEntry.Value != null)
                {
                    Destroy(markerEntry.Value.gameObject);
                }

                removedCollectibles.Add(markerEntry.Key);
            }
        }

        for (int removedIndex = 0; removedIndex < removedCollectibles.Count; removedIndex++)
        {
            _collectibleMarkers.Remove(removedCollectibles[removedIndex]);
        }
    }

    private void UpdateCollectibleMarkerAppearance(PowerCollectible collectible, Image marker)
    {
        Sprite collectibleIcon = GetCollectibleIcon(collectible.Type);
        marker.sprite = collectibleIcon != null ? collectibleIcon : _circleSprite;
        marker.color = GetCollectibleIcon(collectible.Type) != null
            ? Color.white
            : GetCollectibleFallbackColor(collectible.Type);

        float iconSize = Mathf.Max(8f, _player.MinimapCollectibleIconSize);
        marker.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
    }

    private Sprite GetCollectibleIcon(CollectibleType collectibleType)
    {
        switch (collectibleType)
        {
            case CollectibleType.Size:
                return _player.MinimapArmorCollectibleIcon;
            case CollectibleType.SuperStrength:
                return _player.MinimapPowerCollectibleIcon;
            default:
                return _player.MinimapSpeedCollectibleIcon;
        }
    }

    private Color GetCollectibleFallbackColor(CollectibleType collectibleType)
    {
        switch (collectibleType)
        {
            case CollectibleType.Size:
                return new Color(0.72f, 0.88f, 1f, 1f);
            case CollectibleType.SuperStrength:
                return new Color(1f, 0.58f, 0.12f, 1f);
            default:
                return new Color(0.12f, 0.92f, 1f, 1f);
        }
    }

    private void UpdateCollectibleMarkerPositions()
    {
        if (_collectibleMarkerRoot == null || _cameraObject == null || _mapCamera == null)
        {
            return;
        }

        float mapRadiusPixels = Mathf.Max(
            0f,
            (_diameter - 14f) * 0.5f - Mathf.Max(8f, _player.MinimapCollectibleIconSize) * 0.5f);
        float mapRadiusWorld = _mapCamera.orthographicSize;

        foreach (KeyValuePair<PowerCollectible, Image> markerEntry in _collectibleMarkers)
        {
            PowerCollectible collectible = markerEntry.Key;
            Image marker = markerEntry.Value;
            if (collectible == null || marker == null)
            {
                continue;
            }

            Vector3 offset = collectible.transform.position - _player.transform.position;
            Vector2 mapOffset = new Vector2(
                Vector3.Dot(offset, _cameraObject.transform.right),
                Vector3.Dot(offset, _cameraObject.transform.up));
            Vector2 markerPosition = mapOffset / Mathf.Max(1f, mapRadiusWorld) * mapRadiusPixels;
            marker.rectTransform.anchoredPosition = Vector2.ClampMagnitude(markerPosition, mapRadiusPixels);
        }
    }

    private void CreatePlayerMarker(Transform parent, float width, float height, Color color)
    {
        Image markerOutline = CreateImage("Player Marker Outline", parent, _triangleSprite);
        markerOutline.color = color;
        markerOutline.raycastTarget = false;
        RectTransform markerTransform = markerOutline.rectTransform;
        markerTransform.anchorMin = new Vector2(0.5f, 0.5f);
        markerTransform.anchorMax = new Vector2(0.5f, 0.5f);
        markerTransform.pivot = new Vector2(0.5f, 0.5f);
        markerTransform.anchoredPosition = Vector2.zero;
        markerTransform.sizeDelta = new Vector2(width, height);
    }

    private Image CreateImage(string objectName, Transform parent, Sprite sprite)
    {
        GameObject imageObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        return image;
    }

    private void UpdateMapCamera()
    {
        if (_player == null || _terrain == null || _mapCamera == null)
        {
            return;
        }

        Vector3 terrainPosition = _terrain.transform.position;
        Vector3 terrainSize = _terrain.terrainData.size;
        float cameraY = terrainPosition.y + terrainSize.y + _cameraHeight;
        Vector3 playerPosition = _player.transform.position;
        _cameraObject.transform.position = new Vector3(playerPosition.x, cameraY, playerPosition.z);

        Vector3 playerHeading = Vector3.ProjectOnPlane(_player.MovementDirection, Vector3.up);
        if (playerHeading.sqrMagnitude < 0.0001f)
        {
            playerHeading = Vector3.forward;
        }

        playerHeading.Normalize();
        Vector3 mapUp = _rotateWithPlayer ? playerHeading : Vector3.forward;
        _cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.down, mapUp);
        float cameraZoomRatio = _fishCamera != null
            ? _fishCamera.CurrentZoomDistance / _fishCamera.DefaultZoomDistance
            : 1f;
        float zoomInfluence = Mathf.Clamp01(_player.MinimapZoomFollowMultiplier);
        float zoomScale = Mathf.Lerp(1f, cameraZoomRatio, zoomInfluence);
        if (zoomScale > 1f)
        {
            zoomScale = Mathf.Min(zoomScale, Mathf.Max(1f, _player.MinimapMaximumZoomOutMultiplier));
        }

        _mapCamera.orthographicSize = Mathf.Max(1f, _orthographicSize * zoomScale);
        float playerYaw = Vector3.SignedAngle(Vector3.forward, playerHeading, Vector3.up);
        _markerTransform.localEulerAngles = new Vector3(0f, 0f, _rotateWithPlayer ? 0f : -playerYaw);
    }

    private Texture2D CreateCircleTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Minimap Circular Mask",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color32[] pixels = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float radius = center - 1.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float coverage = Mathf.Clamp01(radius + 0.5f - distance);
                byte alpha = (byte)Mathf.RoundToInt(coverage * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private Texture2D CreateTriangleTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Minimap Player Arrow",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            float verticalPosition = (float)y / (size - 1);
            float halfWidth = 0.38f * (1f - verticalPosition);
            for (int x = 0; x < size; x++)
            {
                float horizontalPosition = (float)x / (size - 1);
                bool inside = verticalPosition >= 0.08f && verticalPosition <= 0.94f &&
                    Mathf.Abs(horizontalPosition - 0.5f) <= halfWidth;
                pixels[y * size + x] = inside
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(255, 255, 255, 0);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void OnDestroy()
    {
        if (_cameraObject != null)
        {
            Destroy(_cameraObject);
        }

        if (_canvas != null)
        {
            Destroy(_canvas.gameObject);
        }

        if (_mapTexture != null)
        {
            _mapTexture.Release();
            Destroy(_mapTexture);
        }

        if (_circleSprite != null)
        {
            Destroy(_circleSprite);
        }

        if (_triangleSprite != null)
        {
            Destroy(_triangleSprite);
        }

        if (_circleTexture != null)
        {
            Destroy(_circleTexture);
        }

        if (_triangleTexture != null)
        {
            Destroy(_triangleTexture);
        }
    }
}

using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerFishController))]
[CanEditMultipleObjects]
public class PlayerFishControllerEditor : Editor
{
    private const int PreviewResolution = 256;
    private Camera _previewCamera;
    private RenderTexture _previewTexture;
    private GameObject _previewCameraObject;
    private double _nextPreviewRefresh;

    private void OnEnable()
    {
        EditorApplication.update += RefreshPreview;
    }

    private void OnDisable()
    {
        EditorApplication.update -= RefreshPreview;
        ReleasePreview();
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Minimap Preview", EditorStyles.boldLabel);

        PlayerFishController player = (PlayerFishController)target;
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null || terrain.terrainData == null)
        {
            EditorGUILayout.HelpBox("A cena precisa conter um Terrain para exibir a prévia do minimapa.", MessageType.Info);
            return;
        }

        Rect previewRect = GUILayoutUtility.GetAspectRect(1f, GUILayout.MaxWidth(256f));
        if (Event.current.type == EventType.Repaint)
        {
            RenderPreview(player, terrain);
            GUI.DrawTexture(previewRect, _previewTexture, ScaleMode.ScaleToFit, false);
            DrawPlayerIcon(previewRect, player.MinimapPlayerIcon, player.MinimapIconSize, player);
            Handles.BeginGUI();
            Handles.color = new Color(0.1f, 0.9f, 1f, 0.9f);
            Handles.DrawAAPolyLine(2f,
                new Vector3(previewRect.xMin, previewRect.yMin),
                new Vector3(previewRect.xMax, previewRect.yMin),
                new Vector3(previewRect.xMax, previewRect.yMax),
                new Vector3(previewRect.xMin, previewRect.yMax),
                new Vector3(previewRect.xMin, previewRect.yMin));
            Handles.EndGUI();
        }

        EditorGUILayout.HelpBox("Prévia superior do Terrain. O minimapa do jogo usa recorte circular.", MessageType.None);
    }

    private void RefreshPreview()
    {
        if (this == null || !target || EditorApplication.timeSinceStartup < _nextPreviewRefresh)
        {
            return;
        }

        _nextPreviewRefresh = EditorApplication.timeSinceStartup + 0.15d;
        Repaint();
    }

    private void RenderPreview(PlayerFishController player, Terrain terrain)
    {
        EnsurePreviewResources();
        FishThirdPersonCamera fishCamera = player.GameCamera != null
            ? player.GameCamera.GetComponent<FishThirdPersonCamera>()
            : null;
        float cameraZoomRatio = Application.isPlaying && fishCamera != null
            ? fishCamera.CurrentZoomDistance / fishCamera.DefaultZoomDistance
            : 1f;
        float zoomScale = Mathf.Lerp(1f, cameraZoomRatio, Mathf.Clamp01(player.MinimapZoomFollowMultiplier));
        if (zoomScale > 1f)
        {
            zoomScale = Mathf.Min(zoomScale, Mathf.Max(1f, player.MinimapMaximumZoomOutMultiplier));
        }
        float orthographicSize = Mathf.Max(1f, player.MinimapOrthographicSize * zoomScale);
        Vector3 terrainPosition = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size;
        Vector3 playerPosition = player.transform.position;
        float cameraY = terrainPosition.y + terrainSize.y + Mathf.Max(10f, player.MinimapCameraHeight);

        _previewCamera.orthographicSize = orthographicSize;
        _previewCamera.farClipPlane = Mathf.Max(500f, terrainSize.y + player.MinimapCameraHeight + 50f);
        _previewCamera.cullingMask = 1 << terrain.gameObject.layer;
        _previewCameraObject.transform.position = new Vector3(playerPosition.x, cameraY, playerPosition.z);
        Vector3 playerHeading = Vector3.ProjectOnPlane(player.MovementDirection, Vector3.up);
        if (playerHeading.sqrMagnitude < 0.0001f)
        {
            playerHeading = Vector3.forward;
        }

        playerHeading.Normalize();
        _previewCameraObject.transform.rotation = player.MinimapRotatesWithFish
            ? Quaternion.LookRotation(Vector3.down, playerHeading)
            : Quaternion.LookRotation(Vector3.down, Vector3.forward);
        _previewCamera.Render();
    }

    private void EnsurePreviewResources()
    {
        if (_previewCamera != null)
        {
            return;
        }

        _previewTexture = new RenderTexture(PreviewResolution, PreviewResolution, 16, RenderTextureFormat.ARGB32)
        {
            name = "Minimap Inspector Preview",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear
        };
        _previewTexture.Create();

        _previewCameraObject = new GameObject("Minimap Inspector Preview Camera")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        _previewCamera = _previewCameraObject.AddComponent<Camera>();
        _previewCamera.enabled = false;
        _previewCamera.orthographic = true;
        _previewCamera.aspect = 1f;
        _previewCamera.clearFlags = CameraClearFlags.SolidColor;
        _previewCamera.backgroundColor = new Color(0.025f, 0.09f, 0.12f, 1f);
        _previewCamera.nearClipPlane = 0.1f;
        _previewCamera.allowHDR = false;
        _previewCamera.allowMSAA = false;
        _previewCamera.useOcclusionCulling = false;
        _previewCamera.targetTexture = _previewTexture;
    }

    private void DrawPlayerIcon(Rect previewRect, Sprite icon, float iconSize, PlayerFishController player)
    {
        float size = Mathf.Clamp(iconSize, 8f, 64f) * previewRect.width / Mathf.Max(120f, player.MinimapDiameter);
        Rect iconRect = new Rect(previewRect.center.x - size * 0.5f, previewRect.center.y - size * 0.5f, size, size);
        Vector3 playerHeading = Vector3.ProjectOnPlane(player.MovementDirection, Vector3.up);
        if (playerHeading.sqrMagnitude < 0.0001f)
        {
            playerHeading = Vector3.forward;
        }

        float iconRotation = player.MinimapRotatesWithFish
            ? 0f
            : -Vector3.SignedAngle(Vector3.forward, playerHeading.normalized, Vector3.up);
        GUIUtility.RotateAroundPivot(iconRotation, iconRect.center);
        if (icon != null && icon.texture != null)
        {
            GUI.DrawTexture(iconRect, icon.texture, ScaleMode.ScaleToFit, true);
            GUIUtility.RotateAroundPivot(-iconRotation, iconRect.center);
            return;
        }

        Vector3 tip = new Vector3(iconRect.center.x, iconRect.yMin, 0f);
        Vector3 left = new Vector3(iconRect.xMin, iconRect.yMax, 0f);
        Vector3 right = new Vector3(iconRect.xMax, iconRect.yMax, 0f);
        Handles.BeginGUI();
        Handles.color = new Color(0.95f, 1f, 0.95f, 1f);
        Handles.DrawAAConvexPolygon(tip, right, left);
        Handles.EndGUI();
        GUIUtility.RotateAroundPivot(-iconRotation, iconRect.center);
    }

    private void ReleasePreview()
    {
        if (_previewCameraObject != null)
        {
            DestroyImmediate(_previewCameraObject);
        }

        if (_previewTexture != null)
        {
            _previewTexture.Release();
            DestroyImmediate(_previewTexture);
        }
    }
}

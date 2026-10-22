using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Camera-scoped underwater fog and URP color grading, including Game view edit-mode preview.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera), typeof(Volume), typeof(UniversalAdditionalCameraData))]
public class UnderwaterCameraEffect : MonoBehaviour
{
    [Header("Water Surface")]
    [Tooltip("Keep enabled while the entire game takes place underwater.")]
    public bool AlwaysUnderwater = true;
    [Tooltip("Optional: the water plane transform. Used when Always Underwater is disabled.")]
    public Transform WaterSurface;
    public float WaterLevel = 4f;
    [Min(0.01f)] public float TransitionDepth = 0.5f;

    public float SurfaceHeight => WaterSurface != null ? WaterSurface.position.y : WaterLevel;

    [Header("Underwater Fog")]
    public Color WaterColor = new Color(0.055f, 0.25f, 0.29f, 1f);
    [Tooltip("Approximate distance, in world units, where the scene fades into the water color.")]
    [Min(1f)] public float VisibilityDistance = 30f;

    [Header("Color And Edges")]
    public Color ColorTint = new Color(0.78f, 0.95f, 0.91f, 1f);
    [Range(-100f, 100f)] public float Saturation = -12f;
    [Range(-100f, 100f)] public float Contrast = 5f;
    [Range(-2f, 2f)] public float Exposure = -0.15f;
    [Range(0f, 1f)] public float VignetteIntensity = 0.18f;

    private Camera _camera;
    private UniversalAdditionalCameraData _cameraData;
    private Volume _volume;
    private VolumeProfile _profile;
    private ColorAdjustments _colorAdjustments;
    private Vignette _vignette;
    private bool _savedPostProcessing;
    private LayerMask _savedVolumeLayerMask;
    private bool _renderStateActive;
    private bool _savedFog;
    private FogMode _savedFogMode;
    private Color _savedFogColor;
    private float _savedFogDensity;
    private Color _savedBackgroundColor;
    private CameraClearFlags _savedClearFlags;

    private void OnEnable()
    {
        if (!gameObject.scene.IsValid())
        {
            return;
        }

        _camera = GetComponent<Camera>();
        _cameraData = GetComponent<UniversalAdditionalCameraData>();
        _volume = GetComponent<Volume>();
        _savedPostProcessing = _cameraData.renderPostProcessing;
        _savedVolumeLayerMask = _cameraData.volumeLayerMask;
        _cameraData.renderPostProcessing = true;
        _cameraData.volumeLayerMask = _savedVolumeLayerMask.value | (1 << gameObject.layer);

        // This temporary profile belongs only to this camera; Inspector values are the source of truth.
        _profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _profile.hideFlags = HideFlags.HideAndDontSave;
        _colorAdjustments = _profile.Add<ColorAdjustments>(true);
        _vignette = _profile.Add<Vignette>(true);
        _colorAdjustments.hideFlags = HideFlags.HideAndDontSave;
        _vignette.hideFlags = HideFlags.HideAndDontSave;
        _volume.profile = _profile;
        _volume.isGlobal = true;
        _volume.priority = 100f;
        _volume.weight = 0f;

        RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        RenderPipelineManager.endCameraRendering += EndCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
        RenderPipelineManager.endCameraRendering -= EndCameraRendering;
        RestoreRenderState();

        if (_cameraData != null)
        {
            _cameraData.renderPostProcessing = _savedPostProcessing;
            _cameraData.volumeLayerMask = _savedVolumeLayerMask;
        }

        if (_volume != null)
        {
            _volume.weight = 0f;
            _volume.profile = null;
        }

        if (_profile != null)
        {
            foreach (VolumeComponent component in _profile.components)
            {
                ReleaseObject(component);
            }

            ReleaseObject(_profile);
            _profile = null;
        }
    }

    private void BeginCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
    {
        if (renderingCamera != _camera || _profile == null || _renderStateActive)
        {
            return;
        }

        float surfaceHeight = SurfaceHeight;
        float strength = AlwaysUnderwater
            ? 1f
            : Mathf.Clamp01((surfaceHeight - _camera.transform.position.y) / Mathf.Max(0.01f, TransitionDepth));
        if (strength <= 0f)
        {
            return;
        }

        _savedFog = RenderSettings.fog;
        _savedFogMode = RenderSettings.fogMode;
        _savedFogColor = RenderSettings.fogColor;
        _savedFogDensity = RenderSettings.fogDensity;
        _savedBackgroundColor = _camera.backgroundColor;
        _savedClearFlags = _camera.clearFlags;
        _renderStateActive = true;

        _colorAdjustments.colorFilter.value = ColorTint;
        _colorAdjustments.saturation.value = Saturation;
        _colorAdjustments.contrast.value = Contrast;
        _colorAdjustments.postExposure.value = Exposure;
        _vignette.color.value = new Color(0.01f, 0.08f, 0.09f, 1f);
        _vignette.intensity.value = VignetteIntensity;
        _vignette.smoothness.value = 0.65f;
        _volume.weight = strength;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = WaterColor;
        RenderSettings.fogDensity = 1.75f / Mathf.Max(1f, VisibilityDistance) * strength;
        _camera.backgroundColor = Color.Lerp(_savedBackgroundColor, WaterColor, strength);
        _camera.clearFlags = CameraClearFlags.SolidColor;
    }

    private void EndCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
    {
        if (renderingCamera == _camera)
        {
            RestoreRenderState();
        }
    }

    private void RestoreRenderState()
    {
        if (!_renderStateActive)
        {
            return;
        }

        RenderSettings.fog = _savedFog;
        RenderSettings.fogMode = _savedFogMode;
        RenderSettings.fogColor = _savedFogColor;
        RenderSettings.fogDensity = _savedFogDensity;
        if (_camera != null)
        {
            _camera.backgroundColor = _savedBackgroundColor;
            _camera.clearFlags = _savedClearFlags;
        }

        if (_volume != null)
        {
            _volume.weight = 0f;
        }

        _renderStateActive = false;
    }

    private static void ReleaseObject(Object value)
    {
        if (Application.isPlaying)
        {
            Destroy(value);
        }
        else
        {
            DestroyImmediate(value);
        }
    }
}

using TMPro;
using UnityEngine;

/// <summary>Animates an existing TextMeshPro label child; it never creates or instantiates objects.</summary>
[DisallowMultipleComponent]
public class CollectibleLabelVfx : MonoBehaviour
{
    public TMP_Text Label;
    [Min(0f)] public float FloatAmplitude = 0.12f;
    [Min(0.1f)] public float FloatFrequency = 1.8f;
    [Min(0f)] public float PulseAmount = 0.06f;
    public bool FaceCamera = true;

    private Transform _labelTransform;
    private Vector3 _baseLocalPosition;
    private Vector3 _baseLocalScale;
    private Camera _mainCamera;
    private float _phaseOffset;

    private void Awake()
    {
        if (Label == null)
        {
            Label = GetComponent<TMP_Text>();
        }

        PowerCollectible collectible = GetComponentInParent<PowerCollectible>();
        if (Label == null || collectible == null)
        {
            enabled = false;
            return;
        }

        _labelTransform = Label.transform;
        _baseLocalPosition = _labelTransform.localPosition;
        _baseLocalScale = _labelTransform.localScale;
        _mainCamera = Camera.main;
        _phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        RefreshLabel(collectible);
    }

    public void RefreshLabel(PowerCollectible collectible)
    {
        if (Label == null)
        {
            Label = GetComponent<TMP_Text>();
        }

        if (Label == null || collectible == null)
        {
            return;
        }

        Label.text = collectible.GetDisplayLabel();
        Label.color = GetLabelColor(collectible.Type);
    }

    private void LateUpdate()
    {
        if (_labelTransform == null)
        {
            return;
        }

        float phase = Time.time * FloatFrequency + _phaseOffset;
        _labelTransform.localPosition = _baseLocalPosition + Vector3.up * Mathf.Sin(phase) * FloatAmplitude;
        float pulse = 1f + Mathf.Sin(phase) * PulseAmount;
        _labelTransform.localScale = _baseLocalScale * pulse;

        if (!FaceCamera)
        {
            return;
        }

        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }

        if (_mainCamera != null)
        {
            Vector3 directionToCamera = _mainCamera.transform.position - _labelTransform.position;
            if (directionToCamera.sqrMagnitude > 0.001f)
            {
                _labelTransform.rotation = Quaternion.LookRotation(-directionToCamera, _mainCamera.transform.up);
            }
        }
    }

    private Color GetLabelColor(CollectibleType collectibleType)
    {
        switch (collectibleType)
        {
            case CollectibleType.Size:
                return new Color(0.4f, 0.9f, 1f, 1f);
            case CollectibleType.SuperStrength:
                return new Color(1f, 0.72f, 0.18f, 1f);
            default:
                return new Color(0.15f, 0.95f, 1f, 1f);
        }
    }
}

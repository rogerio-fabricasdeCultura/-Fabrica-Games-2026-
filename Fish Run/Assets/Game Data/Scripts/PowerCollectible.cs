using UnityEngine;

public enum CollectibleType
{
    Speed,
    Size,
    SuperStrength
}

[System.Serializable]
public class CollectibleEffectSettings
{
    [Min(0.1f)] public float Duration = 10f;
    [Min(1f)] public float SpeedMultiplier = 2f;
    [Min(1f)] public float SizeMultiplier = 1.75f;
    [Min(1f)] public float SuperStrengthSizeMultiplier = 1.5f;
    public Material StoneMaterial;
    public Material SpeedTrailMaterial;
    public Material ShieldMaterial;
    [Min(0.1f)] public float ShieldScale = 0.65f;
    [Min(0.1f)] public float SuperStrengthPulseSpeed = 3f;
    [Min(0f)] public float SuperStrengthEmissionStrength = 2f;
    public Color SuperStrengthPulseColor = new Color(1f, 0.8f, 0.12f, 1f);
    [Min(0.05f)] public float SpeedTrailDuration = 0.6f;
    [Min(0f)] public float SpeedTrailWidth = 0.3f;
    public Color SpeedTrailColor = new Color(0.1f, 0.85f, 1f, 0.85f);
}

/// <summary>Attach to a collectible object to grant its configured temporary effect to the fish.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public class PowerCollectible : MonoBehaviour
{
    [Header("Collectible Type")]
    public CollectibleType Type = CollectibleType.Speed;
    [Tooltip("Optional label override. Leave empty to use Speed, Armor or Power based on Type.")]
    public string LabelText;

    [Header("Effect Settings")]
    public CollectibleEffectSettings EffectSettings = new CollectibleEffectSettings();

    private SphereCollider _trigger;
    private bool _collected;

    private void Awake()
    {
        _trigger = GetComponent<SphereCollider>();
        _trigger.isTrigger = true;

        // The fish uses a CharacterController; the collectible supplies the trigger's body.
        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

    }

    private void OnValidate()
    {
        CollectibleLabelVfx[] labelVfxComponents = GetComponentsInChildren<CollectibleLabelVfx>(true);
        foreach (CollectibleLabelVfx labelVfx in labelVfxComponents)
        {
            labelVfx.RefreshLabel(this);
        }
    }

    public string GetDisplayLabel()
    {
        if (!string.IsNullOrWhiteSpace(LabelText))
        {
            return LabelText;
        }

        switch (Type)
        {
            case CollectibleType.Size:
                return "Armor";
            case CollectibleType.SuperStrength:
                return "Power";
            default:
                return "Speed";
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_collected)
        {
            return;
        }

        PlayerFishController player = other.GetComponentInParent<PlayerFishController>();
        if (player == null)
        {
            return;
        }

        // Claim before invoking player events to prevent duplicate collection.
        _collected = true;
        if (!player.TryActivatePower(Type, EffectSettings))
        {
            _collected = false;
            return;
        }

        _trigger.enabled = false;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}

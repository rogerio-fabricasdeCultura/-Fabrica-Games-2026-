using UnityEngine;

/// <summary>Attach to a manually created collectible. Grants the fish a temporary power state.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public class PowerCollectible : MonoBehaviour
{
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
        if (!player.TryActivatePower())
        {
            _collected = false;
            return;
        }

        _trigger.enabled = false;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}

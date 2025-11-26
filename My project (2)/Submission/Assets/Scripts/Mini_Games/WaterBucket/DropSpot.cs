using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Detects when the player enters the spot and whether the player is carrying a bucket.
/// Works with a PlayerCarry component (recommended), or falls back to checking for a child/tagged bucket on the player transform.
/// </summary>
public class DropSpot : MonoBehaviour
{
    [Tooltip("Tag used to identify the player GameObject")]
    public string playerTag = "Player";

    [Tooltip("Tag used to identify bucket objects (if using child-tag fallback)")]
    public string bucketTag = "Bucket";

    [Header("Events")]
    public UnityEvent OnPlayerEnterSpot;          // player entered the spot (any)
    public UnityEvent OnPlayerExitSpot;           // player left the spot
    public UnityEvent OnPlayerWithBucketEnter;    // player entered while carrying bucket
    public UnityEvent OnPlayerWithBucketExit;     // player left or dropped bucket

    // runtime
    public bool PlayerInside { get; private set; } = false;
    public GameObject PlayerObject { get; private set; } = null;

    // true when the player is both inside and carrying a bucket
    public bool IsPlayerWithBucket { get; private set; } = false;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col == null) gameObject.AddComponent<BoxCollider>().isTrigger = true;
        else col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            PlayerInside = true;
            PlayerObject = other.gameObject;
            OnPlayerEnterSpot?.Invoke();
            EvaluatePlayerCarryState();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            PlayerInside = false;
            PlayerObject = null;
            // if player left the spot, clear carrying state
            if (IsPlayerWithBucket)
            {
                IsPlayerWithBucket = false;
                OnPlayerWithBucketExit?.Invoke();
            }
            OnPlayerExitSpot?.Invoke();
        }
    }

    /// <summary>
    /// Call this when the player's carried object changes (for example, when pickup/drop happens)
    /// If you use the PlayerCarry component below, it will call this automatically.
    /// </summary>
    public void EvaluatePlayerCarryState()
    {
        bool hasBucket = false;

        if (!PlayerInside || PlayerObject == null)
        {
            hasBucket = false;
        }
        else
        {
            // 1) Preferred: check PlayerCarry component
            var carry = PlayerObject.GetComponent<PlayerCarry>();
            if (carry != null)
            {
                hasBucket = carry.IsCarrying && carry.CarriedObject != null && carry.CarriedObject.CompareTag(bucketTag);
            }
            else
            {
                // 2) Fallback: scan children for bucket-tagged object (common if bucket is parented to player when carried)
                for (int i = 0; i < PlayerObject.transform.childCount; i++)
                {
                    var c = PlayerObject.transform.GetChild(i);
                    if (c.CompareTag(bucketTag))
                    {
                        hasBucket = true;
                        break;
                    }
                }

                // 3) Final fallback: find any bucket within a small radius around the player's position (in case carry uses physics)
                if (!hasBucket)
                {
                    Collider[] hits = Physics.OverlapSphere(PlayerObject.transform.position, 0.6f);
                    foreach (var h in hits)
                    {
                        if (h.CompareTag(bucketTag))
                        {
                            // ensure the bucket is effectively "attached" to player (distance very small)
                            if (Vector3.Distance(h.transform.position, PlayerObject.transform.position) < 0.8f)
                            {
                                hasBucket = true;
                                break;
                            }
                        }
                    }
                }
            }
        }

        // dispatch enter/exit events for the "player-with-bucket" state
        if (hasBucket && !IsPlayerWithBucket)
        {
            IsPlayerWithBucket = true;
            OnPlayerWithBucketEnter?.Invoke();
        }
        else if (!hasBucket && IsPlayerWithBucket)
        {
            IsPlayerWithBucket = false;
            OnPlayerWithBucketExit?.Invoke();
        }
    }

    // public helper: return the actual carried bucket GameObject, if any
    public GameObject GetCarriedBucket()
    {
        if (!PlayerInside || PlayerObject == null) return null;

        var carry = PlayerObject.GetComponent<PlayerCarry>();
        if (carry != null && carry.IsCarrying && carry.CarriedObject != null && carry.CarriedObject.CompareTag(bucketTag))
            return carry.CarriedObject;

        // fallback: check children
        for (int i = 0; i < PlayerObject.transform.childCount; i++)
        {
            var c = PlayerObject.transform.GetChild(i);
            if (c.CompareTag(bucketTag)) return c.gameObject;
        }

        // last fallback: overlap near player
        Collider[] hits = Physics.OverlapSphere(PlayerObject.transform.position, 0.6f);
        foreach (var h in hits) if (h.CompareTag(bucketTag)) return h.gameObject;

        return null;
    }

    // optional editor gizmo
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        var col = GetComponent<Collider>();
        if (col != null) Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        else Gizmos.DrawWireSphere(transform.position, 0.5f);
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class BucketInteract : MonoBehaviour
{
    [Header("Interaction")]
    public float interactionRange = 3f;
    public KeyCode interactionKey = KeyCode.E;
    public KeyCode dropnKey = KeyCode.Q;

    [Header("Carry placement")]
    public Transform carryAnchor;      // optional: parent here while carrying (recommended)
    public float carryForward = 1.0f;  // used if no carryAnchor
    public float carryUp = -0.5f;
    public float carrySmooth = 20f;

    [Header("Drop physics")]
    public float dropForwardForce = 1f;
    public float dropUpOffset = 0.2f;        // raise bucket slightly when dropped to avoid floor clipping
    public float dropNudgeStep = 0.15f;      // how far to nudge if overlapping
    public int dropNudgeAttempts = 6;        // attempts to find a clean spot

    [Header("Collision Strategy (choose one)")]
    public bool useTriggerWhileCarried = true;  // set colliders to isTrigger while carried (default).
    public bool useIgnoreCollisionWhileCarried = false; // alternatively ignore collisions with player colliders.

    [Header("References")]
    public PlayerManager playerInt;  // assign or auto-find
    public WaterBucket waterBucket;  // assign or auto-find

    // internals
    private Transform player;
    private Rigidbody bucketRb;
    private Rigidbody playerRb;
    private Collider[] bucketColliders;
    private Collider[] playerColliders;
    private bool isCarried = false;
    private List<(Collider a, Collider b)> ignoredPairs = new List<(Collider, Collider)>();

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player == null) Debug.LogWarning("BucketInteract: Player not found (tag 'Player').");

        if (playerInt == null && player != null)
            playerInt = player.GetComponent<PlayerManager>();
        if (playerInt == null)
            Debug.LogWarning("BucketInteract: PlayerManager not assigned and not found on Player.");

        if (waterBucket == null)
            waterBucket = GetComponentInChildren<WaterBucket>() ?? GetComponent<WaterBucket>();
        if (waterBucket == null)
            Debug.LogError("BucketInteract: WaterBucket not found on this GameObject.");

        bucketRb = GetComponent<Rigidbody>() ?? GetComponentInChildren<Rigidbody>();
        if (bucketRb == null)
            Debug.LogWarning("BucketInteract: No Rigidbody found on bucket. Some physics behavior may be disabled.");

        playerRb = player != null ? player.GetComponent<Rigidbody>() : null;

        bucketColliders = GetComponentsInChildren<Collider>(includeInactive: true);
        if (player != null)
            playerColliders = player.GetComponentsInChildren<Collider>(includeInactive: true);
    }

    void Update()
    {
        if (player == null || playerInt == null || waterBucket == null) return;

        if (playerInt.equip == "Bucket")
            HandleDropInput();
        else
            HandlePickupInput();

        if (isCarried)
        {
            if (carryAnchor != null)
            {
                // parent to anchor to avoid phasing while carrying
                if (transform.parent != carryAnchor)
                    transform.SetParent(carryAnchor, worldPositionStays: false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
            else
            {
                UpdateCarryPositionInFrontOfPlayer();
            }
        }
    }

    private void HandlePickupInput()
    {
        float distance = Vector3.Distance(player.position, transform.position);
        if (distance <= interactionRange && Input.GetKeyDown(interactionKey))
        {
            Pickup();
        }
    }

    private void HandleDropInput()
    {
        if (Input.GetKeyDown(dropnKey))
        {
            // drop request: run safe drop coroutine
            StartCoroutine(SafeDropSequence());
        }
    }

    private void Pickup()
    {
        if (playerInt == null || waterBucket == null) return;

        playerInt.equip = "Bucket";

        // zero velocities and make kinematic (if present)
        if (bucketRb != null)
        {
            bucketRb.linearVelocity = Vector3.zero;
            bucketRb.angularVelocity = Vector3.zero;
            bucketRb.isKinematic = true;
        }

        // collision strategy while carried
        if (useTriggerWhileCarried)
        {
            foreach (var col in bucketColliders)
            {
                if (col != null) col.isTrigger = true;
            }
        }
        else if (useIgnoreCollisionWhileCarried && playerColliders != null)
        {
            ignoredPairs.Clear();
            foreach (var bc in bucketColliders)
            {
                if (bc == null) continue;
                foreach (var pc in playerColliders)
                {
                    if (pc == null) continue;
                    Physics.IgnoreCollision(bc, pc, true);
                    ignoredPairs.Add((bc, pc));
                }
            }
        }

        // parent to carryAnchor if assigned, else move to in-front position
        if (carryAnchor != null)
        {
            transform.SetParent(carryAnchor, worldPositionStays: false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
        else
        {
            Vector3 desired = player.position + player.forward * carryForward + Vector3.up * carryUp;
            transform.position = desired;
        }

        waterBucket.StartCarry(playerRb);
        isCarried = true;
    }

   
    private IEnumerator SafeDropSequence()
    {
        if (!isCarried) yield break; // not carrying

        // pre-step: clear equip right away so player can act
        if (playerInt != null)
            playerInt.equip = null;

        // ensure we have a player transform
        if (player == null)
        {
            // fallback: simple Drop()
            DropImmediate();
            yield break;
        }

        // Unparent but keep kinematic/colliders as-is (they're currently triggers or ignored)
        if (transform.parent == carryAnchor)
            transform.SetParent(null, worldPositionStays: true);

        // compute base drop position: in front of player's origin, slightly up
        Vector3 baseDrop = player.position + player.forward * carryForward + Vector3.up * dropUpOffset;

        // attempt to find a non-overlapping position:
        Vector3 tryPos = baseDrop;
        bool foundClear = false;

        for (int i = 0; i < dropNudgeAttempts; i++)
        {
            // move bucket to attempted position while still non-colliding
            transform.position = tryPos;

            // check for overlaps with player colliders (or any non-trigger overlap)
            bool overlaps = false;
            foreach (var bc in bucketColliders)
            {
                if (bc == null) continue;

                // use the collider's bounds center & extents for an overlap test (approx)
                Bounds b = bc.bounds;
                Collider[] hits = Physics.OverlapBox(b.center, b.extents * 0.98f, transform.rotation, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                foreach (var h in hits)
                {
                    // if the hit is part of the player, count as overlap
                    if (player != null && h.transform.IsChildOf(player))
                    {
                        overlaps = true;
                        break;
                    }
                    // also count heavy overlaps with floor/walls as overlaps to nudge up
                    if (!h.transform.IsChildOf(transform) && !h.isTrigger)
                    {
                        // ignore the bucket's own colliders - but any other collider is a potential overlap
                        overlaps = true;
                        break;
                    }
                }
                if (overlaps) break;
            }

            if (!overlaps)
            {
                foundClear = true;
                break;
            }

            // nudge forward then up for next attempt
            tryPos += (player.forward * dropNudgeStep) + (Vector3.up * (dropNudgeStep * 0.25f));
        }

        // if we didn't find a clear spot after attempts, we will still drop at the last tryPos (best-effort)
        transform.position = tryPos;

        // small orientation: face same forward as player but keep upright
        Vector3 lookDir = player.forward;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);

        // wait for fixed update to let physics step align
        yield return new WaitForFixedUpdate();

        // restore collision state: disable triggers or restore ignored collisions
        if (useTriggerWhileCarried)
        {
            foreach (var col in bucketColliders)
            {
                if (col != null) col.isTrigger = false;
            }
        }
        else if (useIgnoreCollisionWhileCarried)
        {
            foreach (var pair in ignoredPairs)
            {
                if (pair.a != null && pair.b != null)
                    Physics.IgnoreCollision(pair.a, pair.b, false);
            }
            ignoredPairs.Clear();
        }

        // re-enable dynamic physics
        if (bucketRb != null)
        {
            bucketRb.isKinematic = false;

            // apply a small forward push so it doesn't interpenetrate
            Vector3 forward = player.forward;
            bucketRb.AddForce(forward * dropForwardForce, ForceMode.VelocityChange);
        }

        // stop carry on the water bucket logic
        waterBucket.StopCarry();
        isCarried = false;
    }

    // Fallback immediate drop if something is very wrong with the safe flow
    private void DropImmediate()
    {
        if (playerInt != null) playerInt.equip = null;

        if (transform.parent == carryAnchor)
            transform.SetParent(null, worldPositionStays: true);

        if (useTriggerWhileCarried)
        {
            foreach (var col in bucketColliders)
                if (col != null) col.isTrigger = false;
        }
        else if (useIgnoreCollisionWhileCarried)
        {
            foreach (var pair in ignoredPairs)
                if (pair.a != null && pair.b != null)
                    Physics.IgnoreCollision(pair.a, pair.b, false);
            ignoredPairs.Clear();
        }

        if (bucketRb != null)
        {
            bucketRb.isKinematic = false;
            Vector3 forward = player != null ? player.forward : Vector3.forward;
            bucketRb.AddForce(forward * dropForwardForce, ForceMode.VelocityChange);
        }

        waterBucket.StopCarry();
        isCarried = false;
    }

    private void UpdateCarryPositionInFrontOfPlayer()
    {
        Vector3 desired = player.position + player.forward * carryForward + Vector3.up * carryUp;
        transform.position = Vector3.Lerp(transform.position, desired, Mathf.Clamp01(Time.deltaTime * carrySmooth));

        Vector3 lookDir = player.forward;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Mathf.Clamp01(Time.deltaTime * carrySmooth));
        }
    }

    void OnDrawGizmosSelected()
    {
        if (player != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }
}

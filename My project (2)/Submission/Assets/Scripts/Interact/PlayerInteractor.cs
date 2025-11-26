using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple player-side interactor:
/// - Raycasts (or spherecast) forward to detect FoodItemInteractive
/// - Shows an optional UI prompt when targetable
/// - Press E to consume (calls FoodItemInteractive.InteractEat())
/// 
/// Attach to the player GameObject (same object as PlayerManager).
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [Header("Interaction")]
    [Tooltip("Maximum distance to interact.")]
    public float interactRange = 2.0f;

    [Tooltip("Radius for sphere check to be more forgiving than a ray.")]
    public float interactRadius = 0.4f;

    [Tooltip("Layer mask for interactable objects (set to include the layer your food prefabs use).")]
    public LayerMask interactLayers = ~0; // default: everything

    [Header("UI")]
    [Tooltip("Optional UI Text element to show 'Press E to eat'")]
    public Text interactPromptText;

    [Tooltip("Local camera used for aiming (optional). If null, uses main camera.")]
    public Camera playerCamera;

    private FoodItemInteractive currentTarget;

    private void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
        if (interactPromptText != null)
            interactPromptText.gameObject.SetActive(false);
    }

    private void Update()
    {
        UpdateTarget();

        if (currentTarget != null)
        {
            if (interactPromptText != null)
            {
                interactPromptText.gameObject.SetActive(true);
                interactPromptText.text = "Press E to eat";
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                currentTarget.InteractEat();
                // after eating, clear the target so prompt disappears until we find another
                currentTarget = null;
                if (interactPromptText != null) interactPromptText.gameObject.SetActive(false);
            }
        }
        else
        {
            if (interactPromptText != null && interactPromptText.gameObject.activeSelf)
                interactPromptText.gameObject.SetActive(false);
        }
    }

    private void UpdateTarget()
    {
        currentTarget = null;

        // prefer camera-based aiming if available
        if (playerCamera != null)
        {
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            RaycastHit hit;
            // Do a spherecast forward to be more forgiving than a single ray
            if (Physics.SphereCast(ray, interactRadius, out hit, interactRange, interactLayers, QueryTriggerInteraction.Collide))
            {
                var fi = hit.collider.GetComponentInParent<FoodItemInteractive>();
                if (fi != null)
                {
                    currentTarget = fi;
                    return;
                }
            }
        }

        // fallback: find nearest FoodItemInteractive within interactRange (distance)
        Collider[] cols = Physics.OverlapSphere(transform.position, interactRange, interactLayers, QueryTriggerInteraction.Collide);
        float nearest = float.MaxValue;
        FoodItemInteractive nearestFI = null;
        foreach (var c in cols)
        {
            var fi = c.GetComponentInParent<FoodItemInteractive>();
            if (fi == null) continue;
            float d = Vector3.Distance(transform.position, fi.transform.position);
            if (d < nearest)
            {
                nearest = d;
                nearestFI = fi;
            }
        }

        currentTarget = nearestFI;
    }

    // Optional: draw debug sphere in editor
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}

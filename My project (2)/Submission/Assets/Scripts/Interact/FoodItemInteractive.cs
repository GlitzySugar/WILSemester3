using UnityEngine;

/// <summary>
/// Food that can be interacted with (press E) to consume immediately.
/// Attach to your food prefab. Requires a Collider (not necessarily isTrigger).
/// </summary>
[RequireComponent(typeof(Collider))]
public class FoodItemInteractive : MonoBehaviour
{
    [Tooltip("How many seconds of hunger this food restores.")]
    public float hungerRestoreSeconds = 60f;

    [Tooltip("Destroy the gameobject when eaten.")]
    public bool destroyOnEat = true;

    [Tooltip("Optional: a small audio to play when eaten.")]
    public AudioClip eatSound;

    /// <summary>
    /// Called by PlayerInteractor when player presses E while looking at / near this food.
    /// </summary>
    public void InteractEat()
    {
        // Apply to starvation system
        if (StarvationSystem.Instance != null)
        {
            // Build a tiny FoodItem-like object to pass, or call AddTime directly
            StarvationSystem.Instance.AddTime(hungerRestoreSeconds);

            if (eatSound != null)
                AudioSource.PlayClipAtPoint(eatSound, transform.position);

            // Optionally spawn particles here

            if (destroyOnEat)
                Destroy(gameObject);
        }
        else
        {
            Debug.LogWarning("FoodItemInteractive: StarvationSystem.Instance is null; cannot apply hunger restore.");
        }
    }

    // Optional: draw a gizmo in editor to make interact radius easier to see
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }
}

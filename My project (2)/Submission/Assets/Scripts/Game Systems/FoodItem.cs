// FoodItem.cs
using UnityEngine;

/// <summary>
/// Simple food pickup / consumable. Call Eat() to consume and restore hunger.
/// If used as a world pickup, attach a collider with isTrigger = true and call Eat() on player interaction.
/// </summary>
public class FoodItem : MonoBehaviour
{
    [Tooltip("Seconds of hunger this food restores.")]
    public float hungerRestoreSeconds = 60f;

    [Tooltip("Destroy the world object when eaten.")]
    public bool destroyOnEat = true;

    /// <summary>Consume this food and add hunger seconds via StarvationSystem.</summary>
    public void Eat()
    {
        if (StarvationSystem.Instance != null)
            StarvationSystem.Instance.EatFood(this);

        // optional sound/particle could go here

        if (destroyOnEat)
            Destroy(gameObject);
    }

    // Example auto-add to player inventory on trigger — comment out if you don't want automatic pickup
    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponent<PlayerManager>();
        if (player != null)
        {
            StarvationSystem.Instance.EatFood(this);
            Destroy(gameObject);
        }
    }
}

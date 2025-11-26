using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A return point (trigger) that accepts a bucket deposit and notifies a manager.
/// Either auto-deposits when the bucket enters the trigger, or requires pressing interactionKey while inside (configurable).
/// </summary>
[RequireComponent(typeof(Collider))]
public class WaterReturnPoint : MonoBehaviour
{
    [Header("Deposit Settings")]
    public bool requirePressToDeposit = false;
    public KeyCode interactionKey = KeyCode.E;

    [Header("Events")]
    public UnityEvent<float> onDeposit; // amount delivered

    private void Reset()
    {
        Collider c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        WaterBucket wb = other.GetComponentInParent<WaterBucket>();
        if (wb != null && !requirePressToDeposit)
        {
            Deposit(wb);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!requirePressToDeposit) return;

        WaterBucket wb = other.GetComponentInParent<WaterBucket>();
        if (wb != null && Input.GetKeyDown(interactionKey))
        {
            Deposit(wb);
        }
    }

    private void Deposit(WaterBucket wb)
    {
        if (wb == null) return;

        float amount = wb.GetCurrentWater();

        // empty the bucket
        wb.ReduceWater(amount);

        onDeposit?.Invoke(amount);
    }
}

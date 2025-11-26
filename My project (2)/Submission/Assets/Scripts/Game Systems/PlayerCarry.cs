using UnityEngine;

public class PlayerCarry : MonoBehaviour
{
    public Transform carryHoldPoint;
    public KeyCode dropKey = KeyCode.Q;

    public bool IsCarrying { get; private set; } = false;
    public GameObject CarriedObject { get; private set; }

    private void Update()
    {
        // Drop object with Q
        if (IsCarrying && Input.GetKeyDown(dropKey))
        {
            Drop();
        }
    }

    public void PickUp(GameObject obj)
    {
        if (obj == null) return;

        CarriedObject = obj;

        // parent to hold point
        if (carryHoldPoint != null)
        {
            obj.transform.SetParent(carryHoldPoint, true);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
        }
        else
        {
            obj.transform.SetParent(transform, true);
        }

        // Stop physics
        var rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        IsCarrying = true;
        NotifyDropSpots();
    }

    public void Drop()
    {
        if (!IsCarrying || CarriedObject == null) return;

        GameObject dropped = CarriedObject;

        // Detach
        dropped.transform.SetParent(null, true);

        // Re-enable physics
        var rb = dropped.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;

        // Slight downward push to make drop feel natural
        dropped.transform.position += Vector3.down * 0.1f;

        IsCarrying = false;
        CarriedObject = null;

        NotifyDropSpots();
    }

    // Inform any DropSpot nearby that carry status changed
    private void NotifyDropSpots()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 1f);
        foreach (var h in hits)
        {
            var ds = h.GetComponent<DropSpot>();
            if (ds != null) ds.EvaluatePlayerCarryState();
        }
    }
}

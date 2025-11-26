using UnityEngine;

public class Broom : MonoBehaviour
{
    public PlayerManager playerInt;
    public Transform holdPoint;

    public KeyCode interactKey = KeyCode.E;
    public KeyCode dropKey = KeyCode.Q;
    public float interactRange = 3f;

    private Transform player;
    private Rigidbody rb;
    private Collider col;
    private Collider[] playerColliders;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (playerInt == null && player != null)
            playerInt = player.GetComponent<PlayerManager>();

        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        if (player != null)
            playerColliders = player.GetComponentsInChildren<Collider>();
        else
            playerColliders = new Collider[0];
    }

    void Update()
    {
        if (player == null) return;

        float dist = Vector3.Distance(player.position, transform.position);

        // PICKUP
        if (Input.GetKeyDown(interactKey) && playerInt.equip != "Broom" && dist <= interactRange)
        {
            Pickup();
        }

        // DROP
        if (Input.GetKeyDown(dropKey) && playerInt.equip == "Broom")
        {
            Drop();
        }
    }

    void Pickup()
    {
        playerInt.equip = "Broom";

        rb.isKinematic = true;
        col.isTrigger = true;

        transform.SetParent(holdPoint, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        foreach (var pc in playerColliders)
            Physics.IgnoreCollision(col, pc, true);
    }

    void Drop()
    {
        playerInt.equip = null;

        transform.SetParent(null);
        rb.isKinematic = false;
        col.isTrigger = false;

        foreach (var pc in playerColliders)
            Physics.IgnoreCollision(col, pc, false);
    }
}

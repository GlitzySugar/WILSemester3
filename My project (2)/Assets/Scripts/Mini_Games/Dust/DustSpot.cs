using UnityEngine;

public class DustSpot : MonoBehaviour
{
    public KeyCode interactKey = KeyCode.E;

    private Transform player;
    private PlayerManager playerInt;

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player != null)
            playerInt = player.GetComponent<PlayerManager>();
    }

    private void OnTriggerStay(Collider other)
    {
        // Only interact with the Player
        if (!other.CompareTag("Player")) return;

        // Check if broom equipped
        if (playerInt == null || playerInt.equip != "Broom") return;

        // Check if player presses E
        if (Input.GetKey(interactKey))
        {
            Destroy(gameObject);
        }
    }
}

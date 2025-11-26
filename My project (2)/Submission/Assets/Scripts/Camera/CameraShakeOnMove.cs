using UnityEngine;

public class CameraShakeOnMove : MonoBehaviour
{
    public bool isDrowsy = false;
    public Transform player;            // Reference to player transform
    public float shakeAmount = 0.05f;   // How much shake/bob
    public float shakeSpeed = 10f;      // How fast the shake oscillates

    private Vector3 originalPos;

    void Start()
    {
        originalPos = transform.localPosition; // Store original local position
    }

    void Update()
    {
        if (isDrowsy)
        {
            shakeAmount = 0.0025f;
            shakeSpeed = 1f;
        }
        else
        {
            shakeAmount = 0.05f;
            shakeSpeed = 10f;
        }
        if (IsPlayerMoving())
        {
            float shakeX = Mathf.Sin(Time.time * shakeSpeed) * shakeAmount;
            float shakeY = Mathf.Cos(Time.time * shakeSpeed * 2) * shakeAmount;

            transform.localPosition = originalPos + new Vector3(shakeX, shakeY, 0);
        }
        else
        {
            transform.localPosition = originalPos;
        }
    }

    bool IsPlayerMoving()
    {
        // Simple check: if WASD or arrow keys pressed
        return Input.GetAxis("Horizontal") != 0 || Input.GetAxis("Vertical") != 0;
    }
}

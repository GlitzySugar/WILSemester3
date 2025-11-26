using UnityEngine;

public class MouseLock : MonoBehaviour
{
    public bool isDrowsy = false;
    public float mouseSensitivity;
    float tempSens = 100f;
    public Transform playerBody;

    private float xRotation = 0f;

    void Start()
    {
        tempSens = mouseSensitivity;
        Cursor.lockState = CursorLockMode.Locked; // Hide and lock cursor
    }

    void Update()
    {
        if (isDrowsy)
        {
            mouseSensitivity = tempSens / 2;
        }
        else
        {
            mouseSensitivity = tempSens;
        }

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f); // Prevent flipping

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f); // Look up/down
        playerBody.Rotate(Vector3.up * mouseX); // Rotate player left/right
    }
}

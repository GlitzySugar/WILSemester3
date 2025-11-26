using UnityEngine;

public class CameraSway : MonoBehaviour
{
    public bool isDrowsy = false;
    public float swayAmount = 0.05f;
    public float swaySpeed = 1f;

    private Vector3 originalPos;

    void Start()
    {
        originalPos = transform.localPosition;
    }

    void Update()
    {
        if (isDrowsy)
        {
            float swayX = Mathf.Sin(Time.time * swaySpeed) * swayAmount;
            float swayY = Mathf.Cos(Time.time * swaySpeed * 0.5f) * swayAmount;

            transform.localPosition = originalPos + new Vector3(swayX, swayY, 0);
        }
        else
        {
            transform.localPosition = originalPos;
        }
    }
}

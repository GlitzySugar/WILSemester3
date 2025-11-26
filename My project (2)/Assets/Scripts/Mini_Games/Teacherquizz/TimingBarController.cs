using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class TimingBarController : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform barFill; // moving handle inside container
    public RectTransform barContainer; // full width
    public Image greenZoneImage; // (optional) the green success zone overlay - purely decorative

    [Header("Sweep settings")]
    public float sweepSpeed = 600f; // pixels per second (tweak)
    public bool pingPong = true;

    [HideInInspector] public Action<int> onSelectionComplete;

    bool sweeping = false;
    float direction = 1f;
    Vector2 handleStart;
    float minX, maxX;
    bool waitingForInput = false;

    void Awake()
    {
        if (barFill == null) Debug.LogError("TimingBarController: assign barFill.");
        if (barContainer == null) Debug.LogError("TimingBarController: assign barContainer.");
        handleStart = barFill.anchoredPosition;
        minX = 0;
        maxX = barContainer.rect.width;
    }

    public void StartSweep()
    {
        StopAllCoroutines();
        sweeping = true;
        direction = 1f;
        StartCoroutine(SweepCoroutine());
    }

    public void StopSweep()
    {
        sweeping = false;
        StopAllCoroutines();
    }

    IEnumerator SweepCoroutine()
    {
        float pos = barFill.anchoredPosition.x;
        while (true)
        {
            // move pos
            pos += direction * sweepSpeed * Time.deltaTime;
            if (pos > maxX) { pos = maxX; if (pingPong) direction = -1f; else pos = minX; }
            if (pos < minX) { pos = minX; if (pingPong) direction = 1f; else pos = maxX; }

            barFill.anchoredPosition = new Vector2(pos, barFill.anchoredPosition.y);

            // input check
            if (Input.GetKeyDown(KeyCode.E))
            {
                int selected = ComputeSlotFromPosition(pos);
                onSelectionComplete?.Invoke(selected);
                yield break;
            }

            yield return null;
        }
    }

    int ComputeSlotFromPosition(float posX)
    {
        // split into 4 equal zones
        float width = Mathf.Max(1f, barContainer.rect.width);
        float zoneSize = width / 4f;
        int idx = Mathf.Clamp(Mathf.FloorToInt(posX / zoneSize), 0, 3);
        return idx;
    }
}

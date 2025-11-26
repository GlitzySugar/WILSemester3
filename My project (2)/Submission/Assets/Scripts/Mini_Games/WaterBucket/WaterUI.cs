using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class WaterUI : MonoBehaviour
{
    [Header("Bucket reference")]
    public WaterBucket bucket;

    [Header("UI Elements")]
    public GameObject uiRoot;         // parent panel of the UI (set active/inactive)
    public Image fillImage;           // set Image.Type = Filled in inspector
    public Text amountText;           // optional text "34 / 100"

    [Header("Bucket visual (in-world)")]
    public Transform waterVisual;     // object inside bucket representing water (scale Y)
    public float waterVisualMaxY = 1f;
    public float waterVisualMinY = 0.05f;

    private void Start()
    {
        if (bucket == null)
        {
            Debug.LogError("WaterUI: bucket not assigned.");
            enabled = false;
            return;
        }

        // subscribe
        bucket.onWaterChanged.AddListener(OnWaterChanged);
        bucket.onCarryStart.AddListener(ShowUI);
        bucket.onCarryEnd.AddListener(HideUI);

        // initial visibility
        if (!bucket.IsBeingCarried())
            HideUI();
        else
            ShowUI();

        // initial update
        OnWaterChanged(bucket.GetCurrentWater());
    }

    private void OnDestroy()
    {
        if (bucket != null)
        {
            bucket.onWaterChanged.RemoveListener(OnWaterChanged);
            bucket.onCarryStart.RemoveListener(ShowUI);
            bucket.onCarryEnd.RemoveListener(HideUI);
        }
    }

    private void OnWaterChanged(float currentWater)
    {
        float normalized = Mathf.Clamp01(currentWater / bucket.maxWater);

        if (fillImage != null)
            fillImage.fillAmount = normalized;

        if (amountText != null)
            amountText.text = $"{Mathf.RoundToInt(currentWater)} / {Mathf.RoundToInt(bucket.maxWater)}";

        if (waterVisual != null)
        {
            Vector3 s = waterVisual.localScale;
            float targetY = Mathf.Lerp(waterVisualMinY, waterVisualMaxY, normalized);
            s.y = targetY;
            waterVisual.localScale = s;
        }
    }

    // Show/hide helpers
    public void ShowUI()
    {
        if (uiRoot != null) uiRoot.SetActive(true);
    }

    public void HideUI()
    {
        if (uiRoot != null) uiRoot.SetActive(false);
    }
}

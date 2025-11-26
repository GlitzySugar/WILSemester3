using UnityEngine;
using UnityEngine.UI;
#if TMP_PRESENT
using TMPro;
#endif

public class JournalEntryUI : MonoBehaviour
{
#if TMP_PRESENT
    public TextMeshProUGUI headerText;
    public TextMeshProUGUI bodyText;
#else
    public Text headerText;
    public Text bodyText;
#endif

    public RectTransform bodyArea;

    public KeyCode toggleKey = KeyCode.E; // key to expand/collapse
    public bool startOpen = false;

    private bool isOpen = false;

    private bool isHovered = false; // detects mouse hover

    void Awake()
    {
        SetOpen(startOpen, true);
    }

    void Update()
    {
        // Only toggle if mouse is over this entry AND key pressed
        if (isHovered && Input.GetKeyDown(toggleKey))
        {
            Toggle();
        }
    }

    // Detect hover using Unity events
    public void OnPointerEnter()
    {
        isHovered = true;
    }

    public void OnPointerExit()
    {
        isHovered = false;
    }

    public void Setup(JournalEntry entry)
    {
        if (headerText != null)
            headerText.text = $"Day {entry.day} — {entry.taskName}";

        if (bodyText != null)
            bodyText.text = entry.ToString();

        SetOpen(startOpen, true);
    }

    private void Toggle()
    {
        SetOpen(!isOpen);
    }

    private void SetOpen(bool open, bool instant = false)
    {
        isOpen = open;

        if (bodyArea != null)
            bodyArea.gameObject.SetActive(open);

        // Force layout update so the item expands/collapses properly
        if (!instant)
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
    }
}

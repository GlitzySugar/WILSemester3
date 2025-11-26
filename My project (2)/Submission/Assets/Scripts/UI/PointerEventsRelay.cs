using UnityEngine;
using UnityEngine.EventSystems;

public class PointerEventsRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public JournalEntryUI entry;

    public void OnPointerEnter(PointerEventData eventData)
    {
        entry.OnPointerEnter();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        entry.OnPointerExit();
    }
}

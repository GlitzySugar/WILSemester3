using UnityEngine;

public class HotkeyDebug : MonoBehaviour
{
    void Update()
    {
        //var ui = FindObjectOfType<JournalUI>(); Debug.Log($"UI:{ui != null} rootAssigned:{ui?.journalRootPanel != null} rootActive:{ui?.journalRootPanel?.activeInHierarchy} contentParent:{ui?.contentParent != null} entryPrefab:{ui?.entryPrefab != null}");

        //if (Input.GetKeyDown(KeyCode.Space))
        //{
        //    Debug.Log("space pressed — testing journal...");
        //    FindObjectOfType<JournalUI>()?.Open();
        //}

        //if (Input.GetKeyDown(KeyCode.F7))
        //{
        //    Debug.Log("F7 pressed — testing starvation...");
        //    Debug.Log("Severity ➜ " + StarvationSystem.Instance?.GetSeverityString());
        //}
    }
}

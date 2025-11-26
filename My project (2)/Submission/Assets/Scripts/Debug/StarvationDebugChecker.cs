using UnityEngine;

public class StarvationDebugChecker : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("---- STARVATION DEBUG CHECK ----");

        // 1. Does the singleton exist?
        Debug.Log("StarvationSystem.Instance != null ➜ " + (StarvationSystem.Instance != null));

        // 2. Does FindObjectOfType detect it in the scene?
        var found = FindObjectOfType<StarvationSystem>();
        Debug.Log("FindObjectOfType<StarvationSystem>() != null ➜ " + (found != null));

        // 3. If found, print values
        if (found != null)
        {
            Debug.Log("Seconds Remaining ➜ " + found.GetSecondsRemaining());
            Debug.Log("Severity ➜ " + found.GetSeverityString());
        }
        else
        {
            Debug.Log("Severity ➜ (no StarvationSystem found)");
        }

        Debug.Log("--------------------------------");
    }
}

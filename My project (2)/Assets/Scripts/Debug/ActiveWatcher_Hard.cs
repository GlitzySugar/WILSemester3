using UnityEngine;
using System.Diagnostics;
using Debug = UnityEngine.Debug;


public class ActiveWatcher_Hard : MonoBehaviour
{
    bool lastSelf;
    bool lastHier;

    void Start()
    {
        lastSelf = gameObject.activeSelf;
        lastHier = gameObject.activeInHierarchy;
        Debug.Log($"[ActiveWatcher_Hard] START - '{name}' initial activeSelf={lastSelf} activeInHierarchy={lastHier} at frame {Time.frameCount} time {Time.realtimeSinceStartup:F3}s");
    }

    void Update()
    {
        // heartbeat so we know Update runs
        if (Time.frameCount % 300 == 0)
            Debug.Log($"[ActiveWatcher_Hard] heartbeat - '{name}' activeSelf={gameObject.activeSelf} activeInHierarchy={gameObject.activeInHierarchy} frame={Time.frameCount}");

        // track changes (optional)
        if (gameObject.activeSelf != lastSelf || gameObject.activeInHierarchy != lastHier)
        {
            Debug.Log($"[ActiveWatcher_Hard] CHANGE - '{name}' activeSelf: {lastSelf}->{gameObject.activeSelf} activeInHierarchy: {lastHier}->{gameObject.activeInHierarchy} at frame {Time.frameCount}");
            lastSelf = gameObject.activeSelf;
            lastHier = gameObject.activeInHierarchy;
        }
    }

    void OnDisable()
    {
        // log that OnDisable was invoked and capture a stack trace
        Debug.LogError($"[ActiveWatcher_Hard] OnDisable detected on '{name}' at frame {Time.frameCount} time {Time.realtimeSinceStartup:F3}s. activeSelf={gameObject.activeSelf} activeInHierarchy={gameObject.activeInHierarchy}");

        // Capture and print stack trace (helpful to see what code path led to disable)
        var st = new StackTrace(true);
        Debug.LogError("[ActiveWatcher_Hard] Stack trace in OnDisable:\n" + st.ToString());

#if UNITY_EDITOR
        // Pause editor so you can inspect hierarchy/inspector at the exact moment
        Debug.LogError("[ActiveWatcher_Hard] Debug.Break() called so you can inspect the editor. Check Console + Hierarchy now.");
        Debug.Break();
#endif
    }

    void OnEnable()
    {
        Debug.Log($"[ActiveWatcher_Hard] OnEnable called on '{name}' at frame {Time.frameCount}");
    }
}

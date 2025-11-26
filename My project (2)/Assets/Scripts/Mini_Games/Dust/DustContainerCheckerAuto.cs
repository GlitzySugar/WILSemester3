using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Attach to the parent GameObject that contains dust pieces as child GameObjects.
/// - autoPopulateAtStart: collects children under this transform at Start and treats each active child as a dust piece.
/// - Works with DustAuto component (preferred) which auto-registers and notifies on OnDestroy.
/// - If you don't use DustAuto, call NotifyDustRemoved() before you Destroy(aDust) in your sweep code.
/// </summary>
[DisallowMultipleComponent]
public class DustContainerCheckerAuto : MonoBehaviour
{
    [Header("Auto-collection")]
    [Tooltip("If true, the checker will gather all active direct children as dust pieces at Start.")]
    public bool autoPopulateAtStart = true;

    [Tooltip("If true, also search grandchildren recursively when populating.")]
    public bool recursiveSearch = false;

    [Header("Task / Journal")]
    [Tooltip("Task name used when recording to journal")]
    public string taskName = "SweepHouse";

    [Tooltip("If true, record a successful mini-game when all dust cleared.")]
    public bool recordAsSuccess = true;

    [Tooltip("If true, call DaySystem.OnMiniGameCompleted() when cleared.")]
    public bool notifyDaySystemOnComplete = true;

    [Header("Events")]
    public UnityEvent onAllCleared;

    // internal tracking of dust pieces (by instance id)
    HashSet<int> trackedDustInstanceIds = new HashSet<int>();

    // store references optionally (for debug/inspect)
    List<GameObject> trackedDustObjects = new List<GameObject>();

    bool completedAlready = false;

    void Start()
    {
        if (autoPopulateAtStart)
            PopulateChildrenAsDust();
        // safety initial check
        CheckAndHandleEmpty(false);
    }

    /// <summary>
    /// Collect all children (or grandchildren if recursiveSearch) as dust pieces.
    /// If a child has a DustAuto component it will also register itself there.
    /// </summary>
    public void PopulateChildrenAsDust()
    {
        trackedDustInstanceIds.Clear();
        trackedDustObjects.Clear();

        if (recursiveSearch)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(includeInactive: false))
            {
                if (t == this.transform) continue;
                RegisterDustInternal(t.gameObject);
            }
        }
        else
        {
            foreach (Transform t in transform)
            {
                if (t == null) continue;
                RegisterDustInternal(t.gameObject);
            }
        }

        Debug.Log($"[DustContainerCheckerAuto] Populated {trackedDustObjects.Count} dust pieces for '{name}'.");
    }

    // internal registration helper
    void RegisterDustInternal(GameObject go)
    {
        if (go == null) return;
        int id = go.GetInstanceID();
        if (!trackedDustInstanceIds.Contains(id))
        {
            trackedDustInstanceIds.Add(id);
            trackedDustObjects.Add(go);
        }
    }

    /// <summary>
    /// Public method for dust pieces or sweep code to register a piece dynamically (e.g. spawned at runtime).
    /// </summary>
    public void RegisterDust(GameObject dust)
    {
        if (dust == null) return;
        RegisterDustInternal(dust);
        Debug.Log($"[DustContainerCheckerAuto] Registered dust '{dust.name}' in '{name}'. Total now: {trackedDustObjects.Count}");
    }

    /// <summary>
    /// Call this when a dust piece is removed/swept (preferred: call from dust component before Destroy()).
    /// If you used DustAuto, it calls this automatically in OnDestroy().
    /// </summary>
    public void NotifyDustRemoved(GameObject dust)
    {
        if (completedAlready) return;

        if (dust == null)
        {
            // unknown object - try to recompute counts
            RemoveMissingOrNullTracked();
            CheckAndHandleEmpty(true);
            return;
        }

        int id = dust.GetInstanceID();
        if (trackedDustInstanceIds.Contains(id))
        {
            trackedDustInstanceIds.Remove(id);
            trackedDustObjects.RemoveAll(x => x == dust);
            Debug.Log($"[DustContainerCheckerAuto] Dust removed: {dust.name} ; remaining={trackedDustInstanceIds.Count}");
        }
        else
        {
            // dust wasn't in the tracked set (maybe spawned after Start) - attempt safe removal
            RemoveMissingOrNullTracked();
            Debug.Log($"[DustContainerCheckerAuto] NotifyDustRemoved called for untracked object '{dust.name}'. Remaining now={trackedDustInstanceIds.Count}");
        }

        CheckAndHandleEmpty(true);
    }

    /// <summary>
    /// Remove any entries whose objects are null or destroyed.
    /// Useful when dusts are destroyed but notify wasn't called.
    /// </summary>
    void RemoveMissingOrNullTracked()
    {
        var stillValid = new List<GameObject>();
        var ids = new HashSet<int>();
        foreach (var g in trackedDustObjects)
        {
            if (g != null)
            {
                stillValid.Add(g);
                ids.Add(g.GetInstanceID());
            }
        }
        trackedDustObjects = stillValid;
        trackedDustInstanceIds = ids;
    }

    /// <summary>
    /// Checks whether the container is empty and handles completion.
    /// </summary>
    public void CheckAndHandleEmpty(bool logIfNotEmpty = false)
    {
        if (completedAlready) return;

        RemoveMissingOrNullTracked();

        int remaining = trackedDustInstanceIds.Count;
        if (remaining == 0)
        {
            completedAlready = true;
            HandleAllCleared();
        }
        else if (logIfNotEmpty)
        {
            Debug.Log($"[DustContainerCheckerAuto] '{name}' not empty yet. Remaining: {remaining}");
        }
    }

    void HandleAllCleared()
    {
        Debug.Log($"[DustContainerCheckerAuto] All dust cleared on '{name}'. Recording mini-game completion.");

        try { onAllCleared?.Invoke(); } catch (Exception ex) { Debug.LogWarning("[DustContainerCheckerAuto] onAllCleared threw: " + ex); }

        try
        {
            if (recordAsSuccess && JournalManager.Instance != null)
            {
                JournalManager.Instance.RecordMiniGameResult(taskName, true);
                Debug.Log("[DustContainerCheckerAuto] Recorded JournalManager.RecordMiniGameResult.");
            }
            else if (recordAsSuccess)
            {
                Debug.LogWarning("[DustContainerCheckerAuto] JournalManager.Instance not found. Skipping journal record.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DustContainerCheckerAuto] Failed to record journal: " + ex);
        }

        try
        {
            if (notifyDaySystemOnComplete && DaySystem.Instance != null)
            {
                DaySystem.Instance.OnMiniGameCompleted();
                Debug.Log("[DustContainerCheckerAuto] Notified DaySystem.OnMiniGameCompleted().");
            }
            else if (notifyDaySystemOnComplete)
            {
                Debug.LogWarning("[DustContainerCheckerAuto] DaySystem.Instance not found.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[DustContainerCheckerAuto] Failed to notify DaySystem: " + ex);
        }
    }

    /// <summary>
    /// For debug / testing: clear tracked list and re-populate from children again
    /// </summary>
    [ContextMenu("Repopulate Dust from Children")]
    public void Repopulate()
    {
        PopulateChildrenAsDust();
    }

#if UNITY_EDITOR
    [ContextMenu("Force Check Dust Now")]
    void EditorForceCheck() { CheckAndHandleEmpty(true); }
#endif
}

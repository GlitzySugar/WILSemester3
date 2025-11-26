using System;
using System.Collections.Generic;
using UnityEngine;

public class MiniGameManager : MonoBehaviour
{
    public static MiniGameManager Instance { get; private set; }

    [Tooltip("Populate with GameObjects that have components implementing IMiniGame (or use MiniGameBase).")]
    public List<GameObject> miniGamePrefabs = new List<GameObject>();

    [Tooltip("If true, won't select the same mini game twice in the same day.")]
    public bool avoidRepeatWithinDay = true;

    [Header("Counters")]
    public int completedMiniGamesThisDay = 0;
    public int completedMiniGamesThisWeek = 0;

    // faster membership test
    private HashSet<string> miniGamesPlayedThisDay = new HashSet<string>(StringComparer.Ordinal);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        if (DaySystem.Instance != null)
            DaySystem.Instance.OnDayStateChanged.AddListener(OnDayStateChanged);
    }

    private void OnDisable()
    {
        if (DaySystem.Instance != null)
            DaySystem.Instance.OnDayStateChanged.RemoveListener(OnDayStateChanged);
    }

    private void OnDayStateChanged(DayState state, int dayIndex, int weekNumber)
    {
        // Reset at morning of a new day
        if (state == DayState.Morning)
        {
            miniGamesPlayedThisDay.Clear();
            completedMiniGamesThisDay = 0;
        }
    }

    /// <summary>
    /// Request starting a randomly-selected mini-game.
    /// </summary>
    public void StartRandomMiniGame()
    {
        var prefab = PickRandomMiniGamePrefab();
        if (prefab == null)
        {
            Debug.LogWarning("MiniGameManager: No mini-game available to start.");
            return;
        }

        GameObject go = Instantiate(prefab, transform);

        if (!go.TryGetComponent<IMiniGame>(out var mini))
        {
            Debug.LogError($"MiniGameManager: Instantiated object {go.name} does not implement IMiniGame. Destroying.");
            Destroy(go);
            return;
        }

        // determine stable id (use prefab name by default; implementer can set different id in result)
        string id = prefab.name;
        if (!string.IsNullOrEmpty(id))
            miniGamesPlayedThisDay.Add(id);

        // start it, pass a safe callback
        try
        {
            mini.StartMiniGame(result =>
            {
                // ensure result exists
                if (result == null) result = new MiniGameResult();

                // fill metadata where missing
                if (string.IsNullOrEmpty(result.miniGameId)) result.miniGameId = id;

                var ds = DaySystem.Instance;
                if (ds != null)
                {
                    result.dayIndex = ds.currentDayIndex;
                    result.weekNumber = ds.currentWeek;
                    result.dayState = ds.currentState.ToString();
                }
                result.timestamp = DateTime.UtcNow.ToString("o");

                // update counters
                completedMiniGamesThisDay++;
                completedMiniGamesThisWeek++;

                // persistence / journal
                if (JournalManager.Instance != null)
                {
                    JournalManager.Instance.RecordMiniGameResult(result);
                }
                else
                {
                    Debug.LogWarning("MiniGameManager: JournalManager.Instance is null; skipping recording.");
                }

                // notify DaySystem (if it exists)
                if (DaySystem.Instance != null)
                {
                    DaySystem.Instance.OnMiniGameCompleted();
                }

                // cleanup: implementer might already destroy their GameObject. Only destroy if it's still valid.
                if (go != null)
                {
                    Destroy(go);
                }
            });
        }
        catch (Exception ex)
        {
            Debug.LogError($"MiniGameManager: Exception when starting mini-game '{id}': {ex}");
            if (go != null) Destroy(go);
        }
    }

    private GameObject PickRandomMiniGamePrefab()
    {
        if (miniGamePrefabs == null || miniGamePrefabs.Count == 0) return null;

        var candidates = new List<GameObject>(capacity: miniGamePrefabs.Count);

        foreach (var p in miniGamePrefabs)
        {
            if (p == null) continue;
            if (!avoidRepeatWithinDay)
            {
                candidates.Add(p);
                continue;
            }

            // prefer prefab.name, fallback to instance id if name missing
            var id = p.name;
            if (!string.IsNullOrEmpty(id) && !miniGamesPlayedThisDay.Contains(id))
            {
                candidates.Add(p);
            }
        }

        if (candidates.Count == 0)
        {
            // all were played today; fallback to full list (but still filter nulls)
            foreach (var p in miniGamePrefabs) if (p != null) candidates.Add(p);
            if (candidates.Count == 0) return null;
        }

        int idx = UnityEngine.Random.Range(0, candidates.Count);
        return candidates[idx];
    }

    /// <summary>
    /// Call this if you need to reset weekly counters (e.g. after JournalManager.GenerateWeeklySummary).
    /// </summary>
    public void ResetWeeklyCounters()
    {
        completedMiniGamesThisWeek = 0;
    }
}

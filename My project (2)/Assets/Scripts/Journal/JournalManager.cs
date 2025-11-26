using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// JournalManager that SKIPS entries when the player is starving and instead tracks missed log counts.
/// - Resolves hunger at creation time from StarvationSystem (preferred)
/// - If hunger is Unknown due to init order, schedules a short retry to update it later
/// - Keeps missed-entry analytics for starvation skips
/// </summary>
public class JournalManager : MonoBehaviour
{
    public static JournalManager Instance { get; private set; }

    [Header("Optional UI (assign JournalUI in inspector)")]
    public JournalUI journalUI;

    [SerializeField] List<JournalEntry> entries = new List<JournalEntry>();

    // Day/week fallback
    int fallbackDay = 1;
    int fallbackWeek = 1;

    // Missed-entries analytics
    [Header("Analytics - missed journal writes due to starvation")]
    [Tooltip("Missed entries count for the current week")]
    public int missedEntriesThisWeek = 0;
    [Tooltip("Total missed entries across the session")]
    public int missedEntriesTotal = 0;

    // store missed counts by week for historical analytics (week -> missedCount)
    Dictionary<int, int> missedEntriesByWeek = new Dictionary<int, int>();

    // retry interval for late hunger resolution (seconds)
    const float HungerRetryDelay = 0.12f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    #region Public API

    /// <summary>
    /// Add a new entry. Day and week are optional - if <=0 will try to read DaySystem or fallback values.
    /// If the resolved hunger state is "Starving", the entry is NOT recorded; instead missed counters are incremented.
    /// </summary>
    public void AddEntry(string taskName, string status, string hungerLevel = null, int day = -1, int week = -1)
    {
        // Resolve hunger deterministically right now if not supplied
        string resolvedHunger = !string.IsNullOrEmpty(hungerLevel) ? hungerLevel : ResolvePlayerHungerStateStringImmediate();

        // SIMPLE DEDUPE: avoid adding the same entry repeatedly (consecutive duplicates)
        if (entries.Count > 0)
        {
            var last = entries[entries.Count - 1];
            if (last.taskName == taskName && last.status == status && last.hungerLevel == resolvedHunger)
            {
                Debug.Log("[Journal] Skipping duplicate consecutive entry for: " + taskName);
                return;
            }
        }

        // If starving, skip the entry entirely and increment missed counters (no "Missed" entry)
        if (!string.IsNullOrEmpty(resolvedHunger) && resolvedHunger.Equals("Starving", StringComparison.OrdinalIgnoreCase))
        {
            int wk = ResolveWeek(week);
            missedEntriesThisWeek++;
            missedEntriesTotal++;
            if (!missedEntriesByWeek.ContainsKey(wk)) missedEntriesByWeek[wk] = 0;
            missedEntriesByWeek[wk]++;

            Debug.Log($"[Journal] Skipped entry for '{taskName}' due to starvation. (week {wk})");
            return;
        }

        // Otherwise create the normal entry (store the resolved hunger string)
        int dayResolved = ResolveDay(day);
        int weekResolved = ResolveWeek(week);
        var e = new JournalEntry(dayResolved, weekResolved, taskName, status, resolvedHunger);
        entries.Add(e);

        Debug.Log($"[Journal] Added entry: task='{taskName}', status='{status}', hunger='{resolvedHunger}', day={dayResolved}, week={weekResolved}");

        // push to UI if available
        if (journalUI != null) journalUI.AddEntryToUI(e);
        else Debug.Log("[Journal] " + e.ToString());

        // If hunger was "Unknown", schedule a short retry since StarvationSystem may initialize later
        if (string.IsNullOrEmpty(resolvedHunger) || resolvedHunger.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            StartCoroutine(UpdateEntryHungerLaterCoroutine(e, HungerRetryDelay));
        }
    }

    /// <summary>
    /// Convenience: record mini-game boolean pass/fail.
    /// </summary>
    public void RecordMiniGameResult(string taskName, bool won, string hungerLevel = null, int day = -1, int week = -1)
    {
        string status = won ? "Task Successful" : "Task Failed";
        AddEntry(taskName, status, hungerLevel, day, week);
    }

    /// <summary>
    /// Reflective overload for object results that contain 'won' and optionally day/week.
    /// This will attempt to read common members by name.
    /// </summary>
    public void RecordMiniGameResult(string taskName, object resultObj, string hungerLevel = null, int day = -1, int week = -1)
    {
        bool won = false;
        int dayFromResult = day;
        int weekFromResult = week;

        if (resultObj != null)
        {
            var t = resultObj.GetType();

            var pf = t.GetProperty("won", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.IgnoreCase);
            if (pf != null) { try { object v = pf.GetValue(resultObj, null); if (v is bool b) won = b; } catch { } }
            else
            {
                var ff = t.GetField("won", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.IgnoreCase);
                if (ff != null) { try { object v = ff.GetValue(resultObj); if (v is bool b) won = b; } catch { } }
            }

            var dayProp = t.GetProperty("dayIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.IgnoreCase)
                       ?? t.GetProperty("day", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.IgnoreCase);
            if (dayProp != null) { try { object v = dayProp.GetValue(resultObj, null); if (v is int iv) dayFromResult = iv; } catch { } }

            var wkProp = t.GetProperty("weekNumber", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.IgnoreCase)
                       ?? t.GetProperty("week", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.IgnoreCase);
            if (wkProp != null) { try { object v = wkProp.GetValue(resultObj, null); if (v is int iv) weekFromResult = iv; } catch { } }
        }

        RecordMiniGameResult(taskName, won, hungerLevel, dayFromResult, weekFromResult);
    }

    /// <summary>
    /// Type-safe overload for MiniGameResult.
    /// </summary>
    public void RecordMiniGameResult(MiniGameResult result)
    {
        if (result == null)
        {
            Debug.LogWarning("JournalManager.RecordMiniGameResult called with null result.");
            return;
        }

        string taskName = !string.IsNullOrEmpty(result.miniGameId) ? result.miniGameId : "MiniGame";
        bool won = false;
        try { won = result.won; } catch { }
        int day = (result.dayIndex > 0) ? result.dayIndex : -1;
        int week = (result.weekNumber > 0) ? result.weekNumber : -1;

        // Let AddEntry handle starvation behavior by passing null hungerLevel; AddEntry will resolve it
        RecordMiniGameResult(taskName, won, null, day, week);
    }

    /// <summary>
    /// Generate a simple weekly summary for the supplied week number.
    /// Now includes missed entry counts (starvation skips).
    /// </summary>
    public void GenerateWeeklySummary(int weekNumber)
    {
        int week = ResolveWeek(weekNumber);

        // select entries from that week
        var weekEntries = entries.Where(e => e.weekNumber == week).ToList();

        int total = weekEntries.Count;
        int successes = weekEntries.Count(e => e.status != null && e.status.IndexOf("successful", StringComparison.OrdinalIgnoreCase) >= 0);
        int fails = total - successes;

        // per-task counts
        var perTask = weekEntries.GroupBy(e => e.taskName ?? "Unknown")
                                  .Select(g => new { Task = g.Key, Count = g.Count(), Success = g.Count(x => x.status != null && x.status.IndexOf("successful", StringComparison.OrdinalIgnoreCase) >= 0) })
                                  .OrderByDescending(x => x.Count)
                                  .ToList();

        int missedThisWeek = GetMissedEntriesForWeek(week);

        // build summary text
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"Weekly Summary — Week {week}");
        sb.AppendLine($"Total mini-games recorded: {total}");
        sb.AppendLine($"Successful: {successes}  Failed: {fails}");
        sb.AppendLine($"Missed journal entries due to starvation: {missedThisWeek}");
        sb.AppendLine("");
        sb.AppendLine("By task:");
        foreach (var t in perTask)
        {
            sb.AppendLine($"- {t.Task}: {t.Count} (successes: {t.Success})");
        }

        string summary = sb.ToString();

        // log and create a journal entry that contains the summary in status field (taskName = "Weekly Summary")
        Debug.Log("[Journal] " + summary);

        AddEntry("Weekly Summary", "Summary:\n" + (total > 0 ? $"{successes} success / {fails} fail" : "No entries"), "N/A", day: 0, week: week);
    }

    /// <summary>
    /// Expose entries (read-only)
    /// </summary>
    public IReadOnlyList<JournalEntry> GetEntries() => entries;

    #endregion

    #region Missed counters API

    public int GetMissedEntriesThisWeek() => missedEntriesThisWeek;
    public int GetMissedEntriesTotal() => missedEntriesTotal;
    public int GetMissedEntriesForWeek(int week)
    {
        int wk = ResolveWeek(week);
        if (missedEntriesByWeek.TryGetValue(wk, out int count)) return count;
        return 0;
    }

    /// <summary>
    /// Reset weekly missed counter (called by DaySystem when new week begins)
    /// </summary>
    public void ResetWeeklyMissedCount(int newWeek = -1)
    {
        missedEntriesThisWeek = 0;
        if (newWeek > 0)
        {
            if (!missedEntriesByWeek.ContainsKey(newWeek)) missedEntriesByWeek[newWeek] = 0;
        }
    }

    #endregion

    #region Helpers: Day/Week resolution

    int ResolveDay(int requested)
    {
        if (requested > 0) return requested;

        var dsType = Type.GetType("DaySystem");
        if (dsType != null)
        {
            var instProp = dsType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (instProp != null)
            {
                var dsInst = instProp.GetValue(null, null);
                if (dsInst != null)
                {
                    var dayProp = dsType.GetProperty("currentDayIndex") ?? dsType.GetProperty("currentDay") ?? dsType.GetProperty("day");
                    if (dayProp != null)
                    {
                        try
                        {
                            object v = dayProp.GetValue(dsInst, null);
                            if (v is int iv) return iv;
                        }
                        catch { }
                    }
                }
            }
        }

        return fallbackDay;
    }

    int ResolveWeek(int requested)
    {
        if (requested > 0) return requested;

        var dsType = Type.GetType("DaySystem");
        if (dsType != null)
        {
            var instProp = dsType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (instProp != null)
            {
                var dsInst = instProp.GetValue(null, null);
                if (dsInst != null)
                {
                    var weekProp = dsType.GetProperty("currentWeek") ?? dsType.GetProperty("week");
                    if (weekProp != null)
                    {
                        try
                        {
                            object v = weekProp.GetValue(dsInst, null);
                            if (v is int iv) return iv;
                        }
                        catch { }
                    }
                }
            }
        }

        return fallbackWeek;
    }

    #endregion

    #region Hunger resolution (immediate + fallback)

    /// <summary>
    /// Try to resolve hunger string immediately using StarvationSystem singleton or FindObjectOfType fallback.
    /// This is the preferred deterministic read used when creating entries.
    /// </summary>
    string ResolvePlayerHungerStateStringImmediate()
    {
        try
        {
            var ss = StarvationSystem.Instance;
            if (ss == null)
            {
                // fallback: find in scene
                ss = UnityEngine.Object.FindObjectOfType<StarvationSystem>();
                if (ss != null)
                    Debug.Log("[Journal] Found StarvationSystem via FindObjectOfType (fallback).");
            }

            if (ss != null)
            {
                try
                {
                    // prefer explicit API
                    return ss.GetSeverityString();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[Journal] Error calling StarvationSystem.GetSeverityString(): " + ex);
                    try
                    {
                        return ss.GetSeverityEnum().ToString();
                    }
                    catch { }
                }
            }
            else
            {
                Debug.Log("[Journal] StarvationSystem not available when resolving hunger (immediate).");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Journal] Exception while trying to read StarvationSystem (immediate): " + ex);
        }

        // final fallback to legacy reflection approach
        return ResolvePlayerHungerStateStringLegacy();
    }

    /// <summary>
    /// Legacy reflection search — keeps compatibility with older hunger systems.
    /// </summary>
    string ResolvePlayerHungerStateStringLegacy()
    {
        try
        {
            var all = UnityEngine.Object.FindObjectsOfType<UnityEngine.MonoBehaviour>();
            foreach (var mb in all)
            {
                if (mb == null) continue;
                var t = mb.GetType();
                if (t.Name.Equals("StarvationSystem", StringComparison.OrdinalIgnoreCase)
                    || t.Name.Equals("PlayerHunger", StringComparison.OrdinalIgnoreCase)
                    || t.Name.Equals("SimpleHunger", StringComparison.OrdinalIgnoreCase))
                {
                    var mi = t.GetMethod("GetHungerState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (mi != null) { var v = mi.Invoke(mb, null); if (v != null) return v.ToString(); }

                    var mi2 = t.GetMethod("GetSeverityString", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (mi2 != null) { var v = mi2.Invoke(mb, null); if (v != null) return v.ToString(); }

                    var prop = t.GetProperty("currentSeverity") ?? t.GetProperty("hungerState") ?? t.GetProperty("HungerState");
                    if (prop != null) { var v = prop.GetValue(mb, null); if (v != null) return v.ToString(); }
                }
            }
        }
        catch { /* ignore */ }

        return "Unknown";
    }

    /// <summary>
    /// Coroutine that attempts to update a newly-created JournalEntry's hunger value a short time later
    /// (useful when StarvationSystem initializes after JournalManager).
    /// </summary>
    IEnumerator UpdateEntryHungerLaterCoroutine(JournalEntry entry, float delay)
    {
        if (entry == null) yield break;
        yield return new WaitForSecondsRealtime(delay);

        // Only update if still unknown
        if (!string.IsNullOrEmpty(entry.hungerLevel) && !entry.hungerLevel.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            yield break;

        string newVal = ResolvePlayerHungerStateStringImmediate();
        if (!string.IsNullOrEmpty(newVal) && !newVal.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log($"[Journal] Updating entry '{entry.taskName}' hunger from '{entry.hungerLevel}' to '{newVal}' after delay.");
            entry.hungerLevel = newVal;

            // update UI if present: easiest approach is to clear+repopulate (safe)
            if (journalUI != null)
            {
                journalUI.ClearUI();
                journalUI.PopulateExisting();
            }
        }
    }

    #endregion

    #region Utilities

    public void SetJournalUI(JournalUI ui)
    {
        journalUI = ui;
    }

    public void IncrementFallbackDay() => fallbackDay++;
    public void IncrementFallbackWeek() => fallbackWeek++;
    #endregion
}

using System;
using UnityEngine;
using UnityEngine.Events;

public enum DayState { Morning = 0, Afternoon = 1 }

[Serializable]
public class DayStateChangedEvent : UnityEvent<DayState, int, int> { } // state, dayIndex, weekNumber

public class DaySystem : MonoBehaviour
{
    public static DaySystem Instance { get; private set; }

    [Header("Day Settings")]
    public int daysPerWeek = 5;
    public int startDayIndex = 1;
    public int startWeekNumber = 1;

    [Header("Runtime")]
    public DayState currentState = DayState.Morning;
    [Range(1, 30)]
    public int currentDayIndex = 1;
    public int currentWeek = 1;

    [Header("Events")]
    public DayStateChangedEvent OnDayStateChanged;
    public UnityEvent OnWeekCompleted;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        currentDayIndex = Mathf.Clamp(startDayIndex, 1, daysPerWeek);
        currentWeek = startWeekNumber;
        currentState = DayState.Morning;
    }

    /// <summary>
    /// Called when a mini-game completes. This will advance the state.
    /// </summary>
    public void OnMiniGameCompleted()
    {
        AdvanceState();
    }

    private void AdvanceState()
    {
        if (currentState == DayState.Morning)
        {
            // Morning → Afternoon
            currentState = DayState.Afternoon;
        }
        else
        {
            // Afternoon → next day morning
            currentState = DayState.Morning;
            currentDayIndex++;

            // New week check
            if (currentDayIndex > daysPerWeek)
            {
                currentDayIndex = 1;
                currentWeek++;

                OnWeekCompleted?.Invoke();

                // Weekly summary + reset
                JournalManager.Instance?.GenerateWeeklySummary(currentWeek - 1);
                MiniGameManager.Instance?.ResetWeeklyCounters();
            }
        }

        OnDayStateChanged?.Invoke(currentState, currentDayIndex, currentWeek);
        Debug.Log($"DaySystem: {currentState} (Day {currentDayIndex}, Week {currentWeek})");
    }

    public string GetReadableTimeStamp()
    {
        return $"Week {currentWeek} Day {currentDayIndex} - {currentState}";
    }
}

// StarvationSystem_Patched.cs
using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Starvation system with safer loading & optional offline decay.
/// - If no saved value exists, starts at full (maxSeconds).
/// - If saved value exists, uses lastSavedTimestamp to apply offline decay.
/// - If saved value is exactly 0 but there's no timestamp (old buggy save), treat as first-run and set to full.
/// - Provides ResetHungerToFull() for debugging.
/// </summary>
public enum HungerSeverity
{
    Full,
    Hungry,
    Starving
}

public class StarvationSystem : MonoBehaviour
{
    public static StarvationSystem Instance { get; private set; }

    [Header("Base hunger settings (seconds)")]
    public float maxSeconds = 300f;
    public float hungryThresholdSecs = 120f;
    public float starvationThresholdSecs = 60f;

    [Header("Gameplay & persistence")]
    public float decayRate = 1f;
    public string prefsKey = "HungerSecondsRemaining";
    public string prefsTimestampKey = "HungerLastSavedUnix"; // store unix seconds

    // runtime
    [SerializeField] private float secondsRemaining = -1f;
    private HungerSeverity currentSeverity = HungerSeverity.Full;
    private Coroutine starvationTickCoroutine = null;
    public float starvationTickInterval = 3f;
    public float minMovementMultiplier = 0.75f;
    public float maxDifficultyMultiplier = 1.4f;

    // events
    public event Action<float> OnHungerChanged;
    public event Action<HungerSeverity> OnSeverityChanged;
    public event Action OnStarvationTick;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadWithOfflineDecay();
    }

    private void Start()
    {
        // Ensure a sensible initial value if load failed
        if (secondsRemaining < 0f) secondsRemaining = maxSeconds;
        NotifyHungerChanged();
        EvaluateSeverity(true);
        StartCoroutine(HungerDecayCoroutine());
    }

    private IEnumerator HungerDecayCoroutine()
    {
        while (true)
        {
            float dt = Time.deltaTime * decayRate;
            if (secondsRemaining > 0f)
            {
                secondsRemaining = Mathf.Max(0f, secondsRemaining - dt);
                NotifyHungerChanged();
                Save(); // persist frequently so crashes won't set to 0 next run
                EvaluateSeverity(false);
            }
            yield return null;
        }
    }

    // Add to StarvationSystem class (once)
    public HungerSeverity GetSeverityEnum()
    {
        return currentSeverity;
    }

    public string GetSeverityString()
    {
        return currentSeverity.ToString();
    }

    #region Loading & Offline decay

    private void LoadWithOfflineDecay()
    {
        // No saved key -> first run: full plate
        if (!PlayerPrefs.HasKey(prefsKey))
        {
            secondsRemaining = maxSeconds;
            Save(); // write initial
            return;
        }

        // load saved seconds
        float savedSeconds = PlayerPrefs.GetFloat(prefsKey, -1f);

        // load timestamp if present
        long savedUnix = 0;
        if (PlayerPrefs.HasKey(prefsTimestampKey))
        {
            savedUnix = Convert.ToInt64(PlayerPrefs.GetString(prefsTimestampKey, "0"));
        }

        // If savedSeconds is <= 0 AND there is no timestamp, assume this was a bad/old save -> reset to full
        if (savedSeconds <= 0f && savedUnix == 0)
        {
            secondsRemaining = maxSeconds;
            Save();
            return;
        }

        // If there is a timestamp, apply offline decay
        if (savedUnix > 0)
        {
            DateTime savedTime = UnixSecondsToDateTime(savedUnix);
            double elapsed = (DateTime.UtcNow - savedTime).TotalSeconds;
            float decayed = savedSeconds - (float)(elapsed * decayRate);
            secondsRemaining = Mathf.Clamp(decayed, 0f, maxSeconds);
            // if decayed value is negative, we'll start at 0 (starving) — that's intended behaviour for offline decay
        }
        else
        {
            // no timestamp but a savedSeconds exists (maybe from earlier version). Use it directly if >0
            secondsRemaining = Mathf.Clamp(savedSeconds, 0f, maxSeconds);
        }
    }

    private static DateTime UnixSecondsToDateTime(long unix)
    {
        DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return epoch.AddSeconds(unix);
    }

    #endregion

    #region Save
    private void Save()
    {
        PlayerPrefs.SetFloat(prefsKey, secondsRemaining);
        // save timestamp
        string unixStr = Convert.ToInt64((DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds).ToString();
        PlayerPrefs.SetString(prefsTimestampKey, unixStr);
        PlayerPrefs.Save();
    }
    #endregion

    #region API & helpers

    public float GetFill() => Mathf.Clamp01(secondsRemaining / maxSeconds);
    public float GetSecondsRemaining() => secondsRemaining;
    public HungerSeverity GetSeverity() => currentSeverity;

    public void AddTime(float seconds)
    {
        if (seconds <= 0f) return;
        secondsRemaining = Mathf.Clamp(secondsRemaining + seconds, 0f, maxSeconds);
        NotifyHungerChanged();
        EvaluateSeverity(false);
        Save();
    }

    public void EatFood(FoodItem food)
    {
        if (food == null) return;
        AddTime(food.hungerRestoreSeconds);
    }

    public void SetSecondsRemaining(float seconds)
    {
        secondsRemaining = Mathf.Clamp(seconds, 0f, maxSeconds);
        NotifyHungerChanged();
        EvaluateSeverity(false);
        Save();
    }

    public void ResetHungerToFull()
    {
        secondsRemaining = maxSeconds;
        NotifyHungerChanged();
        EvaluateSeverity(true);
        Save();
    }

    public bool IsStarving() => currentSeverity == HungerSeverity.Starving;

    public float GetMovementSpeedMultiplier()
    {
        // Hard starvation penalty
        if (secondsRemaining <= 0f)
            return 0.25f; // <-- dramatic slow (25% of normal). Change this value to taste.

        // Normal hunger → interpolate like before
        if (secondsRemaining > hungryThresholdSecs)
            return 1f;

        float t = Mathf.InverseLerp(hungryThresholdSecs, 0f, secondsRemaining);
        return Mathf.Lerp(1f, minMovementMultiplier, 1f - t);
    }


    public float GetMiniGameDifficultyMultiplier()
    {
        if (secondsRemaining > hungryThresholdSecs) return 1f;
        if (secondsRemaining <= starvationThresholdSecs) return maxDifficultyMultiplier;
        float t = Mathf.InverseLerp(hungryThresholdSecs, starvationThresholdSecs, secondsRemaining);
        return Mathf.Lerp(1f, maxDifficultyMultiplier, 1f - t);
    }

    #endregion

    #region Severity & events
    private void NotifyHungerChanged() => OnHungerChanged?.Invoke(GetFill());
    private void EvaluateSeverity(bool forceNotify)
    {
        HungerSeverity next;
        if (secondsRemaining <= starvationThresholdSecs) next = HungerSeverity.Starving;
        else if (secondsRemaining <= hungryThresholdSecs) next = HungerSeverity.Hungry;
        else next = HungerSeverity.Full;

        if (forceNotify || next != currentSeverity)
        {
            currentSeverity = next;
            OnSeverityChanged?.Invoke(currentSeverity);

            if (currentSeverity == HungerSeverity.Starving)
            {
                if (starvationTickCoroutine != null) StopCoroutine(starvationTickCoroutine);
                starvationTickCoroutine = StartCoroutine(StarvationTickLoop());
            }
            else
            {
                if (starvationTickCoroutine != null) { StopCoroutine(starvationTickCoroutine); starvationTickCoroutine = null; }
            }
        }
    }

    private IEnumerator StarvationTickLoop()
    {
        while (currentSeverity == HungerSeverity.Starving)
        {
            OnStarvationTick?.Invoke();
            yield return new WaitForSeconds(starvationTickInterval);
        }
    }
    #endregion

    private void OnApplicationPause(bool pause) { if (pause) Save(); }
    private void OnApplicationQuit() { Save(); }
}

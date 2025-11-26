using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class StarvationAudioController : MonoBehaviour
{
    [Header("Pitch Mapping (lower fill -> deeper sound)")]
    [Tooltip("Pitch when player is fully fed (1 = normal pitch)")]
    public float normalPitch = 1.0f;
    [Tooltip("Pitch when player is starving (e.g. 0.8 is deeper/slower)")]
    public float starvingPitch = 0.8f;
    [Tooltip("How quickly pitch will interpolate (seconds)")]
    public float pitchLerpSpeed = 1.0f;

    [Header("Targets")]
    [Tooltip("If true the controller will attempt to change AudioManager's active music AudioSource pitch.")]
    public bool affectMusic = true;
    [Tooltip("If true the controller will attempt to change SFX pool pitch as well.")]
    public bool affectSFX = false;

    // runtime
    float targetPitch = 1f;
    Coroutine tweenCoroutine;

    void OnEnable()
    {
        targetPitch = normalPitch;
        // try subscribe to StarvationSystem events
        if (StarvationSystem.Instance != null)
        {
            StarvationSystem.Instance.OnHungerChanged += OnHungerChanged;
            StarvationSystem.Instance.OnSeverityChanged += OnSeverityChanged;
            // set initial state
            MapFillToPitch(StarvationSystem.Instance.GetFill());
        }
        else
        {
            // fallback: try to find one later
            StartCoroutine(WaitForStarvationThenSubscribe());
        }
    }

    void OnDisable()
    {
        if (StarvationSystem.Instance != null)
        {
            StarvationSystem.Instance.OnHungerChanged -= OnHungerChanged;
            StarvationSystem.Instance.OnSeverityChanged -= OnSeverityChanged;
        }
        if (tweenCoroutine != null) StopCoroutine(tweenCoroutine);
    }

    IEnumerator WaitForStarvationThenSubscribe()
    {
        float t = 0f;
        float timeout = 5f;
        while (StarvationSystem.Instance == null && t < timeout)
        {
            t += Time.deltaTime;
            yield return null;
        }
        if (StarvationSystem.Instance != null)
        {
            StarvationSystem.Instance.OnHungerChanged += OnHungerChanged;
            StarvationSystem.Instance.OnSeverityChanged += OnSeverityChanged;
            MapFillToPitch(StarvationSystem.Instance.GetFill());
        }
    }

    void OnHungerChanged(float fill)
    {
        MapFillToPitch(fill);
    }

    void OnSeverityChanged(HungerSeverity sev)
    {
        // Optionally override target when severity boundary crossed
        switch (sev)
        {
            case HungerSeverity.Full:
                SetTargetPitch(normalPitch);
                break;
            case HungerSeverity.Hungry:
                // halfway between normal and starving
                SetTargetPitch(Mathf.Lerp(normalPitch, starvingPitch, 0.5f));
                break;
            case HungerSeverity.Starving:
                SetTargetPitch(starvingPitch);
                break;
        }
    }

    void MapFillToPitch(float fill01)
    {
        // fill01: 0..1 where 1 is full. We map to pitch range starvingPitch..normalPitch
        float p = Mathf.Lerp(starvingPitch, normalPitch, Mathf.Clamp01(fill01));
        SetTargetPitch(p);
    }

    void SetTargetPitch(float pitch)
    {
        targetPitch = pitch;
        if (tweenCoroutine != null) StopCoroutine(tweenCoroutine);
        tweenCoroutine = StartCoroutine(TweenPitchCoroutine(targetPitch));
    }

    IEnumerator TweenPitchCoroutine(float toPitch)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.0001f, pitchLerpSpeed);
        // get current pitch from music source (best effort)
        float startPitch = GetMusicSourcePitchSafe();

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / duration);
            float p = Mathf.Lerp(startPitch, toPitch, u);
            ApplyPitchToTargets(p);
            yield return null;
        }

        ApplyPitchToTargets(toPitch);
        tweenCoroutine = null;
    }

    void ApplyPitchToTargets(float pitch)
    {
        if (affectMusic)
        {
            TrySetActiveMusicPitch(pitch);
        }
        if (affectSFX)
        {
            TrySetSFXPoolPitch(pitch);
        }
    }

    float GetMusicSourcePitchSafe()
    {
        try
        {
            var am = AudioManager.Instance;
            if (am == null) return normalPitch;

            // try to get private field "activeMusicSource"
            var fi = am.GetType().GetField("activeMusicSource", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (fi != null)
            {
                var obj = fi.GetValue(am) as AudioSource;
                if (obj != null) return obj.pitch;
            }

            // fallback: try property "musicSourceA" (public private mismatch)
            var pA = am.GetType().GetField("musicSourceA", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (pA != null)
            {
                var a = pA.GetValue(am) as AudioSource;
                if (a != null && a.isPlaying) return a.pitch;
            }
        }
        catch { }
        return normalPitch;
    }

    void TrySetActiveMusicPitch(float pitch)
    {
        try
        {
            var am = AudioManager.Instance;
            if (am == null) return;

            // Preferred: call public method (if you added one). Try that first.
            var miPublic = am.GetType().GetMethod("SetActiveMusicPitch", BindingFlags.Instance | BindingFlags.Public);
            if (miPublic != null)
            {
                miPublic.Invoke(am, new object[] { pitch });
                return;
            }

            // Reflection fallback: find private field "activeMusicSource"
            var fi = am.GetType().GetField("activeMusicSource", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (fi != null)
            {
                var src = fi.GetValue(am) as AudioSource;
                if (src != null)
                {
                    src.pitch = pitch;
                    return;
                }
            }

            // Another common naming: "musicSourceA" / "musicSourceB" — set both if found
            var fA = am.GetType().GetField("musicSourceA", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var fB = am.GetType().GetField("musicSourceB", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (fA != null)
            {
                var a = fA.GetValue(am) as AudioSource;
                if (a != null) a.pitch = pitch;
            }
            if (fB != null)
            {
                var b = fB.GetValue(am) as AudioSource;
                if (b != null) b.pitch = pitch;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[StarvationAudioController] Could not set music pitch via reflection: " + ex.Message);
        }
    }

    void TrySetSFXPoolPitch(float pitch)
    {
        try
        {
            var am = AudioManager.Instance;
            if (am == null) return;

            // sfx pool is a private List<AudioSource> named "sfxPool"
            var fi = am.GetType().GetField("sfxPool", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (fi != null)
            {
                var poolObj = fi.GetValue(am) as System.Collections.IList;
                if (poolObj != null)
                {
                    foreach (var item in poolObj)
                    {
                        var src = item as AudioSource;
                        if (src != null) src.pitch = pitch;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[StarvationAudioController] Could not set SFX pool pitch: " + ex.Message);
        }
    }
}

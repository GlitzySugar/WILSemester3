using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// TeacherNPC: simple NPC that comments on the player when interacted with.
/// - Does NOT start mini-games. It reads the player's starvation/hunger state and shows a line.
/// - Uses WorldSpacePrompt.Ensure(gameObject).Show(...) to show the interact prompt and ShowTemp for short responses.
/// - If a DialogueManager (or a component with method Show(string)) exists, the script will try to use it instead.
/// - Safe fallbacks and reflection are used so missing systems won't break the game.
/// </summary>
[DisallowMultipleComponent]
public class TeacherNPC : MonoBehaviour
{
    [Header("Detection")]
    public Transform player;
    public float interactRange = 2.5f;
    public KeyCode interactKey = KeyCode.E;

    [Header("Prompt")]
    [Tooltip("Prompt shown while player is in range. E.g. 'Press E to talk'")]
    public string promptText = "Press E to talk";

    [Header("Response lines (editable)")]
    [Tooltip("Short lines for Full / OK player")]
    [TextArea(2, 4)]
    public string[] fullLines = new string[] { "You look well-fed today. Keep it up!" };

    [Tooltip("Short lines for Hungry player")]
    [TextArea(2, 4)]
    public string[] hungryLines = new string[] { "You seem a bit hungry — try to get some food." };

    [Tooltip("Short lines for Starving player")]
    [TextArea(2, 4)]
    public string[] starvingLines = new string[] { "You look awful. Hurry — get food now!" };

    [Header("Behavior")]
    [Tooltip("Seconds to wait before the teacher can be interacted with again")]
    public float interactCooldown = 2f;

    [Tooltip("If true, teacher will only comment during Morning state. If false, always listens.")]
    public bool requireMorning = false;

    [Header("Events")]
    public UnityEvent onInteractFull;
    public UnityEvent onInteractHungry;
    public UnityEvent onInteractStarving;

    // runtime
    float lastInteractTime = -999f;
    WorldSpacePrompt prompt;

    void Awake()
    {
        if (player == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) player = p.transform;
        }

        prompt = WorldSpacePrompt.Ensure(gameObject);
        prompt.Show(promptText);
        SetPromptVisible(false);
    }

    void Update()
    {
        if (player == null) return;

        float dist = Vector3.Distance(player.position, transform.position);
        bool close = dist <= interactRange;

        // toggle prompt
        SetPromptVisible(close);

        if (!close) return;

        // input check
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
        {
            OnInteract();
        }
#else
        if (Input.GetKeyDown(interactKey))
        {
            OnInteract();
        }
#endif
    }

    void SetPromptVisible(bool v)
    {
        if (prompt == null) return;
        if (v) prompt.Show(promptText);
        else prompt.Hide();
    }

    void OnInteract()
    {
        // cooldown
        if (Time.time - lastInteractTime < interactCooldown)
        {
            // short temp feedback
            prompt?.ShowTemp("Not yet...", 0.9f);
            return;
        }

        lastInteractTime = Time.time;

        // Optional DaySystem gating
        if (requireMorning)
        {
            var ds = DaySystem.Instance;
            if (ds != null && ds.currentState != DayState.Morning)
            {
                ShowLine("It's not the right time — I'm busy.");
                return;
            }
        }

        // Resolve hunger / starvation string
        string hunger = ResolveHungerString();

        // pick line based on hunger
        string chosen = null;
        try
        {
            if (!string.IsNullOrEmpty(hunger) && hunger.Equals("Starving", StringComparison.OrdinalIgnoreCase))
            {
                chosen = PickRandom(starvingLines);
                ShowLine(chosen);
                onInteractStarving?.Invoke();
            }
            else if (!string.IsNullOrEmpty(hunger) && (hunger.Equals("Hungry", StringComparison.OrdinalIgnoreCase) || hunger.Equals("Normal", StringComparison.OrdinalIgnoreCase) == false && hunger.Equals("Full", StringComparison.OrdinalIgnoreCase) == false && hunger.Equals("Unknown", StringComparison.OrdinalIgnoreCase) == false && hunger.IndexOf("hungry", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                // treat any "Hungry" like hungryLines
                chosen = PickRandom(hungryLines);
                ShowLine(chosen);
                onInteractHungry?.Invoke();
            }
            else
            {
                chosen = PickRandom(fullLines);
                ShowLine(chosen);
                onInteractFull?.Invoke();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[TeacherNPC] Exception picking/playing line: " + ex);
            ShowLine("Hmm.");
        }
    }

    string PickRandom(string[] arr)
    {
        if (arr == null || arr.Length == 0) return "";
        return arr[UnityEngine.Random.Range(0, arr.Length)];
    }

    void ShowLine(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        // 1) first try DialogueManager-like components (Show(string))
        var dm = FindDialogueManagerLike();
        if (dm != null)
        {
            try
            {
                var mi = dm.GetType().GetMethod("Show", new Type[] { typeof(string) });
                if (mi != null)
                {
                    mi.Invoke(dm, new object[] { text });
                    return;
                }
                // other common method names
                mi = dm.GetType().GetMethod("Display", new Type[] { typeof(string) });
                if (mi != null) { mi.Invoke(dm, new object[] { text }); return; }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[TeacherNPC] DialogueManager invoke failed: " + ex);
            }
        }

        // 2) fallback to WorldSpacePrompt.ShowTemp
        if (prompt != null)
        {
            prompt.ShowTemp(text, 2.2f);
            return;
        }

        // 3) last fallback: Debug.Log
        Debug.Log("[TeacherNPC] " + text);
    }

    object FindDialogueManagerLike()
    {
        // look for a component called DialogueManager (common pattern)
        var dm = GameObject.FindObjectOfType<MonoBehaviour>();
        if (dm != null)
        {
            // quick search for any object with type name "DialogueManager" or "DialogueSystem"
            var all = GameObject.FindObjectsOfType<MonoBehaviour>();
            foreach (var mb in all)
            {
                if (mb == null) continue;
                var tn = mb.GetType().Name;
                if (tn.IndexOf("Dialogue", StringComparison.OrdinalIgnoreCase) >= 0)
                    return mb;
            }
        }
        return null;
    }

    /// <summary>
    /// Tries to obtain a hunger severity string from your starvation system.
    /// Tries direct API calls first, then a reflection-based fallback.
    /// Returns "Unknown" if nothing found.
    /// </summary>
    string ResolveHungerString()
    {
        try
        {
            var ss = StarvationSystem.Instance;
            if (ss == null)
            {
                // fallback: FindObjectOfType
                ss = UnityEngine.Object.FindObjectOfType<StarvationSystem>();
                if (ss != null) Debug.Log("[TeacherNPC] Found StarvationSystem via FindObjectOfType.");
            }
            if (ss != null)
            {
                // try GetSeverityString()
                var mi = ss.GetType().GetMethod("GetSeverityString", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (mi != null)
                {
                    var v = mi.Invoke(ss, null);
                    if (v != null) return v.ToString();
                }

                // try GetSeverity() returning enum or GetSeverityEnum
                var mi2 = ss.GetType().GetMethod("GetSeverity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (mi2 != null)
                {
                    var v = mi2.Invoke(ss, null);
                    if (v != null) return v.ToString();
                }

                var prop = ss.GetType().GetProperty("currentSeverity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (prop != null)
                {
                    var pv = prop.GetValue(ss, null);
                    if (pv != null) return pv.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[TeacherNPC] ResolveHungerString fallback error: " + ex);
        }

        // reflection sweep: try find any MB that looks like hunger system
        try
        {
            var all = GameObject.FindObjectsOfType<MonoBehaviour>();
            foreach (var mb in all)
            {
                if (mb == null) continue;
                var t = mb.GetType();
                if (t.Name.IndexOf("Starv", StringComparison.OrdinalIgnoreCase) >= 0 || t.Name.IndexOf("Hunger", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var mi = t.GetMethod("GetSeverityString", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (mi != null) { var v = mi.Invoke(mb, null); if (v != null) return v.ToString(); }

                    var prop = t.GetProperty("currentSeverity") ?? t.GetProperty("hungerState");
                    if (prop != null) { var pv = prop.GetValue(mb, null); if (pv != null) return pv.ToString(); }
                }
            }
        }
        catch { /* ignore */ }

        return "Unknown";
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}

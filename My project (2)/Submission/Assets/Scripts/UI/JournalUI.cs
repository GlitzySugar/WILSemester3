using System;
using System.Collections;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Robust Journal UI that hides/shows without disabling the root GameObject.
/// - Toggle with Tab (or change toggleKey)
/// - Uses CanvasGroup for visibility/interactivity
/// - OpenForced() will activate parent chain + canvas if needed
/// - Safe AddEntryToUI that supports legacy Text or TextMeshPro in prefab
/// </summary>
public class JournalUI : MonoBehaviour
{
    [Header("Input")]
    public KeyCode toggleKey = KeyCode.Tab;

    [Header("Root & CanvasGroup")]
    [Tooltip("Root panel (book background). Keep this GameObject active in the scene; the script hides via CanvasGroup.")]
    public GameObject journalRootPanel;

    [Tooltip("Optional CanvasGroup on the root. If missing, one will be created.")]
    public CanvasGroup journalCanvasGroup;

    [Header("Scroll / Entry Prefab")]
    [Tooltip("Content RectTransform inside ScrollView (where entries are parented).")]
    public RectTransform contentParent;

    [Tooltip("Prefab for a journal entry (legacy Text or TMP).")]
    public GameObject entryPrefab;

    [Header("Animation / Layout")]
    public float openCloseDuration = 0.16f;
    public Vector3 closedScale = new Vector3(0.95f, 0.95f, 0.95f);
    public Vector3 openScale = Vector3.one;

    [Header("Behavior")]
    [Tooltip("Start closed (hidden) but keep object active)")]
    public bool startClosed = true;

    // internal state
    bool isOpen = false;
    Coroutine animCoroutine = null;

    // debounce to avoid instant re-open
    float lastToggleTime = -10f;
    public float toggleCooldown = 0.12f;

    void Awake()
    {
        // Try to auto-find common names if fields not assigned
        if (journalRootPanel == null)
        {
            var panel = GameObject.Find("JournalRootPanel") ?? GameObject.Find("JournalPanel");
            if (panel != null) journalRootPanel = panel;
        }

        if (contentParent == null)
        {
            var contentGO = GameObject.Find("Content") ?? GameObject.Find("JournalContent");
            if (contentGO != null) contentParent = contentGO.GetComponent<RectTransform>();
        }
    }

    void Start()
    {
        if (journalRootPanel == null)
        {
            Debug.LogError("[JournalUI] journalRootPanel is not assigned. Please assign a root panel in the inspector.");
            enabled = false;
            return;
        }

        EnsureCanvasGroup();

        // Keep the object active but hide via CanvasGroup so we don't fight other systems.
        journalRootPanel.SetActive(true);

        if (startClosed)
        {
            isOpen = false;
            journalCanvasGroup.alpha = 0f;
            journalCanvasGroup.interactable = false;
            journalCanvasGroup.blocksRaycasts = false;
            journalRootPanel.transform.localScale = closedScale;
        }
        else
        {
            isOpen = true;
            journalCanvasGroup.alpha = 1f;
            journalCanvasGroup.interactable = true;
            journalCanvasGroup.blocksRaycasts = true;
            journalRootPanel.transform.localScale = openScale;
        }

        Debug.Log("[JournalUI] Initialized. Toggle with " + toggleKey + ".");
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            // if typing in a TMP input or legacy InputField, ignore
            var sel = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            if (sel != null)
            {
                if (sel.GetComponent<InputField>() != null) return;
#if TMP_PRESENT
                if (sel.GetComponent<TMPro.TMP_InputField>() != null) return;
#endif
            }

            Toggle();
        }
    }

    #region Open / Close API

    public void Toggle()
    {
        if (isOpen) Close();
        else Open();
    }

    /// <summary>
    /// Normal Open - honors debounce.
    /// </summary>
    public void Open()
    {
        if (journalRootPanel == null) return;

        float now = Time.realtimeSinceStartup;
        if (now - lastToggleTime < toggleCooldown)
        {
            // ignore immediate open attempts — log stacktrace to help locate callers
#if UNITY_EDITOR
            var st = new StackTrace(true);
            Debug.LogWarning($"[JournalUI] Ignored Open() due to cooldown ({now - lastToggleTime:F3}s). Call stack:\n{st}");
#else
            Debug.LogWarning($"[JournalUI] Ignored Open() due to cooldown ({now - lastToggleTime:F3}s).");
#endif
            return;
        }

        if (isOpen) return; // already open

        // ensure visual state and animate
        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(AnimateOpen(true));
        isOpen = true;
    }

    /// <summary>
    /// Force open: activates parent chain and ensures Canvas settings are valid before showing.
    /// Use this when you suspect parents/canvas may be inactive or incorrectly configured.
    /// </summary>
    public void OpenForced()
    {
        if (journalRootPanel == null)
        {
            Debug.LogError("[JournalUI] OpenForced: journalRootPanel is null");
            return;
        }

        // activate parent chain (activates any deactivated ancestors)
        Transform t = journalRootPanel.transform;
        while (t != null)
        {
            if (!t.gameObject.activeSelf)
            {
                Debug.LogWarning("[JournalUI] OpenForced: activating parent " + t.name);
                t.gameObject.SetActive(true);
            }
            t = t.parent;
        }

        // ensure Canvas is enabled and has a camera if needed
        var canvas = journalRootPanel.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            if (!canvas.enabled)
            {
                Debug.LogWarning("[JournalUI] OpenForced: Canvas disabled - enabling it.");
                canvas.enabled = true;
            }
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)
            {
                Debug.LogWarning("[JournalUI] OpenForced: Canvas worldCamera null - assigning Camera.main");
                canvas.worldCamera = Camera.main;
            }
        }
        else
        {
            Debug.LogWarning("[JournalUI] OpenForced: No Canvas found in parents; UI may not render.");
        }

        EnsureCanvasGroup();

        // bring to front and animate open
        journalRootPanel.transform.SetAsLastSibling();

        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(AnimateOpen(true));
        isOpen = true;
    }

    /// <summary>
    /// Close by fading and disabling interaction. The GameObject remains active.
    /// Sets lastToggleTime so Open() won't immediately reopen due to racing callers.
    /// </summary>
    public void Close()
    {
        if (journalRootPanel == null) return;

        if (!isOpen) return;

        if (animCoroutine != null) StopCoroutine(animCoroutine);
        animCoroutine = StartCoroutine(AnimateOpen(false));
        isOpen = false;

        lastToggleTime = Time.realtimeSinceStartup;
    }

    /// <summary>
    /// Close without animation, keeps GameObject active.
    /// </summary>
    public void CloseWithoutDisable()
    {
        if (journalRootPanel == null) return;
        EnsureCanvasGroup();
        journalCanvasGroup.alpha = 0f;
        journalCanvasGroup.interactable = false;
        journalCanvasGroup.blocksRaycasts = false;
        journalRootPanel.transform.localScale = closedScale;
        isOpen = false;
        lastToggleTime = Time.realtimeSinceStartup;
    }

    #endregion

    #region Animation

    IEnumerator AnimateOpen(bool opening)
    {
        EnsureCanvasGroup();

        float t = 0f;
        float dur = Mathf.Max(0.001f, openCloseDuration);

        Vector3 startScale = opening ? closedScale : openScale;
        Vector3 endScale = opening ? openScale : closedScale;
        float startAlpha = opening ? 0f : 1f;
        float endAlpha = opening ? 1f : 0f;

        // prepare before animation
        journalCanvasGroup.interactable = opening;
        journalCanvasGroup.blocksRaycasts = opening;
        journalRootPanel.transform.localScale = startScale;
        journalCanvasGroup.alpha = startAlpha;

        // animate using unscaled time
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = Mathf.SmoothStep(0f, 1f, t / dur);
            journalRootPanel.transform.localScale = Vector3.Lerp(startScale, endScale, u);
            journalCanvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, u);
            yield return null;
        }

        // finalize
        journalRootPanel.transform.localScale = endScale;
        journalCanvasGroup.alpha = endAlpha;
        journalCanvasGroup.interactable = opening;
        journalCanvasGroup.blocksRaycasts = opening;

        animCoroutine = null;
    }

    #endregion

    #region UI helpers / entries

    /// <summary>
    /// Adds a JournalEntry object to the UI. Prefab may use legacy Text or TextMeshPro.
    /// </summary>
    public void AddEntryToUI(JournalEntry entry)
    {
        if (contentParent == null)
        {
            Debug.LogWarning("[JournalUI] AddEntryToUI: contentParent is null. Assign the ScrollView Content.");
            return;
        }

        if (entry == null)
        {
            Debug.LogWarning("[JournalUI] AddEntryToUI: entry is null");
            return;
        }

        if (entryPrefab == null)
        {
            // fallback: create simple Text element
            var go = new GameObject("JournalEntry_Fallback", typeof(RectTransform));
            go.transform.SetParent(contentParent, false);
            var txt = go.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.text = entry.ToString();
            txt.alignment = TextAnchor.UpperLeft;
            return;
        }

        var instance = Instantiate(entryPrefab, contentParent, false);

        // try to find TextMeshPro first (if present)
#if TMP_PRESENT
        var tmp = instance.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (tmp != null)
        {
            tmp.text = entry.ToString();
            return;
        }
#endif

        // legacy Text fallback
        var t = instance.GetComponentInChildren<Text>();
        if (t != null)
        {
            t.text = entry.ToString();
            return;
        }

        // nothing to write to: log and leave it
        Debug.LogWarning("[JournalUI] AddEntryToUI: entry prefab has no text component to populate.");
    }

    /// <summary>
    /// Populate existing entries from JournalManager into the UI.
    /// </summary>
    public void PopulateExisting()
    {
        if (JournalManager.Instance == null)
        {
            Debug.LogWarning("[JournalUI] PopulateExisting: JournalManager.Instance is null.");
            return;
        }

        ClearUI();

        foreach (var e in JournalManager.Instance.GetEntries())
        {
            AddEntryToUI(e);
        }
    }

    /// <summary>
    /// Remove all children from contentParent
    /// </summary>
    public void ClearUI()
    {
        if (contentParent == null) return;
        for (int i = contentParent.childCount - 1; i >= 0; --i)
        {
            var c = contentParent.GetChild(i);
            if (Application.isPlaying) Destroy(c.gameObject);
            else DestroyImmediate(c.gameObject);
        }
    }

    #endregion

    #region Utilities

    void EnsureCanvasGroup()
    {
        if (journalCanvasGroup == null && journalRootPanel != null)
        {
            journalCanvasGroup = journalRootPanel.GetComponent<CanvasGroup>();
            if (journalCanvasGroup == null)
            {
                journalCanvasGroup = journalRootPanel.AddComponent<CanvasGroup>();
            }
        }
    }

    #endregion
}

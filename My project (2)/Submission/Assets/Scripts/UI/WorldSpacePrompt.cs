using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Attach this to an NPC. It will create (or use) a small world-space Canvas + TextMeshPro
/// above the NPC to show an interact prompt (e.g. "Press E to talk") that always faces the camera.
/// It exposes Show/Hide/ShowTemp functions.
/// </summary>
[RequireComponent(typeof(Transform))]
public class WorldSpacePrompt : MonoBehaviour
{
    [Header("Prompt Settings")]
    [Tooltip("Local offset from the transform where the prompt should appear (e.g. above head)")]
    public Vector3 localOffset = new Vector3(0f, 2.0f, 0f);

    [Tooltip("How far above to float when visible (small vertical lerp)")]
    public float floatUp = 0.12f;

    [Tooltip("Scale of the prompt")]
    public Vector2 baseScale = new Vector2(0.02f, 0.02f);

    [Tooltip("Whether the prompt always faces the main camera")]
    public bool faceCamera = true;

    [Header("References (optional)")]
    public Canvas worldCanvas;           // optional: drag a prefab canvas here
    public TextMeshProUGUI promptText;   // optional: drag TMP text here (if using a prefab)
    public Image keyIconImage;           // optional icon inside canvas

    [Header("Tweaks")]
    public float fadeDuration = 0.12f;
    public float visibleAlpha = 1f;
    public bool startHidden = true;

    // runtime
    Canvas runtimeCanvas;
    RectTransform rootRect;
    CanvasGroup cg;
    TextMeshProUGUI runtimeText;
    bool visible = false;
    Coroutine fadeCoroutine;

    void Awake()
    {
        EnsureCanvas();
        if (startHidden) SetVisibleImmediate(false);
        else SetVisibleImmediate(true);
    }

    void EnsureCanvas()
    {
        if (runtimeCanvas != null) return;

        if (worldCanvas != null)
        {
            runtimeCanvas = worldCanvas;
            // assume you set promptText etc on the inspector prefab
            runtimeText = promptText;
            cg = runtimeCanvas.GetComponent<CanvasGroup>();
            if (cg == null) cg = runtimeCanvas.gameObject.AddComponent<CanvasGroup>();
            rootRect = runtimeCanvas.GetComponent<RectTransform>();
            return;
        }

        // Create a tiny world-space canvas with TMP text
        GameObject go = new GameObject($"{name}_WorldPrompt_Canvas");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localOffset;
        go.transform.localRotation = Quaternion.identity;

        runtimeCanvas = go.AddComponent<Canvas>();
        runtimeCanvas.renderMode = RenderMode.WorldSpace;
        runtimeCanvas.worldCamera = Camera.main;
        runtimeCanvas.sortingOrder = 1000;
        runtimeCanvas.overrideSorting = true;

        rootRect = runtimeCanvas.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(300, 60);
        rootRect.localScale = new Vector3(baseScale.x, baseScale.y, 1f);

        cg = go.AddComponent<CanvasGroup>();

        // Background (optional, transparent)
        GameObject bg = new GameObject("BG");
        bg.transform.SetParent(go.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0f); // invisible by default; style if you want
        var bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0f);
        bgRect.anchorMax = new Vector2(1f, 1f);
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // Text
        GameObject t = new GameObject("PromptText");
        t.transform.SetParent(go.transform, false);
        runtimeText = t.AddComponent<TextMeshProUGUI>();
        runtimeText.alignment = TextAlignmentOptions.Center;
        runtimeText.fontSize = 28;
        runtimeText.text = "Press E to interact";
        var textRect = runtimeText.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(6f, 6f);
        textRect.offsetMax = new Vector2(-6f, -6f);

        // Icon (optional) - we leave space for icons if you later add them
        // (If you want an icon, create an Image and position it left of text.)

        // Ensure it doesn't block raycasts by default
        cg.blocksRaycasts = false;
    }

    void Update()
    {
        if (runtimeCanvas == null) return;

        // keep above head with small float when visible
        Vector3 targetLocal = localOffset + (visible ? new Vector3(0f, floatUp, 0f) : Vector3.zero);
        runtimeCanvas.transform.localPosition = targetLocal;

        if (faceCamera && Camera.main != null)
        {
            // BILLBOARD FIX:
            // compute vector FROM the camera TO the canvas, so the canvas forward faces the camera.
            // This keeps the text readable (not reversed).
            Vector3 canvasPos = runtimeCanvas.transform.position;
            Vector3 camPos = Camera.main.transform.position;

            Vector3 dir = canvasPos - camPos; // direction from camera to canvas
            dir.y = 0f; // keep upright
            if (dir.sqrMagnitude > 0.0001f)
            {
                runtimeCanvas.transform.rotation = Quaternion.LookRotation(dir);
            }

            // If you still see mirrored text, try this alternate line instead (flip the rotation):
            // runtimeCanvas.transform.rotation = Quaternion.LookRotation(-dir);
        }
    }

    // immediate show/hide (no fade)
    void SetVisibleImmediate(bool v)
    {
        visible = v;
        if (cg != null)
        {
            cg.alpha = v ? visibleAlpha : 0f;
            cg.interactable = v;
            cg.blocksRaycasts = false;
        }
        if (rootRect != null)
        {
            rootRect.localScale = new Vector3(baseScale.x, baseScale.y, 1f);
        }
    }

    IEnumerator FadeCoroutine(bool show)
    {
        if (cg == null) yield break;
        float start = cg.alpha;
        float to = show ? visibleAlpha : 0f;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(start, to, t / fadeDuration);
            yield return null;
        }
        cg.alpha = to;
        visible = show;
        cg.interactable = show;
        cg.blocksRaycasts = false;
        fadeCoroutine = null;
    }

    public void Show(string text = null)
    {
        if (runtimeText != null && !string.IsNullOrEmpty(text)) runtimeText.text = text;
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeCoroutine(true));
    }

    public void Hide()
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeCoroutine(false));
    }

    public void ShowTemp(string text, float duration = 1.5f)
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        Show(text);
        StartCoroutine(TempHideCoroutine(duration));
    }

    IEnumerator TempHideCoroutine(float dur)
    {
        yield return new WaitForSecondsRealtime(dur);
        Hide();
    }

    // Helper static accessor to quickly get/create the world prompt on an NPC
    public static WorldSpacePrompt Ensure(GameObject npc)
    {
        var comp = npc.GetComponent<WorldSpacePrompt>();
        if (comp == null) comp = npc.AddComponent<WorldSpacePrompt>();
        return comp;
    }
}

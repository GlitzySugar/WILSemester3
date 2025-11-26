using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InteractPromptUI : MonoBehaviour
{
    public static InteractPromptUI Instance { get; private set; }

    [Header("UI refs")]
    public CanvasGroup rootCanvasGroup;         // root that we fade in/out
    public TextMeshProUGUI promptText;          // the prompt text (e.g. "Press E to talk")
    public Image iconImage;                     // optional icon (E key sprite)
    public float fadeDuration = 0.12f;
    public Vector2 appearScale = Vector2.one;
    public Vector2 hiddenScale = new Vector2(0.9f, 0.9f);

    [Header("Defaults")]
    public string defaultPrompt = "Press E to interact";

    Coroutine currentCoroutine;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // safe defaults if inspector not set
        if (rootCanvasGroup == null) rootCanvasGroup = GetComponent<CanvasGroup>();
        if (rootCanvasGroup == null)
        {
            Debug.LogWarning("InteractPromptUI: missing CanvasGroup reference.");
        }
        if (promptText == null) Debug.LogWarning("InteractPromptUI: missing promptText (TMP).");
        // start hidden
        if (rootCanvasGroup != null)
        {
            rootCanvasGroup.alpha = 0f;
            rootCanvasGroup.interactable = false;
            rootCanvasGroup.blocksRaycasts = false;
            transform.localScale = hiddenScale;
        }
    }

    // Primary API used by NPCs: show / hide prompt with optional custom text
    public void ShowInteractPrompt(bool show, string text = null)
    {
        if (show)
        {
            string t = string.IsNullOrEmpty(text) ? defaultPrompt : text;
            if (promptText != null) promptText.text = t;
            if (currentCoroutine != null) StopCoroutine(currentCoroutine);
            currentCoroutine = StartCoroutine(FadeIn());
        }
        else
        {
            if (currentCoroutine != null) StopCoroutine(currentCoroutine);
            currentCoroutine = StartCoroutine(FadeOut());
        }
    }

    // Convenience API: show for a short duration then hide
    public void ShowTemporary(string text, float duration = 1.5f)
    {
        if (currentCoroutine != null) StopCoroutine(currentCoroutine);
        currentCoroutine = StartCoroutine(ShowTemporaryCoroutine(text, duration));
    }

    IEnumerator ShowTemporaryCoroutine(string text, float dur)
    {
        ShowInteractPrompt(true, text);
        yield return new WaitForSeconds(dur);
        ShowInteractPrompt(false);
    }

    IEnumerator FadeIn()
    {
        if (rootCanvasGroup == null) yield break;
        float t = 0f;
        float startAlpha = rootCanvasGroup.alpha;
        Vector3 startScale = transform.localScale;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / fadeDuration);
            rootCanvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, p);
            transform.localScale = Vector3.Lerp(startScale, appearScale, Mathf.SmoothStep(0f, 1f, p));
            yield return null;
        }
        rootCanvasGroup.alpha = 1f;
        transform.localScale = appearScale;
        rootCanvasGroup.interactable = true;
        rootCanvasGroup.blocksRaycasts = true;
        currentCoroutine = null;
    }

    IEnumerator FadeOut()
    {
        if (rootCanvasGroup == null) yield break;
        float t = 0f;
        float startAlpha = rootCanvasGroup.alpha;
        Vector3 startScale = transform.localScale;
        rootCanvasGroup.interactable = false;
        rootCanvasGroup.blocksRaycasts = false;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / fadeDuration);
            rootCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, p);
            transform.localScale = Vector3.Lerp(startScale, hiddenScale, Mathf.SmoothStep(0f, 1f, p));
            yield return null;
        }
        rootCanvasGroup.alpha = 0f;
        transform.localScale = hiddenScale;
        currentCoroutine = null;
    }

    // Safe static wrappers so other code can call even if Instance missing
    public static void Show(string text = null)
    {
        if (Instance != null) Instance.ShowInteractPrompt(true, text);
    }

    public static void Hide()
    {
        if (Instance != null) Instance.ShowInteractPrompt(false, null);
    }

    public static void ShowTemp(string text, float dur = 1.5f)
    {
        if (Instance != null) Instance.ShowTemporary(text, dur);
    }
}

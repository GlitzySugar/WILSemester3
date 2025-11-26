// PersistentPlateUI_Patched.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections;


[RequireComponent(typeof(Canvas))]
public class PersistentPlateUI : MonoBehaviour
{
    public static PersistentPlateUI Instance { get; private set; }

    [Header("UI References")]
    public Image plateImage;
    public Text secondsText;

    [Header("Settings")]
    public bool persistAcrossScenes = true;
    public int topSortingOrder = 1000;
    public bool alwaysVisible = true;

    [Tooltip("If true the HUD will NOT block pointer events (recommended). Toggle if you need the HUD to capture input temporarily.")]
    public bool allowPointerEvents = false;

    Canvas _canvas;
    CanvasGroup _cg;
    private bool _subscribed = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _canvas = GetComponent<Canvas>();
        if (_canvas != null)
        {
            _canvas.sortingOrder = topSortingOrder;
            // prefer Screen Space - Overlay for HUD
            if (_canvas.renderMode == RenderMode.WorldSpace)
                Debug.LogWarning("PersistentPlateUI: Canvas is WorldSpace. Consider Screen Space - Overlay for HUD.");
        }

        if (persistAcrossScenes)
            DontDestroyOnLoad(gameObject);

      
        _cg = GetComponent<CanvasGroup>();
        if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();

        // Default: don't block other UI to allow interaction through HUD
        _cg.blocksRaycasts = allowPointerEvents;
        _cg.interactable = allowPointerEvents;

       
        DisableGraphicRaycastTargets();

        if (plateImage == null)
            Debug.LogWarning("PersistentPlateUI: plateImage is not assigned in inspector.");
    }

    private void OnEnable()
    {
        TrySubscribe();
        StartCoroutine(WaitForStarvationThenSubscribe());
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        if (Instance == this) Instance = null;
    }

    IEnumerator WaitForStarvationThenSubscribe()
    {
        if (_subscribed) yield break;
        float timeout = 5f;
        float t = 0f;
        while (!_subscribed && StarvationSystem.Instance == null && t < timeout)
        {
            t += Time.deltaTime;
            yield return null;
        }
        TrySubscribe();
    }

    void TrySubscribe()
    {
        if (_subscribed) return;
        if (StarvationSystem.Instance == null) return;

        StarvationSystem.Instance.OnHungerChanged += OnHungerChanged;
        StarvationSystem.Instance.OnSeverityChanged += OnSeverityChanged;

   
        OnHungerChangedSafe(StarvationSystem.Instance.GetFill());
        OnSeverityChangedSafe(StarvationSystem.Instance.GetSeverity());

        _subscribed = true;
    }

    void Unsubscribe()
    {
        if (!_subscribed) return;
        if (StarvationSystem.Instance != null)
        {
            StarvationSystem.Instance.OnHungerChanged -= OnHungerChanged;
            StarvationSystem.Instance.OnSeverityChanged -= OnSeverityChanged;
        }
        _subscribed = false;
    }

  
    public void TemporarilyBlockInput(bool block)
    {
        if (_cg == null) _cg = GetComponent<CanvasGroup>();
        if (_cg != null)
        {
            _cg.blocksRaycasts = block;
            _cg.interactable = block;
        }
    }

    // Ensures the Image/Text don't block pointer events (safe default)
    void DisableGraphicRaycastTargets()
    {
        if (plateImage != null)
        {
            plateImage.raycastTarget = false;
        }

        if (secondsText != null)
        {
           
            secondsText.raycastTarget = false;
        }
    }


    private void OnHungerChanged(float fill) => OnHungerChangedSafe(fill);

    private void OnHungerChangedSafe(float fill)
    {
        if (plateImage == null)
        {
            Debug.LogWarning("PersistentPlateUI: plateImage reference lost or destroyed. Unsubscribing to avoid exceptions.");
            Unsubscribe();
            return;
        }

        plateImage.fillAmount = Mathf.Clamp01(fill);

        if (secondsText != null && StarvationSystem.Instance != null)
        {
            if (secondsText == null)
            {
                Debug.LogWarning("PersistentPlateUI: secondsText reference lost or destroyed.");
            }
            else
            {
                secondsText.text = Mathf.CeilToInt(StarvationSystem.Instance.GetSecondsRemaining()).ToString() + "s";
            }
        }
    }

    private void OnSeverityChanged(HungerSeverity severity) => OnSeverityChangedSafe(severity);

    private void OnSeverityChangedSafe(HungerSeverity severity)
    {
        if (plateImage == null)
        {
            Debug.LogWarning("PersistentPlateUI: plateImage reference lost or destroyed during severity change. Unsubscribing.");
            Unsubscribe();
            return;
        }

        switch (severity)
        {
            case HungerSeverity.Full:
                plateImage.color = Color.white;
                break;
            case HungerSeverity.Hungry:
                plateImage.color = new Color(1f, 0.85f, 0.5f);
                break;
            case HungerSeverity.Starving:
                plateImage.color = new Color(1f, 0.6f, 0.6f);
                break;
        }
    }

  
    public void SetVisible(bool visible)
    {
        if (alwaysVisible) return;
        gameObject.SetActive(visible);
    }
}

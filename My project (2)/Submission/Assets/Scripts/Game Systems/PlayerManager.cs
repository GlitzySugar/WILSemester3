using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(CharacterController))]
public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    [Header("Movement")]
    public float baseMoveSpeed = 5f;
    public float gravity = -9.81f;
    public Transform cameraTransform; // optional: movement relative to camera
    public bool enableMovement = true;

    [Header("Optional")]
    public Animator animator; // optional, if you want to drive animations
    public string equip = null;

    [Header("Eating / Inventory")]
    [Tooltip("Starting food (prefab reference + count).")]
    public List<FoodStack> startingFood = new List<FoodStack>();
    public AudioClip eatSound;
    public float eatActionDelay = 0.5f;

    // Events for UI binding
    [Serializable] public class FloatEvent : UnityEvent<float> { } // for hunger fill (0..1)
    public FloatEvent OnHungerFillChanged;
    public UnityEvent OnInventoryChanged;
    public UnityEvent OnMiniGameStarted;
    public UnityEvent OnMiniGameEnded;
    public UnityEvent OnPlayerAte;

    // runtime
    private CharacterController cc;
    private Vector3 velocity;
    private bool inputEnabled = true;
    private Dictionary<string, int> inventory = new Dictionary<string, int>();

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        Instance = this;

        cc = GetComponent<CharacterController>();

        // initialize inventory from startingFood
        inventory.Clear();
        foreach (var fs in startingFood)
        {
            if (fs.foodPrefab == null) continue;
            string key = GetFoodKey(fs.foodPrefab);
            if (!inventory.ContainsKey(key)) inventory[key] = 0;
            inventory[key] += fs.count;
        }
    }

    private void OnEnable()
    {
        if (StarvationSystem.Instance != null)
        {
            StarvationSystem.Instance.OnHungerChanged += HandleHungerChanged;
            StarvationSystem.Instance.OnSeverityChanged += HandleSeverityChanged;
        }
    }

    private void OnDisable()
    {
        if (StarvationSystem.Instance != null)
        {
            StarvationSystem.Instance.OnHungerChanged -= HandleHungerChanged;
            StarvationSystem.Instance.OnSeverityChanged -= HandleSeverityChanged;
        }
    }

    private void Start()
    {
        // initial UI update
        if (StarvationSystem.Instance != null)
            HandleHungerChanged(StarvationSystem.Instance.GetFill());
        OnInventoryChanged?.Invoke();
    }

    void Update()
    {
        if (inputEnabled && enableMovement)
            HandleMovement();
    }

    #region Movement
    private void HandleMovement()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 dir = new Vector3(h, 0f, v);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        float multiplier = 1f;
        if (StarvationSystem.Instance != null) multiplier = StarvationSystem.Instance.GetMovementSpeedMultiplier();

        Vector3 moveDir;
        if (cameraTransform != null)
        {
            Vector3 forward = cameraTransform.forward;
            forward.y = 0; forward.Normalize();
            Vector3 right = cameraTransform.right;
            right.y = 0; right.Normalize();
            moveDir = forward * dir.z + right * dir.x;
        }
        else
        {
            moveDir = transform.TransformDirection(dir);
        }

        Vector3 move = moveDir * baseMoveSpeed * multiplier;
        cc.Move(move * Time.deltaTime);

        // gravity
        velocity.y += gravity * Time.deltaTime;
        cc.Move(velocity * Time.deltaTime);

        // animator optional
        if (animator != null)
        {
            float speed = new Vector3(move.x, 0, move.z).magnitude;
            animator.SetFloat("moveSpeed", speed);
        }
    }
    #endregion

    #region Inventory & Eating
    [Serializable]
    public class FoodStack
    {
        public GameObject foodPrefab;
        public int count = 1;
    }

    private string GetFoodKey(GameObject foodPrefab)
    {
        return foodPrefab.name;
    }

    public void AddFoodToInventory(GameObject foodPrefab, int count = 1)
    {
        if (foodPrefab == null) return;
        string key = GetFoodKey(foodPrefab);
        if (!inventory.ContainsKey(key)) inventory[key] = 0;
        inventory[key] += count;
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// Eat a food by providing the prefab reference (as used in startingFood). Returns true if eaten.
    /// </summary>
    public bool EatFoodByPrefab(GameObject foodPrefab)
    {
        if (foodPrefab == null) return false;
        string key = GetFoodKey(foodPrefab);
        if (!inventory.ContainsKey(key) || inventory[key] <= 0) return false;

        inventory[key]--;
        OnInventoryChanged?.Invoke();

        // read hunger restore seconds from FoodItem if available
        var fi = foodPrefab.GetComponent<FoodItem>();
        float restore = fi != null ? fi.hungerRestoreSeconds : 30f;

        StartCoroutine(PerformEat(restore));
        return true;
    }

    /// <summary>
    /// Eat by key (string). Useful for UI. Returns true if eaten.
    /// </summary>
    public bool EatFoodByKey(string foodKey)
    {
        if (!inventory.ContainsKey(foodKey) || inventory[foodKey] <= 0) return false;
        inventory[foodKey]--;
        OnInventoryChanged?.Invoke();
        StartCoroutine(PerformEat(30f)); // fallback restore
        return true;
    }

    private IEnumerator PerformEat(float restoreSeconds)
    {
        bool prevInput = inputEnabled;
        inputEnabled = false;

        if (animator != null) animator.SetTrigger("eat");
        if (eatSound != null) AudioSource.PlayClipAtPoint(eatSound, transform.position);

        yield return new WaitForSeconds(eatActionDelay);

        if (StarvationSystem.Instance != null) StarvationSystem.Instance.AddTime(restoreSeconds);

        OnPlayerAte?.Invoke();

        inputEnabled = prevInput;
    }

    public Dictionary<string, int> GetInventorySnapshot()
    {
        return new Dictionary<string, int>(inventory);
    }
    #endregion

    #region Mini-game integration
    /// <summary>
    /// Start a random mini-game and automatically disable player input while it runs.
    /// </summary>
    public void StartRandomMiniGameAndDisableInput()
    {
        if (MiniGameManager.Instance == null)
        {
            Debug.LogWarning("PlayerManager: No MiniGameManager instance found.");
            return;
        }

        StartCoroutine(StartMiniGameRoutine());
    }

    private IEnumerator StartMiniGameRoutine()
    {
        inputEnabled = false;
        OnMiniGameStarted?.Invoke();

        MiniGameManager.Instance.StartRandomMiniGame();

        // wait one frame for instantiation
        yield return null;

        Transform mgParent = MiniGameManager.Instance.transform;
        Transform newChild = null;
        if (mgParent.childCount > 0)
            newChild = mgParent.GetChild(mgParent.childCount - 1);

        if (newChild == null)
        {
            // fallback: re-enable input after a short delay
            yield return new WaitForSeconds(2f);
            inputEnabled = true;
            OnMiniGameEnded?.Invoke();
            yield break;
        }

        // wait until the mini-game object is destroyed
        while (newChild != null)
        {
            if (newChild.gameObject == null) break;
            yield return null;
        }

        // small safety frame
        yield return null;

        inputEnabled = true;
        OnMiniGameEnded?.Invoke();
    }
    #endregion

    #region Starvation callbacks & UI wiring
    private void HandleHungerChanged(float fill)
    {
        OnHungerFillChanged?.Invoke(fill);
        if (animator != null)
            animator.SetFloat("hungerFill", 1f - fill); // invert if you want 0=full, 1=starving
    }

    private void HandleSeverityChanged(HungerSeverity severity)
    {
        if (animator == null) return;
        animator.SetBool("isHungry", severity == HungerSeverity.Hungry);
        animator.SetBool("isStarving", severity == HungerSeverity.Starving);
    }
    #endregion

    #region Pickup helper
    // Example: if the player walks over a FoodItem world instance it will add it to inventory (and destroy world object).
    private void OnTriggerEnter(Collider other)
    {
        var fi = other.GetComponent<FoodItem>();
        if (fi != null)
        {
            AddFoodToInventory(other.gameObject, 1);
            Destroy(other.gameObject);
        }
    }
    #endregion

    #region Debug / Utility
    [ContextMenu("Print Inventory")]
    public void PrintInventory()
    {
        foreach (var kv in inventory) Debug.Log($"{kv.Key} x{kv.Value}");
    }
    #endregion
}

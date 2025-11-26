using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// GogoNPC updated: guards against stale mini-game references so "Finish the task first!"
/// doesn't block forever when the mini-game object was destroyed unexpectedly.
/// Also provides ForceResetMiniGameState() to manually clear state in editor/runtime.
/// </summary>
public class GogoNPC : MonoBehaviour
{
    [Header("Detection")]
    public Transform player;
    public float interactRange = 3f;
    public KeyCode interactKey = KeyCode.E;

    [Header("MiniGames")]
    public GameObject sweepMiniGamePrefab;
    public GameObject bucketMiniGamePrefab;

    [Header("Drop & Delivery")]
    public DropSpot dropSpot;

    [Header("Narrative/Transition")]
    public FadeController fadeController;
    private Scene currentScene;
    private int currentIndex;
    public Transform schoolSpawnPoint;
    public float fadeDuration = 1f;

    // runtime
    private IMiniGame runningMiniGameInterface;
    private object runningBucketMiniGame; // store as object to avoid missing type references
    private GameObject runningMiniGameGo;

    bool IsClose => player != null &&
                    Vector3.Distance(player.position, transform.position) <= interactRange;

    void Awake()
    {
         currentScene = SceneManager.GetActiveScene();
        currentIndex = currentScene.buildIndex;
        if (player == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p) player = p.transform;
        }
    }

    void Update()
    {
        if (!IsClose) return;

        if (Input.GetKeyDown(interactKey))
        {
            HandleInteract();
        }
    }

    // Public helper to clear stuck state (callable from other scripts / inspector via editor button)
    public void ForceResetMiniGameState()
    {
        runningMiniGameInterface = null;
        runningBucketMiniGame = null;
        if (runningMiniGameGo != null)
        {
            try { Destroy(runningMiniGameGo); } catch { }
            runningMiniGameGo = null;
        }
        WorldSpacePrompt.Ensure(gameObject).Show("Reset mini-game state.");
    }

    private void HandleInteract()
    {
        var prompt = WorldSpacePrompt.Ensure(gameObject);

        // --- SANITY CHECK: auto-clear stale references ---
        if (runningMiniGameInterface != null && runningMiniGameGo == null)
        {
            runningMiniGameInterface = null;
            runningBucketMiniGame = null;
            prompt.Show("(Recovered) Ready for tasks.");
            // continue - we recovered
        }
        else if (runningMiniGameInterface != null && runningMiniGameGo != null)
        {
            // If the mini-game object exists but exposes an IsRunning flag and it's false, clear it.
            var comp = runningMiniGameGo.GetComponent(runningMiniGameInterface.GetType());
            if (comp != null)
            {
                var isRunningProp = comp.GetType().GetProperty("IsRunning", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (isRunningProp != null)
                {
                    try
                    {
                        object val = isRunningProp.GetValue(comp, null);
                        if (val is bool b && !b)
                        {
                            runningMiniGameInterface = null;
                            runningBucketMiniGame = null;
                            try { Destroy(runningMiniGameGo); } catch { }
                            runningMiniGameGo = null;
                            prompt.Show("(Recovered) Ready for tasks.");
                        }
                    }
                    catch { }
                }
            }
        }

        // --- IMPORTANT CHANGE: check for bucket hand-in FIRST ---
        // If a bucket is placed in the dropSpot or exists near Gogo, accept it immediately.
        GameObject bucketGO = TryGetPlacedBucketFromDropSpot();
        if (bucketGO == null) bucketGO = FindBucketInRange();

        if (bucketGO != null)
        {
            // If DropSpot expects the player to stand in spot with the bucket (player-carry case), keep that check:
            if (runningBucketMiniGame != null && dropSpot != null)
            {
                dropSpot.EvaluatePlayerCarryState();
                if (dropSpot.IsPlayerWithBucket)
                {
                    // player still carrying it — ask them to stand in the spot
                    prompt.Show("Stand here with the bucket.");
                    return;
                }
            }

            // Accept bucket immediately (no water-level checks)
            StartCoroutine(HandleBucketGiveSequence(bucketGO));
            return;
        }

        // --- If no bucket nearby, only then enforce "finish the task first" ---
        if (runningMiniGameInterface != null)
        {
            prompt.Show("Finish the task first!");
            return;
        }

        // No bucket and no running mini-game -> start a new random task
        bool chooseBucket = UnityEngine.Random.value < 0.5f;

        if (chooseBucket && bucketMiniGamePrefab != null)
        {
            StartBucketMiniGame();
            prompt.Show("Fetch water and bring it back.");
        }
        else if (sweepMiniGamePrefab != null)
        {
            StartSweepMiniGame();
            prompt.Show("Sweep the house.");
        }
        else
        {
            prompt.Show("I have no tasks right now.");
        }
    }

    private GameObject TryGetPlacedBucketFromDropSpot()
    {
        if (dropSpot == null) return null;

        string[] candidateMethods = { "GetPlacedBucket", "GetBucketInSpot", "GetCarriedBucket", "GetBucket", "GetPlacedObject" };

        foreach (var name in candidateMethods)
        {
            var mi = dropSpot.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mi != null)
            {
                try
                {
                    var res = mi.Invoke(dropSpot, null);
                    if (res is GameObject go) return go;
                }
                catch { /* ignore and continue */ }
            }
        }

        string[] candidateProps = { "placedObject", "placedBucket", "bucketInSpot", "currentObject" };
        foreach (var name in candidateProps)
        {
            var p = dropSpot.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
            if (p != null)
            {
                try
                {
                    var res = p.GetValue(dropSpot, null);
                    if (res is GameObject go) return go;
                }
                catch { }
            }

            var f = dropSpot.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
            if (f != null)
            {
                try
                {
                    var res = f.GetValue(dropSpot);
                    if (res is GameObject go) return go;
                }
                catch { }
            }
        }

        return null;
    }

    private GameObject FindBucketInRange()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, interactRange);
        foreach (var c in hits)
        {
            var go = c.transform.root.gameObject;

            if (go.CompareTag("Bucket"))
                return go;

            string[] candidateComponentNames = { "WaterBucket", "BucketMiniGame", "BucketController", "BucketBehaviour", "BucketProps" };
            foreach (var name in candidateComponentNames)
            {
                var comp = go.GetComponent(name);
                if (comp != null) return go;
            }

            var comps = go.GetComponents<Component>();
            foreach (var comp in comps)
            {
                if (comp == null) continue;
                var interfaces = comp.GetType().GetInterfaces();
                foreach (var i in interfaces)
                    if (i.Name.Equals("IBucket", StringComparison.OrdinalIgnoreCase))
                        return go;
            }
        }

        return null;
    }

    private void StartSweepMiniGame()
    {
        // instantiate and start
        runningMiniGameGo = Instantiate(sweepMiniGamePrefab);
        if (runningMiniGameGo == null) return;

        runningMiniGameInterface = runningMiniGameGo.GetComponent<IMiniGame>();
        if (runningMiniGameInterface == null)
        {
            // fallback: clear and destroy if prefab missing interface
            Destroy(runningMiniGameGo);
            runningMiniGameGo = null;
            return;
        }

        // if the IMiniGame.StartMiniGame requires a callback, assume the interface matches your project
        runningMiniGameInterface.StartMiniGame(OnMiniGameComplete);
    }

    private void StartBucketMiniGame()
    {
        runningMiniGameGo = Instantiate(bucketMiniGamePrefab);
        if (runningMiniGameGo == null) return;

        runningMiniGameInterface = runningMiniGameGo.GetComponent<IMiniGame>();
        runningBucketMiniGame = runningMiniGameGo.GetComponent("BucketMiniGame"); // keep as object to avoid compile issues if type missing

        if (runningMiniGameInterface == null)
        {
            Destroy(runningMiniGameGo);
            runningMiniGameGo = null;
            runningBucketMiniGame = null;
            return;
        }

        runningMiniGameInterface.StartMiniGame(OnMiniGameComplete);
    }

    private void OnMiniGameComplete(MiniGameResult result)
    {
        // ensure cleanup of refs
        runningMiniGameInterface = null;
        runningBucketMiniGame = null;

        if (runningMiniGameGo != null)
        {
            try { Destroy(runningMiniGameGo); } catch { }
            runningMiniGameGo = null;
        }

        if (result.won)
        {
            StartCoroutine(NarrativeTransitionToSchool());
        }
        else
        {
            WorldSpacePrompt.Ensure(gameObject).Show("Try again later.");
        }
    }

    private IEnumerator HandleBucketGiveSequence(GameObject bucket)
    {
        WorldSpacePrompt.Ensure(gameObject).Show("Thank you for the water!");

        // record journal entry: Fetching Bucket successful
        string hungerLevel = "Unknown";
        var hungerComp = FindObjectOfType<SimpleHungerStub>() as IPlayerHunger;
        if (hungerComp != null) hungerLevel = hungerComp.GetHungerState().ToString();
        else
        {
            // try to find any IPlayerHunger implementer in scene
            var all = FindObjectsOfType<MonoBehaviour>();
            foreach (var mb in all)
            {
                if (mb is IPlayerHunger ih)
                {
                    hungerLevel = ih.GetHungerState().ToString();
                    break;
                }
            }
        }

        JournalManager.Instance?.AddEntry("Fetching Bucket", "Task Successful", hungerLevel);
        yield return new WaitForSeconds(1f);


        // If we have a runningBucketMiniGame, try to call its win method (if present)
        if (runningBucketMiniGame != null)
        {
            TryCallMethodIfExists(runningBucketMiniGame, "CompleteAsWin", bucket);
        }
        else
        {
            TryCallMethodIfExists(runningMiniGameInterface, "CompleteAsWin", bucket);
            // fallback: destroy the bucket object so it's removed from the world
            try { Destroy(bucket); } catch { }
        }

        dropSpot?.EvaluatePlayerCarryState();

        StartCoroutine(NarrativeTransitionToSchool());
    }

    private void TryCallMethodIfExists(object obj, string methodName, GameObject bucketArg)
    {
        if (obj == null) return;
        var mi = obj.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (mi != null)
        {
            try
            {
                var parameters = mi.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(GameObject))
                    mi.Invoke(obj, new object[] { bucketArg });
                else if (parameters.Length == 0)
                    mi.Invoke(obj, null);
            }
            catch { }
        }
    }

    private IEnumerator NarrativeTransitionToSchool()
    {
        if (fadeController != null) yield return fadeController.FadeOut(fadeDuration);

        if (player != null )
        {
            if(currentIndex == 1) { SceneManager.LoadScene(2); }
            else if (currentIndex == 6) { SceneManager.LoadScene(0); }
           
        }

        if (fadeController != null) yield return fadeController.FadeIn(fadeDuration);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}

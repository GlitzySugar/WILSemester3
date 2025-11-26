using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Simple manager for the fetch-water mini-game.
/// - configure requiredPercentToWin (fraction of bucket max water required on deposit)
/// - configure requiredNumberOfTrips (how many successful deposits required)
/// - optional time limit and starvation toggling
/// </summary>
public class WaterMiniGameManager : MonoBehaviour
{
    [Header("Win Conditions")]
    [Tooltip("Fraction of bucket's max water that must be returned on a trip (0..1).")]
    public float requiredPercentToWin = 0.5f; // 50%

    [Tooltip("Number of successful trips required to win.")]
    public int requiredNumberOfTrips = 1;

    [Header("Time")]
    public float timeLimit = 0f; // 0 = no limit

    [Header("Starvation (optional)")]
    public bool enableStarvationWhileActive = false;
    public WaterBucket[] bucketsToAffect;

    [Header("Events")]
    public UnityEvent onGameStart;
    public UnityEvent onGameEnd;
    public UnityEvent onGameWin;
    public UnityEvent onGameLose;
    public UnityEvent<float> onDepositReceived;

    // internals
    private int successfulTrips = 0;
    private int depositsMade = 0;
    private bool gameActive = false;
    private float timeRemaining = 0f;

    public bool IsActive => gameActive;
    public float TimeRemaining => timeRemaining;

    private void Start()
    {
        timeRemaining = timeLimit;
    }

    private void Update()
    {
        if (!gameActive) return;
        if (timeLimit > 0f)
        {
            timeRemaining -= Time.deltaTime;
            if (timeRemaining <= 0f)
            {
                timeRemaining = 0f;
                EndGame(false);
            }
        }
    }

    public void StartGame()
    {
        successfulTrips = 0;
        depositsMade = 0;
        gameActive = true;
        timeRemaining = timeLimit;

        if (enableStarvationWhileActive)
        {
            foreach (var b in bucketsToAffect) if (b != null) b.StartStarvation();
        }

        onGameStart?.Invoke();
    }

    public void EndGame(bool won)
    {
        gameActive = false;

        if (enableStarvationWhileActive)
        {
            foreach (var b in bucketsToAffect) if (b != null) b.StopStarvation();
        }

        onGameEnd?.Invoke();

        if (won) onGameWin?.Invoke();
        else onGameLose?.Invoke();
    }

    /// <summary>
    /// Call this from WaterReturnPoint.onDeposit (or directly). 
    /// Pass the delivered amount and optionally the bucket's max water (if different from default).
    /// </summary>
    public void OnDeposit(float amountDelivered, float bucketMax = 100f)
    {
        if (!gameActive) return;

        depositsMade++;
        onDepositReceived?.Invoke(amountDelivered);

        float fraction = 0f;
        if (bucketMax > 0f) fraction = amountDelivered / bucketMax;

        if (fraction >= requiredPercentToWin)
        {
            successfulTrips++;
            if (successfulTrips >= requiredNumberOfTrips)
            {
                EndGame(true);
            }
            else
            {
                // continue until required trips reached
            }
        }
        else
        {
            // treat as immediate failure. Modify this behavior if you want partial scoring or retries.
            EndGame(false);
        }
    }
}

using System;
using UnityEngine;

public class BucketMiniGame : MonoBehaviour, IMiniGame
{
    private Action<MiniGameResult> onComplete;
    private bool running = false;
    private bool bucketDelivered = false;

    // optional: references for UI or spawning
    public GameObject bucketPrefab; // if you spawn a bucket
    public Transform bucketSpawnPoint;

    public void StartMiniGame(Action<MiniGameResult> onComplete)
    {
        this.onComplete = onComplete ?? throw new ArgumentNullException(nameof(onComplete));
        running = true;
        bucketDelivered = false;

        // spawn bucket if needed
        if (bucketPrefab != null && bucketSpawnPoint != null)
        {
            Instantiate(bucketPrefab, bucketSpawnPoint.position, bucketSpawnPoint.rotation);
        }

        Debug.Log("BucketMiniGame started: get water and bring it to the drop spot in front of Gogo.");
    }

    public void ResetMiniGame()
    {
        running = false;
        bucketDelivered = false;
    }

    /// <summary>
    /// Called externally by DropSpot or Gogo when they detect the bucket in place.
    /// </summary>
    public void MarkBucketDelivered()
    {
        if (!running) return;
        bucketDelivered = true;
        Debug.Log("BucketMiniGame: bucket marked delivered.");
    }

    /// <summary>
    /// Called when the NPC confirms the end dialogue (player interacts with Gogo while dropSpot.HasBucket)
    /// GogoInteraction will call this to finish the mini-game.
    /// </summary>
    public void CompleteAsWin(GameObject bucketToDestroy)
    {
        if (!running) return;
        running = false;

        // Destroy bucket
        if (bucketToDestroy != null)
        {
            Destroy(bucketToDestroy);
        }

        var result = new MiniGameResult
        {
            miniGameId = gameObject.name,
            won = true,
            score = 1
        };

        onComplete?.Invoke(result);
    }


    public void CompleteAsLose()
    {
        if (!running) return;
        running = false;
        var result = new MiniGameResult
        {
            miniGameId = gameObject.name,
            won = false,
            score = 0
        };
        onComplete?.Invoke(result);
    }

    // expose a quick state check
    public bool IsBucketDelivered() => bucketDelivered;
}

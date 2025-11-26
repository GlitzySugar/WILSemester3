using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class DustContainerPoller : MonoBehaviour
{
    public float pollInterval = 1f;
    public string dustTag = "";
    public UnityEvent onAllCleared;
    public string taskName = "SweepHouse";
    public bool recordAsSuccess = true;

    bool completed = false;

    void Start()
    {
        StartCoroutine(PollCoroutine());
    }

    IEnumerator PollCoroutine()
    {
        while (!completed)
        {
            int count = 0;
            if (!string.IsNullOrEmpty(dustTag))
            {
                var tagged = GameObject.FindGameObjectsWithTag(dustTag);
                foreach (var g in tagged) if (g != null && g.transform.IsChildOf(transform)) count++;
            }
            else
            {
                // count active children
                foreach (Transform t in transform) if (t != null && t.gameObject.activeInHierarchy) count++;
            }

            if (count == 0)
            {
                completed = true;
                onAllCleared?.Invoke();

                if (recordAsSuccess && JournalManager.Instance != null)
                    JournalManager.Instance.RecordMiniGameResult(taskName, true);

                if (DaySystem.Instance != null)
                    DaySystem.Instance.OnMiniGameCompleted();

                Debug.Log("[DustContainerPoller] All dust cleared; recorded and invoked.");
                yield break;
            }

            yield return new WaitForSeconds(pollInterval);
        }
    }
}

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MashEController : MonoBehaviour
{
    [Header("UI")]
    public Image meterFill;
    public float maxMeter = 100f;
    public float decayPerSecond = 20f;
    public float requiredToWin = 60f; // threshold to consider a successful mash

    [HideInInspector] public Action<bool> onMashComplete;
    [HideInInspector] public bool isComplete = false;

    float current = 0f;

    public void ResetMeter()
    {
        current = 0f;
        isComplete = false;
        if (meterFill) meterFill.fillAmount = 0f;
    }

    public void StartMash()
    {
        StopAllCoroutines();
        StartCoroutine(MashLoop());
    }

    IEnumerator MashLoop()
    {
        while (!isComplete)
        {
            // input
            if (Input.GetKeyDown(KeyCode.E))
            {
                current += 8f; // each tap adds (tweak)
                current = Mathf.Min(current, maxMeter);
            }

            // decay
            current -= decayPerSecond * Time.deltaTime;
            current = Mathf.Max(0f, current);

            if (meterFill) meterFill.fillAmount = current / maxMeter;

            // finish when player hits Enter or meter full OR time runs out
            if (current >= maxMeter)
            {
                isComplete = true;
                bool success = current >= requiredToWin;
                onMashComplete?.Invoke(success);
                yield break;
            }

            // also allow player to confirm with E after mashing enough:
            if (Input.GetKeyDown(KeyCode.Return))
            {
                isComplete = true;
                bool success = current >= requiredToWin;
                onMashComplete?.Invoke(success);
                yield break;
            }

            yield return null;
        }
    }
}

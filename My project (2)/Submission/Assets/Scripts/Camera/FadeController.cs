using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FadeController : MonoBehaviour
{
    public Image blackImage;
    private void Awake()
    {
        if (blackImage == null) Debug.LogError("FadeController needs a reference to a fullscreen black Image.");
    }

    public IEnumerator FadeOut(float duration)
    {
        if (blackImage == null) yield break;
        float t = 0f;
        Color c = blackImage.color;
        while (t < duration)
        {
            t += Time.deltaTime;
            c.a = Mathf.Clamp01(t / duration);
            blackImage.color = c;
            yield return null;
        }
        c.a = 1f;
        blackImage.color = c;
        yield return null;
    }

    public IEnumerator FadeIn(float duration)
    {
        if (blackImage == null) yield break;
        float t = 0f;
        Color c = blackImage.color;
        while (t < duration)
        {
            t += Time.deltaTime;
            c.a = Mathf.Clamp01(1f - (t / duration));
            blackImage.color = c;
            yield return null;
        }
        c.a = 0f;
        blackImage.color = c;
        yield return null;
    }
}

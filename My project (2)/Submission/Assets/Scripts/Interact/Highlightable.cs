using UnityEngine;

public class Highlightable : MonoBehaviour
{
    private Renderer objectRenderer;
    private Color originalColor;
    public Color highlightColor = Color.yellow;

    void Start()
    {
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer != null)
            originalColor = objectRenderer.material.color;
    }

    public void Highlight()
    {
        if (objectRenderer != null)
            objectRenderer.material.color = highlightColor;
    }

    public void RemoveHighlight()
    {
        if (objectRenderer != null)
            objectRenderer.material.color = originalColor;
    }
}

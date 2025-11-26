using UnityEngine;

public class PlayerInteract : MonoBehaviour
{
    public float interactDistance = 3f;
    public LayerMask interactLayer;

    private Highlightable currentHighlight;

    void Update()
    {
        HandleHighlighting();

        if (Input.GetKeyDown(KeyCode.E))
        {
            Interact();
        }
    }

    void HandleHighlighting()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactDistance, interactLayer))
        {
            Highlightable highlightable = hit.collider.GetComponent<Highlightable>();

            if (highlightable != null)
            {
                if (currentHighlight != highlightable)
                {
                    ClearHighlight();
                    currentHighlight = highlightable;
                    currentHighlight.Highlight();
                }
            }
            else
            {
                ClearHighlight();
            }
        }
        else
        {
            ClearHighlight();
        }
    }

    void ClearHighlight()
    {
        if (currentHighlight != null)
        {
            currentHighlight.RemoveHighlight();
            currentHighlight = null;
        }
    }

    void Interact()
    {
        if (currentHighlight != null)
        {
            IInteractable interactable = currentHighlight.GetComponent<IInteractable>();
            if (interactable != null)
            {
                interactable.Interact();
            }
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// DialogueManager (key-oriented navigation)
/// - Press `nextKey` (default: E) to advance.
/// - Still supports clicking the `nextButton` if assigned.
/// - Keeps your queue logic and scene load when finished.
/// </summary>
public class DialogueManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] Canvas dialogueCanvas = null;
    [SerializeField] Text dialogueText = null;
    [SerializeField] Text dialogueName = null;
    [SerializeField] RawImage dialogueTexture = null;

    [Header("Content")]
    [SerializeField] DialogueScriptable dialogueObject = null;

    [Header("Controls")]
    [Tooltip("Key the player can press to advance the dialogue.")]
    public KeyCode nextKey = KeyCode.E;

    [Tooltip("Optional Next button (still supported).")]
    public Button nextButton = null;

    // internal flag set either by button listener or cleared after processed
    bool nextPressed = false;

    private void Start()
    {
        if (dialogueCanvas != null) dialogueCanvas.enabled = false;
        // attach button listener if assigned
        if (nextButton != null)
        {
            nextButton.onClick.AddListener(OnNextButtonClicked);
        }

        StartCoroutine(DisplayDialogueCoroutine());
    }

    private void OnDestroy()
    {
        if (nextButton != null)
            nextButton.onClick.RemoveListener(OnNextButtonClicked);
    }

    private void OnNextButtonClicked()
    {
        nextPressed = true;
    }

    IEnumerator DisplayDialogueCoroutine()
    {
        if (dialogueCanvas != null) dialogueCanvas.enabled = true;

        // Enqueue the dialogue content (defensive: clear existing queues first)
        if (dialogueObject == null)
        {
            Debug.LogWarning("[DialogueManager] dialogueObject is not assigned.");
            yield break;
        }

        // If your DialogueScriptable keeps persistent queues, ensure they are empty before enqueuing.
        // Here we enqueue only if the queue is empty to avoid duplicating items on repeated play.
        if (dialogueObject.dialogueQueue == null) dialogueObject.dialogueQueue = new System.Collections.Generic.Queue<string>();
        if (dialogueObject.dialogueNameQueue == null) dialogueObject.dialogueNameQueue = new System.Collections.Generic.Queue<string>();
        if (dialogueObject.dialogueQueueImage == null) dialogueObject.dialogueQueueImage = new System.Collections.Generic.Queue<Texture>();

        // Clear any existing queues (safer)
        dialogueObject.dialogueQueue.Clear();
        dialogueObject.dialogueNameQueue.Clear();
        dialogueObject.dialogueQueueImage.Clear();

        foreach (string dialogue in dialogueObject.dialogueStrings)
            dialogueObject.dialogueQueue.Enqueue(dialogue);

        foreach (string name in dialogueObject.dialogueNameStrings)
            dialogueObject.dialogueNameQueue.Enqueue(name);

        foreach (Texture tex in dialogueObject.dialogueImages)
            dialogueObject.dialogueQueueImage.Enqueue(tex);

        // Main loop: advance when nextKey pressed or nextButton clicked
        while (dialogueObject.dialogueQueue.Count > 0)
        {
            // Wait until key press or button click
            yield return StartCoroutine(WaitForNextInput());

            // Process the next queued item (defensive checks)
            if (dialogueObject.dialogueQueue.Count > 0)
                dialogueText.text = dialogueObject.dialogueQueue.Dequeue();
            else
                dialogueText.text = "";

            if (dialogueObject.dialogueNameQueue.Count > 0)
                dialogueName.text = dialogueObject.dialogueNameQueue.Dequeue();
            else
                dialogueName.text = "";

            if (dialogueObject.dialogueQueueImage.Count > 0)
                dialogueTexture.texture = dialogueObject.dialogueQueueImage.Dequeue();
            else
                dialogueTexture.texture = null;
        }

        // All dialogues consumed -> load next scene (your original behavior)
        if (dialogueObject.dialogueQueue.Count == 0)
        {
            // optional small delay to allow last line to be visible briefly
            yield return new WaitForSecondsRealtime(0.15f);

            // If you want a fade, hook into your FadeController here
            SceneManager.LoadScene(1);
        }

        if (dialogueCanvas != null) dialogueCanvas.enabled = false;
    }

    IEnumerator WaitForNextInput()
    {
        // Reset local flag
        nextPressed = false;

        // Wait until key pressed or button clicked
        while (true)
        {
            // Key press check (legacy input)
            if (Input.GetKeyDown(nextKey))
            {
                yield break;
            }

            // Also support Enter / Space optionally (uncomment if you want)
            // if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) yield break;

            // Button click check
            if (nextPressed)
            {
                nextPressed = false;
                yield break;
            }

            yield return null;
        }
    }
}

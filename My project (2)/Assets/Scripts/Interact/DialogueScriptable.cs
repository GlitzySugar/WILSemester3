using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(fileName = "DialogueObjct", menuName = "Dialogue/Race", order = 0)]
public class DialogueScriptable : ScriptableObject
{
    public List<string> dialogueStrings = new();
    public List<string> dialogueNameStrings = new();
    public List<Texture> dialogueImages = new();
    public Queue<string> dialogueQueue = new();
    public Queue<string> dialogueNameQueue = new();
    public Queue<Texture> dialogueQueueImage = new();
}

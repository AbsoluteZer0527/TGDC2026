using UnityEngine;

[System.Serializable]
public class DialogueLine
{
    [TextArea(1, 4)] public string text;
    [Tooltip("Check for the player's bubble. Leave unchecked for the customer's bubble.")]
    public bool playerSpeaks;
}

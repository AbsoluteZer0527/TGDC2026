using UnityEngine;

[System.Serializable]
public class DialogueLine
{
    [TextArea(1, 4)] public string text;
    [Tooltip("Check for the player's bubble. Leave unchecked for the customer's bubble.")]
    public bool playerSpeaks;
}

[System.Serializable]
public class KeyNpc
{
    public string displayName;
    [Min(1)] public int npcId = 1;
    public Sprite sprite;
}

public class NpcReferenceAttribute : PropertyAttribute
{
    public readonly bool includeRandom;
    public NpcReferenceAttribute(bool includeRandom) => this.includeRandom = includeRandom;
}

[System.Serializable]
public class KeyVisit
{
    [Min(1)] public int visitNumber = 1;
    [NpcReference(false)] public int npcId = 1;
}

[System.Serializable]
public class CustomerConversation
{
    [NpcReference(true)] public int npcId;
    public DialogueLine[] lines;
}

[System.Serializable]
public class BetweenCustomerDialogue
{
    [Min(1)] public int afterCustomer = 1;
    [TextArea(1, 4)] public string[] playerLines;
}

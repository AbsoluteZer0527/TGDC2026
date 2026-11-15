using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Kitchen/Dialogue")]
public sealed class KitchenDialogueData : ScriptableObject
{
    [Header("Greetings")]
    [Tooltip("Random customers say one nonblank entry from this list.")]
    [TextArea(1, 4)] public string[] randomGreetings = Array.Empty<string>();
    [Tooltip("A key NPC plays one matching conversation, in line order.")]
    public CustomerConversation[] keyGreetings = Array.Empty<CustomerConversation>();

    [Header("Farewells and personal dialogue")]
    [Tooltip("One matching conversation is chosen after serving. No match skips the farewell.")]
    public CustomerConversation[] customerEndings = Array.Empty<CustomerConversation>();
    [Tooltip("Player lines to play at the empty counter after the numbered customer leaves.")]
    public BetweenCustomerDialogue[] betweenCustomers = Array.Empty<BetweenCustomerDialogue>();
}

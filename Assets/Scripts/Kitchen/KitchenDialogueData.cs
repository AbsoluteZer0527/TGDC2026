using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Kitchen/Dialogue")]
public sealed class KitchenDialogueData : ScriptableObject
{
    [Header("Greetings")]
    [Tooltip("Used by random customers and by key NPCs without a written greeting.")]
    [TextArea(1, 4)] public string[] randomGreetings = Array.Empty<string>();
    [Tooltip("For each NPC, matching greetings play in list order on successive visits. Later visits repeat the last greeting.")]
    public CustomerConversation[] keyGreetings = Array.Empty<CustomerConversation>();

    [Header("Wrong order")]
    [Tooltip("One is picked at random when a served stew doesn't match the order. The customer then repeats the order and waits.")]
    [TextArea(1, 4)] public string[] wrongOrderLines =
    {
        "Hey, I don't think this is what I ordered. Could you please remake it?",
        "I'm sorry, but this seems wrong...",
    };

    [Header("Farewells and personal dialogue")]
    [Tooltip("Key NPC farewells follow visit order like greetings. Random customers pick a matching farewell at random. No match skips the farewell.")]
    public CustomerConversation[] customerEndings = Array.Empty<CustomerConversation>();
    [Tooltip("Player lines to play at the empty counter after the numbered customer leaves.")]
    public BetweenCustomerDialogue[] betweenCustomers = Array.Empty<BetweenCustomerDialogue>();

    [Header("Cooking for the third customer")]
    public DialogueLine[] forestUnlockLines;

    [Header("End of day: IRS audit")]
    [Tooltip("Key NPC roster ID for the inspector who closes the kitchen each day. 99 selects IRS. This visit does not count toward numbered customer visits.")]
    [NpcReference(false)] public int auditorNpcId = 99;
    [Tooltip("Used for the entrance on every visit and throughout a routine inspection.")]
    public Sprite auditArrivalSprite;
    [Tooltip("Played before the agent walks away and the night summary opens.")]
    public DialogueLine[] auditPassLines = { new DialogueLine { text = "..." } };
    [Header("IRS catches the player")]
    public Sprite auditFailSprite;
    [Tooltip("Played when hunt earnings exceed the laundering limit, before the jail screen.")]
    public DialogueLine[] auditFailLines = { new DialogueLine { text = "Something seems wrong here..." } };

    [Header("IRS eviction notice")]
    public Sprite evictionSprite;
    [Tooltip("Played when savings start and end the day below zero after rent.")]
    public DialogueLine[] evictionLines = { new DialogueLine { text = "Your rent is still overdue. I have to close the stand." } };
}

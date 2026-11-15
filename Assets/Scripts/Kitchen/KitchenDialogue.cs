using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
public sealed class KitchenDialogue : MonoBehaviour
{
    [SerializeField] KitchenDialogueData dialogueFile;

    [Header("Bubbles and playback")]
    [SerializeField] RectTransform orderBubble;
    [SerializeField] TMP_Text orderText;
    [SerializeField] RectTransform playerBubble;
    [SerializeField] TMP_Text playerText;
    [SerializeField] KitchenDialogueEffects dialogueEffects = new();

    Button npcDialogueButton, playerDialogueButton;
    Action onAdvanced;
    GameManager GM => GameManager.I;

    public void Initialize(Action onDialogueAdvanced)
    {
        onAdvanced = onDialogueAdvanced;
        npcDialogueButton = BindDialogueButton(orderBubble ? orderBubble.gameObject : orderText.gameObject);
        if (playerBubble && playerText) playerDialogueButton = BindDialogueButton(playerBubble.gameObject);
    }

    Button BindDialogueButton(GameObject bubble)
    {
        if (!bubble.TryGetComponent<Button>(out var button)) button = bubble.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(AdvanceDialogue);
        return button;
    }

    public void BeginGreeting()
    {
        if (GM.CurrentNpcId != 0)
        {
            GM.BeginCustomerDialogue(CustomerPhase.AtCounter, PickConversation(dialogueFile.keyGreetings));
            return;
        }

        var choices = dialogueFile.randomGreetings.Where(text => !string.IsNullOrWhiteSpace(text)).ToArray();
        var lines = choices.Length == 0 ? Array.Empty<DialogueLine>() :
            new[] { new DialogueLine { text = choices[Random.Range(0, choices.Length)] } };
        GM.BeginCustomerDialogue(CustomerPhase.AtCounter, lines);
    }

    public void BeginFarewell() =>
        GM.BeginCustomerDialogue(CustomerPhase.Farewell, PickConversation(dialogueFile.customerEndings));

    public void BeginPersonalDialogue()
    {
        var personalLines = dialogueFile.betweenCustomers.Where(entry => entry?.afterCustomer == GM.CustomersArrived && entry.playerLines != null)
            .SelectMany(entry => entry.playerLines)
            .Select(text => new DialogueLine { text = text, playerSpeaks = true });
        GM.BeginCustomerDialogue(CustomerPhase.BetweenCustomers, personalLines);
    }

    DialogueLine[] PickConversation(CustomerConversation[] conversations)
    {
        var matches = conversations.Where(c => c?.npcId == GM.CurrentNpcId &&
            c.lines != null && c.lines.Any(line => !string.IsNullOrWhiteSpace(line?.text))).ToArray();
        return matches.Length == 0 ? Array.Empty<DialogueLine>() : matches[Random.Range(0, matches.Length)].lines;
    }

    void AdvanceDialogue()
    {
        if (GM.DayOver || dialogueEffects.FinishTyping() || GM.CurrentCustomerLine == null) return;
        GM.AdvanceCustomerDialogue();
        onAdvanced();
    }

    public void Show(Transform customer)
    {
        DialogueLine line = GM.CurrentCustomerLine;
        bool playerSpeaks = playerDialogueButton && line?.playerSpeaks == true;
        npcDialogueButton.gameObject.SetActive(!playerSpeaks);
        if (playerDialogueButton) playerDialogueButton.gameObject.SetActive(playerSpeaks);
        var button = playerSpeaks ? playerDialogueButton : npcDialogueButton;
        var text = playerSpeaks ? playerText : orderText;
        string body = line != null
            ? line.text
            : "May I have a stew with:\n" + string.Join(", ", GM.CurrentOrder.Select(i => i.displayName));
        var speaker = line?.playerSpeaks == true ? null : customer;
        dialogueEffects.Show(button, text, body, line != null, line != null && !GM.HasAdvancedDialogue, speaker);
    }

    public void Hide()
    {
        dialogueEffects.Stop();
        npcDialogueButton.gameObject.SetActive(false);
        if (playerDialogueButton) playerDialogueButton.gameObject.SetActive(false);
    }

    void Update() => dialogueEffects.Tick(Time.deltaTime);
}

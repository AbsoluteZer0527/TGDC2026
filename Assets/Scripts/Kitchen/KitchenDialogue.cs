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

    [Header("Order icons")]
    [Tooltip("Icon size as a percent of the text size.")]
    [SerializeField, Min(10f)] float orderIconSizePercent = 250f;
    [Tooltip("Raises the icon, in ems (1 = one text height). Negative lowers it.")]
    [SerializeField] float orderIconRaise = 0.3f;
    [Tooltip("Gap between \"2x\" and the icon, in ems. Negative pulls them closer.")]
    [SerializeField] float orderIconGap = 0.1f;
    [Tooltip("Ingredients per row before wrapping to the next row.")]
    [SerializeField, Min(1)] int orderColumns = 3;
    [Tooltip("Width of one column, as a percent of the bubble's text area.")]
    [SerializeField, Range(5f, 50f)] float orderColumnWidthPercent = 28f;
    [Tooltip("Distance between rows (and below the header), in ems.")]
    [SerializeField, Min(0.5f)] float orderRowHeight = 2.2f;

    Button npcDialogueButton, playerDialogueButton;
    Action onAdvanced;
    GameManager GM => GameManager.I;

    public void Initialize(Action onDialogueAdvanced)
    {
        onAdvanced = onDialogueAdvanced;
        RefreshSpriteAssets(orderText.spriteAsset);
        npcDialogueButton = BindDialogueButton(orderBubble ? orderBubble.gameObject : orderText.gameObject);
        if (playerBubble && playerText) playerDialogueButton = BindDialogueButton(playerBubble.gameObject);
    }

    // TMP only links each sprite back to its asset when lookup tables are rebuilt. After editing sprite
    // assets in the Inspector those tables can be stale, which crashes TMP (NullReference in GenerateTextMesh).
    static void RefreshSpriteAssets(TMP_SpriteAsset asset)
    {
        if (!asset) return;
        asset.UpdateLookupTables();
        if (asset.fallbackSpriteAssets == null) return;
        foreach (var fallback in asset.fallbackSpriteAssets)
            if (fallback) fallback.UpdateLookupTables();
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

    public void BeginAudit(bool passed)
    {
        var lines = passed ? dialogueFile.auditPassLines : dialogueFile.auditFailLines;
        if (lines == null || !lines.Any(l => !string.IsNullOrWhiteSpace(l?.text))) // asset predates the fields
            lines = new[] { new DialogueLine { text = passed ? "Everything looks good..." : "Something seems wrong here..." } };
        GM.BeginCustomerDialogue(CustomerPhase.AtCounter, lines);
    }

    public void BeginFarewell() =>
        GM.BeginCustomerDialogue(CustomerPhase.Farewell, PickConversation(dialogueFile.customerEndings));

    public void BeginPersonalDialogue()
    {
        var personalLines = dialogueFile.betweenCustomers.Where(entry => entry?.afterCustomer == GM.CustomersArrived && entry.playerLines != null)
            .SelectMany(entry => entry.playerLines)
            .Where(line => line != null)
            .Select(line => new DialogueLine { text = line.text, playerSpeaks = true, afterLine = line.afterLine });
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
        if ((GM.DayOver && !GM.AuditInProgress) || dialogueEffects.FinishTyping() || GM.CurrentCustomerLine == null) return;
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
            : OrderBody();
        var speaker = line?.playerSpeaks == true ? null : customer;
        dialogueEffects.Show(button, text, body, line != null, line != null && !GM.HasAdvancedDialogue, speaker);
    }

    // "May I have a stew with:" then a grid of "2x [icon]" cells, orderColumns per row, each row centered.
    string OrderBody()
    {
        var cells = GM.CurrentOrder.GroupBy(i => i)
            .Select(g => FormattableString.Invariant(
                $"{g.Count()}x<space={orderIconGap + g.Key.orderIconExtraGap:0.##}em>{OrderIcon(g.Key)}"))
            .ToList();

        var sb = new System.Text.StringBuilder("May I have a stew with:");
        sb.Append(FormattableString.Invariant($"<line-height={orderRowHeight:0.##}em><align=left>"));
        for (int first = 0; first < cells.Count; first += orderColumns)
        {
            int n = Mathf.Min(orderColumns, cells.Count - first);
            float start = 50f - n * orderColumnWidthPercent / 2f; // center this row's columns
            sb.Append('\n');
            for (int c = 0; c < n; c++)
                sb.Append(FormattableString.Invariant($"<pos={start + c * orderColumnWidthPercent:0.#}%>"))
                  .Append(cells[first + c]);
        }
        return sb.Append("</align></line-height>").ToString();
    }

    // Inline TMP sprite of the ingredient's first walk frame (e.g. "Carrot1_0"). Needs the veggie
    // TMP Sprite Asset on orderText; without it (or without frames) the name is shown instead.
    string OrderIcon(IngredientData ing) =>
        orderText.spriteAsset && ing.frames != null && ing.frames.Length > 0 && ing.frames[0]
            ? FormattableString.Invariant(
                $"<size={orderIconSizePercent:0}%><voffset={orderIconRaise:0.##}em><sprite name=\"{ing.frames[0].name}\"></voffset></size>")
            : ing.displayName;

    public void Hide()
    {
        dialogueEffects.Stop();
        npcDialogueButton.gameObject.SetActive(false);
        if (playerDialogueButton) playerDialogueButton.gameObject.SetActive(false);
    }

    void Update() => dialogueEffects.Tick(Time.deltaTime);
}

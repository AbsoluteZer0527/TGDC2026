using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class ForestPigeon : MonoBehaviour
{
    [SerializeField] RectTransform pigeon;
    [SerializeField] TMP_Text dialogue;
    [SerializeField, TextArea] string bountyLine = "remember, the auditor checks today’s stew + today’s hunting. keep that total at or below the daily max...";
    [SerializeField, TextArea] string stewLine = "make sure to find the way back before the stew is overcooked...";
    [SerializeField, Min(1f)] float bountyReadSeconds = 8f, stewReadSeconds = 5f;

    HandRaiseAnimation hand;
    float bountyTimeLeft, stewTimeLeft;

    public bool IsShowing => hand != null && !hand.IsLowered;   // ForestBountyLetter leaves Q to the pigeon

    void Awake()
    {
        hand = new HandRaiseAnimation(pigeon, fromLeft: true);
        pigeon.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        var gm = GameManager.I;
        if (!gm || gm.DayOver || CurtainTransition.IsPlaying)
        {
            hand.Animate(false);
            return;
        }

        if (gm.TryTakeForestStewReminder()) stewTimeLeft = stewReadSeconds;
        if (gm.Soup != SoupState.Ready) stewTimeLeft = 0f;
        // Give the stew deadline priority. An interrupted bounty line keeps its remaining reading time.
        bool showingStew = stewTimeLeft > 0f;
        if (!showingStew && gm.TryTakeForestBountyReminder()) bountyTimeLeft = bountyReadSeconds;
        if (!showingStew && bountyTimeLeft <= 0f)
        {
            hand.Animate(false);
            return;
        }

        dialogue.text = showingStew ? stewLine : bountyLine;
        hand.Animate(true);
        if (!hand.IsRaised) return;
        bool dismiss = Keyboard.current?.qKey.wasPressedThisFrame == true;
        if (showingStew) stewTimeLeft = dismiss ? 0f : stewTimeLeft - Time.unscaledDeltaTime;
        else bountyTimeLeft = dismiss ? 0f : bountyTimeLeft - Time.unscaledDeltaTime;
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

// Q raises/lowers the bounty letter in the forest, like in the kitchen. Only once the pigeon's letter has been read.
// Goes on the forest canvas; Letter is a copy of the kitchen's "Open Letter" (Image + Bounty List View).
public sealed class ForestBountyLetter : MonoBehaviour
{
    [SerializeField] RectTransform letter;

    HandRaiseAnimation hand;
    bool raised;

    void Start()
    {
        hand = new HandRaiseAnimation(letter);   // remembers the authored pose as the raised one
        letter.gameObject.SetActive(false);
    }

    void Update()
    {
        var gm = GameManager.I;
        bool allowed = gm && gm.DeliveryStage == PigeonDeliveryStage.Complete && !gm.DayOver && !PauseMenu.IsOpen;
        if (!allowed) raised = false;
        else if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
        {
            //TODO: (paper rustle as the letter comes up / goes down)
            raised = !raised;
        }
        hand.Animate(raised);
    }
}

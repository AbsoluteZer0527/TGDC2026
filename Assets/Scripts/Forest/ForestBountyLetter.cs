using UnityEngine;
using UnityEngine.InputSystem;

// Q raises/lowers the bounty letter in the forest, like in the kitchen. The gun is put away while it's up;
// clicking to shoot drops the letter and the gun comes back up and fires as normal.
// Goes on the forest canvas; Letter is a copy of the kitchen's "Open Letter" (Image + Bounty List View).
public sealed class ForestBountyLetter : MonoBehaviour
{
    [SerializeField] RectTransform letter;

    HandRaiseAnimation hand;
    ForestPigeon pigeon;
    HitscanGun gun;
    bool raised;

    void Start()
    {
        hand = new HandRaiseAnimation(letter);   // remembers the authored pose as the raised one
        letter.gameObject.SetActive(false);
        pigeon = FindAnyObjectByType<ForestPigeon>();
        gun = FindAnyObjectByType<HitscanGun>();
    }

    void Update()
    {
        var gm = GameManager.I;
        bool allowed = gm && !gm.DayOver && !PauseMenu.IsOpen && !CurtainTransition.IsPlaying;
        bool pigeonTalking = pigeon && pigeon.IsShowing;   // Q dismisses the pigeon first
        if (!allowed) raised = false;
        else if (!pigeonTalking && Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
        {
            //TODO: (paper rustle as the letter comes up / goes down)
            raised = !raised;
        }
        // Shooting wins: the gun's own input starts the aim on this same click.
        bool shooting = gun && gun.player && gun.player.CanAct && Mouse.current != null &&
                        Mouse.current.leftButton.wasPressedThisFrame;
        if (shooting) raised = false;
        if (gun) gun.Holstered = raised;
        hand.Animate(raised);
    }
}

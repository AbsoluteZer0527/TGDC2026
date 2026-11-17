using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class SceneDoor : MonoBehaviour, IPointerClickHandler
{
    public string sceneToLoad = "Forest";
    public Key hotkey = Key.None;

    void Update()
    {
        if (hotkey == Key.None || Keyboard.current == null) return;
        if (Keyboard.current[hotkey].wasPressedThisFrame) Travel();
    }

    public void OnPointerClick(PointerEventData e) => Go();

    public void Go()
    {
        if (sceneToLoad == "Forest" && (!GameManager.I || !GameManager.I.ForestUnlocked))
        {
            AudioManager.Instance?.PlaySFX("reject");
            return;
        }
        Travel();
    }

    void Travel()
    {
        if (GameManager.I != null && GameManager.I.DayOver) return;
        AudioManager.Instance?.PlaySFX("pickup");
        CurtainTransition.TransitionTo(sceneToLoad);
    }
}

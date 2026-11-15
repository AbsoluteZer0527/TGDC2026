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
        if (Keyboard.current[hotkey].wasPressedThisFrame) Go();
    }

    public void OnPointerClick(PointerEventData e) => Go();

    public void Go()
    {
        if (GameManager.I != null && GameManager.I.DayOver) return;
        ScreenFader.LoadScene(sceneToLoad);
    }
}
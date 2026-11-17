using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Esc closes the curtain, freezes the game and shows the pause panel. Resume (or Esc again) opens it back up.
// Goes on Systems next to CurtainTransition; the panel is a child of the Transition Canvas, below the Curtain.
[RequireComponent(typeof(CurtainTransition))]
public sealed class PauseMenu : MonoBehaviour
{
    [SerializeField] GameObject pausePanel;
    [SerializeField] Button resumeButton;
    [SerializeField, Tooltip("Optional.")] Button pauseQuitButton;

    public static bool IsOpen { get; private set; }

    CurtainTransition curtain;
    bool busy;   // curtain is moving

    // Domain reload is off in this project, so statics survive between Play sessions unless reset here.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => IsOpen = false;

    // Each scene has a Systems copy; only the one GameManager keeps runs the menu.
    bool IsMain => GameManager.I != null && GameManager.I.gameObject == gameObject;

    void Start()
    {
        if (!IsMain) return;
        curtain = GetComponent<CurtainTransition>();
        resumeButton.onClick.AddListener(Resume);
        if (pauseQuitButton) pauseQuitButton.onClick.AddListener(StartScreen.Quit);
        pausePanel.SetActive(false);
    }

    void Update()
    {
        if (!IsMain || busy) return;
        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;

        if (IsOpen) Resume();
        else if (!CurtainTransition.IsPlaying && !GameManager.I.DayOver) StartCoroutine(Pause());
    }

    IEnumerator Pause()
    {
        busy = true;
        IsOpen = true;
        Time.timeScale = 0f;
        //TODO: (curtain swishing shut)
        yield return curtain.Shut(true);
        pausePanel.SetActive(true);
        busy = false;
    }

    void Resume()
    {
        if (!busy && IsOpen) StartCoroutine(Unpause());
    }

    IEnumerator Unpause()
    {
        busy = true;
        pausePanel.SetActive(false);
        //TODO: (curtain swishing open)
        yield return curtain.Open();
        Time.timeScale = 1f;
        IsOpen = false;
        busy = false;
        if (FindAnyObjectByType<PlayerController>())   // forest: take the mouse back for looking around
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The title screen: its own scene, first in Build Settings. Play loads the kitchen, Quit closes the game.
// Credits closes a curtain over the title and shows the credits on it; Back opens it again.
public sealed class StartScreen : MonoBehaviour
{
    [SerializeField] Button playButton, quitButton;
    [SerializeField] string firstScene = "Kitchen";

    [Header("Credits")]
    [SerializeField] Button creditsButton;
    [SerializeField, Tooltip("Full-screen Image drawn above the title (hidden until Credits is pressed).")]
    Image curtain;
    [SerializeField, Tooltip("Same frames as the game's curtain, from open to fully closed (Art/Kitchen 1-4).")]
    Sprite[] curtainFrames;
    [SerializeField, Min(0.01f)] float secondsPerFrame = 0.1f;
    [SerializeField, Tooltip("Credits text + Back button. Above the curtain in the Hierarchy.")]
    GameObject creditsPanel;
    [SerializeField] Button backButton;

    bool busy;

    void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        playButton.onClick.AddListener(Play);
        quitButton.onClick.AddListener(Quit);
        if (creditsButton) creditsButton.onClick.AddListener(() => { if (!busy) StartCoroutine(ShowCredits(true)); });
        if (backButton) backButton.onClick.AddListener(() => { if (!busy) StartCoroutine(ShowCredits(false)); });
        if (curtain) curtain.gameObject.SetActive(false);
        if (creditsPanel) creditsPanel.SetActive(false);
    }

    void Play()
    {
        if (busy || (creditsPanel && creditsPanel.activeSelf)) return;
        if (!Application.CanStreamedLevelBeLoaded(firstScene))
        {
            Debug.LogError($"Cannot load start destination '{firstScene}'.");
            return;
        }
        busy = true;
        playButton.interactable = false;
        quitButton.interactable = false;
        if (creditsButton) creditsButton.interactable = false;
        StartCoroutine(StartGame());
    }

    IEnumerator StartGame()
    {
        yield return CloseCurtain();
        CurtainTransition.LoadCoveredScene(firstScene);
    }

    IEnumerator CloseCurtain()
    {
        curtain.gameObject.SetActive(true);
        var hold = new WaitForSecondsRealtime(secondsPerFrame);
        foreach (var frame in curtainFrames)
        {
            curtain.sprite = frame;
            yield return hold;
        }
    }

    IEnumerator ShowCredits(bool show)
    {
        busy = true;
        if (show)
        {
            yield return CloseCurtain();
            creditsPanel.SetActive(true);
        }
        else
        {
            //TODO: (curtain swishing open)
            creditsPanel.SetActive(false);
            var hold = new WaitForSecondsRealtime(secondsPerFrame);
            for (int i = curtainFrames.Length - 2; i >= 0; i--)
            {
                curtain.sprite = curtainFrames[i];
                yield return hold;
            }
            curtain.gameObject.SetActive(false);
        }
        busy = false;
    }

    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

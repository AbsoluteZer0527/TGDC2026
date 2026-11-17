using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        AudioManager.Instance?.PlayMusic("titleMusic");
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
        //TODO: (start game button click)
        playButton.interactable = false;
        SceneManager.LoadScene(firstScene);
    }

    IEnumerator ShowCredits(bool show)
    {
        busy = true;
        var hold = new WaitForSecondsRealtime(secondsPerFrame);
        if (show)
        {
            //TODO: (curtain swishing shut)
            curtain.gameObject.SetActive(true);   // also blocks clicks on the title buttons
            foreach (var frame in curtainFrames)
            {
                curtain.sprite = frame;
                yield return hold;
            }
            creditsPanel.SetActive(true);
        }
        else
        {
            //TODO: (curtain swishing open)
            creditsPanel.SetActive(false);
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

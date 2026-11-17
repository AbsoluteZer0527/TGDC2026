using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class CurtainTransition : MonoBehaviour
{
    [SerializeField] Canvas overlay;
    [SerializeField] Image curtain;
    [SerializeField, Tooltip("From open to fully closed.")] Sprite[] frames;
    [SerializeField, Min(0.01f)] float secondsPerFrame = 0.1f;

    static CurtainTransition active;
    public static bool IsPlaying => active != null;

    // Domain reload is off, so clear this between Play sessions (stopping mid-transition would leave it stuck).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        active = null;
        coveredDestination = null;
        SceneManager.sceneLoaded -= OnCoveredSceneLoaded;
    }

    // The title owns its closing curtain. The gameplay overlay takes over before the first render.
    static string coveredDestination;

    public static void LoadCoveredScene(string sceneName)
    {
        coveredDestination = sceneName;
        SceneManager.sceneLoaded += OnCoveredSceneLoaded;
        SceneManager.LoadSceneAsync(sceneName);
    }

    static void OnCoveredSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != coveredDestination) return;
        SceneManager.sceneLoaded -= OnCoveredSceneLoaded;
        coveredDestination = null;
        var transition = GameManager.I.GetComponent<CurtainTransition>();
        transition.StartCoroutine(transition.RevealScene());
    }

    IEnumerator RevealScene()
    {
        yield return Shut(false);
        yield return null;
        yield return Open();
    }

    // startClosed: skip the closing animation and begin fully shut (e.g. coming from the black night screen).
    public static void TransitionTo(string sceneName, bool startClosed = false)
    {
        if (IsPlaying) return;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"Cannot load curtain destination '{sceneName}'.");
            return;
        }
        var transition = GameManager.I.GetComponent<CurtainTransition>();
        transition.StartCoroutine(transition.Play(sceneName, startClosed));
    }

    public void Close(System.Action whenClosed)
    {
        StartCoroutine(Play(null, false, whenClosed));
    }

    // Menus (start / pause) hold the curtain shut. Real time, so it works while the game is paused.
    // While held, IsPlaying is true, so doors and other transitions wait.
    public IEnumerator Shut(bool animate)
    {
        active = this;
        overlay.gameObject.SetActive(true);
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        if (!animate)
        {
            if (frames.Length > 0) curtain.sprite = frames[frames.Length - 1];
            yield break;
        }
        var hold = new WaitForSecondsRealtime(secondsPerFrame);
        foreach (var frame in frames)
        {
            curtain.sprite = frame;
            yield return hold;
        }
    }

    public IEnumerator Open()
    {
        var hold = new WaitForSecondsRealtime(secondsPerFrame);
        for (int i = frames.Length - 2; i >= 0; i--)
        {
            curtain.sprite = frames[i];
            yield return hold;
        }
        overlay.gameObject.SetActive(false);
        active = null;
    }

    IEnumerator Play(string sceneName, bool startClosed, System.Action whenClosed = null)
    {
        active = this;
        overlay.gameObject.SetActive(true);
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        var hold = new WaitForSecondsRealtime(secondsPerFrame);
        try
        {
            if (startClosed)
            {
                if (frames.Length > 0) curtain.sprite = frames[frames.Length - 1];
            }
            else
            {
                foreach (var frame in frames)
                {
                    curtain.sprite = frame;
                    yield return hold;
                }
            }
            if (sceneName != null) yield return SceneManager.LoadSceneAsync(sceneName);
            whenClosed?.Invoke();
            // Keep the cover through the destination's first render.
            yield return null;
            if (sceneName == null) yield break;
            for (int i = frames.Length - 2; i >= 0; i--)
            {
                curtain.sprite = frames[i];
                yield return hold;
            }
        }
        finally
        {
            overlay.gameObject.SetActive(false);
            active = null;
        }
    }
}

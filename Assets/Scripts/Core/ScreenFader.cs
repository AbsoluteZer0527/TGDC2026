using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Full-screen black fade used for every scene change. Placeholder: swap the visuals later,
// callers only use ScreenFader.LoadScene(...). Lives inside the Systems prefab (persists between scenes).
[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader I { get; private set; }
    public static bool Busy => I && I.busy;

    [Min(0f)] public float fadeOutSeconds = 0.35f;
    [Min(0f)] public float fadeInSeconds = 0.35f;

    CanvasGroup group;
    bool busy;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (I == null) I = this;   // the duplicate Systems in a reloaded scene destroys itself
        SetAlpha(0f);
    }

    void OnDestroy()
    {
        if (I == this) I = null;
    }

    // Fade to black, load the scene, fade back in. Falls back to a plain load if there's no fader.
    public static void LoadScene(string scene, bool startBlack = false)
    {
        if (!I) { SceneManager.LoadScene(scene); return; }
        if (I.busy) return;
        I.StartCoroutine(I.FadeLoad(scene, startBlack));
    }

    IEnumerator FadeLoad(string scene, bool startBlack)
    {
        busy = true;
        //TODO: (door / transition sound)
        if (startBlack) SetAlpha(1f);
        else yield return Fade(0f, 1f, fadeOutSeconds);

        yield return SceneManager.LoadSceneAsync(scene);
        yield return null; // let the new scene's Start run behind the black

        yield return Fade(1f, 0f, fadeInSeconds);
        busy = false;
    }

    IEnumerator Fade(float from, float to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            SetAlpha(Mathf.Lerp(from, to, t / seconds));
            yield return null;
        }
        SetAlpha(to);
    }

    void SetAlpha(float a)
    {
        group.alpha = a;
        group.blocksRaycasts = a > 0.01f; // block clicks while black
    }
}

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

    public static void TransitionTo(string sceneName)
    {
        if (IsPlaying) return;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"Cannot load curtain destination '{sceneName}'.");
            return;
        }
        var transition = GameManager.I.GetComponent<CurtainTransition>();
        transition.StartCoroutine(transition.Play(sceneName));
    }

    IEnumerator Play(string sceneName)
    {
        active = this;
        overlay.gameObject.SetActive(true);
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        var hold = new WaitForSecondsRealtime(secondsPerFrame);
        try
        {
            foreach (var frame in frames)
            {
                curtain.sprite = frame;
                yield return hold;
            }
            yield return SceneManager.LoadSceneAsync(sceneName);
            // Keep the cover through the destination's first render.
            yield return null;
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

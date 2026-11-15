using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public sealed class KitchenDialogueEffects
{
    [SerializeField, Min(1f)] float charactersPerSecond = 38f;
    [SerializeField, Min(0f)] float hopHeight = 0.28f;

    const float takeoff = 0.08f, landing = 0.35f, settled = 0.46f;
    static readonly AnimationCurve bubblePop = new(
        new Keyframe(0f, 0.04f), new Keyframe(0.14f, 1.14f),
        new Keyframe(0.23f, 0.94f), new Keyframe(0.31f, 1.03f), new Keyframe(0.40f, 1f));
    static readonly AnimationCurve squash = new(
        new Keyframe(0f, 0f), new Keyframe(takeoff, 0.14f), new Keyframe(0.14f, -0.10f),
        new Keyframe(0.27f, 0f), new Keyframe(landing, 0.10f), new Keyframe(settled, 0f));

    Button bubble;
    TMP_Text text;
    Transform speaker;
    Vector3 bubbleScale, speakerScale, speakerPosition;
    float elapsed;
    int bodyCharacters;
    bool canAdvance;

    public void Show(Button button, TMP_Text label, string body, bool advance, bool showHint, Transform speakingCustomer)
    {
        Stop();
        bubble = button;
        bubbleScale = button.transform.localScale;
        speaker = speakingCustomer;
        if (speaker)
        {
            speakerScale = speaker.localScale;
            speakerPosition = speaker.localPosition;
        }

        text = label;
        canAdvance = advance;
        bubble.interactable = true;
        text.text = showHint ? body + "\n\n<size=65%>Click to continue</size>" : body;
        text.useMaxVisibleDescender = false;
        text.maxVisibleCharacters = int.MaxValue;
        text.ForceMeshUpdate();
        bodyCharacters = text.textInfo.characterCount;
        // TMP indices point into the source string even when it contains rich-text tags.
        while (bodyCharacters > 0 && text.textInfo.characterInfo[bodyCharacters - 1].index >= body.Length)
            bodyCharacters--;
        text.maxVisibleCharacters = 0;
        elapsed = 0f;
        Pose(0f);
    }

    public void Tick(float deltaTime)
    {
        if (!bubble) return;
        bool moving = elapsed < settled;
        if (!moving && text.maxVisibleCharacters == int.MaxValue) return;
        elapsed += deltaTime;
        if (moving) Pose(Mathf.Min(elapsed, settled));
        if (text.maxVisibleCharacters == int.MaxValue) return;

        int count = Mathf.FloorToInt(Mathf.Max(0f, elapsed - takeoff) * charactersPerSecond);
        if (count >= bodyCharacters) FinishTyping();
        else text.maxVisibleCharacters = count;
    }

    public bool FinishTyping()
    {
        if (!text || text.maxVisibleCharacters == int.MaxValue) return false;
        text.maxVisibleCharacters = int.MaxValue;
        bubble.interactable = canAdvance;
        return true;
    }

    public void Stop()
    {
        if (!bubble) return;
        Pose(settled);
        FinishTyping();
        bubble = null;
        text = null;
        speaker = null;
    }

    void Pose(float time)
    {
        bubble.transform.localScale = bubbleScale * bubblePop.Evaluate(time);
        if (!speaker) return;
        float compression = squash.Evaluate(time);
        speaker.localScale = Vector3.Scale(speakerScale, new Vector3(1f + compression, 1f - compression, 1f));
        float air = Mathf.InverseLerp(takeoff, landing, time);
        speaker.localPosition = speakerPosition + Vector3.up * (4f * air * (1f - air) * hopHeight);
    }
}

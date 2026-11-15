using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SpriteLoop : MonoBehaviour
{
    [SerializeField] Sprite[] frames;
    [SerializeField, Min(0f)] float framesPerSecond = 8f;

    SpriteRenderer spriteRenderer;
    Image image;
    float framePosition;

    void Awake()
    {
        if (!TryGetComponent(out spriteRenderer) && !TryGetComponent(out image))
            enabled = false;
    }

    void ShowFrame(Sprite frame)
    {
        if (spriteRenderer) spriteRenderer.sprite = frame;
        else image.sprite = frame;
    }

    void OnEnable()
    {
        framePosition = 0f;
        if (frames != null && frames.Length > 0)
            ShowFrame(frames[0]);
    }

    void Update()
    {
        if (frames == null || frames.Length == 0) return;
        framePosition = (framePosition + Time.deltaTime * framesPerSecond) % frames.Length;
        ShowFrame(frames[(int)framePosition]);
    }
}

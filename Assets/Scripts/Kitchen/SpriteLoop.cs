using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[DisallowMultipleComponent]
public class SpriteLoop : MonoBehaviour
{
    [SerializeField] Sprite[] frames;
    [SerializeField, Min(0f)] float framesPerSecond = 8f;

    SpriteRenderer spriteRenderer;
    float framePosition;

    void Awake() => spriteRenderer = GetComponent<SpriteRenderer>();

    void OnEnable()
    {
        framePosition = 0f;
        if (frames != null && frames.Length > 0)
            spriteRenderer.sprite = frames[0];
    }

    void Update()
    {
        if (frames == null || frames.Length == 0) return;
        framePosition = (framePosition + Time.deltaTime * framesPerSecond) % frames.Length;
        spriteRenderer.sprite = frames[(int)framePosition];
    }
}

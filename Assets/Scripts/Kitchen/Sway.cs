using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[DisallowMultipleComponent]
public class Sway : MonoBehaviour
{
    [Tooltip("Maximum local Z displacement from the sprite's resting position.")]
    [Min(0f)] public float amplitude = 0.06f;

    [Tooltip("Seconds for one full back-and-forth sway.")]
    [Min(0.1f)] public float period = 4f;

    Vector3 restPosition;
    float phase;
    float elapsed;

    void OnEnable()
    {
        restPosition = transform.localPosition;
        phase = Random.Range(0f, 2f * Mathf.PI);
        elapsed = 0f;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        float angle = elapsed * (2f * Mathf.PI / Mathf.Max(0.1f, period));
        float wind = 0.8f * Mathf.Sin(angle + phase) + 0.2f * Mathf.Sin(1.7f * angle + phase);
        Vector3 position = transform.localPosition;
        position.z = restPosition.z + amplitude * wind * Mathf.SmoothStep(0f, 1f, elapsed);
        transform.localPosition = position;
    }

    void OnDisable()
    {
        Vector3 position = transform.localPosition;
        position.z = restPosition.z;
        transform.localPosition = position;
    }
}

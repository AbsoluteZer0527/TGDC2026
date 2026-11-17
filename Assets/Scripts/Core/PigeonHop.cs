using UnityEngine;

public sealed class PigeonHop : MonoBehaviour
{
    [SerializeField, Min(0f)] float height = 0.22f;
    [SerializeField, Min(0.1f)] float period = 0.9f;

    Vector3 restPosition;
    float phase;

    void OnEnable()
    {
        restPosition = transform.localPosition;
        phase = 0f;
    }

    void Update()
    {
        phase = Mathf.Repeat(phase + Time.deltaTime / Mathf.Max(0.1f, period), 1f);
        transform.localPosition = restPosition + Vector3.up * (4f * height * phase * (1f - phase));
    }

    void OnDisable() => transform.localPosition = restPosition;
}

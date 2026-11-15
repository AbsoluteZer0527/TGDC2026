using UnityEngine;

[DisallowMultipleComponent]
public class PotShake : MonoBehaviour
{
    [SerializeField] SoupState shakeDuring = SoupState.Cooking;
    [SerializeField, Min(0f)] float tiltDegrees = 3f;
    [SerializeField, Min(0f), Tooltip("Full rocking cycles per second.")] float frequency = 1.5f;

    Quaternion restRotation;
    float phase;
    bool shaking;

    void Update()
    {
        var game = GameManager.I;
        if (!game || game.DayOver || game.Soup != shakeDuring)
        {
            StopShaking();
            return;
        }

        if (!shaking)
        {
            restRotation = transform.localRotation;
            phase = 0f;
            shaking = true;
        }

        phase = (phase + Time.deltaTime * frequency) % 1f;
        float wobble = Mathf.Sin(phase * 2f * Mathf.PI);
        transform.localRotation = restRotation * Quaternion.AngleAxis(tiltDegrees * wobble, Vector3.forward);
    }

    void OnDisable() => StopShaking();

    void StopShaking()
    {
        if (!shaking) return;
        transform.localRotation = restRotation;
        shaking = false;
    }
}

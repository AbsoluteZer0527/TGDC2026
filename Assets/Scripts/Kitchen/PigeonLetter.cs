using System;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class PigeonLetter : MonoBehaviour, IPointerClickHandler
{
    public event Action Clicked;

    Quaternion restRotation;
    float elapsed;

    void OnEnable()
    {
        restRotation = transform.localRotation;
        elapsed = 0f;
    }

    void Update()
    {
        if (GameManager.I.DayOver)
        {
            transform.localRotation = restRotation;
            return;
        }

        // Rock twice, then rest briefly so the next shake catches the eye.
        elapsed = Mathf.Repeat(elapsed + Time.deltaTime, 1.6f);
        float progress = Mathf.Clamp01(elapsed / 0.7f);
        float tilt = 8f * Mathf.Sin(progress * 4f * Mathf.PI) * Mathf.Sin(progress * Mathf.PI);
        transform.localRotation = restRotation * Quaternion.AngleAxis(tilt, Vector3.forward);
    }

    void OnDisable() => transform.localRotation = restRotation;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        AudioManager.Instance?.PlaySFX("pickup");
        Clicked?.Invoke();
    }
}

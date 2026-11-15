using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class TimingMeter : MonoBehaviour
{
    public RectTransform marker;
    public bool vertical = true;          // true: marker moves up/down. false: left/right
    public float speed = 1.8f;            // full sweeps per second
    public float markerThickness = 10f;
    public float markerOverhang = 20f;    // marker sticks out past the meter edges

    public bool Running { get; private set; }

    float t;
    int dir = 1;
    Action<float> onDone;

    void Awake() => gameObject.SetActive(false);

    public void Begin(Action<float> callback)
    {
        StopAllCoroutines();
        t = 0f; dir = 1;
        onDone = callback;
        Running = true;
        gameObject.SetActive(true);
        Place(t);
    }

    public void Cancel()
    {
        Running = false;
        onDone = null;
        gameObject.SetActive(false);
    }

    void Update()
    {
        if (!Running) return;

        t += dir * speed * Time.deltaTime;
        if (t >= 1f) { t = 1f; dir = -1; }
        else if (t <= 0f) { t = 0f; dir = 1; }
        Place(t);

        if (!CurtainTransition.IsPlaying && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Running = false;
            var cb = onDone; onDone = null;
            cb?.Invoke(t);                 // 1 = top/right (fast), 0 = bottom/left (slow)
            StartCoroutine(HideSoon());    // freeze briefly so you see where you hit
        }
    }

    void Place(float v)
    {
        if (vertical)
        {
            marker.anchorMin = new Vector2(0, v); marker.anchorMax = new Vector2(1, v);
            marker.sizeDelta = new Vector2(markerOverhang, markerThickness);
        }
        else
        {
            marker.anchorMin = new Vector2(v, 0); marker.anchorMax = new Vector2(v, 1);
            marker.sizeDelta = new Vector2(markerThickness, markerOverhang);
        }
        marker.anchoredPosition = Vector2.zero;
    }

    IEnumerator HideSoon()
    {
        yield return new WaitForSeconds(0.35f);
        if (!Running) gameObject.SetActive(false);
    }
}

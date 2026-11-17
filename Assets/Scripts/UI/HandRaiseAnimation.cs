using UnityEngine;

// Animate from the edge of the canvas to the authored pose.
public sealed class HandRaiseAnimation
{
    const float RaiseSeconds = 0.32f;
    readonly RectTransform rect;
    readonly Vector2 raisedPosition;
    readonly Quaternion raisedRotation;
    readonly bool fromLeft;
    float rightExtent;
    float progress;

    public bool IsLowered => progress == 0f;
    public bool IsRaised => progress == 1f;

    public HandRaiseAnimation(RectTransform rect, bool fromLeft = false)
    {
        this.rect = rect;
        this.fromLeft = fromLeft;
        raisedPosition = rect.anchoredPosition;
        raisedRotation = rect.localRotation;
    }

    public void Animate(bool raise)
    {
        if (!raise && IsLowered) return;
        var parent = (RectTransform)rect.parent;
        Vector2 hiddenPosition = raisedPosition;
        if (fromLeft)
        {
            if (IsLowered)
            {
                // Include the speech bubble, which extends past the pigeon's own rectangle.
                rect.gameObject.SetActive(true);
                rightExtent = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, rect).max.x - rect.localPosition.x;
            }
            float anchorX = Mathf.Lerp(parent.rect.xMin, parent.rect.xMax, rect.anchorMin.x);
            hiddenPosition.x = parent.rect.xMin - anchorX - rightExtent - 8f;
        }
        else
        {
            float anchorY = Mathf.Lerp(parent.rect.yMin, parent.rect.yMax, rect.anchorMin.y);
            hiddenPosition.y = parent.rect.yMin - anchorY - rect.rect.yMax - 8f;
        }
        progress = Mathf.MoveTowards(progress, raise ? 1f : 0f, Time.unscaledDeltaTime / RaiseSeconds);
        float t = Mathf.SmoothStep(0f, 1f, progress);
        rect.anchoredPosition = Vector2.Lerp(hiddenPosition, raisedPosition, t);
        if (!fromLeft) rect.localRotation = raisedRotation * Quaternion.Euler(-30f * (1f - t), 0f, 0f);
        rect.gameObject.SetActive(!IsLowered);
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// The savings amount is visible during the IRS visit or while hovering.
// Works on either:
//  - a 2D sprite (SpriteRenderer) + BoxCollider2D: hover comes from the camera's Physics 2D Raycaster
//    (PigeonDelivery adds one to the main camera at startup), or
//  - a UI Image with Raycast Target on.
public class PiggyBank : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("Savings text shown on hover and during the IRS visit.")]
    [SerializeField] TMP_Text amountText;
    [SerializeField] Color positive = new(1f, 0.83f, 0.3f);   // yellow
    [SerializeField] Color negative = new(1f, 0.3f, 0.3f);    // red
    [SerializeField] Transform bankVisual;
    [SerializeField, Min(0f)] float shakeDegrees = 4f;
    [SerializeField, Min(0f)] float shakesPerSecond = 5f;

    bool hovered;
    Quaternion restRotation;
    float hoverStart;

    void OnDisable()
    {
        SetHovered(false);
        if (amountText) amountText.gameObject.SetActive(false);
    }

    //TODO: (coin jingle on hover)
    public void OnPointerEnter(PointerEventData eventData) => SetHovered(true);
    public void OnPointerExit(PointerEventData eventData) => SetHovered(false);

    void SetHovered(bool on)
    {
        if (bankVisual && on && !hovered)
        {
            restRotation = bankVisual.localRotation;
            hoverStart = Time.unscaledTime;
        }
        else if (bankVisual && !on && hovered) bankVisual.localRotation = restRotation;
        hovered = on;
    }

    void LateUpdate()
    {
        if (hovered && bankVisual)
        {
            float phase = (Time.unscaledTime - hoverStart) * shakesPerSecond * 2f * Mathf.PI;
            bankVisual.localRotation = restRotation * Quaternion.AngleAxis(Mathf.Sin(phase) * shakeDegrees, Vector3.forward);
        }
        var gm = GameManager.I;
        if (!amountText) return;
        bool show = gm && (hovered || gm.AuditorPresent);
        if (amountText.gameObject.activeSelf != show) amountText.gameObject.SetActive(show);
        if (!show) return;
        long s = gm.Savings;
        amountText.text = (s < 0 ? "-$" : "$") + HUD.FormatMoney(System.Math.Abs(s));
        amountText.color = s < 0 ? negative : positive;
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// Kitchen UI piggy bank: hover to see the running total (yellow if positive, red if negative).
// Put this on a UI Image (Raycast Target ON) inside KitchenCanvas.
public class PiggyBank : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("Child text shown only while hovering. Turn its Raycast Target off.")]
    [SerializeField] TMP_Text amountText;
    [SerializeField] Color positive = new(1f, 0.83f, 0.3f);   // yellow
    [SerializeField] Color negative = new(1f, 0.3f, 0.3f);    // red

    bool hovered;

    void OnEnable() => SetHovered(false);

    //TODO: (coin jingle on hover)
    public void OnPointerEnter(PointerEventData eventData) => SetHovered(true);
    public void OnPointerExit(PointerEventData eventData) => SetHovered(false);

    void SetHovered(bool on)
    {
        hovered = on;
        amountText.gameObject.SetActive(on);
    }

    void Update()
    {
        var gm = GameManager.I;
        if (!hovered || !gm) return;
        long s = gm.Savings;
        amountText.text = (s < 0 ? "-$" : "$") + HUD.FormatMoney(System.Math.Abs(s));
        amountText.color = s < 0 ? negative : positive;
    }
}

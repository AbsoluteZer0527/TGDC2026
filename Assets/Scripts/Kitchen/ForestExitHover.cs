using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ForestExitHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ICanvasRaycastFilter
{
    [SerializeField] Image curtain;
    [SerializeField] Image forestIcon;
    [SerializeField] Sprite hoveredCurtain;
    [SerializeField] Sprite hoveredIcon;

    readonly List<Vector2> curtainOutline = new();
    RectTransform frame;

    void Awake()
    {
        frame = (RectTransform)transform;
        curtain.sprite.GetPhysicsShape(0, curtainOutline);
    }

    bool ForestUnlocked => GameManager.I && GameManager.I.ForestUnlocked;

    void OnEnable()
    {
        forestIcon.enabled = ForestUnlocked;
        SetHovered(false);
    }

    void LateUpdate()
    {
        if (forestIcon.enabled != ForestUnlocked) forestIcon.enabled = ForestUnlocked;
    }

    //TODO: (curtain lifting when the pointer enters the hunting button)
    public void OnPointerEnter(PointerEventData eventData) => SetHovered(true);
    public void OnPointerExit(PointerEventData eventData) => SetHovered(false);

    void SetHovered(bool hovered)
    {
        ShowSprite(curtain, hovered ? hoveredCurtain : curtain.sprite);
        ShowSprite(forestIcon, hovered ? hoveredIcon : forestIcon.sprite);
    }

    // All four sprites are slices of full-screen art. Keep their original positions and sizes.
    static void ShowSprite(Image image, Sprite sprite)
    {
        image.overrideSprite = sprite;
        Vector2 sheetSize = new Vector2(sprite.texture.width, sprite.texture.height);
        image.rectTransform.anchorMin = sprite.rect.min / sheetSize;
        image.rectTransform.anchorMax = sprite.rect.max / sheetSize;
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!ForestUnlocked) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(frame, screenPoint, eventCamera, out var local))
            return false;

        Sprite idle = curtain.sprite;
        Vector2 uv = Rect.PointToNormalized(frame.rect, local);
        Vector2 pixel = Vector2.Scale(uv, new Vector2(idle.texture.width, idle.texture.height));
        if (forestIcon.sprite.rect.Contains(pixel)) return true;
        if (!idle.rect.Contains(pixel)) return false;

        // Use the resting fabric outline even while the wider hover art is visible.
        Vector2 point = (pixel - idle.rect.min - idle.pivot) / idle.pixelsPerUnit;
        bool inside = false;
        for (int i = 0, j = curtainOutline.Count - 1; i < curtainOutline.Count; j = i++)
        {
            Vector2 a = curtainOutline[i], b = curtainOutline[j];
            if ((a.y > point.y) != (b.y > point.y) &&
                point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside;
    }
}

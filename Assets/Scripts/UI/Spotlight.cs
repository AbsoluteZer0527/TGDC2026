using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Greys out the screen except a circle around one UI element, with a ring around it.
// Clicks inside the circle go through to the element; a click anywhere else dismisses the spotlight.
// Put this on a RawImage anywhere in the scene: on start it moves itself onto its own full-screen canvas
// above everything (HUD included), just under the scene-transition curtain.
[RequireComponent(typeof(RawImage))]
public sealed class Spotlight : MonoBehaviour, ICanvasRaycastFilter, IPointerClickHandler
{
    [SerializeField, Tooltip("Above the HUD (10), below the curtain (32000).")] int sortingOrder = 31000;

    [SerializeField] Color dim = new(0f, 0f, 0f, 0.65f);
    [SerializeField] Color ring = new(1f, 0.85f, 0.3f, 1f);
    [SerializeField, Min(0f), Tooltip("Screen pixels.")] float ringWidth = 6f;
    [SerializeField, Min(1f), Tooltip("Circle size compared to the target's longest side.")] float padding = 1.3f;
    [SerializeField, Min(0f), Tooltip("Screen pixels of soft edge inside the ring.")] float softEdge = 10f;
    [SerializeField, Min(0.01f)] float fadeSeconds = 0.3f;

    RawImage image;
    RectTransform target;
    Texture2D texture;
    Vector2 center, screenSize;
    float radius, alpha, goal;

    void Awake()
    {
        image = GetComponent<RawImage>();
        image.color = new Color(1f, 1f, 1f, 0f);
        image.enabled = false;

        var layer = new GameObject("SpotlightCanvas", typeof(Canvas), typeof(GraphicRaycaster));
        var canvas = layer.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var rect = (RectTransform)transform;
        rect.SetParent(layer.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    public void Show(RectTransform element)
    {
        target = element;
        goal = 1f;
        image.enabled = true;
        Rebuild();
        //TODO: (soft chime pointing at the hunting button)
    }

    public void Hide() => goal = 0f;

    void Update()
    {
        if (!image.enabled) return;
        if (!target || !target.gameObject.activeInHierarchy) goal = 0f;
        else if (new Vector2(Screen.width, Screen.height) != screenSize) Rebuild();

        alpha = Mathf.MoveTowards(alpha, goal, Time.unscaledDeltaTime / fadeSeconds);
        image.color = new Color(1f, 1f, 1f, alpha);
        if (alpha <= 0f && goal <= 0f) image.enabled = false;
    }

    // Paints the dim + hole + ring into a screen-sized texture (half resolution). Only runs on Show or resize.
    void Rebuild()
    {
        screenSize = new Vector2(Screen.width, Screen.height);
        var canvas = target.GetComponentInParent<Canvas>().rootCanvas;   // the target's canvas, for its screen position
        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        var corners = new Vector3[4];
        target.GetWorldCorners(corners);
        Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
        center = (min + max) / 2f;
        radius = Mathf.Max(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y)) / 2f * padding;

        const int scale = 2;
        int w = Mathf.Max(1, Screen.width / scale), h = Mathf.Max(1, Screen.height / scale);
        if (!texture || texture.width != w || texture.height != h)
        {
            if (texture) Destroy(texture);
            texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        }

        var pixels = new Color32[w * h];
        Color clear = new(dim.r, dim.g, dim.b, 0f);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f) * scale, center);
            Color c = Color.Lerp(clear, dim, Mathf.InverseLerp(radius - softEdge, radius, d));
            float ringAmount = Mathf.Clamp01(1f - Mathf.Abs(d - radius - ringWidth / 2f) / (ringWidth / 2f + 1f));
            pixels[y * w + x] = Color.Lerp(c, ring, ringAmount);
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        image.texture = texture;
    }

    // Raycasts inside the circle fall through to the button underneath.
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera) =>
        image.enabled && goal > 0f && Vector2.Distance(screenPoint, center) > radius;

    public void OnPointerClick(PointerEventData eventData) => Hide();

    void OnDestroy()
    {
        if (texture) Destroy(texture);
    }
}

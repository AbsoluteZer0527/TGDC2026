using UnityEngine;
using UnityEngine.UI;

public class Minimap : MonoBehaviour
{
    public ForestGenerator generator;
    public Transform player;
    [SerializeField] RectTransform viewport, playerArrow;
    [SerializeField] Image fogArt;
    [SerializeField] Sprite[] treeIcons;
    [SerializeField] Sprite homeIcon;
    [SerializeField, Min(1)] float treeSize = 40f, homeSize = 48f;

    [Header("View")]
    [Min(1)] public int viewTiles = 10;
    [Min(0)] public int revealRadius = 2;

    RectTransform markers;
    RawImage fogMask;
    Texture2D fog;
    const int PixelsPerTile = 16;
    int fogPadding;
    bool[] visited;
    bool[] explored;
    Vector2Int lastTile = new Vector2Int(int.MinValue, int.MinValue);

    void LateUpdate()
    {
        if (!generator || !player || generator.Tiles == null) return;
        if (!fog) Init();

        Vector2 position = generator.WorldToTileF(player.position);
        Vector2Int tile = Vector2Int.RoundToInt(position);
        if (tile != lastTile)
        {
            lastTile = tile;
            if (tile.x >= 0 && tile.y >= 0 && tile.x < generator.Width && tile.y < generator.Height)
            {
                int index = tile.x + tile.y * generator.Width;
                if (!visited[index])
                {
                    visited[index] = true;
                    Reveal(tile);
                    fog.Apply(false);
                }
            }
        }

        float step = viewport.rect.width / Mathf.Max(1, viewTiles);
        if (step <= 0f) return;
        Vector2 center = position + Vector2.one * 0.5f;
        markers.sizeDelta = new Vector2(generator.Width, generator.Height) * step;
        markers.anchoredPosition = -center * step;
        Vector2 span = viewport.rect.size / step;
        Vector2 from = (center - span * 0.5f) * PixelsPerTile + Vector2.one * fogPadding;
        span *= PixelsPerTile;
        fogMask.uvRect = new Rect(from.x / fog.width, from.y / fog.height, span.x / fog.width, span.y / fog.height);

        Vector3 forward = player.forward;
        playerArrow.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(forward.z, forward.x) * Mathf.Rad2Deg);
        // Rotation already points the nose left. Flip across that axis to keep the sprite upright.
        Vector3 scale = playerArrow.localScale;
        if (Mathf.Abs(forward.x) > 0.001f)
            playerArrow.localScale = new Vector3(scale.x, forward.x < 0 ? -Mathf.Abs(scale.y) : Mathf.Abs(scale.y), scale.z);
    }

    void Init()
    {
        int width = generator.Width, height = generator.Height;
        markers = new GameObject("Discovered landmarks", typeof(RectTransform)).GetComponent<RectTransform>();
        markers.SetParent(viewport, false);
        markers.anchorMin = markers.anchorMax = Vector2.one * 0.5f;
        markers.pivot = Vector2.zero;
        markers.SetAsFirstSibling();

        // The stencil draws the paper hatching only over unknown cells.
        fogMask = new GameObject("Unexplored cells", typeof(RectTransform), typeof(RawImage), typeof(Mask)).GetComponent<RawImage>();
        fogMask.transform.SetParent(viewport, false);
        Stretch(fogMask.rectTransform);
        fogMask.raycastTarget = false;
        fogMask.GetComponent<Mask>().showMaskGraphic = false;
        fogArt.transform.SetParent(fogMask.transform, false);
        playerArrow.SetAsLastSibling();

        var gm = GameManager.I;
        visited = gm ? gm.VisitedForestTiles : null;
        if (visited == null || visited.Length != width * height)
        {
            visited = new bool[width * height];
            if (gm) gm.VisitedForestTiles = visited;
        }
        explored = new bool[width * height];
        fogPadding = (revealRadius + 1) * PixelsPerTile;
        fog = new Texture2D(width * PixelsPerTile + 2 * fogPadding, height * PixelsPerTile + 2 * fogPadding, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[fog.width * fog.height];
        System.Array.Fill(pixels, (Color32)Color.white);
        fog.SetPixels32(pixels);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            if (visited[x + y * width]) Reveal(new Vector2Int(x, y));
        fog.Apply();
        fogMask.texture = fog;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    void Reveal(Vector2Int center)
    {
        float radius = (revealRadius + 0.5f) * PixelsPerTile;
        int cx = fogPadding + center.x * PixelsPerTile + PixelsPerTile / 2;
        int cy = fogPadding + center.y * PixelsPerTile + PixelsPerTile / 2;
        var pixels = fog.GetRawTextureData<Color32>();
        for (int y = cy - (int)radius; y < cy + radius; y++)
        {
            float dy = y + 0.5f - cy;
            float halfWidth = Mathf.Sqrt(radius * radius - dy * dy);
            int left = Mathf.CeilToInt(cx - halfWidth - 0.5f);
            int right = Mathf.FloorToInt(cx + halfWidth - 0.5f);
            for (int x = left; x <= right; x++)
                pixels[y * fog.width + x] = default;
        }
        for (int dx = -revealRadius; dx <= revealRadius; dx++)
        for (int dy = -revealRadius; dy <= revealRadius; dy++)
        {
            if (dx * dx + dy * dy > revealRadius * (revealRadius + 1)) continue;
            int x = center.x + dx, y = center.y + dy;
            if (x < 0 || x >= generator.Width || y < 0 || y >= generator.Height) continue;
            int index = x + y * generator.Width;
            if (explored[index]) continue;
            explored[index] = true;
            Uncover(x, y);
        }
    }

    void Uncover(int x, int y)
    {
        bool home = generator.DoorTile == new Vector2Int(x, y);
        if (!home && (generator.Tiles[x, y] != ForestGenerator.Wall || treeIcons.Length == 0)) return;
        int hash = unchecked(x * 73856093 ^ y * 19349663) & int.MaxValue;
        var icon = new GameObject(home ? "Home" : "Tree", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        icon.transform.SetParent(markers, false);
        icon.sprite = home ? homeIcon : treeIcons[hash % treeIcons.Length];
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        var rect = icon.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2((x + 0.5f) / generator.Width, (y + 0.5f) / generator.Height);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.one * (home ? homeSize : treeSize);
    }

    void OnDestroy()
    {
        if (fog) Destroy(fog);
    }
}

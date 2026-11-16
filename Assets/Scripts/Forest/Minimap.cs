using UnityEngine;
using UnityEngine.UI;

public class Minimap : MonoBehaviour
{
    public ForestGenerator generator;
    public Transform player;
    public RawImage mapImage;
    public RectTransform playerArrow;

    [Header("View")]
    public int viewTiles = 21;          // how many tiles across the minimap shows
    public int revealRadius = 2;        // tiles around you that get filled in

    [Header("Colors")]
    public Color unexploredColor = new Color(0f, 0f, 0f, 0f);
    public Color floorColor = new Color(0.85f, 0.8f, 0.65f, 1f);
    public Color wallColor = new Color(0.15f, 0.35f, 0.2f, 1f);
    public Color homeColor = new Color(1f, 0.85f, 0.2f, 1f);

    Texture2D tex;
    bool[] explored;
    Vector2Int lastTile = new Vector2Int(int.MinValue, int.MinValue);

    void Update()
    {
        if (!generator || !player || !mapImage || generator.Tiles == null) return;
        if (tex == null) Init();

        Vector2 f = generator.WorldToTileF(player.position);
        Vector2Int tile = Vector2Int.RoundToInt(f);
        if (tile != lastTile) { lastTile = tile; Reveal(tile); }

        // Scroll so the player sits in the middle (+1 = padding pixel, +0.5 = pixel center)
        float u = (f.x + 1.5f) / tex.width;
        float v = (f.y + 1.5f) / tex.height;
        float w = viewTiles / (float)tex.width;
        float h = viewTiles / (float)tex.height;
        mapImage.uvRect = new Rect(u - w / 2f, v - h / 2f, w, h);

        if (playerArrow) playerArrow.localEulerAngles = new Vector3(0f, 0f, -player.eulerAngles.y);
    }

    void Init()
    {
        int W = generator.Width, H = generator.Height;

        // 1-pixel unexplored border so scrolling past the edge shows nothing
        tex = new Texture2D(W + 2, H + 2, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        var gm = GameManager.I;
        if (gm != null)
        {
            if (gm.Explored == null || gm.Explored.Length != W * H) gm.Explored = new bool[W * H];
            explored = gm.Explored;
        }
        else explored = new bool[W * H];

        var pixels = new Color[tex.width * tex.height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = unexploredColor;
        tex.SetPixels(pixels);

        for (int x = 0; x < W; x++)
        for (int y = 0; y < H; y++)
            if (explored[x + y * W]) Paint(x, y);

        tex.Apply();
        mapImage.texture = tex;
    }

    void Reveal(Vector2Int c)
    {
        int W = generator.Width, H = generator.Height, r = revealRadius;
        bool changed = false;

        for (int dx = -r; dx <= r; dx++)
        for (int dy = -r; dy <= r; dy++)
        {
            if (dx * dx + dy * dy > r * r + r) continue;   // roughly circular
            int x = c.x + dx, y = c.y + dy;
            if (x < 0 || y < 0 || x >= W || y >= H) continue;
            int i = x + y * W;
            if (explored[i]) continue;
            explored[i] = true;
            Paint(x, y);
            changed = true;
        }
        if (changed) tex.Apply();
    }

    void Paint(int x, int y)
    {
        Color c = generator.Tiles[x, y] == ForestGenerator.Wall ? wallColor : floorColor;
        if (new Vector2Int(x, y) == generator.DoorTile) c = homeColor;   // the door
        tex.SetPixel(x + 1, y + 1, c);
    }
}
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Sprites;

public sealed class ForestWallArt : MonoBehaviour
{
    [SerializeField] Material windMaterial;
    [SerializeField] Sprite forestWall;
    [SerializeField] Sprite[] cornerTrees;

    readonly List<Mesh> meshes = new();

    public void Build(ForestGenerator maze)
    {
        var walls = new Cards();
        var trees = new Cards();
        var variants = new System.Random(GameManager.I ? GameManager.I.ForestSeed : 12345);
        float tile = maze.tileSize;
        Vector4 wallUV = DataUtility.GetOuterUV(forestWall);

        bool Floor(int x, int y) => x >= 0 && y >= 0 && x < maze.Width && y < maze.Height &&
            maze.Tiles[x, y] != ForestGenerator.Wall;

        for (int x = 0; x < maze.Width; x++)
        for (int y = 0; y < maze.Height; y++)
        {
            if (maze.Tiles[x, y] != ForestGenerator.Wall) continue;
            Vector3 center = maze.TileToWorld(x, y);
            foreach (var direction in Directions)
            {
                if (!Floor(x + direction.x, y + direction.y)) continue;
                // Leave the store-back tile bare so its illustration shows instead of trees.
                if (maze.storeBack && new Vector2Int(x, y) == maze.DoorTile && direction == Vector2Int.up) continue;
                Vector3 outward = new(direction.x, 0, direction.y);
                Vector3 along = direction.x == 0 ? Vector3.right : Vector3.forward;
                int column = direction.x == 0 ? x : y;
                int row = direction.x == 0 ? y : x;
                // Stagger perpendicular faces so the same painted tree doesn't fold around a corner.
                int panel = column + row + (direction.x == 0 ? 0 : 1);
                Vector3 edge = center + outward * tile * 0.5f;
                // One painting spans two tiles. Offset the recessed row to fill gaps between trunks.
                AddWallRow(walls, edge, along, tile, maze.wallHeight, wallUV, panel, 1f);
                AddWallRow(walls, edge - outward * tile * 0.2f, along, tile, maze.wallHeight * 1.12f,
                    wallUV, panel + 1, 0.72f);
            }

            for (int dx = -1; dx <= 1; dx += 2)
            for (int dy = -1; dy <= 1; dy += 2)
            {
                bool sideX = Floor(x + dx, y), sideY = Floor(x, y + dy);
                bool outer = sideX && sideY;
                bool inner = !sideX && !sideY && Floor(x + dx, y + dy);
                if (!outer && !inner) continue;

                Sprite sprite = cornerTrees[variants.Next(cornerTrees.Length)];
                float height = maze.wallHeight * (1.15f + (float)variants.NextDouble() * 0.22f);
                float halfWidth = height * sprite.rect.width / sprite.rect.height * 0.5f;
                float setback = Mathf.Min(tile * 0.08f, halfWidth * 0.25f);
                Vector3 foot = center + new Vector3(dx, 0, dy) * (tile * 0.5f - setback);
                trees.Add(foot, foot, height, DataUtility.GetOuterUV(sprite), halfWidth, 1f);
            }
        }
        Draw("Forest wall strips", walls, forestWall.texture);
        Draw("Billboard corner trees", trees, cornerTrees[0].texture);
    }

    static readonly Vector2Int[] Directions = { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };

    static void AddWallRow(Cards cards, Vector3 center, Vector3 along, float tile, float height,
        Vector4 rect, int column, float shade)
    {
        float start = (column % 2) * 0.5f;
        var uv = new Vector4(Mathf.Lerp(rect.x, rect.z, start), Mathf.Lerp(rect.y, rect.w, 0.20f),
            Mathf.Lerp(rect.x, rect.z, start + 0.5f), rect.w);
        Vector3 half = along * (tile * 0.5f);
        cards.Add(center - half, center + half, height, uv, 0f, shade);
    }

    // UV1 stores the camera-facing offset, wind weight and paint brightness.
    sealed class Cards
    {
        public readonly List<Vector3> vertices = new(), motion = new();
        public readonly List<Vector2> uv = new();
        public readonly List<int> indices = new();
        public float maxOffset;

        public void Add(Vector3 left, Vector3 right, float height, Vector4 rect, float offset, float shade)
        {
            int first = vertices.Count;
            maxOffset = Mathf.Max(maxOffset, offset);
            for (int row = 0; row <= 2; row++)
            {
                float t = row * 0.5f;
                float v = Mathf.Lerp(rect.y, rect.w, t);
                vertices.Add(left + Vector3.up * (height * t));
                vertices.Add(right + Vector3.up * (height * t));
                uv.Add(new Vector2(rect.x, v));
                uv.Add(new Vector2(rect.z, v));
                motion.Add(new Vector3(-offset, t * t, shade));
                motion.Add(new Vector3(offset, t * t, shade));
                if (row == 0) continue;
                int a = first + (row - 1) * 2;
                indices.Add(a); indices.Add(a + 2); indices.Add(a + 1);
                indices.Add(a + 1); indices.Add(a + 2); indices.Add(a + 3);
            }
        }
    }

    void Draw(string label, Cards cards, Texture texture)
    {
        if (cards.vertices.Count == 0) return;
        var mesh = new Mesh { name = label };
        meshes.Add(mesh);
        if (cards.vertices.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(cards.vertices);
        mesh.SetUVs(0, cards.uv);
        mesh.SetUVs(1, cards.motion);
        mesh.SetTriangles(cards.indices, 0);
        var bounds = mesh.bounds;
        bounds.Expand(2f * (cards.maxOffset + 0.3f));
        mesh.bounds = bounds;
        var obj = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
        obj.transform.SetParent(transform, true); // vertices are in the maze's world coordinates
        obj.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = obj.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = windMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        var properties = new MaterialPropertyBlock();
        properties.SetTexture("_MainTex", texture);
        renderer.SetPropertyBlock(properties);
    }

    void OnDestroy()
    {
        foreach (var mesh in meshes) Destroy(mesh);
    }
}

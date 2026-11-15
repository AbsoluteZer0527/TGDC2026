using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

public class ForestGenerator : MonoBehaviour
{
    public const int Wall = 0, Corridor = 1, Room = 2;

    [Serializable]
    public class CreatureSpawn
    {
        public Creature prefab;
        [Min(0)] public float weight = 1f;
    }

    [Header("Layout (in tiles)")]
    public int width = 41;                         // odd numbers; even gets bumped up by 1
    public int height = 41;
    public float tileSize = 4f;                    // corridor width in world units
    public float wallHeight = 4f;
    [Range(0f, 1f)] public float loopChance = 0.1f; // extra openings so the maze has loops
    public int roomCount = 25;
    public int minRoomSize = 3, maxRoomSize = 7;
    public int startRoomSize = 5;

    [Header("World")]
    public GameObject wallPrefab;
    public Transform ground;
    public NavMeshSurface navSurface;
    public float spawnClearRadius = 8f;            // no trees/creatures near the door

    [Header("Trees")]
    public GameObject[] treePrefabs;
    public int roomTreeCount = 250;
    [Range(0f, 1f)] public float corridorTreeChance = 0.15f;
    public float treeSpacing = 2.5f;               // keep >= 2.5 so trees never block a path

    [Header("Bushes")]
    public Vegetation bushPrefab;
    [Min(0)] public int bushCount = 70;
    [Min(0f)] public float bushSpacing = 1.5f;

    [Header("Creatures")]
    public CreatureSpawn[] creatures;
    public int creatureCount = 40;
    public float creatureSpacing = 4f;

    // Read by the minimap
    public int[,] Tiles { get; private set; }
    public int Width => width;
    public int Height => height;
    public Vector2Int CenterTile => new Vector2Int(width / 2, height / 2);

    readonly List<Vector3> used = new();
    readonly List<Vector2Int> roomTiles = new(), corridorTiles = new(), floorTiles = new();
    System.Random rng;

    void Start()
    {
        if (width % 2 == 0) width++;
        if (height % 2 == 0) height++;

        var gm = GameManager.I;
        rng = new System.Random(gm != null ? gm.ForestSeed : 12345);

        GenerateLayout();
        BuildWalls();
        if (ground)
        {
            ground.position = Vector3.zero;
            ground.localScale = new Vector3(width * tileSize / 10f, 1f, height * tileSize / 10f);
        }
        SpawnTrees();
        SpawnBushes();
        if (navSurface) navSurface.BuildNavMesh();   // after walls + vegetation exist
        SpawnCreatures(gm);
    }

    // ---------------- Layout ----------------
    void GenerateLayout()
    {
        var t = new int[width, height];

        // 1) Maze: random depth-first walk over the odd/odd tiles
        var stack = new Stack<Vector2Int>();
        t[1, 1] = Corridor;
        stack.Push(new Vector2Int(1, 1));
        Vector2Int[] dirs = { new(2, 0), new(-2, 0), new(0, 2), new(0, -2) };
        var options = new List<Vector2Int>(4);

        while (stack.Count > 0)
        {
            var c = stack.Peek();
            options.Clear();
            foreach (var d in dirs)
            {
                var n = c + d;
                if (Inside(n) && t[n.x, n.y] == Wall) options.Add(d);
            }
            if (options.Count == 0) { stack.Pop(); continue; }

            var pick = options[rng.Next(options.Count)];
            t[c.x + pick.x / 2, c.y + pick.y / 2] = Corridor;   // knock down the wall between
            t[c.x + pick.x, c.y + pick.y] = Corridor;
            stack.Push(c + pick);
        }

        // 2) Loops: open some walls that sit between two corridors
        for (int x = 1; x < width - 1; x++)
        for (int y = 1; y < height - 1; y++)
        {
            if (t[x, y] != Wall) continue;
            bool horiz = t[x - 1, y] != Wall && t[x + 1, y] != Wall && t[x, y - 1] == Wall && t[x, y + 1] == Wall;
            bool vert  = t[x, y - 1] != Wall && t[x, y + 1] != Wall && t[x - 1, y] == Wall && t[x + 1, y] == Wall;
            if ((horiz || vert) && rng.NextDouble() < loopChance) t[x, y] = Corridor;
        }

        // 3) Rooms (any rectangle 2+ tiles wide overlaps the maze, so it's always connected)
        int minR = Mathf.Max(2, minRoomSize);
        for (int i = 0; i < roomCount; i++)
        {
            int w = Mathf.Min(rng.Next(minR, maxRoomSize + 1), width - 2);
            int h = Mathf.Min(rng.Next(minR, maxRoomSize + 1), height - 2);
            int x0 = rng.Next(1, width - w);
            int y0 = rng.Next(1, height - h);
            Fill(t, x0, y0, w, h, Room);
        }

        // 4) Start room in the center (door + player spawn)
        int s = startRoomSize;
        Fill(t, width / 2 - s / 2, height / 2 - s / 2, s, s, Room);

        Tiles = t;
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            if (t[x, y] == Wall) continue;
            var p = new Vector2Int(x, y);
            floorTiles.Add(p);
            (t[x, y] == Room ? roomTiles : corridorTiles).Add(p);
        }
    }

    bool Inside(Vector2Int p) => p.x >= 1 && p.x <= width - 2 && p.y >= 1 && p.y <= height - 2;

    void Fill(int[,] t, int x0, int y0, int w, int h, int value)
    {
        for (int x = Mathf.Max(1, x0); x < Mathf.Min(width - 1, x0 + w); x++)
        for (int y = Mathf.Max(1, y0); y < Mathf.Min(height - 1, y0 + h); y++)
            t[x, y] = value;
    }

    public Vector3 TileToWorld(int x, int y) =>
        new Vector3((x - width / 2) * tileSize, 0f, (y - height / 2) * tileSize);

    public Vector2 WorldToTileF(Vector3 p) =>
        new Vector2(p.x / tileSize + width / 2, p.z / tileSize + height / 2);

    // ---------------- Walls ----------------
    void BuildWalls()
    {
        var root = new GameObject("Walls").transform;
        root.SetParent(transform);

        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            if (Tiles[x, y] != Wall || !TouchesFloor(x, y)) continue;
            var w = Instantiate(wallPrefab, TileToWorld(x, y) + Vector3.up * (wallHeight / 2f),
                                Quaternion.identity, root);
            w.transform.localScale = new Vector3(tileSize, wallHeight, tileSize);
        }
        StaticBatchingUtility.Combine(root.gameObject);   // fewer draw calls
    }

    bool TouchesFloor(int x, int y)
    {
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            int nx = x + dx, ny = y + dy;
            if (nx >= 0 && ny >= 0 && nx < width && ny < height && Tiles[nx, ny] != Wall) return true;
        }
        return false;
    }

    // ---------------- Trees ----------------
    void SpawnTrees()
    {
        float roomJitter = tileSize / 2f - 0.6f;
        for (int i = 0; i < roomTreeCount; i++)
            if (TryFloorPoint(roomTiles, roomJitter, treeSpacing, out var p)) PlaceTree(p);

        foreach (var tile in corridorTiles)
        {
            if (rng.NextDouble() >= corridorTreeChance) continue;
            // near the tile center only, so there's always room to walk around it
            Vector3 p = TileToWorld(tile.x, tile.y) + new Vector3(Range(-0.3f, 0.3f), 0f, Range(-0.3f, 0.3f));
            if (TryUse(p, treeSpacing)) PlaceTree(p);
        }
    }

    void PlaceTree(Vector3 p)
    {
        var prefab = treePrefabs[rng.Next(treePrefabs.Length)];
        float yaw = Range(0f, 360f);
        float scale = Range(0.8f, 1.3f);
        var tree = Instantiate(prefab, p, Quaternion.Euler(0, yaw, 0), transform);
        tree.transform.localScale *= scale;
    }

    void SpawnBushes()
    {
        if (!bushPrefab) return;

        float jitter = tileSize / 2f - 0.6f;
        for (int i = 0; i < bushCount; i++)
        {
            if (!TryFloorPoint(roomTiles, jitter, bushSpacing, out var p)) continue;
            var bush = Instantiate(bushPrefab, p, Quaternion.identity, transform);
            bush.PickSprite(rng);
        }
    }

    // ---------------- Creatures ----------------
    void SpawnCreatures(GameManager gm)
    {
        float jitter = tileSize / 2f - 1f;
        for (int i = 0; i < creatureCount; i++)
        {
            bool found = TryFloorPoint(floorTiles, jitter, creatureSpacing, out var p);
            var prefab = PickCreature();                 // always roll, keeps later spawns identical
            if (!found || prefab == null) continue;
            if (gm != null && gm.KilledCreatures.Contains(i)) continue;

            var c = Instantiate(prefab, p, Quaternion.Euler(0, Range(0f, 360f), 0), transform);
            c.id = i;
        }
    }

    Creature PickCreature()
    {
        float total = 0f;
        foreach (var c in creatures) total += c.weight;
        float r = Range(0f, total);
        foreach (var c in creatures)
        {
            r -= c.weight;
            if (r <= 0f) return c.prefab;
        }
        return creatures.Length > 0 ? creatures[^1].prefab : null;
    }

    // ---------------- Helpers ----------------
    bool TryFloorPoint(List<Vector2Int> tiles, float jitter, float spacing, out Vector3 point)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            var t = tiles[rng.Next(tiles.Count)];
            Vector3 p = TileToWorld(t.x, t.y) + new Vector3(Range(-jitter, jitter), 0f, Range(-jitter, jitter));
            if (TryUse(p, spacing)) { point = p; return true; }
        }
        point = default;
        return false;
    }

    bool TryUse(Vector3 p, float spacing)
    {
        if (p.magnitude < spawnClearRadius) return false;
        foreach (var u in used)
            if ((u - p).sqrMagnitude < spacing * spacing) return false;
        used.Add(p);
        return true;
    }

    float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
}
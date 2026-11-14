using System.Collections.Generic;
using UnityEngine;

public class ForestGenerator : MonoBehaviour
{
    [Header("World")]
    public float size = 80f;              // square world, centered on (0,0,0)
    public Transform ground;              // a Plane at the origin
    public float spawnClearRadius = 8f;   // keep the area around the door empty

    [Header("Trees")]
    public GameObject[] treePrefabs;
    public int treeCount = 150;
    public float treeSpacing = 2.5f;

    [Header("Creatures")]
    public Creature[] creaturePrefabs;
    public int creatureCount = 20;
    public float creatureSpacing = 4f;

    readonly List<Vector3> used = new();
    System.Random rng;

    // Start (not Awake) so GameManager is guaranteed to exist first
    void Start()
    {
        var gm = GameManager.I;
        rng = new System.Random(gm != null ? gm.ForestSeed : 12345);

        if (ground) ground.localScale = new Vector3(size / 10f, 1, size / 10f);
        BuildWalls();

        for (int i = 0; i < treeCount; i++)
        {
            if (!TryGetPoint(treeSpacing, out var p)) continue;
            var prefab = treePrefabs[rng.Next(treePrefabs.Length)];
            float yaw = Range(0f, 360f);
            float scale = Range(0.8f, 1.3f);
            var tree = Instantiate(prefab, p, Quaternion.Euler(0, yaw, 0), transform);
            tree.transform.localScale *= scale;
        }

        for (int i = 0; i < creatureCount; i++)
        {
            // Always use up the same random numbers, even for dead creatures,
            // so every later position stays identical.
            bool found = TryGetPoint(creatureSpacing, out var p);
            var prefab = creaturePrefabs[rng.Next(creaturePrefabs.Length)];
            if (!found) continue;
            if (gm != null && gm.KilledCreatures.Contains(i)) continue;

            var c = Instantiate(prefab, p, Quaternion.identity, transform);
            c.id = i;
        }
    }

    bool TryGetPoint(float spacing, out Vector3 point)
    {
        float half = size / 2f - 2f;
        for (int attempt = 0; attempt < 30; attempt++)
        {
            var p = new Vector3(Range(-half, half), 0, Range(-half, half));
            if (p.magnitude < spawnClearRadius) continue;

            bool ok = true;
            foreach (var u in used)
                if ((u - p).sqrMagnitude < spacing * spacing) { ok = false; break; }
            if (!ok) continue;

            used.Add(p);
            point = p;
            return true;
        }
        point = default;
        return false;
    }

    float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    void BuildWalls()
    {
        float h = size / 2f;
        Wall(new Vector3(0, 2, h),  new Vector3(size, 4, 1));
        Wall(new Vector3(0, 2, -h), new Vector3(size, 4, 1));
        Wall(new Vector3(h, 2, 0),  new Vector3(1, 4, size));
        Wall(new Vector3(-h, 2, 0), new Vector3(1, 4, size));
    }

    void Wall(Vector3 pos, Vector3 boxSize)
    {
        var w = new GameObject("Wall");
        w.transform.SetParent(transform);
        w.transform.position = pos;
        w.AddComponent<BoxCollider>().size = boxSize;
    }
}
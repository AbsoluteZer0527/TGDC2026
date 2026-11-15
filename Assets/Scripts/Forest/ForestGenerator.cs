using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

public class ForestGenerator : MonoBehaviour
{
    [Serializable]
    public class CreatureSpawn
    {
        public Creature prefab;
        [Min(0)] public float weight = 1f;   // higher = more common
    }

    [Header("World")]
    public float size = 80f;
    public Transform ground;
    public float spawnClearRadius = 8f;
    public NavMeshSurface navSurface;

    [Header("Trees")]
    public Vegetation treePrefab;
    [Min(0)] public int treeCount = 150;
    [Min(0f)] public float treeSpacing = 2.5f;

    [Header("Bushes")]
    public Vegetation bushPrefab;
    [Min(0)] public int bushCount = 70;
    [Min(0f)] public float bushSpacing = 1.5f;

    [Header("Creatures")]
    public CreatureSpawn[] creatures;
    public int creatureCount = 30;
    public float creatureSpacing = 4f;

    readonly List<Vector3> used = new();
    System.Random rng;

    void Start()
    {
        var gm = GameManager.I;
        rng = new System.Random(gm != null ? gm.ForestSeed : 12345);

        if (ground) ground.localScale = new Vector3(size / 10f, 1, size / 10f);
        BuildWalls();

        SpawnVegetation(treePrefab, treeCount, treeSpacing);
        SpawnVegetation(bushPrefab, bushCount, bushSpacing);

        // 2) Walkable map, now that obstacles exist
        if (navSurface) navSurface.BuildNavMesh();

        // 3) Creatures (always use up the same random numbers, even for dead ones)
        for (int i = 0; i < creatureCount; i++)
        {
            bool found = TryGetPoint(creatureSpacing, out var p);
            var prefab = PickCreature();
            if (!found || prefab == null) continue;
            if (gm != null && gm.KilledCreatures.Contains(i)) continue;

            var c = Instantiate(prefab, p, Quaternion.Euler(0, Range(0f, 360f), 0), transform);
            c.id = i;
        }
    }

    void SpawnVegetation(Vegetation prefab, int count, float spacing)
    {
        if (!prefab) return;
        for (int i = 0; i < count; i++)
        {
            if (!TryGetPoint(spacing, out var point)) continue;
            var plant = Instantiate(prefab, point, Quaternion.identity, transform);
            plant.PickSprite(rng);
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

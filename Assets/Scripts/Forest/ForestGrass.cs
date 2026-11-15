using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways, DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class ForestGrass : MonoBehaviour
{
    [Tooltip("Average number of tufts per square metre.")]
    [SerializeField, Range(0f, 40f)] float density = 20f;
    [Tooltip("Typical blade height in metres.")]
    [SerializeField, Range(0.04f, 0.6f)] float bladeHeight = 0.19f;
    [Tooltip("Tip sway as a fraction of blade height.")]
    [SerializeField, Range(0f, 1f)] float windStrength = 0.24f;
    [Tooltip("Blades taper into the painted floor before this distance in metres.")]
    [SerializeField, Range(4f, 60f)] float drawDistance = 24f;

    const float PatchSize = 6f;
    const int Variants = 4;
    readonly Mesh[] patches = new Mesh[Variants];
    MeshFilter floor;
    MeshRenderer floorRenderer;
    MaterialPropertyBlock properties;
    Vector2 lastPatchSize;
    float lastDensity = -1f;

    void OnEnable()
    {
        floor = GetComponent<MeshFilter>();
        floorRenderer = GetComponent<MeshRenderer>();
        properties = new MaterialPropertyBlock();
        RenderPipelineManager.beginCameraRendering += Draw;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= Draw;
        ReleasePatches();
    }

    void ReleasePatches()
    {
        for (int i = 0; i < patches.Length; i++)
        {
            if (!patches[i]) continue;
            if (Application.isPlaying) Destroy(patches[i]);
            else DestroyImmediate(patches[i]);
            patches[i] = null;
        }
    }

    void Draw(ScriptableRenderContext context, Camera camera)
    {
        if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) return;
        if ((camera.cullingMask & (1 << gameObject.layer)) == 0 || !floorRenderer.enabled || !floor.sharedMesh || density <= 0f) return;
        Material material = floorRenderer.sharedMaterial;
        if (!material || !material.HasProperty("_IsGrass")) return;

        // The generator resizes the plane at Start. Use its actual bounds in both edit and play mode.
        Bounds bounds = floor.sharedMesh.bounds;
        float width = bounds.size.x * transform.TransformVector(Vector3.right).magnitude;
        float depth = bounds.size.z * transform.TransformVector(Vector3.forward).magnitude;
        if (width <= 0f || depth <= 0f) return;
        int columns = Mathf.CeilToInt(width / PatchSize);
        int rows = Mathf.CeilToInt(depth / PatchSize);
        Vector2 patchSize = new Vector2(width / columns, depth / rows);
        if (!patches[0] || patchSize != lastPatchSize || density != lastDensity)
        {
            ReleasePatches();
            for (int i = 0; i < Variants; i++)
                patches[i] = BuildPatch(patchSize, density, 7369 + i * 7919);
            lastPatchSize = patchSize;
            lastDensity = density;
        }

        properties.SetFloat("_IsGrass", 1f);
        properties.SetFloat("_BladeHeight", bladeHeight);
        properties.SetFloat("_WindStrength", windStrength);
        properties.SetFloat("_DrawDistance", drawDistance);
        var draw = new RenderParams(material)
        {
            camera = camera,
            layer = gameObject.layer,
            matProps = properties,
            receiveShadows = true,
            shadowCastingMode = ShadowCastingMode.Off
        };

        Vector3 center = transform.TransformPoint(bounds.center);
        Quaternion rotation = transform.rotation;
        Vector3 cameraPosition = camera.transform.position;
        float reach = drawDistance + 0.5f * patchSize.magnitude + bladeHeight * 2f;
        for (int z = 0; z < rows; z++)
        for (int x = 0; x < columns; x++)
        {
            var local = new Vector3((x + 0.5f) * patchSize.x - width * 0.5f, 0f,
                                    (z + 0.5f) * patchSize.y - depth * 0.5f);
            Vector3 position = center + rotation * local;
            if ((position - cameraPosition).sqrMagnitude > reach * reach) continue;
            uint hash = ((uint)x * 73856093u) ^ ((uint)z * 19349663u);
            hash ^= hash >> 11;
            Quaternion facing = (hash & 4) == 0 ? rotation : rotation * Quaternion.Euler(0f, 180f, 0f);
            Graphics.RenderMesh(draw, patches[hash % Variants], 0, Matrix4x4.TRS(position, facing, Vector3.one));
        }
    }

    // A mesh contains many tufts. Four reusable layouts keep mesh memory independent of forest size.
    static Mesh BuildPatch(Vector2 size, float tuftsPerMeter, int seed)
    {
        var rng = new System.Random(seed);
        var vertices = new List<Vector3>();
        var bladeData = new List<Vector4>();
        var indices = new List<int>();
        float Random01() => (float)rng.NextDouble();
        void Triangle(int a, int b, int c)
        {
            indices.Add(a);
            indices.Add(b);
            indices.Add(c);
        }

        int count = Mathf.RoundToInt(size.x * size.y * tuftsPerMeter);
        for (int tuft = 0; tuft < count; tuft++)
        {
            Vector3 root = new Vector3((Random01() - 0.5f) * size.x, 0f, (Random01() - 0.5f) * size.y);
            float tuftScale = Mathf.Lerp(0.70f, 1.14f, Random01());
            float angle = Random01() * 2f * Mathf.PI;
            void Vertex(Vector3 offset, float bend, float palette)
            {
                vertices.Add(root + offset * tuftScale);
                bladeData.Add(new Vector4(root.x, root.z, bend, palette));
            }

            for (int blade = 0; blade < 6; blade++)
            {
                angle += 2.4f + Random01() * 0.65f;
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 side = new Vector3(-outward.z, 0f, outward.x) * Mathf.Lerp(0.08f, 0.14f, Random01());
                Vector3 foot = outward * (0.06f + 0.24f * Random01());
                Vector3 rise = Vector3.up * Mathf.Lerp(0.75f, 1.2f, Random01());
                Vector3 lean = outward * Mathf.Lerp(0.30f, 0.82f, Random01());
                Vector3 middle = foot + rise * 0.54f + lean * 0.23f;
                float shade = Random01();
                int first = vertices.Count;
                Vertex(foot - side, 0f, shade);
                Vertex(foot + side, 0f, shade);
                Vertex(middle - side * 0.55f, 0.54f, shade);
                Vertex(middle + side * 0.55f, 0.54f, shade);
                Vertex(foot + rise + lean, 1f, shade);
                Triangle(first, first + 2, first + 1);
                Triangle(first + 1, first + 2, first + 3);
                Triangle(first + 2, first + 4, first + 3);
            }

            if (Random01() >= 0.028f) continue;
            // Narrow stem and five slightly cupped petals, sharing the tip's wind weight.
            Vector3 head = new Vector3(0.12f, 1.14f, 0.03f);
            int stem = vertices.Count;
            Vertex(Vector3.left * 0.025f, 0f, 0.6f);
            Vertex(Vector3.right * 0.025f, 0f, 0.6f);
            Vertex(head, 1f, 0.6f);
            Triangle(stem, stem + 1, stem + 2);
            float flower = rng.Next(2, 5);
            for (int petal = 0; petal < 5; petal++)
            {
                angle += 2f * Mathf.PI / 5f;
                var direction = new Vector3(Mathf.Cos(angle), 0.25f + 0.3f * Mathf.Sin(angle), Mathf.Sin(angle));
                var across = new Vector3(-direction.z, 0f, direction.x) * 0.054f;
                int first = vertices.Count;
                Vertex(head, 1f, flower);
                Vertex(head + direction * 0.12f - across, 1f, flower);
                Vertex(head + direction * 0.17f, 1f, flower);
                Vertex(head + direction * 0.12f + across, 1f, flower);
                Triangle(first, first + 1, first + 2);
                Triangle(first, first + 2, first + 3);
            }
        }

        var mesh = new Mesh { name = "Forest grass patch", hideFlags = HideFlags.HideAndDontSave };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, bladeData);
        mesh.SetTriangles(indices, 0);
        // Include the highest Inspector settings for height, lean and wind when Unity culls a patch.
        mesh.bounds = new Bounds(Vector3.up * 0.5f, new Vector3(size.x + 4f, 1.2f, size.y + 4f));
        mesh.UploadMeshData(true);
        return mesh;
    }
}

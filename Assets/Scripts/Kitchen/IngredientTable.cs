using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Spawns one IngredientRunner per ingredient, decides when they're on the counter, and handles clicks on them.
public class IngredientTable : MonoBehaviour
{
    public KitchenController kitchen;
    public IngredientRunner runnerPrefab;
    [Tooltip("Empty transform on the counter top. X/Z scale = running area, Y position = surface height.")]
    public Transform area;
    [Tooltip("Only the Ingredient layer.")]
    public LayerMask clickMask = ~0;

    public float dropHeight = 4f;
    public float throwDuration = 0.45f, throwArc = 1.5f;
    [Tooltip("World-space height offset for veggies resting above the pot rim.")]
    [SerializeField] float potRestHeightOffset = 0.08f;
    public float fallAwayDepth = 6f;
    [Tooltip("Full-screen RectTransform on a canvas sorted ABOVE the dialogue. Thrown veggies are drawn here so UI can't hide them.")]
    public RectTransform flyLayer;

    public float SurfaceY => area.position.y;
    public Transform PotTarget => kitchen.potTarget;
    public bool ShouldShow { get; private set; }
    public Camera Cam => cam;

    readonly List<IngredientRunner> runners = new();
    sealed class PotVeggie
    {
        public SpriteRenderer sprite;
        public Vector3 rest;
        public float hopTime;
    }

    readonly List<PotVeggie> potVeggies = new();
    SpriteRenderer stewSurface;
    Camera cam;
    GameManager GM => GameManager.I;

    void Start()
    {
        cam = Camera.main;
        if (kitchen.soupVisual) stewSurface = kitchen.soupVisual.GetComponent<SpriteRenderer>();
        foreach (var ing in kitchen.ingredients)
        {
            var r = Instantiate(runnerPrefab, transform);
            r.Init(ing, this);
            runners.Add(r);
        }
        kitchen.IngredientAdded += OnAdded;
        kitchen.PotDiscarded += TossPotVeggies;
        if (GM.Soup == SoupState.Filling)
            foreach (var ing in GM.PotContents) AddPotVeggie(ing);
    }

    void OnDestroy()
    {
        if (kitchen)
        {
            kitchen.IngredientAdded -= OnAdded;
            kitchen.PotDiscarded -= TossPotVeggies;
        }
        ClearPotVeggies();
    }

    void Update()
    {
        if (GM.Soup != SoupState.Filling) ClearPotVeggies();
        else
        {
            const float hopDuration = 0.32f;
            foreach (var veggie in potVeggies)
            {
                if (!veggie.sprite.enabled) continue;
                veggie.hopTime += Time.deltaTime;
                if (veggie.hopTime >= hopDuration) veggie.hopTime = -Random.Range(0.15f, 0.95f);
                float t = Mathf.Clamp01(veggie.hopTime / hopDuration);
                veggie.sprite.transform.position = veggie.rest + Vector3.up * (4f * t * (1f - t) * runnerPrefab.hopHeight);
            }
        }
        // On the counter once the customer has finished giving the order, and only while the pot accepts ingredients.
        bool show = !GM.DayOver && GM.Phase == CustomerPhase.AtCounter && GM.CurrentCustomerLine == null &&
                    GM.CurrentOrder.Count > 0 && (GM.Soup == SoupState.Empty || GM.Soup == SoupState.Filling);
        if (show != ShouldShow)
        {
            ShouldShow = show;
            foreach (var r in runners)
            {
                if (!show) r.Leave();
                else if (GM.ingredientRefresh || !GM.PotContents.Contains(r.Data)) r.Drop();
            }
        }

        if (!ShouldShow) return;

        // Refresh off: veggies in the soup stay hidden. Once Discard empties the pot, bring them back.
        foreach (var r in runners)
            if (r.IsHidden && !GM.PotContents.Contains(r.Data)) r.Drop();

        var mouse = Mouse.current;
        if (CurtainTransition.IsPlaying || mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
        if (EventSystem.current && EventSystem.current.IsPointerOverGameObject()) return; // clicked on UI

        var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
        if (!Physics.Raycast(ray, out var hit, 100f, clickMask, QueryTriggerInteraction.Collide)) return;
        var runner = hit.collider.GetComponentInParent<IngredientRunner>();
        if (runner && runner.Clickable) kitchen.TryAddIngredient(runner.Data); // fires IngredientAdded -> Throw
    }

    void OnAdded(IngredientData ing)
    {
        if (GM.PotContents.Count == 1) ClearPotVeggies();
        var landing = AddPotVeggie(ing);
        var r = runners.Find(x => x.Data == ing);
        if (r) r.Throw(landing);
    }

    SpriteRenderer AddPotVeggie(IngredientData ing)
    {
        if (!stewSurface || !stewSurface.sprite) return null;
        Sprite art = ing.frames != null && ing.frames.Length > 0 ? ing.frames[0] : ing.icon;
        if (!art) art = runnerPrefab.sprite.sprite;
        if (!art) return null;

        var surface = stewSurface.transform;
        var veggie = new GameObject(ing.displayName + " in pot").AddComponent<SpriteRenderer>();
        var runner = runners.Find(r => r.Data == ing);
        veggie.transform.localScale = (runner ? runner : runnerPrefab).sprite.transform.lossyScale;
        veggie.transform.SetParent(transform, true);
        veggie.sprite = art;
        veggie.sharedMaterial = stewSurface.sharedMaterial;
        veggie.sortingLayerID = stewSurface.sortingLayerID;
        veggie.sortingOrder = stewSurface.sortingOrder + 1;

        Bounds bowl = stewSurface.sprite.bounds;
        veggie.transform.rotation = surface.rotation;
        int index = potVeggies.Count;
        float angle = index * 2.399963f; // golden angle spreads successive throws around the oval
        Vector3 spot = bowl.center;
        if (index > 0)
            spot += new Vector3(Mathf.Cos(angle) * bowl.extents.x * 0.64f, Mathf.Sin(angle) * bowl.extents.y * 0.27f, 0f);
        spot.z -= 0.01f;
        Vector3 rest = surface.TransformPoint(spot) - veggie.transform.TransformVector(art.bounds.center);
        rest.y += potRestHeightOffset;
        rest.z = surface.TransformPoint(bowl.center + Vector3.back * 0.01f).z;
        veggie.transform.position = rest;
        potVeggies.Add(new PotVeggie { sprite = veggie, rest = rest, hopTime = -Random.Range(0.15f, 0.95f) });
        return veggie;
    }

    void TossPotVeggies()
    {
        if (potVeggies.Count == 0)
            foreach (var ing in GM.PotContents) AddPotVeggie(ing);
        foreach (var runner in runners) runner.ReleaseDiscardedThrow();
        foreach (var veggie in potVeggies) StartCoroutine(TossOut(veggie.sprite.transform));
        potVeggies.Clear();
    }

    IEnumerator TossOut(Transform veggie)
    {
        Vector3 start = veggie.position;
        Vector3 velocity = new Vector3(Random.Range(1.5f, 3f) * (Random.value < 0.5f ? -1f : 1f), Random.Range(3f, 5f), 0f);
        for (float t = 0; veggie.position.y >= start.y - fallAwayDepth; t += Time.deltaTime)
        {
            veggie.position = start + velocity * t + Vector3.down * (10f * t * t);
            yield return null;
        }
        Destroy(veggie.gameObject);
    }

    void ClearPotVeggies()
    {
        foreach (var veggie in potVeggies)
        {
            if (!veggie.sprite) continue;
            veggie.sprite.gameObject.SetActive(false);
            Destroy(veggie.sprite.gameObject);
        }
        potVeggies.Clear();
    }

    // Height of the area's (possibly tilted) top plane at this X/Z, so veggies can walk on a slanted board.
    public float SurfaceHeightAt(Vector3 p)
    {
        Vector3 n = area.up, o = area.position;
        if (Mathf.Abs(n.y) < 0.01f) return o.y; // area stood on its side: fall back to flat
        return o.y - (n.x * (p.x - o.x) + n.z * (p.z - o.z)) / n.y;
    }

    public Vector3 RandomPoint() =>
        area.TransformPoint(new Vector3(Random.Range(-0.5f, 0.5f), 0f, Random.Range(-0.5f, 0.5f)));
}

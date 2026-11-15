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
    public float fallAwayDepth = 6f;
    [Tooltip("Full-screen RectTransform on a canvas sorted ABOVE the dialogue. Thrown veggies are drawn here so UI can't hide them.")]
    public RectTransform flyLayer;

    public float SurfaceY => area.position.y;
    public Transform PotTarget => kitchen.potTarget;
    public bool ShouldShow { get; private set; }
    public Camera Cam => cam;

    readonly List<IngredientRunner> runners = new();
    Camera cam;
    GameManager GM => GameManager.I;

    void Start()
    {
        cam = Camera.main;
        foreach (var ing in kitchen.ingredients)
        {
            var r = Instantiate(runnerPrefab, transform);
            r.Init(ing, this);
            runners.Add(r);
        }
        kitchen.IngredientAdded += OnAdded;
    }

    void OnDestroy()
    {
        if (kitchen) kitchen.IngredientAdded -= OnAdded;
    }

    void Update()
    {
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
        var r = runners.Find(x => x.Data == ing);
        if (r) r.Throw();
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

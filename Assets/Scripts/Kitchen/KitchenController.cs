using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class KitchenController : MonoBehaviour
{
    [Header("Data")]
    public IngredientData[] ingredients;
    public int minOrderSize = 2, maxOrderSize = 3;
    public int soupPrice = 10;

    [Header("Timing meter")]
    public TimingMeter meter;
    [Tooltip("Seconds added for a perfect hit (top)")]
    public float fastSecondsPerIngredient = 1f;
    [Tooltip("Seconds added for the worst hit (bottom)")]
    public float slowSecondsPerIngredient = 6f;

    [Header("UI")]
    public Button ingredientButtonPrefab;
    public Transform ingredientButtonParent;
    public Button cookButton, serveButton, discardButton;
    public TMP_Text orderText, potText;
    public RectTransform orderBubble;

    [Header("Customer")]
    public Transform customer;           // root object, feet on the floor
    public Renderer customerRenderer;    // the Visual quad
    public Transform customerArea;       // invisible box customers can stand in
    public float minMoveDistance = 1.5f;
    public Vector3 bubbleOffset = new Vector3(0, 2.3f, 0);

    [Header("Pot")]
    public GameObject soupVisual, readyVisual;
    public Transform throwFrom, potTarget;

    readonly Dictionary<IngredientData, Button> buttons = new();
    string lastThrow = "";
    Camera cam;
    GameManager GM => GameManager.I;

    void Start()
    {
        cam = Camera.main;
        foreach (var ing in ingredients)
        {
            var b = Instantiate(ingredientButtonPrefab, ingredientButtonParent);
            b.GetComponentInChildren<TMP_Text>().text = ing.displayName;
            b.onClick.AddListener(() => OnIngredientClicked(ing));
            buttons[ing] = b;
        }
        cookButton.onClick.AddListener(() => { GM.StartCooking(); lastThrow = ""; Deselect(); });
        serveButton.onClick.AddListener(Serve);
        discardButton.onClick.AddListener(Discard);

        if (GM.CurrentOrder.Count == 0) NewCustomer(); else ShowCustomer();
    }

    void OnIngredientClicked(IngredientData ing)
    {
        Deselect(); // otherwise Space could re-press the selected button
        if (meter.Running || GM.DayOver || !GM.CanAddIngredient(ing)) return;

        meter.Begin(t =>
        {
            float secs = Mathf.Lerp(slowSecondsPerIngredient, fastSecondsPerIngredient, t);
            if (GM.AddIngredient(ing, secs))
            {
                lastThrow = $"{ing.displayName}: +{secs:0.0}s";
                StartCoroutine(ThrowVisual(ing.color));
            }
        });
    }

    void Discard()
    {
        meter.Cancel();
        GM.DiscardSoup();
        lastThrow = "";
        Deselect();
    }

    void Serve()
    {
        Deselect();
        var soup = GM.TakeSoup();
        if (soup == null) return;

        bool correct = soup.Count == GM.CurrentOrder.Count && !soup.Except(GM.CurrentOrder).Any();
        if (correct) GM.SellSoup(soupPrice);
        NewCustomer();
    }

    void Update()
    {
        bool busy = meter.Running || GM.DayOver;

        foreach (var kv in buttons)
        {
            kv.Value.interactable = !busy && GM.CanAddIngredient(kv.Key);
            kv.Value.image.color = GM.PotContents.Contains(kv.Key) ? kv.Key.color : Color.white;
        }
        cookButton.interactable    = !busy && GM.Soup == SoupState.Filling;
        serveButton.interactable   = !GM.DayOver && GM.Soup == SoupState.Ready;
        discardButton.interactable = !GM.DayOver && (GM.Soup != SoupState.Empty || meter.Running);

        potText.text = meter.Running ? "Press SPACE!\n(top = faster)" : GM.Soup switch
        {
            SoupState.Filling => $"{lastThrow}\nCook time: {GM.TotalCookTime:0.0}s\nAdd more or Cook",
            SoupState.Cooking => $"Cooking... {Mathf.CeilToInt(GM.CookTimeLeft)}s",
            SoupState.Ready   => "Soup's ready! Serve it.",
            _                 => "Click an ingredient\nto throw it in"
        };

        if (soupVisual)  soupVisual.SetActive(GM.Soup != SoupState.Empty);
        if (readyVisual) readyVisual.SetActive(GM.Soup == SoupState.Ready);

        // Speech bubble follows the customer's head
        if (orderBubble && cam && customer)
            orderBubble.position = cam.WorldToScreenPoint(customer.position + bubbleOffset);
    }

    void NewCustomer()
    {
        int n = Random.Range(minOrderSize, maxOrderSize + 1);
        GM.CurrentOrder.Clear();
        GM.CurrentOrder.AddRange(ingredients.OrderBy(_ => Random.value).Take(n));
        GM.CustomerColor = Random.ColorHSV(0f, 1f, 0.4f, 0.8f, 0.8f, 1f);

        Vector3 prev = GM.CustomerPos, p = prev;
        for (int i = 0; i < 10; i++)
        {
            p = customerArea.TransformPoint(new Vector3(Random.Range(-0.5f, 0.5f), 0f, Random.Range(-0.5f, 0.5f)));
            if (Vector3.Distance(p, prev) >= minMoveDistance) break;
        }
        GM.CustomerPos = p;
        ShowCustomer();
    }

    void ShowCustomer()
    {
        orderText.text = "One soup with:\n" + string.Join(", ", GM.CurrentOrder.Select(i => i.displayName));
        customer.position = GM.CustomerPos;
        if (customerRenderer) customerRenderer.material.color = GM.CustomerColor;
    }

    IEnumerator ThrowVisual(Color color)
    {
        if (!throwFrom || !potTarget) yield break;
        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(ball.GetComponent<Collider>());
        ball.transform.localScale = Vector3.one * 0.3f;
        ball.GetComponent<Renderer>().material.color = color;

        Vector3 a = throwFrom.position, b = potTarget.position;
        for (float t = 0; t < 1f; t += Time.deltaTime / 0.4f)
        {
            ball.transform.position = Vector3.Lerp(a, b, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 1.5f;
            yield return null;
        }
        Destroy(ball);
    }

    static void Deselect()
    {
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
    }
}
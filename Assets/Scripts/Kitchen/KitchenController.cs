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
    [Min(0.01f)] public float entranceDuration = 0.9f;
    [Min(0f)] public float stepHeight = 0.09f;
    public Vector3 bubbleOffset = new Vector3(0, 2.3f, 0);

    [Header("Pot")]
    public GameObject soupVisual, readyVisual;
    public Transform throwFrom, potTarget;

    readonly Dictionary<IngredientData, Button> buttons = new();
    string lastThrow = "";
    Camera cam;
    Vector3 customerStop, bubbleScale;
    bool customerEntering;
    GameManager GM => GameManager.I;

    void Start()
    {
        cam = Camera.main;
        customerStop = customer.position;
        if (orderBubble) bubbleScale = orderBubble.localScale;
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
        if (customerEntering) return;
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
        serveButton.interactable   = !customerEntering && !GM.DayOver && GM.Soup == SoupState.Ready;
        discardButton.interactable = !GM.DayOver && (GM.Soup != SoupState.Empty || meter.Running);

        potText.text = meter.Running ? "Press SPACE!\n(top = faster)" : GM.Soup switch
        {
            SoupState.Filling => $"{lastThrow}\nCook time: {GM.TotalCookTime:0.0}s\nAdd more or Cook",
            SoupState.Cooking => $"Cooking... {Mathf.CeilToInt(GM.CookTimeLeft)}s",
            SoupState.Ready   => "Soup's ready! Serve it.",
            _                 => "Click an ingredient\nto throw it in"
        };

        if (soupVisual)  soupVisual.SetActive(GM.Soup == SoupState.Cooking || GM.Soup == SoupState.Ready);
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

        ShowCustomer();
        StartCoroutine(CustomerEntrance());
    }

    void ShowCustomer()
    {
        orderText.text = "One soup with:\n" + string.Join(", ", GM.CurrentOrder.Select(i => i.displayName));
        if (customerRenderer) customerRenderer.material.color = GM.CustomerColor;
    }

    IEnumerator CustomerEntrance()
    {
        customerEntering = true;
        if (orderBubble) orderBubble.gameObject.SetActive(false);
        Vector3 start = customerStop + Vector3.right * 8f;
        if (cam)
        {
            Vector3 viewport = cam.WorldToViewportPoint(customerStop);
            viewport.x = 1.2f;
            start = cam.ViewportToWorldPoint(viewport);
        }

        for (float elapsed = 0f; elapsed < entranceDuration; elapsed += Time.deltaTime)
        {
            float t = elapsed / entranceDuration;
            float slide = Mathf.SmoothStep(0f, 1f, t);
            float bob = Mathf.Abs(Mathf.Sin(slide * Mathf.PI * 4f)) * stepHeight;
            customer.position = Vector3.Lerp(start, customerStop, slide) + Vector3.up * bob;
            yield return null;
        }
        customer.position = customerStop;
        if (orderBubble)
        {
            orderBubble.localScale = Vector3.zero;
            orderBubble.gameObject.SetActive(true);
            const float popDuration = 0.2f;
            for (float elapsed = 0f; elapsed < popDuration; elapsed += Time.deltaTime)
            {
                float t = elapsed / popDuration - 1f;
                float scale = 1f + t * t * (2.7f * t + 1.7f);
                orderBubble.localScale = bubbleScale * scale;
                yield return null;
            }
            orderBubble.localScale = bubbleScale;
        }
        customerEntering = false;
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

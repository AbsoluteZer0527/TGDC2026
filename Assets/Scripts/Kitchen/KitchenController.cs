using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(KitchenDialogue), typeof(PigeonDelivery))]
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
    public TMP_Text potText;

    [Header("Customer")]
    public Transform customer;           // root object, feet on the floor
    [SerializeField] SpriteRenderer customerRenderer;
    [Min(0.01f)] public float entranceDuration = 0.9f;
    [Min(0f)] public float stepHeight = 0.09f;

    [Header("Customer roster")]
    [Tooltip("Unscheduled visits use NPC 0 and pick one of these sprites.")]
    [SerializeField] Sprite[] randomNpcSprites = System.Array.Empty<Sprite>();
    [Tooltip("Give each key character a unique positive ID, a name, and a sprite.")]
    [SerializeField] KeyNpc[] keyNpcs = System.Array.Empty<KeyNpc>();
    [Tooltip("Choose who arrives on a numbered visit. Visit 1 opens the day.")]
    [SerializeField] KeyVisit[] keyVisits = System.Array.Empty<KeyVisit>();

    [Header("Pot")]
    public GameObject soupVisual, readyVisual;
    public Transform throwFrom, potTarget;

    readonly Dictionary<IngredientData, Button> buttons = new();
    string lastThrow = "";
    Camera cam;
    Vector3 customerStop;
    Sprite defaultCustomerSprite;
    bool customerEntering;
    KitchenDialogue dialogue;
    PigeonDelivery pigeonDelivery;
    bool CanServe => !customerEntering && GM.Phase == CustomerPhase.AtCounter && GM.CurrentCustomerLine == null && !GM.DayOver;
    GameManager GM => GameManager.I;

    void Start()
    {
        cam = Camera.main;
        customerStop = customer.position;
        if (customerRenderer) defaultCustomerSprite = customerRenderer.sprite;
        dialogue = GetComponent<KitchenDialogue>();
        dialogue.Initialize(ResumeConversation);
        pigeonDelivery = GetComponent<PigeonDelivery>();
        pigeonDelivery.Initialize(cam, ResumeConversation);
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

        if (GM.CustomersArrived == 0) NewCustomer(); else ResumeConversation();
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
        if (!CanServe) return;
        Deselect();
        var soup = GM.TakeSoup();
        if (soup == null) return;

        bool correct = soup.Count == GM.CurrentOrder.Count && !soup.Except(GM.CurrentOrder).Any();
        if (correct) GM.SellSoup(soupPrice);
        GM.CurrentOrder.Clear();
        dialogue.BeginFarewell();
        ResumeConversation();
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
        serveButton.interactable   = CanServe && GM.Soup == SoupState.Ready;
        discardButton.interactable = !GM.DayOver && (GM.Soup != SoupState.Empty || meter.Running);

        potText.text = meter.Running ? "Press SPACE!\n(top = faster)" : GM.Soup switch
        {
            SoupState.Filling => $"{lastThrow}\nAdd more or Cook",
            SoupState.Ruined  => "Soup ruined. Discard it.",
            _                 => string.Empty
        };

        if (soupVisual)  soupVisual.SetActive(GM.Soup == SoupState.Cooking || GM.Soup == SoupState.Ready);
        if (readyVisual) readyVisual.SetActive(GM.Soup == SoupState.Ready);
    }

    void NewCustomer()
    {
        var visit = keyVisits.FirstOrDefault(v => v?.visitNumber == GM.CustomersArrived + 1);
        var npc = keyNpcs.FirstOrDefault(k => k != null && k.npcId > 0 && k.npcId == visit?.npcId);
        var sprite = npc == null ? Pick(randomNpcSprites.Where(s => s).ToArray()) : npc.sprite;
        GM.CustomerArrives(npc?.npcId ?? 0, sprite ? sprite : defaultCustomerSprite);
        int n = Random.Range(minOrderSize, maxOrderSize + 1);
        GM.CurrentOrder.Clear();
        GM.CurrentOrder.AddRange(ingredients.OrderBy(_ => Random.value).Take(n));
        dialogue.BeginGreeting();

        customer.gameObject.SetActive(true);
        if (customerRenderer) customerRenderer.sprite = GM.CurrentNpcSprite;
        StartCoroutine(MoveCustomer(true));
    }

    static T Pick<T>(T[] choices) where T : class =>
        choices.Length == 0 ? null : choices[Random.Range(0, choices.Length)];

    void ResumeConversation()
    {
        customer.gameObject.SetActive(GM.Phase != CustomerPhase.BetweenCustomers);
        pigeonDelivery.Refresh();
        if (GM.DeliveryStage == PigeonDeliveryStage.Flying || GM.DeliveryStage == PigeonDeliveryStage.WaitingForLetter ||
            (GM.DeliveryStage == PigeonDeliveryStage.Reading && GM.CurrentCustomerLine == null))
        {
            dialogue.Hide();
            return;
        }
        if (GM.CurrentCustomerLine == null && GM.Phase == CustomerPhase.Farewell)
        {
            dialogue.BeginPersonalDialogue();
            StartCoroutine(MoveCustomer(false));
            return;
        }
        if (GM.Phase == CustomerPhase.BetweenCustomers && GM.CurrentCustomerLine == null)
        {
            NewCustomer();
            return;
        }
        if (customerRenderer) customerRenderer.sprite = GM.CurrentNpcSprite;
        dialogue.Show(customer);
    }

    IEnumerator MoveCustomer(bool arriving)
    {
        customerEntering = true;
        dialogue.Hide();

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
            float slide = Mathf.SmoothStep(0f, 1f, arriving ? t : 1f - t);
            float bob = Mathf.Abs(Mathf.Sin(slide * Mathf.PI * 4f)) * stepHeight;
            customer.position = Vector3.Lerp(start, customerStop, slide) + Vector3.up * bob;
            yield return null;
        }
        customer.position = arriving ? customerStop : start;
        if (!arriving) customer.gameObject.SetActive(false);
        customerEntering = false;
        ResumeConversation();
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

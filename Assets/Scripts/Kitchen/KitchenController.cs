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
    [Tooltip("How many DIFFERENT ingredients an order asks for")]
    public int minOrderSize = 2, maxOrderSize = 3;
    [Tooltip("Max copies of one ingredient in an order. Only used when GameManager's Ingredient Refresh is on.")]
    [Min(1)] public int maxCopiesPerIngredient = 2;
    public int soupPrice = 10;

    [Header("Cooking")]
    [Tooltip("Cook seconds each ingredient adds to the pot")]
    public float secondsPerIngredient = 3f;

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
    [Tooltip("The IRS agent who visits at the end of every day. Add them to Key Npcs first.")]
    [SerializeField, NpcReference(false)] int auditorNpcId = 99;

    [Header("Pot")]
    public GameObject soupVisual, readyVisual;
    public Transform throwFrom, potTarget;

    readonly Dictionary<IngredientData, Button> buttons = new();
    string lastThrow = "";
    Camera cam;
    Vector3 customerStop;
    Sprite defaultCustomerSprite;
    bool customerEntering;
    bool auditStarted, auditorTalking;
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
        // Old button bar is optional: leave the prefab empty when using IngredientTable runners.
        if (ingredientButtonPrefab && ingredientButtonParent)
        foreach (var ing in ingredients)
        {
            var b = Instantiate(ingredientButtonPrefab, ingredientButtonParent);
            b.GetComponentInChildren<TMP_Text>().text = ing.displayName;
            b.onClick.AddListener(() => TryAddIngredient(ing));
            buttons[ing] = b;
        }
        //TODO: (optional click when the Cook button is pressed)
        cookButton.onClick.AddListener(() => { GM.StartCooking(); lastThrow = ""; Deselect(); });
        serveButton.onClick.AddListener(Serve);
        discardButton.onClick.AddListener(Discard);

        if (GM.AuditInProgress) customer.gameObject.SetActive(false); // came back from the forest at day end: Update starts the IRS visit
        else if (GM.CustomersArrived == 0)
        {
            //TODO: (bell ring for first npc of the day)
            NewCustomer();
        }
        else ResumeConversation();
    }

    // Fired when an ingredient goes into the pot. IngredientTable animates the runner.
    public event System.Action<IngredientData> IngredientAdded;

    // Adds the ingredient with a flat cook time. Returns true if it went in.
    public bool TryAddIngredient(IngredientData ing)
    {
        Deselect();
        if (GM.DayOver || !GM.AddIngredient(ing, secondsPerIngredient)) return false;

        //TODO: (ingredient catch sound on selection, maybe a different "AHHH!" for each ingredient)
        lastThrow = $"{ing.displayName}: +{secondsPerIngredient:0.#}s";
        if (IngredientAdded != null) IngredientAdded(ing);
        else StartCoroutine(ThrowVisual(ing.color)); // fallback sphere when no runners exist
        return true;
    }

    void Discard()
    {
        //TODO: (optional click when the Discard button is pressed)
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

        //TODO: (optional click when the Serve button serves a bowl)
        // Exact match: same ingredients, same number of each. Extra or missing copies make it wrong.
        bool correct = soup.Count == GM.CurrentOrder.Count &&
                       GM.CurrentOrder.GroupBy(i => i).All(g => soup.Count(s => s == g.Key) == g.Count());
        if (correct) GM.SellSoup(soupPrice);
        GM.CurrentOrder.Clear();
        dialogue.BeginFarewell();
        ResumeConversation();
    }

    void Update()
    {
        if (GM.AuditInProgress && !auditStarted && !CurtainTransition.IsPlaying) BeginAudit();
        bool busy = GM.DayOver;

        foreach (var kv in buttons)
        {
            kv.Value.interactable = !busy && GM.CanAddIngredient(kv.Key);
            kv.Value.image.color = GM.PotContents.Contains(kv.Key) ? kv.Key.color : Color.white;
        }
        cookButton.interactable    = !busy && GM.Soup == SoupState.Filling;
        serveButton.interactable   = CanServe && GM.Soup == SoupState.Ready;
        discardButton.interactable = !GM.DayOver && GM.Soup != SoupState.Empty;

        potText.text = GM.Soup switch
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
        foreach (var ing in ingredients.OrderBy(_ => Random.value).Take(n))
        {
            // Duplicates only make sense when thrown veggies come back (ingredient refresh).
            int copies = GM.ingredientRefresh ? Random.Range(1, maxCopiesPerIngredient + 1) : 1;
            for (int c = 0; c < copies; c++) GM.CurrentOrder.Add(ing);
        }
        dialogue.BeginGreeting();

        customer.gameObject.SetActive(true);
        if (customerRenderer) customerRenderer.sprite = GM.CurrentNpcSprite;
        //TODO: (NPC footsteps while walking up to the counter)
        StartCoroutine(MoveCustomer(true));
    }

    static T Pick<T>(T[] choices) where T : class =>
        choices.Length == 0 ? null : choices[Random.Range(0, choices.Length)];

    // End of day: current customer leaves, the IRS walks in and says the pass/fail lines.
    void BeginAudit()
    {
        auditStarted = true;
        StopAllCoroutines();          // cancel any customer walk in progress
        customerEntering = false;
        StartCoroutine(AuditSteps());
    }

    IEnumerator AuditSteps()
    {
        dialogue.Hide();
        if (customer.gameObject.activeSelf) yield return MoveCustomer(false, resume: false);

        var irs = keyNpcs.FirstOrDefault(k => k != null && k.npcId == auditorNpcId);
        GM.AuditorArrives(auditorNpcId, irs?.sprite ? irs.sprite : defaultCustomerSprite);
        dialogue.BeginAudit(GM.AuditPassed);

        customer.gameObject.SetActive(true);
        if (customerRenderer) customerRenderer.sprite = GM.CurrentNpcSprite;
        //TODO: (IRS footsteps / ominous sting)
        yield return MoveCustomer(true, resume: false);
        auditorTalking = true;
        ResumeConversation();
    }

    void ResumeConversation()
    {
        if (GM.AuditInProgress)
        {
            if (!auditorTalking) return;                       // still walking in
            if (GM.CurrentCustomerLine == null) { dialogue.Hide(); GM.FinishAudit(); return; }
            if (customerRenderer) customerRenderer.sprite = GM.CurrentNpcSprite;
            dialogue.Show(customer);
            return;
        }
        if (GM.DayOver) return;                                 // night screen / loss screen

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

    IEnumerator MoveCustomer(bool arriving, bool resume = true)
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
        if (resume) ResumeConversation();
    }

    IEnumerator ThrowVisual(Color color)
    {
        if (!throwFrom || !potTarget) yield break;
        //TODO: (whoosh as the ingredient is thrown)
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
        //TODO: (water pop as the ingredient lands in the pot)
        Destroy(ball);
    }

    static void Deselect()
    {
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
    }
}

using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SoupState { Empty, Filling, Cooking, Ready, Ruined }
public enum CustomerPhase { AtCounter, Farewell, BetweenCustomers }
public enum PigeonDeliveryStage { NotStarted, Flying, WaitingForLetter, Reading, Complete }
// None = daytime. Audit = IRS visit in the kitchen. Summary = black night screen. Lost = game over screen.
public enum NightPhase { None, Audit, Summary, Lost }
public enum LossReason { None, Jail, Evicted }

public class GameManager : MonoBehaviour
{
    public static GameManager I { get; private set; }
    bool restarting;

    [Header("Day")]
    public float dayLength = 300f;
    [Tooltip("Rent charged at the end of each day = this × the day number.")]
    public int rentPerDay = 200;
    [Min(1), Tooltip("Playtest: start the game on this day (difficulty and rent follow it). Keep at 1 for release.")]
    public int startDay = 1;
    public float TimeLeft { get; private set; }
    public NightPhase Night { get; private set; }
    public LossReason Loss { get; private set; }
    public bool DayOver => Night != NightPhase.None;          // freezes normal gameplay
    public LossReason AuditOutcome { get; private set; }
    public bool AuditInProgress => Night == NightPhase.Audit;
    public bool AuditorPresent { get; private set; }
    public int Day { get; private set; } = 1;
    public long RentDue => (long)rentPerDay * Day;

    [Header("Tutorial (the first customers of the game)")]
    [Tooltip("While customer 1, 2, ... is the latest arrival, the day clock stops at this fraction of the day. " +
             "Customers past the end of the list don't stop it.")]
    public float[] tutorialClockCaps = { 0.25f, 0.5f };
    [Tooltip("Fixed cook seconds for customer 1, 2, 3... (customer 3 is long so the player can explore the forest). " +
             "Customers past the end of the list use the normal cook time.")]
    public float[] tutorialCookSeconds = { 10f, 10f, 60f };

    [Header("Soup")]
    [Tooltip("Cook time = ingredients in the pot × this")]
    public float cookSecondsPerIngredient = 7.5f;
    [Tooltip("On: thrown ingredients fall back onto the board and can be added again (duplicates allowed).\n" +
             "Off: each ingredient goes in once and stays in the soup until Discard.")]
    public bool ingredientRefresh = true;
    [Min(0f), Tooltip("Seconds to serve a finished soup before it explodes (day 1). Shrinks to 50% by day 10.")]
    public float servingWindowSeconds = 30f;
    [Min(0f), Tooltip("Seconds the exploded timer stays on the HUD.")]
    public float explosionDisplaySeconds = 4f;
    public SoupState Soup { get; private set; } = SoupState.Empty;
    public List<IngredientData> PotContents { get; } = new();
    public float TotalCookTime => CustomersArrived >= 1 && CustomersArrived <= tutorialCookSeconds.Length && Phase == CustomerPhase.AtCounter
        ? tutorialCookSeconds[CustomersArrived - 1]
        : PotContents.Count * cookSecondsPerIngredient;
    public float SoupTimeLeft { get; private set; }
    public float CookTimeLeft => Soup == SoupState.Cooking ? SoupTimeLeft : 0f;
    float explosionEndsAt;
    public float ExplosionTimeLeft => Mathf.Max(0f, explosionEndsAt - Time.time);

    [Header("Customer")]
    public List<IngredientData> CurrentOrder { get; } = new();
    public int CurrentNpcId { get; private set; }
    public Sprite CurrentNpcSprite { get; private set; }
    public int CustomersArrived { get; private set; }
    public CustomerPhase Phase { get; private set; }
    public bool HasAdvancedDialogue { get; private set; }
    public PigeonDeliveryStage DeliveryStage { get; private set; }
    public float PigeonFlightProgress { get; private set; }
    public bool LetterBackRevealed { get; private set; }
    int lastDialogueAdvanceFrame;
    public bool ForestUnlocked { get; private set; }
    public bool ForestDialoguePending { get; private set; }
    readonly Queue<DialogueLine> customerLines = new();
    public DialogueLine CurrentCustomerLine => customerLines.Count > 0 ? customerLines.Peek() : null;

    public void CustomerArrives(int npcId, Sprite sprite)
    {
        CustomersArrived++;
        CurrentNpcId = npcId;
        CurrentNpcSprite = sprite;
    }

    public void BeginCustomerDialogue(CustomerPhase phase, IEnumerable<DialogueLine> lines)
    {
        Phase = phase;
        customerLines.Clear();
        foreach (var line in lines)
            if (!string.IsNullOrWhiteSpace(line?.text)) customerLines.Enqueue(line);
    }

    public void AdvanceCustomerDialogue()
    {
        if (customerLines.Count == 0) return;
        var line = customerLines.Dequeue();
        if (line.afterLine == AfterDialogueLine.PigeonDelivery && DeliveryStage == PigeonDeliveryStage.NotStarted)
            DeliveryStage = PigeonDeliveryStage.Flying;
        if (line.afterLine == AfterDialogueLine.RevealLetterBack && DeliveryStage == PigeonDeliveryStage.Reading)
            LetterBackRevealed = true;
        lastDialogueAdvanceFrame = Time.frameCount;
        if (line.afterLine == AfterDialogueLine.UnlockForest) ForestUnlocked = true;
        HasAdvancedDialogue = true;
    }

    public void AdvancePigeonFlight(float progress)
    {
        if (DeliveryStage != PigeonDeliveryStage.Flying) return;
        PigeonFlightProgress = Mathf.Clamp01(PigeonFlightProgress + progress);
        if (PigeonFlightProgress >= 1f) DeliveryStage = PigeonDeliveryStage.WaitingForLetter;
    }

    public bool OpenDeliveredLetter()
    {
        if (DayOver || DeliveryStage != PigeonDeliveryStage.WaitingForLetter) return false;
        DeliveryStage = PigeonDeliveryStage.Reading;
        return true;
    }

    public bool FinishReadingLetter()
    {
        if (DayOver || DeliveryStage != PigeonDeliveryStage.Reading ||
            (CurrentCustomerLine != null && (!LetterBackRevealed || customerLines.Count != 1)) ||
            Time.frameCount == lastDialogueAdvanceFrame) return false;
        if (CurrentCustomerLine != null) AdvanceCustomerDialogue();
        DeliveryStage = PigeonDeliveryStage.Complete;
        return true;
    }

    [Header("Money")]
    [Tooltip("$ of dirty money you can launder per $1 of soup sold")]
    public float launderRatio = 3f;
    // Today's numbers (reset every morning). HUD shows them as Stew / Hunt / Max.
    public long CleanMoney { get; private set; }       // Stew: kitchen profit today
    public long DirtyMoney { get; private set; }       // Hunt: illegal money today
    public long LaunderCapacity { get; private set; }  // stew × launderRatio
    public long MaxToday => CleanMoney + LaunderCapacity;          // most you can make today and stay clean
    public bool AuditPassed => CleanMoney + DirtyMoney <= MaxToday; // i.e. Hunt <= stew × ratio
    // Piggy bank: running total across days. Rent comes out of this; negative = in the red.
    public long Savings { get; private set; }
    public long Score => Savings;
    public NightReport LastNight { get; private set; }
    const string HighScoreKey = "HighScore64";
    public long HighScore => long.TryParse(PlayerPrefs.GetString(HighScoreKey), NumberStyles.Integer,
        CultureInfo.InvariantCulture, out long saved) ? saved : PlayerPrefs.GetInt("HighScore", 0);

    [Header("Forest")]
    [Min(1), Tooltip("Hearts the player starts with. Upgrades add to this with AddMaxHearts (kept across days).")]
    public int startingHearts = 3;
    public int MaxHearts { get; private set; }
    public void AddMaxHearts(int amount) => MaxHearts = Mathf.Max(1, MaxHearts + amount);
    [Tooltip("0 = random each day. Set a number to test the same layout every time.")]
    public int debugSeed = 0;
    public int ForestSeed { get; private set; }
    public HashSet<int> KilledCreatures { get; } = new();
    public bool[] VisitedForestTiles { get; set; }   // centers of the minimap reveal circles

    void Awake()
    {
        if (I != null)
        {
            if (!I.restarting) { Destroy(gameObject); return; }
            Destroy(I.gameObject);
        }
        I = this;
        DontDestroyOnLoad(gameObject);
        TimeLeft = dayLength;
        Day = startDay;
        MaxHearts = startingHearts;
        ForestSeed = debugSeed != 0 ? debugSeed : Random.Range(1, int.MaxValue);
    }

    // Tutorial: the clock can't run past the current customer's cap (customer 1 = 25%, customer 2 = 50%).
    float MinTimeLeft
    {
        get
        {
            int i = Mathf.Max(CustomersArrived, 1) - 1;
            return i < tutorialClockCaps.Length ? dayLength * (1f - tutorialClockCaps[i]) : 0f;
        }
    }

    void Update()
    {
        if (DayOver)
        {
            // The audit happens in the kitchen: if the day ended in the forest (or mid-door), go back.
            if (AuditInProgress && SceneManager.GetActiveScene().name != "Kitchen" && !CurtainTransition.IsPlaying)
                CurtainTransition.TransitionTo("Kitchen");
            return;
        }
        float elapsed = Mathf.Min(Time.deltaTime, TimeLeft);
        AdvanceSoupTimer(elapsed);                                           // soup keeps cooking while the clock is held
        TimeLeft -= Mathf.Min(elapsed, Mathf.Max(0f, TimeLeft - MinTimeLeft));
        if (TimeLeft <= 0f) EndDay();
    }

    // Jump the clock forward (e.g. passing out in the forest). The soup keeps cooking through the skipped time.
    public void SkipDayTime(float fraction)
    {
        if (DayOver) return;
        float skip = Mathf.Min(dayLength * fraction, Mathf.Max(0f, TimeLeft - MinTimeLeft));
        AdvanceSoupTimer(skip);
        TimeLeft -= skip;
        if (TimeLeft <= 0f) EndDay();
    }

    // ---------- Pot ----------
    void AdvanceSoupTimer(float elapsed)
    {
        if (Soup != SoupState.Cooking && Soup != SoupState.Ready) return;

        SoupTimeLeft -= elapsed;
        // Carry the remainder of the frame into the next stage.
        if (Soup == SoupState.Cooking && SoupTimeLeft <= 0f)
        {
            Soup = SoupState.Ready;
            //TODO: (loop the tomato clock alarm while the soup is ready and the day is active)
            SoupTimeLeft += servingWindowSeconds * Difficulty.ServingWindow;
        }
        if (Soup == SoupState.Ready && SoupTimeLeft <= 0f)
        {
            Soup = SoupState.Ruined;
            //TODO: (tomato splat when the 30-second serving window expires)
            explosionEndsAt = Time.time + explosionDisplaySeconds + SoupTimeLeft;
        }
        SoupTimeLeft = Mathf.Max(0f, SoupTimeLeft);
    }

    public bool CanAddIngredient(IngredientData ing) =>
        (Soup == SoupState.Empty || Soup == SoupState.Filling) && (ingredientRefresh || !PotContents.Contains(ing));

    public bool AddIngredient(IngredientData ing)
    {
        if (!CanAddIngredient(ing)) return false;
        PotContents.Add(ing);
        Soup = SoupState.Filling;
        return true;
    }

    public bool StartCooking()
    {
        if (Soup != SoupState.Filling) return false;
        explosionEndsAt = 0f;
        SoupTimeLeft = TotalCookTime;
        //TODO: (loop pot bubbling while the soup is cooking and the day is active)
        Soup = SoupState.Cooking;
        if (CustomersArrived >= 3 && Phase == CustomerPhase.AtCounter && !ForestUnlocked)
            ForestDialoguePending = true;
        return true;
    }

    public List<IngredientData> TakeSoup()
    {
        if (Soup != SoupState.Ready) return null;
        var soup = new List<IngredientData>(PotContents);
        DiscardSoup();
        return soup;
    }

    public void DiscardSoup()
    {
        PotContents.Clear();
        SoupTimeLeft = 0f;
        Soup = SoupState.Empty;
    }

    // ---------- Money ----------
    public void SellSoup(int price)
    {
        //TODO: (coins clinking when the customer pays for soup)
        CleanMoney += price;
        LaunderCapacity += (long)System.Math.Round(price * (double)launderRatio);
    }

    //TODO: (coins clinking when a hunt pays out)
    public void AddDirtyMoney(int amount) => DirtyMoney += amount;

    public string Message { get; private set; } = "";
    public float MessageUntil { get; private set; }
    public Sprite MessageIcon { get; private set; }     // optional picture shown left of the message (e.g. a mob's item)
    public void ShowMessage(string text, float duration = 2f, Sprite icon = null)
    {
        Message = text;
        MessageIcon = icon;
        MessageUntil = Time.time + duration;
    }

    // ---------- Day / night ----------
    // Settlement of a passed audit, for the night screen.
    public class NightReport
    {
        public int day;
        public long stew, hunt;       // today's income (all of it was launderable)
        public long rent;
        public long savingsBefore, savingsAfter;
        public long Income => stew + hunt;
        public long Change => savingsAfter - savingsBefore;   // Income - rent
    }

    // 1) Timer hit 0: gameplay freezes and the IRS visits the kitchen (KitchenController runs the visit).
    public void EndDay()
    {
        if (DayOver) return;
        AuditOutcome = !AuditPassed ? LossReason.Jail :
            Savings < 0 && Savings + CleanMoney + DirtyMoney - RentDue < 0 ? LossReason.Evicted : LossReason.None;
        Night = NightPhase.Audit;
        TimeLeft = 0f;
        DiscardSoup();
        explosionEndsAt = 0f;
        CurrentOrder.Clear();
        customerLines.Clear();
        //TODO: (night sound / music sting)
        // Return from the forest for the audit. In the kitchen the inspector enters directly.
        if (SceneManager.GetActiveScene().name != "Kitchen" && !CurtainTransition.IsPlaying)
            CurtainTransition.TransitionTo("Kitchen");
    }

    // The IRS visitor uses the normal customer slot, but doesn't count as a story visit.
    public void AuditorArrives(int npcId, Sprite sprite)
    {
        AuditorPresent = true;
        CurrentNpcId = npcId;
        CurrentNpcSprite = sprite;
    }

    // 2) Called by KitchenController after the IRS finishes talking.
    public void FinishAudit()
    {
        if (!AuditInProgress) return;
        AuditorPresent = false;
        if (AuditOutcome == LossReason.Jail) { Lose(LossReason.Jail); return; }

        var r = new NightReport { day = Day, stew = CleanMoney, hunt = DirtyMoney, rent = RentDue, savingsBefore = Savings };
        Savings += r.Income - r.rent;
        r.savingsAfter = Savings;
        LastNight = r;
        if (Score > HighScore) PlayerPrefs.SetString(HighScoreKey, Score.ToString(CultureInfo.InvariantCulture));
        if (AuditOutcome == LossReason.Evicted) { Lose(LossReason.Evicted); return; }
        Night = NightPhase.Summary;   // NightScreen plays the black-screen animation
    }

    // 3) Called by NightScreen when its animation is done.
    public void FinishNightSummary()
    {
        if (Night != NightPhase.Summary) return;
        StartNextDay();
    }

    void Lose(LossReason reason)
    {
        Loss = reason;
        Night = NightPhase.Lost;      // HUD shows the loss screen with Restart
        //TODO: (loss sting: jail door / eviction)
    }

    // 4) New morning: reset everything per-day. Savings carry over.
    void StartNextDay()
    {
        Day++;
        CleanMoney = 0;
        DirtyMoney = 0;
        LaunderCapacity = 0;

        Night = NightPhase.None;
        TimeLeft = dayLength;
        DiscardSoup();
        explosionEndsAt = 0f;

        // Fresh customer next morning (story progress like CustomersArrived and the pigeon is kept).
        CurrentOrder.Clear();
        customerLines.Clear();
        Phase = CustomerPhase.BetweenCustomers;

        // New forest.
        ForestSeed = debugSeed != 0 ? debugSeed : Random.Range(1, int.MaxValue);
        KilledCreatures.Clear();
        VisitedForestTiles = null;

        CurtainTransition.TransitionTo("Kitchen", startClosed: true);  // curtain opens on the new morning
    }

    public void Restart()
    {
        if (restarting) return;
        // LoadScene completes next frame. Keep serving the outgoing kitchen until
        // the replacement Systems object takes ownership in Awake.
        restarting = true;
        SceneManager.LoadScene("Kitchen");
    }
}

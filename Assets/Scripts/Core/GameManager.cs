using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SoupState { Empty, Filling, Cooking, Ready, Ruined }
public enum CustomerPhase { AtCounter, Farewell, BetweenCustomers }
public enum PigeonDeliveryStage { NotStarted, Flying, WaitingForLetter, Reading, Complete }

public class GameManager : MonoBehaviour
{
    public static GameManager I { get; private set; }

    [Header("Day")]
    public float dayLength = 300f;
    [Tooltip("Rent charged at the end of each day = this × the day number.")]
    public int rentPerDay = 200;
    [Min(0f), Tooltip("Seconds the night screen (with the bill) shows before the next day starts.")]
    public float nightDuration = 4f;
    public float TimeLeft { get; private set; }
    public bool DayOver { get; private set; }   // true during the night between days
    public int Day { get; private set; } = 1;
    public long RentDue => (long)rentPerDay * Day;
    float nightTimeLeft;

    [Header("Soup")]
    [Tooltip("Cook time before the per-ingredient seconds are added")]
    public float baseCookTime = 3f;
    [Tooltip("On: thrown ingredients fall back onto the board and can be added again (duplicates allowed).\n" +
             "Off: each ingredient goes in once and stays in the soup until Discard.")]
    public bool ingredientRefresh = true;
    [Min(0f), Tooltip("Seconds to serve a finished soup before it explodes.")]
    public float servingWindowSeconds = 30f;
    [Min(0f), Tooltip("Seconds the exploded timer stays on the HUD.")]
    public float explosionDisplaySeconds = 4f;
    public SoupState Soup { get; private set; } = SoupState.Empty;
    public List<IngredientData> PotContents { get; } = new();
    public float PendingCookTime { get; private set; }   // sum of per-ingredient seconds
    public float TotalCookTime => baseCookTime + PendingCookTime;
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
    int lastDialogueAdvanceFrame;
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
        lastDialogueAdvanceFrame = Time.frameCount;
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
        if (DayOver || DeliveryStage != PigeonDeliveryStage.Reading || CurrentCustomerLine != null ||
            Time.frameCount == lastDialogueAdvanceFrame) return false;
        DeliveryStage = PigeonDeliveryStage.Complete;
        return true;
    }

    [Header("Money")]
    [Tooltip("$ of dirty money you can launder per $1 of soup sold")]
    public float launderRatio = 3f;
    // Today's numbers. They are settled into Savings at night, then reset.
    public long CleanMoney { get; private set; }       // kitchen profit today
    public long DirtyMoney { get; private set; }       // illegal money today
    public long LaunderCapacity { get; private set; }  // how much dirty money today's sales can cover
    public long Laundered => System.Math.Min(DirtyMoney, LaunderCapacity);
    public bool OverLaunderLimit => DirtyMoney > LaunderCapacity;
    // Running total across days. Rent comes out of this; negative = in debt.
    public long Savings { get; private set; }
    public long Score => Savings;
    public NightReport LastNight { get; private set; }
    const string HighScoreKey = "HighScore64";
    public long HighScore => long.TryParse(PlayerPrefs.GetString(HighScoreKey), NumberStyles.Integer,
        CultureInfo.InvariantCulture, out long saved) ? saved : PlayerPrefs.GetInt("HighScore", 0);

    [Header("Forest")]
    [Tooltip("0 = random each day. Set a number to test the same layout every time.")]
    public int debugSeed = 0;
    public int ForestSeed { get; private set; }
    public HashSet<int> KilledCreatures { get; } = new();
    public bool[] Explored { get; set; }   // minimap fog, one entry per tile, resets each day

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        TimeLeft = dayLength;
        ForestSeed = debugSeed != 0 ? debugSeed : Random.Range(1, int.MaxValue);
    }

    void Update()
    {
        if (DayOver)
        {
            nightTimeLeft -= Time.deltaTime;
            if (nightTimeLeft <= 0f) StartNextDay();
            return;
        }
        float elapsed = Mathf.Min(Time.deltaTime, TimeLeft);
        TimeLeft -= elapsed;
        AdvanceSoupTimer(elapsed);
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
            SoupTimeLeft += servingWindowSeconds;
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

    public bool AddIngredient(IngredientData ing, float cookSeconds)
    {
        if (!CanAddIngredient(ing)) return false;
        PotContents.Add(ing);
        PendingCookTime += cookSeconds;
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
        PendingCookTime = 0f;
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
    public void ShowMessage(string text, float duration = 2f)
    {
        Message = text;
        MessageUntil = Time.time + duration;
    }

    // ---------- Day ----------
    // What happened at the end of a day, for the night screen.
    public class NightReport
    {
        public int day;
        public long kitchenProfit, illegalMoney, launderCapacity;
        public bool exposed;     // illegal money > launder capacity
        public long banked;      // added to Savings
        public long rent;
        public long savingsAfter;
    }

    // Timer hit 0: night falls. Gameplay freezes (everything checks DayOver) and the day is settled.
    void EndDay()
    {
        DayOver = true;
        TimeLeft = 0f;
        nightTimeLeft = nightDuration;

        // ---------- Night events ----------
        var report = new NightReport
        {
            day = Day, kitchenProfit = CleanMoney, illegalMoney = DirtyMoney,
            launderCapacity = LaunderCapacity, exposed = OverLaunderLimit, rent = RentDue,
        };

        if (report.exposed)
        {
            //TODO: (illegal money > what the kitchen could launder: something happens, e.g. raid, fine, game over)
            // Placeholder until that's designed: only the launderable part is kept, the excess is lost.
            report.banked = CleanMoney + Laundered;
        }
        else
        {
            // All illegal money was launderable: kitchen profit + illegal money go into savings.
            report.banked = CleanMoney + DirtyMoney;
        }

        Savings += report.banked;
        Savings -= report.rent;   // rent / debt / tax
        report.savingsAfter = Savings;
        LastNight = report;
        //TODO: (game over / consequence when Savings < 0, i.e. in debt?)
        //TODO: (night sound / music sting)

        if (Score > HighScore) PlayerPrefs.SetString(HighScoreKey, Score.ToString(CultureInfo.InvariantCulture));
    }

    // Advance the day and reset everything that is per-day. Savings carry over.
    void StartNextDay()
    {
        long bill = LastNight != null ? LastNight.rent : 0;
        Day++;
        CleanMoney = 0;
        DirtyMoney = 0;
        LaunderCapacity = 0;

        DayOver = false;
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
        Explored = null;

        ShowMessage($"<color=#FF4D4D>-${bill} rent, debt & taxes</color>\nSavings: ${Savings}\nDay {Day}", 4f);
        SceneManager.LoadScene("Kitchen");
    }

    public void Restart()
    {
        I = null;
        Destroy(gameObject);
        SceneManager.LoadScene("Kitchen");
    }
}

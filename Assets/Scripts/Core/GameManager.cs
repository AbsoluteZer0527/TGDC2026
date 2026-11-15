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
    public float TimeLeft { get; private set; }
    public bool DayOver { get; private set; }

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
    public long CleanMoney { get; private set; }
    public long DirtyMoney { get; private set; }
    public long LaunderCapacity { get; private set; }
    public long Laundered => System.Math.Min(DirtyMoney, LaunderCapacity);
    public long Score => CleanMoney + Laundered;
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
        if (DayOver) return;
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
    void EndDay()
    {
        DayOver = true;
        TimeLeft = 0f;
        if (Score > HighScore) PlayerPrefs.SetString(HighScoreKey, Score.ToString(CultureInfo.InvariantCulture));
    }

    public void Restart()
    {
        I = null;
        Destroy(gameObject);
        SceneManager.LoadScene("Kitchen");
    }
}

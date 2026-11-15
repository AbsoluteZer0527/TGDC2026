using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SoupState { Empty, Filling, Cooking, Ready }
public enum CustomerPhase { AtCounter, Farewell, BetweenCustomers }

public class GameManager : MonoBehaviour
{
    public static GameManager I { get; private set; }

    [Header("Day")]
    public float dayLength = 300f;
    public float TimeLeft { get; private set; }
    public bool DayOver { get; private set; }

    [Header("Soup")]
    [Tooltip("Cook time before the timing-meter seconds are added")]
    public float baseCookTime = 3f;
    public SoupState Soup { get; private set; } = SoupState.Empty;
    public List<IngredientData> PotContents { get; } = new();
    public float PendingCookTime { get; private set; }   // sum from the timing meter
    public float TotalCookTime => baseCookTime + PendingCookTime;
    public float CookTimeLeft { get; private set; }

    [Header("Customer")]
    public List<IngredientData> CurrentOrder { get; } = new();
    public int CurrentNpcId { get; private set; }
    public Sprite CurrentNpcSprite { get; private set; }
    public int CustomersArrived { get; private set; }
    public CustomerPhase Phase { get; private set; }
    public bool HasAdvancedDialogue { get; private set; }
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
        customerLines.Dequeue();
        HasAdvancedDialogue = true;
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
        TimeLeft -= Time.deltaTime;
        if (Soup == SoupState.Cooking)
        {
            CookTimeLeft -= Time.deltaTime;
            if (CookTimeLeft <= 0f) { CookTimeLeft = 0f; Soup = SoupState.Ready; }
        }
        if (TimeLeft <= 0f) EndDay();
    }

    // ---------- Pot ----------
    public bool CanAddIngredient(IngredientData ing) =>
        (Soup == SoupState.Empty || Soup == SoupState.Filling) && !PotContents.Contains(ing);

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
        CookTimeLeft = TotalCookTime;
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
        CookTimeLeft = 0f;
        Soup = SoupState.Empty;
    }

    // ---------- Money ----------
    public void SellSoup(int price)
    {
        CleanMoney += price;
        LaunderCapacity += (long)System.Math.Round(price * (double)launderRatio);
    }

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

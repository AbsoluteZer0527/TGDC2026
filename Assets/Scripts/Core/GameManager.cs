using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SoupState { Empty, Filling, Cooking, Ready }

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
    public Color CustomerColor { get; set; } = Color.white;
    public int CustomersArrived { get; private set; }
    public bool HasAdvancedDialogue { get; private set; }
    readonly Queue<DialogueLine> customerLines = new();
    public DialogueLine CurrentCustomerLine => customerLines.Count > 0 ? customerLines.Peek() : null;

    public void BeginCustomerDialogue(IEnumerable<DialogueLine> lines)
    {
        CustomersArrived++;
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
    public int CleanMoney { get; private set; }
    public int DirtyMoney { get; private set; }
    public int LaunderCapacity { get; private set; }
    public int Laundered => Mathf.Min(DirtyMoney, LaunderCapacity);
    public int Score => CleanMoney + Laundered;
    public int HighScore => PlayerPrefs.GetInt("HighScore", 0);

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
        LaunderCapacity += Mathf.RoundToInt(price * launderRatio);
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
        if (Score > HighScore) PlayerPrefs.SetInt("HighScore", Score);
    }

    public void Restart()
    {
        I = null;
        Destroy(gameObject);
        SceneManager.LoadScene("Kitchen");
    }
}

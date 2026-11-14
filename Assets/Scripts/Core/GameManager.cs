using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SoupState { Empty, Cooking, Ready }

public class GameManager : MonoBehaviour
{
    public static GameManager I { get; private set; }

    [Header("Day")]
    public float dayLength = 300f;
    public float TimeLeft { get; private set; }
    public bool DayOver { get; private set; }

    [Header("Soup")]
    public float cookTime = 20f;
    public SoupState Soup { get; private set; } = SoupState.Empty;
    public float CookTimeLeft { get; private set; }
    public List<IngredientData> PotContents { get; } = new();

    // Current customer, persist over to shooter
    public List<IngredientData> CurrentOrder { get; } = new();
    public Color CustomerColor { get; set; } = Color.white;
    
    [Header("Forest")]
    [Tooltip("0 = random each day. Set a number to test the same layout every time.")]
    public int debugSeed = 0;
    public int ForestSeed { get; private set; }
    public HashSet<int> KilledCreatures { get; } = new();

    [Header("Money")]
    [Tooltip("$ of dirty money you can launder per $1 of soup sold")]
    public float launderRatio = 3f;
    public int CleanMoney { get; private set; }
    public int DirtyMoney { get; private set; }
    public int LaunderCapacity { get; private set; }
    public int Laundered => Mathf.Min(DirtyMoney, LaunderCapacity);
    public int Score => CleanMoney + Laundered;
    public int HighScore => PlayerPrefs.GetInt("HighScore", 0);

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; } // duplicate from another scene
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
            if (CookTimeLeft <= 0f) Soup = SoupState.Ready; // stays Ready indefinitely
        }
        if (TimeLeft <= 0f) EndDay();
    }

    public bool StartCooking(List<IngredientData> ingredients)
    {
        if (Soup != SoupState.Empty || ingredients.Count == 0) return false;
        PotContents.Clear();
        PotContents.AddRange(ingredients);
        CookTimeLeft = cookTime;
        Soup = SoupState.Cooking;
        return true;
    }

    public List<IngredientData> TakeSoup()
    {
        if (Soup != SoupState.Ready) return null;
        var soup = new List<IngredientData>(PotContents);
        PotContents.Clear();
        Soup = SoupState.Empty;
        return soup;
    }

    public void SellSoup(int price)
    {
        CleanMoney += price;
        LaunderCapacity += Mathf.RoundToInt(price * launderRatio);
    }

    public void AddDirtyMoney(int amount) => DirtyMoney += amount;

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
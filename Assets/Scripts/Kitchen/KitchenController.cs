using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class KitchenController : MonoBehaviour
{
    [Header("Data")]
    public IngredientData[] ingredients;
    public int minOrderSize = 2, maxOrderSize = 3;
    public int soupPrice = 10;

    [Header("UI")]
    public Button ingredientButtonPrefab;
    public Transform ingredientButtonParent;
    public Button cookButton, serveButton;
    public TMP_Text orderText, potText;

    [Header("World")]
    public Renderer customerRenderer;
    public GameObject soupVisual;   // shown when pot has soup
    public GameObject readyVisual;  // shown when ready

    readonly List<IngredientData> selected = new();
    readonly Dictionary<IngredientData, Image> buttonImages = new();
    GameManager GM => GameManager.I;

    void Start()
    {
        foreach (var ing in ingredients)
        {
            var b = Instantiate(ingredientButtonPrefab, ingredientButtonParent);
            b.GetComponentInChildren<TMP_Text>().text = ing.displayName;
            buttonImages[ing] = b.image;
            b.onClick.AddListener(() => { if (!selected.Remove(ing)) selected.Add(ing); });
        }
        cookButton.onClick.AddListener(() => { if (GM.StartCooking(selected)) selected.Clear(); });
        serveButton.onClick.AddListener(Serve);

        if (GM.CurrentOrder.Count == 0) NewCustomer(); else ShowCustomer();
    }

    void Update()
    {
        foreach (var kv in buttonImages)
            kv.Value.color = selected.Contains(kv.Key) ? kv.Key.color : Color.white;

        bool canCook = GM.Soup == SoupState.Empty && !GM.DayOver;
        cookButton.interactable = canCook && selected.Count > 0;
        foreach (var img in buttonImages.Values) img.GetComponent<Button>().interactable = canCook;
        serveButton.interactable = GM.Soup == SoupState.Ready && !GM.DayOver;

        potText.text = GM.Soup switch
        {
            SoupState.Cooking => $"Cooking... {Mathf.CeilToInt(GM.CookTimeLeft)}s",
            SoupState.Ready   => "Soup's ready! Serve it.",
            _                 => "Pick ingredients, then Cook"
        };
        if (soupVisual)  soupVisual.SetActive(GM.Soup != SoupState.Empty);
        if (readyVisual) readyVisual.SetActive(GM.Soup == SoupState.Ready);
    }

    void Serve()
    {
        var soup = GM.TakeSoup();
        if (soup == null) return;

        bool correct = soup.Count == GM.CurrentOrder.Count && !soup.Except(GM.CurrentOrder).Any();
        if (correct) GM.SellSoup(soupPrice);
        NewCustomer(); // infinite line, if wrong soup, its unpaid
    }

    void NewCustomer()
    {
        int n = Random.Range(minOrderSize, maxOrderSize + 1);
        GM.CurrentOrder.Clear();
        GM.CurrentOrder.AddRange(ingredients.OrderBy(_ => Random.value).Take(n));
        GM.CustomerColor = Random.ColorHSV(0f, 1f, 0.4f, 0.8f, 0.8f, 1f);
        ShowCustomer();
    }

    void ShowCustomer()
    {
        orderText.text = "One soup with:\n" + string.Join(", ", GM.CurrentOrder.Select(i => i.displayName));
        if (customerRenderer) customerRenderer.material.color = GM.CustomerColor;
    }
}
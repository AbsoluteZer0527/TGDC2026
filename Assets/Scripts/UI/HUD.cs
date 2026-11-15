using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    [Header("Day clock")]
    [SerializeField] Image dayClock;
    [SerializeField] Sprite forestDayClock;
    [SerializeField] RectTransform dayPointer;
    [Tooltip("Pointer rotation at the sun and moon, in degrees.")]
    [SerializeField] Vector2 pointerAngles = new(-58f, 132f);

    [Header("Cooking timer")]
    [SerializeField] Image cookingTimer;
    [SerializeField] Sprite cookingSprite, explodedSprite;
    [SerializeField] TMP_Text soupText;

    [Header("Status")]
    public TMP_Text messageText;
    [FormerlySerializedAs("cleanText")] public TMP_Text stewText;
    [FormerlySerializedAs("dirtyText"), FormerlySerializedAs("totalText")] public TMP_Text huntText;
    [SerializeField] Image openMoney;
    [SerializeField] GameObject foldedMoney;
    [FormerlySerializedAs("launderText")] public TMP_Text maxText;
    public GameObject endPanel;
    public TMP_Text endText;
    public Button restartButton;

    void OnEnable()
    {
        SceneManager.activeSceneChanged += UpdateDayClock;
        UpdateDayClock(default, SceneManager.GetActiveScene());
    }

    void OnDisable() => SceneManager.activeSceneChanged -= UpdateDayClock;

    void UpdateDayClock(Scene previous, Scene current)
    {
        dayClock.overrideSprite = current.name == "Forest" ? forestDayClock : null;
    }

    void Start() => restartButton.onClick.AddListener(() => GameManager.I.Restart());

    void LateUpdate()
    {
        var gm = GameManager.I;
        if (gm == null) return;

        float progress = 1f - Mathf.InverseLerp(0f, gm.dayLength, gm.TimeLeft);
        float angle = Mathf.Lerp(pointerAngles.x, pointerAngles.y, progress);
        dayPointer.localRotation = Quaternion.Euler(0f, 0f, angle);
        UpdateCookingTimer(gm);
        stewText.text = FormatMoney(gm.CleanMoney);
        bool showHunt = gm.DirtyMoney > 0;
        openMoney.enabled = showHunt;
        foldedMoney.SetActive(!showHunt);
        huntText.gameObject.SetActive(showHunt);
        maxText.gameObject.SetActive(showHunt);
        if (showHunt)
        {
            huntText.text = FormatMoney(gm.DirtyMoney);
            maxText.text = FormatMoney(gm.LaunderCapacity - gm.Laundered);
        }

        endPanel.SetActive(gm.DayOver);
        if (gm.DayOver)
            endText.text = $"Day over!\nScore: ${gm.Score}\nHigh score: ${gm.HighScore}";

        if (messageText)
        {
            bool show = Time.time < gm.MessageUntil;
            messageText.gameObject.SetActive(show);
            if (show) messageText.text = gm.Message;
        }
    }

    void UpdateCookingTimer(GameManager gm)
    {
        bool counting = gm.Soup == SoupState.Cooking || gm.Soup == SoupState.Ready;
        bool exploded = gm.ExplosionTimeLeft > 0f;
        cookingTimer.gameObject.SetActive(counting || exploded);
        if (!counting && !exploded) return;

        cookingTimer.sprite = exploded ? explodedSprite : cookingSprite;
        soupText.gameObject.SetActive(counting);
        if (counting)
        {
            bool flash = gm.Soup == SoupState.Ready && !gm.DayOver;
            float yellow = flash ? Mathf.PingPong(Time.time * 4f, 1f) : 0f;
            soupText.color = Color.Lerp(Color.white, Color.yellow, yellow);
            soupText.SetText("{0}s", Mathf.CeilToInt(gm.SoupTimeLeft));
        }

        float pop = exploded ? Mathf.Clamp01((gm.explosionDisplaySeconds - gm.ExplosionTimeLeft) / 0.25f) : 0f;
        cookingTimer.rectTransform.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(pop * Mathf.PI));
    }

    static string FormatMoney(long amount)
    {
        if (amount > -10_000 && amount < 10_000)
            return amount.ToString(CultureInfo.InvariantCulture);

        const string suffixes = "kmbt";
        decimal scaled = amount;
        decimal rounded;
        int unit = 0;
        do
        {
            scaled /= 1_000m;
            rounded = Math.Round(scaled, 1, MidpointRounding.AwayFromZero);
            unit++;
        } while (Math.Abs(rounded) >= 1_000m && unit < suffixes.Length);

        return rounded.ToString("0.#", CultureInfo.InvariantCulture) + suffixes[unit - 1];
    }
}

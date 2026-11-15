using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    public TMP_Text dayText, soupText, messageText;
    [FormerlySerializedAs("cleanText")] public TMP_Text stewText;
    [FormerlySerializedAs("dirtyText")] public TMP_Text totalText;
    [FormerlySerializedAs("launderText")] public TMP_Text maxText;
    public GameObject endPanel;
    public TMP_Text endText;
    public Button restartButton;

    void Start() => restartButton.onClick.AddListener(() => GameManager.I.Restart());

    void Update()
    {
        var gm = GameManager.I;
        if (gm == null) return;

        int t = Mathf.CeilToInt(gm.TimeLeft);
        dayText.text = $"Day ends in {t / 60}:{t % 60:00}";
                soupText.text = gm.Soup switch
        {
            SoupState.Filling => $"Soup: {gm.PotContents.Count} in pot",
            SoupState.Cooking => $"Soup: {Mathf.CeilToInt(gm.CookTimeLeft)}s",
            SoupState.Ready   => "Soup: READY",
            _                 => "Soup: pot empty"
        };
        stewText.text = FormatMoney(gm.CleanMoney);
        totalText.text = FormatMoney(gm.CleanMoney + gm.DirtyMoney);
        maxText.text = FormatMoney(gm.LaunderCapacity - gm.Laundered);

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

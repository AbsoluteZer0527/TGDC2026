using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    public TMP_Text dayText, soupText, cleanText, dirtyText, launderText;
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
        cleanText.text   = $"Soup sales: ${gm.CleanMoney}";
        dirtyText.text   = $"Creature $: ${gm.DirtyMoney} (laundered ${gm.Laundered})";
        launderText.text = $"Can launder: ${Mathf.Max(0, gm.LaunderCapacity - gm.Laundered)} more";

        endPanel.SetActive(gm.DayOver);
        if (gm.DayOver)
            endText.text = $"Day over!\nScore: ${gm.Score}\nHigh score: ${gm.HighScore}";
    }
}
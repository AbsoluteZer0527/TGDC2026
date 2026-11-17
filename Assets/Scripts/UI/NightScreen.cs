using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Night screen between days (after a passed audit), drawn on the closed curtain:
//   1. day counter rolls slot-machine style (old number slides down, new one drops in from above)
//   2. equation appears line by line:   + income (stew + hunt)  /  - rent  /  ----  /  +- total
//   3. piggy bank counts from the old savings to the new savings
// then tells GameManager to continue (next day, or eviction).
// Lives in the Systems prefab's HUDCanvas. Everything visual is placeholder: swap sprites/fonts freely.
public class NightScreen : MonoBehaviour
{
    [Tooltip("Full-screen panel (child of this object) showing the closed curtain. Hidden during the day.")]
    [SerializeField] GameObject panel;

    [Header("Day counter (both texts inside a RectMask2D)")]
    [SerializeField] TMP_Text oldDayText;
    [SerializeField] TMP_Text newDayText;
    [SerializeField] string dayFormat = "Day {0}";

    [Header("Equation")]
    [SerializeField] TMP_Text incomeText;
    [SerializeField] TMP_Text rentText;
    [SerializeField] GameObject divider;
    [SerializeField] TMP_Text totalText;

    [Header("Piggy bank")]
    [SerializeField] TMP_Text piggyText;

    [Header("Timing (seconds)")]
    [SerializeField, Min(0f)] float startDelay = 0.6f;
    [SerializeField, Min(0.01f)] float rollSeconds = 0.6f;
    [SerializeField, Min(0f)] float lineDelay = 0.5f;
    [SerializeField, Min(0.01f)] float countSeconds = 1.5f;
    [SerializeField, Min(0f)] float holdSeconds = 1.5f;

    [Header("Colors")]
    [SerializeField] Color positive = new(1f, 0.83f, 0.3f);   // yellow
    [SerializeField] Color negative = new(1f, 0.3f, 0.3f);    // red
    [SerializeField] Color gain = new(0.49f, 0.84f, 0.49f);   // green

    bool playing;

    void Awake() => panel.SetActive(false);

    void Update()
    {
        var gm = GameManager.I;
        if (!gm) return;
        if (gm.Night == NightPhase.Summary && !playing && gm.LastNight != null)
            StartCoroutine(Play(gm.LastNight));
        else if (gm.Night != NightPhase.Summary && playing)
        {
            StopAllCoroutines();
            playing = false;
            panel.SetActive(false);
        }
    }

    IEnumerator Play(GameManager.NightReport r)
    {
        playing = true;
        panel.SetActive(true);
        incomeText.gameObject.SetActive(false);
        rentText.gameObject.SetActive(false);
        if (divider) divider.SetActive(false);
        totalText.gameObject.SetActive(false);
        SetPiggy(r.savingsBefore);

        // 1. Day counter roll.
        oldDayText.text = string.Format(dayFormat, r.day);
        newDayText.text = string.Format(dayFormat, r.day + 1);
        float h = oldDayText.rectTransform.rect.height;
        Vector2 home = oldDayText.rectTransform.anchoredPosition;
        oldDayText.rectTransform.anchoredPosition = home;
        newDayText.rectTransform.anchoredPosition = home + Vector2.up * h;
        while (CurtainTransition.IsPlaying) yield return null;
        yield return new WaitForSeconds(startDelay);
        //TODO: (slot machine tick)
        for (float t = 0f; t < rollSeconds; t += Time.deltaTime)
        {
            float e = Mathf.SmoothStep(0f, 1f, t / rollSeconds);
            oldDayText.rectTransform.anchoredPosition = home + Vector2.down * (h * e);
            newDayText.rectTransform.anchoredPosition = home + Vector2.up * (h * (1f - e));
            yield return null;
        }
        oldDayText.rectTransform.anchoredPosition = home + Vector2.down * h;
        newDayText.rectTransform.anchoredPosition = home;
        yield return new WaitForSeconds(lineDelay);

        // 2. Equation.
        incomeText.text = $"+ ${HUD.FormatMoney(r.Income)}  <size=60%>(stew + hunt)</size>";
        incomeText.color = gain;
        incomeText.gameObject.SetActive(true);
        yield return new WaitForSeconds(lineDelay);

        rentText.text = $"- ${HUD.FormatMoney(r.rent)}  <size=60%>(rent)</size>";
        rentText.color = negative;
        rentText.gameObject.SetActive(true);
        yield return new WaitForSeconds(lineDelay);

        if (divider) divider.SetActive(true);
        totalText.text = (r.Change >= 0 ? "+ $" : "- $") + HUD.FormatMoney(System.Math.Abs(r.Change));
        totalText.color = r.Change >= 0 ? gain : negative;
        totalText.gameObject.SetActive(true);
        yield return new WaitForSeconds(lineDelay);

        // 3. Piggy bank counts to the new total.
        //TODO: (coin counting sound, up or down)
        for (float t = 0f; t < countSeconds; t += Time.deltaTime)
        {
            float e = Mathf.SmoothStep(0f, 1f, t / countSeconds);
            SetPiggy((long)System.Math.Round(Mathf.Lerp(r.savingsBefore, r.savingsAfter, e)));
            yield return null;
        }
        SetPiggy(r.savingsAfter);
        yield return new WaitForSeconds(holdSeconds);

        GameManager.I.FinishNightSummary(); // next day (or eviction); Update hides the panel
    }

    void SetPiggy(long amount)
    {
        piggyText.text = (amount < 0 ? "-$" : "$") + HUD.FormatMoney(System.Math.Abs(amount));
        piggyText.color = amount < 0 ? negative : positive;
    }
}

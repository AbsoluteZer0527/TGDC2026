using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Rolls the day forward, then replays the deposits and nightly rent on one balance counter.
public class NightScreen : MonoBehaviour
{
    [Tooltip("Full-screen panel (child of this object) showing the closed curtain. Hidden during the day.")]
    [SerializeField] GameObject panel;

    [Header("Day counter (both texts inside a RectMask2D)")]
    [SerializeField] TMP_Text oldDayText;
    [SerializeField] TMP_Text newDayText;
    [SerializeField] string dayFormat = "Day {0}";

    [Header("Summary")]
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

    bool playing;

    Vector2 dayHome;

    void Awake()
    {
        dayHome = oldDayText.rectTransform.anchoredPosition;
        panel.SetActive(false);
    }

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
        rentText.text = "Opening balance";
        rentText.gameObject.SetActive(true);
        if (divider) divider.SetActive(false);
        totalText.gameObject.SetActive(false);
        SetPiggy(r.savingsBefore);

        // 1. Day counter roll.
        oldDayText.text = string.Format(dayFormat, r.day);
        newDayText.text = string.Format(dayFormat, r.day + 1);
        float h = oldDayText.rectTransform.rect.height;
        Vector2 home = dayHome;
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

        long beforeRent = r.savingsBefore + r.Income;
        rentText.text = "Today's earnings";
        yield return RollBalance(r.savingsBefore, beforeRent);
        yield return new WaitForSeconds(lineDelay);

        rentText.text = $"Rent  ${HUD.FormatMoney(r.rent)}";
        yield return RollBalance(beforeRent, r.savingsAfter);
        yield return new WaitForSeconds(lineDelay);
        rentText.text = "Final balance";
        yield return new WaitForSeconds(holdSeconds);

        GameManager.I.FinishNightSummary(); // next day (or eviction); Update hides the panel
    }

    IEnumerator RollBalance(long from, long to)
    {
        if (from == to) yield break;
        var rect = piggyText.rectTransform;
        Vector2 home = rect.anchoredPosition;
        Vector3 scale = rect.localScale;
        float direction = to > from ? 1f : -1f;
        int steps = (int)System.Math.Min(6m, System.Math.Abs((decimal)to - from));
        for (float elapsed = 0f; elapsed < countSeconds; elapsed += Time.deltaTime)
        {
            float progress = elapsed / countSeconds * steps;
            int step = Mathf.FloorToInt(progress);
            float phase = progress - step;
            bool incoming = phase >= 0.5f;
            float offset = incoming ? phase - 1f : phase;
            rect.anchoredPosition = home + Vector2.up * (direction * offset * piggyText.fontSize * 0.65f);
            rect.localScale = new Vector3(scale.x, scale.y * Mathf.Abs(Mathf.Cos(phase * Mathf.PI)), scale.z);
            decimal fraction = (decimal)(step + (incoming ? 1 : 0)) / steps;
            SetPiggy((long)System.Math.Round(from + ((decimal)to - from) * fraction));
            yield return null;
        }
        rect.anchoredPosition = home;
        rect.localScale = scale;
        SetPiggy(to);
    }

    void SetPiggy(long amount)
    {
        piggyText.text = (amount < 0 ? "-$" : "$") + HUD.FormatMoney(System.Math.Abs(amount));
        piggyText.color = amount < 0 ? negative : positive;
    }
}

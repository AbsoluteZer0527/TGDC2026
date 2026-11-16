using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Hearts in the forest. Full every time you enter. At 0 you pass out: back to the kitchen and part of the day is lost.
// Max hearts live on GameManager (Starting Hearts / AddMaxHearts) so upgrades carry over between visits.
public class PlayerHealth : MonoBehaviour
{
    [Range(0f, 1f), Tooltip("Fraction of the whole day that passes when you pass out.")]
    public float dayLostOnFaint = 0.25f;
    [Min(0f), Tooltip("No more damage for this long after a hit. The hearts blink meanwhile.")]
    public float invulnerableSeconds = 1f;

    [Header("UI (all optional)")]
    [Tooltip("One heart Image, used as the template. It's copied once per max heart into its parent " +
             "(give the parent a Horizontal Layout Group).")]
    public Image heartTemplate;
    [Tooltip("No art yet? Leave empty: lost hearts just turn dark.")]
    public Sprite fullHeart, emptyHeart;
    [Range(0f, 1f), Tooltip("Lowest heart opacity while blinking.")]
    public float blinkMinAlpha = 0.25f;
    [Min(0.1f)] public float blinksPerSecond = 6f;
    [Tooltip("Full-screen red Image (Raycast Target off). Flashes when you get hit.")]
    public Image hurtFlash;
    [Min(0.01f)] public float flashSeconds = 0.35f;

    public int MaxHearts => GameManager.I != null ? GameManager.I.MaxHearts : 3;
    public int Hearts { get; private set; }
    public bool Fainted { get; private set; }
    public bool Invulnerable => Time.time < invulnerableUntil;

    readonly List<Image> hearts = new();
    CanvasGroup heartGroup;
    float invulnerableUntil, flashUntil;

    void Start()
    {
        Hearts = MaxHearts;
        if (heartTemplate)
        {
            heartTemplate.gameObject.SetActive(false);
            var row = heartTemplate.transform.parent;
            heartGroup = row.GetComponent<CanvasGroup>();
            if (!heartGroup) heartGroup = row.gameObject.AddComponent<CanvasGroup>();
            heartGroup.blocksRaycasts = false;
        }
        Refresh();
        if (hurtFlash) SetAlpha(hurtFlash, 0f);
    }

    public void Hit(int damage = 1)
    {
        var gm = GameManager.I;
        if (Fainted || damage <= 0 || Invulnerable || CurtainTransition.IsPlaying ||
            (gm != null && gm.DayOver)) return;

        //TODO: (player hurt grunt)
        Hearts = Mathf.Max(0, Hearts - damage);
        invulnerableUntil = Time.time + invulnerableSeconds;
        flashUntil = Time.time + flashSeconds;
        Refresh();
        if (Hearts == 0) Faint();
    }

    // For pickups later.
    public void Heal(int amount)
    {
        if (Fainted || amount <= 0) return;
        Hearts = Mathf.Min(MaxHearts, Hearts + amount);
        Refresh();
    }

    void Faint()
    {
        Fainted = true;
        //TODO: (pass-out sting)
        var gm = GameManager.I;
        if (gm != null)
        {
            gm.ShowMessage("You passed out...", 3f);
            gm.SkipDayTime(dayLostOnFaint);   // ends the day (and goes to the IRS) if this runs the clock out
        }
        CurtainTransition.TransitionTo("Kitchen"); // does nothing if the day-end curtain already started
    }

    void Update()
    {
        if (hearts.Count != MaxHearts) Refresh();   // max hearts changed (upgrade)

        if (heartGroup)
            heartGroup.alpha = Invulnerable && !Fainted
                ? Mathf.Lerp(blinkMinAlpha, 1f, 0.5f + 0.5f * Mathf.Cos(Time.time * blinksPerSecond * 2f * Mathf.PI))
                : 1f;

        if (hurtFlash) SetAlpha(hurtFlash, Mathf.Clamp01((flashUntil - Time.time) / flashSeconds) * 0.45f);
    }

    void Refresh()
    {
        if (!heartTemplate) return;
        while (hearts.Count < MaxHearts)
        {
            var copy = Instantiate(heartTemplate, heartTemplate.transform.parent);
            copy.name = "Heart" + (hearts.Count + 1);
            copy.gameObject.SetActive(true);
            hearts.Add(copy);
        }
        while (hearts.Count > MaxHearts)
        {
            Destroy(hearts[^1].gameObject);
            hearts.RemoveAt(hearts.Count - 1);
        }
        for (int i = 0; i < hearts.Count; i++)
        {
            bool full = i < Hearts;
            Sprite art = full ? fullHeart : emptyHeart;
            if (art) hearts[i].sprite = art;
            hearts[i].color = full || art ? heartTemplate.color : new Color(0.15f, 0.15f, 0.15f, 0.6f);
        }
    }

    static void SetAlpha(Image image, float a)
    {
        var c = image.color;
        c.a = a;
        image.color = c;
    }
}

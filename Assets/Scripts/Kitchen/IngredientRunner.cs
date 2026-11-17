using UnityEngine;
using UnityEngine.UI;

// One veggie on the counter. IngredientTable tells it when to drop in, leave, or jump into the pot.
[RequireComponent(typeof(Collider))]
public class IngredientRunner : MonoBehaviour
{
    enum State { Hidden, Dropping, Running, Thrown, Leaving }

    [Tooltip("SpriteRenderer on the Visual child (the child also has Billboard).")]
    public SpriteRenderer sprite;
    public bool artFacesRight = true;
    [Tooltip("Speed, pause and hop values are the max-difficulty (day 10) values; see Difficulty.cs.")]
    public float speed = 1.2f;
    public float maxPause = 0.8f;
    public float hopHeight = 0.08f, hopsPerSecond = 3f;
    public float gravity = 20f;

    public IngredientData Data { get; private set; }
    bool HasFrames => Data.frames != null && Data.frames.Length > 0;
    public bool Clickable => state == State.Running;
    public bool IsHidden => state == State.Hidden;

    IngredientTable table;
    State state = State.Hidden;
    Vector3 pos, vel, target, throwStart;
    float timer, pauseLeft, hop;
    Color baseColor = Color.white;
    Collider col;
    SpriteRenderer landing;
    Image flyer; // UI copy used during the throw so the dialogue box can't cover it

    public void Init(IngredientData data, IngredientTable owner)
    {
        Data = data;
        table = owner;
        col = GetComponent<Collider>();
        name = "Runner_" + data.displayName;
        if (HasFrames) sprite.sprite = data.frames[0];
        else if (data.icon) sprite.sprite = data.icon;
        else baseColor = data.color; // no art yet: tint the placeholder sprite
        Hide();
    }

    public void Drop()
    {
        pos = table.RandomPoint() + Vector3.up * (table.dropHeight + Random.Range(0f, 1.5f)); // stagger landings
        vel = Vector3.zero;
        SetVisible(true);
        state = State.Dropping;
    }

    public void Leave()
    {
        if (state == State.Hidden || state == State.Thrown) return; // a thrown one finishes its flight first
        vel = new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(3f, 5f), -Random.Range(1f, 2f)); // pop toward camera, then fall
        col.enabled = false;
        state = State.Leaving;
    }

    public void Throw(SpriteRenderer potVeggie = null)
    {
        //TODO: (whoosh as the ingredient is thrown)
        throwStart = pos;
        timer = 0f;
        if (landing) landing.enabled = true;
        landing = potVeggie;
        if (landing) landing.enabled = false;
        col.enabled = false;
        state = State.Thrown;
        if (table.flyLayer && table.Cam) SetFlying(true);
    }

    void Hide() { state = State.Hidden; SetVisible(false); }

    public void ReleaseDiscardedThrow()
    {
        if (state != State.Thrown || !landing) return;
        landing.transform.position = sprite.transform.position;
        landing.enabled = true;
        landing = null;
        Hide();
    }

    void SetVisible(bool on)
    {
        SetFlying(false);
        sprite.gameObject.SetActive(on);
        col.enabled = on;
    }

    void SetFlying(bool on)
    {
        if (on && !flyer)
        {
            flyer = new GameObject(name + "_Flying", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            flyer.transform.SetParent(table.flyLayer, false);
            flyer.raycastTarget = false;
        }
        if (flyer) flyer.gameObject.SetActive(on);
        sprite.enabled = !on;
    }

    // Place the UI copy exactly where the 3D sprite would be on screen, at the same size.
    void UpdateFlyer()
    {
        if (!flyer || !flyer.gameObject.activeSelf) return;
        Vector3 center = sprite.transform.position;
        float worldH = sprite.sprite.bounds.size.y * sprite.transform.lossyScale.y;
        Vector3 a = table.Cam.WorldToScreenPoint(center);
        Vector3 b = table.Cam.WorldToScreenPoint(center + Vector3.up * worldH);
        float scale = table.flyLayer.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(table.flyLayer, a, null, out Vector2 local);
        float h = (b.y - a.y) / scale;
        flyer.sprite = sprite.sprite;
        flyer.color = sprite.color;
        flyer.rectTransform.localPosition = local;
        flyer.rectTransform.sizeDelta = new Vector2(h * sprite.sprite.rect.width / sprite.sprite.rect.height, h);
        flyer.rectTransform.localScale = new Vector3(sprite.flipX ? -1f : 1f, 1f, 1f);
    }

    void OnDestroy()
    {
        if (flyer) Destroy(flyer.gameObject);
    }

    void Update()
    {
        if (state == State.Hidden) return;
        float dt = Time.deltaTime;
        hop = 0f;

        switch (state)
        {
            case State.Dropping:
                vel.y -= gravity * dt;
                pos += vel * dt;
                float ground = table.SurfaceHeightAt(pos);
                if (pos.y <= ground) { pos.y = ground; target = table.RandomPoint(); state = State.Running; }
                break;

            case State.Running:
                Run(dt);
                break;

            case State.Thrown:
                if (GameManager.I.Soup != SoupState.Filling ||
                    (!ReferenceEquals(landing, null) && (!landing || !landing.gameObject.activeSelf)))
                {
                    landing = null;
                    Hide();
                    return;
                }
                timer += dt / table.throwDuration;
                Vector3 pot = table.PotTarget ? table.PotTarget.position : throwStart;
                if (landing) pot = landing.transform.position - (sprite.transform.position - transform.position);
                pos = Vector3.Lerp(throwStart, pot, timer) + Vector3.up * Mathf.Sin(Mathf.Clamp01(timer) * Mathf.PI) * table.throwArc;
                if (timer >= 1f)
                {
                    AudioManager.Instance?.PlaySFX("splash");
                    if (landing) landing.enabled = true;
                    landing = null;
                    if (table.ShouldShow && GameManager.I.ingredientRefresh) Drop(); else Hide();
                    return;
                }
                break;

            case State.Leaving:
                vel.y -= gravity * dt;
                pos += vel * dt;
                if (pos.y < table.SurfaceY - table.fallAwayDepth) { Hide(); return; }
                break;
        }

        transform.position = pos + Vector3.up * hop;
        // Camera looks along +Z: further back on the board = drawn first (behind). Overrides Unity's distance sort.
        sprite.sortingOrder = -Mathf.RoundToInt(pos.z * 100f);
        sprite.color = baseColor;
        if (state == State.Thrown) UpdateFlyer();
    }

    void Run(float dt)
    {
        if (pauseLeft > 0f) { pauseLeft -= dt; SetFrame(0); return; }

        Vector3 to = target - pos;
        to.y = 0f;
        if (to.magnitude < 0.05f)
        {
            pauseLeft = Random.Range(0f, maxPause * Difficulty.MaxPause);
            target = table.RandomPoint();
            return;
        }
        // Inspector values are the day-10 maximums; Difficulty scales them down on earlier days.
        pos += Vector3.ClampMagnitude(to.normalized * speed * Difficulty.RunSpeed * dt, to.magnitude);
        pos.y = table.SurfaceHeightAt(pos); // follow a tilted board
        sprite.flipX = (to.x < 0f) == artFacesRight;
        timer += dt;
        float hps = hopsPerSecond * Difficulty.Hop;
        hop = Mathf.Abs(Mathf.Sin(timer * hps * Mathf.PI)) * hopHeight * Difficulty.Hop;
        SetFrame((int)(timer * hps)); // one frame per hop
    }

    void SetFrame(int i)
    {
        if (HasFrames) sprite.sprite = Data.frames[i % Data.frames.Length];
    }
}

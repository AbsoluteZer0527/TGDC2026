using UnityEngine;

// Spots you from far away, paws the ground, then charges in a straight line (it can't turn).
// Running into a wall or tree stuns it for a few seconds: your chance to shoot it.
public sealed class BoarAI : CreatureAI
{
    protected override string AmbientSfxId => "boar";
    enum State { Wander, WindUp, Charge, Stunned, Rest }

    [Header("Detection")]
    [SerializeField, Min(0), Tooltip("Long sight: spots you within this range if nothing blocks its view.")]
    float detectRadius = 25f;

    [Header("Charge")]
    [SerializeField, Min(0), Tooltip("Pawing the ground before it runs. It keeps aiming at you during this.")]
    float windup = 0.7f;
    [SerializeField, Min(0)] float chargeSpeed = 11f;
    [SerializeField, Min(0.1f), Tooltip("Stops on its own after this long if it never hits anything.")]
    float maxChargeSeconds = 3f;
    [SerializeField, Min(0)] float hitRadius = 1.6f;
    [SerializeField, Min(1)] int damage = 1;

    [Header("Afterwards")]
    [SerializeField, Min(0)] float stunSeconds = 3f;
    [SerializeField, Min(0), Tooltip("Pause after a charge that ran out without hitting a wall or tree.")]
    float restSeconds = 1.2f;

    [Header("Stunned look")]
    [SerializeField, Tooltip("Sprite while stunned. Empty keeps the normal sprite.")]
    Sprite stunnedSprite;
    [SerializeField, Tooltip("Frames looped above its head while stunned.")]
    Sprite[] dizzyFrames;
    [SerializeField, Min(0.1f)] float dizzyFramesPerSecond = 6f;
    [SerializeField, Tooltip("From the top-middle of the stunned sprite, in sprite units (the art is 100 px per unit). " +
                             "Negative X moves it left, over the head.")]
    Vector2 dizzyOffset = new(-1.2f, -2f);   // sits on Boar_1's lowered head
    [SerializeField, Min(0.01f)] float dizzyScale = 1f;

    [Header("Pawing dirt (during the wind-up)")]
    [SerializeField] Color dirtColor = new(0.42f, 0.27f, 0.13f);
    [SerializeField, Min(0)] int dirtCount = 10;
    [SerializeField, Min(0.01f), Tooltip("Square size, in sprite units.")]
    float dirtSize = 0.3f;
    [SerializeField, Min(0.05f), Tooltip("Seconds each square takes to fade in and out before it pops up somewhere else.")]
    float dirtLifetime = 0.3f;
    [SerializeField, Min(0), Tooltip("How far each square kicks up during its life, in sprite units.")]
    float dirtKick = 0.6f;

    State state = State.Wander;
    float stateEnds;
    Vector3 chargeDir;
    bool hitPlayer;

    SpriteRenderer dizzy;
    SpriteRenderer[] dirt;
    Vector3[] dirtStart;
    float[] dirtAge, dirtPeak;
    bool dirtShowing;
    static Sprite square;

    protected override void Start()
    {
        base.Start();
        dizzy = MakeEffect("Dizzy", null);
        dizzy.transform.localScale = Vector3.one * dizzyScale;

        if (!square)
            square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        dirt = new SpriteRenderer[dirtCount];
        dirtStart = new Vector3[dirtCount];
        dirtAge = new float[dirtCount];
        dirtPeak = new float[dirtCount];
        for (int i = 0; i < dirtCount; i++)
        {
            dirt[i] = MakeEffect("Dirt", square);
            dirt[i].transform.localScale = Vector3.one * dirtSize;
        }
    }

    protected override void Tick()
    {
        switch (state)
        {
            case State.Wander:
                creature.ShowAttackPose(false);
                if (CanSeePlayer(detectRadius)) BeginWindUp();
                else Wander();
                break;

            case State.WindUp:
                agent.isStopped = true;
                AimAtPlayer();
                if (Time.time >= stateEnds) BeginCharge();
                break;

            case State.Charge:
                Charge();
                break;

            case State.Stunned:
            case State.Rest:
                agent.isStopped = true;
                if (Time.time >= stateEnds) state = State.Wander;
                break;
        }
        UpdateDirt(state == State.WindUp);
        UpdateDizzy(state == State.Stunned);
    }

    void BeginWindUp()
    {
        //TODO: (boar snort + hoof scrape)
        state = State.WindUp;
        stateEnds = Time.time + windup;
        agent.ResetPath();
        creature.ShowAttackPose(true);   // Attack Sprite on the creature data (same as idle for the boar)
        AimAtPlayer();
    }

    void AimAtPlayer()
    {
        Vector3 to = player.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.01f) chargeDir = to.normalized;
    }

    void BeginCharge()
    {
        //TODO: (boar charge thunder)
        state = State.Charge;
        stateEnds = Time.time + maxChargeSeconds;
        hitPlayer = false;
    }

    void Charge()
    {
        Vector3 before = transform.position;
        Vector3 step = chargeDir * (chargeSpeed * Time.deltaTime);
        agent.Move(step);   // stays on the navmesh, so walls and trees stop it

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        if (!hitPlayer && toPlayer.magnitude < hitRadius && playerHealth)
        {
            //TODO: (boar impact on the player)
            playerHealth.Hit(damage);
            hitPlayer = true;   // one hit per charge; it keeps running
        }

        bool blocked = (transform.position - before).sqrMagnitude < step.sqrMagnitude * 0.1f;
        if (blocked) EndCharge(State.Stunned, stunSeconds);
        else if (Time.time >= stateEnds) EndCharge(State.Rest, restSeconds);
    }

    void EndCharge(State next, float seconds)
    {
        //TODO: (thud + dizzy tweets when stunned)
        state = next;
        stateEnds = Time.time + seconds;
        creature.ShowPose(next == State.Stunned ? stunnedSprite : null);
    }

    public override void OnHurt()
    {
        if (state == State.Wander) BeginWindUp();   // shot from afar: it comes for you
    }

    // ---------------- Effects ----------------
    // Children of the billboarded Visual, so they face the camera and positions are in sprite units.
    SpriteRenderer MakeEffect(string name, Sprite sprite)
    {
        var body = creature.Visual;
        var r = new GameObject(name).AddComponent<SpriteRenderer>();
        r.transform.SetParent(body.transform, false);
        r.sprite = sprite;
        r.sharedMaterial = body.sharedMaterial;
        r.sortingLayerID = body.sortingLayerID;
        r.sortingOrder = body.sortingOrder + 1;
        r.enabled = false;
        return r;
    }

    void UpdateDizzy(bool on)
    {
        dizzy.enabled = on && dizzyFrames != null && dizzyFrames.Length > 0;
        if (!dizzy.enabled) return;
        dizzy.sprite = dizzyFrames[(int)(Time.time * dizzyFramesPerSecond) % dizzyFrames.Length];
        Bounds b = creature.Visual.sprite ? creature.Visual.sprite.bounds : default;
        dizzy.transform.localPosition = new Vector3(b.center.x + dizzyOffset.x, b.max.y + dizzyOffset.y, -0.05f);
    }

    // Small squares around the feet, each fading in and out while kicking up, then reappearing elsewhere.
    void UpdateDirt(bool on)
    {
        bool starting = on && !dirtShowing;
        dirtShowing = on;
        for (int i = 0; i < dirt.Length; i++)
        {
            dirt[i].enabled = on;
            if (!on) continue;
            if (starting) { dirtAge[i] = Random.value; RespawnDirt(i); }

            dirtAge[i] += Time.deltaTime / dirtLifetime;
            if (dirtAge[i] >= 1f) { dirtAge[i] -= 1f; RespawnDirt(i); }

            float t = dirtAge[i];
            dirt[i].transform.localPosition = dirtStart[i] + Vector3.up * (dirtKick * t);
            Color c = dirtColor;
            c.a = dirtPeak[i] * Mathf.Sin(t * Mathf.PI);
            dirt[i].color = c;
        }
    }

    void RespawnDirt(int i)
    {
        Bounds b = creature.Visual.sprite ? creature.Visual.sprite.bounds : default;
        float x = Random.Range(b.min.x + b.size.x * 0.1f, b.max.x - b.size.x * 0.1f);
        float y = b.min.y + Random.Range(0f, 0.3f);
        dirtStart[i] = new Vector3(x, y, -0.1f);   // slightly toward the camera so it draws over the hooves
        dirtPeak[i] = Random.Range(0.4f, 1f);
    }

    void OnDisable()   // dead (Creature disables the AI) or despawned
    {
        if (dizzy) dizzy.enabled = false;
        if (dirt != null) foreach (var d in dirt) if (d) d.enabled = false;
    }
}

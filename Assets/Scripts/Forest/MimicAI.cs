using UnityEngine;

// Disguised as a bush and stays still. Get too close (or shoot it) and it reveals itself (Attack Sprite),
// then chases and attacks like the goose. Escape far enough and it settles back into a bush.
// Disguise: a copy of Bush Prefab if set (identical to the real bushes), otherwise its own World Sprite.
public sealed class MimicAI : GooseAI
{
    protected override string AmbientSfxId => null; // no goose honk; it's pretending to be a bush
    [Header("Disguise")]
    [SerializeField, Tooltip("Optional. The forest's Bush prefab, so the disguise matches the real bushes exactly.")]
    Vegetation bushPrefab;
    [SerializeField, Min(0), Tooltip("Reveals itself when you come this close (no line of sight needed).")]
    float revealRadius = 3.5f;

    bool revealed;
    Vegetation disguise;

    protected override void Start()
    {
        base.Start();
        Disguise();
    }

    protected override void Tick()
    {
        base.Tick();
        creature.ShowAttackPose(revealed);   // keeps its face for the whole chase, not just the bite
    }

    protected override bool Notices(float distance) => distance < revealRadius;

    protected override void Idle() => agent.isStopped = true;

    protected override void OnStartChase()
    {
        //TODO: (bush rustle / mimic snarl on reveal)
        revealed = true;
        if (disguise) Destroy(disguise.gameObject);
        creature.Visual.enabled = true;
    }

    protected override void OnGiveUp() => Disguise();

    void Disguise()
    {
        revealed = false;
        if (!bushPrefab) return;
        creature.Visual.enabled = false;   // its hitbox stays, so it can still be shot while hidden
        if (!disguise) disguise = Instantiate(bushPrefab, transform.position, Quaternion.identity, transform.parent);
        disguise.transform.position = transform.position;
        disguise.PickSprite(new System.Random(creature.SpawnId));
    }

    void OnDestroy()
    {
        if (disguise) Destroy(disguise.gameObject);
    }
}

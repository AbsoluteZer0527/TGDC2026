using UnityEngine;

// Looks exactly like a bush and doesn't move. Get too close (or shoot it) and it reveals itself,
// then chases and attacks exactly like the goose. If you escape far enough it settles and becomes a bush again.
public sealed class MimicAI : GooseAI
{
    [Header("Disguise")]
    [SerializeField, Tooltip("The forest's Bush prefab, so the disguise matches the real bushes.")]
    Vegetation bushPrefab;
    [SerializeField, Min(0), Tooltip("Reveals itself when you come this close (no line of sight needed).")]
    float revealRadius = 3.5f;

    Vegetation disguise;

    protected override void Start()
    {
        base.Start();
        Disguise();
    }

    protected override bool Notices(float distance) => distance < revealRadius;

    protected override void Idle()
    {
        agent.isStopped = true;   // bushes don't walk
        if (!disguise) Disguise();
    }

    protected override void OnStartChase()
    {
        //TODO: (bush rustle / mimic snarl on reveal)
        if (disguise) Destroy(disguise.gameObject);
        disguise = null;
        creature.Visual.enabled = true;
    }

    protected override void OnGiveUp() => Disguise();

    void Disguise()
    {
        creature.ShowAttackPose(false);
        if (!bushPrefab) return;   // no bush assigned: just stays visible as itself
        creature.Visual.enabled = false;
        if (!disguise)
            disguise = Instantiate(bushPrefab, transform.position, Quaternion.identity, transform.parent);
        disguise.transform.position = transform.position;
        disguise.PickSprite(new System.Random(creature.SpawnId));
    }

    void OnDestroy()
    {
        if (disguise) Destroy(disguise.gameObject);
    }
}

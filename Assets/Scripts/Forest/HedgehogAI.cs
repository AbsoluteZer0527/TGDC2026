using UnityEngine;

// Ranged enemy (hedgehog): moves exactly like the unicorn (wander, flee, dodge) but shoots at you when it can see you.
public sealed class HedgehogAI : UnicornAI
{
    [Header("Shooting")]
    [SerializeField, Min(0)] float detectRadius = 16f, memorySeconds = 3f;
    [SerializeField, Min(0.1f)] float fireCooldown = 2.5f;
    [SerializeField, Min(0), Tooltip("Telegraph (attack sprite) before the shot leaves.")]
    float windup = 0.5f;
    [SerializeField, Min(0.1f)] float projectileSpeed = 9f;
    [SerializeField, Min(1)] int damage = 1;
    [SerializeField, Tooltip("Optional. Leave empty for a placeholder orange ball.")]
    EnemyProjectile projectilePrefab;

    float nextShot, fireAt;
    bool aiming;

    protected override void Tick()
    {
        base.Tick();   // unicorn movement
        bool alerted = Alerted(detectRadius, memorySeconds);
        if (aiming)
        {
            if (Time.time >= fireAt) Fire();
            return;
        }
        if (alerted && Time.time >= nextShot && CanSeePlayer(detectRadius))
        {
            //TODO: (hedgehog huff before shooting)
            aiming = true;
            fireAt = Time.time + windup;
            creature.ShowAttackPose(true);
        }
    }

    void Fire()
    {
        aiming = false;
        nextShot = Time.time + fireCooldown * Random.Range(0.8f, 1.2f);
        creature.ShowAttackPose(false);
        if (!CanSeePlayer(detectRadius * 1.2f)) return;   // you ducked behind something during the windup

        //TODO: (hedgehog quill launch)
        Vector3 from = Eyes;
        Vector3 dir = playerEyes.position + Vector3.down * 0.3f - from;   // aim at the chest
        EnemyProjectile.Spawn(projectilePrefab, from, dir, projectileSpeed, damage);
    }

    public override void OnHurt()
    {
        base.OnHurt();
        Alert(memorySeconds);
    }
}

using UnityEngine;

// Melee enemy: wanders until it spots you, then slowly closes in, weaving side to side, and pecks when close.
public sealed class GooseAI : CreatureAI
{
    [Header("Detection")]
    [SerializeField, Min(0)] float detectRadius = 12f, memorySeconds = 4f;
    [Header("Approach")]
    [SerializeField, Min(0)] float approachSpeed = 1.6f, strafeSpeed = 0.8f;
    [SerializeField, Min(0)] float minStrafeTime = 0.6f, maxStrafeTime = 1.4f;
    [Header("Attack")]
    [SerializeField, Min(0)] float attackRange = 1.8f;
    [SerializeField, Min(0), Tooltip("Telegraph before the hit lands: back off in time to dodge it.")]
    float windup = 0.45f;
    [SerializeField, Min(0)] float recover = 0.5f, cooldown = 1.2f;
    [SerializeField, Min(1)] int damage = 1;

    float side = 1f, nextStrafeSwitch, strikeAt, recoverUntil, nextAttack, nextRepath;
    bool windingUp;

    protected override void Tick()
    {
        if (windingUp)
        {
            agent.isStopped = true;
            if (Time.time >= strikeAt) Strike();
            return;
        }
        if (Time.time < recoverUntil) { agent.isStopped = true; return; }
        creature.ShowAttackPose(false);

        if (!Alerted(detectRadius, memorySeconds)) { Wander(); return; }

        Vector3 to = player.position - transform.position;
        to.y = 0f;
        if (to.magnitude <= attackRange)
        {
            agent.isStopped = true;
            if (Time.time >= nextAttack)
            {
                //TODO: (goose honk wind-up)
                windingUp = true;
                strikeAt = Time.time + windup;
                creature.ShowAttackPose(true);
            }
            return;
        }

        // Path toward the player (around walls), plus a gentle sideways weave.
        agent.isStopped = false;
        agent.speed = approachSpeed;
        if (Time.time >= nextRepath)
        {
            agent.SetDestination(player.position);
            nextRepath = Time.time + 0.25f;
        }
        if (Time.time >= nextStrafeSwitch)
        {
            side = -side;
            nextStrafeSwitch = Time.time + Random.Range(minStrafeTime, maxStrafeTime);
        }
        Vector3 sideways = Vector3.Cross(Vector3.up, to.normalized) * side;
        agent.Move(sideways * strafeSpeed * Time.deltaTime);
    }

    void Strike()
    {
        windingUp = false;
        recoverUntil = Time.time + recover;
        nextAttack = Time.time + cooldown;
        Vector3 to = player.position - transform.position;
        to.y = 0f;
        //TODO: (peck / wing slap)
        if (to.magnitude <= attackRange * 1.3f && playerHealth) playerHealth.Hit(damage);
    }

    public override void OnHurt() => Alert(memorySeconds);   // shooting it makes it come for you
}

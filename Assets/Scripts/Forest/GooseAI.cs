using UnityEngine;
using UnityEngine.Serialization;

// Melee enemy. Wander -> spots you -> closes in slowly with a light weave -> winds up and pecks ->
// circles you at a short distance while the attack cools down -> closes in and pecks again.
// Once it has spotted you it keeps chasing (trees blocking its view don't stop it) until you get far away.
public class GooseAI : CreatureAI
{
    protected override string AmbientSfxId => "goose";
    enum State { Wander, Chase, WindUp, Circle }

    [Header("Detection")]
    [SerializeField, Min(0), Tooltip("Spots you within this range if nothing blocks its view.")]
    float detectRadius = 12f;
    [SerializeField, Min(0), Tooltip("Gives up the chase when you're further than this.")]
    float giveUpRadius = 22f;

    [Header("Approach")]
    [SerializeField, Min(0)] float approachSpeed = 1.6f;
    [SerializeField, Min(0), FormerlySerializedAs("strafeSpeed"), Tooltip("Sideways weave while approaching. Keep well below Approach Speed.")]
    float weaveSpeed = 0.5f;
    [SerializeField, Min(0), FormerlySerializedAs("minStrafeTime")] float minWeaveTime = 0.6f;
    [SerializeField, Min(0), FormerlySerializedAs("maxStrafeTime")] float maxWeaveTime = 1.4f;

    [Header("Attack")]
    [SerializeField, Min(0)] float attackRange = 1.8f;
    [SerializeField, Min(0), Tooltip("Telegraph before the hit lands: back off in time to dodge it.")]
    float windup = 0.45f;
    [SerializeField, Min(0), Tooltip("Time between pecks. It circles you meanwhile.")]
    float cooldown = 1.5f;
    [SerializeField, Min(1)] int damage = 1;

    [Header("Circling (during cooldown)")]
    [SerializeField, Min(0)] float circleSpeed = 1.4f;
    [SerializeField, Min(0), Tooltip("Distance it keeps from you while circling.")]
    float circleDistance = 2.6f;

    State state = State.Wander;
    float side = 1f, nextWeaveSwitch, strikeAt, nextAttack, nextRepath;

    protected override void Tick()
    {
        Vector3 to = player.position - transform.position;
        to.y = 0f;
        float distance = to.magnitude;
        Vector3 dir = distance > 0.01f ? to / distance : transform.forward;

        if (state == State.Wander)
        {
            if (Notices(distance)) StartChase();
            else { Idle(); return; }
        }
        if (distance > giveUpRadius && state != State.WindUp)
        {
            state = State.Wander;
            agent.ResetPath();
            OnGiveUp();
            return;
        }

        switch (state)
        {
            case State.Chase:
                if (distance <= attackRange && Time.time >= nextAttack) { BeginWindUp(); break; }
                if (distance <= circleDistance && Time.time < nextAttack) { state = State.Circle; break; }
                Approach(dir);
                break;

            case State.WindUp:
                agent.isStopped = true;
                if (Time.time >= strikeAt) Strike(distance);
                break;

            case State.Circle:
                if (Time.time >= nextAttack) { state = State.Chase; break; }
                CircleAround(dir, distance);
                break;
        }
    }

    // Overridden by MimicAI: how it idles and what makes it notice you.
    protected virtual bool Notices(float distance) => CanSeePlayer(detectRadius);
    protected virtual void Idle()
    {
        creature.ShowAttackPose(false);
        Wander();
    }
    protected virtual void OnStartChase() { }
    protected virtual void OnGiveUp() { }

    void StartChase()
    {
        //TODO: (goose honk on spotting the player)
        state = State.Chase;
        nextRepath = 0f;
        OnStartChase();
    }

    // Path toward the player (around walls), plus a gentle sideways weave.
    void Approach(Vector3 dir)
    {
        agent.isStopped = false;
        agent.speed = approachSpeed;
        if (Time.time >= nextRepath)
        {
            agent.SetDestination(player.position);
            nextRepath = Time.time + 0.25f;
        }
        SwitchSideNow();
        agent.Move(Vector3.Cross(Vector3.up, dir) * (side * weaveSpeed * Time.deltaTime));
    }

    // Strafe sideways around the player while easing toward Circle Distance.
    void CircleAround(Vector3 dir, float distance)
    {
        agent.isStopped = true;   // manual movement only, no path following
        SwitchSideNow();
        Vector3 sideways = Vector3.Cross(Vector3.up, dir) * side;
        float radial = Mathf.Clamp(distance - circleDistance, -1f, 1f);   // + = too far, step in; - = too close, step out
        Vector3 before = transform.position;
        Vector3 step = (sideways + dir * radial).normalized * (circleSpeed * Time.deltaTime);
        agent.Move(step);
        // Blocked by a tree or wall: go the other way.
        if ((transform.position - before).sqrMagnitude < step.sqrMagnitude * 0.1f) side = -side;
    }

    void SwitchSideNow()
    {
        if (Time.time < nextWeaveSwitch) return;
        side = -side;
        nextWeaveSwitch = Time.time + Random.Range(minWeaveTime, maxWeaveTime);
    }

    void BeginWindUp()
    {
        //TODO: (goose honk wind-up)
        state = State.WindUp;
        strikeAt = Time.time + windup;
        agent.isStopped = true;
        creature.ShowAttackPose(true);
    }

    void Strike(float distance)
    {
        //TODO: (peck / wing slap)
        if (distance <= attackRange * 1.3f && playerHealth) playerHealth.Hit(damage);
        creature.ShowAttackPose(false);
        nextAttack = Time.time + cooldown;
        state = State.Circle;
    }

    public override void OnHurt()
    {
        if (state == State.Wander) StartChase();   // shooting it makes it come for you
    }
}

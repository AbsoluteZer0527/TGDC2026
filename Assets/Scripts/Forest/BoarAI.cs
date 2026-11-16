using UnityEngine;

// Spots you from far away, switches to its charging sprite, then charges in a straight line (it can't turn).
// Running into a wall or tree stuns it for a few seconds: your chance to shoot it.
public sealed class BoarAI : CreatureAI
{
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

    State state = State.Wander;
    float stateEnds;
    Vector3 chargeDir;
    bool hitPlayer;

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
    }

    void BeginWindUp()
    {
        //TODO: (boar snort + hoof scrape)
        state = State.WindUp;
        stateEnds = Time.time + windup;
        agent.ResetPath();
        creature.ShowAttackPose(true);   // charging sprite (Attack Sprite on the creature data)
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
        creature.ShowAttackPose(false);
    }

    public override void OnHurt()
    {
        if (state == State.Wander) BeginWindUp();   // shot from afar: it comes for you
    }
}

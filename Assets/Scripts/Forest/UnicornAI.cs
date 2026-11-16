using UnityEngine;

// Used as-is by the Unicorn and the Deer (different speeds on their prefabs).
// Also the movement base for HedgehogAI (same fleeing/dodging, plus shooting).
public class UnicornAI : CreatureAI
{
    [Header("Fleeing and dodging")]
    [SerializeField, Min(0)] float fleeRadius = 9f, strafeRadius = 4f;
    [SerializeField, Min(0)] float fleeSpeed = 2.5f, strafeSpeed = 4.5f, acceleration = 12f;
    [SerializeField, Min(0)] float minJukeTime = 0.5f, maxJukeTime = 1.2f;
    [Header("When shot")]
    [SerializeField, Min(0)] float panicMultiplier = 1.6f, panicDuration = 2f;

    Vector3 velocity;
    float side = 1f, nextJuke, panicUntil;

    protected override void Tick()
    {
        Vector3 away = transform.position - player.position;
        away.y = 0;
        float distance = away.magnitude;
        bool panicking = Time.time < panicUntil;
        if (distance > fleeRadius && !panicking)
        {
            velocity = Vector3.zero;
            Wander();
            return;
        }

        if (agent.hasPath) agent.ResetPath();
        away.Normalize();
        bool strafe = distance < strafeRadius;
        if (strafe && Time.time > nextJuke) Juke();
        Vector3 sideways = Vector3.Cross(Vector3.up, away) * side;
        Vector3 desired = strafe
            ? (sideways + away * 0.5f).normalized * strafeSpeed
            : (away + sideways * 0.35f).normalized * fleeSpeed;
        if (panicking) desired *= panicMultiplier;
        velocity = Vector3.MoveTowards(velocity, desired, acceleration * Time.deltaTime);

        Vector3 before = transform.position;
        Vector3 step = velocity * Time.deltaTime;
        agent.Move(step);
        // Dodge the other way when a tree or wall blocks this step.
        if (step.sqrMagnitude > 0.0001f && (transform.position - before).sqrMagnitude < step.sqrMagnitude * 0.1f)
            Juke();
    }

    void Juke()
    {
        side = -side;
        nextJuke = Time.time + Random.Range(minJukeTime, maxJukeTime);
    }

    public override void OnHurt() => panicUntil = Time.time + panicDuration;
}

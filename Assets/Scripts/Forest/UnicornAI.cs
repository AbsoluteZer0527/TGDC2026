using UnityEngine;

public class UnicornAI : CreatureAI
{
    [Header("Ranges")]
    public float fleeRadius = 9f;     // starts walking away
    public float strafeRadius = 4f;   // too close: dodges side to side

    [Header("Speeds (player moves at 6)")]
    public float wanderSpeed = 1f;
    public float fleeSpeed = 2.5f;
    public float strafeSpeed = 4.5f;
    public float acceleration = 12f;

    [Header("Dodging")]
    public float minJukeTime = 0.5f, maxJukeTime = 1.2f;

    [Header("When shot")]
    public float panicMultiplier = 1.6f;
    public float panicDuration = 2f;

    Vector3 vel;
    float side = 1f, nextJuke, panicUntil;

    protected override void Tick()
    {
        float dist = DistToPlayer;
        bool panicking = Time.time < panicUntil;

        if (dist > fleeRadius && !panicking)
        {
            vel = Vector3.zero;
            Wander(4f, wanderSpeed);
            return;
        }

        if (agent.hasPath) agent.ResetPath(); // steer by hand from here on

        Vector3 away = -ToPlayerFlat().normalized;
        Vector3 sideways = Vector3.Cross(Vector3.up, away) * side;
        Vector3 desired;

        if (dist < strafeRadius)
        {
            if (Time.time > nextJuke) Juke();
            desired = (sideways + away * 0.5f).normalized * strafeSpeed;
        }
        else
        {
            desired = (away + sideways * 0.35f).normalized * fleeSpeed; // slight curve helps slip past trees
        }
        if (panicking) desired *= panicMultiplier;

        vel = Vector3.MoveTowards(vel, desired, acceleration * Time.deltaTime);

        Vector3 before = transform.position;
        Vector3 step = vel * Time.deltaTime;
        agent.Move(step); // stays on the NavMesh, so it can't walk through trees

        // Barely moved = blocked by a tree/wall → dodge the other way
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
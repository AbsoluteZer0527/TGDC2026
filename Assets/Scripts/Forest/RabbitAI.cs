using UnityEngine;
using UnityEngine.AI;

// Doesn't run from you: just scurries between nearby spots fast, with short pauses. Hard to hit, harmless.
public sealed class RabbitAI : CreatureAI
{
    [Header("Scurrying")]
    [SerializeField, Min(0)] float scurrySpeed = 7f;
    [SerializeField, Min(0)] float scurryRadius = 5f;
    [SerializeField, Min(0)] float maxPause = 0.5f;

    bool waiting;
    float moveAt;

    protected override void Awake()
    {
        base.Awake();
        agent.acceleration = 60f;   // darting starts and stops
    }

    protected override void Tick()
    {
        agent.isStopped = false;
        agent.speed = scurrySpeed;
        if (agent.pathPending || (agent.hasPath && agent.remainingDistance > 0.3f)) return;

        if (!waiting)
        {
            waiting = true;
            moveAt = Time.time + Random.Range(0f, maxPause);
            return;
        }
        if (Time.time < moveAt) return;

        waiting = false;
        Vector2 r = Random.insideUnitCircle.normalized * Random.Range(scurryRadius * 0.4f, scurryRadius);
        if (NavMesh.SamplePosition(transform.position + new Vector3(r.x, 0f, r.y), out NavMeshHit hit, 2f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }

    public override void OnHurt() => moveAt = 0f;   // dart off straight away
}

using UnityEngine;
using UnityEngine.AI;

// Doesn't run from you: just scurries between nearby open spots fast, with short pauses. Hard to hit, harmless.
public sealed class RabbitAI : CreatureAI
{
    [Header("Scurrying")]
    [SerializeField, Min(0)] float scurrySpeed = 7f;
    [SerializeField, Min(0)] float scurryRadius = 5f;
    [SerializeField, Min(0)] float maxPause = 0.5f;
    [SerializeField, Min(0), Tooltip("Stops this far short of walls and trees so it doesn't scrape along them.")]
    float wallMargin = 0.6f;

    bool waiting;
    float moveAt, giveUpAt;

    protected override void Awake()
    {
        base.Awake();
        agent.acceleration = 60f;   // darting starts and stops
    }

    protected override void Tick()
    {
        agent.isStopped = false;
        agent.speed = scurrySpeed;

        bool moving = agent.pathPending || (agent.hasPath && agent.remainingDistance > 0.3f);
        if (moving && Time.time < giveUpAt) return;   // still darting (and not stuck)

        if (!waiting)
        {
            waiting = true;
            agent.ResetPath();
            moveAt = Time.time + Random.Range(0f, maxPause);
            return;
        }
        if (Time.time < moveAt) return;

        waiting = false;
        Vector3 spot = PickSpot(out float distance);
        agent.SetDestination(spot);
        giveUpAt = Time.time + distance / Mathf.Max(0.1f, scurrySpeed) + 0.4f;   // blocked: pick another spot
    }

    // A spot it can reach in a straight line: a few random directions, cut short at the first wall or tree.
    // Keeps the longest one, so it darts into open space instead of into the wall it's next to.
    Vector3 PickSpot(out float distance)
    {
        Vector3 from = transform.position, best = from;
        distance = 0f;
        for (int i = 0; i < 6; i++)
        {
            Vector2 r = Random.insideUnitCircle.normalized;
            Vector3 dir = new(r.x, 0f, r.y);
            Vector3 to = from + dir * Random.Range(scurryRadius * 0.4f, scurryRadius);
            if (NavMesh.Raycast(from, to, out NavMeshHit hit, NavMesh.AllAreas))
                to = hit.position - dir * wallMargin;
            float d = Vector3.Distance(from, to);
            if (d > distance) { distance = d; best = to; }
            if (d >= scurryRadius * 0.4f) break;   // good enough
        }
        return best;
    }

    public override void OnHurt() => moveAt = giveUpAt = 0f;   // dart off straight away
}

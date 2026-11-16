using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public sealed class CreatureAI : MonoBehaviour
{
    NavMeshAgent agent;
    Transform player;
    CreatureData data;

    bool waitingToWander;
    float nextWanderTime;
    Vector3 velocity;
    float side = 1f, nextJuke, panicUntil;

    public void Initialize(CreatureData species) => data = species;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false; // sprites billboard, so rotation doesn't matter
    }

    void Start()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc) player = pc.transform;
    }

    void Update()
    {
        if (!data || !player || !agent.isOnNavMesh) return;
        if (GameManager.I != null && GameManager.I.DayOver) { agent.isStopped = true; return; }
        if (data.behavior == CreatureBehavior.FleeAndDodge) FleeAndDodge();
        else Wander();
    }

    public void OnHurt()
    {
        if (data.behavior == CreatureBehavior.FleeAndDodge)
            panicUntil = Time.time + data.fleeAndDodge.panicDuration;
    }

    void FleeAndDodge()
    {
        var settings = data.fleeAndDodge;
        Vector3 away = transform.position - player.position;
        away.y = 0;
        float distance = away.magnitude;
        bool panicking = Time.time < panicUntil;
        if (distance > settings.fleeRadius && !panicking)
        {
            velocity = Vector3.zero;
            Wander();
            return;
        }

        if (agent.hasPath) agent.ResetPath();
        away.Normalize();
        bool strafe = distance < settings.strafeRadius;
        if (strafe && Time.time > nextJuke) Juke(settings);
        Vector3 sideways = Vector3.Cross(Vector3.up, away) * side;
        Vector3 desired = strafe
            ? (sideways + away * 0.5f).normalized * settings.strafeSpeed
            : (away + sideways * 0.35f).normalized * settings.fleeSpeed;
        if (panicking) desired *= settings.panicMultiplier;
        velocity = Vector3.MoveTowards(velocity, desired, settings.acceleration * Time.deltaTime);

        Vector3 before = transform.position;
        Vector3 step = velocity * Time.deltaTime;
        agent.Move(step);
        // Dodge the other way when a tree or wall blocks this step.
        if (step.sqrMagnitude > 0.0001f && (transform.position - before).sqrMagnitude < step.sqrMagnitude * 0.1f)
            Juke(settings);
    }

    void Juke(FleeAndDodgeSettings settings)
    {
        side = -side;
        nextJuke = Time.time + Random.Range(settings.minJukeTime, settings.maxJukeTime);
    }

    // Walk to a random nearby point, pause, repeat.
    void Wander()
    {
        agent.isStopped = false;
        agent.speed = data.wanderSpeed;
        if (agent.pathPending) return;
        if (agent.hasPath && agent.remainingDistance > agent.stoppingDistance + 0.2f) return;

        if (!waitingToWander)
        {
            waitingToWander = true;
            nextWanderTime = Time.time + Random.Range(1f, 3f);
            return;
        }
        if (Time.time < nextWanderTime) return;

        waitingToWander = false;
        Vector2 r = Random.insideUnitCircle * data.wanderRadius;
        Vector3 target = transform.position + new Vector3(r.x, 0, r.y);
        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }
}

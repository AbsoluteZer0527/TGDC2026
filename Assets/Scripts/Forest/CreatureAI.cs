using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent, RequireComponent(typeof(NavMeshAgent), typeof(Creature))]
public abstract class CreatureAI : MonoBehaviour
{
    [Header("Wandering")]
    [SerializeField, Min(0)] float wanderRadius = 4f, wanderSpeed = 1f;

    protected NavMeshAgent agent;
    protected Transform player;

    bool waitingToWander;
    float nextWanderTime;
    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false; // sprites billboard, so rotation doesn't matter
    }

    protected virtual void Start()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc) player = pc.transform;
    }

    protected void Update()
    {
        if (!player || !agent.isOnNavMesh) return;
        if (GameManager.I != null && GameManager.I.DayOver) { agent.isStopped = true; return; }
        Tick();
    }

    protected abstract void Tick();
    public virtual void OnHurt() { }

    // Walk to a random nearby point, pause, repeat.
    protected void Wander()
    {
        agent.isStopped = false;
        agent.speed = wanderSpeed;
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
        Vector2 r = Random.insideUnitCircle * wanderRadius;
        Vector3 target = transform.position + new Vector3(r.x, 0, r.y);
        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }
}

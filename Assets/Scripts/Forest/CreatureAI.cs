using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent, RequireComponent(typeof(NavMeshAgent), typeof(Creature))]
public abstract class CreatureAI : MonoBehaviour
{
    [Header("Wandering")]
    [SerializeField, Min(0)] float wanderRadius = 4f, wanderSpeed = 1f;

    protected NavMeshAgent agent;
    protected Creature creature;
    protected Transform player, playerEyes;
    protected PlayerHealth playerHealth;

    bool waitingToWander;
    float nextWanderTime, alertUntil;
    static int sightMask;

    protected virtual void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false; // sprites billboard, so rotation doesn't matter
        creature = GetComponent<Creature>();
    }

    protected virtual void Start()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (!pc) return;
        player = pc.transform;
        playerEyes = pc.cameraPivot ? pc.cameraPivot : pc.transform;
        playerHealth = pc.GetComponent<PlayerHealth>();
    }

    // Where it looks and shoots from: the middle of its sprite, so short creatures see and shoot from low down.
    protected Vector3 Eyes => creature.VisualCenter;

    // In range, and no wall or tree in between.
    protected bool CanSeePlayer(float radius)
    {
        Vector3 from = Eyes, to = playerEyes.position;
        if ((to - from).sqrMagnitude > radius * radius) return false;
        if (sightMask == 0) sightMask = ~LayerMask.GetMask("Creature", "Player", "Ignore Raycast", "UI");
        return !Physics.Linecast(from, to, sightMask, QueryTriggerInteraction.Ignore);
    }

    // Stays alert for a while after losing sight, so it doesn't give up the moment you step behind a tree.
    protected bool Alerted(float radius, float memorySeconds)
    {
        if (CanSeePlayer(radius)) Alert(memorySeconds);
        return Time.time < alertUntil;
    }

    protected void Alert(float seconds) => alertUntil = Mathf.Max(alertUntil, Time.time + seconds);

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

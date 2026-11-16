using UnityEngine;

// A straight shot from a forest enemy. Costs the player a heart on contact; walls and trees stop it.
// No Rigidbody or collider needed: it sweeps a small sphere along its path each frame.
public sealed class EnemyProjectile : MonoBehaviour
{
    [SerializeField, Min(0.01f)] float radius = 0.25f;
    [SerializeField, Min(0.1f)] float lifetime = 5f;

    Vector3 velocity;
    int damage;
    float dieAt;
    static int hitMask;

    // With no prefab, fires a placeholder orange ball.
    public static void Spawn(EnemyProjectile prefab, Vector3 position, Vector3 direction, float speed, int damage)
    {
        var rotation = Quaternion.LookRotation(direction);
        EnemyProjectile shot;
        if (prefab) shot = Instantiate(prefab, position, rotation);
        else
        {
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(ball.GetComponent<Collider>());
            ball.transform.SetPositionAndRotation(position, rotation);
            ball.transform.localScale = Vector3.one * 0.4f;
            ball.GetComponent<Renderer>().material.color = new Color(1f, 0.5f, 0.1f);
            shot = ball.AddComponent<EnemyProjectile>();
        }
        shot.velocity = direction.normalized * speed;
        shot.damage = damage;
        shot.dieAt = Time.time + shot.lifetime;
    }

    void Update()
    {
        if (Time.time >= dieAt || (GameManager.I != null && GameManager.I.DayOver)) { Destroy(gameObject); return; }
        if (hitMask == 0) hitMask = ~LayerMask.GetMask("Creature", "Ignore Raycast", "UI");

        Vector3 step = velocity * Time.deltaTime;
        if (Physics.SphereCast(transform.position, radius, step.normalized, out RaycastHit hit, step.magnitude,
                hitMask, QueryTriggerInteraction.Ignore))
        {
            var health = hit.collider.GetComponentInParent<PlayerHealth>();
            if (health) health.Hit(damage);
            //TODO: (projectile impact)
            Destroy(gameObject);
            return;
        }
        transform.position += step;
    }
}

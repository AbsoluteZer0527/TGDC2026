using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class HitscanGun : MonoBehaviour
{
    public PlayerController player;
    public Camera cam;
    public float range = 30f;
    public float fireCooldown = 0.3f;
    public int damage = 1;
    public LayerMask hitMask = ~0;        // Everything except Player

    [Header("Feedback (all optional)")]
    public RectTransform gunSprite;       // UI image at bottom of screen
    public float recoilDistance = 40f;
    public LineRenderer tracer;
    public Transform muzzle;
    public float tracerTime = 0.04f;

    Vector2 gunRestPos;
    float nextFire;

    void Start()
    {
        if (gunSprite) gunRestPos = gunSprite.anchoredPosition;
    }

    void Update()
    {
        if (gunSprite)
            gunSprite.anchoredPosition = Vector2.Lerp(gunSprite.anchoredPosition, gunRestPos, 15f * Time.deltaTime);

        if (!player.CanAct) return;
        var m = Mouse.current;
        if (m != null && m.leftButton.isPressed && Time.time >= nextFire) Fire();
    }

    void Fire()
    {
        nextFire = Time.time + fireCooldown;
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        Vector3 end = ray.GetPoint(range);

        if (Physics.Raycast(ray, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            end = hit.point;
            var creature = hit.collider.GetComponentInParent<Creature>();
            if (creature) creature.TakeDamage(damage);
        }

        if (gunSprite) gunSprite.anchoredPosition = gunRestPos + Vector2.down * recoilDistance;
        if (tracer && muzzle) StartCoroutine(ShowTracer(muzzle.position, end));
    }

    IEnumerator ShowTracer(Vector3 a, Vector3 b)
    {
        tracer.SetPosition(0, a);
        tracer.SetPosition(1, b);
        tracer.enabled = true;
        yield return new WaitForSeconds(tracerTime);
        tracer.enabled = false;
    }
}
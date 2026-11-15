using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
    public Sprite idleSprite, aimSprite;
    public Sprite[] reloadFrames;
    public Image muzzleFlash;
    [Min(0.01f)] public float flashDuration = 0.07f;
    [Header("Aim")]
    [Range(0.2f, 1f)] public float aimFovMultiplier = 0.7f;
    [Range(0f, 1f)] public float aimMoveMultiplier = 0.5f;
    public float recoilDistance = 40f;
    public LineRenderer tracer;
    public Transform muzzle;
    public float tracerTime = 0.04f;

    Vector2 gunRestPos;
    Image gunImage;
    float reloadStarted;
    bool aiming;
    float normalFov;
    float nextFire;

    void Awake()
    {
        normalFov = cam.fieldOfView;
        if (gunSprite)
        {
            gunRestPos = gunSprite.anchoredPosition;
            gunImage = gunSprite.GetComponent<Image>();
        }
    }

    void OnDisable()
    {
        aiming = false;
        nextFire = 0f;
        if (player) player.MoveSpeedMultiplier = 1f;
        if (cam) cam.fieldOfView = normalFov;
        if (muzzleFlash) muzzleFlash.enabled = false;
        if (tracer) tracer.enabled = false;
    }

    void OnApplicationFocus(bool focused)
    {
        if (!focused) aiming = false;
    }

    void Update()
    {
        if (gunSprite)
            gunSprite.anchoredPosition = Vector2.Lerp(gunSprite.anchoredPosition, gunRestPos, 15f * Time.deltaTime);

        var m = Mouse.current;
        bool canAct = player.CanAct && m != null && Application.isFocused;
        if (!canAct || Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            aiming = false;
        else
        {
            if (Time.time >= nextFire && m.leftButton.wasPressedThisFrame) aiming = true;
            if (aiming && m.leftButton.wasReleasedThisFrame)
            {
                aiming = false;
                Fire();
            }
        }

        player.MoveSpeedMultiplier = aiming ? aimMoveMultiplier : 1f;
        float targetFov = aiming ? normalFov * aimFovMultiplier : normalFov;
        cam.fieldOfView = Mathf.MoveTowards(cam.fieldOfView, targetFov, 180f * Time.deltaTime);

        bool cycling = Time.time < nextFire;
        //TODO: (gun closing once when the reload cycle finishes)
        bool flashing = cycling && Time.time < reloadStarted;
        if (muzzleFlash)
        {
            muzzleFlash.enabled = flashing;
            if (flashing && gunSprite) muzzleFlash.rectTransform.anchoredPosition = gunSprite.anchoredPosition;
        }
        Sprite pose = (aiming || flashing) && aimSprite ? aimSprite : idleSprite;
        if (cycling && !flashing && reloadFrames != null && reloadFrames.Length > 0)
        {
            float progress = (Time.time - reloadStarted) / (nextFire - reloadStarted);
            int frame = Mathf.Clamp((int)(progress * reloadFrames.Length), 0, reloadFrames.Length - 1);
            if (reloadFrames[frame]) pose = reloadFrames[frame];
        }
        if (gunImage && pose) gunImage.sprite = pose;
    }

    void Fire()
    {
        //TODO: (gunshot and arrow launch together on each shot)
        reloadStarted = Time.time + flashDuration;
        nextFire = reloadStarted + fireCooldown;
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

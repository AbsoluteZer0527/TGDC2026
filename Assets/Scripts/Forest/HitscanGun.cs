using System.Collections;
using System.Collections.Generic;
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
    [Header("Arrow shot")]
    public Sprite arrowShot;
    [SerializeField, Min(0.1f)] float arrowSizeMultiplier = 1.75f;
    [Min(0.01f)] public float arrowFlightTime = 0.22f;

    readonly List<Image> flyingArrows = new();

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
        StopAllCoroutines();
        foreach (var arrow in flyingArrows)
            if (arrow) Destroy(arrow.gameObject);
        flyingArrows.Clear();
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
        AudioManager.Instance?.PlaySFX("gunshot");
        //TODO: (gunshot and arrow launch together on each shot)
        reloadStarted = Time.time + flashDuration;
        nextFire = reloadStarted + fireCooldown;
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        Vector3 end = ray.GetPoint(range);

        if (Physics.Raycast(ray, out RaycastHit aimHit, range, hitMask, QueryTriggerInteraction.Ignore))
            end = aimHit.point;

        if (gunSprite) gunSprite.anchoredPosition = gunRestPos + Vector2.down * recoilDistance;
        StartCoroutine(FlyArrow(end));
    }

    IEnumerator FlyArrow(Vector3 aimPoint)
    {
        Image image = null;
        Camera uiCamera = null;
        Vector3 start = cam.transform.position;
        float launchDepth = cam.nearClipPlane + 0.01f;
        if (gunSprite && arrowShot && aimSprite)
        {
            image = new GameObject("Arrow shot", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            RectTransform arrow = image.rectTransform;
            arrow.SetParent(gunSprite.parent, false);
            arrow.SetSiblingIndex((muzzleFlash ? muzzleFlash.transform : gunSprite).GetSiblingIndex() + 1);
            image.sprite = arrowShot;
            image.raycastTarget = false;
            flyingArrows.Add(image);

            // Both sprites use the same artboard. The arrow slice marks the muzzle in the firing pose.
            Rect gun = gunSprite.rect;
            Rect art = aimSprite.rect;
            float fit = Mathf.Min(gun.width / art.width, gun.height / art.height);
            Vector2 origin = gun.min + (gun.size - art.size * fit) * gunSprite.pivot;
            arrow.position = gunSprite.TransformPoint(origin + (arrowShot.rect.center - art.min) * fit);
            arrow.sizeDelta = arrowShot.rect.size * fit * arrowSizeMultiplier;
            Canvas canvas = image.canvas.rootCanvas;
            uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 muzzle = RectTransformUtility.WorldToScreenPoint(uiCamera, arrow.position);
            start = cam.ScreenToWorldPoint(new Vector3(muzzle.x, muzzle.y, launchDepth));
        }

        Vector3 direction = (aimPoint - start).normalized;
        Vector3 position = start;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, arrowFlightTime);
        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 next = start + direction * (range * t * t);
            // Sweep only the distance the arrow actually covered this frame.
            if (Physics.Linecast(position, next, out RaycastHit hit, hitMask, QueryTriggerInteraction.Ignore))
            {
                var creature = hit.collider.GetComponentInParent<Creature>();
                if (creature) creature.TakeDamage(damage);
                break;
            }
            position = next;
            if (!image) continue;
            Vector3 screen = cam.WorldToScreenPoint(position);
            image.enabled = screen.z >= cam.nearClipPlane;
            if (!image.enabled) continue;
            RectTransform arrow = image.rectTransform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)arrow.parent, screen, uiCamera, out Vector2 local);
            arrow.localPosition = local;
            arrow.localScale = Vector3.one / (1f + Vector3.Distance(start, position));
        }
        if (image)
        {
            flyingArrows.Remove(image);
            Destroy(image.gameObject);
        }
    }
}

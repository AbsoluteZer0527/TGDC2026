using System.Collections;
using UnityEngine;

public sealed class Creature : MonoBehaviour
{
    [SerializeField] SpriteRenderer visual;
    [SerializeField, Min(0.01f)] float fallDuration = 0.35f;
    [SerializeField, Min(0f)] float walkBounceHeight = 0.075f;
    [SerializeField, Range(0.5f, 1f), Tooltip("Hitbox size relative to the sprite. A bit under 1 feels fair " +
                                               "(transparent corners of the art don't count as hits).")]
    float hitboxScale = 0.9f;
    BoxCollider hitbox;

    public CreatureData Data { get; private set; }
    internal SpriteRenderer Visual => visual;
    public int SpawnId { get; private set; }
    CreatureAI ai;
    int hp;
    Coroutine flash;
    Vector3 visualRestPosition, previousPosition;
    float stepPhase, bounceWeight;

    public void Initialize(CreatureData data, int spawnId)
    {
        Data = data;
        SpawnId = spawnId;
        hp = data.maxHealth;
        transform.localScale = Vector3.one * data.worldScale;
        visualRestPosition = visual.transform.localPosition;
        // Hitbox lives on the billboarded sprite, so it always faces the camera and matches the art.
        // The prefab's capsule is one tall shape for every species, so it's switched off.
        if (TryGetComponent<CapsuleCollider>(out var capsule)) capsule.enabled = false;
        if (!visual.TryGetComponent(out hitbox)) hitbox = visual.gameObject.AddComponent<BoxCollider>();
        SetSprite(data.worldSprite);
        previousPosition = transform.position;
        ai = GetComponent<CreatureAI>();
    }

    // Puts the bottom edge of the sprite on the ground, whatever its pivot, so every species stands on the floor.
    // Slices are trimmed to the art, so the bottom edge is the feet. Ground Offset on the data nudges it.
    void SetSprite(Sprite sprite)
    {
        visual.sprite = sprite;
        if (!sprite) return;
        float sink = Data.groundOffset / Mathf.Max(0.01f, Data.worldScale);   // world units -> root space
        visualRestPosition.y = -sprite.bounds.min.y * visual.transform.localScale.y + sink;
        visual.transform.localPosition = visualRestPosition;

        // Box = the sprite's rectangle (in the sprite's own units, so the Visual's scale applies automatically).
        Bounds art = sprite.bounds;
        Vector3 size = art.size * hitboxScale;
        size.z = Mathf.Min(art.size.x, art.size.y) * 0.5f;   // some depth so grazing shots still count
        hitbox.center = art.center;
        hitbox.size = size;
    }

    void LateUpdate()
    {
        if (hp <= 0 || Time.deltaTime <= 0f) return;
        Vector3 movement = transform.position - previousPosition;
        previousPosition = transform.position;
        movement.y = 0f;
        float speed = movement.magnitude / Time.deltaTime;
        // Use actual movement so wandering, dodging and charging share the same gait.
        float stepsPerSecond = Mathf.Min(speed * 2f, 4f);
        stepPhase = Mathf.Repeat(stepPhase + stepsPerSecond * Time.deltaTime, 1f);
        bounceWeight = Mathf.MoveTowards(bounceWeight, Mathf.Clamp01(speed), 8f * Time.deltaTime);
        float hop = 4f * stepPhase * (1f - stepPhase);
        visual.transform.localPosition = visualRestPosition + Vector3.up * (walkBounceHeight * bounceWeight * hop);
    }

    // Middle of the visible sprite (not the hit capsule, which is the same tall shape for every species).
    public Vector3 VisualCenter => visual.bounds.center;

    // Swap to the species' attack sprite (if it has one) while winding up / attacking.
    public void ShowAttackPose(bool on)
    {
        var sprite = on && Data.attackSprite ? Data.attackSprite : Data.worldSprite;
        if (visual.sprite != sprite) SetSprite(sprite);
    }

    public void TakeDamage(int dmg)
    {
        if (hp <= 0 || dmg <= 0) return;
        hp -= dmg;
        if (hp <= 0) { Die(); return; }
        ai.OnHurt();
        if (flash != null) StopCoroutine(flash);
        flash = StartCoroutine(Flash());
    }

    IEnumerator Flash()
    {
        visual.color = new Color(1f, 0.55f, 0.55f);
        yield return new WaitForSeconds(0.08f);
        visual.color = Color.white;
        flash = null;
    }

    void Die()
    {
        visual.transform.localPosition = visualRestPosition;
        ai.enabled = false;
        GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
        foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
        if (visual.TryGetComponent<Billboard>(out var billboard)) billboard.enabled = false;
        if (flash != null) StopCoroutine(flash);
        visual.color = Color.white;        var gm = GameManager.I;
        if (gm != null)
        {
            gm.KilledCreatures.Add(SpawnId);
            gm.AddDirtyMoney(Data.bountyPrice);
            gm.ShowMessage($"+${Data.bountyPrice}  {Data.displayName}", 1.2f, Data.itemIcon);
        }
        StartCoroutine(FallOver());
    }

    IEnumerator FallOver()
    {
        Transform card = visual.transform;
        Quaternion upright = card.rotation;
        Vector3 feet = visual.localBounds.center;
        feet.y = visual.localBounds.min.y;
        Vector3 hinge = card.TransformPoint(feet);
        float elapsed = 0f;
        while (elapsed < fallDuration)
        {
            yield return null;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fallDuration);
            card.rotation = upright * Quaternion.Euler(90f * t * t, 0f, 0f);
            card.position += hinge - card.TransformPoint(feet);
        }
        yield return null;
        Destroy(gameObject);
    }
}

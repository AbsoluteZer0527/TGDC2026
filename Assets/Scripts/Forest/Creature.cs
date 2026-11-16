using System.Collections;
using UnityEngine;

public sealed class Creature : MonoBehaviour
{
    [SerializeField] SpriteRenderer visual;
    [SerializeField, Min(0.01f)] float fallDuration = 0.35f;
    [SerializeField, Min(0f)] float walkBounceHeight = 0.075f;

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
        visual.sprite = data.worldSprite;
        visualRestPosition = visual.transform.localPosition;
        previousPosition = transform.position;
        ai = GetComponent<CreatureAI>();
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

    // Swap to the species' attack sprite (if it has one) while winding up / attacking.
    public void ShowAttackPose(bool on) =>
        visual.sprite = on && Data.attackSprite ? Data.attackSprite : Data.worldSprite;

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
        visual.color = Color.white;
        visual.enabled = true;   // a disguised mimic shows itself as it falls
        var gm = GameManager.I;
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

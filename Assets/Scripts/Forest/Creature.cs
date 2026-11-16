using System.Collections;
using UnityEngine;

public sealed class Creature : MonoBehaviour
{
    [SerializeField] SpriteRenderer visual;
    [SerializeField, Min(0.01f)] float fallDuration = 0.35f;

    public CreatureData Data { get; private set; }
    public int SpawnId { get; private set; }
    CreatureAI ai;
    int hp;
    Coroutine flash;

    public void Initialize(CreatureData data, int spawnId)
    {
        Data = data;
        SpawnId = spawnId;
        hp = data.maxHealth;
        transform.localScale = Vector3.one * data.worldScale;
        visual.sprite = data.worldSprite;
        ai = GetComponent<CreatureAI>();
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
        ai.enabled = false;
        GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
        foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
        if (visual.TryGetComponent<Billboard>(out var billboard)) billboard.enabled = false;
        if (flash != null) StopCoroutine(flash);
        visual.color = Color.white;
        var gm = GameManager.I;
        if (gm != null)
        {
            gm.KilledCreatures.Add(SpawnId);
            gm.AddDirtyMoney(Data.bountyPrice);
            gm.ShowMessage($"+${Data.bountyPrice}  {Data.displayName}", 1.2f);
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

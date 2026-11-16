using System.Collections;
using UnityEngine;

public sealed class Creature : MonoBehaviour
{
    [SerializeField] SpriteRenderer visual;

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
        var gm = GameManager.I;
        if (gm != null)
        {
            gm.KilledCreatures.Add(SpawnId);
            gm.AddDirtyMoney(Data.bountyPrice);
            gm.ShowMessage($"+${Data.bountyPrice}  {Data.displayName}", 1.2f);
        }
        Destroy(gameObject);
    }
}

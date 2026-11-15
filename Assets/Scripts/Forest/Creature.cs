using System.Collections;
using UnityEngine;

public class Creature : MonoBehaviour
{
    public string displayName = "Creature";
    public int maxHealth = 1;
    public int value = 10;
    public Renderer rend;
    [HideInInspector] public int id;

    int hp;
    Color baseColor;

    void Start()
    {
        hp = maxHealth;
        if (rend) baseColor = rend.material.color;
    }

    public void TakeDamage(int dmg)
    {
        //TODO: (optional hurt sound specific to each mob when hit)
        hp -= dmg;
        if (TryGetComponent(out CreatureAI ai)) ai.OnHurt();
        if (hp <= 0) { Die(); return; }
        if (rend) StartCoroutine(Flash());
    }

    IEnumerator Flash()
    {
        rend.material.color = Color.white;
        yield return new WaitForSeconds(0.08f);
        rend.material.color = baseColor;
    }

    void Die()
    {
        var gm = GameManager.I;
        if (gm != null)
        {
            gm.AddDirtyMoney(value);
            gm.KilledCreatures.Add(id);
            gm.ShowMessage($"+${value}  {displayName}", 1.2f);
        }
        Destroy(gameObject);
    }
}
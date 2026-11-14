using System.Collections;
using UnityEngine;

public class Creature : MonoBehaviour
{
    public int maxHealth = 1;
    public int value = 10;            // dirty $ when killed
    public Renderer rend;             // visual quad, flashes white when hit
    [HideInInspector] public int id;  // set by the generator

    int hp;
    Color baseColor;

    void Start()
    {
        hp = maxHealth;
        if (rend) baseColor = rend.material.color;
    }

    public void TakeDamage(int dmg)
    {
        hp -= dmg;
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
        }
        Destroy(gameObject);
    }
}
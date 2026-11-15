using UnityEngine;

public class Vegetation : MonoBehaviour
{
    [SerializeField] SpriteRenderer visual;
    [SerializeField] Sprite[] sprites;
    [SerializeField] Sprite mirroredSprite;

    public void PickSprite(System.Random random)
    {
        if (!visual || sprites == null || sprites.Length == 0) return;
        visual.sprite = sprites[random.Next(sprites.Length)];
        visual.flipX = mirroredSprite && visual.sprite == mirroredSprite;
    }
}

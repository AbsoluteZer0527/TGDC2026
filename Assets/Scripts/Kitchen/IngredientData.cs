using UnityEngine;

[CreateAssetMenu(menuName = "Soup/Ingredient")]
public class IngredientData : ScriptableObject
{
    public string displayName;
    public Color color = Color.white;
    public Sprite icon; // for later
}
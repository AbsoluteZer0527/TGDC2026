using UnityEngine;

[CreateAssetMenu(menuName = "Soup/Ingredient")]
public class IngredientData : ScriptableObject
{
    public string displayName;
    public Color color = Color.white;
    public Sprite icon; // for later
    [Tooltip("Walk cycle on the counter, e.g. Carrot1_0, Carrot2_0. Frame 0 is also the standing pose.")]
    public Sprite[] frames;
}
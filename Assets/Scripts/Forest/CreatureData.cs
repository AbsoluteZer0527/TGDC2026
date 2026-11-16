using UnityEngine;

[CreateAssetMenu(menuName = "Soup/Forest/Creature")]
public sealed class CreatureData : ScriptableObject
{
    public string displayName;
    public Sprite worldSprite;
    [Tooltip("Optional. Shown while this creature winds up and attacks (e.g. Goose attack).")]
    public Sprite attackSprite;
    [Min(0f), Tooltip("Never spawns closer than this to the player's start point. Set ~22 to keep it out of the start room.")]
    public float minSpawnDistance = 0f;
    [Min(0.01f), Tooltip("Size of this species in the forest. 1 is the prefab's original size.")]
    public float worldScale = 1f;
    public Sprite itemIcon;
    [Min(1)] public int maxHealth = 1;
    [Min(0)] public int bountyPrice = 10;

    [Tooltip("A ForestCreature prefab variant with this species' CreatureAI script on its root.")]
    public CreatureAI behaviorPrefab;
}

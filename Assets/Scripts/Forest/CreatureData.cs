using System;
using UnityEngine;

public enum CreatureBehavior { FleeAndDodge, Wander }

[Serializable]
public class FleeAndDodgeSettings
{
    [Min(0)] public float fleeRadius = 9f, strafeRadius = 4f;
    [Min(0)] public float fleeSpeed = 2.5f, strafeSpeed = 4.5f, acceleration = 12f;
    [Min(0)] public float minJukeTime = 0.5f, maxJukeTime = 1.2f;
    [Min(0)] public float panicMultiplier = 1.6f, panicDuration = 2f;
}

[CreateAssetMenu(menuName = "Soup/Forest/Creature")]
public sealed class CreatureData : ScriptableObject
{
    public string displayName;
    public Sprite worldSprite;
    [Min(0.01f), Tooltip("Size of this species in the forest. 1 is the prefab's original size.")]
    public float worldScale = 1f;
    public Sprite itemIcon;
    [Min(1)] public int maxHealth = 1;
    [Min(0)] public int bountyPrice = 10;

    public CreatureBehavior behavior = CreatureBehavior.FleeAndDodge;
    [Min(0)] public float wanderRadius = 4f, wanderSpeed = 1f;
    public FleeAndDodgeSettings fleeAndDodge = new();
}

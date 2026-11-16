using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Soup/Forest/Roster")]
public sealed class ForestRoster : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public CreatureData creature;
        [Min(0)] public float weight;
        public bool IsEligible => creature != null && creature.behaviorPrefab != null && weight > 0;
    }

    [Tooltip("Letter order. A weight of zero removes a species from both the forest and the letter.")]
    [SerializeField] Entry[] entries = Array.Empty<Entry>();
    public IReadOnlyList<Entry> Entries => entries;

    [Header("Daily selection")]
    [Tooltip("On: each day only some species appear (in the forest and on the letter).\n" +
             "Rolls floor(species × Daily Pick Fraction) times by weight; repeats are allowed, so the day " +
             "can end up with fewer species than rolls. Always at least one.")]
    [SerializeField] bool dailySelection = true;
    [SerializeField, Range(0f, 1f)] float dailyPickFraction = 0.8f;

    // Species allowed today. Same result for the whole day (it's seeded by the forest seed).
    public HashSet<CreatureData> Today(int forestSeed)
    {
        var all = new HashSet<CreatureData>();
        foreach (var entry in entries)
            if (entry.IsEligible) all.Add(entry.creature);
        if (!dailySelection || all.Count == 0) return all;

        var rng = new System.Random(forestSeed ^ 0x5bd1e995);   // separate stream from the layout rolls
        int rolls = Mathf.Max(1, Mathf.FloorToInt(all.Count * dailyPickFraction));
        var today = new HashSet<CreatureData>();
        for (int i = 0; i < rolls; i++) today.Add(Pick(rng));
        return today;
    }

    // Weighted pick. allowed = null means any eligible species.
    public CreatureData Pick(System.Random rng, ICollection<CreatureData> allowed = null)
    {
        double totalWeight = 0;
        foreach (var entry in entries)
            if (Allowed(entry, allowed)) totalWeight += entry.weight;

        double roll = rng.NextDouble() * totalWeight;
        foreach (var entry in entries)
        {
            if (!Allowed(entry, allowed)) continue;
            if (roll < entry.weight) return entry.creature;
            roll -= entry.weight;
        }
        return null;
    }

    static bool Allowed(Entry entry, ICollection<CreatureData> allowed) =>
        entry.IsEligible && (allowed == null || allowed.Contains(entry.creature));
}

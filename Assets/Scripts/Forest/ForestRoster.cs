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
        public bool IsEligible => creature != null && weight > 0;
    }

    [Tooltip("Letter order. A weight of zero removes a species from both the forest and the letter.")]
    [SerializeField] Entry[] entries = Array.Empty<Entry>();
    public IReadOnlyList<Entry> Entries => entries;

    public CreatureData Pick(System.Random rng)
    {
        double totalWeight = 0;
        foreach (var entry in entries)
            if (entry.IsEligible) totalWeight += entry.weight;

        double roll = rng.NextDouble() * totalWeight;
        foreach (var entry in entries)
        {
            if (!entry.IsEligible) continue;
            if (roll < entry.weight) return entry.creature;
            roll -= entry.weight;
        }
        return null;
    }
}

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
        [Min(0), Tooltip("How common this species is in the forest compared to the others that day. " +
                         "Also how likely it is to be picked as one of the day's extra species.")]
        public float weight;
        [Tooltip("Appears every day (Daily Selection only picks the extras).")]
        public bool everyDay;
        public bool IsEligible => creature != null && creature.behaviorPrefab != null && weight > 0;
    }

    [Tooltip("Letter order. A weight of zero removes a species from both the forest and the letter.")]
    [SerializeField] Entry[] entries = Array.Empty<Entry>();
    public IReadOnlyList<Entry> Entries => entries;

    [Header("Daily selection")]
    [Tooltip("On: each day has Min–Max species (50/50 for 4 or 5 by default): every 'Every Day' species, " +
             "plus extras picked by weight from the rest, no repeats. Off: every species appears every day.")]
    [SerializeField] bool dailySelection = true;
    [SerializeField, Min(1)] int minSpeciesPerDay = 4, maxSpeciesPerDay = 5;

    // Species allowed today. Same result for the whole day (it's seeded by the forest seed).
    public HashSet<CreatureData> Today(int forestSeed)
    {
        var today = new HashSet<CreatureData>();
        var extras = new List<Entry>();
        foreach (var entry in entries)
        {
            if (!entry.IsEligible) continue;
            if (!dailySelection || entry.everyDay) today.Add(entry.creature);
            else extras.Add(entry);
        }
        if (!dailySelection) return today;

        var rng = new System.Random(forestSeed ^ 0x5bd1e995);   // separate stream from the layout rolls
        int target = rng.Next(minSpeciesPerDay, Mathf.Max(minSpeciesPerDay, maxSpeciesPerDay) + 1);
        while (today.Count < target && extras.Count > 0)
        {
            // Weighted pick without replacement.
            double total = 0;
            foreach (var e in extras) total += e.weight;
            double roll = rng.NextDouble() * total;
            int i = 0;
            while (i < extras.Count - 1 && roll >= extras[i].weight) roll -= extras[i++].weight;
            var picked = extras[i].creature;
            today.Add(picked);
            extras.RemoveAll(e => e.creature == picked);
        }
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

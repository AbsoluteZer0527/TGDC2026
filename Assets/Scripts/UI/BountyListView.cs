using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class BountyListView : MonoBehaviour
{
    [SerializeField] ForestRoster roster;
    [SerializeField] BountyRow rowPrefab;
    [SerializeField] ScrollRect scroll;

    readonly List<BountyRow> rows = new();

    void OnEnable()
    {
        var listed = new HashSet<CreatureData>();
        var today = roster.Today(GameManager.I ? GameManager.I.ForestSeed : 12345);   // only today's species
        int count = 0;
        foreach (var entry in roster.Entries)
        {
            if (!entry.IsEligible || !today.Contains(entry.creature) || !listed.Add(entry.creature)) continue;
            if (count == rows.Count) rows.Add(Instantiate(rowPrefab, scroll.content));
            var row = rows[count++];
            row.Bind(entry.creature);
            row.gameObject.SetActive(true);
        }
        for (int i = count; i < rows.Count; i++)
            rows[i].gameObject.SetActive(false);

        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1f;
    }
}

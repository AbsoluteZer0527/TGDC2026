using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(NpcReferenceAttribute))]
public class NpcReferenceDrawer : PropertyDrawer
{
    public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
    {
        var component = property.serializedObject.targetObject as Component ?? property.serializedObject.context as Component;
        var kitchen = component ? component.GetComponent<KitchenController>() : null;
        if (!kitchen)
        {
            property.intValue = EditorGUI.IntField(rect, label, property.intValue);
            return;
        }
        using var rosterSource = new SerializedObject(kitchen);
        var roster = component == kitchen ? property.serializedObject.FindProperty("keyNpcs")
            : rosterSource.FindProperty("keyNpcs");
        var ids = new List<int>();
        var labels = new List<string>();
        if (((NpcReferenceAttribute)attribute).includeRandom && !property.propertyPath.StartsWith("keyGreetings."))
        {
            ids.Add(0);
            labels.Add("0 — Random customers");
        }
        for (int i = 0; i < roster.arraySize; i++)
        {
            var entry = roster.GetArrayElementAtIndex(i);
            int id = entry.FindPropertyRelative("npcId").intValue;
            if (id <= 0) continue;
            if (ids.Contains(id))
            {
                labels[ids.IndexOf(id)] = $"{id} — Duplicate ID in roster";
                continue;
            }
            ids.Add(id);
            string name = entry.FindPropertyRelative("displayName").stringValue;
            labels.Add($"{id} — {(string.IsNullOrWhiteSpace(name) ? "Unnamed NPC" : name)}");
        }
        if (!ids.Contains(property.intValue))
        {
            ids.Add(property.intValue);
            labels.Add($"{property.intValue} — Missing from roster");
        }
        EditorGUI.BeginProperty(rect, label, property);
        EditorGUI.BeginChangeCheck();
        int selected = EditorGUI.IntPopup(rect, label, property.intValue,
            labels.ConvertAll(name => new GUIContent(name)).ToArray(), ids.ToArray());
        if (EditorGUI.EndChangeCheck()) property.intValue = selected;
        EditorGUI.EndProperty();
    }
}

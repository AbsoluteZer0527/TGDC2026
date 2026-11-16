using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(KitchenDialogue))]
public sealed class KitchenDialogueEditor : Editor
{
    SerializedObject dialogue;

    void OnDisable() => dialogue?.Dispose();

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var file = serializedObject.FindProperty("dialogueFile");
        EditorGUILayout.PropertyField(file);
        serializedObject.ApplyModifiedProperties();

        var asset = file.objectReferenceValue;
        if (asset)
        {
            if (dialogue == null || dialogue.targetObject != asset)
            {
                dialogue?.Dispose();
                dialogue = new SerializedObject(asset, target);
            }
            dialogue.Update();
            DrawPropertiesExcluding(dialogue, "m_Script");
            EditorGUILayout.HelpBox("Auditor NPC ID selects the end-of-day visitor from the kitchen roster. " +
                "99 is the IRS agent. The visit happens when the timer expires and does not advance the numbered customer schedule.", MessageType.Info);
            dialogue.ApplyModifiedProperties();
        }
        else
        {
            EditorGUILayout.HelpBox("Assign the dialogue asset for this kitchen.", MessageType.Info);
        }

        DrawPropertiesExcluding(serializedObject, "m_Script", "dialogueFile");
        serializedObject.ApplyModifiedProperties();
    }
}

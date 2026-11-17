using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
static class PlayModeCursor
{
    static PlayModeCursor()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
            Cursor.SetCursor(PlayerSettings.defaultCursor, PlayerSettings.cursorHotspot, CursorMode.Auto);
        else if (state == PlayModeStateChange.ExitingPlayMode)
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }
}

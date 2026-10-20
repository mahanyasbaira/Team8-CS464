using UnityEditor;
using UnityEngine;

// Dialog helpers that also work in batch mode (GitHub Actions has no screen).
// In batch mode, errors throw so the Unity run exits with an error code, and confirmations say yes.
public static class Team8Editor
{
    public static bool Batch => Application.isBatchMode;

    public static void Fail(string message)
    {
        if (Batch) throw new System.Exception("[Team8] " + message);
        EditorUtility.DisplayDialog("Team8", message, "OK");
    }

    public static bool Confirm(string message)
    {
        if (Batch) return true;
        return EditorUtility.DisplayDialog("Team8", message, "Yes", "Cancel");
    }
}

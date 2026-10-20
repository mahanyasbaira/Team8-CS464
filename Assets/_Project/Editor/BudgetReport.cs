using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Quest triangle budget. Behind closed doors you only ever see one room plus the shell,
// so the check is: room + building <= 750k triangles. If a room is over, decoy props are
// removed first (fewer objects, not lower quality). Still over = the build fails.
public static class BudgetReport
{
    public const int MaxTriangles = 750000;

    public static Dictionary<string, int> LastReport { get; private set; } = new Dictionary<string, int>();

    [MenuItem("Team8/Check Triangle Budget", priority = 23)]
    public static void Check()
    {
        LastReport = new Dictionary<string, int>();
        int shell = PrefabTriangles(BuildingBuilder.PrefabPath);
        LastReport["Building"] = shell;

        for (int r = 1; r <= 3; r++)
        {
            string path = RoomDresser.RoomFolder + "/Room_" + r + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;

            var room = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int tris = Kit.Triangles(room);
                int removed = 0;
                while (tris + shell > MaxTriangles && RoomDresser.RemoveDecoys(room, 1) == 1)
                {
                    removed++;
                    tris = Kit.Triangles(room);
                }
                if (removed > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(room, path);
                    Debug.LogWarning("[Budget] Room_" + r + ": removed " + removed + " decoy props to fit the budget");
                }
                LastReport["Room_" + r] = tris;
                Debug.Log("[Budget] Room_" + r + ": " + tris + " tris + shell " + shell + " = " + (tris + shell) + " / " + MaxTriangles);
                if (tris + shell > MaxTriangles)
                    Team8Editor.Fail("Room_" + r + " is over the Quest budget (" + (tris + shell) + " triangles). Use lighter kit models.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(room);
            }
        }
    }

    static int PrefabTriangles(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return 0;
        var go = PrefabUtility.LoadPrefabContents(path);
        try { return Kit.Triangles(go); }
        finally { PrefabUtility.UnloadPrefabContents(go); }
    }
}

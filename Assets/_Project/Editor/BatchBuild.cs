using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;

// Entry points for GitHub Actions (Unity in batch mode, no screen). Each one is a separate
// Unity run because adding packages needs a recompile in between:
//   1. BatchBuild.SetupPackages      packages + XRI samples + basic settings
//   2. Team8XRSetup.Configure        OpenXR + Meta Quest for Android (needs step 1 compiled)
//   3. BatchBuild.BuildWorld         rooms -> dressing -> building -> budget -> rig -> Main.unity -> bake
//   4. BatchBuild.BuildApk           Builds/Team8-v<version>.apk
// Pass -team8Rebuild to regenerate the generated rooms and the player rig too.
public static class BatchBuild
{
    const string RigPath = "Assets/_Project/Prefabs/Player/PlayerRig.prefab";
    const string MainPath = "Assets/_Project/Scenes/Main.unity";

    static bool Rebuild => System.Environment.GetCommandLineArgs().Contains("-team8Rebuild");

    public static void SetupPackages()
    {
        ProjectSetup.ApplyBasicSettings();
        ProjectSetup.UseNewInputSystem();

        var request = Client.AddAndRemove(ProjectSetup.PackagesToAdd, null);
        while (!request.IsCompleted) Thread.Sleep(200);
        if (request.Status == StatusCode.Failure)
            throw new System.Exception("[Batch] package install failed: " + request.Error.message);
        Debug.Log("[Batch] packages added: " + string.Join(", ", ProjectSetup.PackagesToAdd));

        ProjectSetup.ImportXriSamples();
        AssetDatabase.SaveAssets();
    }

    public static void BuildWorld()
    {
        if (Rebuild) DeleteGeneratedRooms();

        GreyboxBuilder.Build();
        RoomDresser.DressAll();
        BuildingBuilder.Build();
        BudgetReport.Check();

        if (Rebuild || AssetDatabase.LoadAssetAtPath<GameObject>(RigPath) == null)
            PlayerRigBuilder.Build();

        QuestRenderSettings.Apply();

        // Main.unity is fully generated, so always rebuild it from the prefabs, then bake it
        MainSceneBuilder.Build();
        AssetDatabase.Refresh();
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainPath) == null)
            throw new System.Exception("[Batch] " + MainPath + " was not written");
        LightingBaker.Bake();

        ExportLayout("Builds/layout.json");
        AssetDatabase.SaveAssets();
        Debug.Log("[Batch] world built");
    }

    public static void BuildApk()
    {
        ApkBuilder.Build();
    }

    // Rooms the dresser made (DressedRoom.generated) get regenerated; hand-dressed rooms are kept.
    static void DeleteGeneratedRooms()
    {
        for (int r = 1; r <= 3; r++)
        {
            string path = RoomDresser.RoomFolder + "/Room_" + r + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var mark = prefab != null ? prefab.GetComponent<DressedRoom>() : null;
            if (mark != null && mark.generated)
            {
                AssetDatabase.DeleteAsset(path);
                Debug.Log("[Batch] rebuild: deleted generated " + path);
            }
        }
    }

    // Top-down data for tools/plot_layout.py, read from the finished Main.unity:
    // solid boxes (walls, occluders, doors' walls), lever spots, decoy props, triangle budget.
    static void ExportLayout(string path)
    {
        EditorSceneManager.OpenScene(MainPath, OpenSceneMode.Single);
        var items = new List<string>();

        foreach (var box in Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (box.isTrigger || box.name.StartsWith("Floor") || box.name.StartsWith("Ceiling")) continue;
            if (box.GetComponent<GrabbableProp>() != null) continue;
            var b = box.bounds;
            items.Add(Item("solid", box.transform.root.name, box.name, b.center.x, b.center.z, b.size.x, b.size.z));
        }
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name.StartsWith("LeverSpot_"))
                items.Add(Item("lever_spot", t.root.name, t.name, t.position.x, t.position.z, 0, 0));
        }
        foreach (var p in Object.FindObjectsByType<GrabbableProp>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            items.Add(Item("prop", p.transform.root.name, p.propName, p.transform.position.x, p.transform.position.z, 0, 0));
        }

        var budget = BudgetReport.LastReport.Select(kv =>
            string.Format(CultureInfo.InvariantCulture, "\"{0}\":{1}", kv.Key, kv.Value));
        var sb = new StringBuilder();
        sb.Append("{\"max_triangles\":").Append(BudgetReport.MaxTriangles)
          .Append(",\"triangles\":{").Append(string.Join(",", budget)).Append("}")
          .Append(",\"items\":[").Append(string.Join(",", items)).Append("]}");

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, sb.ToString());
        Debug.Log("[Batch] wrote " + path + " (" + items.Count + " items)");
    }

    static string Item(string kind, string group, string name, float x, float z, float w, float d)
    {
        return string.Format(CultureInfo.InvariantCulture,
            "{{\"kind\":\"{0}\",\"group\":\"{1}\",\"name\":\"{2}\",\"x\":{3:F3},\"z\":{4:F3},\"w\":{5:F3},\"d\":{6:F3}}}",
            kind, group.Replace("\"", ""), name.Replace("\"", ""), x, z, w, d);
    }
}

using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

// Entry points for GitHub Actions (Unity in batch mode, no screen). Each one is a separate
// Unity run because adding packages needs a recompile in between:
//   1. BatchBuild.SetupPackages      packages + XRI samples + basic settings
//   2. Team8XRSetup.Configure        OpenXR + Meta Quest for Android (needs step 1 compiled)
//   3. BatchBuild.BuildWorld         greybox -> dressing -> player rig -> Main.unity, layout.json
//   4. BatchBuild.BuildApk           Builds/Team8-v<version>.apk
// Pass -team8Rebuild to rebuild the rig and Main.unity even if they exist.
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
        GreyboxBuilder.Build();
        RoomDresser.DressAll();

        if (Rebuild || AssetDatabase.LoadAssetAtPath<GameObject>(RigPath) == null)
            PlayerRigBuilder.Build();

        // Main.unity is fully generated, so always rebuild it from the prefabs
        MainSceneBuilder.Build();
        AssetDatabase.Refresh();
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainPath) == null)
            throw new System.Exception("[Batch] " + MainPath + " was not written");

        ExportLayout("Builds/layout.json");
        AssetDatabase.SaveAssets();
        Debug.Log("[Batch] world built");
    }

    public static void BuildApk()
    {
        ApkBuilder.Build();
    }

    // Every solid box and lever spot in the rooms, for the top-down map (tools/plot_layout.py).
    static void ExportLayout(string path)
    {
        var sb = new StringBuilder("{\"boxes\":[");
        bool first = true;
        foreach (var name in new[] { "Room_1", "Room_2", "Room_3" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Rooms/" + name + ".prefab");
            if (prefab == null) continue;
            var room = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            foreach (var box in room.GetComponentsInChildren<BoxCollider>(true))
            {
                if (box.isTrigger) continue;
                var b = box.bounds;
                sb.Append(first ? "" : ",").Append(Json(name, box.name, b.center.x, b.center.z, b.size.x, b.size.z));
                first = false;
            }
            foreach (var t in room.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("LeverSpot_")))
            {
                sb.Append(",").Append(Json(name, t.name, t.position.x, t.position.z, 0, 0));
            }
            Object.DestroyImmediate(room);
        }
        sb.Append("]}");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, sb.ToString());
        Debug.Log("[Batch] wrote " + path);
    }

    static string Json(string room, string name, float x, float z, float w, float d)
    {
        var c = System.Globalization.CultureInfo.InvariantCulture;
        return string.Format(c, "{{\"room\":\"{0}\",\"name\":\"{1}\",\"x\":{2:F3},\"z\":{3:F3},\"w\":{4:F3},\"d\":{5:F3}}}",
            room, name.Replace("\"", ""), x, z, w, d);
    }
}

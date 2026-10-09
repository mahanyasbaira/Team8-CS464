using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.PackageManager.UI;
using UnityEngine;

// Team8 > 1 Setup Project
// One-time setup after the first open: XR packages, XRI samples, input system, Android ids.
// Package versions are left to Unity so it picks the ones that match the editor.
public static class ProjectSetup
{
    const string XriPackage = "com.unity.xr.interaction.toolkit";
    const string AppId = "edu.colostate.cs464.team8";
    const string PendingKey = "Team8.SetupPending";

    static readonly string[] PackagesToAdd = { "com.unity.xr.openxr", "com.unity.xr.meta-openxr", "com.unity.test-framework" };
    static readonly string[] SamplesToImport = { "Starter Assets", "XR Device Simulator" };

    static AddAndRemoveRequest request;

    [MenuItem("Team8/1 Setup Project", priority = 1)]
    public static void Setup()
    {
        PlayerSettings.companyName = "Team8";
        PlayerSettings.productName = "Team8 Locomotion Study";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AppId);
        EditorSettings.serializationMode = SerializationMode.ForceText;
        VersionControlSettings.mode = "Visible Meta Files";

        ImportXriSamples();
        UseNewInputSystem();

        SessionState.SetBool(PendingKey, true);
        request = Client.AddAndRemove(PackagesToAdd, null);
        EditorApplication.update += WaitForPackages;
        Debug.Log("[Setup] installing " + string.Join(", ", PackagesToAdd) + " (takes a minute)");
    }

    static void ImportXriSamples()
    {
        foreach (var sample in Sample.FindByPackage(XriPackage, null))
        {
            if (System.Array.IndexOf(SamplesToImport, sample.displayName) < 0) continue;
            if (sample.isImported)
            {
                Debug.Log("[Setup] sample already imported: " + sample.displayName);
                continue;
            }
            bool ok = sample.Import(Sample.ImportOptions.None);
            Debug.Log("[Setup] import " + sample.displayName + ": " + (ok ? "ok" : "FAILED"));
        }
    }

    // Player Settings > Active Input Handling = Input System Package (New). Needs a restart.
    static void UseNewInputSystem()
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
        if (assets == null || assets.Length == 0) return;
        var so = new SerializedObject(assets[0]);
        var prop = so.FindProperty("activeInputHandler");
        if (prop == null || prop.intValue == 1) return;
        prop.intValue = 1;
        so.ApplyModifiedProperties();
        Debug.Log("[Setup] input handling set to the new Input System (restart needed)");
    }

    static void WaitForPackages()
    {
        if (request == null || !request.IsCompleted) return;
        EditorApplication.update -= WaitForPackages;
        if (request.Status == StatusCode.Failure)
        {
            SessionState.SetBool(PendingKey, false);
            EditorUtility.DisplayDialog("Team8 setup", "Package install failed:\n" + request.Error.message, "OK");
        }
        // on success Unity recompiles and reloads, and Finish() runs after the reload
    }

    [InitializeOnLoadMethod]
    static void FinishAfterReload()
    {
        if (!SessionState.GetBool(PendingKey, false)) return;
        // wait until the editor is idle after the reload
        EditorApplication.delayCall += Finish;
    }

    static void Finish()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += Finish;
            return;
        }
        if (!SessionState.GetBool(PendingKey, false)) return;
        SessionState.SetBool(PendingKey, false);

        SettingsService.OpenProjectSettings("Project/XR Plug-in Management");
        bool restart = EditorUtility.DisplayDialog("Team8 setup",
            "Packages and samples are installed.\n\n" +
            "In the XR Plug-in Management window that just opened:\n" +
            "  1. Android tab: tick OpenXR\n" +
            "  2. Tick the Meta Quest feature group\n" +
            "  3. Project Validation: click Fix All\n\n" +
            "Unity needs a restart for the new Input System. Restart now?",
            "Restart", "Later");
        if (restart) EditorApplication.OpenProject(Directory.GetCurrentDirectory());
    }
}

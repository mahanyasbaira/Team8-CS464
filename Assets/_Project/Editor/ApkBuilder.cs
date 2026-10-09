using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Team8 > 5 Build APK
// Quest 3 settings + build to Builds/Team8-v<version>.apk (issue #17).
// Bump Player Settings > Version before every build you hand to Ashley.
public static class ApkBuilder
{
    const string MainPath = "Assets/_Project/Scenes/Main.unity";

    [MenuItem("Team8/5 Build APK", priority = 5)]
    public static void Build()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
        {
            EditorUtility.DisplayDialog("Team8", "Android Build Support isn't installed.\nUnity Hub > Installs > this version > Add modules > Android Build Support (with OpenJDK and SDK/NDK).", "OK");
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainPath) == null)
        {
            EditorUtility.DisplayDialog("Team8", "No Main.unity yet. Run Team8 > 4 Build Main Scene first.", "OK");
            return;
        }

        ApplyQuestSettings();
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        Directory.CreateDirectory("Builds");
        string apk = "Builds/Team8-v" + PlayerSettings.bundleVersion + ".apk";
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { MainPath },
            locationPathName = apk,
            target = BuildTarget.Android,
            options = BuildOptions.None
        });

        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log("[Build] " + apk + " (" + report.summary.totalSize / (1024 * 1024) + " MB)");
            EditorUtility.RevealInFinder(apk);
        }
        else
        {
            EditorUtility.DisplayDialog("Team8", "Build " + report.summary.result + ". See the Console for errors.", "OK");
        }
    }

    [MenuItem("Team8/Apply Quest Settings", priority = 20)]
    public static void ApplyQuestSettings()
    {
        var android = NamedBuildTarget.Android;
        PlayerSettings.SetApplicationIdentifier(android, "edu.colostate.cs464.team8");
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
        PlayerSettings.Android.bundleVersionCode++;
        if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "1.0")
            PlayerSettings.bundleVersion = "0.1.0";
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainPath, true) };
        Debug.Log("[Build] Quest settings applied, version " + PlayerSettings.bundleVersion +
                  " (code " + PlayerSettings.Android.bundleVersionCode + ")");
    }
}

using System.Linq;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

// Team8 > Configure XR: OpenXR loader + Meta Quest support + Touch controller profiles for Android.
// Same as ticking the boxes in Project Settings > XR Plug-in Management by hand.
// Lives in its own assembly that only compiles once the OpenXR package is installed.
public static class Team8XRSetup
{
    const BuildTargetGroup Android = BuildTargetGroup.Android;

    static readonly string[] FeaturesToEnable =
    {
        "MetaQuestFeature",                    // Meta Quest support (Android)
        "OculusTouchControllerProfile",        // Quest controllers
        "MetaQuestTouchPlusControllerProfile", // Quest 3 Touch Plus controllers
    };

    [MenuItem("Team8/Configure XR (OpenXR + Meta Quest)", priority = 21)]
    public static void Configure()
    {
        var perTarget = GetOrCreatePerTarget();
        if (!perTarget.HasSettingsForBuildTarget(Android)) perTarget.CreateDefaultSettingsForBuildTarget(Android);
        if (!perTarget.HasManagerSettingsForBuildTarget(Android)) perTarget.CreateDefaultManagerSettingsForBuildTarget(Android);

        var general = perTarget.SettingsForBuildTarget(Android);
        general.InitManagerOnStart = true;
        var manager = perTarget.ManagerSettingsForBuildTarget(Android);

        bool hasLoader = manager.activeLoaders.Any(l => l != null && l.GetType().Name == "OpenXRLoader");
        if (!hasLoader && !XRPackageMetadataStore.AssignLoader(manager, "UnityEngine.XR.OpenXR.OpenXRLoader", Android))
            throw new System.Exception("[XR] couldn't assign the OpenXR loader for Android");

        FeatureHelpers.RefreshFeatures(Android);
        var openxr = OpenXRSettings.GetSettingsForBuildTargetGroup(Android);
        foreach (var feature in openxr.GetFeatures())
        {
            if (System.Array.IndexOf(FeaturesToEnable, feature.GetType().Name) < 0) continue;
            feature.enabled = true;
            EditorUtility.SetDirty(feature);
            Debug.Log("[XR] enabled " + feature.GetType().Name);
        }

        EditorUtility.SetDirty(openxr);
        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(general);
        AssetDatabase.SaveAssets();
        Debug.Log("[XR] OpenXR configured for Android");
    }

    static XRGeneralSettingsPerBuildTarget GetOrCreatePerTarget()
    {
        XRGeneralSettingsPerBuildTarget perTarget;
        EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out perTarget);
        if (perTarget != null) return perTarget;

        if (!AssetDatabase.IsValidFolder("Assets/XR")) AssetDatabase.CreateFolder("Assets", "XR");
        perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
        AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
        EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
        return perTarget;
    }
}

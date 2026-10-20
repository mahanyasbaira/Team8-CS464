using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Team8 > 4b Bake Lighting
// Baked lighting for Quest: an area light under every LightPanel (from BuildingBuilder),
// light probes for moving things (props, hands, lever handles), one box-projected reflection
// probe per space, then lightmaps (Progressive CPU, works in CI) and occlusion culling.
// No light casts real-time shadows (the budget allows at most one; indoors under a roof a
// real-time sun shadow costs GPU time and lights nothing).
public static class LightingBaker
{
    const string MainPath = "Assets/_Project/Scenes/Main.unity";
    const string SettingsPath = "Assets/_Project/Scenes/Main_Lighting.lighting";

    // spaces that get probes: (centre x, centre z, size x, size z)
    static readonly Vector4[] Spaces =
    {
        new Vector4(4, 4, 8, 8), new Vector4(12, 4, 8, 8), new Vector4(20, 4, 8, 8), // rooms
        new Vector4(12, -1.5f, 30, 2.8f),   // corridor
        new Vector4(-1.5f, 4, 2.8f, 8),     // hall
        new Vector4(25.5f, 4, 2.8f, 8),     // east wing
        new Vector4(BuildingBuilder.LobbyOrigin.x + 3, BuildingBuilder.LobbyOrigin.z + 3, 6, 6),
        new Vector4(BuildingBuilder.PracticeOrigin.x + 2.5f, BuildingBuilder.PracticeOrigin.z + 2.5f, 5, 5),
    };

    [MenuItem("Team8/4b Bake Lighting", priority = 4)]
    public static void Bake()
    {
        if (!Team8Editor.Batch && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(MainPath, OpenSceneMode.Single);
        QuestRenderSettings.Apply();

        var old = GameObject.Find("BakedLighting");
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("BakedLighting").transform;

        // existing directional light: dim fill, baked only, no real-time shadows
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l.type != LightType.Directional) continue;
            l.lightmapBakeType = LightmapBakeType.Baked;
            l.intensity = 0.2f;
            l.shadows = LightShadows.Soft; // only affects the bake
        }

        // one baked rectangle light under every light panel, pointing down
        int panels = 0;
        foreach (var panel in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                     .Where(t => t.name.StartsWith("LightPanel_")).ToList())
        {
            var go = new GameObject("Area_" + panel.name);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(panel.position + Vector3.down * 0.03f, Quaternion.Euler(90, panel.eulerAngles.y, 0));
            var light = go.AddComponent<Light>();
            light.type = LightType.Rectangle;
            light.areaSize = new Vector2(1.2f, 0.3f);
            light.color = new Color(1f, 0.95f, 0.86f);
            light.intensity = 8f;
            light.lightmapBakeType = LightmapBakeType.Baked;
            panels++;
        }

        // light probes + reflection probes per space
        foreach (var s in Spaces)
        {
            var probes = new GameObject("LightProbes").AddComponent<LightProbeGroup>();
            probes.transform.SetParent(root, false);
            probes.probePositions = ProbeGrid(s).ToArray();

            var refl = new GameObject("ReflectionProbe").AddComponent<ReflectionProbe>();
            refl.transform.SetParent(root, false);
            refl.transform.position = new Vector3(s.x, 1.5f, s.y);
            refl.mode = ReflectionProbeMode.Baked;
            refl.boxProjection = true;
            refl.size = new Vector3(s.z, 3f, s.w);
            refl.resolution = 128;
            refl.hdr = false;
        }

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.12f, 0.12f, 0.13f);
        RenderSettings.skybox = null;

        var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(SettingsPath);
        if (settings == null)
        {
            settings = new LightingSettings();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        settings.bakedGI = true;
        settings.realtimeGI = false;
        settings.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
        settings.lightmapResolution = 10;
        settings.lightmapMaxSize = 1024;
        settings.directionalityMode = LightmapsMode.NonDirectional;
        settings.lightmapCompression = LightmapCompression.NormalQuality;
        settings.ao = true;
        settings.aoMaxDistance = 1f;
        settings.mixedBakeMode = MixedLightingMode.Subtractive;
        EditorUtility.SetDirty(settings);
        Lightmapping.lightingSettings = settings;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("[Light] baking " + panels + " area lights (this takes a while on CPU)");
        if (!Lightmapping.Bake()) Team8Editor.Fail("Lightmap bake failed, see the Console");

        StaticOcclusionCulling.smallestOccluder = 0.5f;
        StaticOcclusionCulling.Compute();

        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Light] baked lightmaps + occlusion culling for " + MainPath);
    }

    static IEnumerable<Vector3> ProbeGrid(Vector4 s)
    {
        float x0 = s.x - s.z / 2 + 0.4f, x1 = s.x + s.z / 2 - 0.4f;
        float z0 = s.y - s.w / 2 + 0.4f, z1 = s.y + s.w / 2 - 0.4f;
        int nx = Mathf.Max(2, Mathf.CeilToInt((x1 - x0) / 2f) + 1);
        int nz = Mathf.Max(2, Mathf.CeilToInt((z1 - z0) / 2f) + 1);
        foreach (float y in new[] { 0.5f, 1.7f })
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                    yield return new Vector3(Mathf.Lerp(x0, x1, i / (nx - 1f)), y, Mathf.Lerp(z0, z1, j / (nz - 1f)));
    }
}

// URP asset tuned for Quest: 4x MSAA, no HDR, no extra camera textures, SRP Batcher,
// one shadow cascade at most.
public static class QuestRenderSettings
{
    [MenuItem("Team8/Apply Quest Render Settings", priority = 22)]
    public static void Apply()
    {
        var urp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null)
        {
            Debug.LogWarning("[Render] no URP asset assigned yet (Team8 > 2 Build Greybox creates one)");
            return;
        }
        urp.msaaSampleCount = 4;
        urp.supportsHDR = false;
        urp.supportsCameraDepthTexture = false;
        urp.supportsCameraOpaqueTexture = false;
        urp.useSRPBatcher = true;
        urp.shadowCascadeCount = 1;
        urp.shadowDistance = 15f;
        EditorUtility.SetDirty(urp);
        AssetDatabase.SaveAssets();
        Debug.Log("[Render] URP asset set up for Quest");
    }
}

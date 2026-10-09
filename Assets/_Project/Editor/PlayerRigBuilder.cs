using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

// Team8 > 3 Build Player Rig
// Makes Prefabs/Player/PlayerRig.prefab as a variant of the XRI Starter Assets
// "XR Origin (XR Rig)" and wires our scripts onto it (issues #9 and #10).
// Scene references (room zones, grid origin) are set by Team8 > 4 Build Main Scene.
public static class PlayerRigBuilder
{
    const string PlayerFolder = "Assets/_Project/Prefabs/Player";
    const string RigPath = PlayerFolder + "/PlayerRig.prefab";
    const string MarkerPath = PlayerFolder + "/TrailMarker.prefab";
    const string MatFolder = "Assets/_Project/Materials";

    [MenuItem("Team8/3 Build Player Rig", priority = 3)]
    public static void Build()
    {
        var source = FindStarterRig();
        if (source == null)
        {
            EditorUtility.DisplayDialog("Team8", "Can't find the Starter Assets 'XR Origin (XR Rig)' prefab.\nRun Team8 > 1 Setup Project first.", "OK");
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(RigPath) != null &&
            !EditorUtility.DisplayDialog("Team8", "PlayerRig.prefab already exists. Rebuild it?", "Rebuild", "Cancel"))
            return;

        EnsureFolder(PlayerFolder);
        EnsureFolder(MatFolder);
        var markerPrefab = MakeMarkerPrefab();

        var rig = (GameObject)PrefabUtility.InstantiatePrefab(source);
        rig.name = "PlayerRig";
        try
        {
            Wire(rig, markerPrefab);
            PrefabUtility.SaveAsPrefabAsset(rig, RigPath);
            Debug.Log("[Rig] saved " + RigPath);
        }
        finally
        {
            Object.DestroyImmediate(rig);
        }
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
    }

    static GameObject FindStarterRig()
    {
        foreach (var guid in AssetDatabase.FindAssets("XR Origin t:Prefab"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith("/XR Origin (XR Rig).prefab") && path.Contains("Starter Assets"))
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        return null;
    }

    static void Wire(GameObject rig, GameObject markerPrefab)
    {
        var origin = rig.GetComponent<XROrigin>();
        var head = rig.GetComponentInChildren<Camera>(true).transform;
        var move = rig.GetComponentInChildren<ContinuousMoveProvider>(true);
        var teleport = rig.GetComponentInChildren<TeleportationProvider>(true);
        var snap = rig.GetComponentInChildren<SnapTurnProvider>(true);

        // the hand managers live in the Starter Assets sample, so find them by type name
        var managers = rig.GetComponentsInChildren<MonoBehaviour>(true)
            .Where(m => m != null && m.GetType().Name == "ControllerInputActionManager").ToArray();
        var leftManager = managers.FirstOrDefault(m => IsLeft(m.transform));
        var rightManager = managers.FirstOrDefault(m => !IsLeft(m.transform));

        var teleportRays = rig.GetComponentsInChildren<XRRayInteractor>(true)
            .Where(r => r.name.Contains("Teleport")).ToArray();
        var leftRay = teleportRays.FirstOrDefault(r => IsLeft(r.transform));
        var rightRay = teleportRays.FirstOrDefault(r => !IsLeft(r.transform));

        Require(origin, "XROrigin");
        Require(move, "ContinuousMoveProvider");
        Require(teleport, "TeleportationProvider");
        Require(snap, "SnapTurnProvider");
        Require(leftManager, "left ControllerInputActionManager");
        Require(rightManager, "right ControllerInputActionManager");

        // turning is the same in every condition: right stick, 45 degree snaps
        snap.turnAmount = 45f;
        snap.leftHandTurnInput.inputSourceMode = XRInputValueReader.InputSourceMode.Unused;
        Record(snap);

        // only the left stick moves
        move.rightHandMoveInput.inputSourceMode = XRInputValueReader.InputSourceMode.Unused;
        move.moveSpeed = 1.5f;
        Record(move);

        // comfort vignette off in all conditions so it doesn't hide joystick sickness
        foreach (var m in rig.GetComponentsInChildren<MonoBehaviour>(true))
            if (m != null && m.GetType().Name == "TunnelingVignetteController")
                m.gameObject.SetActive(false);

        // the body needs a collider so walls and gates stop joystick movement
        if (rig.GetComponent<CharacterController>() == null)
        {
            var cc = rig.AddComponent<CharacterController>();
            cc.radius = 0.2f;
            cc.height = 1.6f;
            cc.center = new Vector3(0, 0.8f, 0);
        }

        // trail
        var trailGo = new GameObject("TeleportTrail");
        trailGo.transform.SetParent(rig.transform, false);
        var line = trailGo.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 0;
        line.widthMultiplier = 0.04f;
        line.numCapVertices = 2;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.alignment = LineAlignment.TransformZ;
        trailGo.transform.rotation = Quaternion.Euler(90, 0, 0); // lie flat on the floor
        line.sharedMaterial = UnlitMat("TrailLine", new Color(0.1f, 0.85f, 0.95f));

        var trail = trailGo.AddComponent<TeleportTrail>();
        trail.teleportProvider = teleport;
        trail.head = head;
        trail.rigOrigin = rig.transform;
        trail.markerPrefab = markerPrefab;
        trail.line = line;

        var switcher = rig.AddComponent<LocomotionSwitcher>();
        switcher.leftHand = leftManager;
        switcher.rightHand = rightManager;
        switcher.moveProvider = move;
        switcher.teleportProvider = teleport;
        switcher.leftTeleportInteractor = leftRay;
        switcher.rightTeleportInteractor = rightRay;
        switcher.trail = trail;

        var logger = rig.AddComponent<PositionLogger>();
        logger.head = head;
        logger.teleportProvider = teleport;
    }

    static GameObject MakeMarkerPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(MarkerPath);
        if (existing != null) return existing;

        var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "TrailMarker";
        Object.DestroyImmediate(marker.GetComponent<Collider>());
        marker.transform.localScale = new Vector3(0.25f, 0.005f, 0.25f);
        var r = marker.GetComponent<MeshRenderer>();
        r.sharedMaterial = UnlitMat("TrailMarker", new Color(0.1f, 0.85f, 0.95f));
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        var prefab = PrefabUtility.SaveAsPrefabAsset(marker, MarkerPath);
        Object.DestroyImmediate(marker);
        return prefab;
    }

    static Material UnlitMat(string name, Color color)
    {
        string path = MatFolder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        mat = new Material(shader) { color = color, enableInstancing = true };
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static bool IsLeft(Transform t)
    {
        for (; t != null; t = t.parent)
        {
            if (t.name.Contains("Left")) return true;
            if (t.name.Contains("Right")) return false;
        }
        return false;
    }

    static void Record(Object o)
    {
        EditorUtility.SetDirty(o);
        PrefabUtility.RecordPrefabInstancePropertyModifications(o);
    }

    static void Require(Object o, string what)
    {
        if (o == null) throw new System.Exception("[Rig] couldn't find " + what + " on the Starter Assets rig");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}

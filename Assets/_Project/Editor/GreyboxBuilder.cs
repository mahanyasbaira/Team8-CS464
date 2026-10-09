using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

// Team8 > 2 Build Greybox
// Builds a plain greybox of the study world (3 rooms, practice room, lobby) as prefabs
// plus a sandbox scene, so we can see the layout before the real rooms are dressed.
// Existing room prefabs are never overwritten, so it's safe to run again.
//
// Layout (metres, matches docs/PLAN.md 2.4 and the analysis path plots):
//   Room 1: x 0-8, Room 2: x 8-16, Room 3: x 16-24, all z 0-8. Exit past x = 24.
//   Practice room at z = -20, lobby at z = -40.
public static class GreyboxBuilder
{
    const string Root = "Assets/_Project";
    const string MatFolder = Root + "/Materials/Greybox";
    const string RoomFolder = Root + "/Prefabs/Rooms";
    const string ScenePath = Root + "/Scenes/Sandbox/Sandbox_Greybox.unity";

    const float RoomSize = 8f;
    const float WallHeight = 3f;
    const float WallThick = 0.2f;
    const float DoorWidth = 1.5f;

    // lever spots per room for layouts A, B, C (local x, z)
    static readonly Vector2[][] LeverSpots =
    {
        new[] { new Vector2(6.5f, 6.5f), new Vector2(1.5f, 1.5f), new Vector2(6.0f, 1.5f) }, // room 1
        new[] { new Vector2(1.5f, 6.5f), new Vector2(6.5f, 6.0f), new Vector2(1.5f, 1.5f) }, // room 2
        new[] { new Vector2(6.5f, 1.5f), new Vector2(1.5f, 6.5f), new Vector2(6.5f, 6.5f) }, // room 3
    };

    // extra props per room: x, z, width, height, depth, y rotation
    static readonly float[][][] Props =
    {
        new[] { new[] { 3.5f, 2.5f, 1f, 1.2f, 1f, 0f }, new[] { 4.5f, 5.5f, 2f, 2f, 0.5f, 0f } },
        new[] { new[] { 4f, 4f, 0.6f, 3f, 0.6f, 0f }, new[] { 2.5f, 3f, 1f, 1.2f, 1f, 20f }, new[] { 5.5f, 5f, 1f, 1.2f, 1f, 0f } },
        new[] { new[] { 3f, 4.5f, 2f, 2f, 0.5f, 90f }, new[] { 5.5f, 3f, 1f, 1.2f, 1f, 0f } },
    };

    static Material floorMat, wallMat, propMat, gateMat, leverMat, exitMat;

    [MenuItem("Team8/2 Build Greybox", priority = 2)]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureFolder(MatFolder);
        EnsureFolder(RoomFolder);
        EnsureFolder(Root + "/Scenes/Sandbox");
        EnsureUrp();
        MakeMaterials();

        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        GameObject first = null;
        for (int r = 1; r <= 3; r++)
        {
            var room = PlaceOrBuild("Room_" + r, () => BuildRoom(r));
            if (first == null) first = room;
        }
        PlaceOrBuild("PracticeRoom", BuildPracticeRoom);
        PlaceOrBuild("Lobby", BuildLobby);

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = first;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        Debug.Log("[Greybox] done. Scene: " + ScenePath);
    }

    // Use the existing prefab if someone already made it, otherwise build and save a new one.
    static GameObject PlaceOrBuild(string name, System.Func<GameObject> build)
    {
        string path = RoomFolder + "/" + name + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            Debug.Log("[Greybox] " + name + " already exists, keeping it");
            return (GameObject)PrefabUtility.InstantiatePrefab(existing);
        }
        var go = build();
        PrefabUtility.SaveAsPrefabAssetAndConnect(go, path, InteractionMode.AutomatedAction);
        return go;
    }

    // ---------- rooms ----------

    static GameObject BuildRoom(int r)
    {
        var room = new GameObject("Room_" + r);
        room.transform.position = new Vector3((r - 1) * RoomSize, 0, 0);
        var t = room.transform;

        Floor(t, new Vector3(RoomSize / 2, 0, RoomSize / 2), new Vector2(RoomSize, RoomSize));

        // south + north walls, west wall only on room 1 (other rooms share the previous room's east wall)
        Wall(t, "Wall_S", new Vector3(RoomSize / 2, 0, 0), new Vector3(RoomSize + WallThick, WallHeight, WallThick));
        Wall(t, "Wall_N", new Vector3(RoomSize / 2, 0, RoomSize), new Vector3(RoomSize + WallThick, WallHeight, WallThick));
        if (r == 1) Wall(t, "Wall_W", new Vector3(0, 0, RoomSize / 2), new Vector3(WallThick, WallHeight, RoomSize));

        // east wall with a doorway in the middle
        float side = (RoomSize - DoorWidth) / 2;
        Wall(t, "Wall_E_1", new Vector3(RoomSize, 0, side / 2), new Vector3(WallThick, WallHeight, side));
        Wall(t, "Wall_E_2", new Vector3(RoomSize, 0, RoomSize - side / 2), new Vector3(WallThick, WallHeight, side));

        if (r < 3) Gate(t, "Gate_" + r, new Vector3(RoomSize, 0, RoomSize / 2), gateMat);
        else Exit(t);

        // lever spots, each hidden behind a partition facing the room centre
        var center = new Vector2(RoomSize / 2, RoomSize / 2);
        var letters = new[] { "A", "B", "C" };
        Transform spotA = null;
        for (int i = 0; i < 3; i++)
        {
            var p = LeverSpots[r - 1][i];
            var dir = (center - p).normalized;
            var facing = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.y));

            var spot = new GameObject("LeverSpot_" + letters[i]).transform;
            spot.SetParent(t, false);
            spot.localPosition = new Vector3(p.x, 0, p.y);
            spot.localRotation = facing;
            if (i == 0) spotA = spot;

            var partitionPos = new Vector3(p.x + dir.x * 0.9f, 0, p.y + dir.y * 0.9f);
            var part = Box(t, "Partition_" + letters[i], partitionPos, new Vector3(1.6f, 1.6f, 0.15f), propMat);
            part.transform.localRotation = facing;
        }

        // decoy props
        int n = 0;
        foreach (var pr in Props[r - 1])
        {
            var prop = Box(t, "Prop_" + (++n), new Vector3(pr[0], 0, pr[1]), new Vector3(pr[2], pr[3], pr[4]), propMat);
            prop.transform.localRotation = Quaternion.Euler(0, pr[5], 0);
        }

        var lever = Lever(t, r);
        lever.transform.localPosition = spotA.localPosition;
        lever.transform.localRotation = spotA.localRotation;

        Zone(t, r);

        if (r == 1)
        {
            var spawn = new GameObject("StartSpawn").transform;
            spawn.SetParent(t, false);
            spawn.localPosition = new Vector3(0.8f, 0, RoomSize / 2);
            spawn.localRotation = Quaternion.LookRotation(Vector3.right);
        }
        return room;
    }

    static void Exit(Transform room)
    {
        var exitRoot = new GameObject("ExitDoor");
        exitRoot.transform.SetParent(room, false);
        exitRoot.transform.localPosition = new Vector3(RoomSize, 0, RoomSize / 2);
        var gate = AddGate(exitRoot, exitMat);
        var exit = exitRoot.AddComponent<ExitDoor>();
        exit.gate = gate;

        // small landing outside the door so you can walk/teleport out
        Floor(room, new Vector3(RoomSize + 1.25f, 0, RoomSize / 2), new Vector2(2.5f, 3f), exitMat);

        var zone = new GameObject("ExitZone");
        zone.layer = 2; // Ignore Raycast, so it never blocks the teleport arc
        zone.transform.SetParent(exitRoot.transform, false);
        zone.transform.localPosition = new Vector3(1.25f, WallHeight / 2, 0);
        var box = zone.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(2f, WallHeight, 3f);
        exit.exitZone = box;
    }

    static void Gate(Transform room, string name, Vector3 pos, Material mat)
    {
        var g = new GameObject(name);
        g.transform.SetParent(room, false);
        g.transform.localPosition = pos;
        AddGate(g, mat);
    }

    static GateController AddGate(GameObject g, Material mat)
    {
        var door = Box(g.transform, "Door", Vector3.zero, new Vector3(0.15f, WallHeight, DoorWidth), mat);
        var gc = g.AddComponent<GateController>();
        gc.door = door.transform;
        gc.blockers = new Collider[] { door.GetComponent<Collider>() };
        gc.openHeight = WallHeight;
        return gc;
    }

    static GameObject Lever(Transform parent, int roomIndex)
    {
        var lever = new GameObject(roomIndex == 0 ? "PracticeLever" : "Lever_" + roomIndex);
        lever.transform.SetParent(parent, false);
        Box(lever.transform, "Post", Vector3.zero, new Vector3(0.15f, 1.0f, 0.15f), leverMat);

        var pivot = new GameObject("Handle").transform;
        pivot.SetParent(lever.transform, false);
        pivot.localPosition = new Vector3(0, 1.05f, 0);
        var stick = Box(pivot, "Stick", Vector3.zero, new Vector3(0.06f, 0.35f, 0.06f), leverMat);
        stick.transform.localPosition = new Vector3(0, 0.17f, 0);
        var knob = Box(pivot, "Knob", Vector3.zero, new Vector3(0.12f, 0.12f, 0.12f), leverMat);
        knob.transform.localPosition = new Vector3(0, 0.36f, 0);

        // LeverController requires an XRSimpleInteractable, which gets added with it
        var lc = lever.AddComponent<LeverController>();
        lc.roomIndex = roomIndex;
        lc.handle = pivot;
        return lever;
    }

    static void Zone(Transform room, int index)
    {
        var zone = new GameObject("RoomZone");
        zone.layer = 2;
        zone.transform.SetParent(room, false);
        zone.transform.localPosition = new Vector3(RoomSize / 2, WallHeight / 2, RoomSize / 2);
        var box = zone.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(RoomSize, WallHeight, RoomSize);
        zone.AddComponent<RoomZone>().roomIndex = index;
    }

    // ---------- practice room + lobby ----------

    static GameObject BuildPracticeRoom()
    {
        var room = SimpleRoom("PracticeRoom", new Vector3(0, 0, -20), 5f);
        var lever = Lever(room.transform, 0);
        lever.transform.localPosition = new Vector3(4f, 0, 2.5f);
        lever.transform.localRotation = Quaternion.LookRotation(Vector3.left);
        Marker(room.transform, "PracticeSpawn", new Vector3(1f, 0, 2.5f), Vector3.right);
        Marker(room.transform, "StartButtonSpot", new Vector3(2.5f, 1.2f, 4.5f), Vector3.back);
        return room;
    }

    static GameObject BuildLobby()
    {
        var room = SimpleRoom("Lobby", new Vector3(0, 0, -40), 6f);
        Marker(room.transform, "LobbySpawn", new Vector3(3f, 0, 1.5f), Vector3.forward);
        Marker(room.transform, "OperatorPanelSpot", new Vector3(3f, 1.5f, 5.5f), Vector3.back);
        return room;
    }

    static GameObject SimpleRoom(string name, Vector3 pos, float size)
    {
        var room = new GameObject(name);
        room.transform.position = pos;
        var t = room.transform;
        Floor(t, new Vector3(size / 2, 0, size / 2), new Vector2(size, size));
        Wall(t, "Wall_S", new Vector3(size / 2, 0, 0), new Vector3(size + WallThick, WallHeight, WallThick));
        Wall(t, "Wall_N", new Vector3(size / 2, 0, size), new Vector3(size + WallThick, WallHeight, WallThick));
        Wall(t, "Wall_W", new Vector3(0, 0, size / 2), new Vector3(WallThick, WallHeight, size));
        Wall(t, "Wall_E", new Vector3(size, 0, size / 2), new Vector3(WallThick, WallHeight, size));
        return room;
    }

    static void Marker(Transform parent, string name, Vector3 localPos, Vector3 forward)
    {
        var m = new GameObject(name).transform;
        m.SetParent(parent, false);
        m.localPosition = localPos;
        m.localRotation = Quaternion.LookRotation(forward);
    }

    // ---------- building blocks ----------

    static void Floor(Transform parent, Vector3 center, Vector2 size, Material mat = null)
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(parent, false);
        floor.transform.localPosition = new Vector3(center.x, -0.05f, center.z);
        floor.transform.localScale = new Vector3(size.x, 0.1f, size.y);
        floor.GetComponent<Renderer>().sharedMaterial = mat != null ? mat : floorMat;
        GameObjectUtility.SetStaticEditorFlags(floor, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);

        var area = floor.AddComponent<TeleportationArea>();
        // XRI Starter Assets teleport on interaction layer 31 ("Teleport")
        area.interactionLayers = 1 << 31;
    }

    static void Wall(Transform parent, string name, Vector3 basePos, Vector3 size)
    {
        var w = Box(parent, name, basePos, size, wallMat);
        GameObjectUtility.SetStaticEditorFlags(w, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
    }

    // cube whose bottom sits at basePos.y
    static GameObject Box(Transform parent, string name, Vector3 basePos, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = basePos + new Vector3(0, size.y / 2, 0);
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    // ---------- assets ----------

    // The project starts without a URP asset, so materials would render pink. Make one if needed.
    static void EnsureUrp()
    {
        if (GraphicsSettings.defaultRenderPipeline != null) return;

        EnsureFolder(Root + "/Settings");
        string assetPath = Root + "/Settings/URP_Quest.asset";
        var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
        if (asset == null)
        {
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, Root + "/Settings/URP_Quest_Renderer.asset");
            asset = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(asset, assetPath);
        }
        GraphicsSettings.defaultRenderPipeline = asset;
        Debug.Log("[Greybox] using render pipeline asset " + assetPath);
    }

    static void MakeMaterials()
    {
        floorMat = Mat("Floor", new Color(0.55f, 0.55f, 0.55f));
        wallMat = Mat("Wall", new Color(0.85f, 0.85f, 0.82f));
        propMat = Mat("Prop", new Color(0.55f, 0.42f, 0.30f));
        gateMat = Mat("Gate", new Color(0.75f, 0.25f, 0.20f));
        leverMat = Mat("Lever", new Color(0.95f, 0.75f, 0.10f));
        exitMat = Mat("Exit", new Color(0.20f, 0.70f, 0.40f));
    }

    static Material Mat(string name, Color color)
    {
        string path = MatFolder + "/Greybox_" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        mat = new Material(shader) { color = color, enableInstancing = true };
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}

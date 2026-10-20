using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

// Team8 > 2c Build Building
// Wraps the three rooms, the lobby and the practice room into one building:
// corridors, a front hall, closed doors, ceilings and a roof. It's a separate prefab, so the
// room prefabs (their colliders and lever spots) are never touched. Doors stay closed; the
// TrialManager still teleports the player between lobby, practice and the start.
//
// Plan (metres, y up):            z=8 +----------------------------------------+
//   lobby  x -9.2..-3.2, z 2.2..8.2   |hall| Room_1 | Room_2 | Room_3 |east|
//   practice x -8.2..-3.2, z -3.2..1.8 |    |  0..8  | 8..16  | 16..24 | 24-27
//   hall   x -3..0,  z -3..8      z=0 +----+--------+--------+--------+    |
//   corridor x -3..27, z -3..0         |          south corridor           |
//   east wing x 24..27, z -3..8   z=-3 +----------------------------------------+
// Ceilings have colliders on purpose: a teleport arc that goes up into the ceiling is just
// invalid, so nobody can teleport over a 3 m wall into the corridor. The roof has none.
public static class BuildingBuilder
{
    public const string PrefabPath = "Assets/_Project/Prefabs/Building/Building.prefab";
    public static readonly Vector3 LobbyOrigin = new Vector3(-9.2f, 0, 2.2f);
    public static readonly Vector3 PracticeOrigin = new Vector3(-8.2f, 0, -3.2f);

    const float H = 3f;        // ceiling height, same as the rooms
    const float T = 0.2f;      // wall thickness
    const float FloorY = -0.005f; // a hair below the room floors so overlaps never z-fight

    static Material floorMat, wallMat, ceilingMat, roofMat, doorMat, frameMat, panelMat;

    [MenuItem("Team8/2c Build Building", priority = 2)]
    public static void Build()
    {
        Kit.Load();
        floorMat = Kit.Surface("Corridor_Floor", "floor_concrete", "floor_wood", new Vector2(2, 8), new Color(0.45f, 0.45f, 0.44f));
        wallMat = Kit.Surface("Corridor_Wall", "wall_plaster", "wall_concrete", new Vector2(6, 1.5f), new Color(0.85f, 0.84f, 0.80f));
        ceilingMat = Kit.Surface("Ceiling", "ceiling", "wall_plaster", new Vector2(6, 6), new Color(0.9f, 0.9f, 0.9f));
        roofMat = Kit.Surface("Exterior", "exterior", "wall_concrete", new Vector2(10, 4), new Color(0.5f, 0.48f, 0.45f));
        frameMat = Kit.Flat("DoorFrame", new Color(0.25f, 0.25f, 0.27f));
        doorMat = Kit.Flat("DoorPanel", new Color(0.42f, 0.30f, 0.20f));
        panelMat = Kit.Emissive("LightPanel", new Color(1f, 0.96f, 0.88f));

        var root = new GameObject("Building");
        var t = root.transform;

        // floors (walkable, teleportable) for the parts outside the rooms
        Floor(t, "Floor_Corridor", new Vector3(12, 0, -1.55f), new Vector2(30, 2.9f));
        Floor(t, "Floor_Hall", new Vector3(-1.5f, 0, 3.95f), new Vector2(3, 8.1f));
        Floor(t, "Floor_East", new Vector3(25.55f, 0, 3.95f), new Vector2(2.9f, 8.1f));

        // outer walls
        Wall(t, "Wall_South", new Vector3(12, 0, -3), new Vector3(30 + T, H, T));
        Wall(t, "Wall_East", new Vector3(27, 0, 2.5f), new Vector3(T, H, 11));
        Wall(t, "Wall_North_Hall", new Vector3(-1.5f, 0, 8), new Vector3(3, H, T));
        Wall(t, "Wall_North_East", new Vector3(25.5f, 0, 8), new Vector3(3, H, T));
        Wall(t, "Wall_Hall_Gap", new Vector3(-3.1f, 0, 2f), new Vector3(T, H, 0.4f)); // between practice and lobby

        // doors: one on each side of every room's south wall (same in all rooms),
        // the main entrance into Room_1 from the hall, and lobby / practice doors on the hall
        for (int r = 0; r < 3; r++)
        {
            float x = r * 8 + 4;
            Door(t, "Door_Room" + (r + 1) + "_Corridor", new Vector3(x, 0, -0.11f), 180);
            Door(t, "Door_Room" + (r + 1) + "_Inside", new Vector3(x, 0, 0.11f), 0);
        }
        Door(t, "Door_Entrance", new Vector3(-0.11f, 0, 4), -90);
        Door(t, "Door_Lobby", new Vector3(-3.09f, 0, 5.2f), 90);
        Door(t, "Door_Practice", new Vector3(-3.09f, 0, -0.7f), 90);

        // ceilings (with colliders, see above), one slab per space so each bakes its own lightmap
        for (int r = 0; r < 3; r++) Ceiling(t, "Ceiling_Room" + (r + 1), new Vector3(r * 8 + 4, 0, 4), new Vector2(8, 8));
        Ceiling(t, "Ceiling_Corridor", new Vector3(12, 0, -1.5f), new Vector2(30, 3));
        Ceiling(t, "Ceiling_Hall", new Vector3(-1.5f, 0, 4), new Vector2(3, 8));
        Ceiling(t, "Ceiling_East", new Vector3(25.5f, 0, 4), new Vector2(3, 8));
        Ceiling(t, "Ceiling_Lobby", LobbyOrigin + new Vector3(3, 0, 3), new Vector2(6, 6));
        Ceiling(t, "Ceiling_Practice", PracticeOrigin + new Vector3(2.5f, 0, 2.5f), new Vector2(5, 5));

        // roof over the whole footprint (seen only from outside, no collider)
        var roof = Box(t, "Roof", new Vector3(8.9f, H + 0.25f, 2.5f), new Vector3(36.6f, 0.2f, 11.8f), roofMat, false);
        GameObjectUtility.SetStaticEditorFlags(roof, StaticEditorFlags.BatchingStatic);

        // light panels: LightingBaker turns each one into a baked area light
        for (int r = 0; r < 3; r++)
        {
            LightPanel(t, "LightPanel_Room" + (r + 1) + "_A", new Vector3(r * 8 + 2.7f, 0, 4));
            LightPanel(t, "LightPanel_Room" + (r + 1) + "_B", new Vector3(r * 8 + 5.3f, 0, 4));
        }
        for (float x = 0; x <= 24; x += 6) LightPanel(t, "LightPanel_Corridor_" + x, new Vector3(x, 0, -1.5f));
        LightPanel(t, "LightPanel_Hall", new Vector3(-1.5f, 0, 4));
        LightPanel(t, "LightPanel_East", new Vector3(25.5f, 0, 4));
        LightPanel(t, "LightPanel_Lobby", LobbyOrigin + new Vector3(3, 0, 3));
        LightPanel(t, "LightPanel_Practice", PracticeOrigin + new Vector3(2.5f, 0, 2.5f));

        Kit.EnsureFolder("Assets/_Project/Prefabs/Building");
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[Building] saved " + PrefabPath);
    }

    // ---------- pieces ----------

    static void Floor(Transform parent, string name, Vector3 center, Vector2 size)
    {
        var f = Box(parent, name, new Vector3(center.x, FloorY - 0.1f, center.z), new Vector3(size.x, 0.1f, size.y), floorMat, true);
        Static(f);
        var area = f.AddComponent<TeleportationArea>();
        area.interactionLayers = 1 << 31; // Starter Assets "Teleport" layer
    }

    static void Wall(Transform parent, string name, Vector3 basePos, Vector3 size)
    {
        Static(Box(parent, name, basePos, size, wallMat, true));
    }

    static void Ceiling(Transform parent, string name, Vector3 center, Vector2 size)
    {
        Static(Box(parent, name, new Vector3(center.x, H, center.z), new Vector3(size.x, 0.1f, size.y), ceilingMat, true));
    }

    static void LightPanel(Transform parent, string name, Vector3 floorPos)
    {
        var p = Box(parent, name, new Vector3(floorPos.x, H - 0.03f, floorPos.z), new Vector3(1.2f, 0.03f, 0.3f), panelMat, false);
        Static(p);
    }

    // Closed door against a wall: frame + panel (kit door model if there is one). Visual only,
    // the wall behind it is the collider. yaw 0 faces +z.
    static void Door(Transform parent, string name, Vector3 pos, float yaw)
    {
        var door = new GameObject(name).transform;
        door.SetParent(parent, false);
        door.localPosition = pos;
        door.localRotation = Quaternion.Euler(0, yaw, 0);

        Box(door, "Frame_L", new Vector3(-0.56f, 0, 0), new Vector3(0.08f, 2.2f, 0.06f), frameMat, false);
        Box(door, "Frame_R", new Vector3(0.56f, 0, 0), new Vector3(0.08f, 2.2f, 0.06f), frameMat, false);
        Box(door, "Frame_Top", new Vector3(0, 2.16f, 0), new Vector3(1.2f, 0.08f, 0.06f), frameMat, false);

        var model = Kit.Spawn(Kit.ModelEntry("door"), "Panel");
        if (model != null)
        {
            var b = Kit.LocalBounds(model);
            var s = b.size;
            bool turn = s.x < s.z; // make the wide side face out
            float w = turn ? s.z : s.x;
            float k = Mathf.Min(1.04f / Mathf.Max(w, 0.01f), 2.12f / Mathf.Max(s.y, 0.01f));
            model.transform.SetParent(door, false);
            model.transform.localRotation = turn ? Quaternion.Euler(0, 90, 0) : Quaternion.identity;
            model.transform.localScale = Vector3.one * k;
            var offset = model.transform.localRotation * new Vector3(b.center.x, b.min.y, b.center.z) * k;
            model.transform.localPosition = -offset;
        }
        else
        {
            Box(door, "Panel", Vector3.zero, new Vector3(1.04f, 2.12f, 0.04f), doorMat, false);
            Box(door, "Handle", new Vector3(0.38f, 1.0f, 0.04f), new Vector3(0.12f, 0.03f, 0.04f), frameMat, false);
        }
        Static(door.gameObject);
    }

    // cube whose bottom sits at basePos.y
    static GameObject Box(Transform parent, string name, Vector3 basePos, Vector3 size, Material mat, bool collider)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = basePos + new Vector3(0, size.y / 2, 0);
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static void Static(GameObject go)
    {
        var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI |
                    StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
    }
}

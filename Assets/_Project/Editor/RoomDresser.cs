using System.Collections.Generic;
using System.Linq;
using Team8.Core;
using UnityEditor;
using UnityEngine;

// Team8 > 2b Dress Rooms
// Turns the greybox rooms into themed rooms: 1 English bedroom, 2 antique attic, 3 Victorian kitchen.
// RoomPlanner decides where things go, the themes decide how they look. For study balance:
//   - every lever spot gets a hiding kit with the same size in every room (A under, B behind, C inside)
//   - the lever is small and sits at the kit's lever pose (the LeverSpot_* markers move there)
//   - every room gets the same number of wall pieces, one centre piece and the same number of decoys
// The greybox Partition_/Prop_ boxes are removed (the kits and centre piece replace them).
// Rooms that already have a DressedRoom component are skipped, so hand edits are safe.
public static class RoomDresser
{
    public const string RoomFolder = "Assets/_Project/Prefabs/Rooms";
    public const int PropsPerRoom = 6;
    const float MaxPropSize = 0.3f;
    static readonly string[] Letters = { "A", "B", "C" };

    const StaticEditorFlags Static = StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI |
                                     StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;

    static RoomTheme ThemeFor(int r)
    {
        if (r == 1) return new BedroomTheme();
        if (r == 2) return new AtticTheme();
        return new KitchenTheme();
    }

    [MenuItem("Team8/2b Dress Rooms", priority = 2)]
    public static void DressAll()
    {
        Kit.Load();

        // same number of wall pieces in every room: the smallest count any room can fit
        var plans = new Plan[3];
        for (int r = 1; r <= 3; r++) plans[r - 1] = MakePlan(r);
        if (plans.Any(p => p == null)) return;
        int wallCount = plans.Min(p => p.Slots.Count);

        for (int r = 1; r <= 3; r++) Dress(r, plans[r - 1], wallCount);
        UpdatePracticeLever();
        AssetDatabase.SaveAssets();
    }

    // Deletes the rooms the dresser made (DressedRoom.generated) and builds them again from scratch.
    [MenuItem("Team8/2b Redo Generated Rooms", priority = 3)]
    public static void Redo()
    {
        if (!Team8Editor.Confirm("Delete the generated Room_1..3 prefabs and dress them again? Hand-dressed rooms are kept.")) return;
        for (int r = 1; r <= 3; r++)
        {
            string path = RoomFolder + "/Room_" + r + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var mark = prefab != null ? prefab.GetComponent<DressedRoom>() : null;
            if (mark != null && mark.generated) AssetDatabase.DeleteAsset(path);
        }
        GreyboxBuilder.Build();
        DressAll();
    }

    class Plan
    {
        public (double x, double z)[] Spots;
        public HideKit[] Kits;
        public List<Box2> Center;
        public List<Slot> Slots;
    }

    static Plan MakePlan(int r)
    {
        string path = RoomFolder + "/Room_" + r + ".prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning("[Dress] " + path + " missing, run Team8 > 2 Build Greybox first");
            return null;
        }
        var plan = new Plan();
        plan.Spots = Letters.Select(l =>
        {
            var t = prefab.transform.Find("LeverSpot_" + l);
            return t == null ? (0.0, 0.0) : ((double)t.localPosition.x, (double)t.localPosition.z);
        }).ToArray();
        if (prefab.GetComponent<DressedRoom>() != null)
        {
            // already dressed: spots moved to the kit lever poses, so plan from the original greybox spots
            plan.Spots = OriginalSpots(prefab.transform);
        }
        plan.Kits = RoomPlanner.Kits(plan.Spots);
        plan.Center = RoomPlanner.CenterPieces(plan.Spots, plan.Kits);
        plan.Slots = RoomPlanner.PerimeterSlots(r - 1, plan.Spots, plan.Kits, plan.Center);
        return plan;
    }

    // Dressed rooms keep their kits; the kit roots remember the original spot in their name-free pose.
    static (double x, double z)[] OriginalSpots(Transform room)
    {
        return Letters.Select(l =>
        {
            var k = room.Find("Kit_" + l);
            var spot = k != null ? k.Find("Spot") : null;
            var p = spot != null ? spot.position - room.position : Vector3.zero;
            return ((double)p.x, (double)p.z);
        }).ToArray();
    }

    static void Dress(int r, Plan plan, int wallCount)
    {
        string path = RoomFolder + "/Room_" + r + ".prefab";
        var room = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (room.GetComponent<DressedRoom>() != null)
            {
                Debug.Log("[Dress] Room_" + r + " already dressed, skipping");
                return;
            }
            var theme = ThemeFor(r);
            theme.RoomIndex = r - 1;
            var t = room.transform;

            // greybox blockers out, themed surfaces in
            foreach (var child in t.Cast<Transform>().ToArray())
                if (child.name.StartsWith("Partition_") || child.name.StartsWith("Prop_")) Object.DestroyImmediate(child.gameObject);
            foreach (var mr in room.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr.name == "Floor" && !mr.sharedMaterial.name.Contains("Exit")) mr.sharedMaterial = theme.Floor;
                else if (mr.name.StartsWith("Wall_")) mr.sharedMaterial = theme.Wall;
            }
            if (r > 1) WestSkin(t, theme.Wall);   // the shared wall is painted in the previous room's colour

            // hiding kits + lever spots
            for (int i = 0; i < 3; i++)
            {
                var kit = plan.Kits[i];
                var root = Piece(t, "Kit_" + Letters[i], kit.Cover.X, kit.Cover.Z, kit.Cover.Yaw);
                if (kit.Kind == KitKind.Under) theme.UnderKit(root, kit);
                else if (kit.Kind == KitKind.Behind) theme.BehindKit(root, kit);
                else theme.InsideKit(root, kit);
                Solid(root, new Vector3((float)kit.Cover.W, (float)kit.CoverHeight, (float)kit.Cover.D));

                // remember where the original spot was (for planning again later)
                var mark = new GameObject("Spot").transform;
                mark.SetParent(root, true);
                mark.position = t.position + new Vector3((float)plan.Spots[i].x, 0, (float)plan.Spots[i].z);

                var spot = t.Find("LeverSpot_" + Letters[i]);
                spot.localPosition = new Vector3((float)kit.LeverX, (float)kit.LeverY, (float)kit.LeverZ);
                spot.localRotation = Quaternion.Euler(0, (float)kit.LeverYaw, 0);
            }

            // the lever starts on spot A; the TrialManager moves it per layout
            var lever = GreyboxBuilder.ReplaceLever(t, r);
            var spotA = t.Find("LeverSpot_A");
            lever.transform.localPosition = spotA.localPosition;
            lever.transform.localRotation = spotA.localRotation;

            // centre piece + wall furniture
            foreach (var c in plan.Center)
            {
                var root = Piece(t, "CenterPiece", c.X, c.Z, c.Yaw);
                theme.CenterPiece(root);
                Solid(root, new Vector3((float)RoomPlanner.CenterW, (float)RoomPlanner.CenterH, (float)RoomPlanner.CenterD));
            }
            var slots = RoomPlanner.Spread(plan.Slots, wallCount);
            for (int i = 0; i < slots.Count; i++)
            {
                var root = Piece(t, "WallPiece_" + (i + 1), slots[i].X, slots[i].Z, slots[i].Yaw);
                theme.WallPiece(root, i);
                FitSolid(root, new Vector3((float)RoomPlanner.SlotWidth, 0, (float)RoomPlanner.SlotDepth));
            }

            var decor = new GameObject("Decor").transform;
            decor.SetParent(t, false);
            theme.Decor(decor);
            MarkStatic(t);

            var decoys = RoomPlanner.DecoySlots(plan.Spots, plan.Kits, plan.Center, slots, PropsPerRoom);
            AddDecoys(t, r, theme, decoys);
            if (r == 3) AddExitSign(t);

            var dressed = room.AddComponent<DressedRoom>();
            dressed.theme = theme.Name;
            PrefabUtility.SaveAsPrefabAsset(room, path);
            Debug.Log("[Dress] Room_" + r + " dressed as " + theme.Name + ": 3 kits, " + slots.Count + " wall pieces, " +
                      plan.Center.Count + " centre piece, " + decoys.Count + " decoys");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(room);
        }
    }

    static Transform Piece(Transform room, string name, double x, double z, double yaw)
    {
        var root = new GameObject(name).transform;
        root.SetParent(room, false);
        root.localPosition = new Vector3((float)x, 0, (float)z);
        root.localRotation = Quaternion.Euler(0, (float)yaw, 0);
        return root;
    }

    // one solid box per piece, standing on the floor
    static void Solid(Transform root, Vector3 size)
    {
        var box = root.gameObject.AddComponent<BoxCollider>();
        box.center = new Vector3(0, size.y / 2, 0);
        box.size = size;
    }

    // footprint fixed by the planner, height from what the theme built
    static void FitSolid(Transform root, Vector3 footprint)
    {
        float top = 0.5f;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
        {
            var b = mf.sharedMesh.bounds;
            var m = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var corner = b.center + Vector3.Scale(b.extents, new Vector3(i & 1, (i >> 1) & 1, (i >> 2) & 1) * 2 - Vector3.one);
                top = Mathf.Max(top, m.MultiplyPoint3x4(corner).y);
            }
        }
        Solid(root, new Vector3(footprint.x, top, footprint.z));
    }

    // thin skin over the west face of the shared wall, in this room's wall material (no collider)
    static void WestSkin(Transform room, Material mat)
    {
        foreach (var (z0, z1) in new[] { (0.1f, 3.25f), (4.75f, 7.9f) })
        {
            var skin = RoomPart(room, PrimitiveType.Cube, new Vector3(0.105f, 1.5f, (z0 + z1) / 2), new Vector3(0.01f, 3f, z1 - z0), mat);
            skin.name = "WallSkin_W";
        }
    }

    // everything the dresser built is static (batched + baked) except things that move
    static void MarkStatic(Transform room)
    {
        foreach (Transform root in room)
        {
            string n = root.name;
            if (!(n.StartsWith("Kit_") || n.StartsWith("WallPiece_") || n == "CenterPiece" || n == "Decor" || n == "WallSkin_W")) continue;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.GetComponentInParent<AmbientSpin>(true) == null)
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, Static);
        }
    }

    // ---------- grabbable decoys ----------

    static void AddDecoys(Transform room, int r, RoomTheme theme, List<(double x, double z)> slots)
    {
        var pool = Kit.ModelsWithPrefix("small_prop_");
        for (int i = 0; i < slots.Count; i++)
        {
            GameObject prop = null;
            string id = "prop";
            if (pool.Length > 0)
            {
                var entry = pool[((r - 1) * PropsPerRoom + i) % pool.Length];
                prop = Kit.Spawn(entry, "Decoy_" + (i + 1) + "_" + entry.id);
                id = entry.id;
            }
            if (prop == null)
            {
                prop = new GameObject();
                theme.SmallProp(prop.transform, i);
                id = theme.Name + "_" + prop.transform.GetChild(0).name.ToLowerInvariant();
                prop.name = "Decoy_" + (i + 1) + "_" + id;
            }

            var b = Kit.LocalBounds(prop);
            float k = Mathf.Min(1f, MaxPropSize / Mathf.Max(b.size.x, b.size.y, b.size.z, 0.01f));
            prop.transform.SetParent(room, false);
            prop.transform.localScale = Vector3.one * k;
            prop.transform.localPosition = new Vector3((float)slots[i].x - b.center.x * k, 0.01f - b.min.y * k, (float)slots[i].z - b.center.z * k);
            prop.transform.localRotation = Quaternion.Euler(0, i * 37f, 0);

            var col = prop.AddComponent<BoxCollider>();
            col.center = b.center;
            col.size = b.size;
            var grabbable = prop.AddComponent<GrabbableProp>(); // also adds XRGrabInteractable + Rigidbody
            grabbable.propName = id;
            prop.GetComponent<Rigidbody>().mass = 0.5f;
        }
    }

    // Used by BudgetReport when a room is over the triangle budget: fewer objects, not lower quality.
    public static int RemoveDecoys(GameObject room, int howMany)
    {
        var decoys = room.GetComponentsInChildren<GrabbableProp>(true).Reverse().Take(howMany).ToList();
        foreach (var d in decoys) Object.DestroyImmediate(d.gameObject);
        return decoys.Count;
    }

    // ---------- practice room + signage ----------

    // the practice lever should look exactly like the real ones
    static void UpdatePracticeLever()
    {
        string path = RoomFolder + "/PracticeRoom.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;
        var room = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var lever = room.GetComponentInChildren<LeverController>(true);
            if (lever != null && lever.transform.Find("Base") != null) return;   // already the small one
            GreyboxBuilder.ReplaceLever(room.transform, 0);
            PrefabUtility.SaveAsPrefabAsset(room, path);
            Debug.Log("[Dress] practice lever updated");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(room);
        }
    }

    static void AddExitSign(Transform room)
    {
        var sign = RoomPart(room, PrimitiveType.Cube, new Vector3(7.86f, 2.6f, 4f), new Vector3(0.04f, 0.3f, 0.8f),
            Kit.Emissive("ExitSign", new Color(0.1f, 0.9f, 0.3f)));
        sign.name = "ExitSign";
    }

    public static Transform RoomPart(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go.transform;
    }
}

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Team8 > 2b Dress Rooms
// Turns the greybox rooms into themed rooms (warehouse / office / workshop) with the CC0 kit.
// For study balance the greybox boxes stay as the colliders (same blockers in every room);
// only their look changes: each box gets a kit model scaled to fit inside it and its grey
// renderer is hidden. Lever spots never move. Every room also gets the same number of
// grabbable decoy props in fixed slots away from the lever spots.
// Rooms that already have a DressedRoom component are skipped, so hand edits are safe.
public static class RoomDresser
{
    public const string RoomFolder = "Assets/_Project/Prefabs/Rooms";
    public const int PropsPerRoom = 4;
    const float MinPropDistanceToLever = 1.5f;
    const float MaxPropSize = 0.6f;

    class Theme
    {
        public string name, floor, wall, tall, low;
        public Color floorColor, wallColor;
    }

    static readonly Theme[] Themes =
    {
        new Theme { name = "warehouse", floor = "floor_concrete", wall = "wall_concrete", tall = "tall_metal", low = "low_crate",
                    floorColor = new Color(0.50f, 0.50f, 0.48f), wallColor = new Color(0.70f, 0.70f, 0.66f) },
        new Theme { name = "office", floor = "floor_wood", wall = "wall_plaster", tall = "tall_wood", low = "low_table",
                    floorColor = new Color(0.55f, 0.42f, 0.30f), wallColor = new Color(0.88f, 0.86f, 0.80f) },
        new Theme { name = "workshop", floor = "floor_metal", wall = "wall_concrete", tall = "tall_metal", low = "low_barrel",
                    floorColor = new Color(0.40f, 0.42f, 0.45f), wallColor = new Color(0.62f, 0.64f, 0.60f) },
    };

    [MenuItem("Team8/2b Dress Rooms", priority = 2)]
    public static void DressAll()
    {
        Kit.Load();
        for (int r = 1; r <= 3; r++) Dress(r, Themes[r - 1]);
        AssetDatabase.SaveAssets();
    }

    static void Dress(int r, Theme theme)
    {
        string path = RoomFolder + "/Room_" + r + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            Debug.LogWarning("[Dress] " + path + " missing, run Team8 > 2 Build Greybox first");
            return;
        }
        var room = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (room.GetComponent<DressedRoom>() != null)
            {
                Debug.Log("[Dress] Room_" + r + " already dressed, skipping");
                return;
            }

            var floorMat = Kit.Surface(theme.name + "_Floor", theme.floor, "floor_concrete", new Vector2(4, 4), theme.floorColor);
            var wallMat = Kit.Surface(theme.name + "_Wall", theme.wall, "wall_concrete", new Vector2(4, 1.5f), theme.wallColor);

            foreach (var t in room.GetComponentsInChildren<Transform>(true).ToArray())
            {
                var mr = t.GetComponent<MeshRenderer>();
                if (mr == null) continue;
                if (t.name == "Floor" && !mr.sharedMaterial.name.Contains("Exit")) mr.sharedMaterial = floorMat;
                else if (t.name.StartsWith("Wall_")) mr.sharedMaterial = wallMat;
                else if (t.name.StartsWith("Partition_") || t.name.StartsWith("Prop_")) DressOccluder(room.transform, t, theme);
            }

            AddProps(room.transform, r);
            AddAmbient(room.transform, r);
            if (r == 3) AddExitSign(room.transform);

            room.AddComponent<DressedRoom>().theme = theme.name;
            PrefabUtility.SaveAsPrefabAsset(room, path);
            Debug.Log("[Dress] Room_" + r + " dressed as " + theme.name);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(room);
        }
    }

    // Put a kit model inside the greybox box (same footprint/height), hide the grey box, keep its collider.
    static void DressOccluder(Transform room, Transform box, Theme theme)
    {
        Vector3 size = box.localScale; // greybox boxes are unit cubes scaled to size
        bool tall = size.y >= 1.5f;
        var entry = Kit.ModelEntry(tall ? theme.tall : theme.low);
        if (entry == null) entry = Kit.ModelEntry(tall ? "tall_metal" : "low_crate");
        var model = Kit.Spawn(entry, box.name + "_Model");
        if (model == null) return; // no kit downloaded: keep the grey box visible

        var b = Kit.LocalBounds(model);
        if (b.size == Vector3.zero) { Object.DestroyImmediate(model); return; }
        Vector3 s = b.size, c0 = b.center;

        // turn the model 90 degrees if its long side doesn't match the box's long side
        bool swap = (s.x >= s.z) != (size.x >= size.z);
        float sx = swap ? s.z : s.x, sz = swap ? s.x : s.z;
        float k = Mathf.Min(size.x / Mathf.Max(sx, 0.01f), size.y / Mathf.Max(s.y, 0.01f), size.z / Mathf.Max(sz, 0.01f));

        var rot = box.localRotation * (swap ? Quaternion.Euler(0, 90, 0) : Quaternion.identity);
        var floorPoint = box.localPosition - new Vector3(0, size.y / 2, 0);
        var pivotOffset = rot * (new Vector3(c0.x, c0.y - s.y / 2, c0.z) * k);

        model.transform.SetParent(room, false);
        model.transform.localRotation = rot;
        model.transform.localScale = Vector3.one * k;
        model.transform.localPosition = floorPoint - pivotOffset;
        GameObjectUtility.SetStaticEditorFlags(model, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI |
                                                     StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);

        box.GetComponent<MeshRenderer>().enabled = false;
    }

    // ---------- grabbable decoys ----------

    static void AddProps(Transform room, int r)
    {
        var pool = Kit.ModelsWithPrefix("small_prop_");
        if (pool.Length == 0)
        {
            Debug.LogWarning("[Dress] no small props in the kit, Room_" + r + " gets no decoys");
            return;
        }
        var slots = PropSlots(room, PropsPerRoom);
        for (int i = 0; i < slots.Count; i++)
        {
            var entry = pool[((r - 1) * PropsPerRoom + i) % pool.Length];
            var prop = Kit.Spawn(entry, "Decoy_" + (i + 1) + "_" + entry.id);
            if (prop == null) continue;

            var b = Kit.LocalBounds(prop);
            float k = Mathf.Min(1f, MaxPropSize / Mathf.Max(b.size.x, b.size.y, b.size.z, 0.01f));
            prop.transform.SetParent(room, false);
            prop.transform.localScale = Vector3.one * k;
            prop.transform.localPosition = new Vector3(slots[i].x - b.center.x * k, 0.01f - b.min.y * k, slots[i].y - b.center.z * k);
            prop.transform.localRotation = Quaternion.Euler(0, i * 37f, 0);

            var col = prop.AddComponent<BoxCollider>();
            col.center = b.center;
            col.size = b.size;
            var grabbable = prop.AddComponent<GrabbableProp>(); // also adds XRGrabInteractable + Rigidbody
            grabbable.propName = entry.id;
            prop.GetComponent<Rigidbody>().mass = 0.5f;
        }
    }

    // Fixed, spread-out floor spots: >= 1.5 m from every lever spot, clear of solid boxes,
    // and out of the doorway lanes. Same rules in every room.
    static List<Vector2> PropSlots(Transform room, int count)
    {
        var levers = room.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name.StartsWith("LeverSpot_"))
            .Select(t => new Vector2(t.localPosition.x, t.localPosition.z)).ToList();
        var solids = room.GetComponentsInChildren<BoxCollider>(true).Where(c => !c.isTrigger)
            .Select(c => { var b = c.bounds; b.center -= room.position; b.Expand(0.7f); return b; }).ToList();

        var candidates = new List<Vector2>();
        for (float x = 1f; x <= 7f; x += 0.5f)
        for (float z = 1f; z <= 7f; z += 0.5f)
        {
            var p = new Vector2(x, z);
            if (levers.Any(l => Vector2.Distance(l, p) < MinPropDistanceToLever)) continue;
            if (solids.Any(b => b.Contains(new Vector3(x, 0.5f, z)))) continue;
            if ((x < 1.6f || x > 6.4f) && Mathf.Abs(z - 4f) < 1.3f) continue; // doorway lanes
            candidates.Add(p);
        }

        // farthest-point sampling from the spot nearest the room centre (deterministic)
        var picked = new List<Vector2>();
        if (candidates.Count == 0) return picked;
        picked.Add(candidates.OrderBy(p => Vector2.Distance(p, new Vector2(4, 4))).First());
        while (picked.Count < count && picked.Count < candidates.Count)
            picked.Add(candidates.OrderByDescending(p => picked.Min(q => Vector2.Distance(p, q))).First());
        return picked;
    }

    // Used by BudgetReport when a room is over the triangle budget: fewer objects, not lower quality.
    public static int RemoveDecoys(GameObject room, int howMany)
    {
        var decoys = room.GetComponentsInChildren<GrabbableProp>(true).Reverse().Take(howMany).ToList();
        foreach (var d in decoys) Object.DestroyImmediate(d.gameObject);
        return decoys.Count;
    }

    // ---------- ambient + signage ----------

    // One slow background animation per room, at the room centre or on a wall away from lever spots.
    static void AddAmbient(Transform room, int r)
    {
        var dark = Kit.Flat("AmbientMetal", new Color(0.2f, 0.2f, 0.22f));
        if (r == 1) // ceiling fan in the middle of the warehouse
        {
            var fan = new GameObject("CeilingFan").transform;
            fan.SetParent(room, false);
            fan.localPosition = new Vector3(4, 2.75f, 4);
            Part(fan, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.2f, 0.05f, 0.2f), dark);
            for (int i = 0; i < 4; i++)
            {
                var blade = Part(fan, PrimitiveType.Cube, Vector3.zero, new Vector3(1.0f, 0.02f, 0.14f), dark);
                blade.localRotation = Quaternion.Euler(0, i * 45, 0);
            }
            fan.gameObject.AddComponent<AmbientSpin>().degreesPerSecond = 45f;
        }
        else if (r == 2) // blinking status light on the office's back wall
        {
            var light = Part(room, PrimitiveType.Cube, new Vector3(4, 2.2f, 7.86f), new Vector3(0.08f, 0.08f, 0.04f),
                Kit.Emissive("StatusLight", new Color(0.2f, 1f, 0.3f)));
            light.name = "StatusLight";
            light.gameObject.AddComponent<AmbientBlink>().target = light.GetComponent<Renderer>();
        }
        else // slow wall vent fan in the workshop
        {
            var vent = new GameObject("WallVent").transform;
            vent.SetParent(room, false);
            vent.localPosition = new Vector3(4, 2.3f, 0.14f);
            for (int i = 0; i < 3; i++)
            {
                var blade = Part(vent, PrimitiveType.Cube, Vector3.zero, new Vector3(0.6f, 0.08f, 0.02f), dark);
                blade.localRotation = Quaternion.Euler(0, 0, i * 60);
            }
            var spin = vent.gameObject.AddComponent<AmbientSpin>();
            spin.axis = Vector3.forward;
            spin.degreesPerSecond = 90f;
        }
    }

    static void AddExitSign(Transform room)
    {
        var sign = Part(room, PrimitiveType.Cube, new Vector3(7.86f, 2.6f, 4f), new Vector3(0.04f, 0.3f, 0.8f),
            Kit.Emissive("ExitSign", new Color(0.1f, 0.9f, 0.3f)));
        sign.name = "ExitSign";
    }

    public static Transform Part(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat)
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

using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Team8 > 2b Dress Rooms
// Turns the greybox rooms into themed rooms (warehouse / office / workshop) using the CC0 kit
// that tools/fetch_assets.py downloads. For study balance the greybox boxes stay as the
// colliders (same blockers in every room); only their look changes: each box gets a kit model
// scaled to fit inside it and its own grey renderer is hidden. Lever spots never move.
// Rooms that already have a DressedRoom component are skipped, so hand edits are safe.
public static class RoomDresser
{
    const string RoomFolder = "Assets/_Project/Prefabs/Rooms";
    const string KitFolder = "Assets/_Project/Art/Kit";
    const string IndexPath = KitFolder + "/kit_index.json";
    const string MatFolder = KitFolder + "/Materials";

    [System.Serializable] class ModelEntry { public string role; public string path; }
    [System.Serializable] class TextureEntry { public string role; public string albedo; public string normal; }
    [System.Serializable] class KitIndex { public ModelEntry[] models = new ModelEntry[0]; public TextureEntry[] textures = new TextureEntry[0]; }

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

    static KitIndex kit;

    [MenuItem("Team8/2b Dress Rooms", priority = 2)]
    public static void DressAll()
    {
        kit = LoadIndex();
        for (int r = 1; r <= 3; r++) Dress(r, Themes[r - 1]);
        AssetDatabase.SaveAssets();
    }

    static KitIndex LoadIndex()
    {
        if (!File.Exists(IndexPath))
        {
            Debug.LogWarning("[Dress] no " + IndexPath + " (run tools/fetch_assets.py). Using plain colors only.");
            return new KitIndex();
        }
        return JsonUtility.FromJson<KitIndex>(File.ReadAllText(IndexPath));
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

            var floorMat = Surface(theme.name + "_Floor", theme.floor, "floor_concrete", new Vector2(4, 4), theme.floorColor);
            var wallMat = Surface(theme.name + "_Wall", theme.wall, "wall_concrete", new Vector2(4, 1.5f), theme.wallColor);

            foreach (var t in room.GetComponentsInChildren<Transform>(true).ToArray())
            {
                var mr = t.GetComponent<MeshRenderer>();
                if (mr == null) continue;
                if (t.name == "Floor" && !mr.sharedMaterial.name.Contains("Exit")) mr.sharedMaterial = floorMat;
                else if (t.name.StartsWith("Wall_")) mr.sharedMaterial = wallMat;
                else if (t.name.StartsWith("Partition_") || t.name.StartsWith("Prop_")) DressOccluder(room.transform, t, theme);
            }

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
        string role = size.y >= 1.5f ? theme.tall : theme.low;
        var asset = Model(role);
        if (asset == null) asset = Model(size.y >= 1.5f ? "tall_metal" : "low_crate");
        if (asset == null) return; // no kit downloaded: keep the grey box visible

        var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        model.name = box.name + "_Model";
        foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

        // measure the model at the origin
        model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        model.transform.localScale = Vector3.one;
        var renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { Object.DestroyImmediate(model); return; }
        var b = renderers[0].bounds;
        foreach (var rend in renderers) b.Encapsulate(rend.bounds);
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
        GameObjectUtility.SetStaticEditorFlags(model, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);

        box.GetComponent<MeshRenderer>().enabled = false;
    }

    // One slow background animation per room, at the room centre or on a wall away from lever spots.
    static void AddAmbient(Transform room, int r)
    {
        var dark = Surface("AmbientMetal", null, null, Vector2.one, new Color(0.2f, 0.2f, 0.22f));
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
                Emissive("StatusLight", new Color(0.2f, 1f, 0.3f)));
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
            Emissive("ExitSign", new Color(0.1f, 0.9f, 0.3f)));
        sign.name = "ExitSign";
    }

    static Transform Part(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go.transform;
    }

    // ---------- kit lookup + materials ----------

    static GameObject Model(string role)
    {
        var entry = kit.models.FirstOrDefault(m => m.role == role);
        return entry == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(entry.path);
    }

    static Material Surface(string name, string role, string fallbackRole, Vector2 tiling, Color fallbackColor)
    {
        EnsureFolder(MatFolder);
        string path = MatFolder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        mat = new Material(LitShader()) { enableInstancing = true };
        var tex = kit.textures.FirstOrDefault(t => t.role == role) ?? kit.textures.FirstOrDefault(t => t.role == fallbackRole);
        var albedo = tex == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(tex.albedo);
        if (albedo != null)
        {
            mat.color = Color.white;
            mat.mainTexture = albedo;
            mat.mainTextureScale = tiling;
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(tex.normal);
            if (normal != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
        }
        else
        {
            mat.color = fallbackColor;
        }
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static Material Emissive(string name, Color color)
    {
        EnsureFolder(MatFolder);
        string path = MatFolder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = new Material(LitShader()) { color = color, enableInstancing = true };
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color * 2f);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static Shader LitShader()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        return shader != null ? shader : Shader.Find("Standard");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}

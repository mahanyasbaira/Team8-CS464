using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Shared helpers for the downloaded CC0 kit (tools/fetch_assets.py):
// reads kit_index.json, makes URP Lit PBR materials, and fits models into boxes.
public static class Kit
{
    public const string Folder = "Assets/_Project/Art/Kit";
    const string IndexPath = Folder + "/kit_index.json";
    const string MatFolder = Folder + "/Materials";

    [System.Serializable]
    public class Entry
    {
        public string role, id, path, albedo, normal, mask, occlusion;
    }

    [System.Serializable]
    class Index
    {
        public Entry[] models = new Entry[0];
        public Entry[] textures = new Entry[0];
    }

    static Index index;

    public static bool Loaded => index != null && (index.models.Length > 0 || index.textures.Length > 0);

    public static void Load()
    {
        if (!File.Exists(IndexPath))
        {
            Debug.LogWarning("[Kit] no " + IndexPath + " (run tools/fetch_assets.py). Using plain colors.");
            index = new Index();
            return;
        }
        index = JsonUtility.FromJson<Index>(File.ReadAllText(IndexPath));
        Debug.Log("[Kit] " + index.models.Length + " models, " + index.textures.Length + " textures");
    }

    public static Entry ModelEntry(string role)
    {
        if (index == null) Load();
        return index.models.FirstOrDefault(m => m.role == role);
    }

    public static Entry[] ModelsWithPrefix(string prefix)
    {
        if (index == null) Load();
        return index.models.Where(m => m.role != null && m.role.StartsWith(prefix)).ToArray();
    }

    public static GameObject Model(string role)
    {
        var e = ModelEntry(role);
        return e == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(e.path);
    }

    // ---------- materials ----------

    // PBR surface material from a texture role (falls back to another role, then a flat color).
    public static Material Surface(string name, string role, string fallbackRole, Vector2 tiling, Color fallbackColor)
    {
        if (index == null) Load();
        string path = MatFolder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        var tex = index.textures.FirstOrDefault(t => t.role == role);
        if (tex == null) tex = index.textures.FirstOrDefault(t => t.role == fallbackRole);
        mat = NewLit(fallbackColor);
        if (tex != null && ApplyMaps(mat, tex)) mat.mainTextureScale = tiling;
        return Save(mat, path);
    }

    // One material per kit model built from its own maps (replaces the FBX import material).
    public static Material ForModel(Entry e)
    {
        string path = MatFolder + "/Model_" + e.id + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = NewLit(Color.gray);
        ApplyMaps(mat, e);
        return Save(mat, path);
    }

    public static Material Emissive(string name, Color color)
    {
        string path = MatFolder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = NewLit(color);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color * 2f);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        return Save(mat, path);
    }

    public static Material Flat(string name, Color color)
    {
        string path = MatFolder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        return mat != null ? mat : Save(NewLit(color), path);
    }

    static bool ApplyMaps(Material mat, Entry e)
    {
        var albedo = Tex(e.albedo);
        if (albedo == null) return false;
        mat.color = Color.white;
        mat.mainTexture = albedo;

        var normal = Tex(e.normal);
        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
        }
        var mask = Tex(e.mask);
        if (mask != null)
        {
            mat.SetTexture("_MetallicGlossMap", mask); // R = metal, A = smoothness
            mat.SetFloat("_Smoothness", 1f);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        var occlusion = Tex(e.occlusion);
        if (occlusion != null)
        {
            mat.SetTexture("_OcclusionMap", occlusion);
            mat.EnableKeyword("_OCCLUSIONMAP");
        }
        return true;
    }

    static Texture2D Tex(string path) => string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);

    static Material NewLit(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        return new Material(shader) { color = color, enableInstancing = true };
    }

    static Material Save(Material mat, string path)
    {
        EnsureFolder(MatFolder);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // ---------- models ----------

    // Instantiate a kit model with its PBR material and no colliders. Returns null if missing.
    public static GameObject Spawn(Entry e, string name)
    {
        if (e == null) return null;
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(e.path);
        if (asset == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        go.name = name;
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        var mat = ForModel(e);
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
        return go;
    }

    // Bounds of a model measured at the origin with no rotation and scale 1.
    public static Bounds LocalBounds(GameObject go)
    {
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        go.transform.localScale = Vector3.one;
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.zero);
        var b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    public static int Triangles(GameObject root)
    {
        int tris = 0;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = mf.sharedMesh;
            var r = mf.GetComponent<Renderer>();
            if (mesh == null || r == null || !r.enabled) continue;
            // index counts work even when the mesh isn't Read/Write enabled
            for (int s = 0; s < mesh.subMeshCount; s++) tris += (int)(mesh.GetIndexCount(s) / 3);
        }
        return tris;
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}

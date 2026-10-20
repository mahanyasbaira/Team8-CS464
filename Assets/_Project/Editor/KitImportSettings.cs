using UnityEditor;

// Import settings for everything under Assets/_Project/Art/Kit (the downloaded CC0 kit):
// 2K floors / 1K everything else, normal maps flagged, data maps (metal/smoothness, AO) in
// linear space, compressed meshes with lightmap UVs for baked lighting.
public class KitImportSettings : AssetPostprocessor
{
    const string KitFolder = "Assets/_Project/Art/Kit/";

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(KitFolder)) return;
        var importer = (TextureImporter)assetImporter;
        string lower = assetPath.ToLowerInvariant();

        importer.maxTextureSize = lower.Contains("floor") ? 2048 : 1024;
        importer.mipmapEnabled = true;
        importer.anisoLevel = lower.Contains("floor") ? 4 : 1;

        if (lower.Contains("_nor") || lower.Contains("normal"))
            importer.textureType = TextureImporterType.NormalMap;
        else if (lower.Contains("_mask") || lower.Contains("_occlusion") || lower.Contains("_arm"))
            importer.sRGBTexture = false;
    }

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(KitFolder)) return;
        var importer = (ModelImporter)assetImporter;
        importer.meshCompression = ModelImporterMeshCompression.Medium;
        importer.isReadable = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importAnimation = false;
        importer.generateSecondaryUV = true; // lightmap UVs
    }
}

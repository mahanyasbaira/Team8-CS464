using UnityEditor;

// Import settings for everything under Assets/_Project/Art/Kit (the downloaded CC0 kit):
// 1K textures, normal maps marked as normal maps, compressed meshes, no cameras/lights.
public class KitImportSettings : AssetPostprocessor
{
    const string KitFolder = "Assets/_Project/Art/Kit/";

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(KitFolder)) return;
        var importer = (TextureImporter)assetImporter;
        importer.maxTextureSize = 1024;
        importer.mipmapEnabled = true;
        string lower = assetPath.ToLowerInvariant();
        if (lower.Contains("_nor") || lower.Contains("normal"))
            importer.textureType = TextureImporterType.NormalMap;
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
    }
}

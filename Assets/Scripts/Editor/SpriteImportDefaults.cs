using System;
using UnityEditor;

public sealed class SpriteImportDefaults : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetImporter.importSettingsMissing) return;

        var importer = (TextureImporter)assetImporter;
        bool newArtwork = importer.textureType == TextureImporterType.Default &&
            assetPath.StartsWith("Assets/Art/", StringComparison.Ordinal);
        if (!newArtwork && importer.textureType != TextureImporterType.Sprite) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
    }
}

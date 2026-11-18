using System;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace GLTFast.Editor
{
    internal static class MuseumQuestTextures
    {
        internal static bool AppliesTo(AssetImportContext context)
        {
            string path = context.assetPath;
            return context.selectedBuildTarget == BuildTarget.Android &&
                path.StartsWith("Assets/Models/", StringComparison.Ordinal) &&
                path.EndsWith(".glb", StringComparison.OrdinalIgnoreCase);
        }

        internal static void Compress(AssetImportContext context, Texture2D texture, bool keepReadable)
        {
            if (texture.format is not (TextureFormat.RGB24 or TextureFormat.RGBA32 or TextureFormat.ARGB32))
                return;

            string path = context.assetPath;
            bool closeUp = path.StartsWith("Assets/Models/Artifacts/", StringComparison.Ordinal) ||
                path.StartsWith("Assets/Models/Weapons/", StringComparison.Ordinal);
            TextureFormat format = closeUp ? TextureFormat.ASTC_4x4 : TextureFormat.ASTC_6x6;
            int width = texture.width;
            int height = texture.height;
            int mipCount = texture.mipmapCount;
            bool srgb = texture.isDataSRGB;

            EditorUtility.CompressTexture(texture, format, TextureCompressionQuality.Normal);
            if (texture.format != format || texture.width != width || texture.height != height ||
                texture.mipmapCount != mipCount || texture.isDataSRGB != srgb)
            {
                throw new InvalidOperationException($"ASTC compression changed the texture settings for {path}/{texture.name}.");
            }

            if (!keepReadable && texture.isReadable)
                texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        }
    }
}

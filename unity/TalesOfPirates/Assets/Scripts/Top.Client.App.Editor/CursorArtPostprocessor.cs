using UnityEditor;
using UnityEngine;

namespace Top.Client.App.Editor
{
    /// <summary>
    /// Imports the client's cursor art the way a cursor has to be imported: unfiltered, so
    /// that the pixels of a thirty-two pixel picture stay the pixels the client drew,
    /// uncompressed and without mipmaps for the same reason, and readable because that is
    /// what Cursor.SetCursor asks for.
    /// <br/>
    /// It stands in for the .meta files tools/cursors.ps1 writes, which Unity may have
    /// imported the pictures before it ever saw.
    /// </summary>
    public class CursorArtPostprocessor : AssetPostprocessor
    {
        /// <summary>Where the cursor frames live, which is the only place this touches.</summary>
        private const string Where = "/Ui/cursors/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains(Where))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;

            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.isReadable = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 256;
        }
    }
}
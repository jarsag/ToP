using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Top.Client.App.Editor
{
    /// <summary>
    /// Copies the client's interface art into the project as sprites: the window
    /// backgrounds, the slot frames, the buttons and the item icons that the
    /// inventory and the equipment panel are drawn from. Using the client's own art
    /// is what makes the windows look like the game rather than like a remake of it.
    /// <br/>
    /// The files are copied rather than converted - Unity reads PNG, TGA and DDS
    /// itself - and what it is told about them is that they are sprites, unfiltered
    /// and uncompressed, because a frame drawn at thirty-six pixels has to stay
    /// thirty-six pixels.
    /// </summary>
    public class UiArtWindow : EditorWindow
    {
        /// <summary>Where the art lands, which is where the runtime loads it from by name.</summary>
        private const string Destination = "Assets/Resources/Ui";

        /// <summary>
        /// What is taken, and from where inside the client: the folders and files
        /// the inventory, the equipment panel and the hotbar are drawn from.
        /// </summary>
        private static readonly string[] Sources =
        {
            "texture/ui/corsairs/INV",
            "texture/ui/corsairs/eqform",
            "texture/ui/corsairs/coButtons.png",
            "texture/ui/corsairs/tempbag.png",
            "texture/ui/corsairs/trade.png",
            "texture/ui/PublicC.tga",
            "texture/ui/a001.tga",
            "texture/ui/new6.tga",
            "texture/ui/new4.tga",
            "texture/ui/charge.tga",
            "texture/ui/right.tga",
            "texture/ui/SystemBotton3.tga",
            "texture/ui/StartF.dds",
        };

        /// <summary>The item icons, one per name in iteminfo's icon column.</summary>
        private const string Icons = "texture/icon";

        /// <summary>What Unity reads as a texture.</summary>
        private static readonly string[] Extensions = { ".png", ".tga", ".dds", ".bmp", ".jpg" };

        [SerializeField] private string _client = @"C:\work\TalesOfPirateDX9\Client";
        [SerializeField] private bool _icons = true;

        private string _status;

        [MenuItem("Tools/Import UI art")]
        public static void Open()
        {
            var window = GetWindow<UiArtWindow>();

            window.titleContent = new GUIContent("UI art");
            window.minSize = new Vector2(480f, 220f);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("The client's interface art, as sprites", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                _client = EditorGUILayout.TextField("Client folder", _client);

                if (GUILayout.Button("Browse", GUILayout.Width(70f)))
                {
                    var picked = EditorUtility.OpenFolderPanel("The client to take the art from", _client, string.Empty);

                    if (!string.IsNullOrEmpty(picked))
                    {
                        _client = picked;
                    }
                }
            }

            _icons = EditorGUILayout.ToggleLeft($"Item icons as well, {Icons}", _icons);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                $"Copied into {Destination}, each folder under its own name - so " +
                "texture/ui/corsairs/INV/invform.png is loaded as Ui/INV/invform.",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Unity imports .png, .tga and .dds itself, so nothing is decoded here.",
                EditorStyles.miniLabel);

            EditorGUILayout.Space();

            if (GUILayout.Button("Import", GUILayout.Height(24f)))
            {
                Import();
            }

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space();

                EditorGUILayout.HelpBox(_status, MessageType.Info);
            }
        }

        private void Import()
        {
            if (!Directory.Exists(_client))
            {
                _status = $"there is no client folder at {_client}";

                return;
            }

            var sources = new List<string>(Sources);

            if (_icons)
            {
                sources.Add(Icons);
            }

            var imported = new List<string>();
            var missing = new List<string>();

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var source in sources)
                {
                    Take(source, imported, missing);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }

            Configure(imported);

            _status = $"{imported.Count} file(s) are in {Destination}." +
                      (missing.Count == 0 ? string.Empty : $" Not in the client: {string.Join(", ", missing)}.");
        }

        /// <summary>
        /// Takes one entry of the list: a folder of art, or a single file. What is
        /// not there is reported rather than skipped in silence, because a client
        /// that is missing a file is worth knowing about.
        /// </summary>
        private void Take(string source, List<string> imported, List<string> missing)
        {
            var from = Path.Combine(_client, source.Replace('/', Path.DirectorySeparatorChar));

            if (Directory.Exists(from))
            {
                foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
                {
                    Copy(file, Path.GetFileName(Path.GetDirectoryName(file)), imported);
                }

                return;
            }

            if (File.Exists(from))
            {
                Copy(from, null, imported);

                return;
            }

            missing.Add(source);
        }

        private static void Copy(string from, string folder, List<string> imported)
        {
            if (!Extensions.Contains(Path.GetExtension(from).ToLowerInvariant()))
            {
                return;
            }

            var to = string.IsNullOrEmpty(folder)
                ? $"{Destination}/{Path.GetFileName(from)}"
                : $"{Destination}/{folder}/{Path.GetFileName(from)}";

            Directory.CreateDirectory(Path.GetDirectoryName(to));

            File.Copy(from, to, overwrite: true);

            imported.Add(to);
        }

        /// <summary>
        /// Tells Unity what it has just taken in: a sprite, drawn without filtering
        /// or mipmaps, at one pixel per unit - so a pixel of the client's art is a
        /// pixel of the interface, which is the whole point of taking the layout
        /// from the scripts that drew it.
        /// </summary>
        private static void Configure(List<string> imported)
        {
            for (var i = 0; i < imported.Count; i++)
            {
                if (i % 64 == 0)
                {
                    EditorUtility.DisplayProgressBar("Importing UI art", imported[i], (float)i / imported.Count);
                }

                if (!(AssetImporter.GetAtPath(imported[i]) is TextureImporter importer) || Already(importer))
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 1f;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            EditorUtility.ClearProgressBar();
        }

        private static bool Already(TextureImporter importer)
        {
            return importer.textureType == TextureImporterType.Sprite &&
                   importer.spritePixelsPerUnit == 1f &&
                   importer.filterMode == FilterMode.Point &&
                   !importer.mipmapEnabled &&
                   importer.textureCompression == TextureImporterCompression.Uncompressed;
        }
    }
}

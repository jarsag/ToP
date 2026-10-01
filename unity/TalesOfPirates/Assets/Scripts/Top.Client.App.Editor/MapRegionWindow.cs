using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Top.Client.App;
using Top.Client.Game.Tables;
using Top.Client.Game.World;
using Top.Client.Game.World.Terrain;
using Top.Client.Game.World.Water;
using Top.Content;
using Top.Contracts.Tables.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Top.Client.App.Editor
{
    /// <summary>
    /// Builds a slice of a converted map into the open scene so it can be looked
    /// at, and authored against, without entering play mode. Everything it makes
    /// is marked DontSave and torn down on demand, when the window closes and
    /// when play mode starts: the preview must never reach the scene file, and
    /// must never be standing when the runtime builds its own world.
    /// <br/>
    /// Ground and water only. A map's scene objects arrive through GLTFast,
    /// which calls DontDestroyOnLoad while it loads, and Unity forbids that
    /// outside play mode - so those can only be seen by pressing play, or by
    /// baking them into real assets first. The ground is what placing something
    /// on a map needs, and the ground is what this draws.
    /// </summary>
    public class MapRegionWindow : EditorWindow
    {
        /// <summary>
        /// A fixed name, so a build can find and clear whatever an earlier one
        /// left behind - including one left by a recompile, which drops this
        /// window's references but not the objects in the scene.
        /// </summary>
        private const string RootName = "Map region (preview)";

        /// <summary>
        /// The tree to read, relative to the repository root unless it is
        /// absolute - the same field the MapPreview in the scene carries, which
        /// the converter window writes.
        /// </summary>
        [SerializeField] private string _contentRoot = ContentRoot.Default;
        [SerializeField] private int _mapId = 32;
        [SerializeField] private Vector2Int _from = Vector2Int.zero;
        [SerializeField] private Vector2Int _to = new Vector2Int(1, 1);

        private readonly List<Vector2Int> _loaded = new List<Vector2Int>();

        /// <summary>
        /// The map the scene's MapPreview loads, so the window can say when the
        /// one on screen is not the one play mode would build.
        /// </summary>
        private int _sceneMapId;
        private bool _sceneMapKnown;

        private TableSet _tables;
        private TerrainMaterial _terrain;
        private WaterMaterial _water;
        private TerrainLoader _terrainLoader;
        private WaterLoader _waterLoader;

        private Vector2 _scroll;
        private string _status;
        private bool _working;

        [MenuItem("Tools/Map Region Viewer")]
        private static void Open()
        {
            var window = GetWindow<MapRegionWindow>();

            window.titleContent = new GUIContent("Map Region");
            window.minSize = new Vector2(380f, 300f);
        }

        /// <summary>
        /// A recompile drops this window's references but leaves what it built
        /// standing, so sweep those leftovers once the editor has settled.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void SweepLeftovers()
        {
            EditorApplication.delayCall += () =>
            {
                var root = FindRoot();

                if (root != null)
                {
                    DestroyImmediate(root);
                }
            };
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;

            AdoptScene();
        }

        /// <summary>
        /// Follows the scene again whenever the window is come back to, so what
        /// the converter window has since written is what this one reads without
        /// the window being closed and opened.
        /// </summary>
        private void OnFocus()
        {
            AdoptScene();
        }

        /// <summary>
        /// Starts from what the MapPreview in the scene reads - the content
        /// folder and the map - so the window and play mode look at the same
        /// thing without anybody typing it twice. What this window shows is
        /// still its own to choose; it only starts where the scene is.
        /// </summary>
        private void AdoptScene()
        {
            var preview = MapPreview.InScene();

            if (preview == null)
            {
                _sceneMapKnown = false;

                return;
            }

            var serialized = new SerializedObject(preview);
            var root = serialized.FindProperty("_contentRoot")?.stringValue;
            var map = serialized.FindProperty("_mapId");

            if (!string.IsNullOrEmpty(root))
            {
                _contentRoot = root;
            }

            if (map != null)
            {
                _sceneMapId = map.intValue;
                _sceneMapKnown = true;
            }
        }

        /// <summary>
        /// Says which map the scene will actually load, and offers to make it the
        /// one on screen. The two are separate - this window reads the map table,
        /// play mode reads the MapPreview - and a hero placed on the terrain of
        /// one stands on nothing in the other.
        /// </summary>
        private void DrawSceneMap()
        {
            if (!_sceneMapKnown)
            {
                EditorGUILayout.HelpBox("the open scene has no MapPreview to take the folder and map from",
                    MessageType.Info);

                return;
            }

            if (_sceneMapId == _mapId)
            {
                EditorGUILayout.LabelField("The scene loads this map", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.HelpBox($"The scene loads map {_sceneMapId}, not {_mapId}: play mode would " +
                                        "load a different map than the one on screen.", MessageType.Warning);

                if (GUILayout.Button($"Make the scene load map {_mapId}"))
                {
                    WriteMapToScene();
                }
            }

            DrawMapObject();
        }

        /// <summary>
        /// The object the preview sits on is only a folder. The map builds at the
        /// world coordinates the data has, which is where this window draws it,
        /// so an object left somewhere else is harmless - but it reads as if it
        /// mattered, and it is what made the two views disagree. This offers to
        /// put it back where it says nothing.
        /// </summary>
        private void DrawMapObject()
        {
            var preview = MapPreview.InScene();

            if (preview == null)
            {
                return;
            }

            var position = preview.transform.position;

            if (position == Vector3.zero)
            {
                EditorGUILayout.LabelField("The Map object is at the origin, where the map builds",
                    EditorStyles.miniLabel);

                return;
            }

            EditorGUILayout.LabelField($"The Map object sits at ({position.x:0.#}, {position.y:0.#}, " +
                                       $"{position.z:0.#}) - it does not move the map", EditorStyles.miniLabel);

            if (GUILayout.Button("Put the Map object back at the origin"))
            {
                Undo.RecordObject(preview.transform, "Zero the map preview object");

                preview.transform.position = Vector3.zero;
                preview.transform.rotation = Quaternion.identity;

                EditorUtility.SetDirty(preview);
                EditorSceneManager.MarkSceneDirty(preview.gameObject.scene);

                _status = "the Map object is at the origin - the map was already built there";
            }
        }

        /// <summary>
        /// Points the scene's MapPreview at the map this window is showing, so
        /// pressing play loads what was just being looked at. Nothing does this
        /// by itself: the window reads the scene, it does not steer it.
        /// </summary>
        private void WriteMapToScene()
        {
            var preview = MapPreview.InScene();
            var serialized = preview == null ? null : new SerializedObject(preview);
            var field = serialized?.FindProperty("_mapId");

            if (field == null)
            {
                _status = "no MapPreview in the open scene";

                return;
            }

            field.intValue = _mapId;
            serialized.ApplyModifiedProperties();

            EditorUtility.SetDirty(preview);
            EditorSceneManager.MarkSceneDirty(preview.gameObject.scene);

            _sceneMapId = _mapId;
            _sceneMapKnown = true;
            _status = $"the scene's MapPreview now loads map {_mapId}";
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;

            Clear();
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            // Play mode builds the real world around the hero, so a preview left
            // standing would double the map up.
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                Clear();
            }
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawContentRoot();
            DrawMapPicker();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Chunks", EditorStyles.boldLabel);

            _from = EditorGUILayout.Vector2IntField("From", _from);
            _to = EditorGUILayout.Vector2IntField("To", _to);

            if (GUILayout.Button("Whole map"))
            {
                // Deliberately past the end: the build clamps the window to the
                // map, so nobody has to know how many chunks a map has to ask
                // for all of them.
                _from = Vector2Int.zero;
                _to = new Vector2Int(int.MaxValue, int.MaxValue);
            }

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(_working))
            {
                if (GUILayout.Button("Build", GUILayout.Height(26f)))
                {
                    Build();
                }
            }

            if (GUILayout.Button("Clear"))
            {
                Clear();

                _status = null;
            }

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(_status, MessageType.None);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawContentRoot()
        {
            EditorGUILayout.LabelField("Content", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            var root = EditorGUILayout.TextField("Root", _contentRoot);

            if (root != _contentRoot)
            {
                _contentRoot = root;
                _tables = null;
            }

            if (GUILayout.Button("Default", GUILayout.Width(70f)))
            {
                _contentRoot = ContentRoot.Default;
                _tables = null;
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// The map table names every map the content tree holds, so the picker
        /// is a list rather than an id somebody has to look up.
        /// </summary>
        private void DrawMapPicker()
        {
            if (_tables == null)
            {
                using (new EditorGUI.DisabledScope(_working))
                {
                    if (GUILayout.Button(_working ? "Reading tables..." : "Read maps"))
                    {
                        LoadTables();
                    }
                }

                return;
            }

            var maps = _tables.MapTable.ToList();
            var labels = maps.Select(map => $"{map.Id}  {map.Name}  ({map.DisplayName})").ToArray();
            var current = Mathf.Max(0, maps.FindIndex(map => map.Id == _mapId));
            var picked = EditorGUILayout.Popup("Map", current, labels);

            if (picked >= 0 && picked < maps.Count)
            {
                _mapId = maps[picked].Id;
            }

            DrawSceneMap();
        }

        private async void LoadTables()
        {
            _working = true;

            try
            {
                _tables = await new TableReader(Source()).Read();
                _status = $"{_tables.MapTable.Count} maps";
            }
            catch (Exception exception)
            {
                _tables = null;
                _status = "could not read the tables: " + exception.Message;
            }
            finally
            {
                _working = false;

                Pump();
            }
        }

        private async void Build()
        {
            if (_working)
            {
                return;
            }

            _working = true;

            try
            {
                Clear();

                var source = Source();
                var tables = await Tables(source);

                if (tables == null)
                {
                    return;
                }

                if (!tables.MapTable.TryGetById(_mapId, out var entry))
                {
                    _status = $"the map table names no map {_mapId}";

                    return;
                }

                var shaders = Shaders();

                if (shaders == null)
                {
                    _status = "no ShaderSettings - assign one on the MapPreview in the scene";

                    return;
                }

                var terrain = await TerrainMaterial.Load(source, tables.TerrainTable, shaders.Terrain);

                Pump();

                var water = await WaterMaterial.Load(source, shaders.Water);

                Pump();

                var data = await new MapReader(source).Read(entry.MapPath);

                Pump();

                var root = new GameObject(RootName);

                _terrain = terrain;
                _water = water;
                _terrainLoader = new TerrainLoader(data, Group("Terrain", root.transform), terrain.Material);
                _waterLoader = new WaterLoader(data, Group("Water", root.transform), water.Material);

                var from = Clamp(_from, data, 0);
                var to = new Vector2Int(
                    Mathf.Clamp(_to.x, from.x, data.ChunkCountX - 1),
                    Mathf.Clamp(_to.y, from.y, data.ChunkCountY - 1));

                for (var y = from.y; y <= to.y; y++)
                {
                    for (var x = from.x; x <= to.x; x++)
                    {
                        LoadChunk(new Vector2Int(x, y));
                    }
                }

                Hide(root);
                Frame(root);

                _status = $"{entry.Name}: {data.Width}x{data.Height} tiles, " +
                          $"{data.ChunkCountX}x{data.ChunkCountY} chunks, built {_loaded.Count}.\n" +
                          $"World {Bounds(data, from, to)}.\n{FocusLine()}";
            }
            catch (Exception exception)
            {
                _status = "failed: " + exception.Message;

                Debug.LogException(exception);
            }
            finally
            {
                _working = false;

                Pump();
            }
        }

        private void LoadChunk(Vector2Int chunk)
        {
            _loaded.Add(chunk);

            _terrainLoader.Load(chunk);
            _waterLoader.Load(chunk);
        }

        /// <summary>
        /// Takes the preview down. Every chunk goes back through its loader
        /// rather than the root being destroyed outright, because the loaders
        /// are what free the meshes they built.
        /// </summary>
        private void Clear()
        {
            foreach (var chunk in _loaded)
            {
                _waterLoader?.Unload(chunk);
                _terrainLoader?.Unload(chunk);
            }

            _loaded.Clear();

            _waterLoader = null;
            _terrainLoader = null;

            _terrain?.Dispose();
            _water?.Dispose();

            _terrain = null;
            _water = null;

            var root = FindRoot();

            if (root != null)
            {
                DestroyImmediate(root);
            }
        }

        private async Task<TableSet> Tables(IContentSource source)
        {
            if (_tables != null)
            {
                return _tables;
            }

            _tables = await new TableReader(source).Read();

            return _tables;
        }

        private IContentSource Source()
        {
            return new FolderContentSource(ContentRoot.Resolve(_contentRoot));
        }

        /// <summary>
        /// The shaders the map is drawn with, taken from the MapPreview in the
        /// scene so the preview looks like the thing it is a preview of and
        /// nobody has to drag the asset in twice.
        /// </summary>
        private static ShaderSettings Shaders()
        {
            var preview = MapPreview.InScene();
            var field = preview == null ? null : new SerializedObject(preview).FindProperty("_shaders");

            return field?.objectReferenceValue as ShaderSettings;
        }

        private static Vector2Int Clamp(Vector2Int chunk, MapData data, int minimum)
        {
            return new Vector2Int(
                Mathf.Clamp(chunk.x, minimum, data.ChunkCountX - 1),
                Mathf.Clamp(chunk.y, minimum, data.ChunkCountY - 1));
        }

        /// <summary>
        /// Marks the whole build as unsaveable, so a Ctrl+S with a preview
        /// standing writes the scene without it.
        /// </summary>
        private static void Hide(GameObject root)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                transform.gameObject.hideFlags = HideFlags.DontSave;

                foreach (var component in transform.GetComponents<Component>())
                {
                    if (component != null && component is not Transform)
                    {
                        component.hideFlags = HideFlags.DontSave;
                    }
                }
            }
        }

        /// <summary>
        /// Points the scene view at what was just built, so the map is in front
        /// of you rather than somewhere off screen.
        /// </summary>
        private static void Frame(GameObject root)
        {
            var sceneView = SceneView.lastActiveSceneView;
            var renderers = root.GetComponentsInChildren<Renderer>();

            if (sceneView == null || renderers.Length == 0)
            {
                return;
            }

            var bounds = renderers[0].bounds;

            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            sceneView.Frame(bounds, instant: false);
        }

        private static Transform Group(string name, Transform parent)
        {
            var group = new GameObject(name);

            group.transform.SetParent(parent, worldPositionStays: false);

            return group.transform;
        }

        /// <summary>
        /// Where the built region is in world coordinates. The map is the world
        /// - the runtime builds it at these same numbers - so this line is what
        /// makes the two comparable instead of a guess.
        /// </summary>
        private static string Bounds(MapData data, Vector2Int from, Vector2Int to)
        {
            var near = MapSpace.ToWorld(from.x * data.ChunkSize, from.y * data.ChunkSize, 0f);
            var far = MapSpace.ToWorld((to.x + 1) * data.ChunkSize, (to.y + 1) * data.ChunkSize, 0f);

            return $"x {Mathf.Min(near.x, far.x)}..{Mathf.Max(near.x, far.x)}, " +
                   $"z {Mathf.Min(near.z, far.z)}..{Mathf.Max(near.z, far.z)}";
        }

        /// <summary>
        /// Where the object the preview follows stands, in map units as well as
        /// world ones, so a hero standing off the map - or in a chunk nobody
        /// built - is a number rather than a mystery. Not called Focus: that is
        /// an EditorWindow method and hiding it is a trap.
        /// </summary>
        private static string FocusLine()
        {
            var preview = MapPreview.InScene();
            var field = preview == null ? null : new SerializedObject(preview).FindProperty("_focus");
            var focus = field?.objectReferenceValue as Transform;

            if (focus == null)
            {
                return "no focus object to follow";
            }

            var point = MapSpace.ToMap(focus.position);

            return $"follows '{focus.name}' at map ({point.x:0.#}, {point.y:0.#}), " +
                   $"world ({focus.position.x:0.#}, {focus.position.y:0.#}, {focus.position.z:0.#})";
        }

        private static GameObject FindRoot()
        {
            foreach (var candidate in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (candidate != null && candidate.name == RootName)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Touches the window only if it is still there: an await can outlive
        /// the window being closed, and repainting a destroyed one throws.
        /// </summary>
        private void Pump()
        {
            if (this == null)
            {
                return;
            }

            Repaint();
        }
    }
}

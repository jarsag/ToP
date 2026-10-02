using System;
using System.Collections.Generic;
using Top.Client.App;
using Top.Client.Game.Tables;
using Top.Client.Game.World;
using Top.Content;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Top.Client.App.Editor
{
    /// <summary>
    /// Marks the map's safe zones straight onto the terrain: with drawing on, a
    /// diagonal drawn in the scene view - dragged, or clicked corner by corner -
    /// becomes a zone, and everything the zones do not cover is where the hero is
    /// in danger.
    /// <br/>
    /// Both corners are put on the map's own height field, which is the same thing
    /// the terrain is built from and the runtime walks on, so a zone lands on the
    /// hillside it was drawn over. The zones are scene objects and nothing is
    /// baked: the runtime reads them where they stand.
    /// </summary>
    public class SafeZoneWindow : EditorWindow
    {
        private const string RootName = "Safe Zones";
        private const int EdgeSteps = 12;

        /// <summary>How long a diagonal has to be before letting go counts as drawing rather than as a corner.</summary>
        private const float Shortest = 1f;

        /// <summary>How far a click goes looking for ground. The scene view sits well above the map.</summary>
        private const float Reach = 4000f;

        [SerializeField] private string _contentRoot = ContentRoot.Default;
        [SerializeField] private int _mapId = 1;
        [SerializeField] private bool _draw;

        private MapData _data;
        private string _status;
        private bool _working;

        /// <summary>The corner a zone is being drawn from, and whether the button is down between the two.</summary>
        private Vector3? _from;
        private bool _pressing;

        /// <summary>Where the pointer last found ground. Kept because a repaint has no pointer to cast from.</summary>
        private Vector3 _pointer;
        private bool _pointing;

        [MenuItem("Tools/Safe Zones")]
        public static void Open()
        {
            var window = GetWindow<SafeZoneWindow>();

            window.titleContent = new GUIContent("Safe Zones");
            window.minSize = new Vector2(420f, 240f);
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGui;

            Adopt();
            Load();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;

            _from = null;
            _pressing = false;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Ground the hero is safe on, and war everywhere else", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();

            _contentRoot = EditorGUILayout.TextField("Content folder", _contentRoot);
            _mapId = EditorGUILayout.IntField("Map", _mapId);

            if (EditorGUI.EndChangeCheck())
            {
                Load();
            }

            if (GUILayout.Button("Take the folder and the map from the scene's MapPreview"))
            {
                Adopt();
                Load();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Ground", EditorStyles.boldLabel);

            if (_data == null)
            {
                EditorGUILayout.HelpBox(_working ? "reading the map..." : _status, MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField(_status, EditorStyles.miniLabel);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Zones", EditorStyles.boldLabel);

            _draw = EditorGUILayout.ToggleLeft("Draw zones in the scene view", _draw);

            EditorGUILayout.LabelField(_draw
                    ? "Drag a diagonal over the ground, or click one corner and then the opposite one. " +
                      "Esc or the right button gives up the corner."
                    : "Turn drawing on, then draw a zone on the terrain.",
                EditorStyles.miniLabel);

            if (_from != null)
            {
                var corner = MapSpace.ToMap(_from.Value);

                EditorGUILayout.LabelField($"Holding the corner at map ({corner.x:0}, {corner.y:0})",
                    EditorStyles.miniLabel);

                if (GUILayout.Button("Give it up"))
                {
                    _from = null;
                    _pressing = false;

                    SceneView.RepaintAll();
                }
            }

            if (_draw && _data == null)
            {
                EditorGUILayout.HelpBox("A click cannot find the ground until the map is read.", MessageType.Info);
            }

            EditorGUILayout.Space();
            DrawZones();
        }

        private void DrawZones()
        {
            var zones = FindObjectsByType<SafeZone>();

            EditorGUILayout.LabelField(zones.Length == 0
                    ? "No zones: the whole map is safe until the first one is drawn."
                    : $"{zones.Length} zone(s). Everything outside them is war.",
                EditorStyles.miniLabel);

            foreach (var zone in zones)
            {
                if (zone == null)
                {
                    continue;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    var map = MapSpace.ToMap(zone.transform.position);

                    EditorGUILayout.LabelField($"{zone.name}   map ({map.x:0}, {map.y:0})", GUILayout.Width(220f));

                    EditorGUI.BeginChangeCheck();

                    var size = EditorGUILayout.Vector2Field(GUIContent.none, zone.Size, GUILayout.Width(130f));

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(zone, "Resize a safe zone");

                        zone.Size = size;

                        EditorSceneManager.MarkSceneDirty(zone.gameObject.scene);
                    }

                    if (GUILayout.Button("Select", GUILayout.Width(60f)))
                    {
                        Selection.activeGameObject = zone.gameObject;
                    }

                    if (GUILayout.Button("Delete", GUILayout.Width(60f)))
                    {
                        Undo.DestroyObjectImmediate(zone.gameObject);

                        return;
                    }
                }
            }

            if (zones.Length > 0 && GUILayout.Button("Frame the zones"))
            {
                Frame(zones);
            }
        }

        private void OnSceneGui(SceneView view)
        {
            if (_data == null)
            {
                return;
            }

            var repaint = Event.current.type == EventType.Repaint;

            if (repaint)
            {
                Outline();

                if (_draw && _from != null && _pointing)
                {
                    Preview(_from.Value, _pointer);
                }
            }

            Drawing();
        }

        /// <summary>
        /// The zone being drawn. One corner is what a click leaves standing, the
        /// other is where the pointer is when the button comes up, and together
        /// they are the diagonal of the rectangle.
        /// </summary>
        private void Drawing()
        {
            if (!_draw)
            {
                _from = null;
                _pressing = false;

                return;
            }

            var current = Event.current;

            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                _from = null;
                _pressing = false;

                current.Use();

                return;
            }

            if (current.type == EventType.MouseDown && current.button == 1 && _from != null)
            {
                _from = null;
                _pressing = false;

                current.Use();

                return;
            }

            var moving = current.type == EventType.MouseDown || current.type == EventType.MouseUp ||
                         current.type == EventType.MouseDrag || current.type == EventType.MouseMove;

            if (moving)
            {
                _pointing = MapRay.TryHit(_data, HandleUtility.GUIPointToWorldRay(current.mousePosition), Reach,
                    out _pointer);

                SceneView.RepaintAll();
            }

            if (!_pointing)
            {
                return;
            }

            if (current.type == EventType.MouseDown && current.button == 0 && !current.alt &&
                GUIUtility.hotControl == 0)
            {
                // The first corner of a new zone; a corner already standing is
                // closed by this press instead.
                _from = _from ?? _pointer;
                _pressing = true;

                current.Use();

                return;
            }

            if (_from == null || current.type != EventType.MouseUp || current.button != 0 || !_pressing)
            {
                return;
            }

            _pressing = false;

            if (Far(_from.Value, _pointer))
            {
                Place(_from.Value, _pointer);

                _from = null;
            }

            // Let go without moving and the corner stands, waiting for the click
            // that closes the rectangle.
            current.Use();
        }

        /// <summary>
        /// The edge of every zone, walked over the terrain rather than drawn flat:
        /// a rectangle marked on a hillside has to follow it, or it reads as a slab
        /// hanging over the ground it is supposed to describe.
        /// </summary>
        private void Outline()
        {
            Handles.color = new Color(0.3f, 1f, 0.4f, 0.9f);

            foreach (var zone in FindObjectsByType<SafeZone>())
            {
                if (zone != null)
                {
                    Handles.DrawAAPolyLine(3f, Border(MapSpace.ToMap(zone.transform.position), zone.Size));
                }
            }
        }

        private void Preview(Vector3 from, Vector3 to)
        {
            var a = MapSpace.ToMap(from);
            var b = MapSpace.ToMap(to);
            var size = new Vector2(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));

            Handles.color = new Color(0.6f, 1f, 0.7f, 1f);
            Handles.DrawAAPolyLine(4f, Border(new Vector2((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f), size));

            var centre = (from + to) * 0.5f;

            centre.y = _data.HeightAt(-centre.x, centre.z);

            Handles.Label(centre + (Vector3.up * 2f), $"{size.x:0} x {size.y:0} m");
        }

        /// <summary>The outline of a rectangle of the map, sampled so that every point of it lies on the ground.</summary>
        private Vector3[] Border(Vector2 centre, Vector2 size)
        {
            var half = size * 0.5f;

            var a = new Vector2(centre.x - half.x, centre.y - half.y);
            var b = new Vector2(centre.x + half.x, centre.y - half.y);
            var c = new Vector2(centre.x + half.x, centre.y + half.y);
            var d = new Vector2(centre.x - half.x, centre.y + half.y);

            var points = new List<Vector3>();

            Edge(points, a, b);
            Edge(points, b, c);
            Edge(points, c, d);
            Edge(points, d, a);

            return points.ToArray();
        }

        private void Edge(List<Vector3> points, Vector2 from, Vector2 to)
        {
            for (var i = 0; i <= EdgeSteps; i++)
            {
                var map = Vector2.Lerp(from, to, (float)i / EdgeSteps);

                points.Add(MapSpace.ToWorld(map.x, map.y, _data.HeightAt(map.x, map.y)));
            }
        }

        private static bool Far(Vector3 from, Vector3 to)
        {
            return Vector2.Distance(new Vector2(from.x, from.z), new Vector2(to.x, to.z)) >= Shortest;
        }

        private void Place(Vector3 from, Vector3 to)
        {
            var map = MapSpace.ToMap((from + to) * 0.5f);

            var zone = new GameObject("Safe zone");

            zone.transform.SetParent(Root().transform, worldPositionStays: true);
            zone.transform.position = MapSpace.ToWorld(map.x, map.y, _data.HeightAt(map.x, map.y));

            var component = zone.AddComponent<SafeZone>();

            component.Size = new Vector2(Mathf.Abs(to.x - from.x), Mathf.Abs(to.z - from.z));

            Undo.RegisterCreatedObjectUndo(zone, "Mark a safe zone");

            EditorSceneManager.MarkSceneDirty(zone.scene);

            Selection.activeGameObject = zone;

            Repaint();
            SceneView.RepaintAll();
        }

        private static GameObject Root()
        {
            var root = GameObject.Find(RootName);

            if (root != null)
            {
                return root;
            }

            root = new GameObject(RootName);

            Undo.RegisterCreatedObjectUndo(root, "Mark a safe zone");

            return root;
        }

        private static void Frame(SafeZone[] zones)
        {
            var view = SceneView.lastActiveSceneView;

            if (view == null)
            {
                return;
            }

            var bounds = new Bounds(zones[0].transform.position, Vector3.one);

            foreach (var zone in zones)
            {
                if (zone != null)
                {
                    bounds.Encapsulate(new Bounds(zone.transform.position,
                        new Vector3(zone.Size.x, 1f, zone.Size.y)));
                }
            }

            view.Frame(bounds, false);
        }

        /// <summary>
        /// Reads the folder and the map off the preview in the scene: that is the
        /// ground the zones are marked against and the folder the converter wrote.
        /// </summary>
        private void Adopt()
        {
            var preview = MapPreview.InScene();

            if (preview == null)
            {
                return;
            }

            var serialized = new SerializedObject(preview);
            var contentRoot = serialized.FindProperty("_contentRoot");
            var mapId = serialized.FindProperty("_mapId");

            if (contentRoot != null && !string.IsNullOrEmpty(contentRoot.stringValue))
            {
                _contentRoot = contentRoot.stringValue;
            }

            if (mapId != null)
            {
                _mapId = mapId.intValue;
            }
        }

        private async void Load()
        {
            if (_working)
            {
                return;
            }

            _working = true;
            _data = null;
            _status = "reading the tables";

            try
            {
                var source = Source();
                var tables = await new TableReader(source).Read();

                if (!tables.MapTable.TryGetById(_mapId, out var entry))
                {
                    _status = $"the map table names no map {_mapId}";

                    return;
                }

                _status = $"reading {entry.Name}";

                _data = await new MapReader(source).Read(entry.MapPath);
                _status = $"{entry.Name}: {_data.Width}x{_data.Height} tiles, read from " +
                          $"{ContentRoot.Resolve(_contentRoot)}";
            }
            catch (Exception exception)
            {
                _data = null;
                _status = "could not read the map: " + exception.Message;
            }
            finally
            {
                _working = false;

                Repaint();
                SceneView.RepaintAll();
            }
        }

        private IContentSource Source()
        {
            return new FolderContentSource(ContentRoot.Resolve(_contentRoot));
        }
    }
}

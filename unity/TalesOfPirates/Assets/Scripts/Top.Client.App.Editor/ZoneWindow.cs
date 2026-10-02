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
    /// Marks the map's zones straight onto the terrain: with drawing on, a diagonal
    /// drawn in the scene view - dragged, or clicked corner by corner - becomes a
    /// zone of the kind being drawn.
    /// <br/>
    /// Both corners are put on the map's height field, which is the same thing the
    /// terrain is built from and the runtime walks on, so a zone lands on the
    /// hillside it was drawn over. The zones are scene objects and nothing is baked:
    /// the runtime reads them where they stand.
    /// </summary>
    public class ZoneWindow : EditorWindow
    {
        /// <summary>What the root of the drawn zones is called, and what it used to be called.</summary>
        private static readonly string[] Roots = { "Zones", "Safe Zones" };

        /// <summary>How far apart the lines drawn over a safe zone are, in map units.</summary>
        private const float Grid = 8f;

        /// <summary>How far apart a drawn line is sampled for ground, in map units.</summary>
        private const float Sample = 2f;

        /// <summary>How long a diagonal has to be before letting go counts as drawing rather than as a corner.</summary>
        private const float Shortest = 1f;

        /// <summary>
        /// How wide a spawn point is. It is a place rather than an area - somewhere to be
        /// put down - so it is always the same size and one click is enough to mark it.
        /// </summary>
        private const float SpawnSize = 2f;

        /// <summary>How far a click goes looking for ground. The scene view sits well above the map.</summary>
        private const float Reach = 4000f;

        [SerializeField] private string _contentRoot = ContentRoot.Default;
        [SerializeField] private int _mapId = 1;
        [SerializeField] private ZoneKind _kind = ZoneKind.Safe;
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

        [MenuItem("Tools/Zones")]
        public static void Open()
        {
            var window = GetWindow<ZoneWindow>();

            window.titleContent = new GUIContent("Zones");
            window.minSize = new Vector2(440f, 260f);
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

            UnityEditor.Tools.hidden = false;

            _from = null;
            _pressing = false;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Zones of the map: where the hero is safe, and where he may start",
                EditorStyles.boldLabel);
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

            _kind = (ZoneKind)EditorGUILayout.EnumPopup("New zone kind", _kind);
            _draw = EditorGUILayout.ToggleLeft("Draw zones in the scene view", _draw);

            EditorGUILayout.LabelField(_draw
                    ? _kind == ZoneKind.Spawn
                        ? "Click the ground to put a spawn point down: it is always two metres across. " +
                          "Esc or the right button gives up nothing, because nothing is being held."
                        : "Drag a diagonal over the ground, or click one corner and then the opposite one. " +
                          "Esc or the right button gives up the corner. Drawing hides the transform tools."
                    : "Turn drawing on, then draw a zone on the terrain. The transform tools come back " +
                      "when it is off.",
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
            var zones = FindObjectsByType<Zone>();
            var safe = 0;
            var spawns = 0;

            foreach (var zone in zones)
            {
                if (zone == null)
                {
                    continue;
                }

                if (zone.Kind == ZoneKind.Spawn)
                {
                    spawns++;
                }
                else
                {
                    safe++;
                }
            }

            EditorGUILayout.LabelField(safe == 0
                    ? "No safe zones: the whole map is safe until the first one is drawn."
                    : $"{safe} safe zone(s). Everything outside them is war.",
                EditorStyles.miniLabel);

            EditorGUILayout.LabelField(spawns == 0
                    ? "No spawn points: the hero starts where the scene puts him."
                    : $"{spawns} spawn point(s): the hero starts on one of them, picked at random.",
                EditorStyles.miniLabel);

            EditorGUILayout.Space();

            foreach (var zone in zones)
            {
                if (zone == null)
                {
                    continue;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    var map = MapSpace.ToMap(zone.transform.position);

                    EditorGUI.BeginChangeCheck();

                    var kind = (ZoneKind)EditorGUILayout.EnumPopup(zone.Kind, GUILayout.Width(70f));

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(zone, "Change a zone's kind");

                        zone.Kind = kind;

                        EditorSceneManager.MarkSceneDirty(zone.gameObject.scene);
                        SceneView.RepaintAll();
                    }

                    EditorGUILayout.LabelField($"map ({map.x:0}, {map.y:0})", GUILayout.Width(150f));

                    EditorGUI.BeginChangeCheck();

                    var size = EditorGUILayout.Vector2Field(GUIContent.none, zone.Size, GUILayout.Width(120f));

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(zone, "Resize a zone");

                        zone.Size = size;

                        EditorSceneManager.MarkSceneDirty(zone.gameObject.scene);
                        SceneView.RepaintAll();
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
                UnityEditor.Tools.hidden = false;

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

            var current = Event.current;

            // While drawing, the scene view's own tools have to keep their hands
            // off the left button: without this a click picks up whatever object is
            // under it and the drag that should draw a zone moves that object
            // instead. Alt, the right button and the middle button are left alone,
            // so the view still orbits, pans and zooms.
            var leftish = current.type == EventType.Repaint || current.type == EventType.Layout ||
                          current.button == 0;

            if (_draw && !current.alt && leftish)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
            }

            // And the transform gizmos go away with them, so nothing under the
            // pointer can be grabbed by its handle either.
            UnityEditor.Tools.hidden = _draw;

            Drawing();
        }

        /// <summary>
        /// The zone being drawn. One corner is what a click leaves standing, the
        /// other is where the pointer is when the button comes up, and together they
        /// are the diagonal of what the kind's shape is fitted to.
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

            if (_kind == ZoneKind.Spawn)
            {
                // A spawn point is put down rather than drawn: one click marks the place,
                // at the size a spawn point always is.
                _from = null;
                _pressing = false;

                if (current.type == EventType.MouseDown && current.button == 0 && !current.alt && _pointing)
                {
                    PlaceAt(_pointer, new Vector2(SpawnSize, SpawnSize));

                    current.Use();
                }

                return;
            }

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

            if (current.type == EventType.MouseDown && current.button == 0 && !current.alt)
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
            // that closes the shape.
            current.Use();
        }

        /// <summary>
        /// Every zone drawn on the ground it covers: the edge says how far it
        /// reaches, and the lines across it say which ground that is - which a flat
        /// rectangle cannot say on a hillside.
        /// </summary>
        private void Outline()
        {
            foreach (var zone in FindObjectsByType<Zone>())
            {
                if (zone == null)
                {
                    continue;
                }

                var centre = MapSpace.ToMap(zone.transform.position);
                var colour = Zone.Colour(zone.Kind);

                Handles.color = colour;
                Handles.DrawAAPolyLine(3f, Shape(centre, zone.Size, zone.Kind));

                if (zone.Kind != ZoneKind.Safe)
                {
                    continue;
                }

                var half = zone.Size * 0.5f;
                var across = Mathf.Max(1, Mathf.RoundToInt(zone.Size.x / Grid));
                var down = Mathf.Max(1, Mathf.RoundToInt(zone.Size.y / Grid));

                colour.a = 0.3f;
                Handles.color = colour;

                for (var i = 1; i < across; i++)
                {
                    var x = Mathf.Lerp(centre.x - half.x, centre.x + half.x, (float)i / across);

                    Handles.DrawAAPolyLine(1.5f, Line(new Vector2(x, centre.y - half.y),
                        new Vector2(x, centre.y + half.y)));
                }

                for (var i = 1; i < down; i++)
                {
                    var y = Mathf.Lerp(centre.y - half.y, centre.y + half.y, (float)i / down);

                    Handles.DrawAAPolyLine(1.5f, Line(new Vector2(centre.x - half.x, y),
                        new Vector2(centre.x + half.x, y)));
                }
            }
        }

        private void Preview(Vector3 from, Vector3 to)
        {
            var a = MapSpace.ToMap(from);
            var b = MapSpace.ToMap(to);
            var centre = new Vector2((a.x + b.x) * 0.5f, (a.y + b.y) * 0.5f);
            var size = new Vector2(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));

            Handles.color = Zone.Colour(_kind);
            Handles.DrawAAPolyLine(4f, Shape(centre, size, _kind));

            var label = _kind == ZoneKind.Spawn
                ? $"r {Mathf.Min(size.x, size.y) * 0.5f:0} m"
                : $"{size.x:0} x {size.y:0} m";

            Handles.Label(Ground(centre) + (Vector3.up * 2f), label);
        }

        /// <summary>The shape a kind is drawn as, which is the shape it is tested as: a rectangle, or a circle.</summary>
        private Vector3[] Shape(Vector2 centre, Vector2 size, ZoneKind kind)
        {
            if (kind == ZoneKind.Spawn)
            {
                return Circle(centre, Mathf.Min(size.x, size.y) * 0.5f);
            }

            return Border(centre, size);
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

            points.AddRange(Line(a, b));
            points.AddRange(Line(b, c));
            points.AddRange(Line(c, d));
            points.AddRange(Line(d, a));

            return points.ToArray();
        }

        /// <summary>A circle of the map, walked in steps so that it follows the ground it is drawn on.</summary>
        private Vector3[] Circle(Vector2 centre, float radius)
        {
            const int Steps = 32;

            var points = new Vector3[Steps + 1];

            for (var i = 0; i <= Steps; i++)
            {
                var angle = (float)i / Steps * Mathf.PI * 2f;

                points[i] = Ground(centre + (new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius));
            }

            return points;
        }

        /// <summary>
        /// A straight line of the map, walked in steps so that it lies on the ground
        /// a body would stand on rather than on the sea beside a bridge.
        /// </summary>
        private Vector3[] Line(Vector2 from, Vector2 to)
        {
            var steps = Mathf.Max(2, Mathf.RoundToInt(Vector2.Distance(from, to) / Sample));
            var points = new Vector3[steps + 1];

            for (var i = 0; i <= steps; i++)
            {
                points[i] = Ground(Vector2.Lerp(from, to, (float)i / steps));
            }

            return points;
        }

        private Vector3 Ground(Vector2 map)
        {
            return MapSpace.ToWorld(map.x, map.y, _data.SurfaceAt(map.x, map.y));
        }

        private static bool Far(Vector3 from, Vector3 to)
        {
            return Vector2.Distance(new Vector2(from.x, from.z), new Vector2(to.x, to.z)) >= Shortest;
        }

        private void Place(Vector3 from, Vector3 to)
        {
            PlaceAt((from + to) * 0.5f, new Vector2(Mathf.Abs(to.x - from.x), Mathf.Abs(to.z - from.z)));
        }

        /// <summary>Puts a zone of a size down on a place on the ground, on the map's own surface.</summary>
        private void PlaceAt(Vector3 ground, Vector2 size)
        {
            var map = MapSpace.ToMap(ground);

            var zone = new GameObject(_kind == ZoneKind.Spawn ? "Spawn point" : "Safe zone");

            zone.transform.SetParent(Root().transform, worldPositionStays: true);
            zone.transform.position = MapSpace.ToWorld(map.x, map.y, _data.SurfaceAt(map.x, map.y));

            var component = zone.AddComponent<Zone>();

            component.Kind = _kind;
            component.Size = size;

            Undo.RegisterCreatedObjectUndo(zone, "Mark a zone");

            EditorSceneManager.MarkSceneDirty(zone.scene);

            Selection.activeGameObject = zone;

            Repaint();
            SceneView.RepaintAll();
        }

        private static GameObject Root()
        {
            foreach (var name in Roots)
            {
                var found = GameObject.Find(name);

                if (found != null)
                {
                    return found;
                }
            }

            var root = new GameObject(Roots[0]);

            Undo.RegisterCreatedObjectUndo(root, "Mark a zone");

            return root;
        }

        private static void Frame(Zone[] zones)
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

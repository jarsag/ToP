using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Top.Client.App;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Top.Client.App.Editor
{
    /// <summary>
    /// Runs the converter from inside the editor: pick a client root, a family
    /// and the one unit of it to convert, and write the tree the preview reads.
    /// <br/>
    /// The window owns no conversion of its own - it starts the same command
    /// line program the terminal and the wizard start, and shows what that
    /// program printed. So there is one converter, and this is a front end to
    /// it rather than a second implementation that can drift from it.
    /// </summary>
    public class ConverterWindow : EditorWindow
    {
        private const string DllName = "Top.Conversion.Cli.dll";
        private const string Project = "src/Top.Conversion.Cli/Top.Conversion.Cli.csproj";

        /// <summary>
        /// The family a map belongs to. A map's id is the one the scene loads it
        /// by, so converting one also writes that id into the scene.
        /// </summary>
        private const string MapKind = "map";

        /// <summary>
        /// How many entries of a family are drawn at once. The client has
        /// thousands, and an IMGUI list of thousands is a frame per keystroke;
        /// the filter is what gets to the right one.
        /// </summary>
        private const int MaxRows = 200;

        /// <summary>
        /// How many lines of the converter's output are kept.
        /// </summary>
        private const int MaxLogLines = 400;

        private static readonly string[] Configurations = { "debug", "release" };

        [SerializeField] private string _repository = string.Empty;
        [SerializeField] private string _configuration = "debug";
        [SerializeField] private string _contentRoot = ContentRoot.Default;
        [SerializeField] private string _near = string.Empty;
        [SerializeField] private string _source = string.Empty;
        [SerializeField] private string _filter = string.Empty;
        [SerializeField] private int _section;
        [SerializeField] private string _selected = string.Empty;

        /// <summary>
        /// Whether converting a map also converts the objects standing on it,
        /// which is what the converter does unless asked not to.
        /// </summary>
        [SerializeField] private bool _objects = true;

        private readonly List<RootInfo> _clients = new List<RootInfo>();
        private readonly List<SectionInfo> _sections = new List<SectionInfo>();
        private readonly List<string> _lines = new List<string>();
        private readonly ConcurrentQueue<string> _out = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<string> _errors = new ConcurrentQueue<string>();
        private readonly StringBuilder _stdout = new StringBuilder();

        private Process _process;
        private bool _busy;
        private bool _listing;
        private string _status;
        private string _success;
        private string _progressKind;
        private int _progressIndex;
        private int _progressCount;

        private Vector2 _scroll;
        private Vector2 _entries;
        private Vector2 _log;

        private int _sceneMapId;
        private bool _sceneMapIdKnown;

        /// <summary>
        /// The map a conversion is about to write, held until the converter
        /// says it succeeded. Pointing the scene at a map before it exists
        /// leaves the preview loading nothing at all.
        /// </summary>
        private int _pendingMapId;
        private bool _pendingMap;

        [MenuItem("Tools/Content Converter")]
        private static void Open()
        {
            var window = GetWindow<ConverterWindow>();

            window.titleContent = new GUIContent("Converter");
            window.minSize = new Vector2(460f, 480f);
        }

        private void OnEnable()
        {
            EditorApplication.update += Tick;

            if (string.IsNullOrEmpty(_repository))
            {
                _repository = ContentRoot.Repository;
            }

            if (string.IsNullOrEmpty(_near))
            {
                _near = _repository;
            }
        }

        /// <summary>
        /// The map the scene loads can be changed in the Inspector, so reading
        /// it is left until the window is come back to.
        /// </summary>
        private void OnFocus()
        {
            _sceneMapIdKnown = false;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;

            // A conversion left running after the window is gone would write a
            // tree nobody is watching, with no way to stop it from here.
            Cancel();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawConverter();
            DrawClient();
            DrawContent();
            DrawCategories();
            DrawEntries();
            DrawProgress();
            DrawLog();

            EditorGUILayout.EndScrollView();
        }

        private void DrawConverter()
        {
            EditorGUILayout.LabelField("Converter", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            _repository = EditorGUILayout.TextField("Repository", _repository);

            if (GUILayout.Button("Default", GUILayout.Width(70f)))
            {
                _repository = ContentRoot.Repository;
                _near = _repository;
            }

            EditorGUILayout.EndHorizontal();

            var current = Mathf.Max(0, Array.IndexOf(Configurations, _configuration));

            _configuration = Configurations[EditorGUILayout.Popup("Configuration", current, Configurations)];

            EditorGUILayout.LabelField("Program", Dll(), EditorStyles.miniLabel);

            using (new EditorGUI.DisabledScope(_busy))
            {
                if (GUILayout.Button("Build converter"))
                {
                    Build();
                }
            }
        }

        private void DrawClient()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Client", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            _near = EditorGUILayout.TextField("Near", _near);

            using (new EditorGUI.DisabledScope(_busy))
            {
                if (GUILayout.Button("Scan", GUILayout.Width(70f)))
                {
                    Scan();
                }
            }

            EditorGUILayout.EndHorizontal();

            if (_clients.Count > 0)
            {
                var labels = _clients
                    .Select(client => $"{client.Path}  ({client.Description})")
                    .ToArray();
                var index = Mathf.Max(0, _clients.FindIndex(client => client.Path == _source));
                var picked = EditorGUILayout.Popup("Found", index, labels);

                if (picked != index)
                {
                    _source = _clients[picked].Path;
                }
            }

            EditorGUILayout.BeginHorizontal();

            _source = EditorGUILayout.TextField("Root", _source);

            using (new EditorGUI.DisabledScope(_busy))
            {
                if (GUILayout.Button("Load", GUILayout.Width(70f)))
                {
                    Load();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawContent()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Content", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            _contentRoot = EditorGUILayout.TextField("Folder", _contentRoot);

            if (GUILayout.Button("Default", GUILayout.Width(70f)))
            {
                _contentRoot = ContentRoot.Default;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Read and written as", ContentRoot.Resolve(_contentRoot), EditorStyles.miniLabel);

            var mapId = SceneMapId();

            EditorGUILayout.LabelField("The scene loads map",
                mapId == 0 ? "none - no MapPreview in the open scene" : mapId.ToString(),
                EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("From the scene"))
            {
                AdoptSceneRoot();
            }

            if (GUILayout.Button("Into the scene"))
            {
                ApplyToScene();

                _status = $"the scene's MapPreview reads '{_contentRoot}'";
            }

            var chosenMap = ChosenMap();

            if (chosenMap.HasValue && GUILayout.Button($"Load map {chosenMap.Value}"))
            {
                ApplyMapId(chosenMap.Value);

                _status = $"the scene's MapPreview loads map {chosenMap.Value}";
            }

            if (GUILayout.Button("Open folder"))
            {
                var folder = ContentRoot.Resolve(_contentRoot);

                if (Directory.Exists(folder))
                {
                    EditorUtility.RevealInFinder(folder);
                }
                else
                {
                    _status = $"no folder at '{folder}' yet";
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawCategories()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Category", EditorStyles.boldLabel);

            if (_sections.Count == 0)
            {
                EditorGUILayout.HelpBox("Scan and load a client to see what it offers.", MessageType.None);

                return;
            }

            var index = Mathf.Clamp(_section, 0, _sections.Count - 1);
            var labels = _sections.Select(section => section.Summary).ToArray();

            _section = EditorGUILayout.Popup("Family", index, labels);

            var current = Current;

            if (current != null && !current.Available)
            {
                EditorGUILayout.HelpBox(current.Trouble, MessageType.Warning);
            }
        }

        private void DrawEntries()
        {
            var current = Current;

            if (current == null || !current.Available)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Units", EditorStyles.boldLabel);

            _filter = EditorGUILayout.TextField("Filter", _filter);

            var matches = current.Entries.Where(Matches).ToList();

            EditorGUILayout.LabelField(matches.Count == current.Entries.Count
                ? $"{matches.Count} units"
                : $"{matches.Count} of {current.Entries.Count} units match '{_filter}'");

            _entries = EditorGUILayout.BeginScrollView(_entries, GUILayout.Height(180f));

            foreach (var entry in matches.Take(MaxRows))
            {
                var selected = Key(entry) == _selected;

                if (GUILayout.Toggle(selected, entry.Label, EditorStyles.miniButton) != selected)
                {
                    _selected = selected ? string.Empty : Key(entry);
                }
            }

            if (matches.Count > MaxRows)
            {
                EditorGUILayout.LabelField($"{matches.Count - MaxRows} more - narrow the filter", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndScrollView();

            var chosen = Chosen();

            if (chosen != null && current.Kind == MapKind && chosen.Id != 0)
            {
                EditorGUILayout.LabelField(
                    $"mapinfo id {chosen.Id} - the number the scene loads this map by", EditorStyles.miniLabel);
            }

            if (current.Kind == MapKind)
            {
                _objects = EditorGUILayout.Toggle("Bring the objects standing on it", _objects);
            }

            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(_busy || chosen == null))
            {
                if (GUILayout.Button(chosen == null ? "Convert selected" : $"Convert '{chosen.Name}'"))
                {
                    ConvertOne(chosen);
                }
            }

            using (new EditorGUI.DisabledScope(_busy))
            {
                if (GUILayout.Button($"Convert every {current.Kind} ({current.Entries.Count})"))
                {
                    ConvertFamily(current.Kind);
                }
            }

            if (_busy && GUILayout.Button("Cancel"))
            {
                Cancel();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawProgress()
        {
            if (_progressCount > 0)
            {
                var rect = EditorGUILayout.GetControlRect(false, 18f);

                EditorGUI.ProgressBar(rect, (float)_progressIndex / _progressCount,
                    $"{_progressKind} {_progressIndex}/{_progressCount}");
            }

            if (string.IsNullOrEmpty(_status))
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(_status, MessageType.None);
        }

        private void DrawLog()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);

            if (_busy)
            {
                _log.y = float.MaxValue;
            }

            _log = EditorGUILayout.BeginScrollView(_log, GUILayout.Height(150f));

            foreach (var line in _lines)
            {
                EditorGUILayout.LabelField(line, EditorStyles.miniLabel);
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Clear log"))
            {
                _lines.Clear();
                _status = null;
            }
        }

        /// <summary>
        /// Builds the converter, so the program the window runs exists and is
        /// the one the current sources make.
        /// </summary>
        private void Build()
        {
            _status = "building...";
            _success = "the converter is built";

            Launch($"build \"{ProjectPath()}\" -c {_configuration}", listing: false);
        }

        /// <summary>
        /// Looks for client roots around the near folder, which is how anybody
        /// finds out that yesterday's checkout is one.
        /// </summary>
        private void Scan()
        {
            if (string.IsNullOrEmpty(_near))
            {
                _near = Repository;
            }

            _status = "looking for clients...";

            RunCli($"--list clients --near \"{_near}\" --json", listing: true);
        }

        /// <summary>
        /// Reads what the chosen client offers: the families, and the units of
        /// each that parsed.
        /// </summary>
        private void Load()
        {
            if (string.IsNullOrEmpty(_source))
            {
                _status = "pick a client root first";

                return;
            }

            _status = "reading the client...";

            RunCli($"--list catalog --source \"{_source}\" --json", listing: true);
        }

        private void ConvertOne(EntryInfo entry)
        {
            if (!Ready(out var reason))
            {
                _status = reason;

                return;
            }

            // The folder goes into the scene first, so what is about to be
            // written is what play mode will read. A map is loaded by its
            // mapinfo id rather than by its file, so converting one points the
            // scene at it too: what was written is what gets loaded.
            ApplyToScene();

            var mapId = Current.Kind == MapKind && entry.Id != 0 ? entry.Id : (int?)null;

            _pendingMap = mapId.HasValue;
            _pendingMapId = mapId ?? 0;

            var objects = _objects ? string.Empty : " --no-objects";

            _status = $"converting {entry.Name}...";
            _success = mapId.HasValue
                ? $"converted map {mapId.Value} '{entry.Name}'" +
                  $"{(_objects ? " with the objects standing on it" : string.Empty)} - " +
                  $"the preview reads '{ContentRoot.Resolve(_contentRoot)}'"
                : $"converted '{entry.Name}' - the preview reads '{ContentRoot.Resolve(_contentRoot)}'";

            RunCli($"--source \"{_source}\" --out \"{_contentRoot}\" " +
                   $"--kind {Current.Kind} --unit \"{Key(entry)}\"{objects}", listing: false);
        }

        private void ConvertFamily(string kind)
        {
            if (!Ready(out var reason))
            {
                _status = reason;

                return;
            }

            ApplyToScene();

            _status = $"converting every {kind}...";
            _success = $"converted every {kind} - the preview reads '{ContentRoot.Resolve(_contentRoot)}'";

            RunCli($"--source \"{_source}\" --out \"{_contentRoot}\" --kinds {kind}" +
                   $"{(_objects ? string.Empty : " --no-objects")}", listing: false);
        }

        /// <summary>
        /// Starts the converter itself, which is what makes this window a front
        /// end rather than a second implementation.
        /// </summary>
        private void RunCli(string arguments, bool listing)
        {
            var dll = Dll();

            if (!File.Exists(dll))
            {
                _status = $"no converter at '{dll}' - press Build converter";

                return;
            }

            Launch($"\"{dll}\" {arguments}", listing);
        }

        /// <summary>
        /// Starts a dotnet process. Deliberately not called Start: Unity reads
        /// that name as its own message and refuses any signature but an empty
        /// one.
        /// </summary>
        private void Launch(string arguments, bool listing)
        {
            if (_busy)
            {
                return;
            }

            _lines.Clear();
            _stdout.Clear();
            _progressKind = null;
            _progressIndex = 0;
            _progressCount = 0;
            _busy = true;
            _listing = listing;

            var info = new ProcessStartInfo("dotnet", arguments)
            {
                WorkingDirectory = Repository,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            try
            {
                _process = Process.Start(info);
            }
            catch (Exception exception)
            {
                _busy = false;
                _status = "could not start dotnet: " + exception.Message;

                return;
            }

            _process.OutputDataReceived += (sender, line) => Enqueue(_out, line.Data);
            _process.ErrorDataReceived += (sender, line) => Enqueue(_errors, line.Data);
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        /// <summary>
        /// Collects what the converter says and, when it is done, reads a
        /// listing out of it. Everything the process writes arrives on another
        /// thread, so it is queued there and taken here, where the editor can be
        /// touched.
        /// </summary>
        private void Tick()
        {
            Drain();

            if (_process == null)
            {
                return;
            }

            if (!_process.HasExited)
            {
                Repaint();

                return;
            }

            // WaitForExit after the async readers have fired is what guarantees
            // every line was handed over before the listing is parsed.
            _process.WaitForExit();

            Drain();

            var exit = _process.ExitCode;

            _process.Dispose();
            _process = null;
            _busy = false;

            if (_listing)
            {
                Parse(_stdout.ToString());
            }
            else if (exit != 0)
            {
                // The scene keeps the map it had: the one that failed is not in
                // the tree, and pointing the preview at it would load nothing.
                _status = $"the converter exited with {exit}";
            }
            else if (_pendingMap)
            {
                ApplyMapId(_pendingMapId);

                _status = $"{_success} - the scene now loads map {_pendingMapId}";
            }
            else
            {
                _status = _success ?? "done";
            }

            _pendingMap = false;

            Repaint();
        }

        private void Drain()
        {
            while (_out.TryDequeue(out var line))
            {
                _stdout.AppendLine(line);

                Note(line);
            }

            while (_errors.TryDequeue(out var line))
            {
                Note(line);
            }
        }

        private void Note(string line)
        {
            _lines.Add(line);

            while (_lines.Count > MaxLogLines)
            {
                _lines.RemoveAt(0);
            }

            NoteProgress(line);
        }

        /// <summary>
        /// Picks up the converter's "family done/total" progress lines, so the
        /// window can show a bar without the converter having to speak a second
        /// language for a window.
        /// </summary>
        private void NoteProgress(string line)
        {
            var parts = line.Split(' ');

            if (parts.Length != 2)
            {
                return;
            }

            var counters = parts[1].Split('/');

            if (counters.Length != 2 ||
                !int.TryParse(counters[0], out var index) ||
                !int.TryParse(counters[1], out var count))
            {
                return;
            }

            _progressKind = parts[0];
            _progressIndex = index;
            _progressCount = count;
        }

        private void Parse(string json)
        {
            Payload payload;

            try
            {
                payload = JsonUtility.FromJson<Payload>(json);
            }
            catch (Exception exception)
            {
                _status = "could not read the converter's answer: " + exception.Message;

                return;
            }

            if (payload == null)
            {
                _status = "the converter printed nothing to read";

                return;
            }

            if (payload.Roots != null && payload.Roots.Count > 0)
            {
                _clients.Clear();
                _clients.AddRange(payload.Roots);

                if (string.IsNullOrEmpty(_source))
                {
                    _source = _clients[0].Path;
                }

                _status = $"{_clients.Count} client roots";
            }

            if (payload.Sections != null && payload.Sections.Count > 0)
            {
                _sections.Clear();
                _sections.AddRange(payload.Sections);

                // JsonUtility leaves a missing list null rather than empty, and
                // a listing without one is not worth a null check everywhere
                // else.
                foreach (var section in _sections)
                {
                    if (section.Entries == null)
                    {
                        section.Entries = new List<EntryInfo>();
                    }
                }

                _section = Mathf.Clamp(_section, 0, _sections.Count - 1);

                var available = _sections.Count(section => section.Available);

                _status = $"{available} of {_sections.Count} families can be converted from '{payload.Source}'";
            }
        }

        private bool Ready(out string reason)
        {
            reason = null;

            if (string.IsNullOrEmpty(_source))
            {
                reason = "pick a client root first";
            }
            else if (!Directory.Exists(_source))
            {
                reason = $"no client root at '{_source}'";
            }
            else if (Current == null)
            {
                reason = "load the client first";
            }

            return reason == null;
        }

        /// <summary>
        /// Writes the folder into the MapPreview in the open scene, so play mode
        /// reads what this window just wrote. Without it the setting would be a
        /// promise the window does not keep.
        /// </summary>
        private void ApplyToScene()
        {
            var preview = MapPreview.InScene();

            if (preview == null)
            {
                return;
            }

            var serialized = new SerializedObject(preview);
            var field = serialized.FindProperty("_contentRoot");

            if (field == null || field.stringValue == _contentRoot)
            {
                return;
            }

            field.stringValue = _contentRoot;
            serialized.ApplyModifiedProperties();

            EditorUtility.SetDirty(preview);
            EditorSceneManager.MarkSceneDirty(preview.gameObject.scene);
        }

        /// <summary>
        /// Writes the map's id into the MapPreview of the open scene. The id,
        /// not the file name, is what the scene loads a map by, so a converted
        /// map is only the map being looked at once this agrees with it.
        /// </summary>
        private void ApplyMapId(int mapId)
        {
            _sceneMapId = mapId;
            _sceneMapIdKnown = true;

            var preview = MapPreview.InScene();

            if (preview == null)
            {
                return;
            }

            var serialized = new SerializedObject(preview);
            var field = serialized.FindProperty("_mapId");

            if (field == null || field.intValue == mapId)
            {
                return;
            }

            field.intValue = mapId;
            serialized.ApplyModifiedProperties();

            EditorUtility.SetDirty(preview);
            EditorSceneManager.MarkSceneDirty(preview.gameObject.scene);
        }

        /// <summary>
        /// The map the scene's preview loads, zero when the open scene has no
        /// preview or none assigned.
        /// </summary>
        private int SceneMapId()
        {
            if (_sceneMapIdKnown)
            {
                return _sceneMapId;
            }

            var preview = MapPreview.InScene();
            var field = preview == null ? null : new SerializedObject(preview).FindProperty("_mapId");

            _sceneMapId = field == null ? 0 : field.intValue;
            _sceneMapIdKnown = true;

            return _sceneMapId;
        }

        /// <summary>
        /// The id of the chosen unit when it is a map's, so the one button that
        /// writes it into the scene appears only where it means something.
        /// </summary>
        private int? ChosenMap()
        {
            var current = Current;

            if (current == null || current.Kind != MapKind)
            {
                return null;
            }

            var chosen = Chosen();

            return chosen == null || chosen.Id == 0 ? null : chosen.Id;
        }

        private void AdoptSceneRoot()
        {
            var preview = MapPreview.InScene();
            var field = preview == null ? null : new SerializedObject(preview).FindProperty("_contentRoot");
            var root = field?.stringValue;

            if (string.IsNullOrEmpty(root))
            {
                _status = "no MapPreview with a content folder in the open scene";

                return;
            }

            _contentRoot = root;
            _status = $"the scene's MapPreview reads '{root}'";
        }

        private void Cancel()
        {
            var process = _process;

            _process = null;
            _busy = false;
            _pendingMap = false;

            if (process == null)
            {
                return;
            }

            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("could not stop the converter: " + exception.Message);
            }
            finally
            {
                process.Dispose();
            }

            _status = "cancelled";
        }

        private static void Enqueue(ConcurrentQueue<string> queue, string line)
        {
            if (line != null)
            {
                queue.Enqueue(line);
            }
        }

        private SectionInfo Current
        {
            get
            {
                return _sections.Count == 0
                    ? null
                    : _sections[Mathf.Clamp(_section, 0, _sections.Count - 1)];
            }
        }

        private EntryInfo Chosen()
        {
            var current = Current;

            return current == null
                ? null
                : current.Entries.FirstOrDefault(entry => Key(entry) == _selected);
        }

        /// <summary>
        /// What names an entry to the converter: the id where the family numbers
        /// its units, the name where it does not - which is how a map is named,
        /// by the stem of its file.
        /// </summary>
        private static string Key(EntryInfo entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            return entry.Id != 0 ? entry.Id.ToString() : entry.Name;
        }

        private bool Matches(EntryInfo entry)
        {
            if (string.IsNullOrWhiteSpace(_filter))
            {
                return true;
            }

            var filter = _filter.Trim();

            return Contains(entry.Name, filter) ||
                   Contains(entry.Detail, filter) ||
                   Contains(entry.Label, filter);
        }

        private static bool Contains(string text, string filter)
        {
            return text != null && text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string Repository
        {
            get { return string.IsNullOrEmpty(_repository) ? ContentRoot.Repository : Path.GetFullPath(_repository); }
        }

        private string Dll()
        {
            return Path.Combine(Repository, "artifacts", "bin", "Top.Conversion.Cli", _configuration, DllName);
        }

        private string ProjectPath()
        {
            return Path.Combine(Repository, Project);
        }

        // JsonUtility fills these by name through reflection, so they are public
        // fields with the writer's names and never assigned in code.

#pragma warning disable 0649

        [Serializable]
        private class Payload
        {
            public string Source;

            public List<RootInfo> Roots;

            public List<SectionInfo> Sections;
        }

        [Serializable]
        private class RootInfo
        {
            public string Path;

            public string Description;
        }

        [Serializable]
        private class SectionInfo
        {
            public string Kind;

            public string Summary;

            public bool Available;

            public string Trouble;

            public List<EntryInfo> Entries;
        }

        [Serializable]
        private class EntryInfo
        {
            public int Id;

            public string Name;

            public string Detail;

            public string Label;
        }

#pragma warning restore 0649
    }
}

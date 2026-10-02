using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Top.Client.Game.World;
using Top.Client.Models;
using Top.Logging;
using UnityEngine;

namespace Top.Client.App
{
    /// <summary>
    /// Puts a converted character on the hero and animates it from how the hero
    /// moves.
    /// <br/>
    /// A player character is converted as parts rather than as one file: a rig
    /// that is a skeleton and its clips and draws nothing at all, and one file
    /// per body part and piece of equipment, each carrying its own copy of the
    /// same skeleton and no clips of its own. Both copies have the same bones in
    /// the same rest pose, so the rig's clips play on a part unchanged. Every
    /// part is therefore given them and animates itself, which costs one skeleton
    /// per part and leaves each part's skin exactly as it was built.
    /// </summary>
    public class HeroModel : MonoBehaviour
    {
        [SerializeField] private MapPreview _preview;
        [SerializeField] private HeroController _hero;

        /// <summary>The character's skeleton and its clips; it draws nothing.</summary>
        [SerializeField] private string _rig = "rigs/0003.glb";

        /// <summary>
        /// The body, which every character has whatever else is on. What is worn over
        /// it comes from the inventory: the body carries the face, so a hat has
        /// something to sit on.
        /// <br/>
        /// It is called the body rather than the parts because a scene saved while
        /// this was a list of the character's whole starting outfit would otherwise
        /// keep that outfit on underneath whatever the inventory puts over it, and
        /// two skins in the same place fight over the same pixels.
        /// </summary>
        [SerializeField] private string[] _body =
        {
            "models/character/0003000000.glb"
        };

        /// <summary>
        /// Which of the four player classes the character is, which is the model
        /// iteminfo names per class. The class whose modules start with 0003 - the
        /// rig's own number - is the fourth.
        /// </summary>
        [SerializeField] private int _class = 3;

        /// <summary>
        /// Clips as the converter names them: model, action number, action. These
        /// are the ones played where the hero is safe.
        /// </summary>
        [SerializeField] private string _idle = "0003_01_waiting";
        [SerializeField] private string _move = "0003_05_run";

        /// <summary>
        /// What is played outside the marked safe zones, where the hero is in
        /// danger: the same body standing and running ready for a fight rather
        /// than at ease.
        /// </summary>
        [SerializeField] private string _warIdle = "0003_04_waiting2";
        [SerializeField] private string _warMove = "0003_06_run2";

        /// <summary>
        /// How fast the hero moves while the move clip plays at its own speed.
        /// The original client paced these clips by movement, so walking slowly
        /// is the same clip played slower rather than another clip.
        /// </summary>
        [SerializeField] private float _moveSpeed = 14f;

        [SerializeField] private float _minRate = 0.6f;
        [SerializeField] private float _maxRate = 1.5f;

        /// <summary>Which way the model faces and how big it is, for a model that comes out turned or scaled.</summary>
        [SerializeField] private float _yaw;
        [SerializeField] private float _scale = 1f;

        /// <summary>Whether the hero's own placeholder mesh is hidden once a model is on it.</summary>
        [SerializeField] private bool _hidePlaceholder = true;

        private readonly List<ModelInstance> _instances = new List<ModelInstance>();
        private readonly List<Animation> _animations = new List<Animation>();
        private readonly Dictionary<int, ModelInstance> _worn = new Dictionary<int, ModelInstance>();

        private readonly TaskCompletionSource<bool> _ready = new TaskCompletionSource<bool>();

        private ModelStore _store;
        private Transform _model;
        private Rig _rigClips;
        private string _playing;
        private float _rate = 1f;

        /// <summary>
        /// Finished loading the rig and the body. Anything that wants to put a model on
        /// the hero waits for this: a scene starts its components in an order of their
        /// own, so an inventory can ask for a coat before there is a skeleton to wear it
        /// on, and a model put on then would simply not be there.
        /// </summary>
        public Task Ready => _ready.Task;

        /// <summary>
        /// Whether the hero is standing inside a marked safe zone, which is what
        /// picks between the at-ease clips and the ones for danger.
        /// </summary>
        public bool Safe { get; private set; }

        /// <summary>Which of the four classes the character is, for reading an item's model by class.</summary>
        public int Class => _class;

        private async void Start()
        {
            var preview = _preview != null ? _preview : FindAnyObjectByType<MapPreview>();
            var hero = _hero != null ? _hero : GetComponent<HeroController>();

            _hero = hero;

            if (preview == null || preview.Content == null)
            {
                Log.Error("no map preview to take the content folder from");

                _ready.TrySetResult(true);

                return;
            }

            HidePlaceholder();

            var model = new GameObject("Model");

            model.transform.SetParent(transform, worldPositionStays: false);
            model.transform.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            model.transform.localScale = Vector3.one * _scale;

            // The walker stands the hero's centre on the ground, a little above
            // it; a model with feet of its own is brought back down by that much.
            if (hero != null)
            {
                model.transform.localPosition = Vector3.down * hero.GroundOffset;
            }

            _store = new ModelStore(preview.Content, preview.ModelShader);
            _model = model.transform;

            try
            {
                _rigClips = await Clips(model.transform);

                foreach (var part in _body)
                {
                    await Add(part);
                }
            }
            catch (Exception exception)
            {
                Log.Error("could not build the hero's model", exception);
            }

            _ready.TrySetResult(true);

            Play(_idle, 1f);
        }

        private void Update()
        {
            if (_animations.Count == 0)
            {
                return;
            }

            // Which side of the line the hero stands on is told by his body rather
            // than by anything in the world: the zones are marked in the editor and
            // drawn there alone.
            var where = _hero != null ? _hero.transform.position : transform.position;

            Safe = Zone.IsSafe(where);

            var speed = _hero != null ? _hero.Speed : 0f;

            if (speed <= 0.01f)
            {
                Play(Safe ? _idle : _warIdle, 1f);
            }
            else
            {
                Play(Safe ? _move : _warMove,
                    Mathf.Clamp(speed / Mathf.Max(0.01f, _moveSpeed), _minRate, _maxRate));
            }
        }

        private void OnDestroy()
        {
            foreach (var instance in _instances)
            {
                instance.Dispose();
            }

            _instances.Clear();
            _animations.Clear();
            _worn.Clear();
        }

        /// <summary>
        /// The character's clips and the node they are written against. The rig
        /// is instantiated for them and then switched off: it is a skeleton with
        /// nothing on it, and the clips live as long as the store holds the file
        /// open, which it does while this instance is alive.
        /// </summary>
        private async Task<Rig> Clips(Transform parent)
        {
            var holder = new GameObject("Rig");

            holder.transform.SetParent(parent, worldPositionStays: false);

            var instance = await _store.Instantiate(_rig, holder.transform);

            _instances.Add(instance);

            var source = instance.Root.GetComponentInChildren<Animation>();

            if (source == null)
            {
                Log.Error($"'{_rig}' carries no clips to animate the hero with");

                return new Rig();
            }

            var clips = new List<AnimationClip>();

            foreach (AnimationState state in source)
            {
                clips.Add(state.clip);
            }

            holder.SetActive(false);

            Log.Info($"hero rig '{_rig}': {clips.Count} clips, written against '{source.gameObject.name}'");

            return new Rig { HostName = source.gameObject.name, Clips = clips };
        }

        /// <summary>
        /// Puts a model on the hero - the body, or whatever the inventory wears -
        /// and hands it the rig's clips, so that it animates itself.
        /// </summary>
        private async Task<ModelInstance> Add(string path)
        {
            var instance = await _store.Instantiate(path, _model);

            var host = Host(instance.Root, _rigClips.HostName);
            var animation = host.gameObject.AddComponent<Animation>();

            animation.playAutomatically = false;

            foreach (var clip in _rigClips.Clips)
            {
                animation.AddClip(clip, clip.name);
            }

            _instances.Add(instance);
            _animations.Add(animation);

            // Something put on while the hero is already moving joins what he is
            // doing rather than standing in its rest pose.
            Play(_playing, _rate);

            return instance;
        }

        /// <summary>
        /// Wears a model in place of whatever that slot of the body had on, or takes
        /// the slot's own off when the path is empty: a coat over a back, a hat on a
        /// head, and one of each at a time.
        /// </summary>
        public async Task Wear(int slot, string path)
        {
            TakeOff(slot);

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            await Ready;

            if (_store == null || _model == null || _rigClips.Clips == null)
            {
                return;
            }

            try
            {
                _worn[slot] = await Add(path);
            }
            catch (Exception exception)
            {
                Log.Error($"could not wear '{path}'", exception);
            }
        }

        /// <summary>Takes off whatever covers a slot, leaving the body as it is.</summary>
        public void TakeOff(int slot)
        {
            if (!_worn.TryGetValue(slot, out var instance))
            {
                return;
            }

            _worn.Remove(slot);
            _instances.Remove(instance);

            instance.Dispose();
        }

        /// <summary>
        /// The node in a part the clips belong on: the rig's own host when the
        /// part has a node of that name, and the part's root otherwise - the
        /// curves of a whole skeleton are written either from the bone they hang
        /// off or from the top of the file.
        /// </summary>
        private static Transform Host(Transform part, string hostName)
        {
            if (string.IsNullOrEmpty(hostName))
            {
                return part;
            }

            foreach (var candidate in part.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (candidate.name == hostName)
                {
                    return candidate;
                }
            }

            return part;
        }

        /// <summary>
        /// Switches every part to a clip, once, and sets how fast it runs. All of
        /// them play the same clip at the same rate, which is what keeps a
        /// handful of skeletons looking like one body.
        /// </summary>
        private void Play(string name, float rate)
        {
            _rate = rate;

            if (_animations.Count == 0 || string.IsNullOrEmpty(name))
            {
                return;
            }

            if (_playing != name)
            {
                var played = 0;

                foreach (var animation in _animations)
                {
                    // A part taken off leaves its animation behind as a destroyed
                    // component, which is not a part to play anything on.
                    if (animation == null || animation.GetClip(name) == null)
                    {
                        continue;
                    }

                    animation.CrossFade(name, 0.15f);
                    played++;
                }

                if (played == 0)
                {
                    Log.Warning($"no part has the clip '{name}'");
                }

                _playing = name;
            }

            foreach (var animation in _animations)
            {
                if (animation == null)
                {
                    continue;
                }

                var state = animation[name];

                if (state != null)
                {
                    state.speed = rate;
                }
            }
        }

        private void HidePlaceholder()
        {
            if (!_hidePlaceholder)
            {
                return;
            }

            var placeholder = GetComponent<MeshRenderer>();

            if (placeholder != null)
            {
                placeholder.enabled = false;
            }
        }

        /// <summary>The rig's clips and the node they were written against.</summary>
        private struct Rig
        {
            public string HostName;
            public List<AnimationClip> Clips;
        }
    }
}

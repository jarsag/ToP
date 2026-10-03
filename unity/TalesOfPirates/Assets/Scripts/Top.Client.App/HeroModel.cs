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
        /// <summary>The slots a thing is carried in rather than worn on.</summary>
        private const int RightHand = 5;

        private const int LeftHand = 6;

        /// <summary>How a carried thing sits on its mount, which the model's own axes decide.</summary>
        [SerializeField] private Vector3 _carryRotation = Vector3.zero;

        [SerializeField] private float _carryScale = 1f;

        /// <summary>Whether a slot is one a thing is carried in rather than worn on.</summary>
        private static bool Carried(int slot)
        {
            return slot == RightHand || slot == LeftHand;
        }

        /// <summary>
        /// Puts a carried thing on the mount the client's rig keeps for it. A weapon is not
        /// a part of the body: it is a model whose own skeleton nothing drives, so it is hung
        /// by a holder on a dummy of the rig and goes wherever that dummy goes.
        /// </summary>
        private async Task Carry(int slot, string path)
        {
            if (_store == null)
            {
                Log.Warning($"cannot carry '{path}': the hero has not finished loading");

                return;
            }

            // The dummy the client keeps for this, and failing that the bone it hangs from:
            // either will carry the thing, and a bone is better than nothing.
            var mount = Find(slot == LeftHand ? "dummy_6" : "dummy_9")
                        ?? Find(slot == LeftHand ? "Bip01 L Hand" : "Bip01 R Hand");

            if (mount == null)
            {
                var held = new List<string>();

                foreach (var child in transform.root.GetComponentsInChildren<Transform>(true))
                {
                    if (held.Count < 24)
                    {
                        held.Add(child.name);
                    }
                }

                Log.Warning($"the rig has no mount for '{path}'; the hero holds: {string.Join(", ", held)}");

                return;
            }

            // The rig draws nothing and the loader may have switched it off, and a weapon hung
            // inside something switched off is switched off with it - which is exactly what
            // happened: the mount was found, the model loaded, and nothing was drawn. The branch
            // the mount hangs in holds the skeleton and nothing else, so switching it on shows
            // no more than the weapon that was hung there.
            if (!mount.gameObject.activeInHierarchy)
            {
                for (var at = mount; at != null && at != transform.root; at = at.parent)
                {
                    at.gameObject.SetActive(true);
                }

                Log.Info($"the branch holding '{mount.name}' was switched off and is now on, so what hangs on it is drawn");
            }

            var holder = new GameObject($"Carried {path}");

            holder.transform.SetParent(mount, worldPositionStays: false);
            holder.transform.localRotation = Quaternion.Euler(_carryRotation);
            holder.transform.localScale = Vector3.one * _carryScale;

            ModelInstance instance;

            try
            {
                instance = await _store.Instantiate(path, holder.transform);
            }
            catch (Exception exception)
            {
                Log.Error($"could not carry '{path}'", exception);

                return;
            }

            TakeOff(slot);

            _worn[slot] = instance;

            Tell(path, mount, holder, instance);
        }

        /// <summary>
        /// What a carried thing turned out to be, for when nothing shows on screen: which
        /// mount it went on and whether that mount is switched on, where the holder stands,
        /// and what the model that came back covers. A model drawn at a scale of its own is
        /// the usual reason for a weapon that is there and yet invisible.
        /// </summary>
        private static void Tell(string path, Transform mount, GameObject holder, ModelInstance instance)
        {
            var renderers = holder.GetComponentsInChildren<Renderer>(true);
            var on = 0;
            var bounds = new Bounds(holder.transform.position, Vector3.zero);

            foreach (var renderer in renderers)
            {
                if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                {
                    on++;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            var parent = mount.parent != null ? mount.parent.name : "nothing";

            Log.Info($"carried '{path}': mount '{mount.name}' under '{parent}' at {mount.position} " +
                     $"(switched on: {mount.gameObject.activeInHierarchy}), holder at {holder.transform.position} " +
                     $"(scale {holder.transform.localScale.x}), {renderers.Length} renderer(s), {on} of them on, " +
                     $"covering centre {bounds.center} size {bounds.size}, instance={instance != null}");
        }

        /// <summary>A dummy of the rig, by the name the client gives it.</summary>
        private Transform Find(string name)
        {
            // Every part of a character carries its own copy of the skeleton, so a mount by
            // this name exists several times over - and one of the copies sits inside the rig,
            // which draws nothing and may well be switched off. Anything hung on a switched
            // off object is switched off with it, so a live mount is taken first and a
            // sleeping one only when there is nothing else. The name is matched loosely
            // because a loader may add to it.
            Transform sleeping = null;

            foreach (var child in transform.root.GetComponentsInChildren<Transform>(true))
            {
                if (!string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase) &&
                    child.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (child.gameObject.activeInHierarchy)
                {
                    return child;
                }

                sleeping = sleeping != null ? sleeping : child;
            }

            if (sleeping != null)
            {
                Log.Warning($"the mount '{name}' sits inside something switched off, so what is hung on it will not be drawn");
            }

            return sleeping;
        }

        public async Task Wear(int slot, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            await Ready;

            if (_store == null || _model == null || _rigClips.Clips == null)
            {
                return;
            }

            // A weapon is carried in a hand rather than worn on the body, so it does not go
            // through the parts a body is dressed in.
            if (Carried(slot))
            {
                await Carry(slot, path);

                return;
            }

            ModelInstance instance;

            try
            {
                instance = await Add(path);
            }
            catch (Exception exception)
            {
                Log.Error($"could not wear '{path}'", exception);

                return;
            }

            // Whatever stands in the slot by now goes before this arrives: something may
            // have been put on while this was loading, and two of the same part on one body
            // is a body wearing itself twice.
            TakeOff(slot);

            _worn[slot] = instance;
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

            // How far into the clip the parts already playing it have got. A part put on
            // now would otherwise start its own copy from the beginning and walk a step
            // out of time with the rest - a coat walking its own walk - so it is started
            // where they are.
            var at = Phase(name);
            var played = 0;

            foreach (var animation in _animations)
            {
                // A part taken off leaves its animation behind as a destroyed component,
                // which is not a part to play anything on.
                if (animation == null || animation.GetClip(name) == null)
                {
                    continue;
                }

                var state = animation[name];

                if (_playing != name || !animation.IsPlaying(name))
                {
                    animation.CrossFade(name, 0.15f);

                    // Only a part joining a clip already under way is caught up; a clip
                    // that everything is switching to starts at its own beginning.
                    if (state != null && at > 0f)
                    {
                        state.normalizedTime = at;
                    }
                }

                if (state != null)
                {
                    state.speed = rate;
                }

                played++;
            }

            if (played == 0 && _playing != name)
            {
                Log.Warning($"no part has the clip '{name}'");
            }

            _playing = name;
        }

        /// <summary>
        /// How far into a clip the parts already playing it have got, which is what a part
        /// put on now has to catch up with. Zero when none of them is playing it.
        /// </summary>
        private float Phase(string name)
        {
            foreach (var animation in _animations)
            {
                if (animation == null || !animation.IsPlaying(name))
                {
                    continue;
                }

                var state = animation[name];

                if (state != null)
                {
                    return state.normalizedTime;
                }
            }

            return 0f;
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

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
        [Header("Animations")]

        /// <summary>
        /// The clip a hero at war with a weapon in a hand runs with, and the one he waits with,
        /// named as it appears in the rig - the inspector offers the rig's own clips to choose
        /// from. Empty means nothing changes. They are used only at war with a weapon in a hand:
        /// in a safe zone the weapon is on the hero's back and the plain clips play.
        /// </summary>
        [SerializeField] private string _warRun = string.Empty;

        [SerializeField] private string _warWait = string.Empty;


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
        /// What the hero does when a skill goes off. A model of the client's names its own skill
        /// actions - 0003_11_skill1 and its neighbours - so this is which of them a cast plays, and
        /// empty means a cast makes no gesture at all.
        /// </summary>
        [SerializeField] private string _skill = "0003_11_skill1";

        /// <summary>How long the cast gesture holds before the hero goes back to what he was doing.</summary>
        [SerializeField] private float _skillHold = 0.9f;

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

        /// <summary>How a carried thing sits in a hand, which the model's own axes decide.</summary>
        [SerializeField] private Vector3 _carryRotation = Vector3.zero;

        /// <summary>
        /// How it sits on the back. It is a setting of its own because a weapon lying across a
        /// back is turned differently from the same weapon held upright in a hand - the client
        /// keeps them apart for the same reason.
        /// </summary>
        [SerializeField] private Vector3 _backRotation = new Vector3(0f, 180f, 0f);

        /// <summary>
        /// How far a carried thing is moved from the point it hangs on. A model's own origin is
        /// wherever its author left it, and one that hangs a metre away from the hand is a model
        /// whose geometry does not start at its root - the offset is the cure for that.
        /// </summary>
        [SerializeField] private Vector3 _carryOffset = Vector3.zero;

        [SerializeField] private Vector3 _backOffset = Vector3.zero;

        [SerializeField] private float _carryScale = 1f;

        /// <summary>
        /// Which colour the carried thing's own glow is, out of the four the client's items carry.
        /// <br/>
        /// A glowing item is two models laid over each other rather than one - its own material, and a
        /// second additive one across the same triangles - and the glow is that second layer, given a
        /// colour's texture rather than modelled. A model with a single material has no such layer and
        /// does not glow, whatever is picked here.
        /// </summary>
        [SerializeField] private CarriedItem.GlowColour _glow = CarriedItem.GlowColour.None;

        /// <summary>How hard the glow is laid on, the client drawing one at the strength its tier asks.</summary>
        [SerializeField] private float _glowStrength = 1f;

        /// <summary>
        /// How the glow's sheet is laid over the item: how many times it repeats and how far it is
        /// shifted. A sheet and an item are each of a size of their own, so how much of the sheet ends
        /// up on the item is a matter of taste.
        /// </summary>
        [SerializeField] private Vector2 _glowTiling = new Vector2(1f, 2.2f);

        [SerializeField] private Vector2 _glowOffset = Vector2.zero;

        /// <summary>
        /// How fast the glow's sheet drifts over the item, in tiles a second on each axis. A still
        /// sheet with a figure in it reads as a pattern painted on the item; a moving one as light
        /// about it.
        /// </summary>
        [SerializeField] private Vector2 _glowDrift = new Vector2(0.02f, 0.01f);

        /// <summary>
        /// Everything darker than this on the glow's sheet is left out rather than added, so that a
        /// sheet with dark figures in it lights the figures instead of lifting the whole item faintly.
        /// </summary>
        [SerializeField] private float _glowCutoff;

        /// <summary>
        /// How far the glow's colour is from grey: nought leaves it grey, one is the colour as it
        /// stands, and past one it is pushed further.
        /// </summary>
        [SerializeField] private float _glowSaturation = 1f;

        /// <summary>
        /// The glow a carried thing should wear: its colour, how hard it is laid on, and how its sheet
        /// is laid over the item. Read by the thing itself, which is made at runtime and has no other
        /// way back to these settings - and read every frame, so a colour picked in the inspector while
        /// the game runs is picked up on the next one.
        /// </summary>
        public CarriedItem.GlowColour GlowColour => _glow;

        public float GlowStrength => _glowStrength;

        public Vector2 GlowTiling => _glowTiling;

        public Vector2 GlowOffset => _glowOffset;

        public Vector2 GlowDrift => _glowDrift;

        public float GlowCutoff => _glowCutoff;

        public float GlowSaturation => _glowSaturation;


        /// <summary>How a carried thing sits in a hand, for whatever is hanging on one.</summary>
        public Vector3 HandRotation => _carryRotation;

        /// <summary>How it sits on a back, which is a different angle from a hand.</summary>
        public Vector3 BackRotation => _backRotation;

        /// <summary>How far it is moved from the point it hangs on, in the hand.</summary>
        public Vector3 CarryOffset => _carryOffset;

        /// <summary>How far it is moved from the point it hangs on, on the back.</summary>
        public Vector3 BackOffset => _backOffset;

        /// <summary>How big a carried thing is drawn.</summary>
        public float CarryScale => _carryScale;

        /// <summary>
        /// Whether a weapon is in a hand, which is when the hero is at war and moves the way a
        /// hero with a weapon moves instead of the way an empty handed one does.
        /// </summary>
        private bool Armed()
        {
            if (Safe)
            {
                return false;
            }

            return _worn.ContainsKey(RightHand) || _worn.ContainsKey(LeftHand);
        }

        /// <summary>
        /// Which clip actually plays. The numbers the client used for a hero with a weapon are
        /// not written down anywhere, so they are settings; a clip that is one of the ways of
        /// running, flying aside, gives way to the armed one when there is one.
        /// </summary>
        private string Ask(string name)
        {
            if (string.IsNullOrEmpty(name) || !Armed())
            {
                return name;
            }

            var run = string.IsNullOrEmpty(_warRun) ? null : _warRun;
            var wait = string.IsNullOrEmpty(_warWait) ? null : _warWait;

            if (run != null && Running(name))
            {
                return run;
            }

            if (wait != null && name.IndexOf("waiting", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return wait;
            }

            return name;
        }

        /// <summary>
        /// The first of a list of clips that the rig actually has, or null when it has none of
        /// them. Which number belongs to which weapon is not written down anywhere, so a list can
        /// hold the guesses and the one that exists is the one that plays.
        /// </summary>
        private string First(string[] clips)
        {
            if (clips == null)
            {
                return null;
            }

            foreach (var clip in clips)
            {
                if (string.IsNullOrEmpty(clip))
                {
                    continue;
                }

                foreach (var animation in _animations)
                {
                    if (animation != null && animation.GetClip(clip) != null)
                    {
                        return clip;
                    }
                }
            }

            return null;
        }


        /// <summary>Whether a clip is one of the ways of running, flying aside.</summary>
        private static bool Running(string name)
        {
            return name.IndexOf("run", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   name.IndexOf("fly", StringComparison.OrdinalIgnoreCase) < 0;
        }

        /// <summary>
        /// A clip to hold the hero in while the rig's animations are being looked through. While
        /// one is set the hero plays nothing of its own, so a clip picked by hand stays put
        /// instead of being replaced the moment the state changes.
        /// </summary>
        public string Holding { get; set; }

        /// <summary>Whether a clip is being held for looking through the animations.</summary>
        public bool Held => !string.IsNullOrEmpty(Holding);

        /// <summary>Lets go of a held clip and takes the hero's own animation back.</summary>
        public void LetGo()
        {
            Holding = null;
            _playing = null;

            Play(_idle, _rate);
        }

        /// <summary>
        /// Makes the gesture of a cast. <br/>
        /// It is a clip held rather than played and forgotten: a skill is cast from a standing start,
        /// and a hero who went straight back to walking would cut his own gesture off. The hold lets go
        /// of itself after the setting's own length - or at once, when no clip is named, which is what
        /// a hero with nothing to do with his hands does.
        /// </summary>
        public void Cast()
        {
            if (string.IsNullOrEmpty(_skill))
            {
                return;
            }

            Holding = _skill;

            Play(_skill, 1f);

            CancelInvoke(nameof(LetGo));
            Invoke(nameof(LetGo), Mathf.Max(_skillHold, 0.05f));
        }

        /// <summary>Which clip a cast plays, for anything that wants to know.</summary>
        public string SkillClip => _skill;


        /// <summary>
        /// Where a carried thing hangs: the dummy the rig keeps for each hand, and the one on the
        /// back for a weapon at rest. They are settings rather than names written into the code
        /// because which dummy is which is the client's business, and its spine keeps three of
        /// them - dummy_2, dummy_21, dummy_22 - with no telling from the names which is behind.
        /// A bone does just as well: Bip01 R Hand, Bip01 Spine1, anything with a transform.
        /// </summary>
        [Header("Carried weapon")]
        [SerializeField] private string _mountRight = "dummy_9";

        [SerializeField] private string _mountLeft = "dummy_6";

        [SerializeField] private string _backRight = "dummy_2";

        [SerializeField] private string _backLeft = "dummy_21";

        /// <summary>Where the thing in the right hand hangs, and in the left, at war.</summary>
        public string MountRight => _mountRight;

        public string MountLeft => _mountLeft;

        /// <summary>And where each hangs in a safe zone, on the hero's back.</summary>
        public string BackRight => _backRight;

        public string BackLeft => _backLeft;

        /// <summary>
        /// Points one of the four at a node, for lining a weapon up by hand: the debug view names
        /// every node the rig has, and choosing one says that this is where the thing hangs. The
        /// carried thing reads these every frame, so the change is seen at once.
        /// </summary>
        public void HangOn(string mount, bool left, bool onBack)
        {
            if (onBack)
            {
                if (left)
                {
                    _backLeft = mount;
                }
                else
                {
                    _backRight = mount;
                }
            }
            else
            {
                if (left)
                {
                    _mountLeft = mount;
                }
                else
                {
                    _mountRight = mount;
                }
            }
        }

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

                // A branch switched on after the clips were handed out never started playing
                // them, and a skeleton that stands still leaves what hangs on it standing still
                // beside a hand that moves - so the clip is started on it again.
                Play(_playing, _rate);
            }

            var holder = new GameObject($"Carried {path}");

            holder.transform.SetParent(mount, worldPositionStays: false);

            // Which mount it hangs on is not settled once: a weapon is in the hand out of a
            // safe zone and on the back inside one, and the thing watches for that itself.
            holder.AddComponent<CarriedItem>().Belong(this, slot);
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

            // The model's own animation is stopped now that there is a model to stop. A weapon
            // hung on a hand is carried rather than performed: left to play, its own clip runs
            // beside the hero and the two drift apart.
            foreach (var animator in holder.GetComponentsInChildren<Animator>(true))
            {
                animator.enabled = false;
            }

            foreach (var playing in holder.GetComponentsInChildren<Animation>(true))
            {
                playing.Stop();
                playing.enabled = false;
            }

            // The skeleton the weapon hangs on is a copy of the hero's, and a copy that started
            // its clip at a different moment walks a different step. Playing the current clip
            // again lines it up with the parts that are already moving.
            Play(_playing, _rate);

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
                Log.Info($"the mount '{name}' is inside something switched off; it is switched on to hang this on");
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

            // The rig is the skeleton the clips came from, and it draws nothing - so it was never one
            // of the parts that animate themselves, and its bones and hanging points stood in their
            // rest pose while the hero ran. Anything hung on one of those points stood there with it.
            // It is taken in here, and plays the same clip at the same phase as the parts do.
            foreach (var every in GetComponentsInChildren<Animation>(true))
            {
                if (!_animations.Contains(every))
                {
                    _animations.Add(every);
                }
            }

            if (Held)
            {
                // Something is being looked through by hand, and it is not for the hero to
                // replace the clip he was given.
                return;
            }

            var wanted = Ask(name);

            if (wanted != name)
            {
                Play(wanted, rate);

                return;
            }

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

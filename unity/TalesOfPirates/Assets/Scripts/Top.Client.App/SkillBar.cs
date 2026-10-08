using System.Collections.Generic;
using Top.Client.Game.World;
using Top.Logging;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Top.Client.App
{
    /// <summary>
    /// A skill as it is played: what it is called, what it looks like, how hard it hits, and how long
    /// before it can be cast again. <br/>
    /// Data rather than a prefab, so that a skill can be read and reasoned about without a scene - and
    /// so that the same numbers can be tried in a test before anyone looks at them.
    /// </summary>
    [System.Serializable]
    public class Skill
    {
        public string name = "Chain Lightning";

        /// <summary>What is played on each body it lands on.</summary>
        public string effect = "strhurt";

        /// <summary>How long each body wears the effect for.</summary>
        public float effectLife = 0.5f;

        /// <summary>
        /// How wide the ribbon between one body and the next is, as a share of how far it reaches. <br/>
        /// The client's own art is a road rather than a bolt - the pieces of this effect are seven and
        /// eight metres long along the way they go - so a ribbon drawn to its own proportions reads as a
        /// slab. Nought or less leaves a thin fixed width instead.
        /// </summary>
        [Range(0f, 0.3f)] public float effectWidth = 0.03f;

        /// <summary>The width the ribbon falls back to when no share is named, in metres.</summary>
        public float effectThickness = 0.15f;

        /// <summary>
        /// Whether the effect is cut to the distance it is thrown over. <br/>
        /// The art reaches about eight metres along its own forward, which is right for a thing thrown
        /// into the distance and wrong for a blow that lands two metres away: the pieces run past the
        /// body and out the other side. Cut to the distance, they end where the body is. Turning it off
        /// leaves the effect its own length.
        /// </summary>
        public bool effectStretch = true;

        public int damage = 100;

        /// <summary>How far it reaches for its first body.</summary>
        public float range = 8f;

        /// <summary>How far the chain reaches from one body to the next.</summary>
        public float chainRange = 2f;

        /// <summary>How many bodies the chain touches, the first one included.</summary>
        public int targets = 3;

        public float cooldown = 5f;

        /// <summary>The icon, by name under Resources/Ui.</summary>
        public string icon = "s0214";

        /// <summary>
        /// The gesture the hero makes. Empty keeps whatever clip the hero is set to cast with.
        /// </summary>
        public string clip = "0003_13_skill3";

        /// <summary>
        /// Where in the gesture the effect leaves the hero, nought at its first frame and one at its
        /// last. <br/>
        /// A blow does not leave the hand when the key is pressed: the client kept the frame in its
        /// animation data, and a coral leaves the hand when the arm is furthest forward, which is part
        /// way through the gesture. For the hero's third skill that frame is 6311 of a gesture running
        /// 6284 to 6344, so the blow leaves 27 frames into a gesture of 60 - a little under half way.
        /// </summary>
        [Range(0f, 1f)] public float launch = 0.45f;
    }

    /// <summary>
    /// The hero's skills and the bar they sit on. <br/>
    /// One slot for now: a key casts it, the icon goes dark while it is cooling, and the bodies it
    /// touches each wear its effect. The chain is worked out before anything is drawn - the first body
    /// nearest the hero, then the nearest to that one, then the nearest to that - which is how the
    /// client's own chained blows pick their marks, and it is what lets the whole thing land at once
    /// instead of travelling.
    /// </summary>
    public class SkillBar : MonoBehaviour
    {
        [SerializeField] private List<Skill> _skills = new List<Skill>();

        /// <summary>Key that casts the first skill, while there is only one to cast.</summary>
        [SerializeField] private Key _key = Key.Digit1;

        [SerializeField] private int _slotSize = 52;

        [SerializeField] private float _scale = 1f;

        /// <summary>How long the cursor keeps saying a skill is being aimed after the key is let go.</summary>
        private const float CursorHold = 1f;

        /// <summary>Where a cast is aimed: the same camera a walk is pointed with.</summary>
        [SerializeField] private Camera _camera;

        /// <summary>The map a cast is aimed on, which is the one the ground is read from.</summary>
        [SerializeField] private MapPreview _preview;

        private HeroModel _hero;

        private readonly List<float> _ready = new List<float>();

        private readonly List<Image> _swell = new List<Image>();

        /// <summary>The icons, so that a slot can flash when its skill comes back.</summary>
        private readonly List<Image> _icons = new List<Image>();

        /// <summary>The seconds left, drawn over a slot that is cooling.</summary>
        private readonly List<Text> _left = new List<Text>();

        /// <summary>Until when a slot is flashing, once per coming back.</summary>
        private readonly List<float> _flash = new List<float>();

        /// <summary>How long a slot flashes when its skill comes back, and how bright it goes.</summary>
        private const float FlashSeconds = 0.35f;

        private static readonly Color Ready = new Color(1f, 1f, 0.65f);

        private static readonly Color Spent = new Color(0.35f, 0.35f, 0.4f);

        private Canvas _canvas;

        /// <summary>The skill that would be cast now, which is the first one.</summary>
        public Skill First => _skills.Count > 0 ? _skills[0] : null;

        private void Start()
        {
            _hero = FindAnyObjectByType<HeroModel>();

            Bar();

            _ready.Clear();

            foreach (var skill in _skills)
            {
                _ready.Add(0f);
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return;
            }

            var pressed = keyboard[_key];

            if (pressed != null && pressed.wasPressedThisFrame)
            {
                Cast(0);
            }

            // Held down, the cursor keeps saying so: let go and it holds for its own moment longer, so
            // that a tap of the key still shows what was aimed at.
            if (pressed != null && pressed.isPressed)
            {
                GameCursor.Aiming(CursorHold);
            }

            Cool();
        }

        /// <summary>
        /// Casts the skill in a slot, if it is ready. <br/>
        /// The route is worked out whole and at once, the cursor changes to say a skill is being aimed,
        /// the hero makes the gesture, and the blows themselves leave his hand at the frame the skill's
        /// own setting names - which is part way through the gesture, not at its start.
        /// </summary>
        public bool Cast(int slot)
        {
            if (slot < 0 || slot >= _skills.Count || Time.time < _ready[slot])
            {
                return false;
            }

            var skill = _skills[slot];
            var bodies = Chain(skill);

            if (bodies.Count == 0)
            {
                return false;
            }

            _ready[slot] = Time.time + Mathf.Max(skill.cooldown, 0.01f);

            // The cursor says a skill is being aimed while the key is held and a moment after it is let
            // go, so that letting go does not snatch the cursor away at once.
            GameCursor.Aiming(CursorHold);

            if (_hero != null)
            {
                if (!string.IsNullOrEmpty(skill.clip))
                {
                    _hero.SkillClip = skill.clip;
                }

                _hero.Cast(() => Land(skill, bodies), skill.launch);

                Aimed(skill, bodies);

                return true;
            }

            Land(skill, bodies);

            return true;
        }

        /// <summary>
        /// Lets the blows go: every body on the route is hit and wears the effect, and each blow leaves
        /// the place the one before it landed - the hero's own hand for the first. <br/>
        /// A blow is not drawn over the body it hits: it starts where it was cast from and is turned down
        /// the way it is going, so that it reads as leaving the hand and reaching for the body. The client
        /// draws its effects the same way, reaching out along their own forward from where they are put.
        /// </summary>
        private void Land(Skill skill, List<Enemy> bodies)
        {
            var hand = _hero != null ? _hero.EffectFrom().position : transform.position;

            for (var i = 0; i < bodies.Count; i++)
            {
                var body = bodies[i];
                var at = body.transform.position + (Vector3.up * 1.1f);
                var from = i == 0 ? hand : bodies[i - 1].transform.position + (Vector3.up * 1.1f);
                var way = at - from;

                // Where it starts, the way it is going, and - for everything after the first - the way
                // it came, so that the bodies read as one chain and not as a handful of separate blows.
                // The width and the reach are the skill's own settings: the client's art is a road seven
                // or eight metres long, which is right for a thing thrown into the distance and wrong for
                // a blow that lands two metres away - it runs out the back of the body it hits.
                SkillEffect.Play(skill.effect, from, skill.effectLife, null,
                    i == 0 ? (Vector3?)null : from,
                    way.sqrMagnitude > 0.0001f ? way : (Vector3?)null,
                    skill.effectWidth,
                    skill.effectThickness,
                    skill.effectStretch,
                    way.magnitude);

                body.Strike(skill.damage);
            }

            Log.Info($"{skill.name}: {bodies.Count} body(ies) struck for {skill.damage} each");
        }

        /// <summary>Where a cast is aimed, said out loud so that a miss can be told from a bad aim.</summary>
        private void Aimed(Skill skill, List<Enemy> bodies)
        {
            var pointed = Pointed();
            var hero = _hero != null ? _hero.transform.position : transform.position;

            Log.Info($"{skill.name}: aimed at {pointed} (the hero stands at {hero}), "
                     + $"found {bodies.Count} body(ies) within {skill.range} of it");
        }

        /// <summary>
        /// The bodies a skill lands on, in the order the chain reaches them: the one nearest where the
        /// pointer is, then the nearest to that one, then the nearest to that - never one already on the
        /// chain. <br/>
        /// Worked out whole before anything happens, which is what makes a chained blow land at once:
        /// there is no bolt finding its way, only the route it would have taken.
        /// <br/>
        /// The first body is the one the player pointed at, not the one nearest the hero: a skill is
        /// aimed, and reaching for whatever happens to stand closest would take the choice away. The
        /// pointer is read on the map's own height field, the same way a walk is, and falls back to the
        /// hero when it names no ground - so a cast from off the map still lands on what is nearest.
        /// </summary>
        public List<Enemy> Chain(Skill skill)
        {
            var all = FindObjectsByType<Enemy>();
            var chain = new List<Enemy>();

            if (all.Length == 0)
            {
                return chain;
            }

            var first = Nearest(all, Pointed(), skill.range, null);

            if (first == null)
            {
                return chain;
            }

            chain.Add(first);

            while (chain.Count < Mathf.Max(skill.targets, 1))
            {
                var last = chain[^1];
                var next = Nearest(all, last.transform.position, skill.chainRange, chain);

                if (next == null)
                {
                    break;
                }

                chain.Add(next);
            }

            return chain;
        }

        /// <summary>
        /// Where the pointer is on the ground, or the hero when it is over no ground at all. <br/>
        /// Read the way a walk is read, so that aiming at a place and walking to it mean the same thing
        /// by the same code - and so that aiming off the edge of the map is simply the hero's own place
        /// rather than a refusal to cast.
        /// </summary>
        public Vector3 Pointed()
        {
            var fallback = _hero != null ? _hero.transform.position : transform.position;
            var camera = _camera != null ? _camera : Camera.main;
            var map = _preview != null ? _preview.Data : null;
            var mouse = Mouse.current;

            if (camera == null || map == null || mouse == null)
            {
                return fallback;
            }

            var ray = camera.ScreenPointToRay(mouse.position.ReadValue());

            return MapRay.TryHit(map, ray, 4000f, out var hit) ? hit : fallback;
        }

        /// <summary>The closest body within reach that is not already on the chain.</summary>
        private static Enemy Nearest(Enemy[] all, Vector3 from, float reach, List<Enemy> taken)
        {
            Enemy best = null;
            var closest = reach * reach;

            foreach (var body in all)
            {
                if (body == null || (taken != null && taken.Contains(body)))
                {
                    continue;
                }

                var away = body.transform.position - from;
                var square = (away.x * away.x) + (away.z * away.z);

                // Measured across the ground rather than through the air: the bodies of a spawn stand
                // on different ground, and one a little lower than another is not further away.
                if (square <= closest)
                {
                    closest = square;
                    best = body;
                }
            }

            return best;
        }

        /// <summary>Draws the bar: one slot per skill, its icon, and its key.</summary>
        private void Bar()
        {
            var canvas = new GameObject("Skills", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            _canvas = canvas.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvas.GetComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = _scale;
            scaler.referencePixelsPerUnit = 1f;

            for (var i = 0; i < _skills.Count; i++)
            {
                Slot(canvas.transform, i, _skills[i]);
            }
        }

        /// <summary>One slot: a dark frame, the icon over it, and the key that casts it in the corner.</summary>
        private void Slot(Transform parent, int slot, Skill skill)
        {
            var size = _slotSize;
            var step = size + 6;

            var frame = Clear(parent, $"Slot {slot}", 12f + (slot * step), 12f, size, size);

            frame.color = new Color(0f, 0f, 0f, 0.7f);

            var icon = Clear(frame.transform, "Icon", 3f, 3f, size - 6f, size - 6f);
            var art = Resources.Load<Texture2D>($"Ui/{skill.icon}");

            if (art != null)
            {
                icon.sprite = Sprite.Create(art, new Rect(0f, 0f, art.width, art.height), new Vector2(0.5f, 0.5f));
                icon.color = Color.white;
            }
            else
            {
                icon.color = new Color(0.6f, 0.6f, 0.9f);
            }

            // The dark sheet the cooldown is drawn with, swept away round the middle rather than lifted
            // off the bottom: a skill coming back is a thing going round, and a sheet rising straight up
            // reads as a bar being filled rather than as a clock running down.
            var shadow = Clear(frame.transform, "Cooling", 3f, 3f, size - 6f, size - 6f);

            shadow.sprite = Sheet();
            shadow.color = new Color(0f, 0f, 0f, 0.75f);
            shadow.type = Image.Type.Filled;
            shadow.fillMethod = Image.FillMethod.Radial360;
            shadow.fillOrigin = (int)Image.Origin360.Top;
            shadow.fillClockwise = false;
            shadow.fillAmount = 0f;

            _swell.Add(shadow);

            // The seconds left, over everything: a sweep says roughly how long, and a number says exactly.
            var left = Count(frame.transform, size);

            _left.Add(left);

            _icons.Add(icon);
            _flash.Add(0f);

            var label = KeyLabel(frame.transform, slot);
            label.text = slot == 0 ? "1" : (slot + 1).ToString();
        }

        /// <summary>The seconds left over a slot, which is only up while the skill is cooling.</summary>
        private static Text Count(Transform parent, float size)
        {
            var go = new GameObject("Left", typeof(Text));

            var text = go.GetComponent<Text>();
            var at = (RectTransform)go.transform;

            at.SetParent(parent, false);
            at.anchorMin = Vector2.zero;
            at.anchorMax = Vector2.zero;
            at.pivot = Vector2.zero;
            at.anchoredPosition = new Vector2(0f, (size - 6f) * 0.5f - 10f);
            at.sizeDelta = new Vector2(size - 6f, 20f);

            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = string.Empty;

            return text;
        }

        /// <summary>
        /// A plain white sprite for the pieces that are only a colour. <br/>
        /// A filled image needs one and cannot work without: the fill is done by moving the sprite's own
        /// coordinates about, so an image with no sprite has no coordinates to move and is drawn whole
        /// whatever its fill amount says - which is a black sheet lying over the icon from the moment the
        /// game starts, with the cooldown apparently never having begun.
        /// </summary>
        private static Sprite Sheet()
        {
            if (_sheet != null)
            {
                return _sheet;
            }

            var pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);

            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();

            _sheet = Sprite.Create(pixel, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));

            return _sheet;
        }

        /// <summary>The white sprite, made once and shared by every filled piece.</summary>
        private static Sprite _sheet;

        /// <summary>The key that casts a slot, drawn in its bottom corner.</summary>
        private static Text KeyLabel(Transform parent, int slot)
        {
            var go = new GameObject("Key", typeof(Text));

            var text = go.GetComponent<Text>();
            var at = (RectTransform)go.transform;

            at.SetParent(parent, false);
            at.anchorMin = Vector2.zero;
            at.anchorMax = Vector2.zero;
            at.pivot = Vector2.zero;
            at.anchoredPosition = new Vector2(3f, 1f);
            at.sizeDelta = new Vector2(20f, 16f);

            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 12;
            text.color = new Color(1f, 0.9f, 0.5f);

            return text;
        }

        /// <summary>
        /// Spends and brings back each slot: the dark sheet sweeps away as the skill returns, the
        /// seconds left are counted over it, and the icon flashes once when it is ready again. <br/>
        /// The flash is what makes it readable at a glance - a sweep going round says roughly how long,
        /// and a flash says that the waiting is over without anything having to be read.
        /// </summary>
        private void Cool()
        {
            for (var i = 0; i < _skills.Count && i < _swell.Count; i++)
            {
                var cooling = Mathf.Max(_skills[i].cooldown, 0.01f);
                var left = _ready[i] - Time.time;

                if (left > 0f)
                {
                    var share = Mathf.Clamp01(left / cooling);

                    _swell[i].fillAmount = share;

                    if (i < _left.Count)
                    {
                        _left[i].text = Mathf.CeilToInt(left).ToString();
                    }

                    if (i < _icons.Count)
                    {
                        _icons[i].color = Spent;
                    }

                    continue;
                }

                // Just came back: the flash is started here rather than where the cast happens, because a
                // skill can also come back while nothing is being pressed.
                if (_swell[i].fillAmount > 0f)
                {
                    _swell[i].fillAmount = 0f;

                    if (i < _flash.Count)
                    {
                        _flash[i] = Time.time + FlashSeconds;
                    }
                }

                if (i < _left.Count)
                {
                    _left[i].text = string.Empty;
                }

                if (i < _icons.Count)
                {
                    var flashing = i < _flash.Count && Time.time < _flash[i];

                    _icons[i].color = flashing
                        ? Color.Lerp(Ready, Color.white, 1f - ((_flash[i] - Time.time) / FlashSeconds))
                        : Color.white;
                }
            }
        }

        private static Image Clear(Transform parent, string name, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(Image));

            var at = (RectTransform)go.transform;

            at.SetParent(parent, false);
            at.anchorMin = Vector2.zero;
            at.anchorMax = Vector2.zero;
            at.pivot = Vector2.zero;
            at.anchoredPosition = new Vector2(x, y);
            at.sizeDelta = new Vector2(width, height);

            return go.GetComponent<Image>();
        }
    }
}

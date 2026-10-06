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

        private HeroModel _hero;

        private readonly List<float> _ready = new List<float>();

        private readonly List<Image> _swell = new List<Image>();

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

            Cool();
        }

        /// <summary>
        /// Casts the skill in a slot, if it is ready: the chain is worked out, every body on it is hit
        /// and wears the effect, and the hero makes the gesture.
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

            foreach (var body in bodies)
            {
                // The effect over the body it lands on, and the hurt said out loud. The numbers that
                // go with it are not drawn yet - a blow is tried before it is read.
                SkillEffect.Play(skill.effect, body.transform.position + (Vector3.up * 1.1f), skill.effectLife);

                body.Strike(skill.damage);
            }

            if (_hero != null)
            {
                _hero.Cast();
            }

            Log.Info($"{skill.name}: {bodies.Count} body(ies) struck for {skill.damage} each");

            return true;
        }

        /// <summary>
        /// The bodies a skill lands on, in the order the chain reaches them: the one nearest the hero,
        /// then the nearest to that one, then the nearest to that - never one already on the chain.
        /// <br/>
        /// Worked out whole before anything happens, which is what makes a chained blow land at once:
        /// there is no bolt finding its way, only the route it would have taken.
        /// </summary>
        public List<Enemy> Chain(Skill skill)
        {
            var all = FindObjectsByType<Enemy>();
            var chain = new List<Enemy>();

            if (all.Length == 0)
            {
                return chain;
            }

            var from = _hero != null ? _hero.transform.position : transform.position;

            var first = Nearest(all, from, skill.range, null);

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

            // The dark sheet the cooldown is drawn with: full and opaque while the skill is spent, and
            // shrunk away as it comes back. A round mask would be truer to the client, and a sheet is
            // what there is to draw with until its own art is brought over.
            var shadow = Clear(frame.transform, "Cooling", 3f, 3f, size - 6f, size - 6f);

            shadow.color = new Color(0f, 0f, 0f, 0.75f);
            shadow.type = Image.Type.Filled;
            shadow.fillMethod = Image.FillMethod.Vertical;
            shadow.fillOrigin = (int)Image.OriginVertical.Bottom;
            shadow.fillAmount = 0f;

            _swell.Add(shadow);

            var label = KeyLabel(frame.transform, slot);
            label.text = slot == 0 ? "1" : (slot + 1).ToString();
        }

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

        /// <summary>Fills or empties each slot's shadow as its skill is spent and comes back.</summary>
        private void Cool()
        {
            for (var i = 0; i < _skills.Count && i < _swell.Count; i++)
            {
                var left = _ready[i] - Time.time;

                _swell[i].fillAmount = left <= 0f ? 0f : Mathf.Clamp01(left / Mathf.Max(_skills[i].cooldown, 0.01f));
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

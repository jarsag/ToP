using System;
using UnityEngine;
using Top.Client.Game.World;
using Top.Logging;

namespace Top.Client.App
{
    /// <summary>
    /// A thing carried in a hand rather than worn on the body, and where it hangs: in the
    /// hand while the hero is out of a safe zone, on his back while he is in one, which is
    /// how the client showed a weapon that was not in use. The thing is a model whose own
    /// skeleton nothing drives, so it is hung whole on a dummy of the rig's skeleton and
    /// follows that dummy wherever the animation takes it.
    /// </summary>
    public class CarriedItem : MonoBehaviour
    {
        /// <summary>
        /// Which colour the thing's own glow is, out of the four the client's items carry: a red, a
        /// blue, a yellow and a green. <br/>
        /// A glowing item is not one model but two laid over each other - the item's own material, and a
        /// second additive one stretched across the same triangles - and it is that second one the glow
        /// is: the client gives it this colour's texture rather than modelling the light. A model with a
        /// single material has no such layer and does not glow, whatever is chosen here.
        /// </summary>
        public enum GlowColour
        {
            None,
            Red,
            Blue,
            Yellow,
            Green,
        }

        /// <summary>
        /// The colour to glow when nothing says otherwise - which is the case for a thing carried on its
        /// own, with no hero to ask. A thing the hero carries takes its colour from him instead, so that
        /// changing the setting changes what is already in his hand.
        /// </summary>
        [SerializeField] private GlowColour _glow = GlowColour.None;

        [SerializeField] private float _glowStrength = 1f;
        /// <summary>Which hand it belongs to: five right, six left.</summary>
        private int _slot = 5;

        private HeroModel _hero;

        /// <summary>Where it hangs now, for anything that wants to hang beside it - an effect, say.</summary>
        public Transform Where => _mount;

        private string _name;

        private Transform _mount;

        private bool _safe;

        private bool _hung;

        private GlowColour _lit;

        private float _strength = -1f;

        /// <summary>The renderer the glow is drawn on, once it is known or has been made.</summary>
        private MeshRenderer _shell;

        /// <summary>Whether that layer came with the model rather than being made here.</summary>
        private bool _own;

        /// <summary>The layer the glow is drawn on: the second material of a model that has one.</summary>
        private const int GlowLayer = 1;

        /// <summary>How long the client's glow takes to walk its texture round once.</summary>
        private const float LitAnimationSeconds = 6f;

        /// <summary>The clock the glow drifts on, which belongs to the scene rather than to this thing.</summary>
        private float _clock;

        /// <summary>Tells it where it belongs, which is what the hero knows and it does not.</summary>
        public void Belong(HeroModel hero, int slot)
        {
            _hero = hero;
            _slot = slot;
            _hung = false;
        }

        /// <summary>
        /// Tells it what colour to glow and how hard, when nothing else does. The hero's own settings
        /// are asked for instead whenever there is a hero, so this is only what a thing carried by
        /// nothing wears.
        /// </summary>
        public void Glow(GlowColour colour, float strength)
        {
            _glow = colour;
            _glowStrength = strength;
        }

        private void LateUpdate()
        {
            // The scene's own clock, so that a weapon taken up later is at the same place in the glow's
            // walk as one that has been carried all along - the light is moving over the item, not
            // starting when someone first looked at it.
            _clock = Time.timeSinceLevelLoad;

            var safe = Zone.IsSafe(transform.root.position);

            // Hung again not only when the state changes but whenever the name it should hang on does,
            // which is what makes pointing one at a node work while the game runs.
            var wanted = Mount(safe);

            if (!_hung || safe != _safe || _mount == null || _name != wanted)
            {
                _hung = true;
                _safe = safe;

                Hang(safe);
            }

            // The way it sits is read every frame rather than once: the angles are tuned in the
            // inspector while the game runs, and a weapon that ignored a change until the next time it
            // was put on would be a weapon nobody could line up.
            Dress(safe);

            // The glow too, for the same reason. The colour belongs to the hero's settings and is not
            // held here, so a colour picked while the game runs arrives on the next frame.
            var colour = _hero != null ? _hero.GlowColour : GlowColour.None;
            var strength = _hero != null ? _hero.GlowStrength : 1f;

            Colour(colour != _lit || !Mathf.Approximately(_strength, strength), colour, strength);
        }

        /// <summary>
        /// Lays the chosen colour over the thing's own glow layer, if it has one. <br/>
        /// The layer keeps the place its own material had, because a renderer draws one layer per
        /// material and a layer taken away would shift the ones behind it onto the wrong triangles. A
        /// glow that is off is drawn nowhere, the layer's own art being a full sheet of colour.
        /// </summary>
        private void Colour(bool changed, GlowColour colour, float strength)
        {
            // The glow is walked every frame, not only when the colour changes: the walk is the
            // animation, and one that stopped the moment the setting settled would leave the light
            // standing still on the item.
            if (!changed)
            {
                Drift();

                return;
            }

            _lit = colour;
            _strength = strength;

            var skin = Shell();
            var texture = colour == GlowColour.None
                ? null
                : Resources.Load<Texture2D>($"Effect/glow_{colour.ToString().ToLowerInvariant()}");

            if (skin == null)
            {
                if (colour != GlowColour.None)
                {
                    Log.Info($"{name} has no shape to lay a glow over");
                }

                return;
            }

            _shell = skin;

            // A layer of the model's own is one material among several on one renderer, and it keeps
            // its place: taking it out of the list would shift the materials behind it onto the wrong
            // triangles. A layer made here is a renderer of its own and is simply switched off.
            if (_own)
            {
                var worn = skin.sharedMaterials;

                worn[GlowLayer] = Material(texture, strength);

                skin.sharedMaterials = worn;
            }
            else
            {
                skin.enabled = colour != GlowColour.None;
                skin.sharedMaterial = Material(texture, strength);
            }

            Drift();
        }

        /// <summary>
        /// Walks a glow's texture across itself, one whole tile and round again. <br/>
        /// Taken from the client's own animation for a lit item - lwLitAnimTexCoord360posuv, the one
        /// every entry of its item data names - which moves the texture's coordinates from nought to
        /// one over three hundred and sixty frames and starts over. Six seconds a pass, at the sixty
        /// frames a second the client counts in. It is why a lit item in the client looks as if light
        /// is moving over it rather than a flat sheet of colour lying on it.
        /// </summary>
        private void Drift()
        {
            if (_lit == GlowColour.None)
            {
                return;
            }

            var skin = Shell();
            var material = skin == null ? null : skin.sharedMaterial;

            if (_own)
            {
                var worn = skin.sharedMaterials;

                material = worn.Length > GlowLayer ? worn[GlowLayer] : null;
            }

            if (material == null)
            {
                return;
            }

            var at = Mathf.Repeat(_clock / LitAnimationSeconds, 1f);

            material.mainTextureOffset = new Vector2(at, at);
        }

        /// <summary>
        /// The layer the glow is drawn on, made the first time it is wanted. <br/>
        /// An item whose model carries a second, additive layer already glows - the client turns that
        /// one on and off - and that layer is used as it stands. A model with a single layer has
        /// nothing to turn on, and one is made for it here: the same triangles, drawn a second time
        /// additively over themselves. That is what the client's glow is, and making it from the shape
        /// that is already there is what lets an item the client's own data left dark be lit.
        /// </summary>
        private MeshRenderer Shell()
        {
            if (_shell != null)
            {
                return _shell;
            }

            var skin = GetComponentInChildren<MeshRenderer>();
            var worn = skin == null ? null : skin.sharedMaterials;

            // A model that came with a layer of its own.
            if (worn != null && worn.Length > GlowLayer)
            {
                _shell = skin;
                _own = true;

                return _shell;
            }

            var filter = GetComponentInChildren<MeshFilter>();

            if (filter == null || filter.sharedMesh == null)
            {
                return null;
            }

            var layer = new GameObject("Glow");
            var made = layer.AddComponent<MeshRenderer>();
            var shape = layer.AddComponent<MeshFilter>();

            layer.transform.SetParent(filter.transform, worldPositionStays: false);
            layer.transform.localPosition = Vector3.zero;
            layer.transform.localRotation = Quaternion.identity;
            layer.transform.localScale = Vector3.one;

            shape.sharedMesh = filter.sharedMesh;
            made.enabled = false;

            _shell = made;

            return _shell;
        }

        /// <summary>
        /// An additive material for a glow layer. <br/>
        /// The client's glow is a second pass over the same triangles rather than anything modelled, so
        /// it is drawn additively - the light is added to what is under it rather than replacing it.
        /// </summary>
        private static Material Material(Texture2D texture, float strength)
        {
            var shader = Shader.Find("Legacy Shaders/Particles/Additive");

            if (shader == null)
            {
                shader = Shader.Find("Particles/Additive");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            var made = new Material(shader);

            if (texture != null)
            {
                made.mainTexture = texture;
            }

            made.color = new Color(1f, 1f, 1f, Mathf.Clamp01(strength));

            return made;
        }

        /// <summary>Puts the thing the way the hero says it should look, in hand or on back.</summary>
        private void Dress(bool safe)
        {
            if (_hero == null)
            {
                return;
            }

            transform.localRotation = Quaternion.Euler(safe ? _hero.BackRotation : _hero.HandRotation);
            transform.localPosition = safe ? _hero.BackOffset : _hero.CarryOffset;
            transform.localScale = Vector3.one * _hero.CarryScale;
        }

        /// <summary>Puts the thing on the mount the state asks for.</summary>
        private void Hang(bool safe)
        {
            var name = Mount(safe);
            var mount = Find(name);

            if (mount == null)
            {
                Log.Warning($"there is no mount '{name}' to carry anything on");

                return;
            }

            // A mount inside something switched off would carry the thing into hiding with it.
            if (!mount.gameObject.activeInHierarchy)
            {
                for (var at = mount; at != null && at != transform.root; at = at.parent)
                {
                    at.gameObject.SetActive(true);
                }
            }

            transform.SetParent(mount, worldPositionStays: false);

            _name = name;

            if (_mount != mount)
            {
                _mount = mount;

                Log.Info($"a thing carried in slot {_slot} hangs on '{mount.name}' (in a safe zone: {safe})");
            }
        }

        /// <summary>Which dummy it hangs on, out of the hand it belongs to and the state.</summary>
        private string Mount(bool safe)
        {
            if (_hero == null)
            {
                return _slot == 6 ? "dummy_6" : "dummy_9";
            }

            // Asked every frame rather than remembered: the names are settings, and one of them can
            // be pointed at a node while the game runs.
            if (_slot == 6)
            {
                return safe ? _hero.BackLeft : _hero.MountLeft;
            }

            return safe ? _hero.BackRight : _hero.MountRight;
        }

        /// <summary>A dummy of the hero, preferring one that is switched on.</summary>
        private Transform Find(string name)
        {
            var root = transform.root;
            Transform sleeping = null;

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (child.gameObject.activeInHierarchy)
                {
                    return child;
                }

                sleeping = sleeping != null ? sleeping : child;
            }

            return sleeping;
        }
    }
}
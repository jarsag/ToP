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
        /// The stopgap colour to use when nothing else is asked for, which is the case for a thing
        /// carried on its own with no hero to ask. A thing the hero carries takes its colour from him,
        /// so that changing the setting changes what is already in his hand.
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

        /// <summary>How long one breath of the glow takes, and how far down it breathes.</summary>
        private const float BreathSeconds = 2.4f;

        private const float BreathDepth = 0.45f;

        /// <summary>The clock the glow breathes on, which belongs to the scene rather than to this thing.</summary>
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

            // The glow too, for the same reason. The colour and its strength belong to the hero's
            // settings and are not held here, so anything picked while the game runs arrives next frame.
            var colour = _hero != null ? _hero.GlowColour : _glow;
            var strength = _hero != null ? _hero.GlowStrength : _glowStrength;

            Colour(!Mathf.Approximately(_strength, strength) || colour != _lit, colour, strength);
        }

        /// <summary>
        /// Lays the chosen colour over the thing's own glow layer, if it has one. <br/>
        /// The layer keeps the place its own material had, because a renderer draws one layer per
        /// material and a layer taken away would shift the ones behind it onto the wrong triangles. A
        /// glow that is off is drawn nowhere, the layer's own art being a full sheet of colour.
        /// </summary>
        private void Colour(bool changed, GlowColour colour, float strength)
        {
            // The glow breathes every frame rather than only when the colour changes: the breathing is
            // the animation, and one that stopped as soon as the setting settled would leave a flat
            // sheet of colour lying on the item.
            if (!changed)
            {
                Breathe();

                return;
            }

            _lit = colour;
            _strength = strength;

            var skin = Shell();

            if (skin == null)
            {
                if (colour != GlowColour.None)
                {
                    Log.Info($"{name} has no shape to lay a glow over");
                }

                return;
            }

            _shell = skin;

            // A layer of the model's own already carries the art and the blend the client gave it, so it
            // is tinted rather than replaced: a material made from nothing has no texture, and an
            // additive shader with no texture draws white whatever colour is asked for. A layer made
            // here has no art of its own and is ours to build.
            if (_own)
            {
                Tint(colour, strength);
            }
            else
            {
                var layer = skin.sharedMaterial;

                if (layer != null)
                {
                    layer.color = Tone(colour, colour == GlowColour.None ? 0f : strength);
                }

                // A layer made here keeps the art it was given; only its colour moves.
                skin.enabled = colour != GlowColour.None;
            }

            Breathe();
        }

        /// <summary>
        /// Puts a colour on the glow layer the model brought, leaving its own art and blend alone. A
        /// glow that is off is drawn nowhere rather than left in its own colour, which would light the
        /// item whether one asked for it or not.
        /// </summary>
        private void Tint(GlowColour colour, float strength)
        {
            var worn = _shell.sharedMaterials;

            if (worn == null || worn.Length <= GlowLayer || worn[GlowLayer] == null)
            {
                return;
            }

            worn[GlowLayer].color = Tone(colour, colour == GlowColour.None ? 0f : strength);
        }

        /// <summary>
        /// Lets the glow breathe: its brightness rises and falls a little, slowly, so that it reads as
        /// something alight rather than a painted colour. <br/>
        /// One breath every couple of seconds. The client gets this from an animation of its own - it
        /// moves a glow's texture across the item - and this is the same idea kept simple: no texture
        /// to move, just the light easing in and out.
        /// </summary>
        private void Breathe()
        {
            if (_lit == GlowColour.None || _shell == null)
            {
                return;
            }

            var at = Mathf.Sin(_clock / BreathSeconds * Mathf.PI * 2f) * 0.5f + 0.5f;
            var strength = _strength * Mathf.Lerp(1f - BreathDepth, 1f, at);

            // The layer keeps whatever art and blend the model gave it, so only the tint moves.
            if (_own)
            {
                Tint(_lit, strength);

                return;
            }

            var layer = _shell.sharedMaterial;

            if (layer != null)
            {
                layer.color = Tone(_lit, strength);
            }
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

            // The layer wears the item's own art. A material with no texture draws white under an
            // additive shader whatever colour is asked of it, and a tint over white is nearly white -
            // so the art the item already has is what the colour is laid on.
            var art = skin != null ? skin.sharedMaterial : null;

            made.sharedMaterial = Material(
                GlowColour.None,
                0f,
                art != null ? art.mainTexture : null);

            made.enabled = false;

            _shell = made;

            return _shell;
        }

        /// <summary>
        /// An additive material for a glow layer made here. <br/>
        /// The client's glow is a second pass over the same triangles rather than anything modelled, so
        /// it is drawn additively - the light is added to what is under it rather than replacing it. The
        /// art is the item's own, and the colour is laid over it.
        /// </summary>
        private static Material Material(GlowColour colour, float strength, Texture texture)
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

            var made = new Material(shader) { color = Tone(colour, strength) };

            if (texture != null)
            {
                made.mainTexture = texture;
            }

            return made;
        }

        /// <summary>
        /// The colour a glow of this kind is, at a given strength. <br/>
        /// The four are the ones the client's items carry, and they are what the client's own glow
        /// textures are: a flat sheet of one colour, which is why a colour of our own is the same thing
        /// without the file. The alpha carries the strength, an additive material taking it as how much
        /// light to add.
        /// </summary>
        private static Color Tone(GlowColour colour, float strength)
        {
            switch (colour)
            {
                case GlowColour.Red:
                    return new Color(1f, 0.16f, 0.08f, Mathf.Clamp01(strength));

                case GlowColour.Blue:
                    return new Color(0.2f, 0.45f, 1f, Mathf.Clamp01(strength));

                case GlowColour.Yellow:
                    return new Color(1f, 0.85f, 0.25f, Mathf.Clamp01(strength));

                case GlowColour.Green:
                    return new Color(0.25f, 1f, 0.3f, Mathf.Clamp01(strength));

                default:
                    return new Color(1f, 1f, 1f, 0f);
            }
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
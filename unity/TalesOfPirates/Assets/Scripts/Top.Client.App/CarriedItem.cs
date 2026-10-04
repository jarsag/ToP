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

        /// <summary>
        /// How a glow layer made here lays its sheet over the item: how many times it repeats, and how
        /// far it is shifted. <br/>
        /// A sheet of one's own comes at a size of its own, and an item of a size of its own as well -
        /// so how much of the sheet ends up on the item is a matter of taste rather than of arithmetic.
        /// The default is the shape of a blade: one unit across and two and a bit along, which is what
        /// the client's own sword measures - a sheet keeping its own proportions over it is a picture
        /// rather than a smear.
        /// </summary>
        [SerializeField] private Vector2 _glowTiling = new Vector2(1f, 2.2f);

        [SerializeField] private Vector2 _glowOffset = Vector2.zero;

        /// <summary>
        /// How fast the glow's sheet drifts over the item, in tiles a second on each axis. Nothing is
        /// still about a light: a sheet with a figure in it, moved, reads as light about the item and
        /// not as a pattern painted on it.
        /// </summary>
        [SerializeField] private Vector2 _glowDrift = new Vector2(0.02f, 0.01f);

        /// <summary>
        /// Everything darker than this on the glow's sheet is left out rather than added. A sheet with
        /// dark figures in it would otherwise lift the whole item faintly instead of lighting them.
        /// </summary>
        [SerializeField] private float _glowCutoff;

        /// <summary>
        /// How far the glow's colour is from grey: nought leaves it grey, one is the colour as it
        /// stands, and past one it is pushed further. A glow too vivid for the item under it, or too
        /// pale to be seen on it, is a matter of this rather than of which colour was picked.
        /// </summary>
        [SerializeField] private float _glowSaturation = 1f;

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

        /// <summary>
        /// What the glow shader calls its properties. Set by name rather than by a renderer's shorthand,
        /// which reaches only a property called <c>_Color</c> - a colour of one's own under any other
        /// name being missed by it, silently.
        /// </summary>
        private const string Sheet = "_BaseMap";

        private const string Tint = "_GlowColour";

        private const string Strength = "_Opacity";

        private const string Cutoff = "_Cutoff";

        private const string Saturation = "_Saturation";

        private const string Scale = "_GlowScale";

        private const string Shift = "_GlowOffset";

        /// <summary>The property a shader of the engine's own calls its colour.</summary>
        private const string Legacy = "_Color";

        /// <summary>How long one breath of the glow takes, and how far down it breathes.</summary>
        private const float BreathSeconds = 2.4f;

        private const float BreathDepth = 0.45f;

        /// <summary>The clock the glow breathes on, which belongs to the scene rather than to this thing.</summary>
        private float _clock;

        /// <summary>
        /// The material the glow layer of a model's own carried before anything was put on it, kept so
        /// that asking for no glow gives the item its own layer back rather than leaving a made one.
        /// </summary>
        private Material _glowBack;

        /// <summary>The white spot a glow layer made here is drawn with, made once and shared.</summary>
        private static Texture2D _shape;

        /// <summary>
        /// How the sheet of a glow layer made here is laid over the item, from the settings: how many
        /// times it repeats, where it starts, and how far it drifts a second from there.
        /// </summary>
        private Vector2 _tiling = Vector2.one;

        private Vector2 _offset = Vector2.zero;

        private Vector2 _speed = new Vector2(0.02f, 0.01f);

        private float _edge;

        private float _depth = 1f;

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

            // The glow too, for the same reason. The colour, its strength and how its sheet is laid
            // are the hero's settings and are not held here, so anything picked while the game runs
            // arrives next frame.
            var colour = _hero != null ? _hero.GlowColour : _glow;
            var strength = _hero != null ? _hero.GlowStrength : _glowStrength;
            var tiling = _hero != null ? _hero.GlowTiling : _glowTiling;
            var offset = _hero != null ? _hero.GlowOffset : _glowOffset;
            var speed = _hero != null ? _hero.GlowDrift : _glowDrift;
            var edge = _hero != null ? _hero.GlowCutoff : _glowCutoff;
            var depth = _hero != null ? _hero.GlowSaturation : _glowSaturation;

            _tiling = tiling;
            _offset = offset;
            _speed = speed;
            _edge = edge;
            _depth = depth;

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

            // A layer of the model's own carries the client's own art and blend for it, and the colour
            // comes from that art rather than from a tint - so what is put on it is how much of it is
            // added. A layer made here has no art of its own, and is built from the client's glow sheet
            // for the colour asked for.
            if (_own)
            {
                Set(GlowLayer, _glowBack);

                if (colour != GlowColour.None)
                {
                    Set(GlowLayer, Material(colour, strength));
                }
            }
            else
            {
                skin.enabled = colour != GlowColour.None;
                skin.sharedMaterial = Material(colour, strength);
            }

            Breathe();
        }

        /// <summary>Puts a material on one layer of the glow layer's renderer, leaving the rest.</summary>
        private void Set(int layer, Material material)
        {
            var worn = _shell.sharedMaterials;

            if (worn == null || worn.Length <= layer)
            {
                return;
            }

            worn[layer] = material;

            _shell.sharedMaterials = worn;
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

            // A layer of the model's own carries the client's art for it, so what moves is how much of
            // that art is added rather than what colour it is. White and an alpha: the colour is in the
            // picture, and tinting it as well would only dull what the client drew.
            var layer = _own && _shell.sharedMaterials.Length > GlowLayer
                ? _shell.sharedMaterials[GlowLayer]
                : _shell.sharedMaterial;

            if (layer != null)
            {
                Paint(layer, strength);

                // And the sheet drifts, so that the figure in it moves over the item rather than
                // sitting on it. Wound round so the numbers stay small: a sheet is one tile across,
                // and how many times it has gone round is nothing anyone can see.
                Place(layer, new Vector2(
                    Mathf.Repeat(_offset.x + (_clock * _speed.x), 1f),
                    Mathf.Repeat(_offset.y + (_clock * _speed.y), 1f)));
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

            // A model that came with a layer of its own. Its material is put back when no glow is asked
            // for, so the item keeps the art and the blend its author gave it while it is dark.
            if (worn != null && worn.Length > GlowLayer)
            {
                _shell = skin;
                _own = true;
                _glowBack = worn[GlowLayer];

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

            made.sharedMaterial = Material(GlowColour.None, 0f);

            made.enabled = false;

            _shell = made;

            return _shell;
        }

        /// <summary>
        /// The art a glow layer made here wears. <br/>
        /// A sheet of one's own comes first, when there is one: it is the shape of the light, and a
        /// shape someone chose is better than any of ours. Failing that the client's own sheet for the
        /// colour is used, which is a soft spot already drawn in that colour, and failing that a soft
        /// white spot made in code. A white sheet is what the colour setting is for - it takes any - so
        /// a grey or white sheet of one's own is the one to put here.
        /// </summary>
        private static Texture Light(GlowColour colour)
        {
            var chosen = Resources.Load<Texture2D>("Effect/glow_custom");

            if (chosen != null)
            {
                return chosen;
            }

            return colour == GlowColour.None
                ? null
                : Resources.Load<Texture2D>($"Effect/glow_{colour.ToString().ToLowerInvariant()}");
        }

        /// <summary>
        /// An additive material for a glow layer made here. <br/>
        /// The client's glow is a second pass over the same triangles rather than anything modelled, so
        /// it is drawn additively - the light is added to what is under it rather than replacing it. The
        /// art is the item's own, and the colour is laid over it.
        /// </summary>
        /// <summary>
        /// An additive material for a glow layer made here, drawn by the project's own glow shader so
        /// that the colour and the opacity are settings rather than something baked into a picture.
        /// <br/>
        /// The shader adds to what is under it rather than laying over it - a glow is light, and light
        /// adds - and it takes the shape of the light from its sheet while the colour comes from the
        /// material. The client's own sheet for this colour is used as that shape, being a soft spot of
        /// the right size; a sheet of one's own can be put there instead.
        /// <br/>
        /// The shader's properties are set by name. A renderer's shorthand for a material's colour is
        /// the property called <c>_Color</c>, and a shader with a colour of its own under another name
        /// is not reached by it - which is a setting that takes effect everywhere except where it is
        /// wanted.
        /// </summary>
        private Material Material(GlowColour colour, float strength)
        {
            var shader = Shader.Find("Top/Glow");

            if (shader == null)
            {
                shader = Shader.Find("Legacy Shaders/Particles/Additive");
            }

            var made = new Material(shader);

            var sheet = Light(colour) ?? Shape();

            if (sheet != null)
            {
                made.SetTexture(Sheet, sheet);
            }

            Place(made, _offset);
            Paint(made, strength);

            return made;
        }

        /// <summary>
        /// Says how the glow's sheet lies over the item: how big it is and where it starts, in the
        /// item's own space rather than in its texture coordinates, so that the sheet lies the same way
        /// over every part of a model built from pieces that are turned against each other.
        /// </summary>
        private void Place(Material made, Vector2 offset)
        {
            // The sheet is one tile across, so a scale of one puts a whole sheet over one unit of the
            // item, whatever that unit is. In the item's own space the two axes are the item's own
            // width and its own length - which is what makes a sheet lie along a blade rather than
            // across it.
            made.SetVector(Scale, new Vector4(
                _tiling.x <= 0f ? 1f : _tiling.x,
                1f,
                _tiling.y <= 0f ? 1f : _tiling.y,
                0f));

            made.SetVector(Shift, new Vector4(offset.x, offset.y, 0f, 0f));
        }

        /// <summary>Puts the colour on a glow material, whatever the shader calls its colour.</summary>
        private void Paint(Material made, float strength)
        {
            var tint = Tone(_lit, strength);

            made.SetColor(Tint, tint);
            made.SetColor(Legacy, Color.white);
            made.SetFloat(Cutoff, _edge);
            made.SetFloat(Saturation, _depth);

            // On the engine's own additive shader the colour is the picture's, so what is put there is
            // the sheet of that colour; ours takes the colour itself.
            var sheet = Light(_lit);

            if (sheet != null && made.HasProperty(Sheet))
            {
                made.SetTexture(Sheet, sheet);
            }

            made.SetFloat(Strength, 1f);
        }

        /// <summary>
        /// The colour a glow of this kind is, with how hard it is laid on in its alpha.
        /// <br/>
        /// The four are the ones the client's items carry. They are set here rather than taken from the
        /// client's sheets because a sheet's colour cannot be changed: the shader multiplies, so a sheet
        /// drawn in red stays red whatever is asked for, and a sheet drawn in grey takes any colour -
        /// which is what a sheet of one's own is for.
        /// </summary>
        private static Color Tone(GlowColour colour, float strength)
        {
            var alpha = Mathf.Clamp01(strength);

            switch (colour)
            {
                case GlowColour.Red:
                    return new Color(1f, 0.3f, 0.2f, alpha);

                case GlowColour.Blue:
                    return new Color(0.4f, 0.6f, 1f, alpha);

                case GlowColour.Yellow:
                    return new Color(1f, 0.88f, 0.45f, alpha);

                case GlowColour.Green:
                    return new Color(0.5f, 1f, 0.55f, alpha);

                default:
                    return new Color(1f, 1f, 1f, 0f);
            }
        }

        /// <summary>
        /// The shape of the light a glow layer made here is drawn with: a soft white spot, brightest at
        /// its middle and fading to nothing at its edge, made in code rather than carried as a file.
        /// <br/>
        /// White, so that the colour of the glow is the material's and nothing else - the client's own
        /// glow sheet already carries a colour of its own, which is why it is preferred when there is
        /// one. And round, being a light rather than a shape: what it is laid on is the item.
        /// </summary>
        private static Texture2D Shape()
        {
            if (_shape != null)
            {
                return _shape;
            }

            const int size = 256;

            _shape = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "glow shape",
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[size * size];

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = ((x / (size - 1f)) * 2f) - 1f;
                    var dy = ((y / (size - 1f)) * 2f) - 1f;
                    var away = Mathf.Clamp01(Mathf.Sqrt((dx * dx) + (dy * dy)));

                    // A flat core with a long fall-off, which is what makes it read as light rather
                    // than as a pale disc.
                    var light = 1f - Mathf.SmoothStep(0f, 1f, away);

                    pixels[(y * size) + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(light) * 255f));
                }
            }

            _shape.SetPixels32(pixels);
            _shape.Apply(true);

            return _shape;
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

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Top.Logging;

namespace Top.Client.App
{
    // Plays one of the client's effects. The .eff is the shape of a single particle; the .par is the
    // system that throws those particles about. The shapes are built exactly as the client builds
    // them - see the truth table from I_Effect.cpp, kept in the shape builders below.
    public class EffectView : MonoBehaviour
    {
        [SerializeField] private bool _on;
        [SerializeField] private string _effect = "jj03";
        [SerializeField] private string _mount = "dummy_9";

        // Hang where the weapon hangs, wherever that is: a weapon moves between the hand and the
        // back as the hero comes and goes, and an effect on it has to move too. The mount above is
        // used when there is no weapon to follow.
        [SerializeField] private bool _withWeapon = true;
        // How the effect is turned on the node it hangs from. A weapon is a model with a skeleton of its
        // own, and its nodes face wherever its author left them, so an effect that comes out square to
        // the blade is turned here - a quarter turn about one axis, usually.
        [SerializeField] private Vector3 _holderTurn = Vector3.zero;

        // Play it once and be done, as the client does, or keep throwing particles for as long as it is
        // switched on. Once is the default: an effect is a flash, not a fountain.
        // How long a particle lives and how often another is let go, if the effect has no .par to say
        // so. With one, its own numbers are used and these are not read - they were written by hand to
        // give the effect something to age by before the .par was read at all.
        [SerializeField] private float _life = 4.5f;
        [SerializeField] private float _rate = 6f;

        // Once, the effect ran its length and stopped until it was switched off and on again. It now
        // breathes for as long as the scene lasts, so there is nothing left to switch: the field stays
        // only so that scenes carrying it keep their meaning, and is out of the inspector.
        [HideInInspector] [SerializeField] private bool _once = true;

        [SerializeField] private bool _key = true;

        // How fast the effect turns about the node it hangs from, in degrees a second.
        [SerializeField] private float _spin = 60f;
        [SerializeField] private float _scale = 1f;

        [Serializable]
        private class Emitter
        {
            public string name;
            public string type;
            public string texture;
            public string model;
            public float length;
            public int source;
            public int destination;
            public int alpha;
            public float[] size;
            public float[] position;
            public float[] angle;
            public int segments;
            public float height;
            public float topRadius;
            public float bottomRadius;
            public int rotationLoop;
            public float[] rotationLoopVector;

            // The texture's own animation. An effect whose art is a few still pictures in one image
            // animates by nothing else: an effect of type 3 carries several sets of coordinates into
            // that one image and steps between them on a timer of its own, so the stills are windows
            // onto the art rather than frames in a sequence. The sets arrive as one run of numbers
            // with a stride, because the JSON a scene carries cannot hold a list of lists.
            public int effectType;
            public float textureFrameTime;
            public float[] textureLists;
            public int textureFrameStride;
        }

        [Serializable]
        private class Sheet
        {
            public int version;
            public int technique;
            public Emitter[] emitters;
        }

        // The .par beside an effect: the system that throws one copy of it about rather than the copy
        // itself. An .eff is the shape of a single particle; the .par says how long that particle
        // lives, how often another is let go, and how many may be alive at once - which is what decides
        // whether an effect is a flash or something that breathes for as long as the scene lasts.
        [Serializable]
        private class Par
        {
            public int version;
            public string partName;
            public float length;
            public ParEmitter[] emitters;
        }

        [Serializable]
        private class ParEmitter
        {
            public string type;
            public string partName;
            public string model;
            public string texture;

            // How long each particle lives, how often one is let go, and how many may be alive at once.
            public int count;
            public float life;
            public float step;
            public float delayTime;
            public float playTime;
        }

        private class Particle
        {
            public Transform Root;
            public float Start;

            // How long this copy lives, and which system let it go - the fields a system owns rather
            // than the copy.
            public float Life;
            public ParEmitter Emitter;

            // The shapes of one particle, kept so the texture's own animation can be stepped into
            // them without looking for them again every frame, and the window each is showing.
            public MeshFilter[] Meshes;
            public int[] Frames;
        }

        private readonly List<Particle> _particles = new List<Particle>();
        private Sheet _sheet;

        // The system that throws a copy of the effect about, when the effect has one.
        private Par _par;

        private Transform _holder;

        // The clock everything is measured on, and what is owed to the emitters. Together they are why
        // an effect breathes for as long as the scene lasts rather than running its length and stopping.
        private float _clock;
        private float _owed;

        private void Start()
        {
            // The clock starts with the scene and never starts again, so an effect that is switched on
            // part way through comes in where it would have been, as the client's does - it does not
            // begin from nothing because the object holding it was only just switched on.
            _clock = Time.timeSinceLevelLoad;

            if (_on)
            {
                Begin();
            }
        }

        private void Update()
        {
            if (_key)
            {
                var keyboard = Keyboard.current;

                if (keyboard != null && keyboard.f9Key != null && keyboard.f9Key.wasPressedThisFrame)
                {
                    _on = !_on;

                    Log.Info($"effect {_effect}: {(_on ? "on" : "off")}");
                }
            }

            _clock = Time.timeSinceLevelLoad;

            if (_on)
            {
                Begin();

                // Breathe, for as long as the scene lasts. Nothing here ends the effect and nothing
                // begins it again: a particle is let go whenever the system says one is due, and taken
                // away when its own time is up, so the effect is always the same age as the scene.
                Emit();
            }
            else
            {
                Stop();
            }

            Live();
        }

        /// <summary>
        /// Lets a particle go when the system says one is due. <br/>
        /// The system is the .par: it says how long a particle lives, how often another is let go and
        /// how many may be alive at once. How often is a time between particles rather than a count a
        /// second, and a system that names none is one that lets a particle go as soon as the last has
        /// gone - which is a single particle that is replaced the moment it dies.
        /// </summary>
        private void Emit()
        {
            if (_holder == null || _sheet == null)
            {
                return;
            }

            // With no system to ask, the fields written by hand stand in: so many particles a second,
            // each living as long as Life says.
            if (_par == null || _par.emitters == null || _par.emitters.Length == 0)
            {
                _owed += Time.deltaTime;

                var every = 1f / Mathf.Max(0.5f, _rate);

                while (_owed >= every)
                {
                    _owed -= every;

                    Throw(null);
                }

                return;
            }

            foreach (var emitter in _par.emitters)
            {
                var since = _clock - emitter.delayTime;

                if (since < 0f)
                {
                    continue;
                }

                // Past its play time a system lets nothing more go; zero means it never stops.
                if (emitter.playTime > 0f && since > emitter.playTime)
                {
                    continue;
                }

                var alive = 0;

                foreach (var particle in _particles)
                {
                    if (particle.Emitter == emitter)
                    {
                        alive++;
                    }
                }

                if (alive >= emitter.count)
                {
                    continue;
                }

                // A system that names no time between particles lets one go as soon as there is room,
                // which is what makes a single particle's effect continuous rather than stuttered.
                if (emitter.step <= 0f)
                {
                    Throw(emitter);

                    continue;
                }

                _owed += Time.deltaTime;

                while (_owed >= emitter.step && alive < emitter.count)
                {
                    _owed -= emitter.step;
                    alive++;

                    Throw(emitter);
                }
            }
        }

        private void OnDisable()
        {
            Stop();
        }

        private void Begin()
        {
            if (_sheet == null)
            {
                var file = Resources.Load<TextAsset>($"Effect/{_effect}");

                if (file == null)
                {
                    Log.Warning($"no effect data at Effect/{_effect}");
                    _on = false;

                    return;
                }

                _sheet = JsonUtility.FromJson<Sheet>(file.text);

                if (_sheet == null || _sheet.emitters == null || _sheet.emitters.Length == 0)
                {
                    Log.Warning($"effect {_effect} has nothing in it");
                    _on = false;

                    return;
                }

                Log.Info($"effect {_effect}: {_sheet.emitters.Length} shape(s) per particle");

                // The system that throws a copy of the shape about, if the effect has one. Without it
                // there is still an effect - one shape at the mount - so a missing .par is not a
                // failure; the fields written by hand stand in for what it would have said.
                var system = Resources.Load<TextAsset>($"Effect/{_effect}.par");

                if (system != null)
                {
                    _par = JsonUtility.FromJson<Par>(system.text);

                    Log.Info($"effect {_effect}: .par with {(_par?.emitters?.Length ?? 0)} system(s)");
                }
            }

            var mount = Where();

            if (mount == null)
            {
                return;
            }

            if (_holder == null)
            {
                _holder = new GameObject($"Effect {_effect}").transform;
            }

            // Hung again whenever the node changes, which is what happens when the weapon moves.
            if (_holder.parent != mount)
            {
                _holder.SetParent(mount, worldPositionStays: false);
            }

            _holder.localRotation = Quaternion.Euler(_holderTurn);
        }

        /// <summary>
        /// Lets one copy of the effect go. The system it belongs to is carried with it, because that is
        /// what says how long this copy lives and when the next is due.
        /// </summary>
        private void Throw(ParEmitter from)
        {
            if (_holder == null)
            {
                return;
            }

            var root = new GameObject("Particle").transform;

            root.SetParent(_holder, worldPositionStays: false);

            var meshes = new List<MeshFilter>();
            var frames = new List<int>();

            foreach (var emitter in _sheet.emitters)
            {
                meshes.Add(Shape(emitter, root));
                frames.Add(-1);
            }

            // With no system to ask, the hand-written life stands in; with one, its own.
            var span = from != null && from.life > 0.01f ? from.life : _life;

            _particles.Add(new Particle
            {
                Root = root,
                Start = _clock,
                Life = span > 0.05f ? span : 1f,
                Emitter = from,
                Meshes = meshes.ToArray(),
                Frames = frames.ToArray(),
            });
        }

        private MeshFilter Shape(Emitter emitter, Transform root)
        {
            var shape = new GameObject($"{emitter.model} ({emitter.texture})");

            shape.transform.SetParent(root, worldPositionStays: false);

            var filter = shape.AddComponent<MeshFilter>();
            var skin = shape.AddComponent<MeshRenderer>();

            filter.sharedMesh = Built(emitter);

            var size = Way(emitter.size, 1f);
            var at = Way(emitter.position, 1f);
            var angle = Way(emitter.angle, Mathf.Rad2Deg);

            shape.transform.localPosition = at * _scale;
            shape.transform.localRotation = Quaternion.Euler(angle);
            shape.transform.localScale = size * _scale;

            skin.material = Material(Resources.Load<Texture2D>($"Effect/{emitter.texture}"));

            return filter;
        }

        /// <summary>
        /// The window onto the texture an effect of type 3 is showing at a moment, worked out the way
        /// the client's CTexFrame works it out: a set of coordinates is held for one frame's worth of
        /// time and the next is stepped to at the end of it, round and round. <br/>
        /// This is the whole of the animation for an effect whose art is a handful of still pictures in
        /// one image - no movement is stored in the file at all, only a different window onto the same
        /// texture, which is why such an effect looks like so many sprites until it is set going.
        /// </summary>
        private static int TextureFrame(Emitter emitter, float age)
        {
            var stride = emitter.textureFrameStride;

            if (emitter.effectType != 3 || emitter.textureLists == null || stride <= 0
                || emitter.textureLists.Length < stride * 2 || emitter.textureFrameTime <= 0f)
            {
                return 0;
            }

            var sets = emitter.textureLists.Length / stride;

            if (age <= 0f)
            {
                return 0;
            }

            var at = Mathf.Max(Mathf.CeilToInt(age / emitter.textureFrameTime) - 1, 0);

            return at % sets;
        }

        /// <summary>
        /// Steps every shape of a particle onto the window its texture has reached, if it has moved.
        /// <br/>
        /// The coordinates go in as they are. A shape's own corners are turned over when it is built,
        /// because the client numbers a texture from the top down and a renderer from the bottom up -
        /// but these coordinates are written for a renderer already, so turning them over as well is a
        /// second flip on top of the first: on a two by two atlas it swaps the top pair of windows with
        /// the bottom pair, and the effect is seen through the wrong half of its own art.
        /// </summary>
        private void Animate(Particle particle, float age)
        {
            for (var i = 0; i < particle.Meshes.Length && i < _sheet.emitters.Length; i++)
            {
                var filter = particle.Meshes[i];
                var emitter = _sheet.emitters[i];

                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                var frame = TextureFrame(emitter, age);

                if (particle.Frames[i] == frame)
                {
                    continue;
                }

                var stride = emitter.textureFrameStride;
                var vertices = filter.sharedMesh.vertexCount;

                if (emitter.textureLists == null || stride < vertices * 2)
                {
                    continue;
                }

                var uvs = new Vector2[vertices];
                var first = frame * stride;

                for (var v = 0; v < vertices; v++)
                {
                    uvs[v] = new Vector2(
                        emitter.textureLists[first + v * 2],
                        emitter.textureLists[first + v * 2 + 1]);
                }

                filter.sharedMesh.uv = uvs;

                particle.Frames[i] = frame;
            }
        }

        // The five built-in shapes the client's engine makes in code, and the cylinder. The vertices
        // are the client's own, from I_Effect.cpp: Rect lies flat in XZ with its Z from nothing to
        // one, RectPlane stands in XY centred, RectZ stands in YZ, and each carries the same texture
        // corners. Building them any other way - a Unity quad, say - puts every piece of an effect in
        // the wrong plane, and the pieces never come together.
        private static Mesh Built(Emitter emitter)
        {
            var name = emitter.model == null ? string.Empty : emitter.model.Trim();

            if (name.Equals("Rect", StringComparison.OrdinalIgnoreCase))
            {
                return Quad(
                    new Vector3(-0.5f, 0f, 0f),
                    new Vector3(-0.5f, 0f, 1f),
                    new Vector3(0.5f, 0f, 1f),
                    new Vector3(0.5f, 0f, 0f));
            }

            if (name.Equals("RectPlane", StringComparison.OrdinalIgnoreCase))
            {
                return Quad(
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f));
            }

            if (name.Equals("RectZ", StringComparison.OrdinalIgnoreCase))
            {
                return Quad(
                    new Vector3(0f, 0f, 0f),
                    new Vector3(0f, 0f, 1f),
                    new Vector3(0f, 1f, 1f),
                    new Vector3(0f, 1f, 0f));
            }

            if (name.Equals("Triangle", StringComparison.OrdinalIgnoreCase))
            {
                return Wedge(
                    new Vector3(0f, 0f, 0.5f),
                    new Vector3(-0.5f, 0f, 0f),
                    new Vector3(0.5f, 0f, 0f));
            }

            if (name.Equals("PlaneTriangle", StringComparison.OrdinalIgnoreCase))
            {
                return Wedge(
                    new Vector3(0f, 0.5f, 0f),
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f));
            }

            return Tube(emitter);
        }

        private static Mesh Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var mesh = new Mesh { name = "effect quad" };

            mesh.vertices = new[] { a, b, c, d };
            mesh.uv = new[] { new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        private static Mesh Wedge(Vector3 a, Vector3 b, Vector3 c)
        {
            var mesh = new Mesh { name = "effect triangle" };

            mesh.vertices = new[] { a, b, c };
            mesh.uv = new[] { new Vector2(0.5f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        // A tube of the size the emitter gives: as many sides, as tall, and as wide at the top as at
        // the bottom. It comes a metre tall and a metre across, so the scale of the frame sizes it.
        private static Mesh Tube(Emitter emitter)
        {
            var sides = emitter.segments > 2 && emitter.segments < 128 ? emitter.segments : 8;
            var top = emitter.topRadius > 0.0001f && emitter.topRadius < 100f ? emitter.topRadius : 0.5f;
            var bottom = emitter.bottomRadius > 0.0001f && emitter.bottomRadius < 100f ? emitter.bottomRadius : top;

            var mesh = new Mesh { name = "effect tube" };
            var corners = new List<Vector3>();
            var uvs = new List<Vector2>();
            var faces = new List<int>();

            for (var i = 0; i < sides; i++)
            {
                var at = (float)i / sides * Mathf.PI * 2f;
                var next = (float)(i + 1) / sides * Mathf.PI * 2f;

                var first = new Vector3(Mathf.Cos(at) * bottom, -0.5f, Mathf.Sin(at) * bottom);
                var second = new Vector3(Mathf.Cos(next) * bottom, -0.5f, Mathf.Sin(next) * bottom);
                var third = new Vector3(Mathf.Cos(next) * top, 0.5f, Mathf.Sin(next) * top);
                var fourth = new Vector3(Mathf.Cos(at) * top, 0.5f, Mathf.Sin(at) * top);

                var start = corners.Count;

                corners.Add(first);
                corners.Add(second);
                corners.Add(third);
                corners.Add(fourth);

                uvs.Add(new Vector2((float)i / sides, 0f));
                uvs.Add(new Vector2((float)(i + 1) / sides, 0f));
                uvs.Add(new Vector2((float)(i + 1) / sides, 1f));
                uvs.Add(new Vector2((float)i / sides, 1f));

                faces.Add(start);
                faces.Add(start + 1);
                faces.Add(start + 2);
                faces.Add(start);
                faces.Add(start + 2);
                faces.Add(start + 3);
            }

            mesh.SetVertices(corners);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(faces, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Ages every particle and takes away the ones whose time is up. <br/>
        /// Nothing here ends the effect: a particle finishing is not the effect finishing, and another
        /// is let go in its place, so the effect is as old as the scene for as long as the scene lasts.
        /// </summary>
        private void Live()
        {
            for (var i = _particles.Count - 1; i >= 0; i--)
            {
                var particle = _particles[i];

                if (particle.Root == null)
                {
                    _particles.RemoveAt(i);

                    continue;
                }

                var age = _clock - particle.Start;

                if (age >= particle.Life)
                {
                    Destroy(particle.Root.gameObject);
                    _particles.RemoveAt(i);

                    continue;
                }

                Animate(particle, age);

                particle.Root.localRotation = Quaternion.AngleAxis(_spin * age, Vector3.up);

                // The shapes come into place in a quarter of a second and only dim in the last half
                // second of their life, so a particle is bright for as long as it lives. Fading over a
                // share of the lifetime instead made a long-lived particle crawl into view and a
                // short-lived one blink.
                var grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 0.25f));
                var fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - (particle.Life - 0.5f)) / 0.5f));
                var tint = new Color(fade, fade, fade, fade);

                foreach (var skin in particle.Root.GetComponentsInChildren<Renderer>(true))
                {
                    if (skin.material.HasProperty("_TintColor"))
                    {
                        skin.material.SetColor("_TintColor", tint);
                    }
                    else
                    {
                        skin.material.color = tint;
                    }
                }

                particle.Root.localScale = Vector3.one * grow;
            }
        }

        private static Material Material(Texture2D texture)
        {
            var shader = Shader.Find("Legacy Shaders/Particles/Additive");

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Transparent");
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

            return made;
        }

        // The client keeps its up where Unity keeps its own, so the triples are used as they are; the
        // factor turns its radians into degrees where the triple is an angle.
        private static Vector3 Way(float[] triple, float factor)
        {
            if (triple == null || triple.Length < 3)
            {
                return Vector3.zero;
            }

            return new Vector3(triple[0] * factor, triple[1] * factor, triple[2] * factor);
        }

        // The node to hang on: wherever the weapon is, if following one, and its own mount otherwise.
        private Transform Where()
        {
            if (_withWeapon)
            {
                var carried = GetComponentInParent<CarriedItem>();

                if (carried == null)
                {
                    carried = FindAnyObjectByType<CarriedItem>();
                }

                if (carried != null)
                {
                    // Inside the weapon first: a weapon is a model with a skeleton of its own, and the
                    // points on it are the ones an effect on a blade wants. The hero's own points would
                    // put the effect at his hand rather than at the blade.
                    var onWeapon = Under(carried.transform, _mount);

                    if (onWeapon != null)
                    {
                        return onWeapon;
                    }

                    if (carried.Where != null)
                    {
                        return carried.Where;
                    }
                }
            }

            return Find(_mount);
        }

        /// <summary>A node by name among the children of one object, preferring one that is switched on.</summary>
        private static Transform Under(Transform root, string name)
        {
            Transform sleeping = null;

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child == root || child.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0)
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

        private Transform Find(string name)
        {
            Transform sleeping = null;

            foreach (var child in transform.root.GetComponentsInChildren<Transform>(true))
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

        /// <summary>
        /// Takes the particles away and leaves the node they hang from standing, so that switching the
        /// effect off and on again is not a restart: the clock both the effect's turn and its textures
        /// are measured on belongs to the scene and is never begun again.
        /// </summary>
        private void Stop()
        {
            foreach (var particle in _particles)
            {
                if (particle.Root != null)
                {
                    Destroy(particle.Root.gameObject);
                }
            }

            _particles.Clear();
        }
    }
}
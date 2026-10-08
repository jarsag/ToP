using System;
using System.Collections.Generic;
using Top.Logging;
using UnityEngine;

namespace Top.Client.App
{
    /// <summary>
    /// An effect played where and when it is wanted rather than hung on something and left running: a
    /// skill's blow, which happens at a place and is over. <br/>
    /// The shape of a client effect is what is played - the same JSON an effect hung on a weapon reads,
    /// made of the same shapes and the same texture windows - but nothing here is a scene object with
    /// settings of its own. One of these is put in the world, lives its length, and is taken away.
    /// </summary>
    public class SkillEffect : MonoBehaviour
    {
        [Serializable]
        private class Emitter
        {
            public string name;
            public string texture;
            public string model;
            public float[] size;
            public float[] position;
            public float[] angle;
            public int source;
            public int destination;
            public int effectType;
            public float textureFrameTime;
            public float[] textureLists;
            public int textureFrameStride;
            public int segments;
            public float topRadius;
            public float bottomRadius;
        }

        [Serializable]
        private class Sheet
        {
            public int version;
            public int technique;
            public Emitter[] emitters;
        }

        private MeshFilter[] _parts;

        private Sheet _sheet;

        private float _born;

        private float _length = 0.5f;

        private float _fade = 0.12f;

        /// <summary>The shapes are built and the effect starts living.</summary>
        private void Awake()
        {
            _born = Time.time;
        }

        /// <summary>
        /// Plays an effect of the client's at a place, for as long as it is told to live. <br/>
        /// Its pieces are laid out at once rather than thrown: a blow leaves the hand when the gesture
        /// reaches the frame for it, and the client's own skill effects are drawn where they start
        /// rather than flown along the way.
        /// <br/>
        /// Given a way to face, the effect is turned to look down it - which is what makes a blow read as
        /// leaving the hand that cast it: the pieces are laid around the place they start from and reach
        /// out along the way they are going. Given a tail as well, a ribbon is drawn back along the way
        /// it came, so that a chained blow is a chain of bodies joined one to the next.
        /// </summary>
        public static SkillEffect Play(string name, Vector3 at, float length = 0.5f, Transform parent = null,
            Vector3? tail = null, Vector3? facing = null, float width = 0f, float thickness = 0.15f,
            bool stretch = false, float? over = null)
        {
            var file = Resources.Load<TextAsset>($"Effect/{name}");

            if (file == null)
            {
                Log.Warning($"there is no effect called '{name}'");

                return null;
            }

            var sheet = JsonUtility.FromJson<Sheet>(file.text);

            if (sheet?.emitters == null || sheet.emitters.Length == 0)
            {
                Log.Warning($"effect '{name}' has nothing in it");

                return null;
            }

            var body = new GameObject($"Effect {name}");

            body.transform.SetParent(parent, worldPositionStays: false);
            body.transform.position = at;

            var effect = body.AddComponent<SkillEffect>();

            effect._sheet = sheet;
            effect._length = Mathf.Max(length, 0.05f);
            effect._tail = tail.HasValue ? tail.Value : at;
            effect._facing = facing;
            effect._width = width;
            effect._thickness = thickness;
            effect._stretch = stretch;
            effect._over = over;

            effect.Build();

            return effect;
        }

        /// <summary>Where the effect comes from, which is where it sits when nothing else says.</summary>
        private Vector3 _tail;

        /// <summary>The way the effect is going, when something says - a blow leaving a hand.</summary>
        private Vector3? _facing;

        /// <summary>How wide the ribbon is, as a share of how far it reaches.</summary>
        private float _width;

        /// <summary>The width the ribbon falls back to when no share is named.</summary>
        private float _thickness = 0.15f;

        /// <summary>Whether the effect is cut to the distance it is thrown over.</summary>
        private bool _stretch;

        /// <summary>How far the blow was thrown, when something says - which is what it is cut to.</summary>
        private float? _over;

        /// <summary>
        /// The arc from where the blow came from to where it landed, made of one long piece through the
        /// effect's own middle. <br/>
        /// The effect's shapes are laid around its root and this reaches from the tail to the root, so it
        /// reads as the bolt arriving - and it is the same art the effect already carries rather than a
        /// second kind of thing drawn for the occasion.
        /// </summary>
        private void Arc(string texture)
        {
            var span = transform.position - _tail;

            if (span.sqrMagnitude < 0.01f)
            {
                return;
            }

            // Across the way it goes, so that the ribbon faces the way a body does and not its own edge.
            var across = Vector3.Cross(span.normalized, Vector3.up);

            if (across.sqrMagnitude < 0.0001f)
            {
                across = Vector3.Cross(span.normalized, Vector3.forward);
            }

            across.Normalize();

            var middle = (_tail + transform.position) * 0.5f;
            var half = span.magnitude * 0.5f;

            // How wide the ribbon is: a share of how far it reaches when one is named, and a thin fixed
            // width otherwise. The client's art is a road rather than a bolt, so a ribbon drawn to its
            // own proportions is a slab.
            var wide = _width > 0f
                ? Mathf.Max(0.02f, span.magnitude * _width)
                : Mathf.Max(0.02f, _thickness);

            var mesh = new Mesh { name = "Arc" };
            var vertices = new[]
            {
                middle - (across * wide),
                middle + (across * wide),
                middle + (span.normalized * half) + (across * wide),
                middle + (span.normalized * half) - (across * wide),
                middle - (span.normalized * half) + (across * wide),
                middle - (span.normalized * half) - (across * wide),
            };

            // Lengthwise along the ribbon, so the effect's sheet runs down it the way it runs down a
            // piece that already has a length.
            mesh.vertices = vertices;
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(1f, 0f), new Vector2(0f, 0f),
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 0, 4, 0, 1 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var part = new GameObject("Arc").AddComponent<MeshFilter>();

            part.transform.SetParent(transform, worldPositionStays: false);
            part.sharedMesh = mesh;
            part.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Material(Resources.Load<Texture2D>($"Effect/{texture}"));
        }

        /// <summary>Builds one piece per shape the effect is made of, all under the effect's own root.</summary>
        private void Build()
        {
            var parts = new List<MeshFilter>();

            // Turned before anything is laid out, so that the pieces, which are placed and turned in
            // the effect's own space, come out along the way the blow is going rather than along the
            // world's own axes. The client's effects reach out along their own forward.
            if (_facing.HasValue && _facing.Value.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(_facing.Value.normalized, Vector3.up);
            }

            foreach (var emitter in _sheet.emitters)
            {
                parts.Add(Shape(emitter));
            }

            Cut();

            // The way the blow came, drawn with the first sheet the effect carries, so that a bolt out of
            // a hand reads as one thing with the mark it lands on.
            Arc(parts.Count > 0 ? _sheet.emitters[0].texture : null);

            _parts = parts.ToArray();
        }

        /// <summary>
        /// How far the art reaches along its own forward, out of the shapes it is made of. <br/>
        /// The client's pieces are laid around the place they start and stretch away from it, so the
        /// furthest of them is how far the whole thing reaches - seven and eight metres for this effect,
        /// whose pieces are roads rather than bolts.
        /// </summary>
        private float Reach()
        {
            var furthest = 0.5f;

            foreach (var emitter in _sheet.emitters)
            {
                var size = Way(emitter.size, 1f);

                if (size.z > furthest)
                {
                    furthest = size.z;
                }
            }

            return furthest;
        }

        /// <summary>
        /// Cuts the effect to the distance it was thrown over, so that a blow landing two metres away
        /// ends there instead of running eight metres out the back of the body it hit. <br/>
        /// Only the length is cut: a piece stretched sideways or upwards as well would change shape, and
        /// what is wanted is the same bolt over a shorter way. The distance is the one the blow was
        /// aimed over, handed in rather than worked out here - where the effect starts says nothing about
        /// where it was meant to land.
        /// </summary>
        private void Cut()
        {
            if (!_stretch || !_over.HasValue)
            {
                return;
            }

            var over = _over.Value;

            if (over < 0.05f)
            {
                return;
            }

            var reach = Reach();

            // Never longer than the art's own reach: a blow thrown further than the art can go is drawn
            // at its full length rather than stretched into something the client never drew.
            var share = Mathf.Min(over / reach, 1f);

            // Never so short that nothing is left: a blow landing on the caster's own boots still has to
            // be drawn as something.
            transform.localScale = new Vector3(1f, 1f, Mathf.Max(share, 0.05f));
        }

        private MeshFilter Shape(Emitter emitter)
        {
            var shape = new GameObject($"{emitter.model} ({emitter.texture})");

            shape.transform.SetParent(transform, worldPositionStays: false);

            var filter = shape.AddComponent<MeshFilter>();
            var skin = shape.AddComponent<MeshRenderer>();

            filter.sharedMesh = Built(emitter);

            shape.transform.localPosition = Way(emitter.position, 1f);
            shape.transform.localRotation = Quaternion.Euler(Way(emitter.angle, Mathf.Rad2Deg));
            shape.transform.localScale = Way(emitter.size, 1f);

            skin.material = Material(Resources.Load<Texture2D>($"Effect/{emitter.texture}"));

            return filter;
        }

        /// <summary>
        /// Steps every piece onto the window its texture has reached, and takes the effect away when
        /// its time is up. <br/>
        /// The window steps the way the client steps it - a set of coordinates held for one frame's
        /// worth of time and the next reached at the end of it - which is the whole of the animation
        /// for an effect whose art is a few stills in one image.
        /// </summary>
        private void Update()
        {
            var age = Time.time - _born;

            if (age >= _length)
            {
                Destroy(gameObject);

                return;
            }

            // In over the first moment and out over the last, so that a blow arrives rather than
            // appearing: a sheet of art switched on at full strength reads as a sticker.
            var rise = Mathf.Clamp01(age / Mathf.Max(_fade, 0.01f));
            var fall = 1f - Mathf.Clamp01((age - (_length - _fade)) / Mathf.Max(_fade, 0.01f));
            var face = Mathf.Min(rise, fall);

            for (var i = 0; i < _parts.Length && i < _sheet.emitters.Length; i++)
            {
                var filter = _parts[i];
                var emitter = _sheet.emitters[i];

                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                Window(emitter, filter, age);

                var skin = filter.GetComponent<MeshRenderer>();

                if (skin != null && skin.material != null)
                {
                    skin.material.color = new Color(1f, 1f, 1f, face);
                }
            }
        }

        /// <summary>Puts a piece on the window of its texture that the moment calls for.</summary>
        private static void Window(Emitter emitter, MeshFilter filter, float age)
        {
            var stride = emitter.textureFrameStride;

            if (emitter.effectType != 3 || emitter.textureLists == null || stride <= 0
                || emitter.textureLists.Length < stride * 2 || emitter.textureFrameTime <= 0f)
            {
                return;
            }

            var sets = emitter.textureLists.Length / stride;

            // Held for one frame's worth of time and the next reached at the end of it, round and
            // round - which is how the client's CTexFrame works it out.
            var at = age <= 0f ? 0 : Mathf.Max(Mathf.CeilToInt(age / emitter.textureFrameTime) - 1, 0);
            var frame = at % sets;

            var vertices = filter.sharedMesh.vertexCount;

            if (stride < vertices * 2)
            {
                return;
            }

            var uvs = new Vector2[vertices];
            var first = frame * stride;

            for (var v = 0; v < vertices; v++)
            {
                // As written: these coordinates are for a renderer already, and turning them over as
                // well as the mesh would be a second flip on top of the first.
                uvs[v] = new Vector2(
                    emitter.textureLists[first + (v * 2)],
                    emitter.textureLists[first + (v * 2) + 1]);
            }

            filter.sharedMesh.uv = uvs;
        }

        private static Vector3 Way(float[] triple, float factor)
        {
            if (triple == null || triple.Length < 3)
            {
                return Vector3.zero;
            }

            return new Vector3(triple[0] * factor, triple[1] * factor, triple[2] * factor);
        }

        // The client's own shapes, from I_Effect.cpp, laid out exactly as they are there - see the
        // longer note beside the same code in the effect that hangs on a weapon. A Unity quad is not
        // one of them, and building one puts every piece of an effect in the wrong plane.
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
    }
}

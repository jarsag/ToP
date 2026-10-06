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
        /// Its pieces are laid out at once rather than thrown: a blow lands where it lands, and the
        /// client's own skill effects are drawn at the mark rather than flown to it.
        /// </summary>
        public static SkillEffect Play(string name, Vector3 at, float length = 0.5f, Transform parent = null)
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

            effect.Build();

            return effect;
        }

        /// <summary>Builds one piece per shape the effect is made of, all under the effect's own root.</summary>
        private void Build()
        {
            var parts = new List<MeshFilter>();

            foreach (var emitter in _sheet.emitters)
            {
                parts.Add(Shape(emitter));
            }

            _parts = parts.ToArray();
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

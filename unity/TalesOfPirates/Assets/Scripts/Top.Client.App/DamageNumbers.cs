using System.Collections.Generic;
using UnityEngine;

namespace Top.Client.App
{
    /// <summary>
    /// The numbers that fly off a body when it is hit. <br/>
    /// Made of the client's own sheets rather than of a font: a number is a row of one quad per digit,
    /// each quad taking its cell from a sheet that holds the ten of them - ten across, two sizes down -
    /// and the whole row flying up and away from the camera before it fades. That is what the client
    /// does with them, and it is why a number there reads as part of the blow rather than as a label.
    /// </summary>
    public static class DamageNumbers
    {
        /// <summary>What the client's sheets are: ten digits across, two sizes down.</summary>
        private const int Digits = 10;

        private const int Sizes = 2;

        /// <summary>How long a number is in the air, and how far it travels.</summary>
        private const float Life = 1.1f;

        private const float Rise = 2.2f;

        /// <summary>How big one digit is drawn, in world units, and how much of that is the gap.</summary>
        private const float Height = 0.55f;

        private const float Spacing = 0.75f;

        private static readonly List<Number> Flying = new List<Number>();

        private static Transform _root;

        private class Number
        {
            public Transform Root;

            public float Born;

            public Vector3 Drift;

            public Renderer Skin;

            public Material Ink;

            public float Strength;
        }

        /// <summary>
        /// Puts a number over a place, flying away from the camera the way the client sends them:
        /// across the view rather than into it, to one side or the other at random. <br/>
        /// Which side is not a matter of taste - a number sent towards the camera would be read from
        /// behind - so it is sent along the camera's own cross, the direction across the screen.
        /// </summary>
        public static void Show(Vector3 at, int amount, bool critical = false, bool miss = false)
        {
            var text = miss ? "Miss" : Mathf.Abs(amount).ToString();

            Show(at, text, critical);
        }

        /// <summary>The same, for a sheet named rather than for a number.</summary>
        public static void Show(Vector3 at, string text, bool critical = false)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var sheet = Sheet(critical ? "crit" : "damage");

            if (sheet == null)
            {
                return;
            }

            var camera = Camera.main;
            var across = camera != null ? camera.transform.right : Vector3.right;

            if (Random.value < 0.5f)
            {
                across = -across;
            }

            // Up and to one side: the client lifts a number clear of the body it came off, then lets
            // it drift while it fades.
            var drift = (Vector3.up * Rise) + (across * Random.Range(0.6f, 1.4f));

            Keep(new Number
            {
                Root = Row(text, sheet),
                Born = Time.time,
                Drift = drift,
                Strength = 1f,
            }, at);
        }

        /// <summary>Builds the row of quads one number is drawn as, laid out to the left of its place.</summary>
        private static Transform Row(string text, Texture2D sheet)
        {
            var root = new GameObject($"Damage {text}").transform;

            var width = Height * ((sheet.width / (float)Digits) / (sheet.height / (float)Sizes));

            for (var i = 0; i < text.Length; i++)
            {
                // The cell the character has in the sheet, by where it stands in the ten.
                var digit = text[i] - '0';

                if (digit < 0 || digit >= Digits)
                {
                    continue;
                }

                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);

                Object.Destroy(quad.GetComponent<Collider>());

                quad.name = $"digit {text[i]}";
                quad.transform.SetParent(root, worldPositionStays: false);

                // Laid out left to right about the middle of the number, so that a long one does not
                // hang off to one side of the body it came off.
                var centred = (i - ((text.Length - 1) * 0.5f)) * width * Spacing;

                quad.transform.localPosition = new Vector3(centred, 0f, 0f);
                quad.transform.localScale = new Vector3(width, Height, 1f) * Spacing;

                var skin = quad.GetComponent<MeshRenderer>();

                skin.sharedMaterial = Ink(sheet, digit);
            }

            return root;
        }

        /// <summary>
        /// The material one digit is drawn with: the additive shader of the project's own, taking its
        /// shape from the digit's cell in the sheet and its colour from the material.
        /// </summary>
        private static Material Ink(Texture2D sheet, int digit)
        {
            var shader = Shader.Find("Top/Glow");

            if (shader == null)
            {
                shader = Shader.Find("Legacy Shaders/Particles/Additive");
            }

            var ink = new Material(shader);

            ink.SetTexture("_BaseMap", sheet);

            // One cell of ten across and two down. The lower row is the larger size, which is the one
            // a number over a body wants.
            ink.SetTextureScale("_BaseMap", new Vector2(1f / Digits, 1f / Sizes));
            ink.SetTextureOffset("_BaseMap", new Vector2(digit / (float)Digits, 0f));

            ink.SetColor("_GlowColour", Color.white);

            return ink;
        }

        /// <summary>Puts a number in the world and keeps it until its time is up.</summary>
        private static void Keep(Number number, Vector3 at)
        {
            if (_root == null)
            {
                _root = new GameObject("Damage numbers").transform;
            }

            number.Root.SetParent(_root, worldPositionStays: false);
            number.Root.position = at;

            Flying.Add(number);
        }

        /// <summary>
        /// Flies every number that is out, and takes away the ones whose time is up. Called once a
        /// frame by whoever owns the numbers.
        /// </summary>
        public static void Update()
        {
            for (var i = Flying.Count - 1; i >= 0; i--)
            {
                var number = Flying[i];

                if (number.Root == null)
                {
                    Flying.RemoveAt(i);

                    continue;
                }

                var age = Time.time - number.Born;
                var at = age / Life;

                if (at >= 1f)
                {
                    Object.Destroy(number.Root.gameObject);
                    Flying.RemoveAt(i);

                    continue;
                }

                // Away from the camera, easing off as it goes: fast at the blow, drifting at the end.
                number.Root.position += number.Drift * Time.deltaTime * (1f - (at * 0.7f));

                // A number always faces the reader, and shrinks a little as it goes so that it reads
                // as leaving rather than as standing still and fading.
                var camera = Camera.main;

                if (camera != null)
                {
                    number.Root.rotation = camera.transform.rotation;
                }

                number.Root.localScale = Vector3.one * Mathf.Lerp(1f, 0.75f, at);

                // Faded by the ink rather than by the alpha, the shader adding rather than blending.
                var fade = 1f - (at * at);

                foreach (var skin in number.Root.GetComponentsInChildren<MeshRenderer>())
                {
                    skin.material.SetColor("_GlowColour", new Color(1f, 1f, 1f, fade));
                }
            }
        }

        private static Texture2D Sheet(string name)
        {
            return Resources.Load<Texture2D>($"Effect/{name}");
        }

        /// <summary>
        /// Keeps the numbers flying. Nothing draws itself, so something has to call this once a frame -
        /// this is the smallest thing that can, put in the scene by whoever wants numbers at all.
        /// </summary>
        public class Driver : MonoBehaviour
        {
            private void Update()
            {
                DamageNumbers.Update();
            }
        }
    }
}

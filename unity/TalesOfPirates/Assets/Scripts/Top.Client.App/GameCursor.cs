using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using Top.Logging;

namespace Top.Client.App
{
    /// <summary>
    /// Draws the client's own cursors rather than the system one, and switches between them
    /// the way the game plays: a thing in hand, a thing to pick up, a camera being turned, a
    /// hero being walked. The art comes out of the client's animated cursors, which
    /// tools/cursors.ps1 turns into frames and a list of how long each is shown.
    /// <br/>
    /// Nothing has to be put in a scene: it puts itself up when the game starts.
    /// </summary>
    public class GameCursor : MonoBehaviour
    {
        /// <summary>Which cursor is wanted, which is the name of its folder in the art.</summary>
        private enum Wanted
        {
            Normal,
            Drag,
            Pick,
            Camera,
            Land,
            Skill,
        }

        /// <summary>
        /// Until when a skill is being aimed, so that the cursor says so. <br/>
        /// Set from outside rather than worked out here: the cursor knows nothing of skills, and a
        /// skill asking the cursor to say it is aiming is the whole of what the two have to say to
        /// each other. Kept as an instant rather than a flag because a skill is aimed for a moment.
        /// <br/>
        /// It is put back to nought when the cursor comes up, and it has to be: it is a static of the
        /// editor's own domain, which outlives a play session, while the clock it is compared against
        /// starts again from nought. A moment left over from the last session is a moment far in this
        /// one's future, so the cursor came up already saying a skill was being aimed and stayed that
        /// way until the clock caught up with it.
        /// </summary>
        private static float _aiming;

        /// <summary>
        /// Says a skill is being aimed, and for how long. Called again while the key is held, so that
        /// letting go lets the cursor go back one moment later rather than at once.
        /// </summary>
        public static void Aiming(float seconds)
        {
            _aiming = Mathf.Max(_aiming, Time.unscaledTime + Mathf.Max(seconds, 0f));
        }

        private class Frame
        {
            public Texture2D Texture;

            public Vector2 Hotspot;

            public float Seconds;
        }

        /// <summary>What the art's list of frames looks like, as the importer writes it.</summary>
        [Serializable]
        private class Art
        {
            public Step[] steps;
        }

        [Serializable]
        private class Step
        {
            public string frame;

            public int ms;

            public int[] hotspot;
        }

        private Frame[] _frames = new Frame[0];

        private int _next;

        private float _until;

        private Wanted _state = Wanted.Normal;

        private readonly Dictionary<Wanted, Frame[]> _loaded = new Dictionary<Wanted, Frame[]>();

        /// <summary>Puts a cursor up when the game starts, so that nothing has to be wired.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<GameCursor>() == null)
            {
                new GameObject("Game cursor").AddComponent<GameCursor>();
            }
        }

        private void Awake()
        {
            // Nothing is being aimed when the game starts, whatever was being aimed when it last ran.
            _aiming = 0f;

            Show(Wanted.Normal);
        }

        private void Update()
        {
            var wanted = Which();

            if (wanted != _state)
            {
                Show(wanted);
            }

            Play();
        }

        private void OnDisable()
        {
            // Hand the ordinary cursor back to the system when this goes away, or the game
            // would leave a picture of a hand floating over the desktop.
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        /// <summary>Which cursor is wanted, out of what is going on in the game.</summary>
        private static Wanted Which()
        {
            // Aiming comes before the rest: a skill being aimed is the thing the pointer is for, and
            // the hand of a drag or the mark of a walk would say otherwise.
            if (Time.unscaledTime < _aiming)
            {
                return Wanted.Skill;
            }

            if (UiItemDrag.Carrying)
            {
                return Wanted.Drag;
            }

            if (GroundItems.UnderPointer)
            {
                return Wanted.Pick;
            }

            var mouse = Mouse.current;

            if (mouse == null)
            {
                return Wanted.Normal;
            }

            // The right button turns the camera, the left walks the hero: the client told the
            // two apart by the cursor, and so do we.
            if (mouse.rightButton.isPressed)
            {
                return Wanted.Camera;
            }

            if (mouse.leftButton.isPressed)
            {
                return Wanted.Land;
            }

            return Wanted.Normal;
        }

        /// <summary>Takes up a cursor, loading its art the first time it is asked for.</summary>
        private void Show(Wanted state)
        {
            _state = state;

            if (!_loaded.TryGetValue(state, out var frames))
            {
                frames = Load(state);
                _loaded[state] = frames;
            }

            _frames = frames;
            _next = 0;
            _until = 0f;
        }

        /// <summary>
        /// Puts the frames of one cursor up in turn, each for as long as the client showed
        /// it. The cursor is only set when the time comes: asking the platform for a new one
        /// every frame would be a call for nothing.
        /// </summary>
        private void Play()
        {
            if (_frames.Length == 0 || Time.unscaledTime < _until)
            {
                return;
            }

            var frame = _frames[_next];

            Cursor.SetCursor(frame.Texture, frame.Hotspot, CursorMode.ForceSoftware);

            _until = Time.unscaledTime + frame.Seconds;
            _next = (_next + 1) % _frames.Length;
        }

        /// <summary>Reads one cursor's art out of the imported frames and their timings.</summary>
        private static Frame[] Load(Wanted state)
        {
            var name = state.ToString().ToLowerInvariant();
            var frames = new List<Frame>();
            var list = Resources.Load<TextAsset>($"Ui/cursors/{name}/{name}");

            if (list == null)
            {
                Log.Warning($"no cursor art for '{name}': run tools/cursors.ps1");

                return frames.ToArray();
            }

            var art = JsonUtility.FromJson<Art>(list.text);
            var steps = art != null && art.steps != null ? art.steps : new Step[0];

            foreach (var step in steps)
            {
                if (step == null || string.IsNullOrEmpty(step.frame))
                {
                    continue;
                }

                var file = $"Ui/cursors/{name}/{Path.GetFileNameWithoutExtension(step.frame)}";
                var texture = Resources.Load<Texture2D>(file);

                if (texture == null)
                {
                    Log.Warning($"cursor '{name}' names a frame that is not there: {file}");

                    continue;
                }

                frames.Add(new Frame
                {
                    Texture = texture,
                    Hotspot = step.hotspot != null && step.hotspot.Length >= 2
                        ? new Vector2(step.hotspot[0], step.hotspot[1])
                        : Vector2.zero,
                    Seconds = Mathf.Max(0.01f, step.ms / 1000f),
                });
            }

            return frames.ToArray();
        }
    }
}
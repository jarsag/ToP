using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Top.Client.Game.World;
using Top.Logging;

namespace Top.Client.App
{
    /// <summary>
    /// Steps through the clips the hero's rig has, one key at a time, and says which is playing.
    /// A clip is named in the inspector by a number, and a number says nothing: the way to find
    /// out which one is the run with a staff is to watch it. Like the cursor, it puts itself up
    /// when the game starts, so no scene has to name it.
    /// </summary>
    public class ClipPreview : MonoBehaviour
    {
        /// <summary>Whether the keys are listened for at all.</summary>
        [SerializeField] private bool _on = true;

        /// <summary>Whether the points a carried thing can hang on are named on screen.</summary>
        [SerializeField] private bool _mounts;

        private HeroModel _hero;

        private readonly List<string> _clips = new List<string>();

        private int _at;

        /// <summary>Puts the preview up when the game starts, so that nothing has to be wired.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<ClipPreview>() == null)
            {
                new GameObject("Clip preview").AddComponent<ClipPreview>();
            }
        }

        private void Update()
        {
            if (!_on)
            {
                return;
            }

            var keyboard = Keyboard.current;

            if (keyboard == null || keyboard.f1Key == null || keyboard.f2Key == null)
            {
                return;
            }

            if (_hero == null)
            {
                _hero = FindAnyObjectByType<HeroModel>();

                if (_hero == null)
                {
                    return;
                }
            }

            if (_clips.Count == 0)
            {
                Gather();
            }

            if (keyboard.f4Key != null && keyboard.f4Key.wasPressedThisFrame)
            {
                _mounts = !_mounts;

                Log.Info($"clip preview: the mounts are {(_mounts ? "shown" : "hidden")}");
            }

            if (keyboard.f3Key != null && keyboard.f3Key.wasPressedThisFrame)
            {
                _hero.LetGo();

                Log.Info("clip preview: let go, the hero animates himself again");
            }

            if (keyboard.f2Key.wasPressedThisFrame)
            {
                Step(1);
            }
            else if (keyboard.f1Key.wasPressedThisFrame)
            {
                Step(-1);
            }
        }

        /// <summary>
        /// Names every point the hero's rig keeps for hanging something on, over the point itself,
        /// so that it is plain which dummy is which and where it is. They are drawn as a label at
        /// the point rather than as a gizmo, because a gizmo belongs to the scene view and this is
        /// for looking at the hero while the game runs, free camera included.
        /// </summary>
        private void OnGUI()
        {
            if (!_mounts || _hero == null)
            {
                return;
            }

            var camera = Camera.main;

            if (camera == null)
            {
                return;
            }

            var here = Event.current.mousePosition;

            foreach (var child in _hero.GetComponentsInChildren<Transform>(true))
            {
                if (!child.name.StartsWith("dummy_", StringComparison.Ordinal))
                {
                    continue;
                }

                // A dressed hero carries a copy of the skeleton in every part he wears, and only the
                // live copies are animated: a point drawn from a copy that is switched off would stand
                // still while the hero moves, which is no use for lining a weapon up against him.
                if (!child.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var at = camera.WorldToScreenPoint(child.position);

                if (at.z <= 0f)
                {
                    continue;
                }

                var where = new Rect(at.x - 7f, Screen.height - at.y - 7f, 14f, 14f);

                // A point is a small square to press, and its name shows only while the pointer is
                // over it: the names of twenty-five points over the hero at once would hide him.
                GUI.color = Color.yellow;

                if (GUI.Button(where, GUIContent.none))
                {
                    Point(child.name);
                }

                if (where.Contains(here))
                {
                    GUI.Label(new Rect(where.x + 18f, where.y - 2f, 220f, 18f), child.name);
                }

                GUI.color = Color.white;
            }
        }



        /// <summary>
        /// Points one of the hero's mounts at a node: the one on his back while he is in a safe zone
        /// and the one in his hand while he is not, and the right hand unless shift is held - a hero
        /// has two hands and the pointer is one.
        /// </summary>
        private void Point(string name)
        {
            if (_hero == null)
            {
                return;
            }

            var safe = Zone.IsSafe(_hero.transform.position);
            var left = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;

            _hero.HangOn(name, left, safe);

            Log.Info($"carried thing {(safe ? "on the back" : "in the hand")}, {(left ? "left" : "right")}: {name}");
        }
        /// <summary>The clips the loaded rig actually has, gathered once.</summary>
        private void Gather()
        {
            _clips.Clear();

            foreach (var animation in _hero.GetComponentsInChildren<Animation>(true))
            {
                foreach (AnimationState state in animation)
                {
                    if (!_clips.Contains(state.name))
                    {
                        _clips.Add(state.name);
                    }
                }
            }

            _clips.Sort(StringComparer.Ordinal);

            Log.Info($"clip preview: the hero has {_clips.Count} clip(s); F1 and F2 step through them");
        }

        /// <summary>Plays the clip next to the one playing, and says its name.</summary>
        private void Step(int by)
        {
            if (_clips.Count == 0)
            {
                return;
            }

            _at = (_at + by + _clips.Count) % _clips.Count;

            var name = _clips[_at];

            // Held, so that the hero does not put his own clip back the moment the state changes:
            // stepping through the animations is no use if each one is replaced as it is shown.
            _hero.Holding = name;

            foreach (var animation in _hero.GetComponentsInChildren<Animation>(true))
            {
                animation.Stop();
                animation.Play(name);
            }

            Log.Info($"clip preview {_at + 1}/{_clips.Count}: {name}");
        }
    }
}
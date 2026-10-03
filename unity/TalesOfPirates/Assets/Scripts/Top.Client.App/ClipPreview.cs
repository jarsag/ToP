using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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
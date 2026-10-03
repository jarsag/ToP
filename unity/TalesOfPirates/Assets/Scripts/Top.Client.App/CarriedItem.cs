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
        /// <summary>Which hand it belongs to: five right, six left.</summary>
        private int _slot = 5;

        private HeroModel _hero;

        /// <summary>Where it hangs now, for anything that wants to hang beside it - an effect, say.</summary>
        public Transform Where => _mount;

        private string _name;

        private Transform _mount;

        private bool _safe;

        private bool _hung;

        /// <summary>Tells it where it belongs, which is what the hero knows and it does not.</summary>
        public void Belong(HeroModel hero, int slot)
        {
            _hero = hero;
            _slot = slot;
            _hung = false;
        }

        private void LateUpdate()
        {
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
            // inspector while the game runs, and a weapon that ignored a change until the next
            // time it was put on would be a weapon nobody could line up.
            Dress(safe);
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
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

        private Vector3 _rotation;

        private float _scale = 1f;

        private Transform _mount;

        private bool _safe;

        private bool _hung;

        /// <summary>Tells it where it belongs, which is what the hero knows and it does not.</summary>
        public void Belong(int slot, Vector3 rotation, float scale)
        {
            _slot = slot;
            _rotation = rotation;
            _scale = scale;
            _hung = false;
        }

        private void LateUpdate()
        {
            var safe = Zone.IsSafe(transform.root.position);

            if (_hung && safe == _safe && _mount != null)
            {
                return;
            }

            _hung = true;
            _safe = safe;

            Hang(safe);
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
            transform.localRotation = Quaternion.Euler(_rotation);
            transform.localScale = Vector3.one * _scale;

            if (_mount != mount)
            {
                _mount = mount;

                Log.Info($"a thing carried in slot {_slot} hangs on '{mount.name}' (in a safe zone: {safe})");
            }
        }

        /// <summary>Which dummy it hangs on, out of the hand it belongs to and the state.</summary>
        private string Mount(bool safe)
        {
            if (_slot == 6)
            {
                return safe ? "dummy_21" : "dummy_6";
            }

            return safe ? "dummy_2" : "dummy_9";
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
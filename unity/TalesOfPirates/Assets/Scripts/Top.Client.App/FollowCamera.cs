using UnityEngine;
using UnityEngine.InputSystem;

namespace Top.Client.App
{
    /// <summary>
    /// Keeps the game camera behind and above a target, looking at it. The right
    /// mouse button orbits the offset, which is what makes camera relative
    /// walking worth having - the hero always goes where the player is looking.
    /// </summary>
    public class FollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _offset = new Vector3(0f, 16f, -20f);
        [SerializeField] private float _smoothing = 10f;
        [SerializeField] private float _orbitSpeed = 0.25f;

        private float _yaw;
        private bool _orbiting;

        private void LateUpdate()
        {
            if (_target == null)
            {
                return;
            }

            Orbit();

            var desired = _target.position + (Quaternion.Euler(0f, _yaw, 0f) * _offset);

            // Framerate independent smoothing. Lerping by a raw delta time is
            // not, and a WebGL build rarely holds a steady framerate.
            var t = 1f - Mathf.Exp(-_smoothing * Time.deltaTime);

            transform.position = Vector3.Lerp(transform.position, desired, t);

            var look = _target.position - transform.position;

            if (look.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(look);
            }
        }

        private void Orbit()
        {
            var mouse = Mouse.current;

            if (mouse == null || !mouse.rightButton.isPressed)
            {
                _orbiting = false;

                return;
            }

            // The frame a press starts can carry a wild delta, because the
            // editor warps the cursor when play mode begins or focus moves.
            // Note the press and throw that one delta away, or the camera
            // snaps to a random angle the moment you start playing.
            if (!_orbiting)
            {
                _orbiting = true;

                return;
            }

            _yaw += mouse.delta.ReadValue().x * _orbitSpeed;
        }
    }
}

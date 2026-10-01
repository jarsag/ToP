using UnityEngine;
using UnityEngine.InputSystem;

namespace Top.Client.App
{
    /// <summary>
    /// Flies the game camera over the streamed map: hold the right mouse
    /// button to look, WASD to move along the view, Q and E for height,
    /// shift to sprint and the wheel to change speed. The project has its
    /// active input handling set to the Input System package, so the legacy
    /// Input class would throw here - this reads Keyboard.current and
    /// Mouse.current instead.
    /// </summary>
    public class FlyCamera : MonoBehaviour
    {
        [SerializeField] private float _speed = 200f;
        [SerializeField] private float _sprint = 5f;
        [SerializeField] private float _lookSensitivity = 0.12f;
        [SerializeField] private float _wheelStep = 1.25f;
        [SerializeField] private float _minSpeed = 1f;
        [SerializeField] private float _maxSpeed = 5000f;
        [SerializeField] private float _minPitch = -89f;
        [SerializeField] private float _maxPitch = 89f;

        private float _yaw;
        private float _pitch;

        private void OnEnable()
        {
            var angles = transform.eulerAngles;

            // eulerAngles reports 0-360, so a camera tipped up comes back as
            // e.g. 350 and would snap the wrong way on the first look.
            _yaw = angles.y;
            _pitch = angles.x > 180f ? angles.x - 360f : angles.x;
        }

        private void OnDisable()
        {
            Unlock();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            if (keyboard == null)
            {
                return;
            }

            if (mouse != null && mouse.rightButton.isPressed)
            {
                Look(mouse);
                Lock();
            }
            else
            {
                Unlock();
            }

            Move(keyboard);
        }

        private void Look(Mouse mouse)
        {
            var delta = mouse.delta.ReadValue();

            _yaw += delta.x * _lookSensitivity;
            _pitch = Mathf.Clamp(_pitch - (delta.y * _lookSensitivity), _minPitch, _maxPitch);

            var scroll = mouse.scroll.ReadValue().y;

            if (scroll != 0f)
            {
                _speed = Mathf.Clamp(_speed * (scroll > 0f ? _wheelStep : 1f / _wheelStep),
                    _minSpeed, _maxSpeed);
            }

            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void Move(Keyboard keyboard)
        {
            var direction = Vector3.zero;

            if (keyboard.wKey.isPressed)
            {
                direction += transform.forward;
            }

            if (keyboard.sKey.isPressed)
            {
                direction -= transform.forward;
            }

            if (keyboard.dKey.isPressed)
            {
                direction += transform.right;
            }

            if (keyboard.aKey.isPressed)
            {
                direction -= transform.right;
            }

            if (keyboard.eKey.isPressed)
            {
                direction += Vector3.up;
            }

            if (keyboard.qKey.isPressed)
            {
                direction -= Vector3.up;
            }

            if (direction == Vector3.zero)
            {
                return;
            }

            var speed = keyboard.leftShiftKey.isPressed ? _speed * _sprint : _speed;

            transform.position += direction.normalized * (speed * Time.deltaTime);
        }

        /// <summary>
        /// Hides the cursor while looking, so the mouse can run past the edge
        /// of the game view without the look stopping there. Letting go of
        /// the right button gives it back.
        /// </summary>
        private static void Lock()
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                return;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static void Unlock()
        {
            if (Cursor.lockState == CursorLockMode.None)
            {
                return;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}

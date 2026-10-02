using UnityEngine;
using UnityEngine.InputSystem;

namespace Top.Client.App
{
    /// <summary>
    /// The player's camera: it sits on a sphere around its target, looking down
    /// at an angle the player turns with the right mouse button and pulls in and
    /// out with the wheel.
    /// <br/>
    /// Walking by clicking needs a camera of its own, because where a click lands
    /// depends on where the camera looks from - the same pixel is a different
    /// place on the map from a different angle.
    /// </summary>
    public class IsometricCamera : MonoBehaviour
    {
        [SerializeField] private Transform _target;

        [SerializeField] private float _distance = 12f;
        [SerializeField] private float _heightAngle = 35f;
        [SerializeField] private float _minDistance = 5f;
        [SerializeField] private float _maxDistance = 20f;

        [SerializeField] private float _rotateSpeed = 5f;
        [SerializeField] private float _minVerticalAngle = 5f;
        [SerializeField] private float _maxVerticalAngle = 85f;

        [SerializeField] private float _followSpeed = 5f;
        [SerializeField] private bool _smoothFollow = true;

        private float _horizontalAngle;
        private float _verticalAngle;

        /// <summary>
        /// The object the camera stays on, so whatever switches camera modes can
        /// hand the hero over and take it back.
        /// </summary>
        public Transform Target
        {
            get { return _target; }
            set { _target = value; }
        }

        /// <summary>
        /// Starts from where the camera already stands, rather than snapping to
        /// the angle in the fields: the scene's own angle is usually the one
        /// somebody framed the map in.
        /// </summary>
        private void OnEnable()
        {
            _verticalAngle = _heightAngle;

            if (_target == null)
            {
                return;
            }

            var offset = transform.position - _target.position;

            if (offset.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            _horizontalAngle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            _verticalAngle = Mathf.Asin(offset.y / offset.magnitude) * Mathf.Rad2Deg;
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                return;
            }

            Look();

            var vertical = _verticalAngle * Mathf.Deg2Rad;
            var around = _horizontalAngle * Mathf.Deg2Rad;

            var position = _target.position;

            position.y += Mathf.Sin(vertical) * _distance;
            position.x += Mathf.Cos(vertical) * Mathf.Sin(around) * _distance;
            position.z += Mathf.Cos(vertical) * Mathf.Cos(around) * _distance;

            transform.position = _smoothFollow
                ? Vector3.Lerp(transform.position, position, _followSpeed * Time.deltaTime)
                : position;

            transform.LookAt(_target.position);
        }

        private void Look()
        {
            var mouse = Mouse.current;

            if (mouse == null)
            {
                return;
            }

            if (mouse.rightButton.isPressed)
            {
                var delta = mouse.delta.ReadValue();

                _horizontalAngle += delta.x * _rotateSpeed * 0.1f;
                _verticalAngle = Mathf.Clamp(_verticalAngle - (delta.y * _rotateSpeed * 0.1f),
                    _minVerticalAngle, _maxVerticalAngle);
            }

            var scroll = mouse.scroll.ReadValue().y;

            if (scroll != 0f)
            {
                _distance = Mathf.Clamp(_distance - (scroll * 0.5f), _minDistance, _maxDistance);
            }
        }
    }
}

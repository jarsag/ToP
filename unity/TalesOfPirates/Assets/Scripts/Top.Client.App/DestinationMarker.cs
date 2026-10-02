using UnityEngine;

namespace Top.Client.App
{
    /// <summary>
    /// The mark left where the player clicked: it turns slowly and bobs over the
    /// ground, so a point on a height field reads as a place rather than as a
    /// coordinate.
    /// <br/>
    /// Where it stands is the point on the ground and nothing else - whoever
    /// spawns it puts it exactly at the hit, and the hover is measured from there
    /// every frame, so the gap cannot drift however long the mark lives.
    /// </summary>
    public class DestinationMarker : MonoBehaviour
    {
        [SerializeField] private float _spinSpeed = 90f;

        [SerializeField] private float _hoverHeight = 0.35f;
        [SerializeField] private float _bobAmplitude = 0.12f;
        [SerializeField] private float _bobSpeed = 2f;

        /// <summary>How long the mark lives. Zero leaves it standing until the hero arrives.</summary>
        [SerializeField] private float _lifetime;

        private Vector3 _ground;
        private float _time;

        /// <summary>How far above the ground it is drawn, for whoever builds the mark rather than wires it.</summary>
        public float HoverHeight
        {
            get { return _hoverHeight; }
            set { _hoverHeight = value; }
        }

        /// <summary>How far it drifts up and down around that, for whoever builds the mark.</summary>
        public float BobAmplitude
        {
            get { return _bobAmplitude; }
            set { _bobAmplitude = value; }
        }

        /// <summary>Degrees a second it turns through, for whoever builds the mark.</summary>
        public float SpinSpeed
        {
            get { return _spinSpeed; }
            set { _spinSpeed = value; }
        }

        private void Start()
        {
            _ground = transform.position;

            if (_lifetime > 0f)
            {
                Destroy(gameObject, _lifetime);
            }
        }

        private void Update()
        {
            _time += Time.deltaTime;

            transform.rotation = Quaternion.Euler(0f, _time * _spinSpeed, 0f);

            var height = _ground.y + _hoverHeight + (Mathf.Sin(_time * _bobSpeed) * _bobAmplitude);

            transform.position = new Vector3(_ground.x, height, _ground.z);
        }
    }
}

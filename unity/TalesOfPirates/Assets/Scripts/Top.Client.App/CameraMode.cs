using UnityEngine;
using UnityEngine.InputSystem;

namespace Top.Client.App
{
    /// <summary>
    /// Switches the running game between watching the hero and flying free over
    /// the map. Watching is the isometric camera when the scene has one and the
    /// follow camera when it does not; exactly one camera component is ever on.
    /// <br/>
    /// Free flight is only worth having if the world streams around the camera
    /// rather than around the hero: a camera flown two hundred units away would
    /// otherwise stare at an empty scene, because the preview only builds chunks
    /// around its focus. So while flying, the preview follows the camera and the
    /// hero is left standing where it was - and its controller is switched off
    /// with it, so a click cannot order it about from up there.
    /// </summary>
    public class CameraMode : MonoBehaviour
    {
        [SerializeField] private IsometricCamera _iso;
        [SerializeField] private FollowCamera _follow;
        [SerializeField] private FlyCamera _fly;
        [SerializeField] private HeroController _hero;
        [SerializeField] private MapPreview _preview;

        /// <summary>The key that switches modes.</summary>
        [SerializeField] private Key _toggle = Key.Tab;

        /// <summary>Whether play starts flying free.</summary>
        [SerializeField] private bool _free;

        /// <summary>Whether the controls are shown over the game.</summary>
        [SerializeField] private bool _hint = true;

        private void Awake()
        {
            // Everything below is on this camera or in this scene, so the
            // component works when it is dropped on the camera and nothing else is
            // wired - the fields are there to override a search that is already
            // right.
            _iso = _iso != null ? _iso : GetComponent<IsometricCamera>();
            _follow = _follow != null ? _follow : GetComponent<FollowCamera>();
            _fly = _fly != null ? _fly : GetComponent<FlyCamera>();
            _preview = _preview != null ? _preview : FindAnyObjectByType<MapPreview>();

            var hero = Hero();

            _hero = _hero != null ? _hero : (hero == null ? null : hero.GetComponent<HeroController>());
        }

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return;
            }

            if (keyboard[_toggle].wasPressedThisFrame)
            {
                _free = !_free;

                Apply();
            }
        }

        private void OnGUI()
        {
            if (!_hint)
            {
                return;
            }

            GUI.Label(new Rect(12f, 12f, 1600f, 24f), _free
                ? $"{_toggle}: watch the hero    |    flying: WASD forward, Q and E down and up, " +
                  "hold the right mouse button to look, wheel changes speed, shift sprints"
                : $"{_toggle}: fly free over the map    |    walking: hold the left mouse button to steer, " +
                  "let go to leave a mark    |    right mouse turns the camera, wheel zooms");
        }

        /// <summary>
        /// Puts the camera in the mode that was asked for: one camera component
        /// on, the hero's own controls on with it, and the world streaming around
        /// whoever is being looked through.
        /// </summary>
        private void Apply()
        {
            var hero = Hero();

            if (_iso != null)
            {
                if (_iso.Target == null)
                {
                    _iso.Target = hero;
                }

                _iso.enabled = !_free;
            }

            if (_follow != null)
            {
                // Only one of the two watches: the isometric camera is the one
                // built for pointing at the map, so it wins when both are there.
                _follow.enabled = !_free && _iso == null;
            }

            if (_fly != null)
            {
                _fly.enabled = _free;
            }

            if (_hero != null)
            {
                _hero.enabled = !_free;
            }

            if (_preview != null)
            {
                _preview.Focus = _free ? transform : hero;
            }
        }

        /// <summary>
        /// The hero, wherever it was wired from: its controller says it directly,
        /// and the cameras that watch it know the transform.
        /// </summary>
        private Transform Hero()
        {
            if (_hero != null)
            {
                return _hero.transform;
            }

            if (_iso != null && _iso.Target != null)
            {
                return _iso.Target;
            }

            return _follow != null ? _follow.Target : null;
        }
    }
}

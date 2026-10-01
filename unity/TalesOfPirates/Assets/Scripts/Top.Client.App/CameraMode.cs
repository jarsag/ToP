using UnityEngine;
using UnityEngine.InputSystem;

namespace Top.Client.App
{
    /// <summary>
    /// Switches the game camera between walking with the hero and flying free
    /// over the map, from inside the running game. The two are separate
    /// components on one camera - the follow camera and the fly camera - so
    /// exactly one of them is on at a time.
    /// <br/>
    /// Free flight is only worth having if the world streams around the camera
    /// rather than around the hero: a camera flown two hundred units away would
    /// otherwise stare at an empty scene, because the preview only builds chunks
    /// around its focus. So while flying, the preview follows the camera and the
    /// hero is left standing where it was; coming back hands the camera to the
    /// follow camera, which eases it in behind the hero again.
    /// </summary>
    public class CameraMode : MonoBehaviour
    {
        [SerializeField] private FollowCamera _follow;
        [SerializeField] private FlyCamera _fly;
        [SerializeField] private HeroController _hero;
        [SerializeField] private MapPreview _preview;

        /// <summary>The key that switches modes.</summary>
        [SerializeField] private Key _toggle = Key.Tab;

        /// <summary>Whether play starts flying free.</summary>
        [SerializeField] private bool _free;

        /// <summary>Whether the keys are shown over the game.</summary>
        [SerializeField] private bool _hint = true;

        private void Awake()
        {
            // Everything below is on this camera or in this scene, so the
            // component works when it is dropped on the camera and nothing else
            // is wired - the fields are there to override a search that is
            // already right.
            _follow = _follow != null ? _follow : GetComponent<FollowCamera>();
            _fly = _fly != null ? _fly : GetComponent<FlyCamera>();
            _preview = _preview != null ? _preview : FindAnyObjectByType<MapPreview>();

            var target = _follow != null ? _follow.Target : null;

            _hero = _hero != null ? _hero : (target == null ? null : target.GetComponent<HeroController>());
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

            GUI.Label(new Rect(12f, 12f, 1400f, 24f), _free
                ? $"{_toggle}: walk with the hero    |    flying: WASD forward, Q and E down and up, " +
                  "hold the right mouse button to look, wheel changes speed, shift sprints"
                : $"{_toggle}: fly free over the map    |    walking: WASD, the camera stays behind the hero");
        }

        /// <summary>
        /// Puts the camera in the mode that was asked for: one of the two
        /// components on, the hero standing still while somebody flies, and the
        /// world streaming around whoever is being looked through.
        /// </summary>
        private void Apply()
        {
            if (_follow != null)
            {
                _follow.enabled = !_free;
            }

            if (_fly != null)
            {
                _fly.enabled = _free;
            }

            if (_hero != null)
            {
                _hero.enabled = !_free;
            }

            if (_preview == null)
            {
                return;
            }

            _preview.Focus = _free ? transform : (_follow != null ? _follow.Target : null);
        }
    }
}

using Top.Client.Game.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Top.Client.App
{
    /// <summary>
    /// Drives a placeholder hero across the map preview: WASD walks relative to
    /// where the camera looks, the map's height field keeps the hero on the
    /// ground and its blocked cells keep the hero out of walls. Where the scene
    /// puts this object is where it gets dropped on the map, and it only starts
    /// walking once the preview has finished loading one.
    /// </summary>
    public class HeroController : MonoBehaviour
    {
        [SerializeField] private MapPreview _preview;
        [SerializeField] private float _speed = 14f;

        /// <summary>
        /// How far the hero's body reaches across the map, in map units, for the
        /// blocked check. The hero is a sphere of radius one half, so a little
        /// more than that keeps it clear of walls.
        /// </summary>
        [SerializeField] private float _bodyRadius = 0.6f;

        /// <summary>
        /// Lifts the sphere's centre off the ground by its own radius, so it
        /// rests on the surface instead of sinking half way in.
        /// </summary>
        [SerializeField] private float _groundOffset = 0.5f;

        private MapWalker _walker;

        private void Update()
        {
            var walker = Walker();

            if (walker == null)
            {
                return;
            }

            var camera = Camera.main;
            var move = camera == null ? Vector2.zero : ReadMove(camera.transform);

            if (move != Vector2.zero)
            {
                walker.Step(move * (_speed * Time.deltaTime));
            }

            // The map owns the height, so the hero is placed at ground level
            // every frame rather than falling onto it.
            transform.position = walker.WorldPosition + (Vector3.up * _groundOffset);
        }

        /// <summary>
        /// The walker, built once the preview has a map. Until then there is
        /// nothing to stand on and the hero stays where the scene put it.
        /// </summary>
        private MapWalker Walker()
        {
            if (_walker != null)
            {
                return _walker;
            }

            var map = _preview != null ? _preview.Data : null;

            if (map == null)
            {
                return null;
            }

            _walker = new MapWalker(map, MapSpace.ToMap(transform.position), _bodyRadius);

            return _walker;
        }

        /// <summary>
        /// Turns WASD into a map space delta. The camera decides what "forward"
        /// means so walking always goes where the player is looking, and the
        /// delta crosses into map space through the same conversion the world
        /// uses - map X runs opposite to world X.
        /// </summary>
        private static Vector2 ReadMove(Transform camera)
        {
            var keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return Vector2.zero;
            }

            var forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(camera.right, Vector3.up).normalized;
            var world = Vector3.zero;

            if (keyboard.wKey.isPressed)
            {
                world += forward;
            }

            if (keyboard.sKey.isPressed)
            {
                world -= forward;
            }

            if (keyboard.dKey.isPressed)
            {
                world += right;
            }

            if (keyboard.aKey.isPressed)
            {
                world -= right;
            }

            return world == Vector3.zero ? Vector2.zero : MapSpace.ToMap(world.normalized);
        }
    }
}

using System;
using Top.Client.Game.World;
using Top.Logging;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Top.Client.App
{
    /// <summary>
    /// Walks the hero across the map preview. The player points with the mouse -
    /// holding the button steers, letting it go leaves a mark where the hero is
    /// going - and the hero walks there along the map's own height field, kept out
    /// of walls by its blocked cells and never falling, because its ground is a
    /// number rather than a collider.
    /// <br/>
    /// The keyboard belongs to the free camera; the hero has none, so a click and
    /// a key can never disagree about where the body should be.
    /// </summary>
    public class HeroController : MonoBehaviour
    {
        [SerializeField] private MapPreview _preview;
        [SerializeField] private float _speed = 14f;

        /// <summary>
        /// The mark left at the destination. A prefab of the player's own is used
        /// when one is assigned, with all of its own settings; otherwise a mark is
        /// put together here out of the fields below, so pointing at the map shows
        /// something before anybody has built an asset for it.
        /// </summary>
        [SerializeField] private DestinationMarker _marker;

        /// <summary>What that made-here mark is: the model, and how it sits on the ground.</summary>
        [SerializeField] private MarkSettings _mark = new MarkSettings();

        /// <summary>
        /// How far the hero's body reaches across the map, in map units, for the
        /// blocked check. A little more than the model's own width keeps it clear
        /// of walls.
        /// </summary>
        [SerializeField] private float _bodyRadius = 0.6f;

        /// <summary>
        /// Lifts the body off the ground, so a placeholder sphere rests on the
        /// surface instead of sinking half way in. A model with feet of its own is
        /// brought back down by the same amount.
        /// </summary>
        [SerializeField] private float _groundOffset = 0.5f;

        /// <summary>How close the hero has to get before the order counts as walked.</summary>
        [SerializeField] private float _stopDistance = 0.4f;

        [SerializeField] private float _turnSpeed = 10f;

        /// <summary>How far a click goes looking for ground, in world units.</summary>
        [SerializeField] private float _reach = 2000f;

        /// <summary>
        /// Whether the hero starts on a spawn point marked on the map rather than
        /// where the scene left him. A scene with no spawn points marked keeps him
        /// where he stands either way.
        /// </summary>
        [SerializeField] private bool _spawnOnZone = true;

        private MapWalker _walker;
        private DestinationMarker _placed;
        private Vector2? _destination;

        /// <summary>
        /// How fast the hero is moving across the map, in map units a second.
        /// Whatever animates the hero reads this rather than measuring the
        /// transform itself: standing still is zero, and a wall that stopped the
        /// step is zero too, so a body leaves off running when it stops.
        /// </summary>
        public float Speed { get; private set; }

        /// <summary>
        /// How far the hero's own body is lifted off the ground. A model standing
        /// on its own feet is brought back down by this much, or it would float.
        /// </summary>
        public float GroundOffset => _groundOffset;

        private void Update()
        {
            var walker = Walker();

            if (walker == null)
            {
                return;
            }

            Aim();

            Speed = Walk(walker);

            // The map owns the height, so the hero is placed at ground level
            // every frame rather than falling onto it.
            transform.position = walker.WorldPosition + (Vector3.up * _groundOffset);
        }

        private void OnDisable()
        {
            // Flying free, or switching the controller off some other way, leaves
            // no standing order behind: the hero stays where it was.
            Arrive();
        }

        /// <summary>
        /// Reads the order off the mouse: the ground under the cursor while the
        /// button is held, so dragging steers the hero about, and the same point
        /// once more when the button comes up, which is when the mark is left.
        /// </summary>
        private void Aim()
        {
            var mouse = Mouse.current;
            var camera = Camera.main;
            var map = _preview != null ? _preview.Data : null;

            if (mouse == null || camera == null || map == null)
            {
                return;
            }

            var released = mouse.leftButton.wasReleasedThisFrame;

            if (!mouse.leftButton.isPressed && !released)
            {
                return;
            }

            // A click belongs to the hero only when it is not on the interface: the
            // inventory is drawn over the map, and pointing at a slot is not pointing
            // at the ground under it.
            if (EventSystem.current != null && (EventSystem.current.IsPointerOverGameObject() || UiItemDrag.Carrying))
            {
                return;
            }

            if (!TryGround(camera, map, mouse.position.ReadValue(), out var point))
            {
                return;
            }

            _destination = point;

            if (released)
            {
                Mark(point, map);
            }
        }

        private bool TryGround(Camera camera, MapData map, Vector2 screen, out Vector2 point)
        {
            point = default;

            if (!MapRay.TryHit(map, camera.ScreenPointToRay(screen), _reach, out var hit))
            {
                return false;
            }

            point = MapSpace.ToMap(hit);

            return true;
        }

        /// <summary>
        /// One frame of walking: toward the order if there is one, and nothing at
        /// all otherwise.
        /// </summary>
        private float Walk(MapWalker walker)
        {
            if (_destination == null)
            {
                return 0f;
            }

            var to = _destination.Value - walker.Position;
            var distance = to.magnitude;

            if (distance <= _stopDistance)
            {
                Arrive();

                return 0f;
            }

            var direction = to / distance;
            var step = direction * Mathf.Min(_speed * Time.deltaTime, distance);

            if (!walker.Step(step))
            {
                // Nothing moved at all, so the hero is against something it cannot
                // slide along; holding the order would only look stuck.
                Arrive();

                return 0f;
            }

            Turn(direction);

            return _speed;
        }

        /// <summary>
        /// Faces the way the hero is walking: the body carries the model, so the
        /// body is what turns.
        /// </summary>
        private void Turn(Vector2 direction)
        {
            var world = MapSpace.ToWorld(direction.x, direction.y, 0f);
            var forward = new Vector3(world.x, 0f, world.z);

            if (forward.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(forward),
                1f - Mathf.Exp(-_turnSpeed * Time.deltaTime));
        }

        /// <summary>
        /// Leaves the mark on the ground the order points at. Its height comes
        /// from the map's own field, so the mark sits on the surface whether or
        /// not a chunk has been built there yet.
        /// </summary>
        private void Mark(Vector2 point, MapData map)
        {
            ClearMark();

            var world = MapSpace.ToWorld(point.x, point.y, map.HeightAt(point.x, point.y));

            _placed = _marker != null ? Instantiate(_marker, world, Quaternion.identity) : Made(world);
        }

        /// <summary>
        /// A mark made here rather than by the player: the converted model when
        /// one is named, and a flat disc otherwise - the disc being what the mark
        /// falls back to when no model is there to draw.
        /// </summary>
        private DestinationMarker Made(Vector3 world)
        {
            var root = new GameObject("Destination");

            root.transform.SetPositionAndRotation(world, Quaternion.identity);

            if (string.IsNullOrEmpty(_mark.Model) || _mark.Disc)
            {
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);

                disc.name = "Disc";
                disc.transform.SetParent(root.transform, worldPositionStays: false);
                disc.transform.localScale = new Vector3(_mark.DiscSize, 0.02f, _mark.DiscSize);

                Destroy(disc.GetComponent<Collider>());
            }

            if (!string.IsNullOrEmpty(_mark.Model))
            {
                var model = new GameObject("Mark");

                model.transform.SetParent(root.transform, worldPositionStays: false);

                var content = model.AddComponent<ContentModel>();

                content.Path = _mark.Model;
                content.Rotation = _mark.Rotation;
                content.Scale = _mark.Scale;
            }

            var marker = root.AddComponent<DestinationMarker>();

            // The mark stands on the ground the click found, so this is the only
            // thing that ever raises it off the terrain.
            marker.HoverHeight = _mark.Hover;
            marker.BobAmplitude = _mark.Bob;
            marker.SpinSpeed = _mark.Spin;

            return marker;
        }

        /// <summary>
        /// Drops the order and the mark with it: a hero standing still with a mark
        /// under it reads as a hero about to move.
        /// </summary>
        private void Arrive()
        {
            _destination = null;

            ClearMark();
        }

        private void ClearMark()
        {
            if (_placed == null)
            {
                return;
            }

            Destroy(_placed.gameObject);

            _placed = null;
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

            // A marked spawn point is where the hero starts, and a map with none
            // leaves him exactly where the scene put him.
            Log.Info($"the scene has {Zone.Summary()}");

            if (_spawnOnZone && Zone.TryRandomSpawn(out var spawn))
            {
                var point = MapSpace.ToMap(spawn);

                _walker.PlaceAt(point);

                Log.Info($"the hero starts on a spawn point at map ({point.x:0.0}, {point.y:0.0})");
            }

            return _walker;
        }

        /// <summary>
        /// How the mark left by a click is drawn, for a scene that has no prefab
        /// of its own for one. The hover is a hair rather than a flourish: the
        /// mark is put down on the ground the click found - the same height the
        /// terrain mesh is built from - and only needs enough clearance to stay
        /// out of it.
        /// </summary>
        [Serializable]
        public class MarkSettings
        {
            /// <summary>Model out of the converted tree; empty draws the disc alone.</summary>
            public string Model;

            /// <summary>Which way the model is turned, for a symbol that has to be laid flat.</summary>
            public Vector3 Rotation;

            public float Scale = 1f;

            /// <summary>How far above the ground the mark is drawn, in map units.</summary>
            public float Hover = 0.02f;

            /// <summary>How far it drifts up and down around that, in map units.</summary>
            public float Bob;

            /// <summary>Degrees a second the mark turns through.</summary>
            public float Spin = 90f;

            /// <summary>Whether a flat disc is drawn as well, under the model.</summary>
            public bool Disc;

            /// <summary>How wide that disc is, in map units.</summary>
            public float DiscSize = 1.2f;
        }
    }
}

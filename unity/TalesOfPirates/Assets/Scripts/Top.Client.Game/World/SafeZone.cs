using UnityEngine;

namespace Top.Client.Game.World
{
    /// <summary>
    /// Ground where the hero is out of danger: a rectangle of the map, in world
    /// units, standing where the object that carries it stands. Everything the
    /// marked zones do not cover is war, which is what the hero's animations
    /// answer to.
    /// <br/>
    /// The rectangle is drawn in the editor and nowhere else: at runtime the world
    /// looks the same on both sides of the line, and the only thing that says
    /// which side the hero is on is the clip he is playing.
    /// </summary>
    public class SafeZone : MonoBehaviour
    {
        [SerializeField] private Vector2 _size = new Vector2(64f, 64f);

        private static SafeZone[] _zones;

        /// <summary>How wide and how deep the zone is, in map units.</summary>
        public Vector2 Size
        {
            get { return _size; }
            set { _size = value; }
        }

        /// <summary>
        /// Whether a place in the world is inside this zone. Height is ignored, so
        /// a zone covers whatever ground it was marked over rather than a slab at
        /// one altitude.
        /// </summary>
        public bool Contains(Vector3 world)
        {
            return Inside(transform.position, _size, world);
        }

        /// <summary>
        /// Whether a place is safe. A scene with no zones marked anywhere has
        /// nowhere that is unsafe, so the whole map is safe until the first one is
        /// drawn.
        /// </summary>
        public static bool IsSafe(Vector3 world)
        {
            var zones = Zones();

            if (zones.Length == 0)
            {
                return true;
            }

            foreach (var zone in zones)
            {
                if (zone != null && zone.Contains(world))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The rectangle test itself, kept apart from the scene so it can be reasoned about on its own.</summary>
        public static bool Inside(Vector3 centre, Vector2 size, Vector3 world)
        {
            var offset = world - centre;

            return Mathf.Abs(offset.x) <= size.x * 0.5f && Mathf.Abs(offset.z) <= size.y * 0.5f;
        }

        /// <summary>
        /// The zones in the scene, looked up once and remembered: they are marked
        /// in the editor and do not move while the game runs, so looking again
        /// every frame would only cost time.
        /// </summary>
        private static SafeZone[] Zones()
        {
            if (_zones != null && _zones.Length > 0)
            {
                return _zones;
            }

            _zones = FindObjectsByType<SafeZone>();

            return _zones;
        }

        private void OnDrawGizmos()
        {
            var box = new Vector3(_size.x, Depth, _size.y);
            var centre = transform.position + (Vector3.up * (Depth * 0.5f));

            Gizmos.color = new Color(0.25f, 0.9f, 0.35f, 0.18f);
            Gizmos.DrawCube(centre, box);

            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.9f);
            Gizmos.DrawWireCube(centre, box);
        }

        /// <summary>How tall the drawn box is, which is only a way of seeing it - the zone itself has no height.</summary>
        private const float Depth = 1.5f;
    }
}

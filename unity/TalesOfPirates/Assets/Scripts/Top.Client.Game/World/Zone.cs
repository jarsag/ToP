using System.Collections.Generic;
using UnityEngine;

namespace Top.Client.Game.World
{
    /// <summary>
    /// What a zone is for. The kind is what the tool draws it by and what the
    /// world asks it for: a safe zone is ground the hero is out of danger on, a
    /// spawn point is one of the places he can start from.
    /// </summary>
    public enum ZoneKind
    {
        Safe,
        Spawn,
    }

    /// <summary>
    /// A marked piece of the map. Zones are scene objects in world units and the
    /// runtime reads them where they stand - the drawing belongs to the editor and
    /// nothing is baked.
    /// <br/>
    /// A safe zone is the rectangle drawn over the ground; a spawn point is a
    /// circle, because a place a body is put down on has a reach rather than
    /// corners. Neither has a height: a zone covers whatever ground it was marked
    /// over, so a body is inside it at any altitude, which is why one is drawn as a
    /// column reaching well below and above the terrain.
    /// </summary>
    public class Zone : MonoBehaviour
    {
        [SerializeField] private ZoneKind _kind = ZoneKind.Safe;
        [SerializeField] private Vector2 _size = new Vector2(64f, 64f);

        private static Zone[] _zones;

        /// <summary>What the zone is for.</summary>
        public ZoneKind Kind
        {
            get { return _kind; }
            set { _kind = value; }
        }

        /// <summary>How wide and how deep the zone is, in map units.</summary>
        public Vector2 Size
        {
            get { return _size; }
            set { _size = value; }
        }

        /// <summary>How far a spawn point reaches, in map units.</summary>
        public float Radius => Mathf.Min(_size.x, _size.y) * 0.5f;

        /// <summary>Whether a place in the world is inside this zone.</summary>
        public bool Contains(Vector3 world)
        {
            return Inside(transform.position, _size, _kind, world);
        }

        /// <summary>
        /// Whether a place is safe: inside any zone marked as one. A scene with no
        /// safe zones marked anywhere has nowhere that is unsafe, so the whole map
        /// is safe until the first one is drawn - spawn points do not make the rest
        /// of the world war.
        /// </summary>
        public static bool IsSafe(Vector3 world)
        {
            var marked = false;

            foreach (var zone in Zones())
            {
                if (zone == null || zone._kind != ZoneKind.Safe)
                {
                    continue;
                }

                marked = true;

                if (zone.Contains(world))
                {
                    return true;
                }
            }

            return !marked;
        }

        /// <summary>
        /// One of the spawn points in the scene, picked at random, which is where a
        /// body starts. False when the scene has none, and then whoever asked keeps
        /// the place the scene put them in.
        /// </summary>
        public static bool TryRandomSpawn(out Vector3 where)
        {
            where = default;

            var spawns = new List<Zone>();

            foreach (var zone in Zones())
            {
                if (zone != null && zone._kind == ZoneKind.Spawn)
                {
                    spawns.Add(zone);
                }
            }

            if (spawns.Count == 0)
            {
                return false;
            }

            where = spawns[Random.Range(0, spawns.Count)].transform.position;

            return true;
        }

        /// <summary>The colour a kind is drawn in, which is the only thing telling two kinds apart in a scene.</summary>
        public static Color Colour(ZoneKind kind)
        {
            return kind == ZoneKind.Spawn ? new Color(1f, 0.85f, 0.2f) : new Color(0.3f, 1f, 0.4f);
        }

        /// <summary>The shape test itself, kept apart from the scene so it can be reasoned about on its own.</summary>
        public static bool Inside(Vector3 centre, Vector2 size, ZoneKind kind, Vector3 world)
        {
            var offset = world - centre;

            if (kind == ZoneKind.Spawn)
            {
                var radius = Mathf.Min(size.x, size.y) * 0.5f;

                return (offset.x * offset.x) + (offset.z * offset.z) <= radius * radius;
            }

            return Mathf.Abs(offset.x) <= size.x * 0.5f && Mathf.Abs(offset.z) <= size.y * 0.5f;
        }

        /// <summary>
        /// The zones in the scene, looked up once and remembered: they are marked in
        /// the editor and do not move while the game runs, so looking again every
        /// frame would only cost time.
        /// </summary>
        private static Zone[] Zones()
        {
            if (_zones != null && _zones.Length > 0)
            {
                return _zones;
            }

            _zones = FindObjectsByType<Zone>();

            return _zones;
        }

        private void OnDrawGizmos()
        {
            if (_kind == ZoneKind.Spawn)
            {
                Disc();

                return;
            }

            Column();
        }

        /// <summary>
        /// A safe zone drawn as a column rather than a slab: it has no height worth
        /// speaking of, so it reaches well below and above whatever the terrain
        /// does rather than reading as standing on one altitude.
        /// </summary>
        private void Column()
        {
            const float Below = 8f;
            const float Above = 24f;

            var box = new Vector3(_size.x, Above + Below, _size.y);
            var centre = transform.position + (Vector3.up * ((Above - Below) * 0.5f));
            var colour = Colour(_kind);

            colour.a = 0.07f;
            Gizmos.color = colour;
            Gizmos.DrawCube(centre, box);

            colour.a = 0.7f;
            Gizmos.color = colour;
            Gizmos.DrawWireCube(centre, box);

            // And the footprint where it means something, so the ground it covers
            // can be seen against the terrain it lies on.
            colour.a = 1f;
            Gizmos.color = colour;
            Gizmos.DrawWireCube(transform.position, new Vector3(_size.x, 0f, _size.y));
        }

        /// <summary>
        /// A spawn point drawn as the flat disc it is: a body is put down on the
        /// spot, so what matters is where it is and how far it reaches.
        /// </summary>
        private void Disc()
        {
            const float Thickness = 0.08f;

            var radius = Mathf.Max(0.01f, Radius);
            var matrix = Gizmos.matrix;
            var colour = Colour(_kind);

            Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.identity,
                new Vector3(radius, Thickness, radius));

            colour.a = 0.3f;
            Gizmos.color = colour;
            Gizmos.DrawSphere(Vector3.zero, 1f);

            colour.a = 0.9f;
            Gizmos.color = colour;
            Gizmos.DrawWireSphere(Vector3.zero, 1f);

            Gizmos.matrix = matrix;
        }
    }
}

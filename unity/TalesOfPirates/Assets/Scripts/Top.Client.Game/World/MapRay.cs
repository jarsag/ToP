using UnityEngine;

namespace Top.Client.Game.World
{
    /// <summary>
    /// Casts a ray at a map's ground. The ground is a height field rather than
    /// geometry, so there is nothing to raycast against: the ray is walked
    /// forward and compared with the height under it. That works over a map whose
    /// chunks are still streaming in, and over ground no mesh has been built for
    /// at all - which is exactly what a click on the far side of the map is.
    /// </summary>
    public static class MapRay
    {
        /// <summary>How far the ray is walked between checks, in world units.</summary>
        private const float Step = 0.5f;

        /// <summary>How many times the crossing is halved to find it, which is a fraction of a millimetre here.</summary>
        private const int Refinements = 12;

        /// <summary>
        /// Where a ray meets the map's ground, if it does. A ray that starts
        /// under the ground, or that leaves the map's tiles before it lands,
        /// misses: the edge of the map is not somewhere to walk to.
        /// </summary>
        public static bool TryHit(MapData map, Ray ray, float distance, out Vector3 hit)
        {
            hit = default;

            if (map == null || distance <= 0f || Ground(map, ray.origin) > ray.origin.y)
            {
                return false;
            }

            for (var travelled = Step; travelled <= distance; travelled += Step)
            {
                var point = ray.GetPoint(travelled);

                if (Ground(map, point) <= point.y)
                {
                    continue;
                }

                hit = Refine(map, ray, travelled - Step, travelled);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Halves the step that crossed the ground until the two ends are the
        /// same place, so a click lands on the surface rather than half a metre
        /// into it.
        /// </summary>
        private static Vector3 Refine(MapData map, Ray ray, float above, float below)
        {
            for (var i = 0; i < Refinements; i++)
            {
                var middle = (above + below) * 0.5f;

                if (Ground(map, ray.GetPoint(middle)) > ray.GetPoint(middle).y)
                {
                    below = middle;
                }
                else
                {
                    above = middle;
                }
            }

            return ray.GetPoint(below);
        }

        /// <summary>
        /// The ground under a world point, or infinity where the map has no
        /// ground: past its tiles. Infinity rather than a height means a ray
        /// cannot land there.
        /// <br/>
        /// The height comes from the map's own field rather than from a chunk that has
        /// been built, which is the whole point of walking a ray rather than casting
        /// one: a map streams its chunks in around whoever is looking at it, so ground
        /// on the far side of one - or ground no mesh has been built for at all - has
        /// no chunk to be tested against. Asking for one there refuses a click the map
        /// can answer perfectly well, which is what made a zone impossible to mark on a
        /// map nothing had been built on yet.
        /// </summary>
        private static float Ground(MapData map, Vector3 world)
        {
            var point = MapSpace.ToMap(world);
            var x = Mathf.FloorToInt(point.x);
            var y = Mathf.FloorToInt(point.y);

            if (x < 0 || y < 0 || x >= map.Width || y >= map.Height)
            {
                return float.PositiveInfinity;
            }

            // The surface rather than the relief: a click lands on the bridge a
            // body would stand on, not on the ground under it.
            return map.SurfaceAt(point.x, point.y);
        }
    }
}

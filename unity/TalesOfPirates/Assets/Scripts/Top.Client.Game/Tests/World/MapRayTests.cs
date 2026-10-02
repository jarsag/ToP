using NUnit.Framework;
using Top.Client.Game.World;
using Top.Contracts.Assets.Maps;
using UnityEngine;

namespace Top.Client.Game.Tests.World
{
    /// <summary>
    /// Walking a ray at a height field, which is what a click on the map is:
    /// there is no geometry to raycast against, so the answer has to come out of
    /// the map's own data.
    /// </summary>
    public class MapRayTests
    {
        [Test]
        public void A_ray_straight_down_lands_on_the_ground_under_it()
        {
            var map = Flat(64, 2f);
            var ray = new Ray(new Vector3(-10f, 20f, 10f), Vector3.down);

            Assert.That(MapRay.TryHit(map, ray, 100f, out var hit), Is.True);
            Assert.That(hit.x, Is.EqualTo(-10f).Within(0.01f));
            Assert.That(hit.z, Is.EqualTo(10f).Within(0.01f));
            Assert.That(hit.y, Is.EqualTo(2f).Within(0.01f));
        }

        [Test]
        public void A_ray_that_never_comes_down_misses()
        {
            var map = Flat(64, 2f);
            var ray = new Ray(new Vector3(-10f, 20f, 10f), Vector3.up);

            Assert.That(MapRay.TryHit(map, ray, 100f, out _), Is.False);
        }

        [Test]
        public void Ground_past_the_maps_tiles_is_not_something_to_land_on()
        {
            var map = Flat(64, 2f);

            // World x -200 is map x 200, well past a 64 tile map, so the water
            // tile the client falls back to is not ground here.
            var ray = new Ray(new Vector3(-200f, 20f, 10f), Vector3.down);

            Assert.That(MapRay.TryHit(map, ray, 100f, out _), Is.False);
        }

        [Test]
        public void A_ray_that_starts_under_the_ground_misses()
        {
            var map = Flat(64, 2f);
            var ray = new Ray(new Vector3(-10f, 1f, 10f), Vector3.down);

            Assert.That(MapRay.TryHit(map, ray, 100f, out _), Is.False);
        }

        [Test]
        public void A_slanted_ray_lands_where_the_ground_rises_to_meet_it()
        {
            // A ramp along the map's x: tile x is x metres high, so the ground
            // rises at exactly the angle the ray comes down at.
            var map = Ramp(64);
            var ray = new Ray(new Vector3(0f, 40f, 8f), new Vector3(-1f, -1f, 0f).normalized);

            Assert.That(MapRay.TryHit(map, ray, 200f, out var hit), Is.True);

            // World x runs opposite to map x, so the landing point has to satisfy
            // ground height == map x.
            Assert.That(-hit.x, Is.EqualTo(hit.y).Within(1.5f));
            Assert.That(hit.z, Is.EqualTo(8f).Within(0.01f));
        }

        private static MapData Flat(int size, float ground)
        {
            var map = new MapFile(size, size, size);
            var chunk = new MapChunk(size);

            for (var i = 0; i < chunk.Tiles.Length; i++)
            {
                chunk.Tiles[i] = new MapTile { Height = ground };
            }

            map.Chunks[0, 0] = chunk;

            return new MapData(map);
        }

        private static MapData Ramp(int size)
        {
            var map = new MapFile(size, size, size);
            var chunk = new MapChunk(size);

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    chunk.Tiles[(y * size) + x] = new MapTile { Height = x };
                }
            }

            map.Chunks[0, 0] = chunk;

            return new MapData(map);
        }
    }
}

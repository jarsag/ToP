using System;
using UnityEngine;

namespace Top.Client.Game.World
{
    /// <summary>
    /// Walks a point across a map. The ground is the map's own height field and
    /// the walls are its attribute cells, so nothing here uses Unity physics: a
    /// walker costs a handful of field reads a frame and behaves the same in a
    /// WebGL build as in the editor. The original client worked this way, and
    /// its blocked cells are a layer of their own - a wall here need not be a
    /// wall you can see.
    /// </summary>
    public class MapWalker
    {
        /// <summary>
        /// Attribute cells are half a tile across, so a map coordinate names a
        /// cell at twice its value.
        /// </summary>
        private const float CellsPerTile = 2f;

        private readonly MapData _map;
        private readonly float _radius;

        private Vector2 _position;

        public MapWalker(MapData map, Vector2 position, float radius)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));

            _position = position;
            _radius = Mathf.Max(0f, radius);
        }

        public Vector2 Position => _position;

        /// <summary>
        /// The terrain height under the walker. The terrain mesh is built from
        /// this same field, so standing here puts a body on the surface rather
        /// than near it.
        /// </summary>
        public float GroundHeight => _map.HeightAt(_position.x, _position.y);

        /// <summary>
        /// Where the walker stands in world space, already at ground level.
        /// </summary>
        public Vector3 WorldPosition => MapSpace.ToWorld(_position.x, _position.y, GroundHeight);

        /// <summary>
        /// Moves by a map space delta and reports whether anything moved. Each
        /// axis is tried on its own, so a walker pressed against a wall slides
        /// along it instead of sticking. A step that would put the walker's
        /// circle over a blocked cell, or past the edge of the map, is refused:
        /// the map's edge is a wall like any other.
        /// </summary>
        public bool Step(Vector2 delta)
        {
            var moved = false;

            if (CanStand(_position + new Vector2(delta.x, 0f)))
            {
                _position.x += delta.x;
                moved = true;
            }

            if (CanStand(_position + new Vector2(0f, delta.y)))
            {
                _position.y += delta.y;
                moved = true;
            }

            return moved;
        }

        /// <summary>
        /// Drops the walker somewhere without asking whether it fits, for the
        /// first placement on a map that has only just loaded.
        /// </summary>
        public void PlaceAt(Vector2 position)
        {
            _position = position;
        }

        private bool CanStand(Vector2 position)
        {
            var minX = Cell(position.x - _radius);
            var maxX = Cell(position.x + _radius);
            var minY = Cell(position.y - _radius);
            var maxY = Cell(position.y + _radius);

            for (var cellY = minY; cellY <= maxY; cellY++)
            {
                for (var cellX = minX; cellX <= maxX; cellX++)
                {
                    if (_map.IsBlocked(cellX, cellY))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static int Cell(float mapCoordinate)
        {
            return Mathf.FloorToInt(mapCoordinate * CellsPerTile);
        }
    }
}

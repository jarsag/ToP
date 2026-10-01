using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Top.Contracts.Assets.Maps;
using Top.Contracts.Tables;
using Top.Contracts.Tables.World;

namespace Top.Conversion.Pipeline
{
    /// <summary>
    /// One place on a map a hero can be put, in map units. World space is
    /// (-X, height, Y), so the two numbers here are enough to stand somewhere.
    /// </summary>
    public class SpawnCoordinate
    {
        /// <summary>
        /// "start" for the map's own start, otherwise the kind of the placement
        /// that put something here.
        /// </summary>
        public string Kind { get; set; }

        /// <summary>The sceneobjinfo row the object belongs to, 0 for the start.</summary>
        public int ObjectId { get; set; }

        public float X { get; set; }

        public float Y { get; set; }

        /// <summary>The height of the tile the point stands on.</summary>
        public float Height { get; set; }

        /// <summary>How far the object sits above that tile; 0 for the start.</summary>
        public float HeightOffset { get; set; }

        /// <summary>Which way the object faces, in degrees.</summary>
        public float Facing { get; set; }

        /// <summary>Whether the tile is above the water line the client draws.</summary>
        public bool Land { get; set; }

        /// <summary>
        /// Where the point is in the scene's own space, which is what a
        /// transform takes: world is (-map x, height, map y). Printed as well
        /// as the map coordinates because the two differ by a reflected x, and
        /// a list that gives only one of them is a list somebody has to
        /// translate before it is useful.
        /// </summary>
        public float WorldX => -X;

        public float WorldZ => Y;
    }

    /// <summary>
    /// Every spawn point of one converted map: the map's own start from its
    /// mapinfo row, then every object the map places. It is read from the
    /// converted map rather than from the client, so the list is what the
    /// runtime will actually load - a placement the converter dropped for
    /// standing past the map edge is not offered here either.
    /// </summary>
    public class MapSpawns
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string MapPath { get; set; }

        /// <summary>
        /// The start first, then the placements. Empty when the tree does not
        /// hold this map yet.
        /// </summary>
        public List<SpawnCoordinate> Points { get; set; } = new List<SpawnCoordinate>();
    }

    /// <summary>
    /// Reads a converted map's spawn points: where the client says a map
    /// starts, and where its objects stand.
    /// </summary>
    public static class SpawnReport
    {
        /// <summary>
        /// The water line the client draws at: ground above this is land, and
        /// the water mesh fades where the ground is higher.
        /// </summary>
        public const float LandDepth = -0.5f;

        /// <summary>
        /// The spawn points of one converted map, or null when the map table in
        /// the tree names no such map. A map the table names but the tree does
        /// not hold yet comes back with no points, so "not converted" reads
        /// differently from "no such map".
        /// </summary>
        public static MapSpawns Read(ConversionSettings settings, string unit)
        {
            var entry = Entry(settings, unit);

            if (entry == null)
            {
                return null;
            }

            var spawns = new MapSpawns
            {
                Id = entry.Id,
                Name = entry.Name,
                DisplayName = entry.DisplayName,
                MapPath = entry.MapPath
            };

            var path = settings.Output.At(entry.MapPath);

            if (!File.Exists(path))
            {
                return spawns;
            }

            using var stream = File.OpenRead(path);

            var map = MapFile.Read(stream);

            spawns.Points.Add(Start(map, entry));
            spawns.Points.AddRange(Placed(map));

            return spawns;
        }

        /// <summary>
        /// The list as JSON, for a caller rather than a person.
        /// </summary>
        public static string Json(MapSpawns spawns)
        {
            return JsonConvert.SerializeObject(spawns, Formatting.Indented);
        }

        /// <summary>
        /// The list as lines: one per point, with the numbers a hero needs. The
        /// numbers are written for a machine to read back - a coordinate with a
        /// decimal comma in it is a coordinate somebody has to translate before
        /// it goes into a transform.
        /// </summary>
        public static IEnumerable<string> Lines(MapSpawns spawns)
        {
            var culture = CultureInfo.InvariantCulture;

            yield return $"map {spawns.Id} {spawns.Name} ({spawns.DisplayName}) -> {spawns.MapPath}";

            if (spawns.Points.Count == 0)
            {
                yield return "  not in the tree yet - convert it first";

                yield break;
            }

            var start = spawns.Points[0];

            yield return string.Format(culture, "  start  map {0:0.##},{1:0.##}   world ({2:0.##}, {3:0.##})   " +
                                                "height {4:0.##}", start.X, start.Y, start.WorldX, start.WorldZ,
                             start.Height) +
                         (start.Land ? string.Empty : "   (under water)");

            yield return "      #  kind     object     map x     map y   world x   world z   height  facing";

            var index = 0;

            foreach (var point in spawns.Points.Skip(1))
            {
                index++;

                yield return string.Format(culture,
                    "  {0,5}  {1,-7} {2,7}  {3,9:0.##} {4,9:0.##} {5,9:0.##} {6,9:0.##} {7,8:0.##} {8,7:0.#}",
                    index, point.Kind, point.ObjectId, point.X, point.Y, point.WorldX, point.WorldZ, point.Height,
                    point.Facing);
            }

            yield return "  a tile is a metre; world (-map x, height, map y) is what a transform takes";
        }

        /// <summary>
        /// The map a unit names, by mapinfo id where it numbers one and by file
        /// name otherwise, read from the tree's own map table.
        /// </summary>
        private static MapEntry Entry(ConversionSettings settings, string unit)
        {
            var path = settings.Output.At("tables/maps.json");

            if (!File.Exists(path))
            {
                return null;
            }

            List<MapEntry> maps;

            using (var stream = File.OpenRead(path))
            {
                maps = new TableFormat().Read<MapEntry>(stream);
            }

            var text = unit == null ? string.Empty : unit.Trim();

            if (int.TryParse(text, out var id) && id != 0)
            {
                var byId = maps.FirstOrDefault(entry => entry.Id == id);

                if (byId != null)
                {
                    return byId;
                }
            }

            return maps.FirstOrDefault(entry =>
                string.Equals(entry.Name, text, StringComparison.OrdinalIgnoreCase));
        }

        private static SpawnCoordinate Start(MapFile map, MapEntry entry)
        {
            var height = Height(map, entry.StartX, entry.StartY);

            return new SpawnCoordinate
            {
                Kind = "start",
                X = entry.StartX,
                Y = entry.StartY,
                Height = height,
                Land = height > LandDepth
            };
        }

        private static IEnumerable<SpawnCoordinate> Placed(MapFile map)
        {
            for (var chunkY = 0; chunkY < map.ChunkCountY; chunkY++)
            {
                for (var chunkX = 0; chunkX < map.ChunkCountX; chunkX++)
                {
                    var chunk = map.Chunks[chunkX, chunkY];

                    if (chunk == null)
                    {
                        continue;
                    }

                    foreach (var placement in chunk.Placements)
                    {
                        var height = Height(map, (int)Math.Floor(placement.X), (int)Math.Floor(placement.Y));

                        yield return new SpawnCoordinate
                        {
                            Kind = placement.Kind == PlacementKind.Model ? "model" : "effect",
                            ObjectId = placement.Id,
                            X = placement.X,
                            Y = placement.Y,
                            Height = height,
                            HeightOffset = placement.HeightOffset,
                            Facing = placement.Yaw,
                            Land = height > LandDepth
                        };
                    }
                }
            }
        }

        /// <summary>
        /// The height of the tile a point falls in - the tile the runtime
        /// grounds a body on. A point past the map, or in a chunk the map never
        /// wrote, reads as the underwater tile the client falls back to.
        /// </summary>
        private static float Height(MapFile map, int x, int y)
        {
            if (x < 0 || y < 0 || x >= map.Width || y >= map.Height)
            {
                return MapTile.Underwater.Height;
            }

            var chunk = map.Chunks[x / map.ChunkSize, y / map.ChunkSize];

            if (chunk == null)
            {
                return MapTile.Underwater.Height;
            }

            return chunk.Tiles[((y % map.ChunkSize) * map.ChunkSize) + (x % map.ChunkSize)].Height;
        }
    }
}

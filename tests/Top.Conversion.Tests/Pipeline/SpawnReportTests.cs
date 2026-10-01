using System.Linq;
using NUnit.Framework;
using Top.Conversion.Pipeline;
using Top.Legacy.Tables;
using Top.Legacy.Tables.Records;
using Original = Top.Legacy.MindPower.World;

namespace Top.Conversion.Tests.Pipeline
{
    /// <summary>
    /// The spawn points of a converted map: where the client says the map
    /// starts, and where its objects stand.
    /// </summary>
    public class SpawnReportTests
    {
        private FakeClient _client;

        [SetUp]
        public void SetUp()
        {
            _client = new FakeClient();
        }

        [TearDown]
        public void TearDown()
        {
            _client.Dispose();
        }

        [Test]
        public void TheStartComesFirstWithTheCoordinatesMapinfoGives()
        {
            var spawns = Read(maps: [Map(1, "shore", "Shore", 50, 50)]);

            Assert.That(spawns.Id, Is.EqualTo(1));
            Assert.That(spawns.Name, Is.EqualTo("shore"));
            Assert.That(spawns.MapPath, Is.EqualTo("maps/shore.map"));

            var start = spawns.Points[0];

            Assert.That(start.Kind, Is.EqualTo("start"));
            Assert.That(start.X, Is.EqualTo(50f));
            Assert.That(start.Y, Is.EqualTo(50f));
            Assert.That(start.Land, Is.True);
        }

        [Test]
        public void EveryObjectTheMapPlacesIsAPoint()
        {
            var objects = LegacyMaps.Objects(128, 64);

            objects.Sections[0] = new Original.ObjSection
            {
                Objects = [LegacyMaps.Model(501, 250, 700, 20, 90)]
            };
            objects.Sections[8] = new Original.ObjSection
            {
                Objects = [LegacyMaps.Effect(12, 600, 500, -150, 5730)]
            };

            var spawns = Read(maps: [Map(1, "shore", "Shore", 50, 50)], objects: objects);
            var points = spawns.Points.Skip(1).ToList();

            Assert.That(points, Has.Count.EqualTo(2));

            Assert.That(points[0].Kind, Is.EqualTo("model"));
            Assert.That(points[0].ObjectId, Is.EqualTo(501));
            Assert.That(points[0].X, Is.EqualTo(2.5f), "centimeters become metres");
            Assert.That(points[0].Y, Is.EqualTo(7f));
            Assert.That(points[0].HeightOffset, Is.EqualTo(0.2f));
            Assert.That(points[0].Facing, Is.EqualTo(90f), "model yaw was already whole degrees");

            Assert.That(points[1].Kind, Is.EqualTo("effect"));
            Assert.That(points[1].ObjectId, Is.EqualTo(12));
            Assert.That(points[1].X, Is.EqualTo(70f));
            Assert.That(points[1].Y, Is.EqualTo(5f));
        }

        [Test]
        public void GroundBelowTheWaterLineReadsAsWater()
        {
            var terrain = LegacyMaps.FilledTerrain(LegacyMaps.NewFormat, 64, 64);

            for (var i = 0; i < terrain.Sections.Length; i++)
            {
                terrain.Sections[i] = LegacyMaps.Section(_ => new Original.MapTile
                {
                    HeightStep = -30,
                    Alpha0 = 15,
                    Block = new byte[4]
                });
            }

            var spawns = Read(name: "deep", maps: [Map(1, "deep", "Deep", 10, 10)], terrain: terrain);
            var start = spawns.Points[0];

            Assert.That(start.Height, Is.EqualTo(-3f).Within(0.001f));
            Assert.That(start.Land, Is.False);
        }

        [Test]
        public void AMapIsFoundByItsMapInfoId()
        {
            var spawns = Read(unit: "7", maps: [Map(7, "shore", "Shore", 50, 50)]);

            Assert.That(spawns.Name, Is.EqualTo("shore"));
        }

        [Test]
        public void AMapTheTreeDoesNotHoldYetComesBackWithNoPoints()
        {
            var spawns = Read(name: "garner", convert: false, maps: [Map(1, "garner", "Ascaron", 1, 1)]);

            Assert.That(spawns.Name, Is.EqualTo("garner"));
            Assert.That(spawns.Points, Is.Empty);
        }

        [Test]
        public void AMapTheTableDoesNotNameReadsAsNothing()
        {
            _client.AddMap("stray", LegacyMaps.FilledTerrain(LegacyMaps.NewFormat, 64, 64));

            var settings = _client.Settings();
            var tables = new ClientTables(null, null, null, FakeClient.ActionSet(7), null,
                new Table<MapInfoRecord>([]));

            new TableConverter(settings, tables).ConvertAll().ToList();

            Assert.That(SpawnReport.Read(settings, "stray"), Is.Null);
        }

        [Test]
        public void TheLinesCarryTheCoordinatesAndHowToReadThem()
        {
            var spawns = Read(maps: [Map(1, "shore", "Shore", 50, 50)]);
            var lines = SpawnReport.Lines(spawns).ToList();

            Assert.That(lines[0], Is.EqualTo("map 1 shore (Shore) -> maps/shore.map"));
            Assert.That(lines[1], Does.Contain("start  map 50,50"));
            Assert.That(lines[1], Does.Contain("world (-50, 50)"));
            Assert.That(lines.Any(line => line.Contains("world (-map x, height, map y)")), Is.True);
        }

        [Test]
        public void AWorldCoordinateIsTheMapOneReflected()
        {
            var spawns = Read(maps: [Map(1, "shore", "Shore", 50, 50)]);
            var start = spawns.Points[0];

            Assert.That(start.WorldX, Is.EqualTo(-50f));
            Assert.That(start.WorldZ, Is.EqualTo(50f));
            Assert.That(start.WorldX, Is.EqualTo(-start.X));
            Assert.That(start.WorldZ, Is.EqualTo(start.Y));
        }

        private MapSpawns Read(string name = "shore", string unit = null,
            Original.MapFile terrain = null, Original.ObjFile objects = null,
            MapInfoRecord[] maps = null, bool convert = true)
        {
            _client.AddMap(name, terrain ?? LegacyMaps.FilledTerrain(LegacyMaps.NewFormat, 128, 64), objects);

            var settings = _client.Settings();
            var tables = new ClientTables(null, null, null, FakeClient.ActionSet(7), null,
                new Table<MapInfoRecord>((maps ?? []).ToList()));

            new TableConverter(settings, tables).ConvertAll().ToList();

            if (convert)
            {
                new MapConverter(settings, tables).Convert(name);
            }

            return SpawnReport.Read(settings, unit ?? name);
        }

        private static MapInfoRecord Map(int id, string name, string displayName, int startX, int startY)
        {
            return new MapInfoRecord
            {
                Id = id,
                Name = name,
                DisplayName = displayName,
                InitX = startX,
                InitY = startY
            };
        }
    }
}

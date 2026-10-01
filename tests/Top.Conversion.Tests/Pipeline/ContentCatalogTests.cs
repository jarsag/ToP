using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Top.Conversion.Pipeline;
using Top.Legacy.Tables;
using Top.Legacy.Tables.Records;

namespace Top.Conversion.Tests.Pipeline
{
    public class ContentCatalogTests
    {
        private FakeClient _client;
        private CaptureLog _log;

        [SetUp]
        public void SetUp()
        {
            _client = new FakeClient();
            _log = new CaptureLog();
        }

        [TearDown]
        public void TearDown()
        {
            _log.Dispose();
            _client.Dispose();
        }

        [Test]
        public void EveryFamilyTheClientSuppliesIsListed()
        {
            _client.AddMap("garner", LegacyMaps.FilledTerrain(LegacyMaps.NewFormat, 8, 8));

            var catalog = Catalog(
                characters: [Character(1, "Long Haired Guy", CharacterModalType.MainCharacter, 1)],
                items: [Item(1, "Short Sword", ItemType.Sword)],
                sceneObjects: [Scene(10, "Stone01.lgo")],
                maps: [Map(1, "garner", "Ascaron")]);

            Assert.That(catalog.Section(ContentKind.Character).Entries, Has.Count.EqualTo(1));
            Assert.That(catalog.Section(ContentKind.Item).Entries, Has.Count.EqualTo(1));
            Assert.That(catalog.Section(ContentKind.Scene).Entries, Has.Count.EqualTo(1));
            Assert.That(catalog.Section(ContentKind.Map).Entries, Has.Count.EqualTo(1));
            Assert.That(catalog.Section(ContentKind.Table).Entries, Has.Count.EqualTo(3));
        }

        [Test]
        public void SectionsKeepTheOrderAChooserOffersThem()
        {
            var kinds = Catalog().Sections.Select(section => section.Kind);

            Assert.That(kinds, Is.EqualTo(new[]
            {
                ContentKind.Character, ContentKind.Item, ContentKind.Scene, ContentKind.Map, ContentKind.Table
            }));
        }

        [Test]
        public void AFamilyWhoseTableIsMissingSaysWhichFile()
        {
            var settings = _client.Settings();
            var tables = new ClientTables(null, null, null, null);

            var section = new ContentCatalog(settings, tables, new TableConverter(settings, tables).Units)
                .Section(ContentKind.Character);

            Assert.That(section.Available, Is.False);
            Assert.That(section.Entries, Is.Empty);
            Assert.That(section.Trouble, Does.StartWith("no table at"));
            Assert.That(section.Trouble, Does.Contain("characterinfo.txt"));
        }

        [Test]
        public void AFamilyWhoseTableIsEmptySaysSo()
        {
            var section = Catalog().Section(ContentKind.Character);

            Assert.That(section.Available, Is.False);
            Assert.That(section.Entries, Is.Empty);
            Assert.That(section.Trouble, Does.Contain("characterinfo.txt"));
            Assert.That(section.Summary, Does.Contain("character"));
        }

        [Test]
        public void MapsComeFromTheFolderNotTheTable()
        {
            // garner is on disk and in mapinfo, stray is on disk only, and
            // mapinfo names a map that never shipped.
            _client.AddMap("garner", LegacyMaps.FilledTerrain(LegacyMaps.NewFormat, 8, 8));
            _client.AddMap("stray", LegacyMaps.FilledTerrain(LegacyMaps.NewFormat, 8, 8));

            var entries = Catalog(maps: [Map(1, "garner", "Ascaron"), Map(9, "vanished", "Gone")])
                .Section(ContentKind.Map).Entries;

            Assert.That(entries.Select(entry => entry.Name), Is.EqualTo(new[] { "garner", "stray" }));
        }

        [Test]
        public void ACharacterEntryCarriesItsModalTypeAndModel()
        {
            var entry = Catalog(characters:
                [
                    Character(1, "Long Haired Guy", CharacterModalType.MainCharacter, 1),
                    Character(6, "Weird Monster", CharacterModalType.Other, 724)
                ]).Section(ContentKind.Character).Entries;

            Assert.That(entry[0].Detail, Is.EqualTo("player, model 1"));
            Assert.That(entry[1].Detail, Is.EqualTo("monster, model 724"));
        }

        [Test]
        public void AnItemEntrySaysWhetherItIsWorn()
        {
            var entries = Catalog(items:
            [
                Item(1, "Short Sword", ItemType.Sword),
                Item(2, "Cotton Shirt", ItemType.Clothing)
            ]).Section(ContentKind.Item).Entries;

            Assert.That(entries[0].Detail, Is.EqualTo("Sword, held"));
            Assert.That(entries[1].Detail, Is.EqualTo("Clothing, worn"));
        }

        [Test]
        public void AMapEntryPrefersTheDisplayName()
        {
            _client.AddMap("garner", LegacyMaps.FilledTerrain(LegacyMaps.NewFormat, 8, 8));

            var entry = Catalog(maps: [Map(1, "garner", "Ascaron")]).Section(ContentKind.Map)
                .Entries.Single();

            Assert.That(entry.Id, Is.EqualTo(1));
            Assert.That(entry.Name, Is.EqualTo("garner"));
            Assert.That(entry.Detail, Is.EqualTo("Ascaron"));
            Assert.That(entry.Label, Is.EqualTo("    1  garner  -  Ascaron"));
        }

        [Test]
        public void AMapMissingFromMapinfoStillLists()
        {
            _client.AddMap("stray", LegacyMaps.FilledTerrain(LegacyMaps.NewFormat, 8, 8));

            var entry = Catalog().Section(ContentKind.Map).Entries.Single();

            Assert.That(entry.Detail, Is.Null);
            Assert.That(entry.Label, Is.EqualTo("stray"));
        }

        [Test]
        public void AMapInfoRowWithoutAFileIsNotOffered()
        {
            var section = Catalog(maps: [Map(1, "garner", "Ascaron")]).Section(ContentKind.Map);

            Assert.That(section.Available, Is.False);
            Assert.That(section.Trouble, Does.Contain("map"));
        }

        [Test]
        public void ASceneRowWithoutAModelFileIsNotOffered()
        {
            var section = Catalog(sceneObjects: [Scene(10, "Stone01.lgo"), Scene(11, string.Empty)])
                .Section(ContentKind.Scene);

            Assert.That(section.Entries.Select(entry => entry.Id), Is.EqualTo(new[] { 10 }));
        }

        [Test]
        public void ASceneTableWithNoUsableRowSaysWhy()
        {
            var section = Catalog(sceneObjects: [Scene(11, string.Empty)]).Section(ContentKind.Scene);

            Assert.That(section.Available, Is.False);
            Assert.That(section.Trouble, Does.Contain("sceneobjinfo.txt"));
        }

        [Test]
        public void ALabelLeadsWithTheId()
        {
            var entry = Catalog(characters: [Character(7, "Artisan", CharacterModalType.Other, 13)])
                .Section(ContentKind.Character).Entries.Single();

            Assert.That(entry.Label, Is.EqualTo("    7  Artisan  -  monster, model 13"));
        }

        [Test]
        public void AnUnnamedRowStillGetsALabel()
        {
            var entry = Catalog(characters: [Character(3, string.Empty, CharacterModalType.Other, 13)])
                .Section(ContentKind.Character).Entries.Single();

            Assert.That(entry.Label, Is.EqualTo("    3  (unnamed)  -  monster, model 13"));
        }

        [Test]
        public void FilteringMatchesTheName()
        {
            var catalog = Catalog(items:
            [
                Item(1, "Short Sword", ItemType.Sword),
                Item(2, "Cotton Shirt", ItemType.Clothing)
            ]);

            Assert.That(Names(catalog, ContentKind.Item, "sword"), Is.EqualTo(new[] { 1 }));
            Assert.That(Names(catalog, ContentKind.Item, "SWORD"), Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void FilteringMatchesTheDetail()
        {
            var catalog = Catalog(items:
            [
                Item(1, "Short Sword", ItemType.Sword),
                Item(2, "Cotton Shirt", ItemType.Clothing)
            ]);

            Assert.That(Names(catalog, ContentKind.Item, "worn"), Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void FilteringMatchesTheIdEvenForANamelessRow()
        {
            var catalog = Catalog(items:
            [
                Item(1, "Short Sword", ItemType.Sword),
                Item(2, "Cotton Shirt", ItemType.Clothing)
            ]);

            Assert.That(Names(catalog, ContentKind.Item, "2"), Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void AnEmptyFilterKeepsEverything()
        {
            var catalog = Catalog(items:
            [
                Item(1, "Short Sword", ItemType.Sword),
                Item(2, "Cotton Shirt", ItemType.Clothing)
            ]);

            Assert.That(Names(catalog, ContentKind.Item, null), Has.Count.EqualTo(2));
            Assert.That(Names(catalog, ContentKind.Item, "   "), Has.Count.EqualTo(2));
        }

        [Test]
        public void AFilterThatMatchesNothingIsEmpty()
        {
            var catalog = Catalog(items: [Item(1, "Short Sword", ItemType.Sword)]);

            Assert.That(Names(catalog, ContentKind.Item, "nothing like this"), Is.Empty);
        }

        [Test]
        public void TableEntriesNameTheUnitAndItsOutput()
        {
            var entries = Catalog().Section(ContentKind.Table).Entries;

            Assert.That(entries.Select(entry => entry.Name),
                Is.EqualTo(new[] { "sceneobjects", "terrains", "maps" }));
            Assert.That(entries.All(entry => entry.Id == 0 && entry.Detail.Length > 0), Is.True);
        }

        [Test]
        public void AUnitIsFoundByItsId()
        {
            var catalog = Catalog(
                characters: [Character(7, "Long Haired Guy", CharacterModalType.MainCharacter, 1)]);

            Assert.That(catalog.TryFind(ContentKind.Character, "7", out var entry, out var trouble), Is.True);
            Assert.That(entry.Name, Is.EqualTo("Long Haired Guy"));
            Assert.That(trouble, Is.Null);
        }

        [Test]
        public void AUnitIsFoundByNameWithoutCase()
        {
            _client.AddMap("garner", LegacyMaps.FilledTerrain(LegacyMaps.NewFormat, 8, 8));

            var catalog = Catalog(maps: [Map(1, "garner", "Ascaron")]);

            Assert.That(catalog.TryFind(ContentKind.Map, "GARNER", out var entry, out var trouble), Is.True);
            Assert.That(entry.Name, Is.EqualTo("garner"));
            Assert.That(entry.Id, Is.EqualTo(1));
            Assert.That(trouble, Is.Null);
        }

        [Test]
        public void AMapIsFoundByItsMapInfoId()
        {
            _client.AddMap("garner", LegacyMaps.FilledTerrain(LegacyMaps.NewFormat, 8, 8));
            _client.AddMap("stray", LegacyMaps.FilledTerrain(LegacyMaps.NewFormat, 8, 8));

            var catalog = Catalog(maps: [Map(9, "garner", "Ascaron")]);

            // The id is what the scene loads a map by, the name is what the
            // converter keys it by, and either finds it.
            Assert.That(catalog.TryFind(ContentKind.Map, "9", out var entry, out _), Is.True);
            Assert.That(entry.Name, Is.EqualTo("garner"));

            // A file mapinfo never names has no id, so only its name finds it.
            Assert.That(catalog.TryFind(ContentKind.Map, "stray", out var stray, out _), Is.True);
            Assert.That(stray.Id, Is.Zero);
        }

        [Test]
        public void AnUnknownUnitSaysWhatIsMissing()
        {
            var catalog = Catalog(
                characters: [Character(7, "Long Haired Guy", CharacterModalType.MainCharacter, 1)]);

            Assert.That(catalog.TryFind(ContentKind.Character, "99", out var entry, out var trouble), Is.False);
            Assert.That(entry, Is.Null);
            Assert.That(trouble, Does.Contain("99"));
        }

        [Test]
        public void AFamilyThatCannotBeReadReportsItsOwnTrouble()
        {
            Assert.That(Catalog().TryFind(ContentKind.Character, "1", out _, out var trouble), Is.False);
            Assert.That(trouble, Does.Contain("characterinfo.txt"));
        }

        [Test]
        public void AnUnknownFamilyIsRefusedRatherThanThrown()
        {
            Assert.That(Catalog().TryFind("nonsense", "1", out _, out var trouble), Is.False);
            Assert.That(trouble, Does.Contain("nonsense"));
        }

        private static IReadOnlyList<int> Names(ContentCatalog catalog, string kind, string filter)
        {
            return catalog.Matching(kind, filter).Select(entry => entry.Id).ToList();
        }

        private ContentCatalog Catalog(IEnumerable<CharacterInfoRecord> characters = null,
            IEnumerable<ItemInfoRecord> items = null,
            IEnumerable<SceneObjectInfoRecord> sceneObjects = null,
            IEnumerable<MapInfoRecord> maps = null)
        {
            var settings = _client.Settings();
            var tables = new ClientTables(
                new Table<CharacterInfoRecord>((characters ?? []).ToList()),
                new Table<ItemInfoRecord>((items ?? []).ToList()),
                new Table<SceneObjectInfoRecord>((sceneObjects ?? []).ToList()),
                FakeClient.ActionSet(7),
                null,
                new Table<MapInfoRecord>((maps ?? []).ToList()));

            return new ContentCatalog(settings, tables, new TableConverter(settings, tables).Units);
        }

        private static CharacterInfoRecord Character(int id, string name, CharacterModalType modal, int model)
        {
            return new CharacterInfoRecord { Id = id, Name = name, ModalType = modal, Model = model };
        }

        private static ItemInfoRecord Item(int id, string name, ItemType type)
        {
            return new ItemInfoRecord
            {
                Id = id,
                Name = name,
                Type = type,
                Modules = ["0", "0", "0", "0", "0"]
            };
        }

        private static SceneObjectInfoRecord Scene(int id, string name)
        {
            return new SceneObjectInfoRecord { Id = id, Name = name, Type = 1 };
        }

        private static MapInfoRecord Map(int id, string name, string displayName)
        {
            return new MapInfoRecord { Id = id, Name = name, DisplayName = displayName };
        }
    }
}

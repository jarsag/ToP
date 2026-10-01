using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Top.Conversion.Pipeline;
using Top.Legacy.Tables;
using Top.Legacy.Tables.Records;

namespace Top.Conversion.Tests.Pipeline
{
    /// <summary>
    /// The JSON the Unity editor window reads. Its shape is a contract with
    /// JsonUtility on the other side - Pascal case keys, every list present,
    /// every string present - so these tests check the shape, not only the
    /// values.
    /// </summary>
    public class CatalogReportTests
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
        public void ARootCarriesTheDescriptionAPickerShows()
        {
            _client.AddMap("garner", LegacyMaps.FilledTerrain(LegacyMaps.NewFormat, 8, 8));

            var roots = (JArray)JObject.Parse(CatalogReport.Roots([_client.ClientRoot]))["Roots"];

            Assert.That(roots, Has.Count.EqualTo(1));
            Assert.That(roots[0]["Path"].Value<string>(), Is.EqualTo(_client.ClientRoot));
            Assert.That(roots[0]["Description"].Value<string>(), Does.Contain("maps"));
        }

        [Test]
        public void BothListsArePresentWhicheverWasAskedFor()
        {
            // The window parses both listings with one type, so a listing that
            // simply left a list out would be unreadable there.
            var roots = JObject.Parse(CatalogReport.Roots([]));
            var catalog = Report(Catalog());

            foreach (var report in new[] { roots, catalog })
            {
                Assert.That(report["Source"], Is.Not.Null);
                Assert.That(report["Roots"], Is.Not.Null);
                Assert.That(report["Sections"], Is.Not.Null);
            }
        }

        [Test]
        public void EveryFamilyIsListedInTheOrderAChooserOffersIt()
        {
            var sections = Sections(Report(Catalog()));

            Assert.That(sections.Select(section => section["Kind"].Value<string>()), Is.EqualTo(new[]
            {
                ContentKind.Character, ContentKind.Item, ContentKind.Scene, ContentKind.Map, ContentKind.Table
            }));
        }

        [Test]
        public void AFamilyWithNothingToOfferIsStillAListWithAReason()
        {
            var character = Section(Catalog(), ContentKind.Character);

            Assert.That(character["Available"].Value<bool>(), Is.False);
            Assert.That(character["Trouble"].Value<string>(), Does.Contain("characterinfo.txt"));
            Assert.That(character["Summary"].Value<string>(), Does.Contain(ContentKind.Character));
            Assert.That(character["Entries"], Is.Empty);
        }

        [Test]
        public void AnEntryCarriesWhatAPickerPrints()
        {
            _client.AddMap("garner", LegacyMaps.FilledTerrain(LegacyMaps.NewFormat, 8, 8));

            var entry = Section(Catalog(maps: [Map(1, "garner", "Ascaron")]), ContentKind.Map)["Entries"][0];

            // The map's id is the number the scene loads it by, so the window
            // has to receive it as a number rather than as text in a label.
            Assert.That(entry["Id"].Value<int>(), Is.EqualTo(1));
            Assert.That(entry["Name"].Value<string>(), Is.EqualTo("garner"));
            Assert.That(entry["Detail"].Value<string>(), Is.EqualTo("Ascaron"));
            Assert.That(entry["Label"].Value<string>(), Does.Contain("garner"));
        }

        [Test]
        public void AnIdStaysANumberSoAWindowCanConvertByIt()
        {
            var section = Section(
                Catalog(characters: [Character(7, "Long Haired Guy", CharacterModalType.MainCharacter, 1)]),
                ContentKind.Character);

            Assert.That(section["Entries"][0]["Id"].Value<int>(), Is.EqualTo(7));
        }

        private JObject Report(ContentCatalog catalog)
        {
            return JObject.Parse(CatalogReport.Catalog(_client.ClientRoot, catalog));
        }

        private JToken Section(ContentCatalog catalog, string kind)
        {
            return Sections(Report(catalog)).First(section => section["Kind"].Value<string>() == kind);
        }

        private static JArray Sections(JObject report)
        {
            return (JArray)report["Sections"];
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

        private static MapInfoRecord Map(int id, string name, string displayName)
        {
            return new MapInfoRecord { Id = id, Name = name, DisplayName = displayName };
        }
    }
}

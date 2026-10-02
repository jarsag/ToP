using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Top.Conversion.Pipeline;
using Top.Legacy.Tables;
using Top.Legacy.Tables.Records;

namespace Top.Conversion.Tests.Pipeline
{
    public class ItemConverterTests
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

        private static ItemInfoRecord Item(int id, ItemType type, params string[] modules)
        {
            var columns = new string[5];

            for (var i = 0; i < columns.Length; i++)
            {
                columns[i] = i - 1 >= 0 && i - 1 < modules.Length ? modules[i - 1] : "0";
            }

            return new ItemInfoRecord { Id = id, Name = $"item {id}", Type = type, Modules = columns };
        }

        private ItemConverter Converter(params ItemInfoRecord[] items)
        {
            var settings = _client.Settings();
            var tables = new ClientTables(null,
                new Table<ItemInfoRecord>([..items]), null, null);

            return new ItemConverter(settings, tables, new ModelConverter(settings));
        }

        [Test]
        public void Converts_the_module_each_framework_holds_the_item_as()
        {
            _client.AddModel("item", "lgo/dirk.lgo", "held0.lgo");
            _client.AddModel("item", "lgo/dirk.lgo", "held1.lgo");
            var item = Item(5, ItemType.Sword, "held0", "held1");

            var result = Converter(item).Convert(5);

            Assert.That(result.Outcome, Is.EqualTo(ConversionOutcome.Converted));
            Assert.That(result.Wearable, Is.False);
            Assert.That(result.Modules, Is.EqualTo(new[] { "held0", "held1", null, null }));
            Assert.That(result.ModuleArtifacts[0].Kind, Is.EqualTo("item"));
            Assert.That(result.ModuleArtifacts[0].ModelPath, Is.EqualTo(_client.Converted("item", "held0")));
            Assert.That(result.ModuleArtifacts[1].Name, Is.EqualTo("held1"));
            Assert.That(result.ModuleArtifacts[2], Is.Null);
            Assert.That(result.Artifacts.Count(), Is.EqualTo(2));
        }

        [Test]
        public void An_item_no_framework_holds_converts_the_model_iteminfo_names()
        {
            // A coin, a gem, or the symbol that marks where a spot is: iteminfo
            // names the file itself instead of numbering a part per framework,
            // which is how 141 rows of this client are written.
            _client.AddModel("item", "lgo/dirk.lgo", "target.lgo");

            var item = new ItemInfoRecord
            {
                Id = 2295,
                Name = "Movement Symbol",
                Type = ItemType.General,
                Modules = ["target", "0", "0", "0", "0"]
            };

            var result = Converter(item).Convert(2295);

            Assert.That(result.Outcome, Is.EqualTo(ConversionOutcome.Converted));
            Assert.That(result.Modules, Is.EqualTo(new[] { "target", null, null, null }));
            Assert.That(result.ModuleArtifacts[0].ModelPath, Is.EqualTo(_client.Converted("item", "target")));
            Assert.That(result.Artifacts.Count(), Is.EqualTo(1));
        }

        [Test]
        public void Dressing_a_character_ignores_an_items_own_model()
        {
            // An own model is not a body part, so a character asking what its
            // framework wears gets nothing from an item shaped that way.
            _client.AddModel("item", "lgo/dirk.lgo", "target.lgo");

            var item = new ItemInfoRecord
            {
                Id = 2295,
                Name = "Movement Symbol",
                Type = ItemType.General,
                Modules = ["target", "0", "0", "0", "0"]
            };

            var result = Converter(item).ConvertModules(item, 1);

            Assert.That(result.Outcome, Is.EqualTo(ConversionOutcome.Skipped));
            Assert.That(result.Artifacts, Is.Empty);
        }

        [Test]
        public void A_wearable_takes_its_module_from_the_character_models()
        {
            _client.AddModel("character", "lgo/dirk.lgo", "worn0.lgo");

            var result = Converter(Item(5, ItemType.Clothing, "worn0")).Convert(5);

            Assert.That(result.Wearable, Is.True);
            Assert.That(result.ModuleArtifacts[0].Kind, Is.EqualTo("character"));
        }

        [Test]
        public void A_module_in_the_other_folder_is_used_and_reported()
        {
            _client.AddModel("character", "lgo/dirk.lgo", "held0.lgo");

            var result = Converter(Item(5, ItemType.Sword, "held0")).Convert(5);

            Assert.That(result.ModuleArtifacts[0].Kind, Is.EqualTo("character"));
            Assert.That(_log.Warnings,
                Has.Some.Contains("found under model/character instead of model/item"));
        }

        [Test]
        public void An_item_with_no_modules_is_skipped()
        {
            var result = Converter(Item(5, ItemType.Sword)).Convert(5);

            Assert.That(result.Outcome, Is.EqualTo(ConversionOutcome.Skipped));
            Assert.That(result.Artifacts, Is.Empty);
            Assert.That(_log.Warnings, Has.Some.Contains("has no models for any model"));
        }

        [Test]
        public void A_module_with_no_source_file_fails_the_item()
        {
            var result = Converter(Item(5, ItemType.Sword, "held0")).Convert(5);

            Assert.That(result.Outcome, Is.EqualTo(ConversionOutcome.Failed));
            Assert.That(result.Modules[0], Is.EqualTo("held0"), "the item still names it");
            Assert.That(_log.Warnings, Has.Some.Contains("no source file for module held0"));
        }

        [Test]
        public void An_unknown_id_fails()
        {
            Assert.That(Converter().Convert(5).Outcome, Is.EqualTo(ConversionOutcome.Failed));
            Assert.That(_log.Errors, Has.Some.Contains("no iteminfo row 5"));
        }

        [Test]
        public void Converting_for_one_framework_leaves_the_others_alone()
        {
            _client.AddModel("item", "lgo/dirk.lgo", "held0.lgo");
            _client.AddModel("item", "lgo/dirk.lgo", "held1.lgo");
            var item = Item(5, ItemType.Sword, "held0", "held1");

            var result = Converter(item).ConvertModules(item, model: 1);

            Assert.That(result.ModuleArtifacts[0], Is.Null);
            Assert.That(result.ModuleArtifacts[1].Name, Is.EqualTo("held1"));
            Assert.That(result.Modules[0], Is.EqualTo("held0"), "still named, just not converted");
        }

        [Test]
        public void An_item_with_no_module_for_the_asked_framework_fails()
        {
            _client.AddModel("item", "lgo/dirk.lgo", "held0.lgo");
            var item = Item(5, ItemType.Sword, "held0");

            var result = Converter(item).ConvertModules(item, model: 2);

            Assert.That(result.Outcome, Is.EqualTo(ConversionOutcome.Failed));
            Assert.That(_log.Warnings, Has.Some.Contains("has no model for model 2"));
        }

        [Test]
        public void A_framework_beyond_the_ones_items_have_models_for_fails()
        {
            _client.AddModel("item", "lgo/dirk.lgo", "held0.lgo");
            var item = Item(5, ItemType.Sword, "held0");

            var result = Converter(item).ConvertModules(item, model: 9);

            Assert.That(result.Outcome, Is.EqualTo(ConversionOutcome.Failed));
            Assert.That(_log.Warnings, Has.Some.Contains("has no model for model 9"));
        }

        [Test]
        public void Items_sharing_a_module_convert_it_once()
        {
            _client.AddModel("item", "lgo/dirk.lgo", "held0.lgo");

            var results = Converter(Item(5, ItemType.Sword, "held0"), Item(6, ItemType.Sword, "held0"))
                .ConvertAll()
                .ToList();

            Assert.That(results.Select(result => result.Outcome),
                Is.EqualTo(new[] { ConversionOutcome.Converted, ConversionOutcome.Skipped }));
        }
    }
}

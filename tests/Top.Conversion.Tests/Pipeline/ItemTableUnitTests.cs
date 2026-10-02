using System.Linq;
using NUnit.Framework;
using Top.Contracts.Tables;
using Top.Contracts.Tables.Items;
using Top.Conversion.Pipeline;
using Top.Conversion.Pipeline.Tables;
using Top.Legacy.Tables;
using Top.Legacy.Tables.Records;

namespace Top.Conversion.Tests.Pipeline
{
    public class ItemTableUnitTests
    {
        private static ItemInfoRecord Item(int id, string name, ItemType type, string icon, params string[] modules)
        {
            var columns = new string[5];

            for (var i = 0; i < columns.Length; i++)
            {
                columns[i] = i - 1 >= 0 && i - 1 < modules.Length ? modules[i - 1] : "0";
            }

            return new ItemInfoRecord { Id = id, Name = name, Type = type, Icon = icon, Modules = columns };
        }

        private static ItemEntry Entry(ItemInfoRecord item)
        {
            var tables = new ClientTables(null, new Top.Legacy.Tables.Table<ItemInfoRecord>([item]), null, null);

            return new ItemTableUnit(tables).Entries().Cast<ItemEntry>().Single();
        }

        [Test]
        public void A_worn_item_names_the_model_its_class_wears_and_the_slot_it_covers()
        {
            // The sheepy costume is made for the fourth class alone, and covers the
            // second slot of the body.
            var entry = Entry(Item(359, "Sheepy Costume", ItemType.Clothing, "e0063",
                "0", "0", "0", "0003190002"));

            Assert.That(entry.Id, Is.EqualTo(359));
            Assert.That(entry.Name, Is.EqualTo("Sheepy Costume"));
            Assert.That(entry.Type, Is.EqualTo((int)ItemType.Clothing));
            Assert.That(entry.Icon, Is.EqualTo("e0063"));
            Assert.That(entry.Models[3], Is.EqualTo("models/character/0003190002.glb"));
            Assert.That(entry.Models[0], Is.Null);
            Assert.That(entry.Slot, Is.EqualTo(2));
        }

        [Test]
        public void A_held_item_comes_from_the_item_models_and_covers_no_slot()
        {
            var entry = Entry(Item(1, "Short Sword", ItemType.Sword, "w0001", "10100001"));

            Assert.That(entry.Models[0], Is.EqualTo("models/item/10100001.glb"));
            Assert.That(entry.Slot, Is.EqualTo(0), "a sword is held rather than worn");
        }

        [Test]
        public void A_face_is_worn_but_covers_no_slot_of_its_own()
        {
            // A face is the body's own model rather than something over it, which is
            // what its module - suit zero, slot zero - says.
            var entry = Entry(Item(2554, "Face 1", ItemType.Face, "e0063", "0", "0", "0", "0003000000"));

            Assert.That(entry.Models[3], Is.EqualTo("models/character/0003000000.glb"));
            Assert.That(entry.Slot, Is.EqualTo(0));
        }

        [Test]
        public void An_item_with_no_models_at_all_names_none()
        {
            var entry = Entry(Item(2295, "Movement Symbol", ItemType.General, "0"));

            Assert.That(entry.Models, Is.Null);
            Assert.That(entry.Icon, Is.Null, "iteminfo says zero rather than naming an icon");
            Assert.That(entry.Slot, Is.EqualTo(0));
        }
    }
}

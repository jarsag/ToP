using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using Top.Contracts.Tables.Items;

namespace Top.Contracts.Tables.Tests
{
    public class ItemTableTests
    {
        private static List<ItemEntry> Read(string json)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

            return new TableFormat().Read<ItemEntry>(stream);
        }

        [Test]
        public void Json_reads_as_typed_entries()
        {
            var entries = Read("""
                [
                  { "id": 359, "name": "Sheepy Costume", "type": 22, "icon": "e0063",
                    "models": [null, null, null, "models/character/0003190002.glb"], "slot": 2 },
                  { "id": 2196, "name": "Sheepy Cap", "type": 20 }
                ]
                """);

            Assert.That(entries, Has.Count.EqualTo(2));
            Assert.That(entries[0].Id, Is.EqualTo(359));
            Assert.That(entries[0].Name, Is.EqualTo("Sheepy Costume"));
            Assert.That(entries[0].Type, Is.EqualTo(22));
            Assert.That(entries[0].Icon, Is.EqualTo("e0063"));
            Assert.That(entries[0].Slot, Is.EqualTo(2));
            Assert.That(entries[0].Models[3], Is.EqualTo("models/character/0003190002.glb"));
            Assert.That(entries[0].Models[0], Is.Null, "a class the item is not made for names no model");

            Assert.That(entries[1].Models, Is.Null, "an entry that names no models reads as none");
            Assert.That(entries[1].Slot, Is.EqualTo(0));
                Assert.That(entries[1].Icon, Is.Null);
        }
    }
}

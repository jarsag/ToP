using System.Collections.Generic;
using System.Linq;
using Top.Contracts.Tables;
using Top.Contracts.Tables.Items;
using Top.Legacy.Tables.Custom;
using Top.Legacy.Tables.Records;

namespace Top.Conversion.Pipeline.Tables
{
    /// <summary>
    /// Maps iteminfo rows to item entries. What comes out is what the runtime needs
    /// to put a thing in a hand or on a back: the model of the class wearing it,
    /// and which slot of the body it covers.
    /// </summary>
    public class ItemTableUnit(ClientTables tables) : ITableUnit
    {
        /// <summary>
        /// The model of the item lying on the ground. The client keeps it in the same
        /// column that names an item nobody wears or holds, so it is there for every
        /// item: a dropped sword is drawn as the sword, not as a sword being carried.
        /// </summary>
        private static string DropModel(ItemInfoRecord row)
        {
            return ItemModules.TryGetOwnModel(row, out var model)
                ? OutputPaths.ModelContentPath(ContentKind.Item, model)
                : null;
        }
        /// <summary>How many player classes iteminfo names a model for.</summary>
        private const int Classes = 4;

        public string Name => "items";

        public string Path => ItemTable.Path;

        public IEnumerable<TableEntry> Entries()
        {
            return tables.Items?.Select(Entry);
        }

        private static ItemEntry Entry(ItemInfoRecord row)
        {
            var wearable = ItemModules.IsWearable(row.Type);
            var models = new string[Classes];
            var slot = 0;

            for (var model = 0; model < Classes; model++)
            {
                if (!ItemModules.TryGetModule(row, model, out var module))
                {
                    continue;
                }

                models[model] = OutputPaths.ModelContentPath(
                    wearable ? ContentKind.Character : ContentKind.Item, module);

                if (wearable && slot == 0)
                {
                    slot = Slot(module);
                }
            }

            return new ItemEntry
            {
                Id = row.Id,
                Name = string.IsNullOrEmpty(row.Name) ? null : row.Name,
                Type = (int)row.Type,
                Icon = string.IsNullOrEmpty(row.Icon) || row.Icon == "0" ? null : row.Icon,
                DropModel = DropModel(row),
                Models = models.Any(path => path != null) ? models : null,
                Slot = slot == 0 ? Hand((int)row.Type) : slot,
            };
        }

        /// <summary>
        /// Which slot of the body a worn module covers, which the client writes as
        /// the last digit of the module's name: 0003190001 is the first, 0003610002
        /// the second, and a face - 0003000000 - covers none of them.
        /// </summary>
        /// <summary>
        /// Which hand an item is carried in, out of the type the client gives it: swords,
        /// two handed swords, bows, guns, daggers and staves in the right hand, a shield or
        /// a left hand guard in the left. Zero for what is worn on the body instead, and for
        /// what is not carried at all - arrows, potions, materials.
        /// </summary>
        private static int Hand(int type)
        {
            switch (type)
            {
                case 1:
                case 2:
                case 3:
                case 4:
                case 7:
                case 9:
                    return 5;
                case 6:
                case 11:
                    return 6;
                default:
                    return 0;
            }
        }

        private static int Slot(string module)
        {
            var last = module.Length > 0 ? module[module.Length - 1] : '0';

            return char.IsDigit(last) ? last - '0' : 0;
        }
    }
}

using Top.Legacy.Tables.Records;

namespace Top.Legacy.Tables.Custom
{
    /// <summary>
    /// Resolves an item's per-framework model module: iteminfo column
    /// model + 1 of the five module columns names the .lgo the client
    /// uses for player framework 0-3 - a skinned wearable part for
    /// equipment, an item mesh for held weapons. "0" or blank means the
    /// item has no model for that framework.
    /// </summary>
    public static class ItemModules
    {
        public static bool TryGetModule(ItemInfoRecord item, int model, out string module)
        {
            module = null;

            if (item?.Modules == null || model < 0 || model + 1 >= item.Modules.Length)
            {
                return false;
            }

            var value = item.Modules[model + 1];

            if (string.IsNullOrEmpty(value) || value == "0")
            {
                return false;
            }

            module = value;
            return true;
        }

        /// <summary>
        /// The one model an item carries when none of its frameworks has a
        /// module: a coin, a gem, or a symbol that marks a spot. iteminfo's
        /// model column names the file itself for those instead of numbering a
        /// part per framework - 141 rows of this client's 6343 are shaped that
        /// way, and every one of them was skipped before.
        /// </summary>
        public static bool TryGetOwnModel(ItemInfoRecord item, out string model)
        {
            model = null;

            if (item?.Modules == null || item.Modules.Length == 0)
            {
                return false;
            }

            var value = item.Modules[0];

            if (string.IsNullOrEmpty(value) || value == "0")
            {
                return false;
            }

            model = value;
            return true;
        }

        public static bool IsWearable(ItemType type)
        {
            return type is ItemType.Hair or ItemType.Face or ItemType.Clothing or ItemType.Glove
                or ItemType.Boot or ItemType.Tattoo or ItemType.Hairdo;
        }
    }
}

using Top.Contracts.Tables.Items;

namespace Top.Client.Game.Tables
{
    /// <summary>
    /// An item as the game needs it: what it is called, which icon stands for it,
    /// which slot of the body it covers, and the model the class asking wears.
    /// </summary>
    public struct ItemLookup
    {
        public string Name;
        public string Icon;
        public int Slot;

        /// <summary>The model for the class that asked, null when the item is not made for it.</summary>
        public string Model;
    }

    /// <summary>
    /// The items a converted tree names, looked up by the id the client uses. It
    /// keeps the table itself to itself: what asks is a game, and a game wants to
    /// know what a thing is called and what it looks like on a body of a class,
    /// not how a table is written.
    /// </summary>
    public class ItemCatalog
    {
        private readonly ItemTable _items;

        public ItemCatalog(ItemTable items)
        {
            _items = items;
        }

        /// <summary>How many items the tree names.</summary>
        public int Count => _items?.Count ?? 0;

        /// <summary>
        /// What an item is, for a character of a class. False when the tree names no
        /// such item, which is what an id from a newer client looks like.
        /// </summary>
        public bool TryGet(int id, int playerClass, out ItemLookup item)
        {
            item = default;

            if (_items == null || !_items.TryGetById(id, out var entry))
            {
                return false;
            }

            item.Name = entry.Name;
            item.Icon = entry.Icon;
            item.Slot = entry.Slot;
            item.Model = Model(entry, playerClass);

            return true;
        }

        private static string Model(ItemEntry entry, int playerClass)
        {
            var models = entry.Models;

            if (models == null || playerClass < 0 || playerClass >= models.Length)
            {
                return null;
            }

            return models[playerClass];
        }
    }
}

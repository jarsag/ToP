using Top.Contracts.Tables.Items;
using Top.Contracts.Tables.World;

namespace Top.Client.Game.Tables
{
    public class TableSet
    {
        public TableSet(SceneObjectTable sceneObjectTable, TerrainTable terrainTable, MapTable mapTable,
            ItemTable itemTable = null)
        {
            SceneObjectTable = sceneObjectTable;
            TerrainTable = terrainTable;
            MapTable = mapTable;
            Items = itemTable == null ? null : new ItemCatalog(itemTable);
        }

        public SceneObjectTable SceneObjectTable { get; }

        public TerrainTable TerrainTable { get; }

        public MapTable MapTable { get; }

        /// <summary>
        /// What items are, and what each is made of. Null in a tree converted before
        /// items were written: a map loads without it, and only dressing somebody
        /// needs it.
        /// </summary>
        public ItemCatalog Items { get; }
    }
}

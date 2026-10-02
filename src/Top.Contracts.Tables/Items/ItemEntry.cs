using Newtonsoft.Json;

namespace Top.Contracts.Tables.Items
{
    /// <summary>
    /// One iteminfo row as the runtime needs it: what the thing is called, what
    /// kind of thing it is, which icon stands for it, and the model each player
    /// class wears or holds it as.
    /// </summary>
    public class ItemEntry : TableEntry
    {
        [JsonProperty("name", DefaultValueHandling = DefaultValueHandling.Ignore)] public string Name;

        [JsonProperty("type")] public int Type;

        /// <summary>
        /// The name iteminfo gives the icon, which the client resolves to
        /// texture/icon/&lt;icon&gt;.png. Null when the row names none.
        /// </summary>
        [JsonProperty("icon", DefaultValueHandling = DefaultValueHandling.Ignore)] public string Icon;

        /// <summary>
        /// The model per player class, in the order iteminfo lists the classes, and
        /// null where that class has no such model - most equipment is made for one
        /// class or two, not for all four.
        /// </summary>
        [JsonProperty("models", DefaultValueHandling = DefaultValueHandling.Ignore)] public string[] Models;

        /// <summary>
        /// Which slot of the body the item covers, read from the module the client
        /// names. Zero covers none: a sword is held rather than worn, and a face is
        /// part of the body rather than something over it.
        /// </summary>
        [JsonProperty("slot")] public int Slot;

        /// <summary>
        /// The model of the item lying on the ground, out of the client's own model
        /// column - the file it draws when something has been dropped rather than worn or
        /// held. Null when the row names none, which is a hundred or so items out of six
        /// thousand.
        /// </summary>
        [JsonProperty("dropModel", DefaultValueHandling = DefaultValueHandling.Ignore)] public string DropModel;
    }
}

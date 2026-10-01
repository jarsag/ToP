using System.Collections.Generic;
using Top.Conversion.Pipeline;

namespace Top.Conversion.Cli
{
    /// <summary>
    /// Converts what one catalog entry names: the unit itself, and - for a map
    /// - the scene objects standing on that map. Both the one shot run and the
    /// wizard come here, so picking a map means the same thing in either.
    /// </summary>
    internal static class EntryRun
    {
        /// <summary>
        /// The runs the entry came to, in the order their summaries belong: the
        /// unit first, then whatever stood on it.
        /// </summary>
        internal static IReadOnlyList<KindRun> Convert(CatalogEntry entry, ConversionPipeline pipeline,
            bool objects)
        {
            var run = new KindRun(entry.Kind);
            var runs = new List<KindRun> { run };

            foreach (var result in Kinds.One(entry, pipeline))
            {
                run.Add(result);

                if (!objects || result is not MapResult map || map.Objects.Count == 0)
                {
                    continue;
                }

                var placed = new KindRun(ContentKind.Scene);

                runs.Add(placed);

                foreach (var placedResult in Kinds.Objects(map, pipeline, placed))
                {
                    placed.Add(placedResult);
                }
            }

            return runs;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Top.Conversion.Pipeline;
using Top.Logging;

namespace Top.Conversion.Cli
{
    /// <summary>
    /// Which converter a content family belongs to, for a whole family or for
    /// one unit of it. Both the flag driven run and the wizard come here, so
    /// the two can never disagree about what a kind means.
    /// </summary>
    internal static class Kinds
    {
        /// <summary>
        /// Every unit of a family.
        /// </summary>
        internal static IEnumerable<UnitResult> All(string kind, ConversionPipeline pipeline,
            IProgress<ConversionProgress> progress)
        {
            return kind switch
            {
                ContentKind.Character => pipeline.Characters.ConvertAll(progress),
                ContentKind.Item => pipeline.Items.ConvertAll(progress),
                ContentKind.Scene => pipeline.SceneObjects.ConvertAll(progress),
                ContentKind.Table => pipeline.Tables.ConvertAll(progress),
                ContentKind.Map => pipeline.Maps.ConvertAll(progress),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };
        }

        /// <summary>
        /// The one unit a catalog entry names.
        /// </summary>
        internal static IEnumerable<UnitResult> One(CatalogEntry entry, ConversionPipeline pipeline)
        {
            switch (entry.Kind)
            {
                case ContentKind.Character:
                    yield return pipeline.Characters.Convert(entry.Id);

                    break;

                case ContentKind.Item:
                    yield return pipeline.Items.Convert(entry.Id);

                    break;

                case ContentKind.Scene:
                    yield return pipeline.SceneObjects.Convert(entry.Id);

                    break;

                case ContentKind.Map:
                    yield return pipeline.Maps.Convert(entry.Name);

                    break;

                case ContentKind.Table:
                    yield return Table(entry, pipeline);

                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(entry), entry.Kind, null);
            }
        }

        /// <summary>
        /// The scene objects a converted map places, one unit each. A client
        /// holds thousands of them and a map names a handful, so these are the
        /// ones worth converting along with the map - the rest would be a
        /// family the map never mentions.
        /// </summary>
        internal static IEnumerable<UnitResult> Objects(MapResult map, ConversionPipeline pipeline,
            IProgress<ConversionProgress> progress = null)
        {
            var index = 0;

            foreach (var id in map.Objects)
            {
                progress?.Report(new ConversionProgress($"scene object {id}", ++index, map.Objects.Count));

                yield return pipeline.SceneObjects.Convert(id);
            }
        }

        private static TableResult Table(CatalogEntry entry, ConversionPipeline pipeline)
        {
            var unit = pipeline.Tables.Units
                .FirstOrDefault(candidate => candidate.Name == entry.Name);

            if (unit == null)
            {
                Log.Error($"no table unit named '{entry.Name}'");

                return new TableResult(entry.Name, ConversionOutcome.Failed);
            }

            return pipeline.Tables.Convert(unit);
        }
    }
}

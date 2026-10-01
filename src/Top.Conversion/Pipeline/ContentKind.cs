using System;
using System.Collections.Generic;

namespace Top.Conversion.Pipeline
{
    /// <summary>
    /// The content families the command line converts. Conversion output is
    /// laid out under these names.
    /// </summary>
    public static class ContentKind
    {
        public const string Character = "character";

        public const string Item = "item";

        public const string Scene = "scene";

        public const string Table = "table";

        public const string Map = "map";

        private static readonly IReadOnlyList<string> Nothing = Array.Empty<string>();

        private static readonly IReadOnlyList<string> Tables = new[] { Table };

        /// <summary>
        /// The kinds a converted tree must also hold before this one can be
        /// read. A reader opens the client's table set before it touches
        /// anything else, so a map converted on its own produces a tree that
        /// loads nowhere: the terrain and its textures are there, but nothing
        /// can name the map or place its objects.
        /// </summary>
        public static IReadOnlyList<string> Requires(string kind)
        {
            return kind == Map ? Tables : Nothing;
        }

        /// <summary>
        /// A run's kinds with what they require filled in, keeping the given
        /// order and putting each requirement right after the kind that needs
        /// it. A kind already named is never added twice.
        /// </summary>
        public static IReadOnlyList<string> Expand(IEnumerable<string> kinds)
        {
            var expanded = new List<string>();

            foreach (var kind in kinds)
            {
                if (!expanded.Contains(kind))
                {
                    expanded.Add(kind);
                }

                foreach (var required in Requires(kind))
                {
                    if (expanded.Contains(required))
                    {
                        continue;
                    }

                    expanded.Add(required);
                }
            }

            return expanded;
        }
    }
}

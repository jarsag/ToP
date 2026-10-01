using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Top.Conversion.Pipeline
{
    /// <summary>
    /// The original client's own root: the folder whose model, animation, map
    /// and scripts/table children the pipeline reads. It is not the folder a
    /// player launches - a distribution keeps the assets one level down, in a
    /// Client folder - so a guess has to probe for the assets rather than
    /// trust the name.
    /// </summary>
    public static class ClientRoot
    {
        /// <summary>
        /// How many folders a guess looks in around the one it starts from.
        /// </summary>
        public const int MaxCandidates = 12;

        /// <summary>
        /// Whether a folder holds a client's assets. scripts/table is the
        /// marker: no distribution ships the pipeline's tables without also
        /// shipping models or maps, and no source checkout has scripts/table
        /// beside them.
        /// </summary>
        public static bool Looks(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return false;
            }

            var source = new SourcePaths(path);

            return Directory.Exists(source.Tables) &&
                   (Directory.Exists(source.Models) || Directory.Exists(source.Maps));
        }

        /// <summary>
        /// The client roots near a folder, nearest first: the folder itself,
        /// its Client and reference/assets children, then the same again one
        /// level up and one level down. A client checked out beside the
        /// project is what this is for.
        /// </summary>
        public static IReadOnlyList<string> Candidates(string near)
        {
            var found = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var guess in Guesses(near))
            {
                if (found.Count == MaxCandidates)
                {
                    break;
                }

                var full = Full(guess);

                if (Looks(full) && seen.Add(full))
                {
                    found.Add(full);
                }
            }

            return found;
        }

        /// <summary>
        /// A one-line description of a client root for a chooser: how many
        /// maps, characters and items it actually holds. Reading the tables
        /// here would cost more than the count is worth, so it counts files.
        /// </summary>
        public static string Describe(string path)
        {
            var source = new SourcePaths(path);

            return $"{Count(source.Maps, "*.map")} maps, " +
                   $"{Count(source.ModelDir("character"), "*.lgo")} character parts, " +
                   $"{Count(source.ModelDir("item"), "*.lgo")} item modules";
        }

        private static IEnumerable<string> Guesses(string near)
        {
            var root = Full(near);
            var parent = Path.GetDirectoryName(root);

            foreach (var candidate in Around(root))
            {
                yield return candidate;
            }

            if (string.IsNullOrEmpty(parent))
            {
                yield break;
            }

            foreach (var candidate in Around(parent))
            {
                yield return candidate;
            }
        }

        private static IEnumerable<string> Around(string folder)
        {
            yield return folder;

            if (!Directory.Exists(folder))
            {
                yield break;
            }

            yield return Path.Combine(folder, "Client");
            yield return Path.Combine(folder, "reference", "assets");

            string[] children;

            try
            {
                children = Directory.GetDirectories(folder);
            }
            catch (IOException)
            {
                yield break;
            }
            catch (UnauthorizedAccessException)
            {
                yield break;
            }

            foreach (var child in children.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            {
                yield return child;
                yield return Path.Combine(child, "Client");
            }
        }

        private static string Full(string path)
        {
            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException
                                                  or PathTooLongException)
            {
                return path;
            }
        }

        private static int Count(string folder, string pattern)
        {
            try
            {
                return Directory.Exists(folder) ? Directory.GetFiles(folder, pattern).Length : 0;
            }
            catch (IOException)
            {
                return 0;
            }
            catch (UnauthorizedAccessException)
            {
                return 0;
            }
        }
    }
}

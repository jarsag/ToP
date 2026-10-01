using System;
using System.Collections.Generic;
using System.Linq;
using Top.Conversion.Pipeline;

namespace Top.Conversion.Cli
{
    /// <summary>
    /// Encapsulates command line arguments for the Top.Conversion.Cli application.
    /// </summary>
    internal class Arguments
    {
        public const string DefaultSource = "reference/assets";

        public const string DefaultOutput = "artifacts/content";

        private static readonly string[] EveryKind =
        [
            ContentKind.Character, ContentKind.Item, ContentKind.Scene, ContentKind.Table, ContentKind.Map
        ];

        public static readonly string Usage = $"""
            usage: Top.Conversion.Cli [--source <dir>] [--out <dir>] [--kinds <list>] [--pick|--no-pick]

              --source <dir>  original client root, default {DefaultSource}
              --out <dir>     converted tree root, default {DefaultOutput}
              --kinds <list>  comma separated, any of {string.Join(", ", EveryKind)}. Default all
              --pick          ask which client, category and unit to convert
              --no-pick       never ask, even with no other argument
              --help, -h      this text

            With no argument at all this asks, so a client can be browsed without
            knowing its layout. Naming a client, an output or a kind converts
            straight away instead. Relative paths resolve against the current
            directory. A run overwrites what is already in the tree - delete the
            tree root first for a clean one.

            A reader opens the client's tables before anything else, so naming
            map converts the table family too: without it the tree holds a map
            that nothing can name. A map's placed objects additionally need the
            scene family - without it only the terrain shows.

            """;

        private Arguments(string source, string output, IReadOnlyList<string> kinds, bool? pick)
        {
            Source = source;
            Output = output;
            Kinds = kinds;
            Pick = pick;
        }

        public string Source { get; }

        public string Output { get; }

        public IReadOnlyList<string> Kinds { get; }

        /// <summary>
        /// Whether to run the wizard: true when asked for, false when refused,
        /// null when the command line does not say and the default applies.
        /// </summary>
        public bool? Pick { get; }

        public static bool TryParse(IReadOnlyList<string> args, out Arguments arguments, out string error)
        {
            var source = DefaultSource;
            var output = DefaultOutput;
            IReadOnlyList<string> kinds = EveryKind;
            bool? pick = null;

            arguments = null;
            error = null;

            for (var i = 0; i < args.Count; i++)
            {
                var name = args[i];

                switch (name)
                {
                    case "--source":
                        if (!TryValue(args, name, ref i, out source, out error))
                        {
                            return false;
                        }

                        break;

                    case "--out":
                        if (!TryValue(args, name, ref i, out output, out error))
                        {
                            return false;
                        }

                        break;

                    case "--kinds":
                        if (!TryValue(args, name, ref i, out var list, out error) ||
                            !TryKinds(list, out kinds, out error))
                        {
                            return false;
                        }

                        break;

                    case "--pick":
                        pick = true;

                        break;

                    case "--no-pick":
                        pick = false;

                        break;

                    default:
                        error = $"unknown argument '{name}'";

                        return false;
                }
            }

            arguments = new Arguments(source, output, kinds, pick);

            return true;
        }

        private static bool TryValue(IReadOnlyList<string> args, string name, ref int i, out string value,
            out string error)
        {
            if (i + 1 >= args.Count)
            {
                value = null;
                error = $"{name} needs a value";

                return false;
            }

            value = args[++i];
            error = null;

            return true;
        }

        private static bool TryKinds(string list, out IReadOnlyList<string> kinds, out string error)
        {
            var named = list.Split(',')
                .Select(kind => kind.Trim().ToLowerInvariant())
                .Where(kind => kind.Length > 0)
                .Distinct()
                .ToList();

            kinds = named;
            error = null;

            if (named.Count == 0)
            {
                error = "--kinds needs at least one kind";

                return false;
            }

            var unknown = named.FirstOrDefault(kind => !EveryKind.Contains(kind, StringComparer.Ordinal));

            if (unknown == null)
            {
                return true;
            }

            error = $"unknown kind '{unknown}', expected any of {string.Join(", ", EveryKind)}";

            return false;
        }
    }
}

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

        /// <summary>
        /// What --list can be asked for, as constants so the run, the help text
        /// and the Unity window cannot drift apart.
        /// </summary>
        public const string ListClients = "clients";

        public const string ListCatalog = "catalog";

        /// <summary>
        /// The listing that names one unit: a converted map's spawn points,
        /// which belong to a map rather than to a whole client.
        /// </summary>
        public const string ListSpawns = "spawns";

        private static readonly string[] EveryKind =
        [
            ContentKind.Character, ContentKind.Item, ContentKind.Scene, ContentKind.Table, ContentKind.Map
        ];

        public static readonly string Usage = $"""
            usage: Top.Conversion.Cli [--source <dir>] [--out <dir>] [--kinds <list>] [--pick|--no-pick]
                                      [--kind <kind> --unit <name|id>] [--list clients|catalog] [--near <dir>] [--json]

              --source <dir>  original client root, default {DefaultSource}
              --out <dir>     converted tree root, default {DefaultOutput}
              --kinds <list>  comma separated, any of {string.Join(", ", EveryKind)}. Default all
              --kind <kind>   one family, with --unit, to convert a single unit of it
              --unit <name|id> the one unit of --kind: an id where the family numbers
                              its units, a name otherwise - a map answers to both its
                              mapinfo id and the name of its file
              --list <what>   clients (the roots near --near), catalog (what --source
                              offers) or spawns (where a converted map says a hero
                              can stand, with --kind map --unit <name|id>)
              --near <dir>    where --list clients looks, default the current directory
              --json          print --list as JSON, the shape the Unity window reads
              --no-objects    convert a map without the objects standing on it. By
                              default a map brings them: the scene loads a map by
                              its id and draws what the map places on it, so a map
                              converted without them shows bare ground
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

        private Arguments(string source, string output, IReadOnlyList<string> kinds, bool? pick,
            string kind, string unit, string list, string near, bool json, bool objects)
        {
            Source = source;
            Output = output;
            Kinds = kinds;
            Pick = pick;
            Kind = kind;
            Unit = unit;
            List = list;
            Near = near;
            Json = json;
            Objects = objects;
        }

        public string Source { get; }

        public string Output { get; }

        public IReadOnlyList<string> Kinds { get; }

        /// <summary>
        /// The one family --unit names a unit of, null when the run converts
        /// whole families instead.
        /// </summary>
        public string Kind { get; }

        /// <summary>
        /// The one unit of <see cref="Kind"/> to convert, by id where the
        /// family numbers its units and by name where it does not.
        /// </summary>
        public string Unit { get; }

        /// <summary>
        /// What --list was asked for - "clients" or "catalog" - or null for a
        /// run that converts.
        /// </summary>
        public string List { get; }

        /// <summary>
        /// Where --list clients looks for a client root.
        /// </summary>
        public string Near { get; }

        /// <summary>
        /// Whether --list prints JSON for a caller to read rather than lines
        /// for a person.
        /// </summary>
        public bool Json { get; }

        /// <summary>
        /// Whether a map brings the scene objects standing on it along. True
        /// unless --no-objects says otherwise.
        /// </summary>
        public bool Objects { get; }

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
            string kind = null;
            string unit = null;
            string list = null;
            var near = ".";
            var json = false;
            var objects = true;

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
                        if (!TryValue(args, name, ref i, out var names, out error) ||
                            !TryKinds(names, out kinds, out error))
                        {
                            return false;
                        }

                        break;

                    case "--kind":
                        if (!TryValue(args, name, ref i, out kind, out error))
                        {
                            return false;
                        }

                        if (!ContentKind.IsKnown(kind))
                        {
                            error = $"unknown kind '{kind}', expected any of " +
                                    string.Join(", ", ContentKind.Everything);

                            return false;
                        }

                        break;

                    case "--unit":
                        if (!TryValue(args, name, ref i, out unit, out error))
                        {
                            return false;
                        }

                        break;

                    case "--list":
                        if (!TryValue(args, name, ref i, out var asked, out error) ||
                            !TryList(asked, out list, out error))
                        {
                            return false;
                        }

                        break;

                    case "--near":
                        if (!TryValue(args, name, ref i, out near, out error))
                        {
                            return false;
                        }

                        break;

                    case "--json":
                        json = true;

                        break;

                    case "--no-objects":
                        objects = false;

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

            // A listing that names a unit says what it needs itself, before the
            // rules that speak for a conversion.
            if (list == ListSpawns)
            {
                if (unit == null || kind == null)
                {
                    error = $"--list {ListSpawns} needs --kind {ContentKind.Map} --unit <name|id>";

                    return false;
                }

                if (kind != ContentKind.Map)
                {
                    error = $"--list {ListSpawns} only lists maps";

                    return false;
                }
            }
            else if (list != null && unit != null)
            {
                error = "--list only lists; drop --kind and --unit";

                return false;
            }

            // One unit is named by a family and a unit together: a family alone
            // is what --kinds is for, and a unit alone has nothing to look in.
            if (unit != null && kind == null)
            {
                error = "--unit needs --kind";

                return false;
            }

            if (kind != null && unit == null)
            {
                error = "--kind needs --unit; use --kinds to convert a whole family";

                return false;
            }

            if (json && list == null)
            {
                error = "--json needs --list";

                return false;
            }

            if (string.IsNullOrWhiteSpace(near))
            {
                near = ".";
            }

            arguments = new Arguments(source, output, kinds, pick, kind, unit, list, near, json, objects);

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

        /// <summary>
        /// The two things --list knows how to list. Anything else is refused
        /// here rather than by an empty printout.
        /// </summary>
        private static bool TryList(string what, out string list, out string error)
        {
            list = what == null ? string.Empty : what.Trim().ToLowerInvariant();
            error = null;

            if (list == ListClients || list == ListCatalog || list == ListSpawns)
            {
                return true;
            }

            error = $"unknown list '{what}', expected {ListClients}, {ListCatalog} or {ListSpawns}";

            return false;
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

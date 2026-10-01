using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Top.Conversion.Pipeline;
using Top.Logging;

namespace Top.Conversion.Cli
{
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    internal static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Contains("--help") || args.Contains("-h"))
            {
                Console.Write(Arguments.Usage);

                return 0;
            }

            if (!Arguments.TryParse(args, out var arguments, out var error))
            {
                Console.Error.WriteLine(error);
                Console.Error.WriteLine();
                Console.Error.Write(Arguments.Usage);

                return 2;
            }

            Log.Writer = new ConsoleLog();

            // Listing answers a caller rather than a person, so it never asks
            // and never needs a client root to exist: what a missing client
            // offers is exactly what the listing is there to say.
            if (arguments.List != null)
            {
                return List(arguments);
            }

            // Asking is the default only when nothing at all was asked for and
            // somebody is there to answer: an empty command line on a terminal
            // opens the wizard, and the same command line in a script keeps
            // converting the whole default tree as it always did.
            var interactive = arguments.Pick ?? (args.Length == 0 && ConsolePrompt.CanPrompt);

            if (interactive)
            {
                var prompt = new ConsolePrompt(Console.In, Console.Out);

                return new Wizard(prompt, Directory.GetCurrentDirectory(), arguments.Output, arguments.Objects)
                    .Run();
            }

            if (!Directory.Exists(arguments.Source))
            {
                Console.Error.WriteLine($"no client root at '{Path.GetFullPath(arguments.Source)}'");

                return 2;
            }

            if (arguments.Kind != null)
            {
                return Report(ConvertOne(arguments));
            }

            return Report(Convert(arguments));
        }

        /// <summary>
        /// Prints what a client root near a folder looks like, or what one
        /// client offers, for a caller that has no terminal: lines for a
        /// person, JSON for the Unity editor window.
        /// </summary>
        private static int List(Arguments arguments)
        {
            if (arguments.List == Arguments.ListClients)
            {
                var roots = ClientRoot.Candidates(arguments.Near);

                if (arguments.Json)
                {
                    Console.WriteLine(CatalogReport.Roots(roots));

                    return 0;
                }

                foreach (var root in roots)
                {
                    Console.WriteLine($"{root}  ({ClientRoot.Describe(root)})");
                }

                if (roots.Count == 0)
                {
                    Console.WriteLine($"no client root found near {Path.GetFullPath(arguments.Near)}");
                }

                return 0;
            }

            var settings = new ConversionSettings(arguments.Source, arguments.Output);

            if (arguments.List == Arguments.ListSpawns)
            {
                var spawns = SpawnReport.Read(settings, arguments.Unit);

                if (spawns == null)
                {
                    Console.Error.WriteLine($"the map table in '{Path.GetFullPath(arguments.Output)}' " +
                                            $"names no map '{arguments.Unit}'");

                    return 1;
                }

                if (arguments.Json)
                {
                    Console.WriteLine(SpawnReport.Json(spawns));

                    return 0;
                }

                foreach (var line in SpawnReport.Lines(spawns))
                {
                    Console.WriteLine(line);
                }

                return spawns.Points.Count > 0 ? 0 : 1;
            }

            var pipeline = new ConversionPipeline(settings);
            var catalog = new ContentCatalog(settings, pipeline.ClientTables, pipeline.Tables.Units);

            if (arguments.Json)
            {
                Console.WriteLine(CatalogReport.Catalog(arguments.Source, catalog));

                return 0;
            }

            foreach (var section in catalog.Sections)
            {
                Console.WriteLine($"  {section.Summary}");
            }

            return 0;
        }

        /// <summary>
        /// Converts the one unit --kind and --unit name, plus whatever that
        /// unit cannot be read without - the same bargain the wizard makes, for
        /// a caller that already knows what it wants.
        /// </summary>
        private static IReadOnlyList<KindRun> ConvertOne(Arguments arguments)
        {
            var settings = new ConversionSettings(arguments.Source, arguments.Output, overwrite: true);
            var pipeline = new ConversionPipeline(settings);
            var catalog = new ContentCatalog(settings, pipeline.ClientTables, pipeline.Tables.Units);

            Console.WriteLine($"from {Path.GetFullPath(arguments.Source)}");
            Console.WriteLine($"to   {Path.GetFullPath(arguments.Output)}");

            if (!catalog.TryFind(arguments.Kind, arguments.Unit, out var entry, out var trouble))
            {
                Log.Error(trouble);

                return new[] { new KindRun(arguments.Kind) { Trouble = trouble } };
            }

            Console.WriteLine();

            var runs = new List<KindRun>(EntryRun.Convert(entry, pipeline, arguments.Objects));

            foreach (var run in runs)
            {
                Console.WriteLine(run.Summary);
            }

            foreach (var required in ContentKind.Requires(entry.Kind))
            {
                Console.WriteLine($"a {entry.Kind} cannot be read without {required}, converting that too");

                var needed = new KindRun(required);

                runs.Add(needed);

                Console.WriteLine();

                foreach (var result in Kinds.All(required, pipeline, needed))
                {
                    needed.Add(result);
                }

                Console.WriteLine(needed.Summary);
            }

            return runs;
        }

        private static IReadOnlyList<KindRun> Convert(Arguments arguments)
        {
            var settings = new ConversionSettings(arguments.Source, arguments.Output, overwrite: true);
            var pipeline = new ConversionPipeline(settings);
            var runs = new List<KindRun>();

            Console.WriteLine($"from {Path.GetFullPath(arguments.Source)}");
            Console.WriteLine($"to   {Path.GetFullPath(arguments.Output)}");

            var kinds = ContentKind.Expand(arguments.Kinds);

            // A map brings the objects standing on it. Asking for the scene
            // family as well means this run already converts them, so the two
            // would only write the same units twice.
            var placed = arguments.Objects && arguments.Kinds.Contains(ContentKind.Map) &&
                         !arguments.Kinds.Contains(ContentKind.Scene)
                ? new KindRun(ContentKind.Scene)
                : null;

            foreach (var kind in arguments.Kinds)
            {
                foreach (var required in ContentKind.Requires(kind).Where(needed =>
                             !arguments.Kinds.Contains(needed)))
                {
                    Console.WriteLine($"note: {kind} cannot be read without {required}, converting it too");
                }
            }

            foreach (var kind in kinds)
            {
                var run = new KindRun(kind);

                runs.Add(run);

                // Right after the maps it belongs to, so the summary reads as
                // the work the run did.
                if (placed != null && kind == ContentKind.Map)
                {
                    runs.Add(placed);
                }

                Console.WriteLine();

                try
                {
                    foreach (var result in Kinds.All(kind, pipeline, run))
                    {
                        run.Add(result);

                        if (placed == null || result is not MapResult map || map.Objects.Count == 0)
                        {
                            continue;
                        }

                        foreach (var placedResult in Kinds.Objects(map, pipeline, placed))
                        {
                            placed.Add(placedResult);
                        }
                    }
                }
                catch (Exception exception)
                {
                    Log.Error($"{kind} stopped", exception);

                    run.Trouble = "stopped early";
                }

                if (run.Total == 0 && run.Trouble == null)
                {
                    Log.Error($"no {kind} units to convert - " +
                              $"is '{Path.GetFullPath(arguments.Source)}' the client root?");

                    run.Trouble = "no units";
                }

                Console.WriteLine(run.Summary);
            }

            return runs;
        }

        private static int Report(IReadOnlyList<KindRun> runs)
        {
            var failed = runs.Sum(run => run.Failed);

            Console.WriteLine();
            Console.WriteLine("summary");

            foreach (var run in runs)
            {
                Console.WriteLine($"  {run.Summary}");
            }

            if (failed > 0)
            {
                Console.WriteLine($"  {failed} failed in all");
            }

            return failed > 0 || runs.Any(run => run.Trouble != null) ? 1 : 0;
        }
    }
}

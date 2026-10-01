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

            // Asking is the default only when nothing at all was asked for and
            // somebody is there to answer: an empty command line on a terminal
            // opens the wizard, and the same command line in a script keeps
            // converting the whole default tree as it always did.
            var interactive = arguments.Pick ?? (args.Length == 0 && ConsolePrompt.CanPrompt);

            if (interactive)
            {
                var prompt = new ConsolePrompt(Console.In, Console.Out);

                return new Wizard(prompt, Directory.GetCurrentDirectory(), arguments.Output).Run();
            }

            if (!Directory.Exists(arguments.Source))
            {
                Console.Error.WriteLine($"no client root at '{Path.GetFullPath(arguments.Source)}'");

                return 2;
            }

            return Report(Convert(arguments));
        }

        private static IReadOnlyList<KindRun> Convert(Arguments arguments)
        {
            var settings = new ConversionSettings(arguments.Source, arguments.Output, overwrite: true);
            var pipeline = new ConversionPipeline(settings);
            var runs = new List<KindRun>();

            Console.WriteLine($"from {Path.GetFullPath(arguments.Source)}");
            Console.WriteLine($"to   {Path.GetFullPath(arguments.Output)}");

            var kinds = ContentKind.Expand(arguments.Kinds);

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

                Console.WriteLine();

                try
                {
                    foreach (var result in Kinds.All(kind, pipeline, run))
                    {
                        run.Add(result);
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Top.Conversion.Pipeline;

namespace Top.Conversion.Cli
{
    /// <summary>
    /// The interactive run: pick a client, pick what to pull out of it, pick
    /// the one unit to pull, convert it, offer another. Each pick lists only
    /// what parsed, so an empty client shows as an unavailable family rather
    /// than as a run that converts nothing.
    /// </summary>
    internal class Wizard
    {
        /// <summary>
        /// How many units of the chosen family are on screen at once. The
        /// client has thousands of items, so a page has to stay readable and
        /// the filter is what gets you to the right one.
        /// </summary>
        internal const int PageSize = 25;

        private readonly ConsolePrompt _prompt;
        private readonly string _near;
        private readonly string _output;

        /// <summary>
        /// The kinds this session has already emitted in full, so a run that
        /// needs one does not write it again for every unit picked.
        /// </summary>
        private readonly HashSet<string> _complete = new HashSet<string>(StringComparer.Ordinal);

        private string _source;

        internal Wizard(ConsolePrompt prompt, string near, string output)
        {
            _prompt = prompt;
            _near = near;
            _output = output;
        }

        /// <summary>
        /// Runs until the user quits, and reports whether anything failed.
        /// </summary>
        internal int Run()
        {
            _prompt.Line("Tales of Pirates converter");
            _prompt.Line();

            var source = AskSource();

            if (source == null)
            {
                return 0;
            }

            _source = source;

            var output = AskOutput();

            if (output == null)
            {
                return 0;
            }

            // One pipeline for the whole session: a rig or a module two units
            // share is built once, and what already converted reads as
            // skipped, exactly as it would in a single batch.
            var settings = new ConversionSettings(source, output, overwrite: true);
            var pipeline = new ConversionPipeline(settings);
            var catalog = new ContentCatalog(settings, pipeline.ClientTables, pipeline.Tables.Units);

            var failed = 0;

            while (true)
            {
                var section = AskSection(catalog);

                if (section == null)
                {
                    break;
                }

                // A reader opens the client's tables as one set, so a single
                // one of them is as unreadable as none: this family converts
                // whole rather than offering a choice that cannot work.
                if (section.Kind == ContentKind.Table)
                {
                    _prompt.Line();
                    _prompt.Line($"converting all {section.Entries.Count} tables, " +
                                 "which a reader opens as one set");

                    failed += ConvertWhole(section.Kind, pipeline);
                }
                else
                {
                    var step = AskEntry(catalog, section, out var entry);

                    if (step == Step.Quit)
                    {
                        break;
                    }

                    if (step == Step.Back)
                    {
                        continue;
                    }

                    _prompt.Line();
                    _prompt.Line($"converting {entry.Kind} '{Named(entry)}' to {Full(output)}");

                    failed += Convert(entry, pipeline);
                    failed += ConvertNeeds(entry.Kind, pipeline);
                }

                if (!AskAgain())
                {
                    break;
                }
            }

            _prompt.Line();
            _prompt.Line(failed > 0 ? $"{failed} failed" : "done");

            return failed > 0 ? 1 : 0;
        }

        private string AskSource()
        {
            var candidates = ClientRoot.Candidates(_near);
            var options = candidates
                .Select(path => $"{path}  ({ClientRoot.Describe(path)})")
                .ToList();
            var typed = options.Count;

            options.Add("another path");

            if (candidates.Count == 0)
            {
                _prompt.Line($"no client root found near {Full(_near)}");
            }

            while (true)
            {
                _prompt.Line();
                _prompt.Line("client root");

                var index = _prompt.Choose("client", options);

                if (index == null)
                {
                    return null;
                }

                if (index != typed)
                {
                    return candidates[index.Value];
                }

                var answer = _prompt.Ask("path");

                if (answer == null)
                {
                    return null;
                }

                if (answer.Length > 0 && ClientRoot.Looks(answer))
                {
                    return Full(answer);
                }

                _prompt.Line($"'{answer}' is not a client root: no scripts/table " +
                             "beside a model or map folder");
            }
        }

        private string AskOutput()
        {
            var answer = _prompt.Ask("output root", _output);

            return answer == null || answer.Length == 0 ? _output : answer;
        }

        private CatalogSection AskSection(ContentCatalog catalog)
        {
            var sections = catalog.Sections;
            var available = sections.Where(section => section.Available).ToList();

            _prompt.Line();

            foreach (var section in sections.Where(section => !section.Available))
            {
                _prompt.Line($"  {section.Summary}");
            }

            if (available.Count == 0)
            {
                _prompt.Line($"nothing to convert in {Full(_source)}");

                return null;
            }

            _prompt.Line("convert what");

            var index = _prompt.Choose("category", available.Select(section => section.Summary).ToList());

            return index == null ? null : available[index.Value];
        }

        private Step AskEntry(ContentCatalog catalog, CatalogSection section, out CatalogEntry entry)
        {
            entry = null;

            var filter = string.Empty;
            var page = 0;

            while (true)
            {
                var matches = catalog.Matching(section.Kind, filter);
                var pages = Math.Max(1, (matches.Count + PageSize - 1) / PageSize);

                page = Math.Clamp(page, 0, pages - 1);

                var window = matches.Skip(page * PageSize).Take(PageSize).ToList();

                _prompt.Line();
                _prompt.Line(filter.Length == 0
                    ? $"{section.Kind}: {matches.Count}"
                    : $"{section.Kind}: {matches.Count} of {section.Entries.Count} matching '{filter}'");

                if (window.Count == 0)
                {
                    _prompt.Line("  nothing matches");
                }

                for (var i = 0; i < window.Count; i++)
                {
                    _prompt.Line($"  {i + 1,3}) {window[i].Label}");
                }

                if (pages > 1)
                {
                    _prompt.Line($"  page {page + 1} of {pages}   (n)ext  (p)revious");
                }

                _prompt.Line("  (f)ilter  (b)ack  (q)uit");

                var answer = _prompt.Ask("number");

                if (answer == null)
                {
                    return Step.Quit;
                }

                switch (answer.ToLowerInvariant())
                {
                    case "n":
                        page++;

                        continue;

                    case "p":
                        page--;

                        continue;

                    case "f":
                        var typed = _prompt.Ask("filter", filter.Length == 0 ? "all" : filter);

                        if (typed == null)
                        {
                            return Step.Quit;
                        }

                        filter = typed.Equals("all", StringComparison.OrdinalIgnoreCase)
                            ? string.Empty
                            : typed;
                        page = 0;

                        continue;

                    case "b":
                        return Step.Back;

                    case "q":
                        return Step.Quit;
                }

                if (int.TryParse(answer, out var number) && number >= 1 && number <= window.Count)
                {
                    entry = window[number - 1];

                    return Step.Entry;
                }

                _prompt.Line(window.Count > 0
                    ? $"enter 1 to {window.Count}, or n, p, f, b, q"
                    : "enter f to widen the filter, or b, q");
            }
        }

        /// <summary>
        /// The one unit a catalog entry names, plus whatever that unit cannot
        /// be read without.
        /// </summary>
        private int Convert(CatalogEntry entry, ConversionPipeline pipeline)
        {
            var run = new KindRun(entry.Kind);

            foreach (var result in Kinds.One(entry, pipeline))
            {
                run.Add(result);
            }

            _prompt.Line(run.Summary);

            return run.Failed;
        }

        /// <summary>
        /// Every unit of a kind, remembered as done so a later pick that needs
        /// it does not write it again.
        /// </summary>
        private int ConvertWhole(string kind, ConversionPipeline pipeline)
        {
            _complete.Add(kind);

            var run = new KindRun(kind);

            foreach (var result in Kinds.All(kind, pipeline, run))
            {
                run.Add(result);
            }

            _prompt.Line(run.Summary);

            return run.Failed;
        }

        /// <summary>
        /// The kinds the one just converted cannot be read without. Emitting
        /// a tree a reader chokes on is worse than the extra work, so these
        /// come along whether or not they were asked for.
        /// </summary>
        private int ConvertNeeds(string kind, ConversionPipeline pipeline)
        {
            var failed = 0;

            foreach (var required in ContentKind.Requires(kind))
            {
                if (_complete.Contains(required))
                {
                    continue;
                }

                _prompt.Line($"a {kind} cannot be read without {required}, converting that too");

                failed += ConvertWhole(required, pipeline);
            }

            return failed;
        }

        private bool AskAgain()
        {
            var answer = _prompt.Ask("convert another? (y/n)", "y");

            return answer != null && answer.Equals("y", StringComparison.OrdinalIgnoreCase);
        }

        private static string Named(CatalogEntry entry)
        {
            return string.IsNullOrEmpty(entry.Name) ? entry.Label : entry.Name;
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

        private enum Step
        {
            Entry,

            Back,

            Quit
        }
    }
}

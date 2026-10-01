using System;
using System.Collections.Generic;
using System.IO;

namespace Top.Conversion.Cli
{
    /// <summary>
    /// The wizard's whole view of the terminal: write a line, read a line.
    /// The reader and writer are injected so a session can be scripted from
    /// a string and checked without a terminal in front of it. Every read
    /// reports end of input as null, and every caller treats that as a quit,
    /// so a wizard that runs out of input stops instead of looping.
    /// </summary>
    internal class ConsolePrompt
    {
        private readonly TextReader _reader;
        private readonly TextWriter _writer;

        internal ConsolePrompt(TextReader reader, TextWriter writer)
        {
            _reader = reader;
            _writer = writer;
        }

        /// <summary>
        /// A wizard driven by a pipe has nobody to answer it, so it is not
        /// offered unless the terminal is a real one.
        /// </summary>
        internal static bool CanPrompt
        {
            get
            {
                try
                {
                    return !Console.IsInputRedirected && !Console.IsOutputRedirected;
                }
                catch (IOException)
                {
                    return false;
                }
            }
        }

        internal void Line(string text = "")
        {
            _writer.WriteLine(text);
        }

        /// <summary>
        /// Prints a question and reads the answer trimmed, or null at end of
        /// input. An empty answer comes back empty rather than as the shown
        /// default, so a caller can tell "take the default" from "no more
        /// input" - the two have to end differently.
        /// </summary>
        internal string Ask(string question, string shown = null)
        {
            _writer.Write(shown == null ? $"{question}: " : $"{question} [{shown}]: ");

            var answer = _reader.ReadLine();

            if (answer == null)
            {
                Line();

                return null;
            }

            return answer.Trim();
        }

        /// <summary>
        /// Prints numbered options and reads the chosen number, returned as a
        /// zero based index. A number outside the list re-asks, so a mistyped
        /// digit never picks the wrong thing. "q" and end of input are both a
        /// null, because every caller stops on either.
        /// </summary>
        internal int? Choose(string question, IReadOnlyList<string> options)
        {
            for (var i = 0; i < options.Count; i++)
            {
                Line($"  {i + 1,3}) {options[i]}");
            }

            while (true)
            {
                var answer = Ask(question);

                if (answer == null || Quits(answer))
                {
                    return null;
                }

                if (int.TryParse(answer, out var number) && number >= 1 && number <= options.Count)
                {
                    return number - 1;
                }

                Line($"enter 1 to {options.Count}, or q to quit");
            }
        }

        internal static bool Quits(string answer)
        {
            return answer.Equals("q", StringComparison.OrdinalIgnoreCase) ||
                   answer.Equals("quit", StringComparison.OrdinalIgnoreCase);
        }
    }
}

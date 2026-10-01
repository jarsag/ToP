using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Top.Conversion.Pipeline
{
    /// <summary>
    /// The client guesses and the content catalog as JSON, for a caller with no
    /// terminal to read: the Unity editor window. The shape is fixed - Pascal
    /// case keys, every string and every list always present, even when empty -
    /// because Unity parses it with JsonUtility, which knows neither
    /// dictionaries nor polymorphism nor a field the writer left out.
    /// </summary>
    public static class CatalogReport
    {
        /// <summary>
        /// The client roots near a folder, each with the one line description a
        /// picker shows beside it. Counts here are file counts, so a chooser
        /// costs no table reads.
        /// </summary>
        public static string Roots(IReadOnlyList<string> roots)
        {
            return Write(new Payload
            {
                Roots = roots.Select(path => new RootInfo
                {
                    Path = path,
                    Description = ClientRoot.Describe(path)
                }).ToList()
            });
        }

        /// <summary>
        /// Every family a client offers, with the entries a picker lists and
        /// the reason a family offers nothing.
        /// </summary>
        public static string Catalog(string source, ContentCatalog catalog)
        {
            return Write(new Payload
            {
                Source = source,
                Sections = catalog.Sections.Select(Section).ToList()
            });
        }

        private static SectionInfo Section(CatalogSection section)
        {
            return new SectionInfo
            {
                Kind = section.Kind,
                Summary = section.Summary,
                Available = section.Available,
                Trouble = section.Trouble ?? string.Empty,
                Entries = section.Entries.Select(Entry).ToList()
            };
        }

        private static EntryInfo Entry(CatalogEntry entry)
        {
            return new EntryInfo
            {
                Id = entry.Id,
                Name = entry.Name ?? string.Empty,
                Detail = entry.Detail ?? string.Empty,
                Label = entry.Label
            };
        }

        private static string Write(Payload payload)
        {
            return JsonConvert.SerializeObject(payload, Formatting.Indented);
        }

        private class Payload
        {
            public string Source { get; set; } = string.Empty;

            public List<RootInfo> Roots { get; set; } = new List<RootInfo>();

            public List<SectionInfo> Sections { get; set; } = new List<SectionInfo>();
        }

        private class RootInfo
        {
            public string Path { get; set; }

            public string Description { get; set; }
        }

        private class SectionInfo
        {
            public string Kind { get; set; }

            public string Summary { get; set; }

            public bool Available { get; set; }

            public string Trouble { get; set; }

            public List<EntryInfo> Entries { get; set; }
        }

        private class EntryInfo
        {
            public int Id { get; set; }

            public string Name { get; set; }

            public string Detail { get; set; }

            public string Label { get; set; }
        }
    }
}

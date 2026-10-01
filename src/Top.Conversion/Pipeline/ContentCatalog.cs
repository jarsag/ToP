using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Top.Conversion.Pipeline.Tables;
using Top.Legacy.Tables.Custom;
using Top.Legacy.Tables.Records;

namespace Top.Conversion.Pipeline
{
    /// <summary>
    /// One thing a client offers to convert: a characterinfo row, an iteminfo
    /// row, a sceneobjinfo row, a .map on disk or one of the emitted tables.
    /// </summary>
    public class CatalogEntry
    {
        public CatalogEntry(string kind, int id, string name, string detail)
        {
            Kind = kind;
            Id = id;
            Name = name;
            Detail = detail;
        }

        public string Kind { get; }

        /// <summary>
        /// The row id, or 0 for a unit the client names rather than numbers.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// What the converters key the unit by: a row name, the stem of a
        /// .map file, or a table unit's name.
        /// </summary>
        public string Name { get; }

        public string Detail { get; }

        public string Label
        {
            get
            {
                var head = Id != 0
                    ? $"{Id,5}  {Title}"
                    : Title;

                return string.IsNullOrEmpty(Detail) ? head : $"{head}  -  {Detail}";
            }
        }

        private string Title => string.IsNullOrEmpty(Name) ? "(unnamed)" : Name;
    }

    /// <summary>
    /// What one content family of a client holds, and why it holds nothing
    /// when the client cannot supply it.
    /// </summary>
    public class CatalogSection
    {
        public CatalogSection(string kind, IReadOnlyList<CatalogEntry> entries, string trouble)
        {
            Kind = kind;
            Entries = entries;
            Trouble = trouble;
        }

        public string Kind { get; }

        public IReadOnlyList<CatalogEntry> Entries { get; }

        /// <summary>
        /// Why this family cannot be converted, null when it can.
        /// </summary>
        public string Trouble { get; }

        public bool Available => Trouble == null;

        public string Summary => Trouble == null ? $"{Kind} ({Entries.Count})" : $"{Kind}: {Trouble}";
    }

    /// <summary>
    /// Everything a client offers, read through the same tables and folders
    /// the converters use - so a unit listed here is a unit that parsed, and
    /// a table that failed to parse shows as an unavailable family instead of
    /// an empty list.
    /// </summary>
    public class ContentCatalog
    {
        private readonly ConversionSettings _settings;
        private readonly ClientTables _tables;
        private readonly IReadOnlyList<ITableUnit> _tableUnits;

        private readonly Lazy<CatalogSection> _characters;
        private readonly Lazy<CatalogSection> _items;
        private readonly Lazy<CatalogSection> _sceneObjects;
        private readonly Lazy<CatalogSection> _maps;
        private readonly Lazy<CatalogSection> _emittedTables;

        public ContentCatalog(ConversionSettings settings, ClientTables tables,
            IReadOnlyList<ITableUnit> tableUnits)
        {
            _settings = settings;
            _tables = tables;
            _tableUnits = tableUnits;

            _characters = new Lazy<CatalogSection>(Characters);
            _items = new Lazy<CatalogSection>(Items);
            _sceneObjects = new Lazy<CatalogSection>(SceneObjects);
            _maps = new Lazy<CatalogSection>(Maps);
            _emittedTables = new Lazy<CatalogSection>(Tables);
        }

        /// <summary>
        /// Every family, in the order a chooser offers them.
        /// </summary>
        public IReadOnlyList<CatalogSection> Sections =>
        [
            _characters.Value,
            _items.Value,
            _sceneObjects.Value,
            _maps.Value,
            _emittedTables.Value
        ];

        public CatalogSection Section(string kind)
        {
            return kind switch
            {
                ContentKind.Character => _characters.Value,
                ContentKind.Item => _items.Value,
                ContentKind.Scene => _sceneObjects.Value,
                ContentKind.Map => _maps.Value,
                ContentKind.Table => _emittedTables.Value,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };
        }

        /// <summary>
        /// A family's entries that match a filter: a substring of the name,
        /// the detail or the id, case insensitively. An empty filter keeps
        /// everything, which is what a chooser shows before one is typed.
        /// </summary>
        public IReadOnlyList<CatalogEntry> Matching(string kind, string filter)
        {
            var entries = Section(kind).Entries;

            return string.IsNullOrWhiteSpace(filter)
                ? entries
                : entries.Where(entry => Matches(entry, filter.Trim())).ToList();
        }

        public static bool Matches(CatalogEntry entry, string filter)
        {
            return Contains(entry.Name, filter, StringComparison.OrdinalIgnoreCase) ||
                   Contains(entry.Detail, filter, StringComparison.OrdinalIgnoreCase) ||
                   (entry.Id != 0 && Contains(entry.Id.ToString(CultureInfo.InvariantCulture), filter,
                       StringComparison.Ordinal));
        }

        private static bool Contains(string text, string filter, StringComparison comparison)
        {
            return text != null && text.Contains(filter, comparison);
        }

        /// <summary>
        /// The one entry a unit names: the id where the family numbers its
        /// units, the name without case where it does not. A map answers to
        /// both - its mapinfo id and the stem of its file - because the first
        /// is what the scene loads it by and the second is what its converter
        /// keys it by. A family that cannot be read at all reports its own
        /// trouble rather than an empty search.
        /// </summary>
        public bool TryFind(string kind, string unit, out CatalogEntry entry, out string trouble)
        {
            entry = null;

            if (!ContentKind.IsKnown(kind))
            {
                trouble = $"no family named '{kind}'";

                return false;
            }

            var section = Section(kind);

            if (!section.Available)
            {
                trouble = section.Trouble;

                return false;
            }

            var text = unit == null ? string.Empty : unit.Trim();

            if (text.Length == 0)
            {
                trouble = $"no {kind} named";

                return false;
            }

            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id != 0)
            {
                entry = section.Entries.FirstOrDefault(candidate => candidate.Id == id);

                if (entry != null)
                {
                    trouble = null;

                    return true;
                }
            }

            entry = section.Entries.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, text, StringComparison.OrdinalIgnoreCase));

            if (entry != null)
            {
                trouble = null;

                return true;
            }

            trouble = $"no {kind} named '{text}'";

            return false;
        }

        private CatalogSection Characters()
        {
            var path = _settings.Source.Table("characterinfo.txt");

            if (_tables.Characters == null)
            {
                return Missing(ContentKind.Character, path);
            }

            var entries = _tables.Characters
                .Select(row => new CatalogEntry(ContentKind.Character, row.Id, row.Name, Character(row)))
                .ToList();

            return Filled(ContentKind.Character, entries, path);
        }

        private static string Character(CharacterInfoRecord row)
        {
            var modal = row.ModalType switch
            {
                CharacterModalType.MainCharacter => "player",
                CharacterModalType.Boat => "boat",
                CharacterModalType.Employee => "employee",
                CharacterModalType.Other => "monster",
                _ => $"modal {row.ModalType}"
            };

            return $"{modal}, model {row.Model}";
        }

        private CatalogSection Items()
        {
            var path = _settings.Source.Table("iteminfo.txt");

            if (_tables.Items == null)
            {
                return Missing(ContentKind.Item, path);
            }

            var entries = _tables.Items
                .Select(row => new CatalogEntry(ContentKind.Item, row.Id, row.Name, Item(row)))
                .ToList();

            return Filled(ContentKind.Item, entries, path);
        }

        private static string Item(ItemInfoRecord row)
        {
            var worn = ItemModules.IsWearable(row.Type) ? "worn" : "held";

            return $"{row.Type}, {worn}";
        }

        private CatalogSection SceneObjects()
        {
            var path = _settings.Source.Table("sceneobjinfo.txt");

            if (_tables.SceneObjects == null)
            {
                return Missing(ContentKind.Scene, path);
            }

            // A row without a model file is one the converter has nothing to
            // do with, so it is not offered.
            var entries = _tables.SceneObjects
                .Where(row => !string.IsNullOrEmpty(row.Name))
                .Select(row => new CatalogEntry(ContentKind.Scene, row.Id, row.Name,
                    string.IsNullOrEmpty(row.DisplayName) ? $"type {row.Type}" : row.DisplayName))
                .ToList();

            return Filled(ContentKind.Scene, entries, path);
        }

        private CatalogSection Maps()
        {
            var folder = _settings.Source.Maps;

            if (!Directory.Exists(folder))
            {
                return new CatalogSection(ContentKind.Map, [], $"no folder at '{folder}'");
            }

            var entries = Directory.GetFiles(folder, "*.map")
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Select(Map)
                .ToList();

            return Filled(ContentKind.Map, entries, folder);
        }

        /// <summary>
        /// One .map file as an entry. The id is the mapinfo row's, because that
        /// is the number a client loads a map by - the preview in the scene
        /// carries the same one - while the name stays the stem of the file,
        /// which is the key the map converter takes.
        /// </summary>
        private CatalogEntry Map(string name)
        {
            var row = Row(name);

            return new CatalogEntry(ContentKind.Map, row?.Id ?? 0, name, Display(row));
        }

        private MapInfoRecord Row(string name)
        {
            return _tables.Maps?.FirstOrDefault(record =>
                string.Equals(record.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// What mapinfo calls a map, so a chooser shows "Ascaron" where the
        /// file is called "garner". The id leads the label, so it is not
        /// repeated here.
        /// </summary>
        private static string Display(MapInfoRecord row)
        {
            return string.IsNullOrEmpty(row?.DisplayName) ? null : row.DisplayName;
        }

        private CatalogSection Tables()
        {
            if (_tableUnits.Count == 0)
            {
                return new CatalogSection(ContentKind.Table, [], "no table units");
            }

            var entries = _tableUnits
                .Select(unit => new CatalogEntry(ContentKind.Table, 0, unit.Name, unit.Path))
                .ToList();

            return new CatalogSection(ContentKind.Table, entries, null);
        }

        private static CatalogSection Filled(string kind, IReadOnlyList<CatalogEntry> entries, string source)
        {
            return entries.Count > 0
                ? new CatalogSection(kind, entries, null)
                : new CatalogSection(kind, [], $"'{source}' holds nothing to convert");
        }

        private static CatalogSection Missing(string kind, string path)
        {
            return new CatalogSection(kind, [], $"no table at '{path}'");
        }
    }
}

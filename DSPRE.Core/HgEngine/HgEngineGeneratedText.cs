using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// Text archives hg-engine's datagen tools write line by line from one string field of each Species.c or
    /// Moves.c entry, line i from the entry for id i. Editing a main archive edits that field; the others are
    /// copies with a fixed transform (article, capitals, padding) and follow their main archive.
    /// </summary>
    public static class HgEngineGeneratedText
    {
        public sealed record Source(int Archive, string RelPath, string Header, string[] Field, int MainArchive)
        {
            public bool IsMain => Archive == MainArchive;
            public string FieldLabel => string.Join(".", Field);

            /// <summary>
            /// When an entry makes several lines, each the field with fixed wording around it: line i is entry
            /// i / Wraps.Length, wrapped by Wraps[i % Wraps.Length] and Suffix. Null for one plain line per entry.
            /// </summary>
            public string[] Wraps { get; init; }
            public string Suffix { get; init; } = "";

            public int LinesPerEntry => Wraps?.Length ?? 1;
        }

        private const string SpeciesC = "data/Species.c";
        private const string MovesC = "data/Moves.c";
        private const string SpeciesH = "include/constants/species.h";
        private const string MovesH = "include/constants/moves.h";

        private static readonly Source[] Sources =
        {
            new(237, SpeciesC, SpeciesH, new[] { "textData", "name" }, 237),
            new(238, SpeciesC, SpeciesH, new[] { "textData", "name" }, 237),
            new(817, SpeciesC, SpeciesH, new[] { "textData", "name" }, 237),
            new(803, SpeciesC, SpeciesH, new[] { "textData", "pokedexEntry" }, 803),
            new(816, SpeciesC, SpeciesH, new[] { "textData", "classification" }, 816),
            new(823, SpeciesC, SpeciesH, new[] { "textData", "classification" }, 816),
            new(814, SpeciesC, SpeciesH, new[] { "textData", "height" }, 814),
            new(815, SpeciesC, SpeciesH, new[] { "textData", "height" }, 814),
            new(812, SpeciesC, SpeciesH, new[] { "textData", "weight" }, 812),
            new(813, SpeciesC, SpeciesH, new[] { "textData", "weight" }, 812),
            new(750, MovesC, MovesH, new[] { "names", "name" }, 750),
            new(751, MovesC, MovesH, new[] { "names", "capsName" }, 751),
            new(749, MovesC, MovesH, new[] { "description" }, 749),
            // movedatagen writes three battle lines per move from fullName.
            new(3, MovesC, MovesH, new[] { "names", "fullName" }, 3)
            {
                Wraps = new[] { "{STRVAR_1, 1, 0, 0} used\\n", "The wild {STRVAR_1, 1, 0, 0} used\\n", "The opposing {STRVAR_1, 1, 0, 0} used\\n" },
                Suffix = "!",
            },
        };

        /// <summary>The field an archive is generated from, or null when it is not one of these.</summary>
        public static Source For(int archive) => Sources.FirstOrDefault(s => s.Archive == archive);

        /// <summary>Reads the field for every line, or null for a line whose entry or field the source lacks.</summary>
        public static bool TryReadLines(Source source, int count, out string[] lines, out string error)
        {
            lines = null;
            if (!TryLoad(source, out string text, out var byId, out error)) return false;

            lines = new string[count];
            var path = PathOf(source);
            for (int line = 0; line < count; line++)
            {
                if (!byId.TryGetValue(line / source.LinesPerEntry, out var entry)) continue;
                if (!ElementScanner.TryLocateValueSpan(text, entry.Open, entry.Close, path, out int vs, out int ve)) continue;
                string value = Unquote(text.Substring(vs, ve - vs));
                lines[line] = source.Wraps == null ? value : source.Wraps[line % source.LinesPerEntry] + value + source.Suffix;
            }
            return true;
        }

        /// <summary>Writes every changed line of a main archive back to its field. Nothing is written on failure.</summary>
        public static bool TryWriteLines(Source source, IReadOnlyList<string> lines, IReadOnlyList<string> before, out string error)
        {
            error = null;
            if (!source.IsMain) { error = $"Text archive {source.Archive} follows archive {source.MainArchive}; edit that one."; return false; }
            if (!TryLoad(source, out string text, out var byId, out error)) return false;

            var changed = new Dictionary<int, (string Designator, string Value)>();
            for (int line = 0; line < lines.Count; line++)
            {
                if (line < before.Count && lines[line] == before[line]) continue;
                int id = line / source.LinesPerEntry;
                if (!byId.TryGetValue(id, out var entry))
                {
                    error = $"{source.RelPath} has no entry for line {line}, so it cannot be edited here.";
                    return false;
                }
                string value = lines[line] ?? "";
                if (source.Wraps != null)
                {
                    string wrap = source.Wraps[line % source.LinesPerEntry];
                    if (!value.StartsWith(wrap, StringComparison.Ordinal) || !value.EndsWith(source.Suffix, StringComparison.Ordinal)
                        || value.Length < wrap.Length + source.Suffix.Length)
                    {
                        error = $"Line {line} is built from {source.FieldLabel} as \"{wrap}...{source.Suffix}\"; keep that wording and change only the name.";
                        return false;
                    }
                    value = value.Substring(wrap.Length, value.Length - wrap.Length - source.Suffix.Length);
                }
                if (changed.TryGetValue(id, out var other) && other.Value != value)
                {
                    error = $"Lines for {entry.Designator} give two different {source.FieldLabel} values; they all come from one field.";
                    return false;
                }
                changed[id] = (entry.Designator, value);
            }
            if (changed.Count == 0) return true;

            var path = PathOf(source);
            foreach (var (_, (designator, value)) in changed)
            {
                string literal = HgEngineTrainerSource.ToCStringLiteral(value);
                if (!HgEngineSourcePatcher.TryUpsertField(ref text, designator, path, literal))
                {
                    error = $"Could not write {source.FieldLabel} for {designator} in {source.RelPath}.";
                    return false;
                }
            }

            try { HgEngineFileCache.WriteText(FullPath(source), text); }
            catch (Exception ex)
            {
                error = $"Could not save {source.RelPath}: {ex.Message}";
                return false;
            }
            return true;
        }

        private static IReadOnlyList<FieldPathSegment> PathOf(Source source) =>
            source.Field.Select(FieldPathSegment.Field).ToArray();

        private static string FullPath(Source source) =>
            Path.Combine(HgEngineProject.RepoRootWindows, source.RelPath.Replace('/', Path.DirectorySeparatorChar));

        private static readonly Regex EntryStart = new(@"\[\s*([A-Za-z_][A-Za-z0-9_]*|\d+)\s*\]\s*=\s*\{", RegexOptions.Compiled);

        // One pass over the file. A file may spell an entry with any alias of its id, so each designator is
        // resolved to its value; the first entry for an id wins, as it does for the compiler's designated init.
        private static bool TryLoad(Source source, out string text, out Dictionary<int, (string Designator, int Open, int Close)> byId, out string error)
        {
            text = null; byId = null; error = null;
            if (!HgEngineProject.IsLinked) { error = "No hg-engine checkout is linked."; return false; }
            string full = FullPath(source);
            if (!File.Exists(full)) { error = $"{source.RelPath} was not found in the checkout."; return false; }
            var symbols = HgEngineSymbolTable.Load(source.Header);
            if (symbols == null) { error = $"{source.Header} could not be read."; return false; }

            text = HgEngineFileCache.GetText(full);
            byId = new Dictionary<int, (string, int, int)>();
            int from = 0;
            while (true)
            {
                var m = EntryStart.Match(text, from);
                if (!m.Success) break;
                int open = m.Index + m.Length - 1;
                if (!BraceScanner.TryFindMatchingBrace(text, open, out int close)) break;
                string designator = m.Groups[1].Value;
                if (int.TryParse(designator, out int id) || symbols.TryGetValue(designator, out id))
                    byId.TryAdd(id, (designator, open, close));
                from = close + 1;
            }
            return true;
        }

        private static string Unquote(string raw)
        {
            raw = raw.Trim();
            if (raw.Length < 2 || raw[0] != '"' || raw[^1] != '"') return raw;
            var sb = new StringBuilder(raw.Length - 2);
            for (int i = 1; i < raw.Length - 1; i++)
            {
                if (raw[i] == '\\' && i + 1 < raw.Length - 1 && (raw[i + 1] == '\\' || raw[i + 1] == '"')) i++;
                sb.Append(raw[i]);
            }
            return sb.ToString();
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>Read/write access to data/PokeFormDataTbl.c: which form species exist for a base
    /// species, and whether each needs the NEEDS_REVERSION flag. Source-only, no packed-ROM narc.
    /// Writes replace only one entry's own "{ ... }" span, or append a new entry before the closing
    /// "};", never touching #ifdef guards or any other species' entry.</summary>
    public static class HgEngineFormRegistry
    {
        private const string RelPath = "data/PokeFormDataTbl.c";
        // The table is u16[][32]; src/pokemon.c reads slot (form - 1) of 32.
        internal const int MaxFormSlots = 32;

        public readonly struct FormSlot
        {
            public bool NeedsReversion { get; }
            public string SpeciesSymbol { get; }
            public FormSlot(bool needsReversion, string speciesSymbol)
            {
                NeedsReversion = needsReversion;
                SpeciesSymbol = speciesSymbol;
            }
        }

        /// <summary>Parses every "[SPECIES_X] = { ... }" entry, keyed by the base species' designator.
        /// Unrecognized slot text is skipped rather than guessed.</summary>
        public static Dictionary<string, List<FormSlot>> LoadAll()
        {
            var result = new Dictionary<string, List<FormSlot>>(StringComparer.Ordinal);
            if (!HgEngineProject.IsLinked) return result;
            string path = Path.Combine(HgEngineProject.RepoPathUnc, RelPath.Replace('/', '\\'));
            if (!File.Exists(path)) return result;
            string text = File.ReadAllText(path);

            foreach (Match m in Regex.Matches(text, @"\[\s*(SPECIES_\w+)\s*\]\s*=\s*\{"))
            {
                int open = m.Index + m.Length - 1;
                if (!BraceScanner.TryFindMatchingBrace(text, open, out int close)) continue;
                result[m.Groups[1].Value] = ParseSlots(text, open, close);
            }
            return result;
        }

        private static List<FormSlot> ParseSlots(string text, int open, int close)
        {
            var slots = new List<FormSlot>();
            foreach (var (s, e) in SplitTopLevel(text, open, close))
            {
                string raw = text.Substring(s, e - s).Trim();
                if (raw.Length == 0) continue;

                var withReversion = Regex.Match(raw, @"^NEEDS_REVERSION\s*\|\s*(SPECIES_\w+)$");
                if (withReversion.Success) { slots.Add(new FormSlot(true, withReversion.Groups[1].Value)); continue; }

                var plain = Regex.Match(raw, @"^(SPECIES_\w+)$");
                if (plain.Success) { slots.Add(new FormSlot(false, plain.Groups[1].Value)); continue; }
                // Unrecognized slot text: skip, don't guess.
            }
            return slots;
        }

        /// <summary>Comma-splits an entry's direct children at brace depth 0, skipping "//" comments.</summary>
        private static List<(int start, int end)> SplitTopLevel(string text, int open, int close)
        {
            var spans = new List<(int, int)>();
            int i = open + 1, elemStart = i;
            while (i < close)
            {
                if (text[i] == '/' && i + 1 < close && text[i + 1] == '/')
                {
                    while (i < close && text[i] != '\n') i++;
                    continue;
                }
                if (text[i] == ',') { spans.Add((elemStart, i)); elemStart = i + 1; i++; continue; }
                i++;
            }
            spans.Add((elemStart, close));
            return spans;
        }

        /// <summary>The species id a form's personal data, learnset and hidden ability are read from, as
        /// PokeOtherFormMonsNoGet in src/pokemon.c resolves it. A form with no entry uses the species itself.</summary>
        public static int ResolveFormSpecies(int speciesId, int form)
        {
            if (form <= 0 || !HgEngineProject.IsActive) return speciesId;
            return ResolveFormSpecies(speciesId, form, (baseId, formNo) =>
            {
                var speciesTable = HgEngineSymbolTable.Load("include/constants/species.h");
                if (speciesTable == null || !speciesTable.TryGetNameWithPrefix(baseId, "SPECIES_", out string designator)) return 0;
                if (!LoadAll().TryGetValue(designator, out var slots) || formNo - 1 >= slots.Count) return 0;
                return speciesTable.TryGetValue(slots[formNo - 1].SpeciesSymbol, out int id) ? id : 0;
            });
        }

        internal static int ResolveFormSpecies(int speciesId, int form, Func<int, int, int> tableSlot)
        {
            if (form <= 0) return speciesId;
            // The engine still sends the vanilla form species to their fixed personal files.
            switch (speciesId)
            {
                case DSPRE.ROMFiles.SpeciesFile.DEOXYS_ID_NUM: return form <= 3 ? 495 + form : speciesId;
                case DSPRE.ROMFiles.SpeciesFile.WORMADAM_ID_NUM: return form <= 2 ? 498 + form : speciesId;
                case DSPRE.ROMFiles.SpeciesFile.GIRATINA_ID_NUM: return form <= 1 ? 500 + form : speciesId;
                case DSPRE.ROMFiles.SpeciesFile.SHAYMIN_ID_NUM: return form <= 1 ? 501 + form : speciesId;
                case DSPRE.ROMFiles.SpeciesFile.ROTOM_ID_NUM: return form <= 5 ? 502 + form : speciesId;
            }
            int target = form <= MaxFormSlots ? tableSlot(speciesId, form) : 0;
            return target > 0 ? target : speciesId;
        }

        /// <summary>Replaces (or inserts) one base species' entire form-slot list in one shot.</summary>
        public static bool TrySaveSpeciesForms(int baseSpeciesId, IReadOnlyList<FormSlot> desiredSlots, out string error)
        {
            error = null;
            if (desiredSlots.Count > MaxFormSlots) { error = $"A species can have at most {MaxFormSlots} forms."; return false; }
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }

            var speciesTable = HgEngineSymbolTable.Load("include/constants/species.h");
            if (speciesTable == null || !speciesTable.TryGetNameWithPrefix(baseSpeciesId, "SPECIES_", out string designator))
            { error = $"Could not resolve a species designator for id {baseSpeciesId}."; return false; }

            string path = Path.Combine(HgEngineProject.RepoPathUnc, RelPath.Replace('/', '\\'));
            if (!File.Exists(path)) { error = $"Source file not found: {path}"; return false; }
            string text = File.ReadAllText(path);

            string body = string.Concat(desiredSlots.Select(s =>
                "\n        " + (s.NeedsReversion ? "NEEDS_REVERSION | " : "") + s.SpeciesSymbol + ","));

            if (HgEngineSourcePatcher.TryFindEntry(text, designator, out int open, out int close))
            {
                text = string.Concat(text.AsSpan(0, open + 1), body, "\n    ", text.AsSpan(close));
            }
            else
            {
                int insertAt = text.LastIndexOf("};", StringComparison.Ordinal);
                if (insertAt < 0) { error = "Could not find the end of PokeFormDataTbl to insert a new entry."; return false; }
                string newEntry = $"    [{designator}] = {{{body}\n    }},\n";
                text = string.Concat(text.AsSpan(0, insertAt), newEntry, text.AsSpan(insertAt));
            }

            HgEngineFileCache.WriteText(path, text);
            return true;
        }
    }
}

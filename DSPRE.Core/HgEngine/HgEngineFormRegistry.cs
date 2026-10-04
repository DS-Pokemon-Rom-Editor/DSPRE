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
        /// Unrecognized slot text is skipped rather than guessed; a save refuses such an entry.</summary>
        public static Dictionary<string, List<FormSlot>> LoadAll()
        {
            var result = new Dictionary<string, List<FormSlot>>(StringComparer.Ordinal);
            if (!HgEngineProject.IsLinked) return result;
            string text = ReadSource(RelPath);
            if (text == null) return result;
            var table = Table(text);
            if (table == null) return result;
            foreach (var entry in table.Init.Items)
                if (entry.IndexText != null && entry.List != null) result[entry.IndexText.Trim()] = ParseSlots(text, entry.List, out _);
            return result;
        }

        // The table is the declaration with the most [NAME] entries.
        private static CDeclaration Table(string text) =>
            CSourceFile.For(text).Declarations.OrderByDescending(d => d.Init.Items.Count(i => i.IndexText != null)).FirstOrDefault();

        private static List<FormSlot> ParseSlots(string text, CInitList list, out bool complete)
        {
            var slots = new List<FormSlot>();
            complete = true;
            foreach (var item in list.Items)
            {
                string raw = item.ValueText(text).Trim();
                if (item.IsConditional) { complete = false; continue; }
                var withReversion = Regex.Match(raw, @"^NEEDS_REVERSION\s*\|\s*(SPECIES_\w+)$");
                if (withReversion.Success) { slots.Add(new FormSlot(true, withReversion.Groups[1].Value)); continue; }
                var plain = Regex.Match(raw, @"^(SPECIES_\w+)$");
                if (plain.Success) { slots.Add(new FormSlot(false, plain.Groups[1].Value)); continue; }
                complete = false;   // unrecognized: skipped, never guessed
            }
            return slots;
        }

        private static string ReadSource(string rel)
        {
            string path = Path.Combine(HgEngineProject.RepoPathUnc, rel.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? HgEngineFileCache.GetText(path).Replace("\r\n", "\n") : null;
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

            string path = Path.Combine(HgEngineProject.RepoPathUnc, RelPath.Replace('/', Path.DirectorySeparatorChar));
            string text = ReadSource(RelPath);
            if (text == null) { error = $"Source file not found: {path}"; return false; }
            var table = Table(text);
            if (table == null) { error = $"{RelPath} has no form table."; return false; }

            string body = string.Concat(desiredSlots.Select(s =>
                "\n        " + (s.NeedsReversion ? "NEEDS_REVERSION | " : "") + s.SpeciesSymbol + ","));

            var entry = table.Init.Items.FirstOrDefault(i => i.IndexText != null && i.List != null
                && (i.IndexText.Trim() == designator || (speciesTable.TryGetValue(i.IndexText.Trim(), out int v) && v == baseSpeciesId)));
            if (entry != null)
            {
                ParseSlots(text, entry.List, out bool complete);
                if (!complete) { error = $"{RelPath}: {designator}'s forms include text DSPRE doesn't read, so it wasn't rewritten."; return false; }
                text = string.Concat(text.AsSpan(0, entry.List.Open + 1), body, "\n    ", text.AsSpan(entry.List.Close));
            }
            else
            {
                int insertAt = HgEngineSwarms.LineStart(text, table.Init.Close);
                text = text.Insert(insertAt, $"    [{designator}] = {{{body}\n    }},\n");
            }

            if (!HgEngineVerifiedWrite.TryWrite(path, RelPath, text, written =>
                {
                    var back = Table(written)?.Init.Items.FirstOrDefault(i => i.IndexText?.Trim() == designator || (entry != null && i.IndexText?.Trim() == entry.IndexText.Trim()));
                    if (back?.List == null) return "the entry is missing";
                    var slots = ParseSlots(written, back.List, out bool ok);
                    return ok && slots.Select(x => (x.NeedsReversion, x.SpeciesSymbol)).SequenceEqual(desiredSlots.Select(x => (x.NeedsReversion, x.SpeciesSymbol))) ? null : "the forms differ";
                }, out error)) return false;
            return MapFormsToBase(designator, desiredSlots, speciesTable, out error);
        }

        private const string MappingRelPath = "data/FormToSpeciesMapping.c";

        /// <summary>
        /// The game and build_learnsets.py find a form's base species in FormToSpeciesMapping.c (a028 9_12), indexed
        /// from SPECIES_MEGA_START. Every form now listed for the base gets that line, added or corrected.
        /// </summary>
        private static bool MapFormsToBase(string baseDesignator, IReadOnlyList<FormSlot> slots, HgEngineSymbolTable species, out string error)
        {
            error = null;
            string path = Path.Combine(HgEngineProject.RepoPathUnc, MappingRelPath.Replace('/', Path.DirectorySeparatorChar));
            string text = ReadSource(MappingRelPath);
            if (text == null || !species.TryGetValue("SPECIES_MEGA_START", out int megaStart)) return true;
            int? Index(string designator) => HgEngineSourceExpression.TryEvaluate(designator, n => species.TryGetValue(n, out int v) ? v : null, out int i) ? i : null;
            string original = text;
            foreach (var slot in slots)
            {
                if (!species.TryGetValue(slot.SpeciesSymbol, out int id) || id < megaStart) continue;
                var table = Table(text);
                if (table == null) { error = $"{MappingRelPath} has no table."; return false; }
                var entry = table.Init.Items.FirstOrDefault(i => i.IndexText != null && Index(i.IndexText) == id - megaStart);
                if (entry != null)
                {
                    if (entry.ValueText(text).Trim() != baseDesignator)
                        text = text.Substring(0, entry.ValueStart) + baseDesignator + text.Substring(entry.ValueEnd);
                    continue;
                }
                text = text.Insert(HgEngineSwarms.LineStart(text, table.Init.Close), $"    [{slot.SpeciesSymbol} - SPECIES_MEGA_START] = {baseDesignator},\n");
            }
            if (text == original) return true;
            species.TryGetValue(baseDesignator, out int baseId);
            return HgEngineVerifiedWrite.TryWrite(path, MappingRelPath, text, written =>
            {
                var table = Table(written);
                foreach (var slot in slots)
                {
                    if (!species.TryGetValue(slot.SpeciesSymbol, out int id) || id < megaStart) continue;
                    var e = table?.Init.Items.FirstOrDefault(i => i.IndexText != null && Index(i.IndexText) == id - megaStart);
                    if (e == null || Index(e.ValueText(written)) != baseId) return $"{slot.SpeciesSymbol} doesn't map to {baseDesignator}";
                }
                return null;
            }, out error);
        }
    }
}

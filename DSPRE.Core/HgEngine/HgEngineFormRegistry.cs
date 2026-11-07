using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
        /// Unrecognised slot text is skipped, never guessed; a save refuses such an entry.</summary>
        public static Dictionary<string, List<FormSlot>> LoadAll()
        {
            Dictionary<string, List<FormSlot>> result = new Dictionary<string, List<FormSlot>>(StringComparer.Ordinal);
            if (!HgEngineProject.IsLinked) return result;
            string text = ReadSource(RelPath);
            if (text == null) return result;
            CDeclaration table = Table(text);
            if (table == null) return result;
            foreach (CInitItem entry in table.Init.Items)
                if (entry.IndexText != null && entry.List != null) result[entry.IndexText.Trim()] = ParseSlots(text, entry.List, out _);
            return result;
        }

        // The table is the declaration with the most [NAME] entries.
        private static CDeclaration Table(string text) =>
            CSourceFile.For(text).Declarations.OrderByDescending(d => d.Init.Items.Count(i => i.IndexText != null)).FirstOrDefault();

        private static List<FormSlot> ParseSlots(string text, CInitList list, out bool complete)
        {
            List<FormSlot> slots = new List<FormSlot>();
            complete = true;
            foreach (CInitItem item in list.Items)
            {
                if (ReadSlot(text, item) is FormSlot slot) slots.Add(slot);
                else complete = false;   // unrecognized: skipped, never guessed
            }
            return slots;
        }

        private static FormSlot? ReadSlot(string text, CInitItem item)
        {
            if (item.IsConditional) return null;
            string raw = item.ValueText(text).Trim();
            List<string> tokens = CLexer.Tokenize(raw).Select(t => t.Text(raw)).ToList();
            bool reverts = tokens.Count == 3 && tokens[0] == "NEEDS_REVERSION" && tokens[1] == "|";
            string form = reverts ? tokens[2] : tokens.Count == 1 ? tokens[0] : null;
            return form?.StartsWith("SPECIES_", StringComparison.Ordinal) == true ? new FormSlot(reverts, form) : null;
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
                FormsOf(baseId) is IReadOnlyList<int> forms && formNo - 1 < forms.Count ? forms[formNo - 1] : 0);
        }

        internal static int ResolveFormSpecies(int speciesId, int form, Func<int, int, int> tableSlot)
        {
            if (form <= 0) return speciesId;
            // The engine still sends the vanilla form species to their fixed personal files.
            switch (speciesId)
            {
                case DSPRE.ROMFiles.SpeciesFile.DeoxysIdNum: return form <= 3 ? 495 + form : speciesId;
                case DSPRE.ROMFiles.SpeciesFile.WormadamIdNum: return form <= 2 ? 498 + form : speciesId;
                case DSPRE.ROMFiles.SpeciesFile.GiratinaIdNum: return form <= 1 ? 500 + form : speciesId;
                case DSPRE.ROMFiles.SpeciesFile.ShayminIdNum: return form <= 1 ? 501 + form : speciesId;
                case DSPRE.ROMFiles.SpeciesFile.RotomIdNum: return form <= 5 ? 502 + form : speciesId;
            }
            int target = form <= MaxFormSlots ? tableSlot(speciesId, form) : 0;
            return target > 0 ? target : speciesId;
        }

        public static bool TryGetBaseAndForm(int speciesId, out int baseId, out int form)
        {
            baseId = form = 0;
            if (FormIds() is not FormIdMaps maps || !maps.BaseOf.TryGetValue(speciesId, out (int BaseId, int Form) found)) return false;
            (baseId, form) = found;
            return true;
        }

        /// <summary>Returns the species ids of the forms in form order, starting at form 1.</summary>
        public static IReadOnlyList<int> FormsOf(int baseId) =>
            FormIds() is FormIdMaps maps && maps.Forms.TryGetValue(baseId, out List<int> forms) ? forms : Array.Empty<int>();

        private sealed record FormIdMaps(string Text, HgEngineSymbolTable Species, Dictionary<int, (int BaseId, int Form)> BaseOf, Dictionary<int, List<int>> Forms);
        private static FormIdMaps _formIds;

        // Cached until species.h or the form table changes, since writers look up every species they write.
        private static FormIdMaps FormIds()
        {
            if (!HgEngineProject.IsActive) return null;
            HgEngineSymbolTable speciesTable = HgEngineSymbolTable.Load("include/constants/species.h");
            string path = Path.Combine(HgEngineProject.RepoPathUnc, RelPath.Replace('/', Path.DirectorySeparatorChar));
            if (speciesTable == null || !File.Exists(path)) return null;
            string text = HgEngineFileCache.GetText(path);
            FormIdMaps cached = _formIds;
            if (cached != null && ReferenceEquals(cached.Text, text) && ReferenceEquals(cached.Species, speciesTable)) return cached;

            int? Lookup(string name) => speciesTable.TryGetValue(name, out int v) ? v : null;
            Dictionary<int, (int, int)> baseOf = new Dictionary<int, (int, int)>();
            Dictionary<int, List<int>> forms = new Dictionary<int, List<int>>();
            string source = text.Replace("\r\n", "\n");
            foreach (CInitItem entry in Table(source)?.Init.Items ?? new List<CInitItem>())
            {
                if (entry.IndexText == null || entry.List == null || !HgEngineSourceExpression.TryEvaluate(entry.IndexText.Trim(), Lookup, out int baseSpecies)) continue;
                List<int> ids = new List<int>();
                // The species word has five form bits, so forms past 31 can't be written. An unreadable slot ends the
                // list, since skipping it would renumber the forms after it.
                for (int i = 0; i < entry.List.Items.Count && i < 31; i++)
                {
                    if (ReadSlot(source, entry.List.Items[i]) is not FormSlot slot || Lookup(slot.SpeciesSymbol) is not int id) break;
                    ids.Add(id);
                    baseOf.TryAdd(id, (baseSpecies, i + 1));
                }
                if (ids.Count > 0) forms[baseSpecies] = ids;
            }
            return _formIds = new FormIdMaps(text, speciesTable, baseOf, forms);
        }

        /// <summary>Replaces (or inserts) one base species' entire form-slot list in one shot.</summary>
        public static bool TrySaveSpeciesForms(int baseSpeciesId, IReadOnlyList<FormSlot> desiredSlots, out string error)
        {
            error = null;
            if (desiredSlots.Count > MaxFormSlots) { error = $"A species can have at most {MaxFormSlots} forms."; return false; }
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }

            HgEngineSymbolTable speciesTable = HgEngineSymbolTable.Load("include/constants/species.h");
            if (speciesTable == null || !speciesTable.TryGetNameWithPrefix(baseSpeciesId, "SPECIES_", out string designator))
            { error = $"Species {baseSpeciesId} has no SPECIES_ name in the checkout."; return false; }

            string path = Path.Combine(HgEngineProject.RepoPathUnc, RelPath.Replace('/', Path.DirectorySeparatorChar));
            string text = ReadSource(RelPath);
            if (text == null) { error = $"{path} is missing from the checkout."; return false; }
            CDeclaration table = Table(text);
            if (table == null) { error = $"{RelPath} has no form table."; return false; }

            string body = string.Concat(desiredSlots.Select(s =>
                "\n        " + (s.NeedsReversion ? "NEEDS_REVERSION | " : "") + s.SpeciesSymbol + ","));

            CInitItem entry = table.Init.Items.FirstOrDefault(i => i.IndexText != null && i.List != null
                && (i.IndexText.Trim() == designator || (speciesTable.TryGetValue(i.IndexText.Trim(), out int v) && v == baseSpeciesId)));
            if (entry != null)
            {
                ParseSlots(text, entry.List, out bool complete);
                if (!complete) { error = $"{RelPath}: {designator}'s forms include text DSPRE doesn't read, so it wasn't rewritten."; return false; }
                text = string.Concat(text.AsSpan(0, entry.List.Open + 1), body, "\n    ", text.AsSpan(entry.List.Close));
            }
            else
            {
                int insertAt = HgEngineSourcePatcher.LineStart(text, table.Init.Close);
                text = text.Insert(insertAt, $"    [{designator}] = {{{body}\n    }},\n");
            }

            if (!HgEngineVerifiedWrite.TryWrite(path, RelPath, text, written =>
                {
                    CInitItem back = Table(written)?.Init.Items.FirstOrDefault(i => i.IndexText?.Trim() == designator || (entry != null && i.IndexText?.Trim() == entry.IndexText.Trim()));
                    if (back?.List == null) return "the entry is missing";
                    List<FormSlot> slots = ParseSlots(written, back.List, out bool ok);
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
            foreach (FormSlot slot in slots)
            {
                if (!species.TryGetValue(slot.SpeciesSymbol, out int id) || id < megaStart) continue;
                CDeclaration table = Table(text);
                if (table == null) { error = $"{MappingRelPath} has no table."; return false; }
                CInitItem entry = table.Init.Items.FirstOrDefault(i => i.IndexText != null && Index(i.IndexText) == id - megaStart);
                if (entry != null)
                {
                    if (entry.ValueText(text).Trim() != baseDesignator)
                        text = text.Substring(0, entry.ValueStart) + baseDesignator + text.Substring(entry.ValueEnd);
                    continue;
                }
                text = text.Insert(HgEngineSourcePatcher.LineStart(text, table.Init.Close), $"    [{slot.SpeciesSymbol} - SPECIES_MEGA_START] = {baseDesignator},\n");
            }
            if (text == original) return true;
            species.TryGetValue(baseDesignator, out int baseId);
            return HgEngineVerifiedWrite.TryWrite(path, MappingRelPath, text, written =>
            {
                CDeclaration table = Table(written);
                foreach (FormSlot slot in slots)
                {
                    if (!species.TryGetValue(slot.SpeciesSymbol, out int id) || id < megaStart) continue;
                    CInitItem e = table?.Init.Items.FirstOrDefault(i => i.IndexText != null && Index(i.IndexText) == id - megaStart);
                    if (e == null || Index(e.ValueText(written)) != baseId) return $"{slot.SpeciesSymbol} doesn't map to {baseDesignator}";
                }
                return null;
            }, out error);
        }
    }
}

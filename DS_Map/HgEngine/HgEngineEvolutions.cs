using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>Source-text read/write for data/Evolutions.c's per-species <c>EvolutionTable.entries[]</c>
    /// (hg-engine's <c>MAX_EVOS_PER_POKE</c> is 9; DSPRE's UI only edits 7 slots). Each entry is a
    /// positional <c>{ method, param, target }</c> literal, not a dotted field, so this locates
    /// <c>.entries</c> as one block and splits/rebuilds its elements directly. The method list is read
    /// live from <c>include/pokemon.h</c>'s <c>EvoMethod</c> enum, never hardcoded. Slots beyond what
    /// DSPRE edits are preserved verbatim on write, never truncated.
    ///
    /// <c>target</c> is packed, per <c>GetMonEvolutionInternal.c</c>'s own <c>evoTable[i].target &amp;
    /// 0x7FF</c>/<c>&amp; 0xF800 &gt;&gt; 11</c>: bits 0-10 are the species id, bits 11-15 are a form to
    /// switch to on evolution (written as <c>SPECIES_X | (form &lt;&lt; 11)</c>). No current entry in this
    /// checkout uses a nonzero form, but the field is read/written losslessly so a hand-added one never
    /// gets silently misread or dropped.</summary>
    public static class HgEngineEvolutions
    {
        private const string SourceRelPath = "data/Evolutions.c";
        private const string SpeciesHeaderRelPath = "include/constants/species.h";
        private const string MethodHeaderRelPath = "include/pokemon.h";
        private const string ItemHeaderRelPath = "include/constants/item.h";
        private const string MoveHeaderRelPath = "include/constants/moves.h";
        private const string MethodPrefix = "EVO_";
        private static readonly FieldPathSegment[] EntriesPath = { FieldPathSegment.Field("entries") };
        private static readonly Regex FormBitsExpr = new(@"^(.+?)\s*\|\s*\(?\s*(\d+)\s*<<\s*11\s*\)?\s*$");

        public struct EvoEntry
        {
            public int MethodValue;
            public string MethodName;   // null if the value doesn't resolve to any known EVO_* name
            public int Param;
            public int TargetSpeciesId;
            public int TargetFormId;    // 0 = no form override; see class doc
        }

        /// <summary>The real EVO_* method names declared in this checkout, in ascending value order, for a
        /// dynamic dropdown (hg-engine forks add/reorder these; DSPRE's own vanilla EvolutionMethod enum
        /// must never be used as a stand-in once hg-engine is linked).</summary>
        public static List<(string Name, int Value)> GetMethodOptions()
        {
            var result = new List<(string Name, int Value)>();
            var table = HgEngineSymbolTable.Load(MethodHeaderRelPath);
            if (table == null) return result;
            foreach (var kv in table.ByName)
                if (kv.Key.StartsWith(MethodPrefix, StringComparison.Ordinal)) result.Add((kv.Key, kv.Value));
            result.Sort((a, b) => a.Value.CompareTo(b.Value));
            return result;
        }

        /// <summary>Reads up to <paramref name="slotCount"/> entries. A species with no entry at all in
        /// Evolutions.c (e.g. a fakemon added after the last dump) reads as an empty list, not an error.</summary>
        public static bool TryGetEntries(int speciesId, int slotCount, out List<EvoEntry> entries, out string error)
        {
            entries = new List<EvoEntry>();
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator))
            { error = $"Could not resolve a species designator for id {speciesId}."; return false; }

            string text = TryReadSource(out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }

            if (!HgEngineSourcePatcher.TryGetFieldValue(text, designator, EntriesPath, out string rawEntriesBlock))
                return true;   // no entry: caller treats an empty list as all-EVO_NONE

            var methodTable = HgEngineSymbolTable.Load(MethodHeaderRelPath);
            var itemTable = HgEngineSymbolTable.Load(ItemHeaderRelPath);
            var moveTable = HgEngineSymbolTable.Load(MoveHeaderRelPath);

            var raw = HgEngineSourcePatcher.SplitArrayValue(rawEntriesBlock);
            for (int i = 0; i < slotCount && i < raw.Count; i++)
            {
                var parts = HgEngineSourcePatcher.SplitArrayValue(raw[i].Trim());
                if (parts.Count < 3) { entries.Add(default); continue; }

                int method = ResolveToken(parts[0], methodTable);
                int param = ResolveToken(parts[1], itemTable, moveTable, species);
                ResolveTarget(parts[2], species, out int target, out int targetForm);
                string methodName = methodTable != null && methodTable.TryGetNameWithPrefix(method, MethodPrefix, out string mn) ? mn : null;

                entries.Add(new EvoEntry { MethodValue = method, MethodName = methodName, Param = param, TargetSpeciesId = target, TargetFormId = targetForm });
            }
            return true;
        }

        /// <summary>Writes exactly <paramref name="uiEntries"/> into the first slots, preserving any
        /// further slots the real source already declares beyond that count unchanged. Inserts a brand
        /// new entry (fine in C to declare fewer than MAX_EVOS_PER_POKE elements; the rest zero-init to
        /// EVO_NONE) if the species has none yet.</summary>
        public static bool TrySetEntries(int speciesId, IReadOnlyList<(string MethodName, int Param, int TargetSpeciesId, int TargetFormId)> uiEntries, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }
            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            if (species == null || !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string designator))
            { error = $"Could not resolve a species designator for id {speciesId}."; return false; }

            string text = TryReadSource(out string path);
            if (text == null) { error = $"Source file not found: {path}"; return false; }

            List<string> existingRaw = null;
            if (HgEngineSourcePatcher.TryGetFieldValue(text, designator, EntriesPath, out string rawEntriesBlock))
                existingRaw = HgEngineSourcePatcher.SplitArrayValue(rawEntriesBlock);

            int totalSlots = existingRaw != null && existingRaw.Count > uiEntries.Count ? existingRaw.Count : uiEntries.Count;
            var built = new List<string>(totalSlots);
            for (int i = 0; i < totalSlots; i++)
            {
                if (i < uiEntries.Count)
                {
                    var e = uiEntries[i];
                    string speciesToken = species.TryGetNameWithPrefix(e.TargetSpeciesId, "SPECIES_", out string tn) ? tn : e.TargetSpeciesId.ToString();
                    string targetLiteral = e.TargetFormId != 0 ? $"{speciesToken} | ({e.TargetFormId} << 11)" : speciesToken;
                    built.Add($"{{ {e.MethodName}, {e.Param}, {targetLiteral} }}");
                }
                else
                {
                    built.Add(existingRaw[i].Trim());
                }
            }
            string newBlock = "{\n            " + string.Join(",\n            ", built) + ",\n        }";

            if (HgEngineSourcePatcher.TryFindEntry(text, designator, out _, out _))
            {
                if (!HgEngineSourcePatcher.TryReplaceField(ref text, designator, EntriesPath, newBlock))
                { error = $"Could not locate .entries for species {speciesId}."; return false; }
            }
            else
            {
                string newEntry = $"\n    [{designator}] = {{\n        .entries = {newBlock},\n    }},\n";
                if (!HgEngineHeaderEditor.TryInsertBeforeFinalCloseBrace(ref text, newEntry))
                { error = $"Could not insert a new Evolutions entry for species {speciesId}."; return false; }
            }

            File.WriteAllText(path, text);
            return true;
        }

        private static int ResolveToken(string token, params HgEngineSymbolTable[] tables)
        {
            token = token.Trim();
            if (int.TryParse(token, out int v)) return v;
            foreach (var t in tables)
                if (t != null && t.TryGetValue(token, out int tv)) return tv;
            return 0;
        }

        /// <summary>Splits a target token into species id + form override, handling all three shapes seen
        /// or possible in source: a plain symbol (<c>SPECIES_X</c>, form 0), a plain packed number, or an
        /// explicit <c>SPECIES_X | (form &lt;&lt; 11)</c> expression.</summary>
        internal static void ResolveTarget(string token, HgEngineSymbolTable species, out int speciesId, out int formId)
        {
            token = token.Trim();
            formId = 0;

            var m = FormBitsExpr.Match(token);
            string speciesToken = token;
            if (m.Success)
            {
                speciesToken = m.Groups[1].Value.Trim();
                int.TryParse(m.Groups[2].Value, out formId);
            }

            if (int.TryParse(speciesToken, out int raw))
            {
                if (!m.Success) { speciesId = raw & 0x7FF; formId = (raw >> 11) & 0x1F; return; }
                speciesId = raw;   // already just the species portion of an explicit OR expression
                return;
            }

            speciesId = species != null && species.TryGetValue(speciesToken, out int sv) ? sv : 0;
        }

        private static string TryReadSource(out string path)
        {
            path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', '\\'));
            return File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
        }
    }
}

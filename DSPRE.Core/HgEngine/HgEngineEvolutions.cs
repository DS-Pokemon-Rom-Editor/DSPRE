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
    /// switch to on evolution. The checkout spells it <c>MON_WITH_FORM(SPECIES_X, form)</c>, which is
    /// what a changed target is written as; an unchanged one keeps its own spelling.</summary>
    public static class HgEngineEvolutions
    {
        private const string SourceRelPath = "data/Evolutions.c";
        private const string SpeciesHeaderRelPath = "include/constants/species.h";
        private const string MethodHeaderRelPath = "include/pokemon.h";
        private const string ItemHeaderRelPath = "include/constants/item.h";
        private const string MoveHeaderRelPath = "include/constants/moves.h";
        // EVO_HAS_MOVE_TYPE names a type, e.g. Sylveon's TYPE_FAIRY.
        private const string TypeHeaderRelPath = "include/constants/battle_constants.h";
        private const string MethodPrefix = "EVO_";
        private static readonly FieldPathSegment[] EntriesPath = { FieldPathSegment.Field("entries") };
        private static readonly Regex Identifier = new(@"\b[A-Za-z_]\w*\b");

        public struct EvoEntry
        {
            public int MethodValue;
            public string MethodName;   // null if the value doesn't resolve to any known EVO_* name
            public int Param;
            public int TargetSpeciesId;
            public int TargetFormId;    // 0 = no form override; see class doc
            // The method or target text didn't resolve, so the row isn't what the source says; saving it would erase the entry.
            public bool Unresolved;
            public string RawText;
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
            var typeTable = HgEngineSymbolTable.Load(TypeHeaderRelPath);

            var raw = HgEngineSourcePatcher.SplitArrayValue(rawEntriesBlock);
            for (int i = 0; i < slotCount && i < raw.Count; i++)
            {
                string rawEntry = raw[i].Trim();
                if (rawEntry.Length == 0) { entries.Add(default); continue; }
                var parts = HgEngineSourcePatcher.SplitArrayValue(rawEntry);
                if (parts.Count < 3) { entries.Add(new EvoEntry { Unresolved = true, RawText = rawEntry }); continue; }

                bool methodOk = TryResolveToken(parts[0], out int method, methodTable);
                int param = ResolveToken(parts[1], itemTable, moveTable, species, typeTable);
                bool targetOk = ResolveTarget(parts[2], species, out int target, out int targetForm);
                string methodName = methodTable != null && methodTable.TryGetNameWithPrefix(method, MethodPrefix, out string mn) ? mn : null;

                entries.Add(new EvoEntry
                {
                    MethodValue = method, MethodName = methodName, Param = param, TargetSpeciesId = target, TargetFormId = targetForm,
                    Unresolved = !methodOk || !targetOk, RawText = rawEntry,
                });
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

            var paramTables = new[]
            {
                HgEngineSymbolTable.Load(ItemHeaderRelPath), HgEngineSymbolTable.Load(MoveHeaderRelPath), species, HgEngineSymbolTable.Load(TypeHeaderRelPath),
            };
            int totalSlots = existingRaw != null && existingRaw.Count > uiEntries.Count ? existingRaw.Count : uiEntries.Count;
            var built = new List<string>(totalSlots);
            for (int i = 0; i < totalSlots; i++)
            {
                if (i < uiEntries.Count)
                {
                    var e = uiEntries[i];
                    string existing = existingRaw != null && i < existingRaw.Count ? existingRaw[i] : null;
                    built.Add(BuildEntryLiteral(e.MethodName, e.Param, e.TargetSpeciesId, e.TargetFormId, existing, species, paramTables));
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

            HgEngineFileCache.WriteText(path, text);
            return true;
        }

        /// <summary>One <c>{ method, param, target }</c> literal. An unchanged param or target keeps the
        /// spelling <paramref name="existingRaw"/> already has.</summary>
        internal static string BuildEntryLiteral(string methodName, int param, int targetSpeciesId, int targetFormId, string existingRaw,
            HgEngineSymbolTable species, HgEngineSymbolTable[] paramTables)
        {
            string speciesToken = species != null && species.TryGetNameWithPrefix(targetSpeciesId, "SPECIES_", out string tn) ? tn : targetSpeciesId.ToString();
            string targetLiteral = HgEngineTrainerSource.FormatSpecies(speciesToken, targetFormId);
            string paramLiteral = param.ToString();
            if (existingRaw != null)
            {
                var parts = HgEngineSourcePatcher.SplitArrayValue(existingRaw.Trim());
                if (parts.Count >= 3)
                {
                    // ITEM_, MOVE_, TYPE_ or a name DSPRE can't read.
                    if (parts[0].Trim() == methodName && ResolveToken(parts[1], paramTables) == param)
                        paramLiteral = parts[1].Trim();
                    if (ResolveTarget(parts[2], species, out int oldTarget, out int oldForm) && oldTarget == targetSpeciesId && oldForm == targetFormId)
                        targetLiteral = parts[2].Trim();
                }
            }
            return $"{{ {methodName}, {paramLiteral}, {targetLiteral} }}";
        }

        private static int ResolveToken(string token, params HgEngineSymbolTable[] tables)
            => TryResolveToken(token, out int v, tables) ? v : 0;

        private static bool TryResolveToken(string token, out int value, params HgEngineSymbolTable[] tables)
        {
            token = token.Trim();
            if (int.TryParse(token, out value)) return true;
            foreach (var t in tables)
                if (t != null && t.TryGetValue(token, out value)) return true;
            value = 0;
            return false;
        }

        /// <summary>Splits a target into species id + form override. Names are swapped for their values
        /// first so the trainer parser handles every shape: <c>SPECIES_X</c>, a packed number,
        /// <c>MON_WITH_FORM(SPECIES_X, n)</c> and <c>SPECIES_X | (n &lt;&lt; 11)</c>. False when a name
        /// doesn't resolve.</summary>
        internal static bool ResolveTarget(string token, HgEngineSymbolTable species, out int speciesId, out int formId)
        {
            string numeric = Identifier.Replace(token ?? "", m =>
                m.Value != "MON_WITH_FORM" && species != null && species.TryGetValue(m.Value, out int v) ? v.ToString() : m.Value);
            return HgEngineTrainerSource.TryParseSpecies(numeric, null, out speciesId, out formId);
        }

        private static string TryReadSource(out string path)
        {
            path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', '\\'));
            return File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
        }
    }
}

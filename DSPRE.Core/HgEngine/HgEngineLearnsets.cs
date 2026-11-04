using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using NarcAPI;

namespace DSPRE.HgEngine
{
    /// <summary>hg-engine's learnsets narc (a/0/3/3) is one combined table of u32 (level &lt;&lt; 16) | move rows,
    /// built from data/learnsets/learnsets.json. Sync splits it into one file per species, keeping the u32
    /// entries so move ids past 511 survive; <see cref="LearnsetData"/> reads that width while the project is
    /// hg-engine-linked.</summary>
    public static class HgEngineLearnsets
    {
        public const string SourceRelPath = "data/learnsets/learnsets.json";
        private const string SpeciesHeaderRelPath = "include/constants/species.h";
        private const string MovesHeaderRelPath = "include/constants/moves.h";
        private const string FormMapRelPath = "data/FormToSpeciesMapping.c";

        /// <summary>One species' level-up moves as the linked checkout's build reads them from learnsets.json,
        /// in file order with MOVE_ names resolved through moves.h. A form id reads its own SPECIES_ key; when
        /// that list is missing or empty and data/FormToSpeciesMapping.c names a base species, the base list
        /// is returned, as scripts/build_learnsets.py does. A species with no list at all gives an empty list.
        /// False with <paramref name="error"/> when the list can't be read or names an unknown move. Reads only.</summary>
        public static bool TryGetLevelMoves(int speciesId, out List<(int level, int move)> moves, out string error)
        {
            moves = new List<(int level, int move)>();
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }

            HgEngineSymbolTable species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            HgEngineSymbolTable moveTable = HgEngineSymbolTable.Load(MovesHeaderRelPath);
            if (species == null || moveTable == null) { error = "Couldn't read species.h or moves.h from the checkout."; return false; }

            string path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', Path.DirectorySeparatorChar));
            string formPath = Path.Combine(HgEngineProject.RepoPathUnc, FormMapRelPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) { error = $"{path} is missing from the checkout."; return false; }
            if (!File.Exists(formPath)) { error = $"{formPath} is missing from the checkout."; return false; }

            try
            {
                Dictionary<string, string> formToBase = ParseFormToBase(HgEngineFileCache.GetText(formPath));
                return TryReadLevelMoves(HgEngineFileCache.GetText(path), speciesId, species, moveTable, formToBase, out moves, out error);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>FormToSpeciesMapping.c's <c>[SPECIES_FORM - SPECIES_MEGA_START] = SPECIES_BASE</c> rows, by name.</summary>
        internal static Dictionary<string, string> ParseFormToBase(string text)
        {
            const string offset = "- SPECIES_MEGA_START";
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (CDeclaration table in CSourceFile.For(text).Declarations)
                foreach (CInitItem row in table.Init.Items)
                {
                    string index = CSourceFile.Normalize(row.IndexText ?? "");
                    if (index.EndsWith(offset, StringComparison.Ordinal))
                        map[index.Substring(0, index.Length - offset.Length).Trim()] = row.ValueText(text).Trim();
                }
            return map;
        }

        /// <summary>Pure half of <see cref="TryGetLevelMoves"/>.</summary>
        internal static bool TryReadLevelMoves(string json, int speciesId, HgEngineSymbolTable species, HgEngineSymbolTable moves,
            IReadOnlyDictionary<string, string> formToBase, out List<(int level, int move)> list, out string error)
        {
            list = new List<(int level, int move)>();
            List<JsonText.Member> root = JsonText.Members(json, 0);
            if (root == null) { error = "learnsets.json isn't a JSON object."; return false; }
            return TryReadLevelMoves(json, root, speciesId, species, moves, formToBase, list, out error);
        }

        // Takes the root parsed once, so a save checking many species doesn't rescan the file for each.
        private static bool TryReadLevelMoves(string json, List<JsonText.Member> root, int speciesId, HgEngineSymbolTable species, HgEngineSymbolTable moves,
            IReadOnlyDictionary<string, string> formToBase, List<(int level, int move)> list, out string error)
        {
            error = null;
            // Same key choice as the save: the file's own alias wins.
            List<string> names = species.ByName
                .Where(kv => kv.Value == speciesId && kv.Key.StartsWith("SPECIES_", StringComparison.Ordinal))
                .Select(kv => kv.Key).ToList();
            string key = names.FirstOrDefault(n => root.Any(m => m.Key == n));
            if (key == null && !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out key))
            {
                error = $"No SPECIES_ name for species {speciesId}.";
                return false;
            }

            if (!TryReadList(json, root, key, moves, list, out error)) return false;
            if (list.Count > 0) return true;

            string form = formToBase.ContainsKey(key) ? key : names.FirstOrDefault(n => formToBase.ContainsKey(n));
            return form == null || TryReadList(json, root, formToBase[form], moves, list, out error);
        }

        private static bool TryReadList(string json, List<JsonText.Member> root, string key, HgEngineSymbolTable moves,
            List<(int level, int move)> list, out string error)
        {
            error = null;
            JsonText.Member entry = root.FirstOrDefault(m => m.Key == key);
            if (entry == null) return true;
            if (json[entry.ValueStart] != '{') { error = $"{key} isn't an object in learnsets.json."; return false; }
            List<JsonText.Member> fields = JsonText.Members(json, entry.ValueStart);
            if (fields == null) { error = $"Couldn't read {key} in learnsets.json."; return false; }
            JsonText.Member levelMoves = fields.FirstOrDefault(m => m.Key == "LevelMoves");
            if (levelMoves == null) return true;
            if (json[levelMoves.ValueStart] != '[') { error = $"{key} LevelMoves isn't a list."; return false; }

            try
            {
                using JsonDocument doc = System.Text.Json.JsonDocument.Parse(json.AsMemory(levelMoves.ValueStart, levelMoves.ValueEnd - levelMoves.ValueStart));
                int index = 0;
                foreach (JsonElement row in doc.RootElement.EnumerateArray())
                {
                    index++;
                    if (row.ValueKind != System.Text.Json.JsonValueKind.Object || !row.TryGetProperty("Level", out JsonElement levelValue) || !TryReadLevel(levelValue, out int level))
                    {
                        error = $"{key} level-up move {index} has no readable Level.";
                        return false;
                    }
                    string move = row.TryGetProperty("Move", out JsonElement moveValue) && moveValue.ValueKind == System.Text.Json.JsonValueKind.String
                        ? moveValue.GetString().Trim() : "";
                    if (move.Length == 0 || !moves.TryGetValue(move, out int moveId))
                    {
                        error = $"{key} level-up move {index} names unknown move \"{move}\".";
                        return false;
                    }
                    list.Add((level, moveId));
                }
                return true;
            }
            catch (System.Text.Json.JsonException ex)
            {
                list.Clear();
                error = $"{key} LevelMoves couldn't be read: {ex.Message}";
                return false;
            }
        }

        // Python's int() also takes a numeric string.
        private static bool TryReadLevel(System.Text.Json.JsonElement value, out int level)
        {
            level = 0;
            return value.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Number => value.TryGetInt32(out level),
                System.Text.Json.JsonValueKind.String => int.TryParse(value.GetString().Trim(), out level),
                _ => false,
            };
        }

        /// <summary>The learnsets.json lists that name moves only. The build turns MachineMoves into each species'
        /// compatibility with every sMachineMoves slot teaching one of them, level-up moves included.</summary>
        public const string MachineMovesField = "MachineMoves", EggMovesField = "EggMoves", TutorMovesField = "TutorMoves";

        /// <summary>
        /// One species' move list from learnsets.json, in file order. <paramref name="inherited"/> is true when the species
        /// has no list and the build uses its base species' list, as build_learnsets.py does for forms.
        /// </summary>
        public static bool TryGetMoveNames(int speciesId, string field, out List<int> moves, out bool inherited, out string error)
        {
            moves = new List<int>();
            inherited = false;
            if (!TryLoad(out string json, out HgEngineSymbolTable species, out HgEngineSymbolTable moveTable, out Dictionary<string, string> formToBase, out error)) return false;
            List<JsonText.Member> root = JsonText.Members(json, 0);
            if (root == null) { error = "learnsets.json isn't a JSON object."; return false; }
            if (!TryKeyFor(speciesId, species, root, out string key, out List<string> names, out error)) return false;

            if (!TryReadNames(json, root, key, field, moveTable, moves, out error)) return false;
            if (moves.Count > 0) return true;
            string form = formToBase.ContainsKey(key) ? key : names.FirstOrDefault(n => formToBase.ContainsKey(n));
            if (form == null) return true;
            if (!TryReadNames(json, root, formToBase[form], field, moveTable, moves, out error)) return false;
            inherited = moves.Count > 0;
            return true;
        }

        /// <summary>Each form's base species, from FormToSpeciesMapping.c; a form with no list uses its base's.</summary>
        public static Dictionary<int, int> FormBases()
        {
            Dictionary<int, int> bases = new Dictionary<int, int>();
            if (!TryLoad(out _, out HgEngineSymbolTable species, out _, out Dictionary<string, string> formToBase, out _)) return bases;
            foreach ((string form, string baseName) in formToBase)
                if (species.TryGetValue(form, out int formId) && species.TryGetValue(baseName, out int baseId)) bases[formId] = baseId;
            return bases;
        }

        /// <summary>
        /// Every species' own list in one pass, keyed by species id; a species without a list is absent (the
        /// build gives a form its base species' list, which <see cref="TryGetMoveNames"/> reports).
        /// </summary>
        public static bool TryGetAllMoveNames(string field, out Dictionary<int, List<int>> lists, out string error)
        {
            lists = new Dictionary<int, List<int>>();
            if (!TryLoad(out string json, out HgEngineSymbolTable species, out HgEngineSymbolTable moveTable, out _, out error)) return false;
            List<JsonText.Member> root = JsonText.Members(json, 0);
            if (root == null) { error = "learnsets.json isn't a JSON object."; return false; }
            foreach (JsonText.Member member in root)
            {
                if (!species.TryGetValue(member.Key, out int id) || lists.ContainsKey(id)) continue;
                List<int> moves = new List<int>();
                if (!TryReadNames(json, root, member.Key, field, moveTable, moves, out error)) return false;
                if (moves.Count > 0) lists[id] = moves;
            }
            return true;
        }

        /// <summary>Writes one species' move list into learnsets.json, keeping the names the file already spells.</summary>
        public static bool TrySaveMoveNames(int speciesId, string field, IReadOnlyList<int> moves, out string error) =>
            TrySaveMoveNames(field, new Dictionary<int, IReadOnlyList<int>> { [speciesId] = moves }, out error);

        /// <summary>Writes several species' lists in one go, so the file is written once.</summary>
        public static bool TrySaveMoveNames(string field, IReadOnlyDictionary<int, IReadOnlyList<int>> lists, out string error)
        {
            if (!TryLoad(out string json, out HgEngineSymbolTable species, out HgEngineSymbolTable moveTable, out _, out error)) return false;
            string original = json;
            foreach ((int speciesId, IReadOnlyList<int> moves) in lists)
                if (!TryApplyMoveNames(ref json, speciesId, field, moves, species, moveTable, out error)) return false;
            if (json == original) return true;

            try
            {
                string path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', Path.DirectorySeparatorChar));
                byte[] bytes = File.ReadAllBytes(path);
                bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
                HgEngineFileCache.WriteText(path, json, new UTF8Encoding(bom));
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryApplyMoveNames(ref string json, int speciesId, string field, IReadOnlyList<int> moves,
            HgEngineSymbolTable species, HgEngineSymbolTable moveTable, out string error)
        {
            error = null;
            List<JsonText.Member> root = JsonText.Members(json, 0);
            if (root == null) { error = "learnsets.json isn't a JSON object."; return false; }
            if (!TryKeyFor(speciesId, species, root, out string key, out _, out error)) return false;

            Dictionary<int, string> kept = new Dictionary<int, string>();
            JsonText.Member existing = root.FirstOrDefault(m => m.Key == key);
            List<JsonText.Member> fields = existing != null && json[existing.ValueStart] == '{' ? JsonText.Members(json, existing.ValueStart) : null;
            JsonText.Member current = fields?.FirstOrDefault(m => m.Key == field);
            if (current != null)
                foreach (string name in JsonText.Strings(json, current.ValueStart, current.ValueEnd))
                    if (moveTable.TryGetValue(name.Trim(), out int id)) kept.TryAdd(id, name);

            List<string> names = new List<string>(moves.Count);
            foreach (int move in moves)
            {
                if (!kept.TryGetValue(move, out string name) && !moveTable.TryGetNameWithPrefix(move, "MOVE_", out name))
                { error = $"No MOVE_ name for move {move}."; return false; }
                names.Add(name);
            }

            string newline = json.Contains("\r\n") ? "\r\n" : "\n";
            string unit = JsonText.IndentUnit(json);
            string updated;
            if (existing == null)
            {
                string member = $"\"{key}\": {{{newline}{unit}{unit}\"{field}\": {RenderNames(names, unit + unit, unit, newline)}{newline}{unit}}}";
                int rootEnd = JsonText.SkipValue(json, JsonText.SkipWs(json, 0));
                updated = JsonText.InsertMember(json, JsonText.SkipWs(json, 0), rootEnd, root, member, unit, "", newline);
            }
            else if (fields == null) { error = $"{key} isn't an object in learnsets.json."; return false; }
            else if (current != null)
            {
                if (json[current.ValueStart] != '[') { error = $"{key} {field} isn't a list."; return false; }
                updated = json.Substring(0, current.ValueStart) + RenderNames(names, LineIndent(json, current.KeyStart), unit, newline)
                        + json.Substring(current.ValueEnd);
            }
            else
            {
                string outer = LineIndent(json, existing.KeyStart);
                updated = JsonText.InsertMember(json, existing.ValueStart, existing.ValueEnd, fields,
                    $"\"{field}\": {RenderNames(names, outer + unit, unit, newline)}", outer + unit, outer, newline);
            }
            json = updated;
            return true;
        }

        private static bool TryLoad(out string json, out HgEngineSymbolTable species, out HgEngineSymbolTable moves,
            out Dictionary<string, string> formToBase, out string error)
        {
            json = null; formToBase = null; error = null;
            species = moves = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }
            species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            moves = HgEngineSymbolTable.Load(MovesHeaderRelPath);
            if (species == null || moves == null) { error = "Couldn't read species.h or moves.h from the checkout."; return false; }
            string path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', Path.DirectorySeparatorChar));
            string formPath = Path.Combine(HgEngineProject.RepoPathUnc, FormMapRelPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path) || !File.Exists(formPath)) { error = $"{SourceRelPath} or {FormMapRelPath} is missing from the checkout."; return false; }
            try
            {
                string text = HgEngineFileCache.GetText(path);
                json = text.Length > 0 && text[0] == '﻿' ? text.Substring(1) : text;
                formToBase = ParseFormToBase(HgEngineFileCache.GetText(formPath));
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                error = ex.Message;
                return false;
            }
        }

        // The file's own spelling of the species wins over other names species.h gives the same id.
        private static bool TryKeyFor(int speciesId, HgEngineSymbolTable species, List<JsonText.Member> root,
            out string key, out List<string> names, out string error)
        {
            error = null;
            names = species.ByName
                .Where(kv => kv.Value == speciesId && kv.Key.StartsWith("SPECIES_", StringComparison.Ordinal))
                .Select(kv => kv.Key).ToList();
            key = names.FirstOrDefault(n => root.Any(m => m.Key == n));
            if (key != null || species.TryGetNameWithPrefix(speciesId, "SPECIES_", out key)) return true;
            error = $"No SPECIES_ name for species {speciesId}.";
            return false;
        }

        private static bool TryReadNames(string json, List<JsonText.Member> root, string key, string field,
            HgEngineSymbolTable moves, List<int> list, out string error)
        {
            error = null;
            JsonText.Member entry = root.FirstOrDefault(m => m.Key == key);
            if (entry == null) return true;
            if (json[entry.ValueStart] != '{') { error = $"{key} isn't an object in learnsets.json."; return false; }
            JsonText.Member value = JsonText.Members(json, entry.ValueStart)?.FirstOrDefault(m => m.Key == field);
            if (value == null) return true;
            try
            {
                using JsonDocument doc = System.Text.Json.JsonDocument.Parse(json.AsMemory(value.ValueStart, value.ValueEnd - value.ValueStart));
                if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array) { error = $"{key} {field} isn't a list."; return false; }
                foreach (JsonElement row in doc.RootElement.EnumerateArray())
                {
                    string name = row.ValueKind == System.Text.Json.JsonValueKind.String ? row.GetString().Trim() : "";
                    if (!moves.TryGetValue(name, out int id)) { error = $"{key} {field} names unknown move \"{name}\"."; return false; }
                    list.Add(id);
                }
                return true;
            }
            catch (System.Text.Json.JsonException ex)
            {
                list.Clear();
                error = $"{key} {field} couldn't be read: {ex.Message}";
                return false;
            }
        }

        // Matches Python's json.dump(indent=N): one name per line.
        private static string RenderNames(IReadOnlyList<string> names, string indent, string unit, string newline)
        {
            if (names.Count == 0) return "[]";
            StringBuilder sb = new StringBuilder("[");
            for (int i = 0; i < names.Count; i++)
            {
                sb.Append(newline).Append(indent).Append(unit).Append('"').Append(names[i]).Append('"');
                if (i < names.Count - 1) sb.Append(',');
            }
            return sb.Append(newline).Append(indent).Append(']').ToString();
        }

        public const uint Terminator = 0x0000FFFF;

        // build_learnsets.py regenerates MAX_LEVELUP_MOVES from the longest list, but refuses rows past 64 slots.
        public const int BuildMaxRowSlots = 64;

        /// <summary>Rebuilds every species' learnset file from hg-engine's combined table.
        /// Species past the table's own row count get an empty file instead of a missing one.</summary>
        public static bool Sync(string learnsetNarcPath, string repoUnc, string unpackedDir, int totalSpeciesCount, out string error)
        {
            error = null;
            int maxLevelupMoves = ReadMaxLevelupMoves(repoUnc);
            if (maxLevelupMoves <= 0) { error = "Couldn't read MAX_LEVELUP_MOVES from the checkout's generated header."; return false; }

            Narc narc = Narc.Open(learnsetNarcPath);
            if (narc == null || narc.ElementCount == 0) { error = $"Couldn't read the built NARC: {learnsetNarcPath}"; return false; }
            byte[] table = narc.GetElementBytes(0);

            int rowBytes = maxLevelupMoves * 4;
            int rowCount = table.Length / rowBytes;

            List<byte[]> rows = new List<byte[]>(totalSpeciesCount);
            for (int species = 0; species < totalSpeciesCount; species++)
                rows.Add(species < rowCount ? ConvertRow(table, species * rowBytes, maxLevelupMoves) : BitConverter.GetBytes(Terminator));
            ArchiveMembers.ReplaceAll(unpackedDir, rows);
            return true;
        }

        /// <summary>One species' row, trimmed after hg-engine's terminator, entries kept as u32.</summary>
        internal static byte[] ConvertRow(byte[] table, int rowStart, int maxLevelupMoves)
        {
            using MemoryStream mem = new MemoryStream();
            using BinaryWriter writer = new BinaryWriter(mem);

            for (int i = 0; i < maxLevelupMoves && rowStart + i * 4 + 4 <= table.Length; i++)
            {
                uint raw = BitConverter.ToUInt32(table, rowStart + i * 4);
                if ((raw & 0xFFFF) == 0xFFFF) break;
                writer.Write(raw);
            }
            writer.Write(Terminator);
            return mem.ToArray();
        }

        private static int ReadMaxLevelupMoves(string repoUnc)
        {
            string path = Path.Combine(repoUnc, "include", "constants", "generated", "learnsets.h");
            if (!File.Exists(path)) return -1;
            return CLexer.TryParseInt(CSourceFile.For(HgEngineFileCache.GetText(path)).FindDefine("MAX_LEVELUP_MOVES")?.Body, out int max) ? max : -1;
        }

        private static string LevelKey(int speciesId) => "level:" + speciesId;

        private static string LevelText(IEnumerable<(int level, int move)> list) => string.Join(" ", list.Select(m => $"{m.level}:{m.move}"));

        /// <summary>Same as <see cref="TryGetLevelMoves"/>, for an editor that will save this list. The save compares
        /// against what this loads.</summary>
        public static bool TryGetLevelMovesForEditing(int speciesId, out List<(int level, int move)> moves, out string error)
        {
            if (!TryGetLevelMoves(speciesId, out moves, out error)) return false;
            HgEngineLoadedEntries.Store(LevelsPath, LevelKey(speciesId), LevelText(moves));
            return true;
        }

        /// <summary>Same as <see cref="TryGetAllLevelMoves"/>, for an editor that will save these lists.</summary>
        public static bool TryGetAllLevelMovesForEditing(out Dictionary<int, List<(int level, int move)>> lists, out string error)
        {
            if (!TryGetAllLevelMoves(out lists, out error)) return false;
            foreach ((int id, List<(int level, int move)> list) in lists) HgEngineLoadedEntries.Store(LevelsPath, LevelKey(id), LevelText(list));
            return true;
        }

        private static string LevelsPath => Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', Path.DirectorySeparatorChar));

        /// <summary>Every species' own level-up list in one pass, keyed by species id; forms that inherit are absent.</summary>
        public static bool TryGetAllLevelMoves(out Dictionary<int, List<(int level, int move)>> lists, out string error)
        {
            lists = new Dictionary<int, List<(int level, int move)>>();
            if (!TryLoad(out string json, out HgEngineSymbolTable species, out HgEngineSymbolTable moveTable, out _, out error)) return false;
            List<JsonText.Member> root = JsonText.Members(json, 0);
            if (root == null) { error = "learnsets.json isn't a JSON object."; return false; }
            foreach (JsonText.Member member in root)
            {
                if (!species.TryGetValue(member.Key, out int id) || lists.ContainsKey(id)) continue;
                List<(int level, int move)> list = new List<(int level, int move)>();
                if (!TryReadList(json, root, member.Key, moveTable, list, out error)) return false;
                if (list.Count > 0) lists[id] = list;
            }
            return true;
        }

        /// <summary>Writes one species' level-up list into the linked checkout's learnsets.json.</summary>
        public static bool TrySaveLevelMoves(int speciesId, IReadOnlyList<(int level, int move)> entries, out string error) =>
            TrySaveLevelMoves(new Dictionary<int, IReadOnlyList<(int level, int move)>> { [speciesId] = entries }, out error);

        /// <summary>Writes several species' level-up lists, writing the file once.</summary>
        public static bool TrySaveLevelMoves(IReadOnlyDictionary<int, IReadOnlyList<(int level, int move)>> lists, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }

            HgEngineSymbolTable species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            HgEngineSymbolTable moves = HgEngineSymbolTable.Load(MovesHeaderRelPath);
            if (species == null || moves == null) { error = "Couldn't read species.h or moves.h from the checkout."; return false; }

            string path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) { error = $"{path} is missing from the checkout."; return false; }

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
                string text = new UTF8Encoding(false).GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));

                Dictionary<string, string> formToBase = ParseFormToBase(HgEngineFileCache.GetText(Path.Combine(HgEngineProject.RepoPathUnc, FormMapRelPath.Replace('/', Path.DirectorySeparatorChar))));
                List<JsonText.Member> root = JsonText.Members(text, 0);
                foreach (int speciesId in lists.Keys)
                {
                    // Nothing loaded for it means nothing to compare.
                    if (root == null || HgEngineLoadedEntries.Get(path, LevelKey(speciesId)) == null) continue;
                    List<(int level, int move)> onDisk = new List<(int level, int move)>();
                    if (!TryReadLevelMoves(text, root, speciesId, species, moves, formToBase, onDisk, out _)) continue;
                    if (!HgEngineLoadedEntries.ChangedOutside(path, LevelKey(speciesId), LevelText(onDisk))) continue;
                    error = HgEngineLoadedEntries.ConflictMessage("learnsets.json", new[] { species.TryGetNameWithPrefix(speciesId, "SPECIES_", out string name) ? name : speciesId.ToString() });
                    return false;
                }

                string updated = text;
                foreach ((int speciesId, IReadOnlyList<(int level, int move)> entries) in lists)
                    if (!TryApplyLevelMoves(updated, speciesId, entries, species, moves, out updated, out error))
                    {
                        error = lists.Count > 1 ? $"Species {speciesId}: {error}" : error;
                        return false;
                    }
                if (updated == text) return true;

                HgEngineFileCache.WriteText(path, updated, new UTF8Encoding(bom));
                // What a later read sees. A form saved empty reads its base's list, which isn't an outside change.
                List<JsonText.Member> written = JsonText.Members(updated, 0);
                foreach ((int speciesId, IReadOnlyList<(int level, int move)> entries) in lists)
                {
                    List<(int level, int move)> back = new List<(int level, int move)>();
                    bool read = written != null && TryReadLevelMoves(updated, written, speciesId, species, moves, formToBase, back, out _);
                    HgEngineLoadedEntries.Remember(path, LevelKey(speciesId), LevelText(read ? back : entries));
                }
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>Resolves names, checks the build's row limit and splices the list into the JSON text.</summary>
        internal static bool TryApplyLevelMoves(string json, int speciesId, IReadOnlyList<(int level, int move)> entries,
            HgEngineSymbolTable species, HgEngineSymbolTable moves, out string updated, out string error)
        {
            updated = null;
            error = null;

            if (entries.Count + 1 > BuildMaxRowSlots)
            {
                error = $"Too many level-up moves ({entries.Count}). hg-engine allows at most {BuildMaxRowSlots - 1}.";
                return false;
            }

            List<JsonText.Member> root = JsonText.Members(json, 0);
            if (root == null) { error = "learnsets.json isn't a JSON object."; return false; }

            // A species can have alias names; the file's own key wins, and a missing key means a form that
            // currently inherits its base species' list.
            string key = species.ByName
                .Where(kv => kv.Value == speciesId && kv.Key.StartsWith("SPECIES_", StringComparison.Ordinal))
                .Select(kv => kv.Key)
                .FirstOrDefault(n => root.Any(m => m.Key == n));
            if (key == null && !species.TryGetNameWithPrefix(speciesId, "SPECIES_", out key))
            {
                error = $"No SPECIES_ name for species {speciesId}.";
                return false;
            }

            string newline = json.Contains("\r\n") ? "\r\n" : "\n";
            string unit = JsonText.IndentUnit(json);

            JsonText.Member existing = root.FirstOrDefault(m => m.Key == key);
            Dictionary<int, string> keptNames = new();
            if (existing != null)
                foreach (string name in ExistingMoveNames(json, existing))
                    if (moves.TryGetValue(name, out int id)) keptNames.TryAdd(id, name);

            List<(int level, string move)> names = new List<(int level, string move)>(entries.Count);
            foreach ((int level, int move) in entries)
            {
                if (level < 0 || level > 0xFFFF) { error = $"Level {level} is out of range."; return false; }
                if (!keptNames.TryGetValue(move, out string moveName) && !moves.TryGetNameWithPrefix(move, "MOVE_", out moveName))
                {
                    error = $"No MOVE_ name for move {move}.";
                    return false;
                }
                names.Add((level, moveName));
            }

            if (existing != null)
            {
                if (json[existing.ValueStart] != '{') { error = $"{key} isn't an object in learnsets.json."; return false; }
                List<JsonText.Member> fields = JsonText.Members(json, existing.ValueStart);
                if (fields == null) { error = $"Couldn't read {key} in learnsets.json."; return false; }
                JsonText.Member levelMoves = fields.FirstOrDefault(m => m.Key == "LevelMoves");
                string outer = LineIndent(json, existing.KeyStart);

                if (levelMoves != null)
                {
                    if (json[levelMoves.ValueStart] != '[') { error = $"{key} LevelMoves isn't a list."; return false; }
                    string list = RenderList(names, LineIndent(json, levelMoves.KeyStart), unit, newline);
                    updated = json.Substring(0, levelMoves.ValueStart) + list + json.Substring(levelMoves.ValueEnd);
                }
                else
                {
                    string member = $"\"LevelMoves\": {RenderList(names, outer + unit, unit, newline)}";
                    updated = JsonText.InsertMember(json, existing.ValueStart, existing.ValueEnd, fields, member, outer + unit, outer, newline);
                }
            }
            else
            {
                string member = $"\"{key}\": {{{newline}{unit}{unit}\"LevelMoves\": {RenderList(names, unit + unit, unit, newline)}{newline}{unit}}}";
                int rootEnd = JsonText.SkipValue(json, JsonText.SkipWs(json, 0));
                updated = JsonText.InsertMember(json, JsonText.SkipWs(json, 0), rootEnd, root, member, unit, "", newline);
            }
            return true;
        }

        private static IEnumerable<string> ExistingMoveNames(string json, JsonText.Member species)
        {
            List<JsonText.Member> fields = JsonText.Members(json, species.ValueStart);
            JsonText.Member levelMoves = fields?.FirstOrDefault(m => m.Key == "LevelMoves");
            if (levelMoves == null) yield break;
            string previous = null;
            foreach (string text in JsonText.Strings(json, levelMoves.ValueStart, levelMoves.ValueEnd))
            {
                if (previous == "Move") yield return text;
                previous = text;
            }
        }

        // Matches Python's json.dump(indent=N), which is how hg-engine writes this file.
        private static string RenderList(IReadOnlyList<(int level, string move)> entries, string indent, string unit, string newline)
        {
            if (entries.Count == 0) return "[]";
            string item = indent + unit, field = item + unit;
            StringBuilder sb = new StringBuilder("[");
            for (int i = 0; i < entries.Count; i++)
            {
                sb.Append(newline).Append(item).Append('{')
                  .Append(newline).Append(field).Append("\"Level\": ").Append(entries[i].level).Append(',')
                  .Append(newline).Append(field).Append("\"Move\": \"").Append(entries[i].move).Append('"')
                  .Append(newline).Append(item).Append('}');
                if (i < entries.Count - 1) sb.Append(',');
            }
            return sb.Append(newline).Append(indent).Append(']').ToString();
        }

        private static string LineIndent(string text, int index) => HgEngineSourcePatcher.IndentOfLine(text, index);
    }
}

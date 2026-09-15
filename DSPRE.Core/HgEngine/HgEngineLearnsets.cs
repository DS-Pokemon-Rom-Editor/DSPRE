using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }

            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            var moveTable = HgEngineSymbolTable.Load(MovesHeaderRelPath);
            if (species == null || moveTable == null) { error = "Could not read species.h or moves.h from the checkout."; return false; }

            string path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', '\\'));
            string formPath = Path.Combine(HgEngineProject.RepoPathUnc, FormMapRelPath.Replace('/', '\\'));
            if (!File.Exists(path)) { error = $"Source file not found: {path}"; return false; }
            if (!File.Exists(formPath)) { error = $"Source file not found: {formPath}"; return false; }

            try
            {
                var formToBase = ParseFormToBase(HgEngineFileCache.GetText(formPath));
                return TryReadLevelMoves(HgEngineFileCache.GetText(path), speciesId, species, moveTable, formToBase, out moves, out error);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                error = ex.Message;
                return false;
            }
        }

        // The same per-line pattern build_learnsets.py matches.
        private static readonly Regex FormMapLine = new(@"\[(SPECIES_\w+)\s*-\s*SPECIES_MEGA_START\]\s*=\s*(SPECIES_\w+),");

        internal static Dictionary<string, string> ParseFormToBase(string text)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string line in text.Split('\n'))
            {
                Match m = FormMapLine.Match(line);
                if (m.Success) map[m.Groups[1].Value] = m.Groups[2].Value;
            }
            return map;
        }

        /// <summary>Pure half of <see cref="TryGetLevelMoves"/>.</summary>
        internal static bool TryReadLevelMoves(string json, int speciesId, HgEngineSymbolTable species, HgEngineSymbolTable moves,
            IReadOnlyDictionary<string, string> formToBase, out List<(int level, int move)> list, out string error)
        {
            list = new List<(int level, int move)>();
            error = null;

            var root = JsonSpan.Members(json, 0);
            if (root == null) { error = "learnsets.json is not a JSON object."; return false; }

            // Same key choice as the save: the file's own alias wins.
            var names = species.ByName
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

        private static bool TryReadList(string json, List<JsonSpan.Member> root, string key, HgEngineSymbolTable moves,
            List<(int level, int move)> list, out string error)
        {
            error = null;
            var entry = root.FirstOrDefault(m => m.Key == key);
            if (entry == null) return true;
            if (json[entry.ValueStart] != '{') { error = $"{key} is not an object in learnsets.json."; return false; }
            var fields = JsonSpan.Members(json, entry.ValueStart);
            if (fields == null) { error = $"Could not read {key} in learnsets.json."; return false; }
            var levelMoves = fields.FirstOrDefault(m => m.Key == "LevelMoves");
            if (levelMoves == null) return true;
            if (json[levelMoves.ValueStart] != '[') { error = $"{key} LevelMoves is not a list."; return false; }

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json.AsMemory(levelMoves.ValueStart, levelMoves.ValueEnd - levelMoves.ValueStart));
                int index = 0;
                foreach (var row in doc.RootElement.EnumerateArray())
                {
                    index++;
                    if (row.ValueKind != System.Text.Json.JsonValueKind.Object || !row.TryGetProperty("Level", out var levelValue) || !TryReadLevel(levelValue, out int level))
                    {
                        error = $"{key} level-up move {index} has no readable Level.";
                        return false;
                    }
                    string move = row.TryGetProperty("Move", out var moveValue) && moveValue.ValueKind == System.Text.Json.JsonValueKind.String
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
                error = $"{key} LevelMoves could not be read: {ex.Message}";
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

        public const uint Terminator = 0x0000FFFF;

        // build_learnsets.py regenerates MAX_LEVELUP_MOVES from the longest list, but refuses rows past 64 slots.
        public const int BuildMaxRowSlots = 64;

        /// <summary>Rebuilds every species' learnset file from hg-engine's combined table.
        /// Species past the table's own row count get an empty file instead of a missing one.</summary>
        public static bool Sync(string learnsetNarcPath, string repoUnc, string unpackedDir, int totalSpeciesCount, out string error)
        {
            error = null;
            int maxLevelupMoves = ReadMaxLevelupMoves(repoUnc);
            if (maxLevelupMoves <= 0) { error = "Could not read MAX_LEVELUP_MOVES from the checkout's generated header."; return false; }

            Narc narc = Narc.Open(learnsetNarcPath);
            if (narc == null || narc.ElementCount == 0) { error = $"Failed to parse built narc: {learnsetNarcPath}"; return false; }
            byte[] table = narc.GetElementBytes(0);

            int rowBytes = maxLevelupMoves * 4;
            int rowCount = table.Length / rowBytes;

            if (Directory.Exists(unpackedDir)) Directory.Delete(unpackedDir, true);
            Directory.CreateDirectory(unpackedDir);

            for (int species = 0; species < totalSpeciesCount; species++)
            {
                byte[] outBytes = species < rowCount
                    ? ConvertRow(table, species * rowBytes, maxLevelupMoves)
                    : BitConverter.GetBytes(Terminator);
                File.WriteAllBytes(Path.Combine(unpackedDir, species.ToString("D4")), outBytes);
            }
            return true;
        }

        /// <summary>One species' row, trimmed after hg-engine's terminator, entries kept as u32.</summary>
        internal static byte[] ConvertRow(byte[] table, int rowStart, int maxLevelupMoves)
        {
            using var mem = new MemoryStream();
            using var writer = new BinaryWriter(mem);

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
            var m = Regex.Match(File.ReadAllText(path), @"#define\s+MAX_LEVELUP_MOVES\s+(\d+)");
            return m.Success ? int.Parse(m.Groups[1].Value) : -1;
        }

        /// <summary>Writes one species' level-up list into the linked checkout's learnsets.json.</summary>
        public static bool TrySaveLevelMoves(int speciesId, IReadOnlyList<(int level, int move)> entries, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return false; }

            var species = HgEngineSymbolTable.Load(SpeciesHeaderRelPath);
            var moves = HgEngineSymbolTable.Load(MovesHeaderRelPath);
            if (species == null || moves == null) { error = "Could not read species.h or moves.h from the checkout."; return false; }

            string path = Path.Combine(HgEngineProject.RepoPathUnc, SourceRelPath.Replace('/', '\\'));
            if (!File.Exists(path)) { error = $"Source file not found: {path}"; return false; }

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
                string text = new UTF8Encoding(false).GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));

                if (!TryApplyLevelMoves(text, speciesId, entries, species, moves, out string updated, out error)) return false;
                if (updated == text) return true;

                HgEngineFileCache.WriteText(path, updated, new UTF8Encoding(bom));
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

            var root = JsonSpan.Members(json, 0);
            if (root == null) { error = "learnsets.json is not a JSON object."; return false; }

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
            string unit = DetectIndentUnit(json);

            var existing = root.FirstOrDefault(m => m.Key == key);
            Dictionary<int, string> keptNames = new();
            if (existing != null)
                foreach (string name in ExistingMoveNames(json, existing))
                    if (moves.TryGetValue(name, out int id)) keptNames.TryAdd(id, name);

            var names = new List<(int level, string move)>(entries.Count);
            foreach (var (level, move) in entries)
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
                if (json[existing.ValueStart] != '{') { error = $"{key} is not an object in learnsets.json."; return false; }
                var fields = JsonSpan.Members(json, existing.ValueStart);
                if (fields == null) { error = $"Could not read {key} in learnsets.json."; return false; }
                var levelMoves = fields.FirstOrDefault(m => m.Key == "LevelMoves");
                string outer = LineIndent(json, existing.KeyStart);

                if (levelMoves != null)
                {
                    if (json[levelMoves.ValueStart] != '[') { error = $"{key} LevelMoves is not a list."; return false; }
                    string list = RenderList(names, LineIndent(json, levelMoves.KeyStart), unit, newline);
                    updated = json.Substring(0, levelMoves.ValueStart) + list + json.Substring(levelMoves.ValueEnd);
                }
                else
                {
                    string member = $"\"LevelMoves\": {RenderList(names, outer + unit, unit, newline)}";
                    updated = InsertMember(json, existing.ValueStart, existing.ValueEnd, fields, member, outer + unit, outer, newline);
                }
            }
            else
            {
                string member = $"\"{key}\": {{{newline}{unit}{unit}\"LevelMoves\": {RenderList(names, unit + unit, unit, newline)}{newline}{unit}}}";
                int rootEnd = JsonSpan.SkipValue(json, JsonSpan.SkipWs(json, 0));
                updated = InsertMember(json, JsonSpan.SkipWs(json, 0), rootEnd, root, member, unit, "", newline);
            }
            return true;
        }

        private static IEnumerable<string> ExistingMoveNames(string json, JsonSpan.Member species)
        {
            var fields = JsonSpan.Members(json, species.ValueStart);
            var levelMoves = fields?.FirstOrDefault(m => m.Key == "LevelMoves");
            if (levelMoves == null) yield break;
            foreach (Match m in Regex.Matches(json.Substring(levelMoves.ValueStart, levelMoves.ValueEnd - levelMoves.ValueStart),
                         "\"Move\"\\s*:\\s*\"([^\"]+)\""))
                yield return m.Groups[1].Value;
        }

        // Matches Python's json.dump(indent=N), which is how hg-engine writes this file.
        private static string RenderList(IReadOnlyList<(int level, string move)> entries, string indent, string unit, string newline)
        {
            if (entries.Count == 0) return "[]";
            string item = indent + unit, field = item + unit;
            var sb = new StringBuilder("[");
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

        private static string InsertMember(string json, int objStart, int objEnd, List<JsonSpan.Member> members,
            string member, string memberIndent, string closeIndent, string newline)
        {
            if (members == null || members.Count == 0)
                return json.Substring(0, objStart + 1) + newline + memberIndent + member + newline + closeIndent + json.Substring(objEnd - 1);
            int after = members[^1].ValueEnd;
            return json.Substring(0, after) + "," + newline + memberIndent + member + json.Substring(after);
        }

        private static string LineIndent(string text, int index)
        {
            int lineStart = text.LastIndexOf('\n', Math.Max(0, index - 1)) + 1;
            int i = lineStart;
            while (i < index && (text[i] == ' ' || text[i] == '\t')) i++;
            return text.Substring(lineStart, i - lineStart);
        }

        private static string DetectIndentUnit(string json)
        {
            var m = Regex.Match(json, "\\{\\r?\\n([ \\t]+)\"");
            return m.Success ? m.Groups[1].Value : "  ";
        }

        /// <summary>Minimal string-aware scanner that reports character offsets, which System.Text.Json
        /// does not, so edits can splice the original text instead of reserializing 3 MB of JSON.</summary>
        internal static class JsonSpan
        {
            internal sealed class Member
            {
                public string Key;
                public int KeyStart;
                public int ValueStart;
                public int ValueEnd;   // exclusive
            }

            public static List<Member> Members(string s, int objStart)
            {
                int i = SkipWs(s, objStart);
                if (i >= s.Length || s[i] != '{') return null;
                var result = new List<Member>();
                i = SkipWs(s, i + 1);
                if (i < s.Length && s[i] == '}') return result;
                while (i < s.Length)
                {
                    if (s[i] != '"') return null;
                    int keyStart = i;
                    int keyEnd = SkipString(s, i);
                    if (keyEnd < 0) return null;
                    string key = s.Substring(keyStart + 1, keyEnd - keyStart - 2);
                    i = SkipWs(s, keyEnd);
                    if (i >= s.Length || s[i] != ':') return null;
                    int valueStart = SkipWs(s, i + 1);
                    int valueEnd = SkipValue(s, valueStart);
                    if (valueEnd < 0) return null;
                    result.Add(new Member { Key = key, KeyStart = keyStart, ValueStart = valueStart, ValueEnd = valueEnd });
                    i = SkipWs(s, valueEnd);
                    if (i < s.Length && s[i] == ',') { i = SkipWs(s, i + 1); continue; }
                    if (i < s.Length && s[i] == '}') return result;
                    return null;
                }
                return null;
            }

            public static int SkipWs(string s, int i)
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
                return i;
            }

            private static int SkipString(string s, int i)
            {
                for (i++; i < s.Length; i++)
                {
                    if (s[i] == '\\') { i++; continue; }
                    if (s[i] == '"') return i + 1;
                }
                return -1;
            }

            public static int SkipValue(string s, int i)
            {
                if (i >= s.Length) return -1;
                if (s[i] == '"') return SkipString(s, i);
                if (s[i] == '{' || s[i] == '[')
                {
                    int depth = 0;
                    for (; i < s.Length; i++)
                    {
                        char c = s[i];
                        if (c == '"') { i = SkipString(s, i); if (i < 0) return -1; i--; continue; }
                        if (c == '{' || c == '[') depth++;
                        else if ((c == '}' || c == ']') && --depth == 0) return i + 1;
                    }
                    return -1;
                }
                while (i < s.Length && s[i] != ',' && s[i] != '}' && s[i] != ']' && !char.IsWhiteSpace(s[i])) i++;
                return i;
            }
        }
    }
}

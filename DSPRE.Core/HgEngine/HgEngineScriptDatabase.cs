using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// The script command database an hg-engine folder project's scripts are read and written with: HGSS's, with the
    /// checkout's own flag names where HGSS's leaves a flag unnamed (FLAG_UNK_*). hg-engine keeps every vanilla command
    /// and layout, so commands are unchanged; what it adds are flags such as HIDDEN_ABILITIES_FLAG, whose numbers each
    /// checkout sets in include/config.h.
    /// </summary>
    public static class HgEngineScriptDatabase
    {
        private static readonly Regex ConfigFlag = new(@"^\s*#define\s+(\w*FLAG\w*)\s+(0x[0-9A-Fa-f]+|\d+)\b", RegexOptions.Multiline);
        private static readonly Regex EquFlag = new(@"^\s*\.equ\s+(\w+)\s*,\s*(0x[0-9A-Fa-f]+|\d+)\b", RegexOptions.Multiline);

        /// <summary>Flag names the checkout defines, by number: config.h first, then asm/include/flags.inc.</summary>
        public static Dictionary<int, string> CheckoutFlags()
        {
            Dictionary<int, string> flags = new Dictionary<int, string>();
            if (!HgEngineProject.IsActive) return flags;
            void Read(string rel, Regex pattern)
            {
                string path = Path.Combine(HgEngineProject.RepoRootWindows, rel.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path)) return;
                foreach (Match m in pattern.Matches(File.ReadAllText(path)))
                {
                    string name = m.Groups[1].Value;
                    if (name.StartsWith("NUM_", StringComparison.Ordinal) || name.EndsWith("_BASE", StringComparison.Ordinal)) continue;
                    int value = Convert.ToInt32(m.Groups[2].Value, m.Groups[2].Value.StartsWith("0x") ? 16 : 10);
                    flags.TryAdd(value, name);
                }
            }
            Read("include/config.h", ConfigFlag);
            Read("asm/include/flags.inc", EquFlag);
            return flags;
        }

        /// <summary>The v2 database with the checkout's names for flags it leaves unnamed, or the text unchanged.</summary>
        public static byte[] Overlay(byte[] v2)
        {
            Dictionary<int, string> flags = CheckoutFlags();
            if (flags.Count == 0) return v2;
            JsonNode root;
            try { root = JsonNode.Parse(v2); }
            catch (JsonException) { return v2; }
            if (root?["flags"] is not JsonObject table) return v2;

            Dictionary<int, string> byId = new Dictionary<int, string>();
            foreach ((string name, JsonNode node) in table)
                if (node?["id"] is JsonValue v && v.TryGetValue(out int id)) byId.TryAdd(id, name);

            bool changed = false;
            foreach ((int id, string name) in flags.OrderBy(f => f.Key))
            {
                if (table.ContainsKey(name)) continue;
                byId.TryGetValue(id, out string current);
                if (current != null && !current.StartsWith("FLAG_UNK_", StringComparison.Ordinal)) continue;
                if (current != null) table.Remove(current);
                table[name] = new JsonObject { ["id"] = id };
                byId[id] = name;
                changed = true;
            }
            if (!changed) return v2;
            string text = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            return Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n").Replace("\n", "\r\n"));
        }
    }
}

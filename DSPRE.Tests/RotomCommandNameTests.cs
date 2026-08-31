using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DSPRE;
using DSPRE.Resources;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// Script commands are named the way the editor names them.
    ///
    /// There are two databases side by side. The old one keys commands by hex id and calls command 0
    /// "Nop"; the newer v2 file keys them by the rotom name, calls it "Noop", and keeps the old one as
    /// legacy_name. The script editor, the rotom formatter and the language server all use the rotom
    /// names, so those are the words somebody sees and types, and showing the old ones puts names on
    /// screen that do not exist in their editor.
    ///
    /// This reads both files itself and requires the loaded database to agree with the v2 one. It can
    /// fail: if the loader went back to the old names, every renamed command would mismatch and be
    /// listed.
    /// </summary>
    public class RotomCommandNameTests
    {
        private readonly ITestOutputHelper _out;
        public RotomCommandNameTests(ITestOutputHelper o) { _out = o; }

        private static readonly string Databases =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DSPRE", "databases");

        private static readonly (string Legacy, string V2, string Game)[] Pairs =
        {
            ("hgss_scrcmd_database.json", "hgss_v2.json", "HeartGold/SoulSilver"),
            ("platinum_scrcmd_database.json",             "platinum_v2.json", "Platinum"),
            ("diamond_pearl_scrcmd_database.json",        "diamond_pearl_v2.json", "Diamond/Pearl"),
        };

        private static Dictionary<ushort, string> LegacyNames(string path)
        {
            var map = new Dictionary<ushort, string>();
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("scrcmd", out var root)) return map;
            foreach (var p in root.EnumerateObject())
            {
                if (!p.Value.TryGetProperty("name", out var n)) continue;
                map[Convert.ToUInt16(p.Name.Substring(2), 16)] = n.GetString();
            }
            return map;
        }

        private static Dictionary<ushort, string> RotomNames(string path)
        {
            var map = new Dictionary<ushort, string>();
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("commands", out var root)) return map;
            foreach (var p in root.EnumerateObject())
            {
                // Script commands, movements and macros share one list and their ids overlap, so the
                // type has to be checked: without it movement 0 (FaceNorth) lands on script command 0.
                if (!p.Value.TryGetProperty("type", out var t) || t.GetString() != "script_cmd") continue;
                if (!p.Value.TryGetProperty("id", out var idElem)) continue;
                if (!idElem.TryGetInt32(out int id) || id < 0 || id > ushort.MaxValue) continue;
                map[(ushort)id] = p.Name;
            }
            return map;
        }

        [Fact]
        public void HowManyCommandNamesTheTwoDatabasesDisagreeOn()
        {
            Assert.True(Directory.Exists(Databases), "the script databases are not on this machine, so nothing was checked");

            int gamesChecked = 0, totalLegacy = 0, totalRotom = 0, totalDiffer = 0, onlyLegacy = 0;
            foreach (var (legacyFile, v2File, game) in Pairs)
            {
                string lp = Path.Combine(Databases, legacyFile), vp = Path.Combine(Databases, v2File);
                if (!File.Exists(lp) || !File.Exists(vp)) { _out.WriteLine($"{game}: one of the two files is missing, skipped"); continue; }

                var legacy = LegacyNames(lp);
                var rotom = RotomNames(vp);
                int differ = legacy.Count(k => rotom.TryGetValue(k.Key, out var r) && r != k.Value);
                int missing = legacy.Count(k => !rotom.ContainsKey(k.Key));

                gamesChecked++;
                totalLegacy += legacy.Count; totalRotom += rotom.Count;
                totalDiffer += differ; onlyLegacy += missing;

                _out.WriteLine($"{game}: {legacy.Count} commands in the old database, {rotom.Count} in v2; "
                               + $"{differ} are named differently, {missing} are in the old one only");
                foreach (var k in legacy.Where(k => rotom.TryGetValue(k.Key, out var r) && r != k.Value).Take(5))
                    _out.WriteLine($"   0x{k.Key:X4}: {k.Value} -> {rotom[k.Key]}");
            }

            Assert.True(gamesChecked > 0, "no game had both databases, so nothing was checked");
            _out.WriteLine($"TOTAL across {gamesChecked} games: {totalLegacy} old entries, {totalRotom} v2 entries, "
                           + $"{totalDiffer} renamed, {onlyLegacy} with no v2 name");
            Assert.True(totalDiffer > 0, "the two databases agree on every name, which means the v2 file was not read");
        }

        [Fact]
        public void TheLoadedDatabaseUsesTheRotomNames()
        {
            string legacy = Path.Combine(Databases, "hgss_scrcmd_database.json");
            string v2 = Path.Combine(Databases, "hgss_v2.json");
            Assert.True(File.Exists(legacy) && File.Exists(v2),
                "the HeartGold databases are not on this machine, so nothing was checked");

            ScriptDatabaseJsonLoader.InitializeFromJson(legacy, RomInfo.GameVersions.HeartGold);
            var loaded = ScriptDatabase.HGSSScrCmdInfo;
            Assert.True(loaded.Count > 100, $"only {loaded.Count} commands loaded, so nothing was really checked");

            var rotom = RotomNames(v2);
            var wrong = new List<string>();
            int checkedCount = 0, renamed = 0;
            foreach (var kv in loaded)
            {
                if (!rotom.TryGetValue(kv.Key, out var want)) continue;
                checkedCount++;
                if (kv.Value.Name != want) wrong.Add($"0x{kv.Key:X4}: shows {kv.Value.Name}, v2 says {want}");
                if (kv.Value.LegacyName != kv.Value.Name) renamed++;
            }

            _out.WriteLine($"{loaded.Count} commands loaded; {checkedCount} have a rotom name; "
                           + $"{renamed} now show a different name than the old database did");
            Assert.True(checkedCount > 100, $"only {checkedCount} commands could be compared");
            Assert.True(wrong.Count == 0,
                $"{wrong.Count} commands are still showing the old name: " + string.Join(", ", wrong.Take(8)));

            // The old name is kept so a project written before the rename can still be read back.
            Assert.All(loaded.Values, v => Assert.False(string.IsNullOrEmpty(v.LegacyName)));
        }
    }
}

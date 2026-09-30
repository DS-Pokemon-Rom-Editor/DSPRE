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
    /// <summary>Script commands are named the way the editor names them.</summary>
    // Rewrites ScriptDatabase's statics, which RomInfo also rebuilds on every ROM load
    // (RomInfo.cs:412), so this cannot run beside the tests that open ROMs.
    [Collection("rom")]
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

        [SkippableFact]
        public void APerRomDatabaseStillGetsTheRotomNames()
        {
            string legacy = Path.Combine(Databases, "hgss_scrcmd_database.json");
            string v2 = Path.Combine(AppPaths.DatabasePath, "hgss_v2.json");
            Skip.If(!File.Exists(legacy) || !File.Exists(v2), "the HeartGold databases are not on this machine");

            // The app loads edited_databases/<rom>/scrcmd_database.json, with no v2 file beside it.
            string root = Path.Combine(Path.GetTempPath(), "dspre_perrom_db_" + Guid.NewGuid().ToString("N"));
            string perRom = Path.Combine(root, "edited_databases", "HeartGold (USA)", "scrcmd_database.json");
            Directory.CreateDirectory(Path.GetDirectoryName(perRom));
            File.Copy(legacy, perRom);
            try
            {
                ScriptDatabaseJsonLoader.InitializeFromJson(perRom, RomInfo.GameVersions.HeartGold);
                var loaded = ScriptDatabase.HGSSScrCmdInfo;
                var rotom = RotomNames(v2);

                int renamed = loaded.Count(kv => kv.Value.LegacyName != kv.Value.Name
                                                 && rotom.TryGetValue(kv.Key, out var want) && kv.Value.Name == want);
                _out.WriteLine($"{loaded.Count} commands loaded from the per-ROM copy; {renamed} carry their v2 name");
                Assert.True(renamed > 0, "no command took its v2 name, so the v2 file was not found from the per-ROM path");

                // 0x4020 is VAR_OBJ_0; VAR_OBJ_GFX_BASE is the start of that range, not its name.
                Assert.True(ScriptDatabase.varNames.TryGetValue(0x4020, out string var4020), "0x4020 has no name");
                Assert.Equal("VAR_OBJ_0", var4020);
            }
            finally
            {
                try { Directory.Delete(root, recursive: true); } catch { }
            }
        }

        [Theory]
        [InlineData("VAR_OBJ_GFX_BASE", 0)]
        [InlineData("VAR_BASE", 0)]
        [InlineData("VAR_0x4020", 1)]
        [InlineData("VAR_SPECIAL_0x8000", 1)]
        [InlineData("VAR_OBJ_0", 2)]
        [InlineData("VAR_FOLLOWER_TRAINER_NUM", 2)]
        public void RangeMarkersRankBelowRealNames(string name, int expected)
            => Assert.Equal(expected, ScriptDatabaseJsonLoader.VarNameRank(name));
    }
}

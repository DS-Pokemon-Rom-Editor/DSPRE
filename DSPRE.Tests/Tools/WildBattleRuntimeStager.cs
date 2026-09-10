using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DSPRE;
using DSPRE.ROMFiles;
using NarcAPI;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>
    /// Builds disposable ROMs that start a scripted wild battle from a step trigger, for filming wild intros.
    /// Opt-in: DSPRE_WILD_STAGE (probe-heartgold, probe-platinum, heartgold, platinum) and DSPRE_WILD_OUTPUT;
    /// staging also reads _HEADER, _TRIGGER ("x,y,w,h"), _SCRIPT, _SPECIES, _LEVEL, _SHINY and _NAME.
    /// </summary>
    [Collection("rom")]
    public class WildBattleRuntimeStager
    {
        private readonly ITestOutputHelper _out;
        public WildBattleRuntimeStager(ITestOutputHelper output) => _out = output;

        private static readonly int[] HeartGoldProbeHeaders = { 300 };
        private static readonly int[] PlatinumProbeHeaders = { 414, 411 };

        [SkippableFact]
        public void ProbeHeartGold()
        {
            Skip.If(!ModeIs("probe-heartgold"), "DSPRE_WILD_STAGE is not probe-heartgold");
            Probe("IPKE", TestRoms.HeartGold, HeartGoldProbeHeaders, "heartgold");
        }

        [SkippableFact]
        public void ProbePlatinum()
        {
            Skip.If(!ModeIs("probe-platinum"), "DSPRE_WILD_STAGE is not probe-platinum");
            Probe("CPUE", TestRoms.Platinum, PlatinumProbeHeaders, "platinum");
        }

        [SkippableFact]
        public void StageHeartGold()
        {
            Skip.If(!ModeIs("heartgold"), "DSPRE_WILD_STAGE is not heartgold");
            Stage("IPKE", TestRoms.HeartGold, "heartgold");
        }

        [SkippableFact]
        public void StagePlatinum()
        {
            Skip.If(!ModeIs("platinum"), "DSPRE_WILD_STAGE is not platinum");
            Stage("CPUE", TestRoms.Platinum, "platinum");
        }

        private void Probe(string code, string project, int[] headers, string game)
        {
            string output = RequireOutputDirectory();
            Skip.If(!Directory.Exists(project), $"the {game} test project is not available");
            SettingsManager.Load();
            new RomInfo(code, project);
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.scripts, DirNames.eventFiles, DirNames.matrices, DirNames.maps });

            var lines = new List<string>();
            foreach (int id in headers)
            {
                MapHeader header = MapHeader.GetMapHeader((ushort)id);
                Assert.NotNull(header);
                var matrix = new GameMatrix(header.matrixID);
                lines.Add($"== Header {id}: matrix {header.matrixID} ({matrix.width}x{matrix.height}), events {header.eventFileID}, scripts {header.scriptFileID}, level scripts {header.levelScriptID}, wild {header.wildPokemon}");
                for (int my = 0; my < matrix.height; my++)
                    for (int mx = 0; mx < matrix.width; mx++)
                    {
                        ushort mapId = matrix.maps[my, mx];
                        if (mapId == GameMatrix.EMPTY) continue;
                        var map = new MapFile(mapId, gameFamily);
                        lines.Add($"-- Matrix cell ({mx},{my}) map {mapId}: . walkable, # blocked");
                        for (int row = 0; row < MapFile.mapSize; row++)
                        {
                            var line = new StringBuilder();
                            for (int column = 0; column < MapFile.mapSize; column++)
                                line.Append(map.collisions[row, column] == 0 ? '.' : '#');
                            lines.Add($"{row:D2} {line}");
                        }
                    }

                var events = new EventFile(header.eventFileID);
                lines.AddRange(events.warps.Select(w => $"Warp ({w.xMatrixPosition},{w.yMatrixPosition}) local ({w.xMapPosition},{w.yMapPosition}) -> header {w.header} anchor {w.anchor}"));
                lines.AddRange(events.overworlds.Select(o => $"Overworld {o.owID} local ({o.xMapPosition},{o.yMapPosition}) matrix ({o.xMatrixPosition},{o.yMatrixPosition}) type {o.type} script {o.scriptNumber} flag {o.flag} sight {o.sightRange} orient {o.orientation}"));
                lines.AddRange(events.triggers.Select(t => $"Trigger local ({t.xMapPosition},{t.yMapPosition}) matrix ({t.xMatrixPosition},{t.yMatrixPosition}) size {t.widthX}x{t.heightY} script {t.scriptNumber} var 0x{t.variableWatched:X4} == {t.expectedVarValue}"));
                lines.AddRange(events.spawnables.Select(s => $"Spawnable local ({s.xMapPosition},{s.yMapPosition}) script {s.scriptNumber}"));

                var script = new ScriptFile(header.scriptFileID);
                lines.Add($"Script file {header.scriptFileID}: {script.allScripts.Count} scripts, parse failed {script.parseFailedDueToInvalidCommand}");
                for (int i = 0; i < script.allScripts.Count; i++)
                {
                    var cmds = script.allScripts[i].commands;
                    string first = cmds == null ? "(uses another script)" : string.Join("; ", cmds.Take(6).Select(c => c.name));
                    lines.Add($"  script {i + 1}: {cmds?.Count ?? 0} commands: {first}");
                }
            }
            string file = Path.Combine(output, $"wild-probe-{game}.txt");
            File.WriteAllLines(file, lines);
            _out.WriteLine($"Wrote {file}");
        }

        private void Stage(string code, string project, string game)
        {
            string output = RequireOutputDirectory();
            int headerId = IntSetting("DSPRE_WILD_HEADER");
            int[] trigger = Environment.GetEnvironmentVariable("DSPRE_WILD_TRIGGER")?.Split(',').Select(int.Parse).ToArray();
            Assert.True(trigger != null && trigger.Length == 4, "DSPRE_WILD_TRIGGER must be x,y,width,height");
            int scriptIndex = IntSetting("DSPRE_WILD_SCRIPT");
            ushort species = checked((ushort)IntSetting("DSPRE_WILD_SPECIES"));
            ushort level = checked((ushort)IntSetting("DSPRE_WILD_LEVEL"));
            bool shiny = Environment.GetEnvironmentVariable("DSPRE_WILD_SHINY") == "1";
            string name = Environment.GetEnvironmentVariable("DSPRE_WILD_NAME") ?? $"wild-{game}";
            Assert.False(shiny && code != "IPKE", "only HeartGold's wild battle command takes a shiny flag");

            string work = CreateTemporaryProjectCopy(project, game);
            try
            {
                SettingsManager.Load();
                new RomInfo(code, work);
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.scripts, DirNames.eventFiles });

                MapHeader header = MapHeader.GetMapHeader((ushort)headerId);
                Assert.NotNull(header);

                // The script the trigger runs becomes: lock, the wild battle, release, end.
                var script = new ScriptFile(header.scriptFileID);
                Assert.False(script.parseFailedDueToInvalidCommand, "the map script did not parse completely, so it is unsafe to rewrite");
                Assert.InRange(scriptIndex, 1, script.allScripts.Count);
                var before = script.allScripts[scriptIndex - 1].commands?.Select(c => c.name).ToList() ?? new List<string>();

                var battleParams = new List<byte[]> { BitConverter.GetBytes(species), BitConverter.GetBytes(level) };
                // HeartGold's wild battle command with a shiny flag, and Platinum's plain one.
                ushort battleId = code == "IPKE" ? (ushort)589 : (ushort)292;
                if (code == "IPKE") battleParams.Add(new[] { (byte)(shiny ? 1 : 0) });
                script.allScripts[scriptIndex - 1].commands = new List<ScriptCommand>
                {
                    new ScriptCommand("LockAll"),
                    new ScriptCommand(battleId, battleParams),
                    new ScriptCommand("ReleaseAll"),
                    new ScriptCommand("End"),
                };
                byte[] stagedScript = script.ToByteArray();
                Assert.NotNull(stagedScript);
                var paths = ScriptFile.GetFilePaths(header.scriptFileID);
                File.WriteAllBytes(paths.binPath, stagedScript);
                File.WriteAllBytes(paths.txtPath, new UTF8Encoding(false).GetBytes(script.ToPlainText(includeActions: true)));
                ScriptFile.ClearPlaintextCache();

                var reopened = new ScriptFile(new MemoryStream(File.ReadAllBytes(paths.binPath)), fileID: header.scriptFileID);
                var staged = reopened.allScripts[scriptIndex - 1].commands;
                Assert.Equal(4, staged.Count);
                Assert.Equal(new ushort?[] { 96, battleId, 97, 2 }, staged.Select(c => c.id));
                ScriptCommand battle = staged[1];
                Assert.Equal(species, BitConverter.ToUInt16(battle.cmdParams[0], 0));
                Assert.Equal(level, BitConverter.ToUInt16(battle.cmdParams[1], 0));
                if (code == "IPKE") Assert.Equal(shiny ? 1 : 0, battle.cmdParams[2][0]);
                Assert.Equal(script.allScripts.Count, reopened.allScripts.Count);

                // Below 0x4000 the game reads a trigger's variable number as its value, so variable 0 compared
                // with 0 always matches, whatever the save holds.
                var events = new EventFile(header.eventFileID);
                int triggersBefore = events.triggers.Count;
                const ushort unusedTempVar = 0;
                Assert.DoesNotContain(events.triggers, t => t.variableWatched == unusedTempVar);
                events.triggers.Add(new Trigger(0, 0)
                {
                    scriptNumber = checked((ushort)scriptIndex),
                    xMapPosition = (short)trigger[0],
                    yMapPosition = (short)trigger[1],
                    widthX = checked((ushort)trigger[2]),
                    heightY = checked((ushort)trigger[3]),
                    zPosition = 0,
                    variableWatched = unusedTempVar,
                    expectedVarValue = 0,
                });
                File.WriteAllBytes(Filesystem.GetEventPath(header.eventFileID), events.ToByteArray());
                var reopenedEvents = new EventFile(new MemoryStream(File.ReadAllBytes(Filesystem.GetEventPath(header.eventFileID))));
                Assert.Equal(triggersBefore + 1, reopenedEvents.triggers.Count);
                Trigger added = Assert.Single(reopenedEvents.triggers, t => t.variableWatched == unusedTempVar);
                Assert.Equal((trigger[0], trigger[1], trigger[2], trigger[3], scriptIndex),
                    ((int)added.xMapPosition, (int)added.yMapPosition, (int)added.widthX, (int)added.heightY, (int)added.scriptNumber));
                Assert.Equal(events.overworlds.Count, reopenedEvents.overworlds.Count);
                Assert.Equal(events.warps.Count, reopenedEvents.warps.Count);

                RepackArchive(DirNames.scripts);
                RepackArchive(DirNames.eventFiles);
                Narc packedScripts = Narc.Open(gameDirs[DirNames.scripts].packedDir);
                Assert.Equal(stagedScript, packedScripts.GetElementBytes(header.scriptFileID));

                string rom = Path.Combine(output, name + ".nds");
                Assert.True(DSUtils.RepackROM(rom) && File.Exists(rom), "ROM repack failed");
                File.WriteAllLines(Path.Combine(output, name + ".txt"), new[]
                {
                    $"Wild battle runtime stage ({game})",
                    $"Header {headerId}: script file {header.scriptFileID}, event file {header.eventFileID}",
                    $"Script {scriptIndex}: [{string.Join(", ", before)}] -> [{string.Join(", ", staged.Select(c => c.name))}]",
                    $"Battle: species {species}, level {level}, shiny {shiny}",
                    $"Trigger: local ({trigger[0]},{trigger[1]}) size {trigger[2]}x{trigger[3]}, var 0x{unusedTempVar:X4} == 0; triggers {triggersBefore} -> {reopenedEvents.triggers.Count}",
                    $"ROM: {Path.GetFileName(rom)}",
                });
                _out.WriteLine($"Built {Path.GetFileName(rom)}");
            }
            finally
            {
                DeleteTemporaryProject(work);
            }
        }

        private static bool ModeIs(string expected) =>
            string.Equals(Environment.GetEnvironmentVariable("DSPRE_WILD_STAGE"), expected, StringComparison.OrdinalIgnoreCase);

        private static int IntSetting(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            Assert.False(string.IsNullOrWhiteSpace(value), $"{name} is required");
            return int.Parse(value);
        }

        private static string RequireOutputDirectory()
        {
            string output = Environment.GetEnvironmentVariable("DSPRE_WILD_OUTPUT");
            Assert.False(string.IsNullOrWhiteSpace(output), "DSPRE_WILD_OUTPUT is required");
            output = Path.GetFullPath(output);
            Directory.CreateDirectory(output);
            return output;
        }

        private static string Root => Path.Combine(Path.GetTempPath(), "DSPRE", "WildBattle");

        private static string CreateTemporaryProjectCopy(string source, string game)
        {
            Assert.True(Directory.Exists(source), $"the {game} test project is not available");
            string work = Path.Combine(Root, $"{game}-{Guid.NewGuid():N}");
            foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(work, Path.GetRelativePath(source, directory)));
            Directory.CreateDirectory(work);
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                File.Copy(file, Path.Combine(work, Path.GetRelativePath(source, file)));
            return work;
        }

        private static void DeleteTemporaryProject(string work)
        {
            string fullWork = Path.GetFullPath(work);
            string safeRoot = Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.StartsWith(safeRoot, fullWork, StringComparison.OrdinalIgnoreCase);
            if (Directory.Exists(fullWork)) Directory.Delete(fullWork, recursive: true);
        }

        private static void RepackArchive(DirNames archive)
        {
            var directory = gameDirs[archive];
            Narc.FromFolder(directory.unpackedDir).Save(directory.packedDir);
        }
    }
}

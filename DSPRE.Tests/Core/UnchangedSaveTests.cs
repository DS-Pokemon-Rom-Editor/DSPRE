using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DSPRE;
using DSPRE.Avalonia.ViewModels.Pokemon;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using Xunit;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>
    /// Loading every record of a writer and saving it unchanged, through the same calls its editor makes,
    /// must leave every file it can touch byte for byte as the game shipped it. The touched files are put
    /// back afterwards, so the test projects are never left edited.
    /// </summary>
    [Collection("rom")]
    public class UnchangedSaveTests
    {
        public static TheoryData<string> Games => new() { "Platinum", "HeartGold", "Diamond" };
        public static TheoryData<string> DpPt => new() { "Platinum", "Diamond" };

        private static void Open(string game)
        {
            var (path, code) = game switch
            {
                "Platinum" => (TestRoms.Platinum, "CPUE"),
                "HeartGold" => (TestRoms.HeartGold, "IPKE"),
                _ => (TestRoms.Diamond, "ADAE"),
            };
            Skip.If(!Directory.Exists(path), $"the {game} test project is not available");
            SettingsManager.Load();
            new RomInfo(code, path);
        }

        private static void Unpack(params DirNames[] archives) =>
            DSUtils.TryUnpackNarcs(archives.Where(a => gameDirs.ContainsKey(a)).ToList());

        private static string Dir(DirNames archive) => gameDirs[archive].unpackedDir;

        private static IEnumerable<(int Id, string Path)> Members(string dir) =>
            RomFiles.Settled(dir)
                .Select(p => (ok: int.TryParse(Path.GetFileName(p), out int id), id, p))
                .Where(t => t.ok)
                .Select(t => (t.id, t.p));

        /// <summary>
        /// Runs <paramref name="save"/>, which returns how many records it saved, and fails on any byte it
        /// changed in <paramref name="guarded"/> (folders or files), ARM9 or the overlays.
        /// </summary>
        private static void SavesUnchanged(string what, IEnumerable<string> guarded, Func<int> save)
        {
            var roots = guarded.Where(p => !string.IsNullOrEmpty(p)).Append(arm9Path).Append(overlayPath).Distinct().ToList();
            List<string> Files() => roots.SelectMany(r => Directory.Exists(r) ? Directory.GetFiles(r, "*", SearchOption.AllDirectories)
                                                         : File.Exists(r) ? new[] { r } : Array.Empty<string>()).ToList();

            var before = Files().ToDictionary(f => f, File.ReadAllBytes);
            var changes = new List<string>();
            int saved;
            try
            {
                saved = save();
                foreach (var (file, original) in before)
                {
                    if (!File.Exists(file)) { changes.Add($"{Name(file)} was deleted"); continue; }
                    byte[] now = File.ReadAllBytes(file);
                    if (!now.AsSpan().SequenceEqual(original)) changes.Add($"{Name(file)}: {Difference(original, now)}");
                }
                foreach (string file in Files().Where(f => !before.ContainsKey(f))) changes.Add($"{Name(file)} was created");
            }
            finally
            {
                foreach (string file in Files().Where(f => !before.ContainsKey(f))) File.Delete(file);
                foreach (var (file, original) in before)
                    if (!File.Exists(file) || !File.ReadAllBytes(file).AsSpan().SequenceEqual(original)) File.WriteAllBytes(file, original);
            }

            Assert.True(saved > 0, $"{what}: no records were saved, so nothing was checked");
            Assert.True(changes.Count == 0,
                $"{what}: saving {saved} unchanged records changed {changes.Count} files:\n" + string.Join("\n", changes.Take(12)));
        }

        private static string Name(string file) => Path.GetRelativePath(workDir, file);

        private static string Difference(byte[] was, byte[] now)
        {
            int n = Math.Min(was.Length, now.Length), at = 0;
            while (at < n && was[at] == now[at]) at++;
            return was.Length == now.Length
                ? $"first difference at 0x{at:X} ({was[at]:X2} became {now[at]:X2})"
                : $"{was.Length} bytes became {now.Length}, first difference at 0x{at:X}";
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void Trainers(string game)
        {
            Open(game);
            Unpack(DirNames.trainerProperties, DirNames.trainerParty);
            SavesUnchanged($"{game} trainers", new[] { Dir(DirNames.trainerProperties), Dir(DirNames.trainerParty) }, () =>
            {
                int n = 0;
                foreach (var (id, propPath) in Members(Dir(DirNames.trainerProperties)).ToList())
                {
                    string partyPath = Path.Combine(Dir(DirNames.trainerParty), Path.GetFileName(propPath));
                    if (!File.Exists(partyPath)) continue;
                    TrainerFile trainer;
                    using (var prop = File.OpenRead(propPath))
                    using (var party = File.OpenRead(partyPath))
                        trainer = new TrainerFile(new TrainerProperties((ushort)id, prop), party, "");
                    File.WriteAllBytes(propPath, trainer.trp.ToByteArray());
                    File.WriteAllBytes(partyPath, trainer.party.ToByteArray());
                    n++;
                }
                return n;
            });
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void WildEncounters(string game)
        {
            Open(game);
            Unpack(DirNames.encounters);
            SavesUnchanged($"{game} wild encounters", new[] { Dir(DirNames.encounters) }, () =>
            {
                int n = 0;
                foreach (var (id, _) in Members(Dir(DirNames.encounters)).ToList())
                {
                    EncounterFile file = gameFamily == GameFamilies.HGSS ? new EncounterFileHGSS(id) : new EncounterFileDPPt(id);
                    file.SaveToFileDefaultDir(id, showSuccessMessage: false);
                    n++;
                }
                return n;
            });
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void AreaData(string game)
        {
            Open(game);
            Unpack(DirNames.areaData);
            SavesUnchanged($"{game} area data", new[] { Dir(DirNames.areaData) }, () =>
            {
                int n = 0;
                foreach (var (id, path) in Members(Dir(DirNames.areaData)).ToList())
                {
                    ROMFiles.AreaData area;
                    using (var s = File.OpenRead(path)) area = new ROMFiles.AreaData(s);
                    area.SaveToFileDefaultDir(id, showSuccessMessage: false);
                    n++;
                }
                return n;
            });
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void Trades(string game)
        {
            Open(game);
            Unpack(DirNames.tradeData);
            SavesUnchanged($"{game} trades", new[] { Dir(DirNames.tradeData) }, () =>
            {
                int count = TradeData.GetTradeCount();
                for (int id = 0; id < count; id++) new TradeData(id).SaveToFileDefaultDir(id, showSuccessMessage: false);
                return count;
            });
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void Evolutions(string game)
        {
            Open(game);
            Unpack(DirNames.evolutions);
            SavesUnchanged($"{game} evolutions", new[] { Dir(DirNames.evolutions) }, () =>
            {
                int n = 0;
                foreach (var (id, _) in Members(Dir(DirNames.evolutions)).ToList())
                {
                    new EvolutionFile(id).SaveToFileDefaultDir(id, showSuccessMessage: false);
                    n++;
                }
                return n;
            });
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void EggMoves(string game)
        {
            Open(game);
            Unpack(DirNames.eggMoves);
            var guarded = gameDirs.ContainsKey(DirNames.eggMoves) ? new[] { Dir(DirNames.eggMoves) } : Array.Empty<string>();
            SavesUnchanged($"{game} egg moves", guarded, () =>
            {
                var editor = new EggMoveEditorViewModel();
                Task.Run(() => editor.SaveCommand()).GetAwaiter().GetResult();
                return EggMoveData.ReadFromRom().Count;
            });
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void MachineMoves(string game)
        {
            Open(game);
            SavesUnchanged($"{game} TM and HM moves", Array.Empty<string>(), () =>
            {
                int[] moves = TMEditor.ReadMachineMoves();
                TMEditor.WriteMachines(moves, TMEditor.ReadMachinePalettes());
                return moves.Length;
            });
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void BattleTower(string game)
        {
            Open(game);
            Skip.If(!gameDirs.ContainsKey(DirNames.battleTowerTrainers), $"{game} has no Battle Tower archives listed");
            Unpack(DirNames.battleTowerTrainers, DirNames.battleTowerPokemon);
            SavesUnchanged($"{game} Battle Tower", new[] { Dir(DirNames.battleTowerTrainers), Dir(DirNames.battleTowerPokemon) }, () =>
            {
                var trainers = new BattleTowerTrainerFile(load: true);
                var sets = new BattleTowerPokemonSetFile(load: true);
                Assert.True(trainers.SaveToNarc(showSuccessMessage: false) && sets.SaveToNarc(showSuccessMessage: false), "a Battle Tower save failed");
                return trainers.Trainers.Count + sets.Sets.Count;
            });
        }

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void Swarms(string game)
        {
            Open(game);
            string why = SwarmTable.WhyNot();
            Skip.If(why != null, $"{game} swarms: {why}");
            var editor = new SwarmsViewModel();
            editor.Setup();
            Assert.False(editor.HasProblem, $"{game}: the shipped swarm table reports a problem: {editor.Problem}");
            SavesUnchanged($"{game} swarms", Array.Empty<string>(), () =>
            {
                Assert.True(Task.Run(() => editor.SaveChangesAsync()).GetAwaiter().GetResult(), "the swarm save failed");
                return editor.Rows.Count;
            });
        }

        [SkippableTheory]
        [MemberData(nameof(DpPt))]
        public void SpecialEncounterFiles(string game)
        {
            Open(game);
            Unpack(DirNames.encounterExtended);
            SavesUnchanged($"{game} Trophy Garden, Great Marsh and Honey Tree", new[] { Filesystem.encounterExtended }, () =>
            {
                var garden = new TrophyGardenEncounterFile(load: true);
                var marsh = new GreatMarshEncounterFile(load: true);
                var honey = new HoneyTreeEncounterFile(load: true);
                Assert.True(garden.SaveToNarc(showSuccessMessage: false), "the Trophy Garden save failed");
                Assert.True(marsh.SaveToNarc(showSuccessMessage: false), "the Great Marsh save failed");
                Assert.True(honey.SaveToNarc(showSuccessMessage: false), "the Honey Tree save failed");
                return 3;
            });
        }

        [SkippableFact]
        public void HeadbuttAndSafariZone()
        {
            Open("HeartGold");
            Unpack(DirNames.headbutt, DirNames.safariZone);
            SavesUnchanged("HeartGold Headbutt and Safari Zone", new[] { Filesystem.headbutt, Filesystem.safariZone }, () =>
            {
                int n = 0;
                foreach (var (id, _) in Members(Filesystem.headbutt).ToList())
                {
                    Assert.True(new HeadbuttEncounterFile((ushort)id).SaveToFile(id), $"Headbutt file {id} did not save");
                    n++;
                }
                foreach (var (id, _) in Members(Filesystem.safariZone).ToList())
                {
                    Assert.True(new SafariZoneEncounterFile(id).SaveToFile(id), $"Safari Zone file {id} did not save");
                    n++;
                }
                return n;
            });
        }

        [SkippableFact]
        public void BugContest()
        {
            Open("HeartGold");
            string encounters = Filesystem.GetBugContestEncounterPath(), trainers = Filesystem.GetBugContestTrainerPath();
            Skip.If(!File.Exists(encounters) || !File.Exists(trainers), "the HeartGold Bug Contest files are not unpacked");
            SavesUnchanged("HeartGold Bug Contest", new[] { encounters, trainers }, () =>
            {
                Assert.True(new BugContestEncounterFile(load: true).SaveToFile(showSuccessMessage: false), "the Bug Contest encounter save failed");
                BugContestTrainerFile.Load(trainers).Save(trainers);
                return 2;
            });
        }
    }
}

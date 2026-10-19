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
    public partial class UnchangedSaveTests
    {
        public static TheoryData<string> Games => new() { "Platinum", "HeartGold", "Diamond" };
        public static TheoryData<string> DpPt => new() { "Platinum", "Diamond" };

        private static void Open(string game)
        {
            (string path, string code) = game switch
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
            DSUtils.TryUnpackNarcs(archives.Where(a => GameDirs.ContainsKey(a)).ToList());

        private static string Dir(DirNames archive) => GameDirs[archive].unpackedDir;

        private static IEnumerable<(int Id, string Path)> Members(string dir) =>
            RomFiles.Settled(dir)
                .Select(p => (ok: int.TryParse(Path.GetFileName(p), out int id), id, p))
                .Where(t => t.ok)
                .Select(t => (t.id, t.p));

        /// <summary>
        /// Runs <paramref name="save"/>, which returns how many records it saved, and fails on any byte it
        /// changed in <paramref name="guarded"/> (folders or files), ARM9 or the overlays.
        /// </summary>
        /// <param name="restored">Project files the save may rewrite in another form (expanded text or script sources); put back, not compared.</param>
        private static void SavesUnchanged(string what, IEnumerable<string> guarded, Func<int> save, IEnumerable<string> restored = null)
        {
            List<string> roots = guarded.Where(p => !string.IsNullOrEmpty(p)).Append(Arm9Path).Append(OverlayPath).Distinct().ToList();
            List<string> FilesUnder(IEnumerable<string> under) => under.SelectMany(r => Directory.Exists(r) ? Directory.GetFiles(r, "*", SearchOption.AllDirectories)
                                                         : File.Exists(r) ? new[] { r } : Array.Empty<string>()).ToList();
            List<string> Files() => FilesUnder(roots);

            List<string> extraRoots = (restored ?? Array.Empty<string>()).Where(p => !string.IsNullOrEmpty(p)).ToList();
            Dictionary<string, byte[]> extra = FilesUnder(extraRoots).Where(f => !roots.Any(r => f.StartsWith(r, StringComparison.OrdinalIgnoreCase)))
                .ToDictionary(f => f, File.ReadAllBytes);
            Dictionary<string, byte[]> before = Files().ToDictionary(f => f, File.ReadAllBytes);
            List<string> changes = new List<string>();
            int saved;
            try
            {
                saved = save();
                foreach ((string file, byte[] original) in before)
                {
                    if (!File.Exists(file)) { changes.Add($"{Name(file)} was deleted"); continue; }
                    byte[] now = File.ReadAllBytes(file);
                    if (!now.AsSpan().SequenceEqual(original)) changes.Add($"{Name(file)}: {Difference(original, now)}");
                }
                foreach (string file in Files().Where(f => !before.ContainsKey(f))) changes.Add($"{Name(file)} was created");
            }
            finally
            {
                foreach (string file in FilesUnder(extraRoots).Where(f => !extra.ContainsKey(f) && !before.ContainsKey(f))) File.Delete(file);
                foreach ((string file, byte[] original) in extra)
                    if (!File.Exists(file) || !File.ReadAllBytes(file).AsSpan().SequenceEqual(original)) File.WriteAllBytes(file, original);
                foreach (string file in Files().Where(f => !before.ContainsKey(f))) File.Delete(file);
                foreach ((string file, byte[] original) in before)
                    if (!File.Exists(file) || !File.ReadAllBytes(file).AsSpan().SequenceEqual(original)) File.WriteAllBytes(file, original);
            }

            Assert.True(saved > 0, $"{what}: no records were saved, so nothing was checked");
            Assert.True(changes.Count == 0,
                $"{what}: saving {saved} unchanged records changed {changes.Count} files:\n" + string.Join("\n", changes.Take(12)));
        }

        private static string Name(string file) => Path.GetRelativePath(WorkDir, file);

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
            Unpack(DirNames.TrainerProperties, DirNames.TrainerParty);
            SavesUnchanged($"{game} trainers", new[] { Dir(DirNames.TrainerProperties), Dir(DirNames.TrainerParty) }, () =>
            {
                int n = 0;
                foreach ((int id, string propPath) in Members(Dir(DirNames.TrainerProperties)).ToList())
                {
                    if (!File.Exists(Path.Combine(Dir(DirNames.TrainerParty), Path.GetFileName(propPath)))) continue;
                    TrainerFile trainer = TrainerRecords.Load(id);
                    TrainerRecords.Save(id, trainer.Trp, trainer.Party);
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
            Unpack(DirNames.Encounters);
            SavesUnchanged($"{game} wild encounters", new[] { Dir(DirNames.Encounters) }, () =>
            {
                int n = 0;
                foreach ((int id, string _) in Members(Dir(DirNames.Encounters)).ToList())
                {
                    EncounterFile file = GameFamily == GameFamilies.HGSS ? new EncounterFileHGSS(id) : new EncounterFileDPPt(id);
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
            Unpack(DirNames.AreaData);
            SavesUnchanged($"{game} area data", new[] { Dir(DirNames.AreaData) }, () =>
            {
                int n = 0;
                foreach ((int id, string path) in Members(Dir(DirNames.AreaData)).ToList())
                {
                    ROMFiles.AreaData area;
                    using (FileStream s = File.OpenRead(path)) area = new ROMFiles.AreaData(s);
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
            Unpack(DirNames.TradeData);
            SavesUnchanged($"{game} trades", new[] { Dir(DirNames.TradeData) }, () =>
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
            Unpack(DirNames.Evolutions);
            SavesUnchanged($"{game} evolutions", new[] { Dir(DirNames.Evolutions) }, () =>
            {
                int n = 0;
                foreach ((int id, string _) in Members(Dir(DirNames.Evolutions)).ToList())
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
            Unpack(DirNames.EggMoves);
            string[] guarded = GameDirs.ContainsKey(DirNames.EggMoves) ? new[] { Dir(DirNames.EggMoves) } : Array.Empty<string>();
            SavesUnchanged($"{game} egg moves", guarded, () =>
            {
                EggMoveEditorViewModel editor = new EggMoveEditorViewModel();
                Task.Run(() => editor.SaveCommandAsync()).GetAwaiter().GetResult();
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
            Skip.If(!GameDirs.ContainsKey(DirNames.BattleTowerTrainers), $"{game} has no Battle Tower archives listed");
            Unpack(DirNames.BattleTowerTrainers, DirNames.BattleTowerPokemon);
            SavesUnchanged($"{game} Battle Tower", new[] { Dir(DirNames.BattleTowerTrainers), Dir(DirNames.BattleTowerPokemon) }, () =>
            {
                BattleTowerTrainerFile trainers = new BattleTowerTrainerFile(load: true);
                BattleTowerPokemonSetFile sets = new BattleTowerPokemonSetFile(load: true);
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
            SwarmsViewModel editor = new SwarmsViewModel();
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
            Unpack(DirNames.EncounterExtended);
            SavesUnchanged($"{game} Trophy Garden, Great Marsh and Honey Tree", new[] { Filesystem.EncounterExtended }, () =>
            {
                TrophyGardenEncounterFile garden = new TrophyGardenEncounterFile(load: true);
                GreatMarshEncounterFile marsh = new GreatMarshEncounterFile(load: true);
                HoneyTreeEncounterFile honey = new HoneyTreeEncounterFile(load: true);
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
            Unpack(DirNames.Headbutt, DirNames.SafariZone);
            SavesUnchanged("HeartGold Headbutt and Safari Zone", new[] { Filesystem.Headbutt, Filesystem.SafariZone }, () =>
            {
                int n = 0;
                foreach ((int id, string _) in Members(Filesystem.Headbutt).ToList())
                {
                    Assert.True(new HeadbuttEncounterFile((ushort)id).SaveToFile(id), $"Headbutt file {id} did not save");
                    n++;
                }
                foreach ((int id, string _) in Members(Filesystem.SafariZone).ToList())
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

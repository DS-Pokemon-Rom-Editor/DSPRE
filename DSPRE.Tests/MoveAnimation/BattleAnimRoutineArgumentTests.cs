using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>
    /// Every routine call in both shipped ROMs, checked against what the routines can actually take.
    /// </summary>
    [Collection("rom")]
    public class BattleAnimRoutineArgumentTests
    {
        private readonly ITestOutputHelper _out;
        public BattleAnimRoutineArgumentTests(ITestOutputHelper o) { _out = o; }

        private const int RoutineCount = 84;
        private const int WorkSlots = 8 + 2;

        private static readonly string HeartGold = TestRoms.HeartGold;
        private static readonly string Platinum = TestRoms.Platinum;

        private static string ScriptDir(string project, string gameCode)
        {
            if (!Directory.Exists(project)) return null;
            try { new RomInfo(gameCode, project); } catch { return null; }
            var narc = new ScriptNarc(DirNames.wazaEffectScripts);
            return narc.Available ? gameDirs[DirNames.wazaEffectScripts].unpackedDir : null;
        }

        [Fact]
        public void EveryHeartGoldRoutineCallFitsWhatTheRoutinesCanTake()
            => Sweep(HeartGold, "IPKE", WazaSeqVersion.HGSS);

        [Fact]
        public void EveryPlatinumRoutineCallFitsWhatTheRoutinesCanTake()
            => Sweep(Platinum, "CPUE", WazaSeqVersion.Plat);

        private void Sweep(string project, string gameCode, WazaSeqVersion version)
        {
            string dir = ScriptDir(project, gameCode);
            Assert.True(dir != null, gameCode + ": the move-effect archive could not be unpacked, so nothing was checked");

            var badId = new List<string>();
            var tooMany = new List<string>();
            var seen = new SortedSet<int>();
            int sites = 0;

            foreach (var f in RomFiles.Settled(dir))
            {
                var bytes = File.ReadAllBytes(f);
                if (bytes.Length == 0) continue;
                string name = Path.GetFileName(f);
                foreach (var c in BattleAnimScript.Parse(bytes, version))
                {
                    if (BattleAnimCommands.Name(version, c.OpId) != "CallFunc" || c.Args.Length < 2) continue;
                    sites++;
                    int id = c.Args[0], count = c.Args[1];
                    seen.Add(id);
                    if (id < 0 || id >= RoutineCount) badId.Add($"{name}: routine {id}");
                    if (count < 0 || count > WorkSlots) tooMany.Add($"{name}: routine {id} passes {count} words");
                }
            }

            _out.WriteLine($"{gameCode}: {sites} routine calls, {seen.Count} distinct routines");
            Assert.True(sites > 2000, $"only {sites} calls were seen");
            Assert.True(badId.Count == 0, $"{badId.Count} calls name a routine the table has no entry for: {string.Join(", ", badId.Take(10))}");
            Assert.True(tooMany.Count == 0, $"{tooMany.Count} calls pass more words than the work array holds: {string.Join(", ", tooMany.Take(10))}");
        }

        /// <summary>
        /// Every routine the scripts call has an entry saying what its words mean, and every word a script
        /// passes that the routine actually reads has a meaning written down for it.
        /// </summary>
        [Fact]
        public void EveryRoutineTheScriptsCallHasItsWordsWrittenDown()
        {
            string dir = ScriptDir(HeartGold, "IPKE");
            Assert.True(dir != null, "the move-effect archive could not be unpacked, so nothing was checked");

            var called = new SortedSet<int>();
            var widest = new Dictionary<int, int>();
            foreach (var f in RomFiles.Settled(dir))
            {
                var bytes = File.ReadAllBytes(f);
                if (bytes.Length == 0) continue;
                foreach (var c in BattleAnimScript.Parse(bytes, WazaSeqVersion.HGSS))
                {
                    if (BattleAnimCommands.Name(WazaSeqVersion.HGSS, c.OpId) != "CallFunc" || c.Args.Length < 2) continue;
                    called.Add(c.Args[0]);
                    widest[c.Args[0]] = Math.Max(widest.GetValueOrDefault(c.Args[0]), c.Args[1]);
                }
            }

            var noEntry = called.Where(id => BattleAnimFuncs.Get(id) == null).ToList();
            Assert.True(noEntry.Count == 0,
                $"{noEntry.Count} routines the scripts call have no entry: {string.Join(", ", noEntry)}");

            // The table must cover every word the scripts hand over, or the editor has nothing to say
            // about the ones past the end.
            var tooShort = called
                .Where(id => widest[id] > BattleAnimFuncs.Get(id).Words.Length)
                .Select(id => $"{BattleAnimFuncs.Get(id).Name} is handed {widest[id]} words but only {BattleAnimFuncs.Get(id).Words.Length} are written down")
                .ToList();
            Assert.True(tooShort.Count == 0, string.Join("; ", tooShort));

            int blanks = called.Sum(id => BattleAnimFuncs.Get(id).Words.Count(string.IsNullOrEmpty));
            int described = called.Sum(id => BattleAnimFuncs.Get(id).Words.Count(w => !string.IsNullOrEmpty(w)));
            _out.WriteLine($"{called.Count} routines called, {described} words explained, {blanks} left blank because the routine never reads them");

            // Counted against pokeplatinum's named script variables: words a routine is handed but never reads,
            // or reads into a variable it never uses.
            Assert.Equal(19, blanks);
            Assert.True(described > 90, $"only {described} words are explained");
        }

        [Fact]
        public void EveryRoutineEntryNamesTheRoutineItCameFrom()
        {
            foreach (var r in BattleAnimFuncs.Known)
            {
                Assert.False(string.IsNullOrWhiteSpace(r.Summary), $"routine {r.Id} has no summary");
                Assert.Matches(@"^\w+$", r.Source);
                Assert.True(r.Words.Length <= BattleAnimFuncs.WorkSlots,
                    $"routine {r.Id} claims more words than the work array holds");
            }
        }

        // ── which Pokemon a target flag picks out ───────────────────────────────────

        private const int A = BattleAnimTargetFlags.Attacker, D = BattleAnimTargetFlags.Defender,
                          Sprites = BattleAnimTargetFlags.BattlerSprites;

        [Fact]
        public void TheNamesAreRelativeToTheMoveNotToTheSides()
        {
            Assert.Equal(new[] { 7 }, BattleAnimTargetFlags.Targets(A | Sprites, 7, 9));
            Assert.Equal(new[] { 9 }, BattleAnimTargetFlags.Targets(D | Sprites, 7, 9));
            Assert.Equal(new[] { 7, 9 }, BattleAnimTargetFlags.Targets(A | D, 7, 9));
        }

        [Fact]
        public void ASpecificBattlerFlagPicksTheSameSlotWhoeverAttacks()
        {
            // pokeplatinum's BATTLE_ANIM_BATTLER_PLAYER_1 and _ENEMY_1, as Cosmic Power fades them.
            int player1 = BattleAnimTargetFlags.SpecificBattler | A, enemy1 = BattleAnimTargetFlags.SpecificBattler | D;
            Assert.Equal(new[] { BattleAnimTargetFlags.PlayerSlot }, BattleAnimTargetFlags.Targets(player1, 1, 0));
            Assert.Equal(new[] { BattleAnimTargetFlags.EnemySlot }, BattleAnimTargetFlags.Targets(enemy1, 1, 0));
            Assert.Equal("the enemy's first Pokemon", BattleAnimTargetFlags.Describe(enemy1));
        }

        [Fact]
        public void AnAllyOnlyFlagPicksNobodyInASingleBattle()
        {
            // The games only look an ally up in a double battle, so these do nothing at all.
            Assert.Empty(BattleAnimTargetFlags.Targets(BattleAnimTargetFlags.AttackerPartner | Sprites, 0, 1));
            Assert.Empty(BattleAnimTargetFlags.Targets(BattleAnimTargetFlags.DefenderPartner | Sprites, 0, 1));
        }

        [Fact]
        public void AllBattlersIsEverybodyAndNotAttackerIsEverybodyElse()
        {
            Assert.Equal(new[] { 0, 1 }, BattleAnimTargetFlags.Targets(BattleAnimTargetFlags.AllBattlers | Sprites, 0, 1));
            Assert.Equal(new[] { 1 }, BattleAnimTargetFlags.Targets(BattleAnimTargetFlags.NotAttacker | Sprites, 0, 1));
            // A move that hits its own user leaves nobody else.
            Assert.Empty(BattleAnimTargetFlags.Targets(BattleAnimTargetFlags.NotAttacker, 0, 0));
        }

        [Fact]
        public void TheFlagReadsAsWordsSomebodyCanUnderstand()
        {
            Assert.Equal("defender (as battle sprites)", BattleAnimTargetFlags.Describe(D | Sprites));
            Assert.Equal("attacker (as battle sprites)", BattleAnimTargetFlags.Describe(A | Sprites));
            Assert.Equal("everyone (as battle sprites)", BattleAnimTargetFlags.Describe(BattleAnimTargetFlags.AllBattlers | Sprites));
            Assert.Equal("copy 0 (as dropped sprites)",
                BattleAnimTargetFlags.Describe(BattleAnimTargetFlags.PokemonSprites | BattleAnimTargetFlags.PokemonSprite0));
        }
    }
}

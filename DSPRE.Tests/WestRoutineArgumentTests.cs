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
    ///
    /// A move script calls a routine with FUNC_CALL id, count, words. The id indexes WeSysSP_FuncTable
    /// (west_sp.c:218 indexes it directly, no offset) which has 84 entries, and the words land in
    /// waza_eff_gp_wk, which holds ten (we_sys.h:92). WEST_FUNC_CALL copies count words in and then zeros
    /// the rest, so a routine handed fewer words than it reads still runs and sees zeros.
    ///
    /// MoveAnimationRoutines.md lists what each routine reads, with the source it came from.
    /// </summary>
    [Collection("rom")]
    public class WestRoutineArgumentTests
    {
        private readonly ITestOutputHelper _out;
        public WestRoutineArgumentTests(ITestOutputHelper o) { _out = o; }

        private const int RoutineCount = 84;   // NELEMS(WeSysSP_FuncTable)
        private const int WorkSlots = 8 + 2;   // WE_GENE_WK_MAX, we_sys.h:92

        private const string HeartGold = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";
        private const string Platinum =
            @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents";

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
                foreach (var c in WestScript.Parse(bytes, version))
                {
                    if (WestOpcodes.Name(version, c.OpId) != "WEST_FUNC_CALL" || c.Args.Length < 2) continue;
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
        /// Every routine the scripts call has an entry saying what its words mean, and every word a
        /// script passes that the routine actually reads has a meaning written down for it.
        ///
        /// This is what makes the editor able to say what a call does. A word the routine never reads
        /// is deliberately left blank rather than invented, and this counts those separately so the
        /// blanks cannot quietly grow.
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
                foreach (var c in WestScript.Parse(bytes, WazaSeqVersion.HGSS))
                {
                    if (WestOpcodes.Name(WazaSeqVersion.HGSS, c.OpId) != "WEST_FUNC_CALL" || c.Args.Length < 2) continue;
                    called.Add(c.Args[0]);
                    widest[c.Args[0]] = Math.Max(widest.GetValueOrDefault(c.Args[0]), c.Args[1]);
                }
            }

            var noEntry = called.Where(id => WestRoutines.Get(id) == null).ToList();
            Assert.True(noEntry.Count == 0,
                $"{noEntry.Count} routines the scripts call have no entry: {string.Join(", ", noEntry)}");

            // The table must cover every word the scripts hand over, or the editor has nothing to say
            // about the ones past the end.
            var tooShort = called
                .Where(id => widest[id] > WestRoutines.Get(id).Words.Length)
                .Select(id => $"{WestRoutines.Get(id).Name} is handed {widest[id]} words but only {WestRoutines.Get(id).Words.Length} are written down")
                .ToList();
            Assert.True(tooShort.Count == 0, string.Join("; ", tooShort));

            int blanks = called.Sum(id => WestRoutines.Get(id).Words.Count(string.IsNullOrEmpty));
            int described = called.Sum(id => WestRoutines.Get(id).Words.Count(w => !string.IsNullOrEmpty(w)));
            _out.WriteLine($"{called.Count} routines called, {described} words explained, {blanks} left blank because the routine never reads them");

            // Counted, not guessed: eight routines are handed one word nothing in their call graph reads
            // (8), DISP_MOVE is handed two more than it reads (2), and WE_070, WE_T06, WE_T08 and
            // RECT_VIEW each skip a word in the middle (2+2+1+1).
            Assert.Equal(16, blanks);
            Assert.True(described > 90, $"only {described} words are explained");
        }

        [Fact]
        public void EveryRoutineEntryNamesTheSourceItCameFrom()
        {
            foreach (var r in WestRoutines.Known)
            {
                Assert.False(string.IsNullOrWhiteSpace(r.Summary), $"routine {r.Id} has no summary");
                Assert.Matches(@"^\w+, \w+\.c:\d+$", r.Source);
                Assert.True(r.Words.Length <= WestRoutines.WorkSlots,
                    $"routine {r.Id} claims more words than the work array holds");
            }
        }

        // ── which Pokemon a target flag picks out ───────────────────────────────────

        [Fact]
        public void TheNamesAreRelativeToTheMoveNotToTheSides()
        {
            // M1 is the attacker and E1 the defender, whichever side either is on (we_tool.c:1431).
            Assert.Equal(new[] { 7 }, WestTargetFlags.Targets(WestTargetFlags.M1 | WestTargetFlags.Ssp, 7, 9));
            Assert.Equal(new[] { 9 }, WestTargetFlags.Targets(WestTargetFlags.E1 | WestTargetFlags.Ssp, 7, 9));
            Assert.Equal(new[] { 7, 9 }, WestTargetFlags.Targets(WestTargetFlags.M1 | WestTargetFlags.E1, 7, 9));
        }

        [Fact]
        public void AnAllyOnlyFlagPicksNobodyInASingleBattle()
        {
            // The games only look an ally up in a double battle, so these do nothing at all.
            Assert.Empty(WestTargetFlags.Targets(WestTargetFlags.M2 | WestTargetFlags.Ssp, 0, 1));
            Assert.Empty(WestTargetFlags.Targets(WestTargetFlags.E2 | WestTargetFlags.Ssp, 0, 1));
        }

        [Fact]
        public void StageIsEverybodyAndOtherIsEverybodyButTheAttacker()
        {
            Assert.Equal(new[] { 0, 1 }, WestTargetFlags.Targets(WestTargetFlags.Stage | WestTargetFlags.Ssp, 0, 1));
            Assert.Equal(new[] { 1 }, WestTargetFlags.Targets(WestTargetFlags.Other | WestTargetFlags.Ssp, 0, 1));
            // A move that hits its own user leaves nobody else to be "other".
            Assert.Empty(WestTargetFlags.Targets(WestTargetFlags.Other, 0, 0));
        }

        [Fact]
        public void TheFlagReadsAsWordsSomebodyCanUnderstand()
        {
            Assert.Equal("defender (as battle sprites)", WestTargetFlags.Describe(WestTargetFlags.E1 | WestTargetFlags.Ssp));
            Assert.Equal("attacker (as battle sprites)", WestTargetFlags.Describe(WestTargetFlags.M1 | WestTargetFlags.Ssp));
            Assert.Equal("everyone (as battle sprites)", WestTargetFlags.Describe(WestTargetFlags.Stage | WestTargetFlags.Ssp));
            Assert.Equal("cap 0 (as dropped sprites)", WestTargetFlags.Describe(WestTargetFlags.Cap | WestTargetFlags.C0));
        }
    }
}

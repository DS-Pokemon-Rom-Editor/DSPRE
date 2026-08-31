using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DSPRE;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>
    /// Where a move can put its particles, and what shape it can throw them in.
    ///
    /// Checking that the preview puts particles where the game does means checking every way a move can ask
    /// for that, not the two or three moves that happen to look wrong. There are two independent choices:
    /// the anchor, which is the callback number on the ADD_PARTICLE command and decides what the emitter is
    /// tied to, and the emission shape, which is in the particle data and decides how the particles leave it.
    ///
    /// This reads every script and every archive in both games and writes out one move for each, so the set
    /// to record is chosen from the data instead of by hand.
    /// </summary>
    [Collection("rom")]
    public class ParticleAnchorCoverageTests
    {
        private readonly ITestOutputHelper _out;
        public ParticleAnchorCoverageTests(ITestOutputHelper o) { _out = o; }

        private const string HeartGold = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";
        private const string Platinum =
            @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents";

        private static readonly string Doc =
            @"C:\Romhacking\Tooling\DSPRE\Research\Moves\Animation\MoveAnimationParticlePlacement.md";

        [Fact]
        public void WriteEveryAnchorAndEmissionShapeWithAMoveThatUsesIt()
        {
            Assert.True(Directory.Exists(Platinum), "the Platinum project is not there, so nothing was checked");
            new RomInfo("CPUE", Platinum);

            var scripts = RomFiles.Settled(gameDirs[DirNames.wazaEffectScripts].unpackedDir);
            var archives = RomFiles.Settled(gameDirs[DirNames.wazaParticle].unpackedDir);
            Assert.True(scripts.Length > 400 && archives.Length > 100,
                $"only {scripts.Length} scripts and {archives.Length} archives, so this proves nothing");

            var names = RomInfo.GetAttackNames() ?? Array.Empty<string>();
            string Nm(int m) => m < names.Length && !string.IsNullOrWhiteSpace(names[m]) ? $"{m} {names[m]}" : m.ToString();

            // anchor (the ADD_PARTICLE callback) -> the moves that use it
            var anchors = new SortedDictionary<int, List<int>>();
            // and which archives each move loads, so a shape can be traced back to a move
            var loads = new Dictionary<int, List<int>>();

            for (int move = 0; move < scripts.Length; move++)
            {
                var bytes = File.ReadAllBytes(scripts[move]);
                if (bytes.Length == 0) continue;
                foreach (var c in WestScript.Parse(bytes, WazaSeqVersion.Plat))
                {
                    string op = WestOpcodes.Name(WazaSeqVersion.Plat, c.OpId);
                    if (op == "WEST_LOAD_PARTICLE" && c.Args.Length >= 2)
                    {
                        if (!loads.TryGetValue(move, out var la)) loads[move] = la = new List<int>();
                        if (!la.Contains(c.Args[1])) la.Add(c.Args[1]);
                    }
                    if (op != "WEST_ADD_PARTICLE" || c.Args.Length < 3) continue;
                    int cb = c.Args[2];
                    if (!anchors.TryGetValue(cb, out var l)) anchors[cb] = l = new List<int>();
                    if (!l.Contains(move)) l.Add(move);
                }
            }

            // emission shape -> the archives that use it, then back to a move that loads one of them
            var shapes = new SortedDictionary<int, List<int>>();
            for (int i = 0; i < archives.Length; i++)
            {
                byte[] b;
                try { b = File.ReadAllBytes(archives[i]); } catch { continue; }
                if (b.Length < 32) continue;
                SpaArchive a;
                try { a = SpaArchive.Parse(b); } catch { continue; }
                if (a?.Emitters == null) continue;
                foreach (var em in a.Emitters)
                {
                    if (!shapes.TryGetValue(em.InitPosType, out var l)) shapes[em.InitPosType] = l = new List<int>();
                    if (!l.Contains(i)) l.Add(i);
                }
            }

            int MoveLoading(int archive)
            {
                foreach (var kv in loads) if (kv.Value.Contains(archive)) return kv.Key;
                return -1;
            }

            var sb = new StringBuilder();
            sb.Append("[Research](../../ResearchNotes.md) / [Move Research](../MoveResearch.md) / Move Animation Particle Placement\n\n");
            sb.Append("# Where moves put their particles\n\n");
            sb.Append("Generated from the Platinum ROM by `ParticleAnchorCoverageTests`. Do not edit by hand.\n\n");
            sb.Append("Two independent choices decide where a move's particles appear and how they leave. The anchor is\n");
            sb.Append("the last value on the ADD_PARTICLE command and says what the emitter is tied to. The emission\n");
            sb.Append("shape sits in the particle data and says how the particles are thrown from it. To check the\n");
            sb.Append("preview puts particles where the game does, one move from each row has to be recorded.\n\n");
            sb.Append($"- {scripts.Length} scripts read, {archives.Length} particle archives read\n");
            sb.Append($"- {anchors.Count} different anchors, {shapes.Count} different emission shapes\n\n");

            sb.Append("## Anchors\n\n| anchor | moves using it | some that do |\n|---:|---:|---|\n");
            foreach (var kv in anchors)
                sb.Append($"| {kv.Key} | {kv.Value.Count} | {string.Join(", ", kv.Value.Take(10).Select(Nm))} |\n");

            sb.Append("\n## Emission shapes\n\n| shape | archives using it | one to record |\n|---:|---:|---|\n");
            foreach (var kv in shapes)
            {
                int m = kv.Value.Select(MoveLoading).FirstOrDefault(x => x >= 0);
                sb.Append($"| {kv.Key} | {kv.Value.Count} | {(m >= 0 ? Nm(m) : "no move loads one")} |\n");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Doc));
            File.WriteAllText(Doc, sb.ToString());

            _out.WriteLine($"{scripts.Length} scripts, {archives.Length} archives: "
                           + $"{anchors.Count} anchors, {shapes.Count} emission shapes");
            _out.WriteLine("anchors: " + string.Join(", ", anchors.Select(k => $"{k.Key} ({k.Value.Count} moves)")));
            _out.WriteLine("shapes: " + string.Join(", ", shapes.Select(k => $"{k.Key} ({k.Value.Count} archives)")));
            _out.WriteLine("written to " + Doc);

            Assert.True(anchors.Count > 0 && shapes.Count > 0, "nothing was found, so this proves nothing");
        }
    }
}

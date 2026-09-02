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
    /// Writes out what DSPRE's preview does for a list of moves, so it can be put beside a recording.
    ///
    /// These are the preview's own numbers, taken from the player rather than from a picture, so they are
    /// exact to the frame. What they cannot do is say whether the preview LOOKS like the game; that needs
    /// the rendered frames compared image against image, which this does not do.
    ///
    /// A tool rather than a check: it does nothing unless DSPRE_PREVIEW_MOVES names some moves.
    /// </summary>
    [Collection("rom")]
    public class PreviewMeasurementDump
    {
        private readonly ITestOutputHelper _out;
        public PreviewMeasurementDump(ITestOutputHelper o) { _out = o; }

        private const string Platinum =
            @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents";

        private static readonly string Scratch =
            @"C:\Users\safea\AppData\Local\Temp\claude\C--Romhacking-Tooling-DSPRE\4e7579f1-fc54-4253-a949-eba625115ec9\scratchpad";

        /// <summary>Runs one variant of a move and reports what it does. `second` picks the odd-turn branch.</summary>
        private static (int frames, string kind, double size, int flashAt, int movedAt, double furthest,
                        int particles, int actors)
            Run(System.Collections.Generic.List<WazaSeqCommand> cmds, ScriptNarc particlesNarc, bool second)
        {
            var w = new WestPlayer(cmds, WazaSeqVersion.Plat, particlesNarc, 64, 120, 190, 60,
                                   attackerIsEnemy: true, selfTarget: false) { SecondTurnVariant = second };
            var res = WestCats.Extract(cmds, WazaSeqVersion.Plat);
            if (res.HasCellAnimation)
            {
                var cells = new WeCellAnimRenderer();
                if (cells.Load(res.Char, res.Pltt, res.Cell, res.CellAnm)) w.Cells = cells;
            }

            int frames = 0, movedAt = -1, flashAt = -1, mostActors = 0, mostParticles = 0;
            double darkest = 0, brightest = 0, furthest = 0;
            while (frames < 900 && !w.Finished)
            {
                w.Step(); frames++;
                mostActors = Math.Max(mostActors, w.CatsActors.Count);
                mostParticles = Math.Max(mostParticles, w.LiveParticles().Count());

                double moved = Math.Max(Math.Abs(w.MonDX[1]) + Math.Abs(w.MonShakeX[1]),
                                        Math.Abs(w.MonDY[1]) + Math.Abs(w.MonShakeY[1]));
                if (moved > 0.5 && movedAt < 0) movedAt = frames;
                furthest = Math.Max(furthest, moved);

                double lum = (0.2126 * w.FadeR + 0.7152 * w.FadeG + 0.0722 * w.FadeB) / 255.0;
                double dark = w.FadeOpacity * (1 - lum);
                double light = Math.Max(w.BgFlashAmount, w.FadeOpacity * lum);
                if (Math.Max(dark, light) > Math.Max(darkest, brightest)) flashAt = frames;
                darkest = Math.Max(darkest, dark);
                brightest = Math.Max(brightest, light);
            }

            string kind = darkest < 0.05 && brightest < 0.05 ? "flat" : darkest >= brightest ? "darkens" : "brightens";
            return (frames, kind, Math.Max(darkest, brightest), flashAt, movedAt, furthest, mostParticles, mostActors);
        }

        [Fact]
        public void DumpTheMovesNamedInTheEnvironment()
        {
            string list = Environment.GetEnvironmentVariable("DSPRE_PREVIEW_MOVES");
            if (string.IsNullOrWhiteSpace(list)) { _out.WriteLine("DSPRE_PREVIEW_MOVES not set; nothing to do"); return; }

            var moves = list.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => int.Parse(x.Trim())).ToList();
            Assert.True(Directory.Exists(Platinum), "the Platinum project is not there");
            new RomInfo("CPUE", Platinum);

            var narc = new ScriptNarc(DirNames.wazaEffectScripts);
            var particles = new ScriptNarc(DirNames.wazaParticle);
            Assert.True(narc.Available && particles.Available, "the move or particle archive is missing");

            var names = RomInfo.GetAttackNames() ?? Array.Empty<string>();
            var json = new StringBuilder("{\n");

            foreach (int move in moves)
            {
                var bytes = narc.Get(move);
                if (bytes == null || bytes.Length == 0) { _out.WriteLine($"move {move}: no script"); continue; }
                var cmds = WestScript.Parse(bytes, WazaSeqVersion.Plat);
                int pos = 0; foreach (var c in cmds) { c.WordPos = pos; pos += 1 + c.Args.Length; }

                // Cast by the enemy, matching the recordings, with the cell resources the script asks for.
                // A move with a turn check holds two whole animations and the games alternate them by turn, so
                // both are measured; everything else has one.
                bool twoTurn = cmds.Any(c => WestOpcodes.Name(WazaSeqVersion.Plat, c.OpId) == "WEST_TURN_CHK");
                var runA = Run(cmds, particles, second: false);
                var runB = twoTurn ? Run(cmds, particles, second: true) : runA;

                int frames = runA.frames, movedAt = runA.movedAt, flashAt = runA.flashAt;
                int mostActors = runA.actors, mostParticles = runA.particles;
                double furthest = runA.furthest;

                if (Environment.GetEnvironmentVariable("DSPRE_TRACE_MOVE") == move.ToString())
                {
                    var w2 = new WestPlayer(cmds, WazaSeqVersion.Plat, particles, 64, 120, 190, 60,
                                            attackerIsEnemy: true, selfTarget: false);
                    foreach (var c in cmds)
                        _out.WriteLine($"    {WestOpcodes.Name(WazaSeqVersion.Plat, c.OpId)} "
                                       + string.Join(" ", c.Args));
                    int calls = cmds.Count(c => WestOpcodes.Name(WazaSeqVersion.Plat, c.OpId) == "WEST_FUNC_CALL");
                    _out.WriteLine($"  trace: {cmds.Count} commands, {calls} routine calls");
                    foreach (var g in cmds.Where(c => WestOpcodes.Name(WazaSeqVersion.Plat, c.OpId) == "WEST_FUNC_CALL")
                                          .GroupBy(c => c.Args[0]).OrderByDescending(g2 => g2.Count()))
                        _out.WriteLine($"    routine {g.Key} ({WestScriptDisplay.RoutineName(g.Key)}) x{g.Count()}");
                    for (int i = 0; i < 200 && !w2.Finished; i++)
                    {
                        w2.Step();
                        if (i % 4 == 0)
                        {
                            var live = w2.LiveParticles().ToList();
                            if (live.Count > 0)
                                _out.WriteLine($"    p{i,3} {live.Count,3} particles  "
                                    + $"x {live.Min(q => q.X),7:F1}..{live.Max(q => q.X),7:F1}  "
                                    + $"y {live.Min(q => q.Y),7:F1}..{live.Max(q => q.Y),7:F1}  "
                                    + $"vx {live.Average(q => q.VX),7:F2} vy {live.Average(q => q.VY),7:F2}");
                        }
                        if (i % 4 == 0)
                            _out.WriteLine($"    f{i,3} dx={w2.MonDX[1],8:F1} dy={w2.MonDY[1],7:F1} "
                                           + $"shx={w2.MonShakeX[1],6:F1} shy={w2.MonShakeY[1],6:F1} "
                                           + $"fade={w2.FadeOpacity,5:F2} rgb=({w2.FadeR},{w2.FadeG},{w2.FadeB}) "
                                           + $"bgflash={w2.BgFlashAmount,4:F2} hasBg={w2.HasBackground}");
                    }
                }

                string kind = runA.kind;
                string name = move < names.Length ? names[move] : "";

                _out.WriteLine($"move {move} {name}: {frames} frames, {kind}, flash at {flashAt}, "
                               + $"movement from {(movedAt < 0 ? "never" : movedAt.ToString())} furthest {furthest:F0}px, "
                               + $"{mostParticles} particles, {mostActors} cell actors"
                               + (twoTurn ? $"; second turn: {runB.frames} frames, {runB.kind}, {runB.particles} particles" : ""));

                json.Append($"  \"move{move:D3}\": {{\"name\": \"{name}\", \"frames\": {frames}, ")
                    .Append($"\"flashKind\": \"{kind}\", \"flashSize\": {runA.size:F2}, ")
                    .Append($"\"flashAfter\": {flashAt}, \"movesAt\": {movedAt}, \"furthestPx\": {furthest:F0}, ")
                    .Append($"\"particles\": {mostParticles}, \"cellActors\": {mostActors}, ")
                    .Append($"\"twoTurn\": {(twoTurn ? "true" : "false")}, \"secondFrames\": {runB.frames}, ")
                    .Append($"\"secondFlashKind\": \"{runB.kind}\", \"secondFurthestPx\": {runB.furthest:F0}, ")
                    .Append($"\"secondParticles\": {runB.particles}}},\n");
            }

            string text = json.ToString().TrimEnd('\n', ',') + "\n}\n";
            Directory.CreateDirectory(Scratch);
            File.WriteAllText(Path.Combine(Scratch, "preview_measurements.json"), text);
            _out.WriteLine("written to preview_measurements.json");
        }
    }
}

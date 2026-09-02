using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>Whether calling a routine actually makes the preview do anything.</summary>
    [Collection("rom")]
    public class WestRoutineEffectTests
    {
        private readonly ITestOutputHelper _out;
        public WestRoutineEffectTests(ITestOutputHelper o) { _out = o; }

        private const string HeartGold = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";

        private static string ScriptDir()
        {
            if (!Directory.Exists(HeartGold)) return null;
            try { new RomInfo("IPKE", HeartGold); } catch { return null; }
            var narc = new ScriptNarc(DirNames.wazaEffectScripts);
            return narc.Available ? gameDirs[DirNames.wazaEffectScripts].unpackedDir : null;
        }

        /// <summary>One real call of each routine, taken from the first script that makes it.</summary>
        private static Dictionary<int, int[]> RealCalls(string dir)
        {
            var found = new Dictionary<int, int[]>();
            foreach (var f in RomFiles.Settled(dir))
            {
                var bytes = File.ReadAllBytes(f);
                if (bytes.Length == 0) continue;
                foreach (var c in WestScript.Parse(bytes, WazaSeqVersion.HGSS))
                {
                    if (WestOpcodes.Name(WazaSeqVersion.HGSS, c.OpId) != "WEST_FUNC_CALL" || c.Args.Length < 2) continue;
                    if (!found.ContainsKey(c.Args[0])) found[c.Args[0]] = c.Args;
                }
            }
            return found;
        }

        /// <summary>Everything the player can visibly do, as one string, so a change of any kind shows up.</summary>
        private static string Snapshot(WestPlayer w)
        {
            string s = "";
            for (int m = 0; m < 2; m++)
                s += $"{w.MonDX[m]},{w.MonDY[m]},{w.MonRot[m]},{w.MonScaleX[m]},{w.MonScaleY[m]},"
                   + $"{w.MonTintA[m]},{w.MonShakeX[m]},{w.MonShakeY[m]},{w.MonMosaic[m]},"
                   + $"{w.MonClip[m]},{w.MonAlpha[m]},{w.MonVisible[m]}|";
            s += $"{w.ShakeX},{w.ShakeY},{w.FadeOpacity},{w.BgFlashAmount},{w.Grayscale},{w.RasterActive},"
               + $"{w.HasBackground},{w.MonWarpAmp},{w.Ghosts.Count},{w.CatsActors.Count},{w.Notes.Count}";
            return s;
        }

        [Fact]
        public void EveryRoutineTheScriptsCallMakesThePreviewDoSomething()
        {
            string dir = ScriptDir();
            Assert.True(dir != null, "the move-effect archive could not be unpacked, so nothing was checked");

            var calls = RealCalls(dir);
            Assert.True(calls.Count >= 77, $"only {calls.Count} routines were found being called");

            var opId = WestOpcodes.Id(WazaSeqVersion.HGSS, "WEST_FUNC_CALL");
            var silent = new List<string>();
            int ran = 0;

            foreach (var kv in calls.OrderBy(k => k.Key))
            {
                int id = kv.Key;
                var script = new List<WazaSeqCommand> { new WazaSeqCommand(opId, kv.Value) { WordPos = 0 } };
                var w = new WestPlayer(script, WazaSeqVersion.HGSS, null, 64, 120, 190, 60,
                                       attackerIsEnemy: false, selfTarget: false);
                string before = Snapshot(w);
                // Watch every frame, not just the last one.
                bool moved = false;
                for (int i = 0; i < 240 && !moved; i++)
                {
                    w.Step();
                    if (Snapshot(w) != before) moved = true;
                }
                ran++;
                if (!moved) silent.Add($"{WestRoutines.Get(id)?.Name ?? id.ToString()} ({id})");
            }

            _out.WriteLine($"{ran} routines driven with a real call; {ran - silent.Count} changed something, {silent.Count} did not");
            foreach (var s in silent) _out.WriteLine("  silent: " + s);

            // Each of these is silent for a reason that was checked, not waved away.
            var expected = new[]
            {
                // The games' own sample routines, which really do nothing.
                "TEST_1 (0)", "TEST_2 (1)", "TEST_3 (2)", "TEST_4 (3)",
                // Keeps the dropped copies drawn while particle data streams in, which a preview never
                // waits for, so there is nothing to keep drawn.
                "ALL_DROP (78)",
                // Right to do nothing with the words the scripts actually pass.
                "WE_DISP_DEF (62)", "WE_175 / SHAKE (27)",
                // These act on something an earlier command in the real script creates: a particle emitter,
                // a dropped copy, or a cell actor.
                "EMIT_STRAIGHT (65)", "EMIT_PARABOLIC (66)", "EMIT_ROTATION (72)", "EMIT_SIMPLE_UD (73)",
                "POKE_OAM_VIEW (75)", "WE_T08 (56)", "WE_057 (49)",
            };
            var unexpected = silent.Except(expected).ToList();
            Assert.True(unexpected.Count == 0,
                $"{unexpected.Count} of {ran} routines did nothing at all: {string.Join(", ", unexpected)}");
        }
    }
}

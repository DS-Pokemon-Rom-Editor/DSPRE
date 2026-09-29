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
    public class BattleAnimRoutineEffectTests
    {
        private readonly ITestOutputHelper _out;
        public BattleAnimRoutineEffectTests(ITestOutputHelper o) { _out = o; }

        private static readonly string HeartGold = TestRoms.HeartGold;

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
                foreach (var c in BattleAnimScript.Parse(bytes, WazaSeqVersion.HGSS))
                {
                    if (BattleAnimCommands.Name(WazaSeqVersion.HGSS, c.OpId) != "CallFunc" || c.Args.Length < 2) continue;
                    if (!found.ContainsKey(c.Args[0])) found[c.Args[0]] = c.Args;
                }
            }
            return found;
        }

        /// <summary>Everything the player can visibly do, as one string, so a change of any kind shows up.</summary>
        private static string Snapshot(BattleAnimPlayer w)
        {
            string s = "";
            for (int m = 0; m < 2; m++)
                s += $"{w.MonDX[m]},{w.MonDY[m]},{w.MonRot[m]},{w.MonScaleX[m]},{w.MonScaleY[m]},"
                   + $"{w.MonTintA[m]},{w.MonShakeX[m]},{w.MonShakeY[m]},{w.MonMosaic[m]},"
                   + $"{w.MonClip[m]},{w.MonAlpha[m]},{w.MonVisible[m]}|";
            s += $"{w.ShakeX},{w.ShakeY},{w.FadeOpacity},{w.BgFlashAmount},{w.Grayscale},{w.RasterActive},"
               + $"{w.HasBackground},{w.MonWarpAmp},{w.Ghosts.Count},{w.SpriteActors.Count},{w.Notes.Count}";
            return s;
        }

        [Fact]
        public void EveryRoutineTheScriptsCallMakesThePreviewDoSomething()
        {
            string dir = ScriptDir();
            Assert.True(dir != null, "the move-effect archive could not be unpacked, so nothing was checked");

            var calls = RealCalls(dir);
            Assert.True(calls.Count >= 77, $"only {calls.Count} routines were found being called");

            var opId = BattleAnimCommands.Id(WazaSeqVersion.HGSS, "CallFunc");
            var silent = new List<string>();
            int ran = 0;

            foreach (var kv in calls.OrderBy(k => k.Key))
            {
                int id = kv.Key;
                var script = new List<WazaSeqCommand> { new WazaSeqCommand(opId, kv.Value) { WordPos = 0 } };
                var w = new BattleAnimPlayer(script, WazaSeqVersion.HGSS, null, 64, 120, 190, 60,
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
                if (!moved) silent.Add($"{BattleAnimFuncs.Get(id)?.Name ?? id.ToString()} ({id})");
            }

            _out.WriteLine($"{ran} routines driven with a real call; {ran - silent.Count} changed something, {silent.Count} did not");
            foreach (var s in silent) _out.WriteLine("  silent: " + s);

            // Each of these is silent for a reason that was checked, not waved away.
            var expected = new[]
            {
                // The games' own sample routines, which really do nothing.
                "Nop (0)", "AnimExample (1)", "SoundExample (2)", "GenericExample (3)",
                // Keeps the dropped copies drawn while particle data streams in, which a preview never
                // waits for, so there is nothing to keep drawn.
                "RenderPokemonSprites (78)",
                // Right to do nothing with the words the scripts actually pass.
                "MoveBattlerToDefaultPos (62)", "Flail (27)",
                // These act on something an earlier command in the real script creates: a particle emitter,
                // a dropped copy, or a cell actor.
                "MoveEmitterA2BLinear (65)", "MoveEmitterA2BParabolic (66)", "RevolveEmitter (72)", "MoveEmitterViewportTop (73)",
                "SetPokemonSpritePriority (75)", "Superpower (56)", "Surf (49)",
            };
            var unexpected = silent.Except(expected).ToList();
            Assert.True(unexpected.Count == 0,
                $"{unexpected.Count} of {ran} routines did nothing at all: {string.Join(", ", unexpected)}");
        }
    }
}

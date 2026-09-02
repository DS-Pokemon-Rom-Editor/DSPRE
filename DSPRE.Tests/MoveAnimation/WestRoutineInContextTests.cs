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
    /// <summary>The routines that need the rest of their script around them.</summary>
    [Collection("rom")]
    public class WestRoutineInContextTests
    {
        private readonly ITestOutputHelper _out;
        public WestRoutineInContextTests(ITestOutputHelper o) { _out = o; }

        private const string HeartGold = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";

        // The seven the isolated harness cannot reach, with the name the table gives them.
        private static readonly (int Id, string Name)[] NeedContext =
        {
            (49, "WE_057"), (56, "WE_T08"), (65, "EMIT_STRAIGHT"), (66, "EMIT_PARABOLIC"),
            (72, "EMIT_ROTATION"), (73, "EMIT_SIMPLE_UD"), (75, "POKE_OAM_VIEW"),
        };

        private static string ScriptDir()
        {
            if (!Directory.Exists(HeartGold)) return null;
            try { new RomInfo("IPKE", HeartGold); } catch { return null; }
            var narc = new ScriptNarc(DirNames.wazaEffectScripts);
            return narc.Available ? gameDirs[DirNames.wazaEffectScripts].unpackedDir : null;
        }

        private static List<WazaSeqCommand> Load(byte[] bytes)
        {
            var cmds = WestScript.Parse(bytes, WazaSeqVersion.HGSS);
            int pos = 0;
            foreach (var c in cmds) { c.WordPos = pos; pos += 1 + c.Args.Length; }
            return cmds;
        }

        private static List<WazaSeqCommand> Copy(List<WazaSeqCommand> cmds)
            => cmds.Select(c => new WazaSeqCommand(c.OpId, (int[])c.Args.Clone()) { WordPos = c.WordPos }).ToList();

        private static string Frame(WestPlayer w)
        {
            var s = new System.Text.StringBuilder();
            for (int m = 0; m < 2; m++)
                s.Append(w.MonDX[m].ToString("F2")).Append(',').Append(w.MonDY[m].ToString("F2")).Append(',')
                 .Append(w.MonRot[m].ToString("F2")).Append(',').Append(w.MonScaleX[m].ToString("F2")).Append(',')
                 .Append(w.MonScaleY[m].ToString("F2")).Append(',').Append(w.MonTintA[m].ToString("F2")).Append(',')
                 .Append(w.MonShakeX[m].ToString("F2")).Append(',').Append(w.MonShakeY[m].ToString("F2")).Append(',')
                 .Append(w.MonMosaic[m].ToString("F2")).Append(',').Append(w.MonClip[m].ToString("F2")).Append(',')
                 .Append(w.MonAlpha[m].ToString("F2")).Append(',').Append(w.MonVisible[m]).Append('|');

            s.Append(w.ShakeX.ToString("F2")).Append(',').Append(w.ShakeY.ToString("F2")).Append(',')
             .Append(w.FadeOpacity.ToString("F3")).Append(',').Append(w.BgFlashAmount.ToString("F3")).Append(',')
             .Append(w.Grayscale).Append(',').Append(w.RasterActive).Append(',').Append(w.HasBackground).Append(',')
             .Append(w.MonWarpAmp.ToString("F2")).Append(',').Append(w.Ghosts.Count).Append(',').Append(w.CatsActors.Count);

            foreach (var g in w.Ghosts)
                s.Append('/').Append(g.Dx.ToString("F2")).Append(',').Append(g.Dy.ToString("F2")).Append(',')
                 .Append(g.Alpha.ToString("F2"));

            // The dropped copies, which is all several of these routines ever touch.
            foreach (var c in w.Caps)
                s.Append('@').Append(c.SrcMon).Append(',').Append(c.Dx.ToString("F2")).Append(',')
                 .Append(c.Dy.ToString("F2")).Append(',').Append(c.ScaleX.ToString("F2")).Append(',')
                 .Append(c.ScaleY.ToString("F2")).Append(',').Append(c.Alpha.ToString("F2")).Append(',')
                 .Append(c.RotDeg.ToString("F2")).Append(',').Append(c.Mosaic.ToString("F2")).Append(',')
                 .Append(c.TintA.ToString("F2")).Append(',').Append(c.Visible);

            foreach (var act in w.CatsActors)
                s.Append('^').Append(act.X.ToString("F2")).Append(',').Append(act.Y.ToString("F2")).Append(',')
                 .Append(act.ScaleX.ToString("F2")).Append(',').Append(act.ScaleY.ToString("F2")).Append(',')
                 .Append(act.Alpha.ToString("F2")).Append(',').Append(act.Visible);

            // Where the particles are, which is the whole point for the routines that move them.
            foreach (var p in w.LiveParticles())
                s.Append('#').Append(p.X.ToString("F2")).Append(',').Append(p.Y.ToString("F2")).Append(',')
                 .Append(p.Z.ToString("F2")).Append(',').Append(p.Scale.ToString("F2")).Append(',')
                 .Append(p.Alpha.ToString("F2")).Append(',').Append(p.Rotation.ToString("F3"));

            return s.ToString();
        }

        /// <summary>Every frame of a whole run, and whether it reached the routine being tested.</summary>
        private static (string Trace, bool Reached) Run(List<WazaSeqCommand> cmds, ScriptNarc particles,
                                                       bool attackerIsEnemy, bool secondTurn, int lookFor)
        {
            var w = new WestPlayer(cmds, WazaSeqVersion.HGSS, particles, 64, 120, 190, 60,
                                   attackerIsEnemy: attackerIsEnemy, selfTarget: false)
            { SecondTurnVariant = secondTurn };
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 900 && !w.Finished; i++) { w.Step(); sb.Append(Frame(w)).Append(';'); }
            return (sb.ToString(), w.RoutinesRun.Contains(lookFor));
        }

        [Fact]
        public void TheRoutinesThatNeedTheirScriptAroundThemChangeTheRun()
        {
            string dir = ScriptDir();
            Assert.True(dir != null, "the move-effect archive could not be unpacked, so nothing was checked");

            var files = RomFiles.Settled(dir);
            Assert.True(files.Length >= 500, "only " + files.Length + " scripts were available");

            var particles = new ScriptNarc(DirNames.wazaParticle);
            Assert.True(particles.Available, "the particle archive is missing, so the runs would not be comparable");

            int funcCall = WestOpcodes.Id(WazaSeqVersion.HGSS, "WEST_FUNC_CALL");
            const int emptyRoutine = 0;   // TEST_1, one of the sample routines, empty

            var unchanged = new List<string>();
            var neverReached = new List<string>();
            int checkedRoutines = 0, runsCompared = 0;

            foreach (var pair in NeedContext)
            {
                int id = pair.Id;
                string name = pair.Name;

                var callers = files.Where(f =>
                {
                    var b = File.ReadAllBytes(f);
                    return b.Length > 0 && Load(b).Any(c => c.OpId == funcCall && c.Args.Length > 0 && c.Args[0] == id);
                }).ToList();
                Assert.True(callers.Count > 0, name + " (" + id + ") is called by no script, so the list itself is wrong");

                checkedRoutines++;
                bool reachedAnywhere = false, differsSomewhere = false;
                int reachedRuns = 0;

                foreach (var f in callers)
                {
                    // Both sides, and both of the two animations a TURN_CHK move alternates between, because
                    // a call can sit in a branch only one of those four combinations ever runs.
                    foreach (bool asEnemy in new[] { false, true })
                    foreach (bool secondTurn in new[] { false, true })
                    {
                        var real = Load(File.ReadAllBytes(f));
                        var without = Copy(real);
                        foreach (var c in without)
                            if (c.OpId == funcCall && c.Args.Length > 0 && c.Args[0] == id) c.Args[0] = emptyRoutine;

                        var with = Run(real, particles, asEnemy, secondTurn, id);
                        if (!with.Reached) continue;   // this path does not call it; proves nothing
                        reachedAnywhere = true;
                        reachedRuns++;
                        runsCompared++;

                        var got = Run(without, particles, asEnemy, secondTurn, id);
                        if (with.Trace != got.Trace) { differsSomewhere = true; break; }
                    }
                    if (differsSomewhere) break;
                }

                if (!reachedAnywhere) neverReached.Add(name + " (" + id + "), " + callers.Count + " scripts");
                else if (!differsSomewhere) unchanged.Add(name + " (" + id + "), " + reachedRuns + " runs reached it");

                _out.WriteLine(name + " (" + id + "): " + callers.Count + " scripts call it, "
                               + reachedRuns + " runs reached it, "
                               + (!reachedAnywhere ? "NEVER REACHED"
                                  : differsSomewhere ? "the run changes when it is taken out"
                                  : "the run is identical without it"));
            }

            Assert.Equal(NeedContext.Length, checkedRoutines);
            Assert.True(runsCompared > 0, "no run actually reached any of these routines, so nothing was checked");
            _out.WriteLine(checkedRoutines + " routines, " + runsCompared
                           + " runs that reached one compared against the same script with that call emptied out");

            Assert.True(neverReached.Count == 0,
                neverReached.Count + " routines were never reached from either side, so this proves nothing about them: "
                + string.Join("; ", neverReached));
            Assert.True(unchanged.Count == 0,
                unchanged.Count + " routines make no difference to the run: " + string.Join("; ", unchanged));
        }
    }
}

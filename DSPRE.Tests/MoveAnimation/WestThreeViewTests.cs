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
    /// <summary>The three ways of reading a move script.</summary>
    [Collection("rom")]
    public class WestThreeViewTests
    {
        private readonly ITestOutputHelper _out;
        public WestThreeViewTests(ITestOutputHelper o) { _out = o; }

        private const string HeartGold = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";

        private static string ScriptDir()
        {
            if (!Directory.Exists(HeartGold)) return null;
            try { new RomInfo("IPKE", HeartGold); } catch { return null; }
            var narc = new ScriptNarc(DirNames.wazaEffectScripts);
            return narc.Available ? gameDirs[DirNames.wazaEffectScripts].unpackedDir : null;
        }

        private static List<WazaSeqCommand> Load(string path)
        {
            var cmds = WestScript.Parse(File.ReadAllBytes(path), WazaSeqVersion.HGSS);
            int pos = 0;
            foreach (var c in cmds) { c.WordPos = pos; pos += 1 + c.Args.Length; }
            return cmds;
        }

        [Fact]
        public void TheReadableViewsNameEveryRoutineInsteadOfNumberingIt()
        {
            string dir = ScriptDir();
            Assert.True(dir != null, "the move-effect archive could not be unpacked, so nothing was checked");

            int calls = 0, named = 0, scripts = 0;
            var bare = new List<string>();

            foreach (var f in RomFiles.Settled(dir))
            {
                var bytes = File.ReadAllBytes(f);
                if (bytes.Length == 0) continue;
                var cmds = Load(f);
                if (cmds.Count == 0) continue;
                scripts++;

                foreach (var mode in new[] { WestViewMode.Guided, WestViewMode.Script })
                {
                    var lines = WestScriptDisplay.Build(cmds, WazaSeqVersion.HGSS, mode);
                    foreach (var line in lines)
                    {
                        if (line.IsHeading || line.Index < 0 || line.Covers != 1) continue;
                        var c = cmds[line.Index];
                        if (WestOpcodes.Name(WazaSeqVersion.HGSS, c.OpId) != "WEST_FUNC_CALL" || c.Args.Length < 1) continue;
                        calls++;
                        string want = WestScriptDisplay.RoutineName(c.Args[0]);
                        if (line.Text.Contains(want)) named++;
                        else bare.Add($"{Path.GetFileName(f)}: {line.Text.Trim()}");
                    }
                }
            }

            _out.WriteLine($"{scripts} scripts; {calls} routine calls across the two readable views, {named} showing the routine's name");
            Assert.True(scripts >= 500, $"only {scripts} scripts were read");
            Assert.True(calls > 3000, $"only {calls} routine calls were seen");
            Assert.True(bare.Count == 0,
                $"{bare.Count} calls showed a bare number instead of the routine's name: {string.Join(" | ", bare.Take(5))}");
        }

        [Fact]
        public void TheRawViewLeavesEverythingWhereItIs()
        {
            string dir = ScriptDir();
            Assert.True(dir != null, "the move-effect archive could not be unpacked, so nothing was checked");

            int scripts = 0, lines = 0;
            foreach (var f in RomFiles.Settled(dir))
            {
                var bytes = File.ReadAllBytes(f);
                if (bytes.Length == 0) continue;
                var cmds = Load(f);
                if (cmds.Count == 0) continue;
                scripts++;

                var raw = WestScriptDisplay.Build(cmds, WazaSeqVersion.HGSS, WestViewMode.Raw);
                // One line per command, in order, nothing folded and nothing dropped.
                Assert.Equal(cmds.Count, raw.Count);
                for (int i = 0; i < cmds.Count; i++)
                {
                    Assert.Equal(i, raw[i].Index);
                    Assert.Equal(1, raw[i].Covers);
                    // Every word must be there to read, which is the whole point of this view.
                    foreach (int a in cmds[i].Args)
                        Assert.Contains(a.ToString(), raw[i].Text);
                }
                lines += raw.Count;
            }
            _out.WriteLine($"{scripts} scripts, {lines} lines, one for every command");
            Assert.True(scripts >= 500, $"only {scripts} scripts were read");
        }

        [Fact]
        public void RenamingARoutineChangesWhatAllThreeViewsCallIt()
        {
            string dir = ScriptDir();
            Assert.True(dir != null, "the move-effect archive could not be unpacked, so nothing was checked");

            // WT_SHAKE, the routine the scripts call most after the loading one.
            const int id = 36;
            string original = WestScriptDisplay.RoutineName(id);
            Assert.Equal("WT_SHAKE", original);

            try
            {
                LabelStore.SetLabel("west_routines", id, "Rattle the sprite", global: false);
                Assert.Equal("Rattle the sprite", WestScriptDisplay.RoutineName(id));

                // And it has to reach the lines themselves, in the views that name things.
                var cmds = new List<WazaSeqCommand>
                {
                    new WazaSeqCommand(WestOpcodes.Id(WazaSeqVersion.HGSS, "WEST_FUNC_CALL"),
                                       new[] { id, 5, 1, 0, 2, 6, 264 }) { WordPos = 0 },
                };
                foreach (var mode in new[] { WestViewMode.Guided, WestViewMode.Script })
                {
                    var lines = WestScriptDisplay.Build(cmds, WazaSeqVersion.HGSS, mode);
                    Assert.Contains(lines, l => l.Text.Contains("Rattle the sprite"));
                }
            }
            finally
            {
                LabelStore.SetLabel("west_routines", id, original, global: false);
            }
            Assert.Equal(original, WestScriptDisplay.RoutineName(id));
        }

        /// <summary>The guided view reads front to back, and its ending is at the end.</summary>
        [Fact]
        public void TheGuidedViewPutsTheEndingLast()
        {
            string dir = ScriptDir();
            Assert.True(dir != null, "the move-effect archive could not be unpacked, so nothing was checked");

            int scripts = 0, withEnding = 0;
            var wrong = new List<string>();

            foreach (var f in RomFiles.Settled(dir))
            {
                var bytes = File.ReadAllBytes(f);
                if (bytes.Length == 0) continue;
                var cmds = Load(f);
                if (cmds.Count == 0) continue;
                scripts++;

                var headings = WestScriptDisplay.Build(cmds, WazaSeqVersion.HGSS, WestViewMode.Guided)
                                                .Where(l => l.IsHeading).Select(l => l.Text).ToList();
                int at = headings.IndexOf("Where it ends");
                if (at < 0) continue;
                withEnding++;
                if (at != headings.Count - 1)
                    wrong.Add($"{Path.GetFileName(f)}: ending is heading {at + 1} of {headings.Count}");
            }

            _out.WriteLine($"{scripts} scripts, {withEnding} of them end with a SEQEND, {wrong.Count} put the ending early");
            Assert.True(scripts >= 500, $"only {scripts} scripts were read");
            Assert.True(withEnding >= 500, $"only {withEnding} scripts had an ending at all, so this checked almost nothing");
            Assert.True(wrong.Count == 0,
                $"{wrong.Count} scripts show their ending before the end: {string.Join(", ", wrong.Take(8))}");
        }

        /// <summary>Setting up the next command's arguments is not the same thing as waiting.</summary>
        [Fact]
        public void SettingUpArgumentsIsNotFiledUnderTiming()
        {
            var cmds = new List<WazaSeqCommand>
            {
                new WazaSeqCommand(WestOpcodes.Id(WazaSeqVersion.HGSS, "WEST_WORK_SET"), new[] { 4, 1 }) { WordPos = 0 },
                new WazaSeqCommand(WestOpcodes.Id(WazaSeqVersion.HGSS, "WEST_WAIT"), new[] { 10 }) { WordPos = 3 },
                new WazaSeqCommand(WestOpcodes.Id(WazaSeqVersion.HGSS, "WEST_TURN_CHK"), new[] { 3, 8 }) { WordPos = 5 },
            };
            var headings = WestScriptDisplay.Build(cmds, WazaSeqVersion.HGSS, WestViewMode.Guided)
                                            .Where(l => l.IsHeading).Select(l => l.Text).ToList();
            Assert.Contains("Settings for the next command", headings);
            Assert.Contains("How it is timed", headings);
            Assert.Contains("Which version plays", headings);
        }

        [Fact]
        public void ATargetFlagReadsAsWhoItHitsRatherThanANumber()
        {
            // WT_SHAKE's last word is a target flag; 264 is the defender's battle sprite.
            var cmds = new List<WazaSeqCommand>
            {
                new WazaSeqCommand(WestOpcodes.Id(WazaSeqVersion.HGSS, "WEST_FUNC_CALL"),
                                   new[] { 36, 5, 1, 0, 2, 6, 264 }) { WordPos = 0 },
            };
            var line = WestScriptDisplay.Build(cmds, WazaSeqVersion.HGSS, WestViewMode.Script).Single();
            Assert.Contains("defender", line.Text);
            Assert.DoesNotContain("264", line.Text);
            // The columnar views drop the "(as battle sprites)" half so the line fits the pane; the detail
            // pane beside them keeps it, which is where somebody goes for the whole answer.
            Assert.DoesNotContain("as battle sprites", line.Text);
            Assert.Contains("as battle sprites", line.Detail);
        }
    }
}

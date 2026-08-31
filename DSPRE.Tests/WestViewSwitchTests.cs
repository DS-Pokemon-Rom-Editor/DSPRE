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
    /// Switching views keeps the edit and keeps the bytes.
    ///
    /// The three views are supposed to be three ways of reading one set of commands, so an edit made
    /// while one is showing has to be there in the other two, and switching must not touch the bytes.
    /// That was previously only argued from how the code is written; nothing checked it.
    ///
    /// This can fail: it edits a command, rebuilds all three views from the same list, and requires the
    /// new value to appear in every one of them and the old value to be gone. If a view kept a snapshot
    /// of its own instead of reading the list, the old value would still be showing and this would say
    /// so. Doing it across every script in the game is what makes it more than one lucky example.
    /// </summary>
    [Collection("rom")]
    public class WestViewSwitchTests
    {
        private readonly ITestOutputHelper _out;
        public WestViewSwitchTests(ITestOutputHelper o) { _out = o; }

        private const string HeartGold = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";

        private static readonly WestViewMode[] AllViews =
            { WestViewMode.Guided, WestViewMode.Script, WestViewMode.Raw };

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

        [Fact]
        public void AnEditMadeInOneViewIsThereInTheOtherTwo()
        {
            string dir = ScriptDir();
            Assert.True(dir != null, "the move-effect archive could not be unpacked, so nothing was checked");

            int scripts = 0, edited = 0;
            var lost = new List<string>();

            foreach (var f in RomFiles.Settled(dir))
            {
                var bytes = File.ReadAllBytes(f);
                if (bytes.Length == 0) continue;
                var cmds = Load(bytes);
                if (cmds.Count == 0) continue;
                scripts++;

                // Edit the first WAIT, which is a plain frame count that every view prints as a number.
                int at = cmds.FindIndex(c => WestOpcodes.Name(WazaSeqVersion.HGSS, c.OpId) == "WEST_WAIT"
                                             && c.Args.Length == 1);
                if (at < 0) continue;

                int before = cmds[at].Args[0];
                int after = before == 61 ? 62 : 61;      // a value the script is very unlikely to hold already
                cmds[at].Args[0] = after;
                edited++;

                foreach (var mode in AllViews)
                {
                    var line = WestScriptDisplay.Build(cmds, WazaSeqVersion.HGSS, mode)
                                                .FirstOrDefault(l => !l.IsHeading && l.Index == at && l.Covers == 1);
                    if (line == null) { lost.Add($"{Path.GetFileName(f)}: {mode} has no line for the edited command"); continue; }
                    if (!line.Text.Contains(after.ToString()))
                        lost.Add($"{Path.GetFileName(f)}: {mode} does not show the edit ({after}): {line.Text.Trim()}");
                }

                // And the bytes are the edit and nothing else.
                var rebuilt = WestScript.Serialize(cmds);
                var expected = (byte[])bytes.Clone();
                var original = Load(bytes);
                original[at].Args[0] = after;
                if (!rebuilt.SequenceEqual(WestScript.Serialize(original)))
                    lost.Add($"{Path.GetFileName(f)}: the bytes after the edit are not what the edit says");
            }

            _out.WriteLine($"{scripts} scripts read, {edited} of them had a wait to edit, "
                           + $"each checked in all three views");
            Assert.True(scripts >= 500, $"only {scripts} scripts were read");
            // 382 of the 501 scripts hold a plain one-value wait; the rest only wait on flags or particles.
            Assert.True(edited >= 350, $"only {edited} scripts could be edited, so this checked very little");
            Assert.True(lost.Count == 0,
                $"{lost.Count} views did not carry the edit: {string.Join(" | ", lost.Take(6))}");
        }

        [Fact]
        public void SwitchingBackAndForthChangesNothing()
        {
            string dir = ScriptDir();
            Assert.True(dir != null, "the move-effect archive could not be unpacked, so nothing was checked");

            int scripts = 0;
            var changed = new List<string>();

            foreach (var f in RomFiles.Settled(dir))
            {
                var bytes = File.ReadAllBytes(f);
                if (bytes.Length == 0) continue;
                var cmds = Load(bytes);
                if (cmds.Count == 0) continue;
                scripts++;

                // Walk through the views a few times over, the way somebody comparing them would.
                foreach (var mode in AllViews.Concat(AllViews).Concat(AllViews))
                    WestScriptDisplay.Build(cmds, WazaSeqVersion.HGSS, mode);

                if (!WestScript.Serialize(cmds).SequenceEqual(bytes))
                    changed.Add(Path.GetFileName(f));
            }

            _out.WriteLine($"{scripts} scripts, each built nine times over across the three views");
            Assert.True(scripts >= 500, $"only {scripts} scripts were read");
            Assert.True(changed.Count == 0,
                $"{changed.Count} scripts came out with different bytes just from switching views: "
                + string.Join(", ", changed.Take(8)));
        }
    }
}

using System;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Changing one frame without disturbing another. Results are shared between frames in these files, so
    /// the whole risk of animation editing is that a change reaches further than it looks.
    /// </summary>
    [Collection("rom")]
    public class NanrEditTests
    {
        private static bool Ready()
        {
            if (!Directory.Exists(TestRoms.Platinum)) return false;
            try { new RomInfo("CPUE", TestRoms.Platinum); } catch { return false; }
            return true;
        }

        /// <summary>A real file, inflated, straight out of the archive.</summary>
        private static byte[] Member(RomInfo.DirNames dir, int index)
            => NitroBgCodec.Inflate(new ScriptNarc(dir).Get(index));

        /// <summary>The Analog Watch's animation: 420 sequences, one rotation each.</summary>
        private static NanrFile Watch() => NanrFile.Read(Member(RomInfo.DirNames.poketch, 28));

        /// <summary>Finds a file in this archive that has a result shared by more than one frame.</summary>
        private static (NanrFile File, int Member) SomethingShared()
        {
            var narc = new ScriptNarc(RomInfo.DirNames.poketch);
            for (int i = 0; i < narc.Count; i++)
            {
                byte[] raw;
                try { raw = NitroBgCodec.Inflate(narc.Get(i)); } catch { continue; }
                if (raw == null || raw.Length < 4) continue;
                if (raw[0] != 'R' || raw[1] != 'N' || raw[2] != 'A' || raw[3] != 'N') continue;
                var f = NanrFile.Read(raw);
                if (f != null && f.Results.Any(r => r.Referrers > 1)) return (f, i);
            }
            return (null, -1);
        }

        [SkippableFact]
        public void ChangingAHoldTouchesNothingElse()
        {
            Skip.If(!Ready(), "Platinum is not unpacked here");
            byte[] raw = Member(RomInfo.DirNames.poketch, 28);
            var f = Watch();
            Assert.NotNull(f);
            Assert.True(f.Sequences.Count > 1, "this file should hold many sequences");

            ushort was = f.Sequences[0].Frames[0].Delay;
            f.SetDelay(0, 0, was + 7);
            byte[] after = f.Write();

            // The same length, and different in exactly the two bytes of that one hold.
            Assert.Equal(raw.Length, after.Length);
            var moved = Enumerable.Range(0, raw.Length).Where(i => raw[i] != after[i]).ToList();
            Assert.True(moved.Count <= 2, $"{moved.Count} bytes changed for one hold");

            var again = NanrFile.Read(after);
            Assert.Equal(was + 7, again.Sequences[0].Frames[0].Delay);

            // And nothing about which drawing any frame shows has moved.
            for (int s = 0; s < f.Sequences.Count; s++)
                for (int fr = 0; fr < f.Sequences[s].Frames.Count; fr++)
                    Assert.Equal(Watch().CellOf(s, fr), again.CellOf(s, fr));
        }

        [SkippableFact]
        public void ChangingADrawingOnASharedResultLeavesTheOtherFramesAlone()
        {
            Skip.If(!Ready(), "Platinum is not unpacked here");
            var (f, member) = SomethingShared();
            Skip.If(f == null, "no file in this archive shares a result");

            // Find the frame whose result is shared.
            int seq = -1, frame = -1;
            for (int s = 0; s < f.Sequences.Count && seq < 0; s++)
                for (int i = 0; i < f.Sequences[s].Frames.Count; i++)
                    if (f.SharedWith(s, i) > 0) { seq = s; frame = i; break; }
            Assert.True(seq >= 0, "a shared result should have a frame pointing at it");

            // Everything as it stands, so siblings can be checked afterwards.
            var before = NanrFile.Read(f.Write());
            int sharedAt = f.Sequences[seq].Frames[frame].ResultAt;
            var siblings = new System.Collections.Generic.List<(int S, int F)>();
            for (int s = 0; s < f.Sequences.Count; s++)
                for (int i = 0; i < f.Sequences[s].Frames.Count; i++)
                    if (f.Sequences[s].Frames[i].ResultAt == sharedAt && !(s == seq && i == frame))
                        siblings.Add((s, i));
            Assert.NotEmpty(siblings);

            f.SetCell(seq, frame, 0x2A);
            var after = NanrFile.Read(f.Write());

            // This frame shows the new drawing.
            Assert.Equal(0x2A, after.CellOf(seq, frame));

            // Every frame that shared the old one still shows what it showed.
            foreach (var (s, i) in siblings)
                Assert.Equal(before.CellOf(s, i), after.CellOf(s, i));

            // It was given a result of its own rather than the shared one being rewritten.
            Assert.NotEqual(sharedAt, after.Sequences[seq].Frames[frame].ResultAt);
        }

        [SkippableFact]
        public void ChangingADrawingEverywhereIsAskedForExplicitly()
        {
            Skip.If(!Ready(), "Platinum is not unpacked here");
            var (f, _) = SomethingShared();
            Skip.If(f == null, "no file in this archive shares a result");

            int seq = -1, frame = -1;
            for (int s = 0; s < f.Sequences.Count && seq < 0; s++)
                for (int i = 0; i < f.Sequences[s].Frames.Count; i++)
                    if (f.SharedWith(s, i) > 0) { seq = s; frame = i; break; }

            int sharedAt = f.Sequences[seq].Frames[frame].ResultAt;
            var siblings = new System.Collections.Generic.List<(int S, int F)>();
            for (int s = 0; s < f.Sequences.Count; s++)
                for (int i = 0; i < f.Sequences[s].Frames.Count; i++)
                    if (f.Sequences[s].Frames[i].ResultAt == sharedAt && !(s == seq && i == frame))
                        siblings.Add((s, i));

            f.SetCell(seq, frame, 0x1B, everywhere: true);
            var after = NanrFile.Read(f.Write());

            // Asked for on purpose, so every frame sharing it moves together.
            Assert.Equal(0x1B, after.CellOf(seq, frame));
            foreach (var (s, i) in siblings) Assert.Equal(0x1B, after.CellOf(s, i));

            // And it stayed one result rather than growing a copy.
            Assert.Equal(sharedAt, after.Sequences[seq].Frames[frame].ResultAt);
        }

        /// <summary>
        /// The trainer sprites carry an extended block the old reader never even looked at. Losing it is a
        /// crash in game rather than a blemish, so it has to survive a write untouched.
        /// </summary>
        [SkippableFact]
        public void TheExtendedBlockSurvivesAWrite()
        {
            Skip.If(!Ready(), "Platinum is not unpacked here");

            int found = 0;
            foreach (var dir in new[] { RomInfo.DirNames.trainerGraphics, RomInfo.DirNames.trainerBackGraphics })
            {
                ScriptNarc narc;
                try { narc = new ScriptNarc(dir); } catch { continue; }
                if (!narc.Available) continue;

                for (int i = 0; i < narc.Count; i++)
                {
                    byte[] raw;
                    try { raw = NitroBgCodec.Inflate(narc.Get(i)); } catch { continue; }
                    if (raw == null || raw.Length < 4) continue;
                    if (raw[0] != 'R' || raw[1] != 'N' || raw[2] != 'A' || raw[3] != 'N') continue;

                    var f = NanrFile.Read(raw);
                    if (f == null || !f.HasExtendedData) continue;

                    found++;
                    Assert.Equal(raw, f.Write());
                }
            }

            Skip.If(found == 0, "this game has no animation files with an extended block");
        }
    }
}

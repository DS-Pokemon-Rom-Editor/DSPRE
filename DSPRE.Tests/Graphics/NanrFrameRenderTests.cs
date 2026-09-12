using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// Draws the frames of real animations through the same path the editor draws them with, and writes
    /// each one out as a picture. This is how an animation is shown to be playing rather than asserted to
    /// be: the frames either differ from one another or they do not.
    /// </summary>
    [Collection("rom")]
    public class NanrFrameRenderTests
    {
        private readonly ITestOutputHelper _out;
        public NanrFrameRenderTests(ITestOutputHelper output) => _out = output;

        private static string Where => Environment.GetEnvironmentVariable("DSPRE_FRAME_OUT");

        private static bool Ready()
        {
            if (!Directory.Exists(TestRoms.Platinum)) return false;
            try { new RomInfo("CPUE", TestRoms.Platinum); } catch { return false; }
            return true;
        }

        private static byte[] Member(int i)
            => NitroBgCodec.Inflate(new ScriptNarc(RomInfo.DirNames.poketch).Get(i));

        /// <summary>
        /// An animation, the layout its frames name, the sheet that layout draws from, and the shared sheet
        /// the game loads ahead of it where there is one. Stopwatch's cells start at tile 80 and reach 424
        /// while its own sheet holds 348 tiles, because the first 80 belong to the shared figures.
        /// </summary>
        private static readonly (int Anim, int Cells, int Sprites, int Shared, string Name)[] Subjects =
        {
            (73, 72, 74, -1, "matchup-checker"),   // 16 frames, loops: the Luvdisc swim
            (19, 18, 22,  2, "stopwatch"),         // 4 frames, loops, counts from the shared figures
            (98, 97, 99, -1, "link-searcher"),     // 6 frames, loops
            (41, 40, 42, -1, "dowsing-machine"),   // 18 frames, plays once: the radar sweep
        };

        // Sprite memory holds the shared sheet first, then the screen's own.
        private static byte[] Sheet(int own, int shared)
        {
            byte[] mine = DsBgScreen.ReadCharacters(Member(own));
            if (shared < 0) return mine;
            byte[] first = DsBgScreen.ReadCharacters(Member(shared));
            if (first.Length == 0) return mine;
            var both = new byte[first.Length + mine.Length];
            first.CopyTo(both, 0);
            mine.CopyTo(both, first.Length);
            return both;
        }

        [SkippableFact]
        public void TheFramesOfARealAnimationDifferFromOneAnother()
        {
            Skip.If(!Ready(), "Platinum is not unpacked here");
            string outDir = Where;

            int drawn = 0, moving = 0;
            foreach (var (anim, cellsAt, spritesAt, shared, name) in Subjects)
            {
                var file = NanrFile.Read(Member(anim));
                Assert.NotNull(file);

                var banks = DsBgScreen.ReadCells(Member(cellsAt));
                byte[] chars = Sheet(spritesAt, shared);
                ushort[] colours = DsBgScreen.Row(DsBgScreen.ReadColours(Member(0)), 0);
                Assert.NotEmpty(banks);

                // The longest sequence is the one worth looking at.
                int pick = -1, longest = 0;
                for (int i = 0; i < file.Sequences.Count; i++)
                    if (file.Sequences[i].Frames.Count > longest)
                    { longest = file.Sequences[i].Frames.Count; pick = i; }
                Assert.True(pick >= 0, $"{name} has no sequences");
                Skip.If(longest < 2, $"{name}'s longest sequence is one frame");

                var shots = new List<byte[]>();
                for (int f = 0; f < longest; f++)
                {
                    var rgba = new byte[DsBgScreen.Width * DsBgScreen.Height * 4];
                    int cell = file.CellOf(pick, f);
                    var (sx, sy) = file.ShiftOf(pick, f);
                    var (deg, kx, ky) = file.TurnOf(pick, f);
                    if (cell >= 0 && cell < banks.Count)
                        DsBgScreen.DrawCellTurned(rgba, banks[cell], chars, _ => colours,
                                                  DsBgScreen.Width / 2 + sx, DsBgScreen.Height / 2 + sy,
                                                  deg, kx, ky);
                    shots.Add(rgba);
                    drawn++;

                    if (outDir != null)
                    {
                        Directory.CreateDirectory(Path.Combine(outDir, name));
                        File.WriteAllBytes(Path.Combine(outDir, name, $"{f:d2}.raw"), rgba);
                    }
                }

                // At least one frame has to differ from the first, or nothing is animating.
                bool differs = shots.Skip(1).Any(s => !s.SequenceEqual(shots[0]));
                Assert.True(differs, $"{name}: every one of its {longest} frames drew the same picture");
                moving++;

                var seq = file.Sequences[pick];
                _out.WriteLine($"{name}: sequence {pick}, {longest} frames, "
                             + $"holds {string.Join(",", seq.Frames.Select(x => x.Delay))}, "
                             + $"mode {seq.PlayMode}, cells "
                             + string.Join(",", Enumerable.Range(0, longest).Select(f => file.CellOf(pick, f))));
            }

            Assert.True(drawn > 0, "nothing was drawn");
            Assert.True(moving >= 2, $"only {moving} animations were shown to move");
            _out.WriteLine($"drew {drawn} frames across {moving} animations"
                         + (outDir == null ? "" : $", written to {outDir}"));
        }

        /// <summary>
        /// A hold of zero would spin the preview as fast as the clock ticks, so every frame of the ones
        /// being shown has a real duration.
        /// </summary>
        [SkippableFact]
        public void EveryFrameOfThoseAnimationsIsHeldForAtLeastOneTick()
        {
            Skip.If(!Ready(), "Platinum is not unpacked here");
            int checkedFrames = 0;
            foreach (var (anim, _, _, _, name) in Subjects)
            {
                var file = NanrFile.Read(Member(anim));
                Assert.NotNull(file);
                foreach (var s in file.Sequences)
                    foreach (var f in s.Frames)
                    {
                        Assert.True(f.Delay >= 1, $"{name} holds a frame for {f.Delay} ticks");
                        checkedFrames++;
                    }
            }
            Assert.True(checkedFrames > 0, "no frames were checked");
        }
    }
}

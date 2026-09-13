using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>The border round a message box, read out of the ROM so an edited one shows.</summary>
    [Collection("rom")]
    public class FieldWindowFrameTests
    {
        private static readonly string Project = TestRoms.HeartGold;

        private static FieldWindowFrame Frame(int index = 0)
        {
            Assert.True(Directory.Exists(Project), $"these tests need the project at {Project}");
            new RomInfo("IPKE", Project);
            var f = FieldWindowFrame.Load(index);
            Assert.NotNull(f);
            return f;
        }

        [Fact]
        public void TheBorderComesOutTheSizeTheMessageBoxNeeds()
        {
            var f = Frame();
            byte[] rgba = f.Compose(FieldMessageWindow.TilesWide, FieldMessageWindow.TilesHigh,
                                    out int w, out int h);

            // Two tile columns left of the writing and three right, a row above and below: for this
            // window that is the whole screen width and the bottom forty eight pixels.
            Assert.Equal(FieldMessageWindow.FrameWidth, w);
            Assert.Equal(FieldMessageWindow.FrameHeight, h);
            Assert.Equal(FieldMessageWindow.ScreenWidth, w);
            Assert.Equal(w * h * 4, rgba.Length);
        }

        [Fact]
        public void TheCornersAreSeeThroughSoTheBoxIsRounded()
        {
            var f = Frame();
            byte[] rgba = f.Compose(FieldMessageWindow.TilesWide, FieldMessageWindow.TilesHigh,
                                    out int w, out int h);

            byte AlphaAt(int x, int y) => rgba[(y * w + x) * 4 + 3];

            // Colour 0 is the one the hardware leaves see-through on a background layer, and these
            // frames use it to round the corners off.
            Assert.Equal(0, AlphaAt(0, 0));
            Assert.Equal(0, AlphaAt(w - 1, 0));
            Assert.Equal(0, AlphaAt(0, h - 1));
            Assert.Equal(0, AlphaAt(w - 1, h - 1));

            // The middle of an edge is solid, or there would be no border at all.
            Assert.Equal(255, AlphaAt(w / 2, 2));
        }

        [Fact]
        public void TheBorderLeavesTheMiddleAloneBecauseThatIsWhereTheWritingGoes()
        {
            var f = Frame();
            byte[] rgba = f.Compose(FieldMessageWindow.TilesWide, FieldMessageWindow.TilesHigh,
                                    out int w, out int h);
            byte AlphaAt(int x, int y) => rgba[(y * w + x) * 4 + 3];

            // BmpTalkWinWriteMain lays out seventeen of the eighteen tiles and leaves number 8, the
            // middle one, out. The middle is the bitmap window, which talk_msg.c fills itself.
            for (int x = FieldMessageWindow.TextLeft; x < FieldMessageWindow.TextLeft + FieldMessageWindow.TextWidth; x++)
                Assert.Equal(0, AlphaAt(x, h / 2));

            // The sides of that same row are painted, or there would be no border at all.
            Assert.Equal(255, AlphaAt(4, h / 2));
            Assert.Equal(255, AlphaAt(w - 5, h / 2));
        }

        [Fact]
        public void ThePaperTheWritingSitsOnIsTheColourTheGamesFillWith()
        {
            // talk_msg.c:121 fills the box with colour 15 before writing in it.
            uint paper = Frame().PaperArgb;
            Assert.Equal(0xFFu, paper >> 24);      // solid, not see-through
        }

        [Fact]
        public void AllTwentyFramesTheGameOffersCanBeRead()
        {
            Assert.True(Directory.Exists(Project), $"these tests need the project at {Project}");
            new RomInfo("IPKE", Project);

            int read = 0;
            for (int i = 0; i < FieldWindowFrame.FrameCount; i++)
            {
                var f = FieldWindowFrame.Load(i);
                Assert.True(f != null, $"frame {i} could not be read");
                f.Compose(4, 2, out int w, out int h);
                Assert.Equal((2 + 4 + 3) * 8, w);
                Assert.Equal((1 + 2 + 1) * 8, h);
                read++;
            }
            Assert.Equal(FieldWindowFrame.FrameCount, read);
        }

        [Fact]
        public void DifferentFramesReallyLookDifferent()
        {
            // If the index were being ignored every frame would come out the same.
            byte[] first = Frame(0).Compose(8, 2, out _, out _);
            byte[] other = Frame(5).Compose(8, 2, out _, out _);
            Assert.False(first.SequenceEqual(other), "frame 5 came out identical to frame 0");
        }

        [Fact]
        public void AskingForAFrameThatIsNotThereFallsBackRatherThanFailing()
        {
            var fallback = Frame(999);
            byte[] a = fallback.Compose(8, 2, out _, out _);
            byte[] b = Frame(0).Compose(8, 2, out _, out _);
            Assert.True(a.SequenceEqual(b), "an out of range frame should come back as the first one");
        }

        /// <summary>Each frame uses its own colours; a fixed palette entry gave Platinum the next frame's.</summary>
        [SkippableTheory]
        [InlineData("IPKE", "HeartGold")]
        [InlineData("CPUE", "Platinum")]
        [InlineData("ADAE", "Diamond")]
        public void EachFrameIsDrawnInItsOwnColours(string code, string game)
        {
            string project = code == "IPKE" ? TestRoms.HeartGold : code == "CPUE" ? TestRoms.Platinum : TestRoms.Diamond;
            Skip.If(!Directory.Exists(project), $"{game} not unpacked here");
            new RomInfo(code, project);

            var narc = new DSPRE.Avalonia.Data.ScriptNarc(RomInfo.DirNames.windowFrames);
            var tags = Enumerable.Range(0, narc.Count).Select(i =>
            {
                var b = narc.Get(i);
                return b == null || b.Length < 4 ? "" : new string(new[] { (char)b[0], (char)b[1], (char)b[2], (char)b[3] });
            }).ToList();
            int systemColours = tags.IndexOf("RLCN");
            Assert.True(systemColours > 0, $"{game}: no palettes in the window frame archive");

            for (int i = 0; i < FieldWindowFrame.FrameCount; i++)
            {
                var f = FieldWindowFrame.Load(i);
                Assert.NotNull(f);
                Assert.Equal(systemColours + 1 + i, f.PaletteEntry);
                Assert.Equal("RLCN", tags[f.PaletteEntry]);
            }
        }

        /// <summary>
        /// Menus and the yes/no box are filled and written in the system font's colours; the border's own
        /// colour 15 is black, which is what turned them black.
        /// </summary>
        [SkippableTheory]
        [InlineData("IPKE", "HeartGold")]
        [InlineData("CPUE", "Platinum")]
        public void MenuWindowsAreWhiteWithGreyWriting(string code, string game)
        {
            string project = code == "IPKE" ? TestRoms.HeartGold : TestRoms.Platinum;
            Skip.If(!Directory.Exists(project), $"{game} not unpacked here");
            new RomInfo(code, project);

            uint[] font = FieldWindowFrame.LoadSystemFontColours();
            Assert.NotNull(font);
            Assert.Equal(0xFF5A5A52u, font[1]);     // letters
            Assert.Equal(0xFFACBDBDu, font[2]);     // their shadow
            Assert.Equal(0xFFFFFFFFu, font[15]);    // the paper

            var standard = FieldWindowFrame.LoadStandard();
            Assert.NotNull(standard);
            byte[] rgba = standard.ComposeStandard(6, 4, out int w, out int h, font[15]);
            int at = (h / 2 * w + w / 2) * 4;
            Assert.Equal(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, rgba.Skip(at).Take(4).ToArray());
            // The border itself is still drawn from its own colours.
            Assert.Equal(0, rgba[3]);                // a see-through corner
            Assert.Equal(0xFF, rgba[(4 * w + w / 2) * 4 + 3]);
        }

        [Fact]
        public void TheBorderStretchesToWhateverWidthItIsAskedFor()
        {
            var f = Frame();
            f.Compose(10, 2, out int narrow, out _);
            f.Compose(20, 2, out int wide, out _);
            Assert.Equal((20 - 10) * 8, wide - narrow);
        }
    }
}

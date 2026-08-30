using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The box an NPC talks from: where it sits, and how the words are broken up to fit it.
    /// </summary>
    public class FieldMessageWindowTests
    {
        // Six pixels a letter, so the sums in these tests are easy to follow.
        private static FieldTextLayout Layout(int width = FieldMessageWindow.TextWidth, int lines = 2)
            => new FieldTextLayout(t => (t ?? "").Length * 6, width, lines);

        [Fact]
        public void TheBoxSitsWhereTheGamesPutIt()
        {
            // talk_msg.c:79 with FLD_MSG_WIN_PX/PY/SX/SY from fld_bmp.h: tile 2,19 and 27 by 4 tiles.
            Assert.Equal(2, FieldMessageWindow.TileX);
            Assert.Equal(19, FieldMessageWindow.TileY);
            Assert.Equal(27, FieldMessageWindow.TilesWide);
            Assert.Equal(4, FieldMessageWindow.TilesHigh);

            Assert.Equal(16, FieldMessageWindow.TextLeft);
            Assert.Equal(152, FieldMessageWindow.TextTop);
            Assert.Equal(216, FieldMessageWindow.TextWidth);
            Assert.Equal(32, FieldMessageWindow.TextHeight);

            // BmpTalkWinWriteMain puts two tile columns left of the writing and three right of it, plus
            // a row above and below, which for this window is the whole screen width and the bottom
            // forty eight pixels.
            Assert.Equal(0, FieldMessageWindow.FrameLeft);
            Assert.Equal(144, FieldMessageWindow.FrameTop);
            Assert.Equal(FieldMessageWindow.ScreenWidth, FieldMessageWindow.FrameWidth);
            Assert.Equal(48, FieldMessageWindow.FrameHeight);
            Assert.Equal(FieldMessageWindow.ScreenHeight,
                         FieldMessageWindow.FrameTop + FieldMessageWindow.FrameHeight);

            // Two sixteen pixel lines fill the four tiles it is high.
            Assert.Equal(16, FieldMessageWindow.LineHeight);
            Assert.Equal(FieldMessageWindow.TextHeight,
                         FieldMessageWindow.LineHeight * FieldMessageWindow.LinesPerPage);
        }

        [Fact]
        public void NoLineIsEverWiderThanTheBox()
        {
            var layout = Layout();
            string text = string.Join(" ", Enumerable.Repeat("PIKACHU", 40));

            foreach (string line in layout.Lines(text))
                Assert.True(line.Length * 6 <= FieldMessageWindow.TextWidth,
                    $"this line runs past the edge of the box: {line}");
        }

        [Fact]
        public void APageNeverHoldsMoreLinesThanTheBoxShows()
        {
            var layout = Layout();
            var pages = layout.Pages(string.Join(" ", Enumerable.Repeat("TRAINER", 60)));

            Assert.NotEmpty(pages);
            foreach (string page in pages)
                Assert.True(page.Split('\n').Length <= FieldMessageWindow.LinesPerPage);
        }

        [Fact]
        public void EveryWordSurvivesTheBreakingUp()
        {
            var layout = Layout();
            const string text = "I am training my POKEMON so that I can beat the GYM LEADER one day.";

            string back = string.Join(" ", layout.Pages(text)).Replace("\n", " ");
            Assert.Equal(text.Split(' '), back.Split(' '));
        }

        [Fact]
        public void AWordTooLongForTheBoxStillGetsALineOfItsOwn()
        {
            var layout = Layout(width: 30);      // five letters fit
            var lines = layout.Lines("tiny ENORMOUSWORD tiny");

            Assert.Contains("ENORMOUSWORD", lines);
            Assert.Equal(3, lines.Count);
        }

        [Fact]
        public void NothingToSayMakesNoPages()
        {
            var layout = Layout();
            Assert.Empty(layout.Pages(""));
            Assert.Empty(layout.Pages(null));
        }
    }
}

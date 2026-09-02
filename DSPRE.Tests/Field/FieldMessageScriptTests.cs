using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The three ways a message can break, which PokeFontPrint in pmfprint.c treats quite differently.
    /// </summary>
    public class FieldMessageScriptTests
    {
        // Six pixels a letter keeps the arithmetic in these tests obvious.
        private static int Measure(string t) => (t ?? "").Length * 6;

        private static System.Collections.Generic.List<FieldMessageFrame> Frames(string text)
            => FieldMessageScript.Frames(text, Measure);

        [Fact]
        public void ALineBreakDoesNotStopToWaitForAnyone()
        {
            // CR_ returns PRINT_RESULT_LOOP: it moves down a line and carries straight on.
            var frames = Frames("first line\\nsecond line");

            Assert.Single(frames);
            Assert.Equal(new[] { "first line", "second line" }, frames[0].Lines);
            Assert.Equal(MessageWait.None, frames[0].Wait);
        }

        [Fact]
        public void TheClearCodeWaitsAndThenEmptiesTheBox()
        {
            // NORMAL_WAIT_ goes to PRINTSEQ_TRGWAIT_CLEAR, which fills the window with the background
            // and puts the writing position back to the very start.
            var frames = Frames("page one\\rpage two");

            Assert.Equal(2, frames.Count);
            Assert.Equal(new[] { "page one" }, frames[0].Lines);
            Assert.Equal(MessageWait.Clear, frames[0].Wait);

            // Nothing of the first page is left over.
            Assert.Equal(new[] { "page two" }, frames[1].Lines);
            Assert.Equal(MessageWait.None, frames[1].Wait);
        }

        [Fact]
        public void TheScrollCodeKeepsTheLastLineAndCarriesOnUnderneath()
        {
            // SCROLL_WAIT_ goes to PRINTSEQ_TRGWAIT_SCROLL, which shifts the window up by one line
            // height. The line that was at the bottom is still there, at the top.
            var frames = Frames("line one\\nline two\\fline three");

            Assert.Equal(2, frames.Count);
            Assert.Equal(new[] { "line one", "line two" }, frames[0].Lines);
            Assert.Equal(MessageWait.Scroll, frames[0].Wait);

            // This is the whole point: "line two" survives the scroll, a clear would have lost it.
            Assert.Equal(new[] { "line two", "line three" }, frames[1].Lines);
        }

        [Fact]
        public void ScrollingIsNotTheSameAsClearing()
        {
            var scrolled = Frames("one\\ntwo\\fthree");
            var cleared = Frames("one\\ntwo\\rthree");

            Assert.Equal(new[] { "two", "three" }, scrolled[1].Lines);
            Assert.Equal(new[] { "three" }, cleared[1].Lines);
            Assert.NotEqual(scrolled[1].Lines, cleared[1].Lines);
        }

        [Fact]
        public void TheRealTriangleCharactersMeanTheSameAsTheWrittenOnes()
        {
            // 0x25bc is "▼" and 0x25bd is "▽"; the text may carry either the character or the spelling.
            Assert.Equal(Frames("a\\rb").Select(f => f.Wait), Frames("a▼b").Select(f => f.Wait));
            Assert.Equal(Frames("a\\fb").Select(f => f.Wait), Frames("a▽b").Select(f => f.Wait));
        }

        [Fact]
        public void TheWaitOnlyCodeLeavesTheBoxExactlyAsItWas()
        {
            // SIMPLE_WAIT_, 0x25a0, waits and does nothing else.
            var frames = Frames("hold on■ and on");

            Assert.Equal(2, frames.Count);
            Assert.Equal(MessageWait.Simple, frames[0].Wait);
            Assert.Equal(new[] { "hold on" }, frames[0].Lines);
        }

        [Fact]
        public void SeveralWaitsInARowAllGetTheirOwnTurn()
        {
            var frames = Frames("one\\rtwo\\rthree\\rfour");
            Assert.Equal(4, frames.Count);
            Assert.Equal(MessageWait.Clear, frames[0].Wait);
            Assert.Equal(MessageWait.Clear, frames[2].Wait);
            Assert.Equal(MessageWait.None, frames[3].Wait);
        }

        [Fact]
        public void TheLastFrameNeverAsksForAnotherPress()
        {
            foreach (string text in new[] { "plain", "a\\nb", "a\\rb", "a\\fb", "a■b" })
                Assert.Equal(MessageWait.None, Frames(text).Last().Wait);
        }

        // ── saying when the text will not fit ───────────────────────────────────────────
        [Fact]
        public void ALineWiderThanTheBoxIsReportedRatherThanQuietlyReflowed()
        {
            // The games do not fit text for you: writing past the edge just draws outside the window.
            string tooLong = new string('W', 60);
            var frames = Frames(tooLong + "\\rshort");

            Assert.True(frames[0].TooWide, "an over-long line should be called out");
            Assert.False(frames[1].TooWide);
        }

        [Fact]
        public void MoreLinesThanTheBoxHoldsIsReported()
        {
            var frames = Frames("one\\ntwo\\nthree");
            Assert.True(frames[0].TooManyLines, "a third line does not fit and should be called out");
            Assert.Equal(2, frames[0].Lines.Count);
        }

        [Fact]
        public void TextThatCarriesNoBreaksAtAllIsFittedToTheBox()
        {
            // Script text that has never been through the games' own formatting has nothing to say
            // about line breaks, so rather than run off the edge it is fitted to the box.
            var frames = Frames(string.Join(" ", Enumerable.Repeat("WORD", 60)));

            Assert.True(frames.Count > 1);
            foreach (var f in frames)
            {
                Assert.True(f.Lines.Count <= FieldMessageWindow.LinesPerPage);
                foreach (string line in f.Lines)
                    Assert.True(Measure(line) <= FieldMessageWindow.TextWidth);
            }
            Assert.Equal(MessageWait.None, frames.Last().Wait);
        }

        [Fact]
        public void NothingToSayMakesNoFrames()
        {
            Assert.Empty(Frames(""));
            Assert.Empty(Frames(null));
        }
    }
}

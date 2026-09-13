using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Printing a message a letter at a time with RenderText's timing: four print ticks a letter at mid
    /// speed, a line break one tick cheaper, and a wait at every page break.
    /// </summary>
    public class FieldTextPrinterTests
    {
        private static List<FieldMessageFrame> Pages(string text) =>
            FieldMessageScript.Frames(text, t => (t ?? "").Length * 6);

        private static int TicksToFinish(FieldTextPrinter p, int limit = 10000)
        {
            int ticks = 0;
            while (!p.Finished && ticks < limit) { p.Tick(false, false); ticks++; }
            return ticks;
        }

        [Theory]
        [InlineData(FieldTextSpeed.Slow, 8)]
        [InlineData(FieldTextSpeed.Mid, 4)]
        [InlineData(FieldTextSpeed.Fast, 1)]
        public void EachLetterTakesTheSpeedsDelayAndTheEndIsReadOneDelayLater(FieldTextSpeed speed, int delay)
        {
            var p = new FieldTextPrinter(Pages("ABCD"), speed);
            // Four letters a delay apart, then the end of the string a delay after the last.
            Assert.Equal(4 * delay + 1, TicksToFinish(p));
            Assert.Equal("ABCD", p.Text);
        }

        [Fact]
        public void ALineBreakCostsOneTickLessThanALetter()
        {
            var plain = new FieldTextPrinter(Pages("ABC"), FieldTextSpeed.Mid);
            var broken = new FieldTextPrinter(Pages("A\\nBC"), FieldTextSpeed.Mid);
            Assert.Equal(TicksToFinish(plain) + 3, TicksToFinish(broken));
            Assert.Equal(new[] { "A", "BC" }, broken.Lines);
        }

        [Fact]
        public void AClearWaitsForAPressThenStartsAnEmptyBox()
        {
            int turned = 0;
            var p = new FieldTextPrinter(Pages("one\\rtwo"), FieldTextSpeed.Fast) { PageTurned = () => turned++ };

            for (int i = 0; i < 20; i++) p.Tick(false, false);
            Assert.True(p.WaitingForPress);
            Assert.Equal("one", p.Text);
            Assert.False(p.Finished);

            p.Tick(true, true);
            Assert.Equal(1, turned);
            Assert.False(p.WaitingForPress);

            TicksToFinish(p);
            Assert.Equal("two", p.Text);
        }

        [Fact]
        public void AScrollSlidesTheLinesUpOverFourTicksAndKeepsTheLastOne()
        {
            var p = new FieldTextPrinter(Pages("one\\ntwo\\fthree"), FieldTextSpeed.Fast);
            for (int i = 0; i < 30 && !p.WaitingForPress; i++) p.Tick(false, false);
            Assert.True(p.WaitingForPress);

            p.Tick(true, true);
            var slides = new List<int>();
            for (int i = 0; i < FieldTextPrinter.ScrollTicks; i++) { slides.Add(p.ScrollPixels); p.Tick(false, false); }
            Assert.Equal(new[] { 4, 8, 12, 16 }, slides);

            TicksToFinish(p);
            Assert.Equal(new[] { "two", "three" }, p.Lines);
        }

        [Fact]
        public void AWaitAtTheVeryEndStillNeedsAPress()
        {
            var p = new FieldTextPrinter(Pages("bye\\r"), FieldTextSpeed.Fast);
            for (int i = 0; i < 50; i++) p.Tick(false, false);
            Assert.True(p.WaitingForPress);
            Assert.False(p.Finished);
            p.Tick(true, true);
            TicksToFinish(p);
            Assert.True(p.Finished);
        }

        [Fact]
        public void APressHurriesItAndHoldingKeepsItHurrying()
        {
            var slow = new FieldTextPrinter(Pages("ABCDEFGHIJ"), FieldTextSpeed.Slow);
            slow.Tick(false, false);                  // A
            slow.Tick(true, true);                     // the press skips the wait: B
            Assert.Equal("AB", slow.Text);
            slow.Tick(false, true);
            slow.Tick(false, true);
            Assert.Equal("ABCD", slow.Text);
            slow.Tick(false, false);                   // let go, and it is back to slow
            Assert.Equal("ABCD", slow.Text);
        }

        [Fact]
        public void TextThatCannotBeHurriedIgnoresThePress()
        {
            var p = new FieldTextPrinter(Pages("ABCDEFGHIJ"), FieldTextSpeed.Slow, skippable: false);
            p.Tick(false, false);
            p.Tick(true, true);
            p.Tick(false, true);
            Assert.Equal("A", p.Text);
        }

        [Fact]
        public void AnInstantMessageShowsOnlyWhatComesBeforeItsFirstWait()
        {
            var p = new FieldTextPrinter(Pages("first\\rsecond"), FieldTextSpeed.Mid, instant: true);
            Assert.True(p.Finished);
            Assert.Equal("first", p.Text);
        }

        [Fact]
        public void TheArrowBobsZeroOneTwoOneEveryNineTicks()
        {
            var p = new FieldTextPrinter(Pages("x\\ry"), FieldTextSpeed.Fast);
            while (!p.WaitingForPress) p.Tick(false, false);
            var poses = new List<int>();
            for (int i = 0; i < 36; i++) { if (i % 9 == 0) poses.Add(p.ArrowOffset); p.Tick(false, false); }
            Assert.Equal(new[] { 0, 1, 2, 1 }, poses);
        }
    }
}

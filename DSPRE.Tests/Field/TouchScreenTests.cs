using System;
using System.IO;
using System.Linq;
using DSPRE.Avalonia.Data;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>The bottom screens the field shows: Platinum's Pokétch and HeartGold's touch menu and Poké Ball screen.</summary>
    [Collection("rom")]
    public class TouchScreenTests
    {
        // ── where a touch lands, from the overlay 27 tables ──────────────────────────

        [Theory]
        [InlineData(128, 70, 0)]
        [InlineData(10, 60, 0)]
        [InlineData(128, 120, 1)]
        [InlineData(128, 45, -1)]      // above YES
        [InlineData(1, 70, -1)]        // off its left edge
        public void TheTouchYesNoAnswersToItsTwoBars(int x, int y, int expected)
            => Assert.Equal(expected, HgssTouchScreen.HitChoice(2, true, x, y));

        [Theory]
        [InlineData(60, 48, 0)]
        [InlineData(200, 48, 1)]
        [InlineData(200, 144, 4)]
        [InlineData(60, 144, -1)]      // five entries leave the bottom left empty
        public void AFiveEntryListSplitsIntoTwoColumns(int x, int y, int expected)
            => Assert.Equal(expected, HgssTouchScreen.HitChoice(5, false, x, y));

        [Fact]
        public void TheDPadStaysInAColumnGoingUpAndDownAndCrossesOverSideways()
        {
            Assert.Equal(2, HgssTouchScreen.Neighbour(5, false, 0, 0, 1));
            Assert.Equal(1, HgssTouchScreen.Neighbour(5, false, 0, 1, 0));
            Assert.Equal(3, HgssTouchScreen.Neighbour(5, false, 4, 0, -1));
            Assert.Equal(-1, HgssTouchScreen.Neighbour(5, false, 4, -1, 0));
            Assert.Equal(1, HgssTouchScreen.Neighbour(2, true, 0, 0, 1));
            Assert.Equal(-1, HgssTouchScreen.Neighbour(2, true, 1, 0, 1));    // the yes/no never wraps
        }

        [Fact]
        public void TheConfirmingBlinkIsOffOnOffOn()
        {
            var shown = Enumerable.Range(0, HgssTouchScreen.BlinkFrames).Select(HgssTouchScreen.BlinkShows).ToArray();
            Assert.Equal(new[] { false, false, false, false, true, true, true, true, false, false, false, false, true, true }, shown);
            Assert.True(HgssTouchScreen.BlinkShows(-1));
        }

        [Theory]
        [InlineData(100, 100, PoketchScreen.Spot.Screen)]
        [InlineData(240, 50, PoketchScreen.Spot.Up)]
        [InlineData(240, 120, PoketchScreen.Spot.Down)]
        [InlineData(240, 170, PoketchScreen.Spot.None)]
        [InlineData(5, 5, PoketchScreen.Spot.None)]
        public void ThePoketchAnswersOnItsFaceAndItsTwoButtons(int x, int y, PoketchScreen.Spot expected)
            => Assert.Equal(expected, PoketchScreen.HitTest(x, y));

        // ── drawn from the ROM ───────────────────────────────────────────────────────

        private static bool Differs(byte[] a, byte[] b, int x0, int y0, int x1, int y1)
        {
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    int p = (y * DsBgScreen.Width + x) * 4;
                    if (a[p] != b[p] || a[p + 1] != b[p + 1] || a[p + 2] != b[p + 2]) return true;
                }
            return false;
        }

        [SkippableFact]
        public void ThePoketchIsDrawnWithItsButtonsFaceAndClock()
        {
            Skip.If(!Directory.Exists(TestRoms.Platinum), "Platinum not unpacked here");
            new RomInfo("CPUE", TestRoms.Platinum);
            var screen = PoketchScreen.Load();
            Assert.NotNull(screen);

            byte[] idle = screen.RenderWatch(false, 0, false, 12, 34, PoketchScreen.Look.Free, PoketchScreen.Look.Free);
            Assert.Equal(DsBgScreen.Width * DsBgScreen.Height * 4, idle.Length);

            byte[] locked = screen.RenderWatch(false, 0, false, 12, 34, PoketchScreen.Look.Lock, PoketchScreen.Look.Free);
            byte[] held = screen.RenderWatch(false, 0, false, 12, 34, PoketchScreen.Look.Hold, PoketchScreen.Look.Free);
            Assert.True(Differs(idle, locked, 224, 32, 256, 96), "a locked press should change the up button");
            Assert.True(Differs(locked, held, 224, 32, 256, 96), "holding looks different from a locked press");
            Assert.False(Differs(idle, locked, 224, 96, 256, 160), "the down button was not touched");

            // Eight and nine are kept apart from the other digits in the file.
            byte[] eight = screen.RenderWatch(false, 0, false, 18, 34, PoketchScreen.Look.Free, PoketchScreen.Look.Free);
            byte[] nine = screen.RenderWatch(false, 0, false, 19, 34, PoketchScreen.Look.Free, PoketchScreen.Look.Free);
            Assert.True(Differs(idle, eight, 64, 56, 96, 128));
            Assert.True(Differs(eight, nine, 64, 56, 96, 128));

            byte[] lit = screen.RenderWatch(false, 0, true, 12, 34, PoketchScreen.Look.Free, PoketchScreen.Look.Free);
            Assert.True(Differs(idle, lit, 40, 40, 180, 50), "the backlight should change the face");
            byte[] girl = screen.RenderWatch(true, 0, false, 12, 34, PoketchScreen.Look.Free, PoketchScreen.Look.Free);
            Assert.True(Differs(idle, girl, 0, 0, 256, 192), "a girl's Pokétch has a different casing");

            byte[] before = screen.RenderUnavailable();
            Assert.True(Differs(idle, before, 0, 0, 256, 192));
        }

        [SkippableFact]
        public void TheTouchMenuDimsForScriptsButLeavesTheAButton()
        {
            Skip.If(!Directory.Exists(TestRoms.HeartGold), "HeartGold not unpacked here");
            new RomInfo("IPKE", TestRoms.HeartGold);
            var screen = HgssTouchScreen.Load();
            Assert.NotNull(screen);

            byte[] idle = screen.RenderMenu(null, null, false, false, false, -1, HgssTouchScreen.TalkMessage);
            byte[] dimmed = screen.RenderMenu(null, null, true, false, false, -1, HgssTouchScreen.TalkMessage);
            byte[] pressed = screen.RenderMenu(null, null, false, true, false, -1, HgssTouchScreen.TalkMessage);
            byte[] lit = screen.RenderMenu(null, null, false, false, false, 0, HgssTouchScreen.TalkMessage);

            Assert.True(Differs(idle, dimmed, 16, 10, 80, 60), "the icons should dim");
            Assert.False(Differs(idle, dimmed, 170, 150, 256, 190), "the A button never dims");
            Assert.True(Differs(idle, pressed, 168, 144, 256, 192), "a held A button looks pressed");
            Assert.True(Differs(idle, lit, 16, 10, 80, 60), "a touched icon lights up");
        }

        [SkippableFact]
        public void ThePokeBallScreenShowsItsBarsAndTheRedFrame()
        {
            Skip.If(!Directory.Exists(TestRoms.HeartGold), "HeartGold not unpacked here");
            new RomInfo("IPKE", TestRoms.HeartGold);
            var screen = HgssTouchScreen.Load();
            Assert.NotNull(screen);

            byte[] empty = screen.RenderChoices(null, Array.Empty<string>(), true, 0, true);
            byte[] yesNo = screen.RenderChoices(null, new[] { "YES", "NO" }, true, 0, false);
            byte[] framed = screen.RenderChoices(null, new[] { "YES", "NO" }, true, 0, true);
            byte[] onNo = screen.RenderChoices(null, new[] { "YES", "NO" }, true, 1, true);
            Assert.True(Differs(empty, yesNo, 0, 48, 256, 144), "the bars should be drawn");
            Assert.True(Differs(yesNo, framed, 0, 48, 256, 96), "the red frame goes round YES");
            Assert.True(Differs(framed, onNo, 0, 96, 256, 144), "and round NO when the cursor is there");

            for (int n = 2; n <= 8; n++)
            {
                byte[] list = screen.RenderChoices(null, Enumerable.Range(0, n).Select(i => $"E{i}").ToArray(), false, 0, true);
                Assert.True(Differs(empty, list, 0, 0, 256, 192), $"a {n} entry list should draw its bars");
            }
        }
    }
}

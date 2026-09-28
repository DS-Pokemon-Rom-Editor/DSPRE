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

        // The game's rectangles take in their top and left edge but not their bottom and right one.
        [Theory]
        [InlineData(3, 50, 0)]
        [InlineData(250, 91, 0)]
        [InlineData(251, 60, -1)]      // right edge of the table's 3..251
        [InlineData(128, 92, -1)]      // bottom edge of 50..92, and above NO's 99
        [InlineData(128, 99, 1)]
        [InlineData(128, 140, -1)]
        public void TheYesNoRectanglesAreHalfOpen(int x, int y, int expected)
            => Assert.Equal(expected, HgssTouchScreen.HitChoice(2, true, x, y));

        [Theory]
        [InlineData(3, 27, 3, 0)]
        [InlineData(128, 164, 3, -1)]
        [InlineData(122, 3, 8, 0)]
        [InlineData(123, 3, 8, -1)]    // the gap between the columns
        [InlineData(131, 187, 8, 7)]
        [InlineData(252, 187, 8, -1)]
        public void ListRectanglesComeFromTheGamesTables(int x, int y, int count, int expected)
            => Assert.Equal(expected, HgssTouchScreen.HitChoice(count, false, x, y));

        [Theory]
        [InlineData(8, 0, HgssTouchScreen.MenuSpot.Strip, 0)]
        [InlineData(160, 5, HgssTouchScreen.MenuSpot.None, 0)]
        [InlineData(20, 16, HgssTouchScreen.MenuSpot.None, 0)]
        [InlineData(16, 22, HgssTouchScreen.MenuSpot.Icon, 0)]
        [InlineData(76, 30, HgssTouchScreen.MenuSpot.None, 0)]
        [InlineData(100, 133, HgssTouchScreen.MenuSpot.Icon, 6)]
        [InlineData(100, 134, HgssTouchScreen.MenuSpot.None, 0)]
        [InlineData(203, 8, HgssTouchScreen.MenuSpot.Item, 0)]
        [InlineData(255, 20, HgssTouchScreen.MenuSpot.None, 0)]
        [InlineData(210, 76, HgssTouchScreen.MenuSpot.Item, 1)]
        [InlineData(210, 77, HgssTouchScreen.MenuSpot.None, 0)]
        [InlineData(251, 133, HgssTouchScreen.MenuSpot.Shoes, 0)]
        [InlineData(252, 100, HgssTouchScreen.MenuSpot.None, 0)]
        [InlineData(168, 144, HgssTouchScreen.MenuSpot.AButton, 0)]
        [InlineData(200, 188, HgssTouchScreen.MenuSpot.None, 0)]
        [InlineData(255, 150, HgssTouchScreen.MenuSpot.None, 0)]
        public void TheMenuRectanglesAreHalfOpen(int x, int y, HgssTouchScreen.MenuSpot spot, int index)
        {
            var (s, i) = HgssTouchScreen.HitMenu(x, y);
            Assert.Equal(spot, s);
            Assert.Equal(index, i);
        }

        [Fact]
        public void TheAButtonOffersFishing()
        {
            Assert.Contains(HgssTouchScreen.FishingMessage, HgssTouchScreen.ALabelMessages);
            Assert.Equal(22, HgssTouchScreen.FishingMessage);
        }

        [Fact]
        public void TheBugContestMovesTheIconsAlong()
        {
            Assert.Equal(0, HgssTouchScreen.SlotOf(0, false));
            Assert.Equal(1, HgssTouchScreen.SlotOf(0, true));      // RETIRE takes the first slot
            Assert.Equal(0, HgssTouchScreen.SlotOf(7, true));
            Assert.Equal(-1, HgssTouchScreen.SlotOf(5, true));     // no SAVE during the contest
            Assert.Equal(-1, HgssTouchScreen.SlotOf(7, false));
        }

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
            Assert.False(Differs(idle, dimmed, 54, 0, 70, 16), "the X mark never dims");

            // With the menu open, the icon under its cursor stays solid while the rest dim.
            byte[] open = screen.RenderMenu(null, null, null, new HgssTouchScreen.MenuLook
                { Busy = true, Cursor = 0, RegisteredItems = true });
            byte[] still = screen.RenderMenu(null, null, null, new HgssTouchScreen.MenuLook { RegisteredItems = true });
            Assert.False(Differs(still, open, 24, 22, 64, 54), "the icon under the cursor stays solid");
            Assert.True(Differs(still, open, 24, 62, 64, 94), "the other icons dim");

            // The item frames only show with something registered to them.
            byte[] none = screen.RenderMenu(null, null, null, new HgssTouchScreen.MenuLook { RegisteredItems = false });
            Assert.True(Differs(still, none, 200, 8, 256, 40), "the first item frame comes and goes");
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

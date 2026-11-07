using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The HGSS summary screen's touch areas in arm9. There is no grid cursor on them; the art and the move and ribbon cursors are
    /// placed in code, so only the party icons and the ribbons move with their areas.
    /// </summary>
    public sealed class SummaryTouchButtons : IScreenButtons
    {
        /// <param name="Ended">Whether the list ends with the 0xFF entry; the move screen's three buttons are single rects.</param>
        private sealed record Page(string Name, string[] Buttons, bool Ended = true);

        // Cancel, Change and the 5th move, the box arrows, the move list, the ribbons, then the page tabs with the party icons.
        private static readonly Page[] Pages =
        {
            new Page("Tabs", new[] { "Info", "Stats and moves", "Ribbons", "Back", "Pokémon 1", "Pokémon 2", "Pokémon 3", "Pokémon 4", "Pokémon 5", "Pokémon 6" }),
            new Page("Moves", new[] { "Move 1", "Move 2", "Move 3", "Move 4" }),
            new Page("Move buttons", new[] { "Cancel", "Change", "5th move" }, Ended: false),
            new Page("Box arrows", new[] { "Previous", "Next" }),
            new Page("Ribbons", Enumerable.Range(1, 9).Select(i => $"Ribbon {i}").Concat(new[] { "Up", "Down", "Close" }).ToArray()),
        };

        private const int Tabs = 0, FirstIcon = 4, Icons = 6, Ribbons = 4, RibbonSprites = 9, FirstRibbonTemplate = 54, FirstIconTemplate = 73;

        private readonly ScreenTables _tables;
        private readonly int[] _starts;
        private readonly int[] _icons;
        private readonly int[] _ribbons;
        private readonly int _frame = -1;
        // sub_0208C380 puts the ribbon cursor at 32 per column and a row step down from the first ribbon's spot.
        private readonly int _cursor = -1;
        private readonly byte[] _data;

        private SummaryTouchButtons(int anchor, int frame, int cursor, ScreenTables tables)
        {
            _tables = tables;
            _starts = Pages.Select((p, i) => tables.Take(ScreenTables.InArm9, anchor + RomLocator.SummaryTouchLists[i], p.Buttons.Length * TouchRects.Size)).ToArray();
            _icons = Enumerable.Range(0, Icons)
                .Select(i => tables.Take(ScreenTables.InArm9, anchor + RomLocator.SummaryIcons + i * RomLocator.SummaryIconStride, 4)).ToArray();
            // The 3 by 3 ribbon sprites' templates (54 to 62) sit in the same table as the party icons' (73 to 78).
            _ribbons = Enumerable.Range(0, RibbonSprites).Select(i => tables.Take(ScreenTables.InArm9,
                anchor + RomLocator.SummaryIcons + (FirstRibbonTemplate + i - FirstIconTemplate) * RomLocator.SummaryIconStride, 4)).ToArray();
            if (frame > 0) _frame = tables.Take(ScreenTables.InArm9, frame, 2 * Icons);
            if (cursor > 0) _cursor = tables.Take(ScreenTables.InArm9, cursor, CursorCode.Length * 2);
            _data = tables.Finish();
            if (_cursor >= 0 && !Enumerable.Range(0, CursorCode.Length).All(i => (BitConverter.ToUInt16(_data, _cursor + 2 * i) & 0xFF00) == CursorCode[i])) _cursor = -1;
        }

        /// <summary>Null when this ROM's tables aren't known or don't look like the game's.</summary>
        public static SummaryTouchButtons Read()
        {
            RomInfo.SummaryTouchSites sites = RomLocator.SummaryTouchSites;
            if (sites == null) return null;
            ScreenTables tables = new ScreenTables();
            byte[] arm9 = tables.FileOf(ScreenTables.InArm9);
            if (arm9 == null) return null;
            int a = (int)sites.Cancel;
            for (int i = 0; i < Pages.Length; i++)
            {
                int list = a + RomLocator.SummaryTouchLists[i], end = list + Pages[i].Buttons.Length * TouchRects.Size;
                // Each ended list stops where the game's does; the move buttons must at least read as areas on the screen.
                if (Pages[i].Ended ? end < 0 || end >= arm9.Length || arm9[end] != 0xFF
                                   : Enumerable.Range(0, Pages[i].Buttons.Length).Any(b => !TouchRects.OnScreen(arm9, list + b * TouchRects.Size)))
                    return null;
            }
            SummaryTouchButtons buttons = new SummaryTouchButtons(a, (int)sites.Frame, (int)sites.RibbonCursor, tables);
            return tables.Missing ? null : buttons;
        }

        public IReadOnlyList<string> PageNames => Pages.Select(p => p.Name).ToList();
        public string[] ButtonsOf(int page) => Pages[page].Buttons;
        public int StopsOf(int page) => 0;

        private int TouchAt(int page, int button) => _starts[page] + button * TouchRects.Size;

        public (int Top, int Bottom, int Left, int Right) AreaOf(int page, int button) => TouchRects.Read(_data, TouchAt(page, button));

        /// <summary>The party icons' centres in pixels, Pokémon 1 to 6.</summary>
        public IReadOnlyList<(int X, int Y)> IconSpots =>
            _icons.Select(at => ((int)BitConverter.ToInt16(_data, at), (int)BitConverter.ToInt16(_data, at + 2))).ToList();

        /// <summary>The ribbon sprites' centres in pixels, Ribbon 1 to 9.</summary>
        public IReadOnlyList<(int X, int Y)> RibbonSpots =>
            _ribbons.Select(at => ((int)BitConverter.ToInt16(_data, at), (int)BitConverter.ToInt16(_data, at + 2))).ToList();

        /// <summary>Where the selection frame sits on each Pokémon, Pokémon 1 to 6, or null when the table isn't known.</summary>
        public IReadOnlyList<(int X, int Y)> FrameSpots =>
            _frame < 0 ? null : Enumerable.Range(0, Icons).Select(i => ((int)_data[_frame + 2 * i], (int)_data[_frame + 2 * i + 1])).ToList();

        public void Move(int page, int button, int dx, int dy)
        {
            (dx, dy) = TouchRects.Move(_data, TouchAt(page, button), dx, dy);
            if (page == Ribbons && button < RibbonSprites)
            {
                ScreenSpots.Shift(_data, _ribbons[button], 2, dx, 255);
                ScreenSpots.Shift(_data, _ribbons[button] + 2, 2, dy, 191);
                FollowRibbonCursor();
                return;
            }
            if (page != Tabs || button < FirstIcon) return;
            int at = _icons[button - FirstIcon];
            ScreenSpots.Shift(_data, at, 2, dx, 255);
            ScreenSpots.Shift(_data, at + 2, 2, dy, 191);
            // The selection frame keeps its own spot per Pokémon, so it goes with the icon.
            if (_frame < 0) return;
            ScreenSpots.Shift(_data, _frame + 2 * (button - FirstIcon), 1, dx, 255);
            ScreenSpots.Shift(_data, _frame + 2 * (button - FirstIcon) + 1, 1, dy, 191);
        }

        // movs r3, #step / movs r0 / lsls r1, r4, #5 / muls / lsls r0 / adds r1, #x / adds r3, #y: the high bytes stay.
        private static readonly int[] CursorCode = { 0x2300, 0x2000, 0x0100, 0x4300, 0x0200, 0x3100, 0x3300 };

        // The cursor sits on Ribbon 1's spot and steps down by Ribbon 4's distance below it. Its 32-pixel column step is a shift.
        private void FollowRibbonCursor()
        {
            if (_cursor < 0) return;
            IReadOnlyList<(int X, int Y)> spots = RibbonSpots;
            int step = spots[3].Y - spots[0].Y;
            if (spots[0].X is >= 0 and <= 255) _data[_cursor + 10] = (byte)spots[0].X;
            if (spots[0].Y is >= 0 and <= 255) _data[_cursor + 12] = (byte)spots[0].Y;
            if (step is >= 0 and <= 255) _data[_cursor] = (byte)step;
        }

        public void Resize(int page, int button, int width, int height) => TouchRects.Resize(_data, TouchAt(page, button), width, height);

        public (double X, double Y) CursorCentre(int page, int stop) => (0, 0);
        public int Target(int page, int stop, CursorDirection d) => stop;
        public void SetTarget(int page, int stop, CursorDirection d, int target) { }
        public IReadOnlyList<(int Value, string Name)> Choices(int page) => Array.Empty<(int, string)>();
        public int ValueOf(int page, int stop) => stop;
        public int StopOf(int page, int value) => value;
        public string Fixed(int page, int stop, CursorDirection d) => null;

        public byte[] ToBytes() => _tables.Copy();
        public void FromBytes(byte[] bytes) => _tables.Load(bytes);
        public byte[] FileBytes() => _tables.FileBytes();

        public string WhyNot() => null;

        public string WhyNotWrite(byte[] bytes) => bytes == null || bytes.Length != _data.Length ? "The summary tables don't match this ROM." : null;

        public void WriteFile(byte[] bytes)
        {
            if (WhyNotWrite(bytes) is string why) throw new InvalidOperationException(why);
            _tables.Write(bytes);
        }
    }
}

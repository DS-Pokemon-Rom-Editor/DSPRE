using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The HGSS summary screen's touch areas in arm9. There is no grid cursor on them; the art and the move and ribbon cursors are
    /// placed in code, so only the party icons move with their areas.
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

        private const int Tabs = 0, FirstIcon = 4, Icons = 6;

        private readonly ScreenTables _tables;
        private readonly int[] _starts;
        private readonly int[] _icons;
        private readonly byte[] _data;

        private SummaryTouchButtons(int anchor, ScreenTables tables)
        {
            _tables = tables;
            _starts = Pages.Select((p, i) => tables.Take(ScreenTables.InArm9, anchor + RomLocator.SummaryTouchLists[i], p.Buttons.Length * TouchRects.Size)).ToArray();
            _icons = Enumerable.Range(0, Icons)
                .Select(i => tables.Take(ScreenTables.InArm9, anchor + RomLocator.SummaryIcons + i * RomLocator.SummaryIconStride, 4)).ToArray();
            _data = tables.Finish();
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
            SummaryTouchButtons buttons = new SummaryTouchButtons(a, tables);
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

        public void Move(int page, int button, int dx, int dy)
        {
            (dx, dy) = TouchRects.Move(_data, TouchAt(page, button), dx, dy);
            if (page != Tabs || button < FirstIcon) return;
            int at = _icons[button - FirstIcon];
            ScreenSpots.Shift(_data, at, 2, dx, 255);
            ScreenSpots.Shift(_data, at + 2, 2, dy, 191);
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

using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The HGSS field touch menu (overlay 27), with the yes/no menu and the 2 to 8 option lists that scripts show there. Each menu arrow
    /// tries three slots in order and takes the first one shown.
    /// </summary>
    public sealed class FieldTouchMenuButtons : IScreenButtons, IScreenButtonRows
    {
        private const int Slots = 7, Tries = 3, Tile = 8;
        private const int SpriteCount = 15, NameCount = 8;
        // The list pointer tables follow each other, one u32 per list; each cursor row has room for eight spots of x, y and look.
        private const int ListCursorRow = 24, Lists = 7, FirstList = 2, ListPage = 3, ListTable = Lists * 4;
        private const int Window = 8, Spot = 3, NoMove = 0xFF;

        private static readonly string[] MenuButtons =
            { "Pokédex", "Pokémon", "Bag", "Pokégear", "Trainer card", "Save", "Options", "MENU", "Item 1", "Item 2" };
        // Maps each menu button to its touch list entry; the list starts with the MENU strip.
        private static readonly int[] MenuRect = { 1, 2, 3, 4, 5, 6, 7, 0, 8, 9 };
        // Each button's sprites in the fifteen-spot table: icons, item icons and their bases, the shoes and their mark, the A button, the X mark.
        private static readonly int[][] MenuSprites = { new[] { 0 }, new[] { 1 }, new[] { 2 }, new[] { 3 }, new[] { 4 }, new[] { 5 }, new[] { 6 },
                                                        new[] { 14 }, new[] { 7, 9 }, new[] { 8, 10 } };
        private static readonly string[] ExtraButtons = { "Running shoes", "A button" };
        private static readonly int[][] ExtraSprites = { new[] { 11, 13 }, new[] { 12 } };
        private static readonly string[] YesNoButtons = { "Yes", "No" };

        private enum Run { Menu, Extra, Sprites, Names, Flow, YesNo, YesNoCursor }
        private enum ListPart { Keys, Rects, Windows, Cursors }

        private readonly ScreenTables _tables;
        private readonly int[] _starts;
        private readonly byte[] _data;

        private FieldTouchMenuButtons(int overlay, IEnumerable<(int At, int Length)> runs, ScreenTables tables)
        {
            _tables = tables;
            _starts = runs.Select(r => tables.Take(overlay, r.At, r.Length)).ToArray();
            _data = tables.Finish();
        }

        private static int ListRun(int count, ListPart part) => (int)Run.YesNoCursor + 1 + (count - FirstList) * 4 + (int)part;

        private int Start(Run run) => _starts[(int)run];
        private int Start(int count, ListPart part) => _starts[ListRun(count, part)];

        /// <summary>Null when this ROM's tables aren't known or don't look like the game's.</summary>
        public static FieldTouchMenuButtons Read()
        {
            RomInfo.TouchMenuSites sites = RomLocator.TouchMenuSites;
            if (sites == null) return null;
            ScreenTables tables = new ScreenTables();
            byte[] file = tables.FileOf(sites.Overlay);
            if (file == null) return null;
            bool In(long at, int length) => at >= 0 && at + length <= file.Length;
            RomLocator.TouchMenuTables t = RomLocator.TouchMenuDistances;
            int a = (int)sites.MenuRects;
            if (!In(a, 10 * TouchRects.Size + 1) || !In(a + t.ExtraRects, 2 * TouchRects.Size + 1) || !In(sites.YesNo, 2 * TouchRects.Size + 1)
                || !In(a + t.Flow, Slots * 4 * Tries) || !In(sites.ListCursors, Lists * ListCursorRow) || !In(sites.ListKeys, 3 * ListTable)) return null;
            // Every touch list ends where the game's does, and every arrow names a slot or option.
            if (file[a + 10 * TouchRects.Size] != 0xFF || file[a + t.ExtraRects + 2 * TouchRects.Size] != 0xFF || file[sites.YesNo + 2 * TouchRects.Size] != 0xFF)
                return null;
            if (file.Skip(a + t.Flow).Take(Slots * 4 * Tries).Any(v => v >= Slots)) return null;

            List<(int, int)> runs = new List<(int, int)>
            {
                (a, 10 * TouchRects.Size), (a + t.ExtraRects, 2 * TouchRects.Size), (a + t.Sprites, SpriteCount * 4), (a + t.Names, NameCount * 8),
                (a + t.Flow, Slots * 4 * Tries), ((int)sites.YesNo, 2 * TouchRects.Size), ((int)sites.YesNoCursor, 2 * 4),
            };
            long ram = OverlayUtils.OverlayTable.GetRAMAddress(sites.Overlay);
            for (int list = 0; list < Lists; list++)
            {
                int count = list + FirstList;
                int Pointer(int table)
                {
                    long at = BitConverter.ToUInt32(file, (int)sites.ListKeys + table * ListTable + list * 4) - ram;
                    return In(at, 1) ? (int)at : -1;
                }
                int keys = Pointer(0), rects = Pointer(1), windows = Pointer(2);
                if (keys < 0 || rects < 0 || windows < 0 || !In(keys, count * 4) || !In(rects, count * TouchRects.Size + 1) ||
                    !In(windows, count * Window) || file[rects + count * TouchRects.Size] != 0xFF ||
                    file.Skip(keys).Take(count * 4).Any(v => v != NoMove && v >= count))
                    return null;
                runs.Add((keys, count * 4));
                runs.Add((rects, count * TouchRects.Size));
                runs.Add((windows, count * Window));
                runs.Add(((int)sites.ListCursors + list * ListCursorRow, count * Spot));
            }
            FieldTouchMenuButtons buttons = new FieldTouchMenuButtons(sites.Overlay, runs, tables);
            return tables.Missing ? null : buttons;
        }

        private static int ListCount(int page) => page - ListPage + FirstList;

        public IReadOnlyList<string> PageNames =>
            new[] { "Menu", "Side buttons", "Yes or no" }.Concat(Enumerable.Range(FirstList, Lists).Select(n => $"List with {n} options")).ToList();

        public string[] ButtonsOf(int page) => page switch
        {
            0 => MenuButtons,
            1 => ExtraButtons,
            2 => YesNoButtons,
            _ => Enumerable.Range(1, ListCount(page)).Select(i => $"Option {i}").ToArray(),
        };

        // The yes/no menu's arrows are in code: Up picks Yes and Down picks No.
        public int StopsOf(int page) => page switch { 0 => Slots, 1 or 2 => 0, _ => ListCount(page) };

        private int RectAt(int page, int button) => page switch
        {
            0 => Start(Run.Menu) + MenuRect[button] * TouchRects.Size,
            1 => Start(Run.Extra) + button * TouchRects.Size,
            2 => Start(Run.YesNo) + button * TouchRects.Size,
            _ => Start(ListCount(page), ListPart.Rects) + button * TouchRects.Size,
        };

        public (int Top, int Bottom, int Left, int Right) AreaOf(int page, int button) => TouchRects.Read(_data, RectAt(page, button));

        /// <summary>The fifteen sprite spots, in the game's order.</summary>
        public IReadOnlyList<(int X, int Y)> SpriteSpots =>
            Enumerable.Range(0, SpriteCount).Select(i => ((int)BitConverter.ToUInt16(_data, Start(Run.Sprites) + i * 4),
                                                          (int)BitConverter.ToUInt16(_data, Start(Run.Sprites) + i * 4 + 2))).ToList();

        /// <summary>The icon name windows, in tiles.</summary>
        public IReadOnlyList<(int X, int Y)> NameSpots =>
            Enumerable.Range(0, NameCount).Select(i => ((int)BitConverter.ToUInt32(_data, Start(Run.Names) + i * 8),
                                                        (int)BitConverter.ToUInt32(_data, Start(Run.Names) + i * 8 + 4))).ToList();

        /// <summary>A list's option text windows in tiles, or null for the other pages.</summary>
        public IReadOnlyList<(int X, int Y, int Width, int Height)> OptionWindows(int page)
        {
            if (page < ListPage) return null;
            int count = ListCount(page), at = Start(count, ListPart.Windows);
            return Enumerable.Range(0, count).Select(i => ((int)_data[at + i * Window + 1], (int)_data[at + i * Window + 2],
                                                           (int)_data[at + i * Window + 3], (int)_data[at + i * Window + 4])).ToList();
        }

        public int MoveStep(int page, int button) => page >= ListPage || (page == 0 && button < Slots) ? Tile : 1;

        // The icon, its name and any bases move with the area; names move in whole tiles. On the yes/no menu and the lists the
        // cursor spot and the option's text window move too.
        public void Move(int page, int button, int dx, int dy)
        {
            int step = MoveStep(page, button);
            (dx, dy) = TouchRects.Move(_data, RectAt(page, button), (int)Math.Round(dx / (double)step) * step, (int)Math.Round(dy / (double)step) * step);
            if (page == 2)
            {
                int at = Start(Run.YesNoCursor) + button * 4;
                ScreenSpots.Shift(_data, at, 2, dx, 255);
                ScreenSpots.Shift(_data, at + 2, 2, dy, 191);
                return;
            }
            if (page >= ListPage)
            {
                int count = ListCount(page), spot = Start(count, ListPart.Cursors) + button * Spot;
                ScreenSpots.Shift(_data, spot, 1, dx, 255);
                ScreenSpots.Shift(_data, spot + 1, 1, dy, 191);
                int window = Start(count, ListPart.Windows) + button * Window;
                ScreenSpots.Shift(_data, window + 1, 1, (int)Math.Round(dx / (double)Tile), 32 - _data[window + 3]);
                ScreenSpots.Shift(_data, window + 2, 1, (int)Math.Round(dy / (double)Tile), 24 - _data[window + 4]);
                return;
            }
            foreach (int sprite in page == 0 ? MenuSprites[button] : ExtraSprites[button])
            {
                int at = Start(Run.Sprites) + sprite * 4;
                ScreenSpots.Shift(_data, at, 2, dx, 255);
                ScreenSpots.Shift(_data, at + 2, 2, dy, 191);
            }
            if (page != 0 || button >= Slots) return;
            (int nx, int ny) = NameSpots[button];
            (int x, int y) = (Math.Clamp(nx + (int)Math.Round(dx / (double)Tile), 0, 23), Math.Clamp(ny + (int)Math.Round(dy / (double)Tile), 0, 22));
            // Windows are cleared when drawn, so a name already on the new spot swaps to the old one.
            int other = NameSpots.ToList().FindIndex(n => n == (x, y));
            if (other >= 0 && other != button) SetName(other, nx, ny);
            SetName(button, x, y);
        }

        private void SetName(int window, int x, int y)
        {
            int at = Start(Run.Names) + window * 8;
            BitConverter.TryWriteBytes(_data.AsSpan(at), (uint)x);
            BitConverter.TryWriteBytes(_data.AsSpan(at + 4), (uint)y);
        }

        public void Resize(int page, int button, int width, int height) => TouchRects.Resize(_data, RectAt(page, button), width, height);

        public (double X, double Y) CursorCentre(int page, int stop)
        {
            if (page >= ListPage)
            {
                int spot = Start(ListCount(page), ListPart.Cursors) + stop * Spot;
                return (_data[spot], _data[spot + 1]);
            }
            (int top, int bottom, int left, int right) = AreaOf(page, stop);
            return ((left + right) / 2.0, (top + bottom) / 2.0);
        }

        private int TryAt(int stop, CursorDirection d, int i) => Start(Run.Flow) + (stop * 4 + (int)d) * Tries + i;

        private int KeyAt(int page, int stop, CursorDirection d) => Start(ListCount(page), ListPart.Keys) + stop * 4 + (int)d;

        // On a list the game skips an arrow holding -1 or the option itself, so the editor shows both as Stay.
        public int Target(int page, int stop, CursorDirection d)
        {
            if (page < ListPage) return _data[TryAt(stop, d, 0)];
            int to = _data[KeyAt(page, stop, d)];
            return to == NoMove ? stop : to;
        }

        public void SetTarget(int page, int stop, CursorDirection d, int target)
        {
            if (page < ListPage) _data[TryAt(stop, d, 0)] = (byte)target;
            else if (target != Target(page, stop, d)) _data[KeyAt(page, stop, d)] = target == stop ? (byte)NoMove : (byte)target;
        }

        /// <summary>A slot or option; the stop itself means stay.</summary>
        public IReadOnlyList<(int Value, string Name)> Choices(int page) =>
            (page < ListPage ? MenuButtons.Take(Slots) : ButtonsOf(page)).Select((n, i) => (i, n)).ToList();

        public int ValueOf(int page, int stop) => stop;
        public int StopOf(int page, int value) => value;
        public string Fixed(int page, int stop, CursorDirection d) => null;

        public string RowsLabel => "If that slot is hidden";
        public string RowsTip => "Tried in order";

        public IReadOnlyList<string> RowNames(int page) =>
            page != 0 ? Array.Empty<string>()
                : Enumerable.Range(0, Slots).SelectMany(s => CursorDirections.All.Select(d => $"{d} from {MenuButtons[s]}")).ToList();

        public int RowLength(int page) => Tries - 1;

        public int RowValue(int page, int row, int i) => _data[Start(Run.Flow) + row * Tries + 1 + i];

        public void SetRowValue(int page, int row, int i, int value) => _data[Start(Run.Flow) + row * Tries + 1 + i] = (byte)value;

        public byte[] ToBytes() => _tables.Copy();
        public void FromBytes(byte[] bytes) => _tables.Load(bytes);
        public byte[] FileBytes() => _tables.FileBytes();

        // ov27_0225B398 asserts the slot is under seven, and a larger one reads past the shown flags; ov27_0225CA68 takes any
        // list key that isn't -1 as the new option.
        private IEnumerable<string> Problems(byte[] data)
        {
            if (data.Skip(Start(Run.Flow)).Take(Slots * 4 * Tries).Any(v => v >= Slots)) yield return "Every arrow needs one of the seven slots.";
            for (int count = FirstList; count < FirstList + Lists; count++)
                if (data.Skip(Start(count, ListPart.Keys)).Take(count * 4).Any(v => v != NoMove && v >= count))
                    yield return $"List with {count} options: every arrow needs one of its options.";
        }

        public string WhyNot() => _tables.NewProblem(Problems, _data);

        public string WhyNotWrite(byte[] bytes) =>
            bytes == null || bytes.Length != _data.Length ? "The touch menu tables don't match this ROM." : _tables.NewProblem(Problems, bytes);

        public void WriteFile(byte[] bytes)
        {
            if (WhyNotWrite(bytes) is string why) throw new InvalidOperationException(why);
            _tables.Write(bytes);
        }
    }
}

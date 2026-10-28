using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The HeartGold and SoulSilver bag's bottom screen: touch areas per menu, where the arrow keys go and where the cursor
    /// sprite sits. The buttons' art doesn't move with them.
    /// </summary>
    public sealed class FieldBagButtons : IScreenButtons
    {
        /// <param name="At">Its tables, from the context menu's arrow table.</param>
        /// <param name="Spots">Each stop's entry in its cursor spot table (x, y, animation, palette).</param>
        /// <param name="First">The value the page's first stop has in its arrow table.</param>
        private sealed record Page(string Name, string[] Buttons, RomLocator.BagTable At, int[] Spots, int First = 0)
        {
            public int Stops => At.Flow < 0 ? 0 : Spots.Length;
        }

        private const int Arrows = 4, Spot = 4;
        // bag_app_main.c BAG_APP_CURSOR_POS_RESET_TO_CURR_POCKET.
        private const int OpenPocket = 17;
        private const int Cancel = 16;

        private static readonly string[] MainButtons =
        {
            "Pocket 1", "Pocket 2", "Pocket 3", "Pocket 4", "Pocket 5", "Pocket 6", "Pocket 7", "Pocket 8",
            "Item 1", "Item 2", "Item 3", "Item 4", "Item 5", "Item 6", "Previous", "Next", "Cancel",
        };

        private static readonly Page[] Layout =
        {
            new Page("Pockets and items", MainButtons, RomLocator.BagTables[0], Enumerable.Range(0, 17).ToArray()),
            new Page("Item menu", new[] { "Option 1", "Option 2", "Option 3", "Option 4", "Cancel" }, RomLocator.BagTables[1],
                     new[] { 17, 18, 19, 20, Cancel }),
            new Page("Moving an item", MainButtons.Skip(8).ToArray(), RomLocator.BagTables[2], Enumerable.Range(0, 9).ToArray(), 8),
            new Page("Toss amount", new[] { "Hundreds up", "Tens up", "Ones up", "Hundreds down", "Tens down", "Ones down", "Confirm", "Cancel" },
                     RomLocator.BagTables[3], Array.Empty<int>()),
            new Page("Sell amount", new[] { "Tens up", "Ones up", "Tens down", "Ones down", "Confirm", "Cancel" }, RomLocator.BagTables[4],
                     Array.Empty<int>()),
        };

        private readonly ScreenTables _tables;
        // Where each page's touch areas and arrows start in _data.
        private readonly (int Touch, int Flow)[] _starts;
        private readonly Dictionary<int, int> _spotTables = new Dictionary<int, int>();
        private readonly byte[] _data;

        private FieldBagButtons(int overlay, int anchor, ScreenTables tables)
        {
            _tables = tables;
            _starts = Layout.Select(p => (tables.Take(overlay, anchor + p.At.Touch, p.Buttons.Length * TouchRects.Size),
                                          p.At.Flow >= 0 ? tables.Take(overlay, anchor + p.At.Flow, p.Spots.Length * Arrows) : tables.Count)).ToArray();
            foreach (int table in Layout.Where(p => p.At.Flow >= 0).Select(p => p.At.Cursor).Distinct())
                _spotTables[table] = tables.Take(overlay, anchor + table, SpotCount(table) * Spot);
            _data = tables.Finish();
        }

        private static int SpotCount(int table) => Layout.Where(p => p.At.Flow >= 0 && p.At.Cursor == table).Max(p => p.Spots.Max()) + 1;

        /// <summary>Null when this ROM's tables aren't known or don't look like the game's.</summary>
        public static FieldBagButtons Read()
        {
            RomInfo.BagButtonSites sites = RomLocator.BagButtonSites;
            if (sites == null) return null;
            ScreenTables tables = new ScreenTables();
            byte[] file = tables.FileOf(sites.Overlay);
            if (file == null) return null;
            int a = (int)sites.ContextFlow;
            foreach (Page p in Layout)
            {
                int end = a + p.At.Touch + p.Buttons.Length * TouchRects.Size;
                // Each touch list ends where the game's does.
                if (a + p.At.Touch < 0 || end >= file.Length || file[end] != 0xFF) return null;
            }
            FieldBagButtons buttons = new FieldBagButtons(sites.Overlay, a, tables);
            return tables.Missing ? null : buttons;
        }

        public IReadOnlyList<string> PageNames => Layout.Select(p => p.Name).ToList();
        public string[] ButtonsOf(int page) => Layout[page].Buttons;
        public int StopsOf(int page) => Layout[page].Stops;

        private int TouchAt(int page, int button) => _starts[page].Touch + button * TouchRects.Size;

        public (int Top, int Bottom, int Left, int Right) AreaOf(int page, int button) => TouchRects.Read(_data, TouchAt(page, button));

        private int SpotAt(int page, int stop) => _spotTables[Layout[page].At.Cursor] + Layout[page].Spots[stop] * Spot;

        public (double X, double Y) CursorCentre(int page, int stop)
        {
            int at = SpotAt(page, stop);
            return (_data[at], _data[at + 1]);
        }

        public void Move(int page, int button, int dx, int dy)
        {
            (dx, dy) = TouchRects.Move(_data, TouchAt(page, button), dx, dy);
            if (button >= StopsOf(page)) return;
            int s = SpotAt(page, button);
            ScreenSpots.Shift(_data, s, 1, dx, 255);
            ScreenSpots.Shift(_data, s + 1, 1, dy, 191);
        }

        public void Resize(int page, int button, int width, int height) => TouchRects.Resize(_data, TouchAt(page, button), width, height);

        private int ArrowAt(int page, int stop, CursorDirection d) => _starts[page].Flow + stop * Arrows + (int)d;

        public int Target(int page, int stop, CursorDirection d) => _data[ArrowAt(page, stop, d)];

        public void SetTarget(int page, int stop, CursorDirection d, int target)
        {
            if (Fixed(page, stop, d) == null) _data[ArrowAt(page, stop, d)] = (byte)target;
        }

        public IReadOnlyList<(int Value, string Name)> Choices(int page)
        {
            Page p = Layout[page];
            List<(int, string)> choices = Enumerable.Range(0, p.Stops).Select(s => (ValueOf(page, s), p.Buttons[s])).ToList();
            if (page == 0) choices.Add((OpenPocket, "Open pocket"));
            return choices;
        }

        public int ValueOf(int page, int stop) => Layout[page].First + stop;

        public int StopOf(int page, int value)
        {
            int stop = value - Layout[page].First;
            return stop >= 0 && stop < StopsOf(page) ? stop : -1;
        }

        // bag_app_main.c BagAppMainTask_HandleInput_Normal: pockets go to the next pocket that has items, Cancel ignores Left and Right.
        public string Fixed(int page, int stop, CursorDirection d)
        {
            if (page != 0 || d is not (CursorDirection.Left or CursorDirection.Right)) return null;
            if (stop < 8) return "The game picks the next pocket";
            return stop == Cancel ? "The game ignores this" : null;
        }

        public byte[] ToBytes() => _tables.Copy();
        public void FromBytes(byte[] bytes) => _tables.Load(bytes);
        public byte[] FileBytes() => _tables.FileBytes();

        public string WhyNot() => _tables.NewProblem(Problems, _data);

        private IEnumerable<string> Problems(byte[] data)
        {
            for (int page = 0; page < Layout.Length; page++)
            {
                HashSet<int> allowed = Choices(page).Select(c => c.Value).ToHashSet();
                for (int stop = 0; stop < StopsOf(page); stop++)
                    foreach (CursorDirection d in CursorDirections.All)
                        if (Fixed(page, stop, d) == null && !allowed.Contains(data[ArrowAt(page, stop, d)]))
                            yield return $"{Layout[page].Name}: {d} from {Layout[page].Buttons[stop]} goes to a button the cursor can't reach.";
            }
        }

        public string WhyNotWrite(byte[] bytes) =>
            bytes == null || bytes.Length != _data.Length ? "The bag tables don't match this ROM." : _tables.NewProblem(Problems, bytes);

        public void WriteFile(byte[] bytes)
        {
            if (WhyNotWrite(bytes) is string why) throw new InvalidOperationException(why);
            _tables.Write(bytes);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using Flow = DSPRE.ROMFiles.PartyCursorFlow;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    // Edits where the arrow keys move the party menu cursor.
    public sealed partial class ScreenWindowLayoutViewModel : ICursorEditor
    {
        private Flow _flow;
        private PendingArchiveWrites.Target _cursorTable;
        // HGSS only. The Pokémon and item menus come after the party sets in the Menu picker.
        private PartyContextMenu _menu;
        private PendingArchiveWrites.Target _menuTable;
        private readonly List<(PartyContextMenu.Menu Menu, int Options)> _menuSets = new List<(PartyContextMenu.Menu, int)>();

        private void OpenCursor()
        {
            if (_screen != "party") return;
            _flow = Flow.Read();
            if (_flow == null) return;
            _cursorTable = new PendingArchiveWrites.Target("cursor", _flow.FileBytes, _flow.WriteFile);
            foreach (Flow.Set set in Flow.Sets) CursorSets.Add(Flow.SetName(set));
            _menu = PartyContextMenu.Read();
            if (_menu != null)
            {
                _menuTable = new PendingArchiveWrites.Target("menu", _menu.FileBytes, _menu.WriteFile);
                foreach (PartyContextMenu.Menu menu in new[] { PartyContextMenu.Menu.Pokemon, PartyContextMenu.Menu.Item })
                    for (int n = 2; n <= PartyContextMenu.MostOptions(menu); n++)
                    {
                        _menuSets.Add((menu, n));
                        CursorSets.Add($"{(menu == PartyContextMenu.Menu.Pokemon ? "Pokémon" : "Item")} menu with {n} options");
                    }
            }
            for (int row = 0; row < Flow.BackRows; row++) BackRows.Add(BackRow(row));
            FillCursor();
        }

        private (PartyContextMenu.Menu Menu, int Options)? MenuSet =>
            _cursorSet >= Flow.Sets.Length && _cursorSet - Flow.Sets.Length < _menuSets.Count ? _menuSets[_cursorSet - Flow.Sets.Length] : null;

        private const int MenuStay = -1;

        // Moving or resizing a menu label moves its frame and touch area too; the frame sits one tile outside the label.
        private void FollowLabel(ScreenWindowLayout.Window label)
        {
            if (_menu == null) return;
            int window = Enumerable.Range(0, PartyContextMenu.Buttons).FirstOrDefault(i => PartyContextMenu.ButtonName(i) == label.Name, -1);
            if (window < 0) return;
            (int x, int y, int _, int _) = _menu.Frame(window);
            _menu.Move(window, label.X - 1 - x, label.Y - 1 - y);
            _menu.Resize(window, label.Width + 2, label.Height + 2);
        }

        public string CursorSetLabel => "Menu";
        public ObservableCollection<string> CursorSets { get; } = new ObservableCollection<string>();
        public ObservableCollection<CursorStopRow> CursorStops { get; } = new ObservableCollection<CursorStopRow>();
        public ObservableCollection<CursorBackRow> BackRows { get; } = new ObservableCollection<CursorBackRow>();

        /// <summary>The screen the party panels are on.</summary>
        public Bitmap CursorPicture => RomInfo.GameFamily == RomInfo.GameFamilies.HGSS ? _bottom : _top;

        /// <summary>Raised when stops, arrows or the selection change, so the picture's arrows redraw.</summary>
        public event Action CursorChanged;

        private Flow.Set Set => Flow.Sets[Math.Clamp(_cursorSet, 0, Flow.Sets.Length - 1)];

        private int _cursorSet;
        public int CursorSetIndex
        {
            get => _cursorSet;
            set
            {
                if (value < 0 || value == _cursorSet) return;
                _cursorSet = value;
                Raise();
                if (MenuSet is (PartyContextMenu.Menu menu, int options)) ShowStateOf(menu, options);
                else ShowStateOf(Set);
                Raise(nameof(HasBackRows));
                FillCursor();
            }
        }

        // The Cursor tab draws the Layout's picture, so switch the Layout to the state this set shows.
        private void ShowStateOf(Flow.Set set)
        {
            string state = set switch
            {
                Flow.Set.MultiBattle => "Multi battle",
                Flow.Set.BattleEntry or Flow.Set.EggChoice => "Confirm and cancel",
                _ => "Normal",
            };
            int index = States.IndexOf(state);
            if (index >= 0) StateIndex = index;
        }

        // Five or more options put field moves in the left column.
        private void ShowStateOf(PartyContextMenu.Menu menu, int options)
        {
            string state = menu == PartyContextMenu.Menu.Item ? "Item menu" : options >= 5 ? "Menu with field moves" : "Menu open";
            int index = States.IndexOf(state);
            if (index >= 0) StateIndex = index;
        }

        // Lets the picture draw the open menu's options where the game puts them.
        private (bool Item, int[] Windows)? ShownMenu =>
            MenuSet is (PartyContextMenu.Menu menu, int options) && _menu != null ? (menu == PartyContextMenu.Menu.Item, _menu.OptionWindows(menu, options)) : null;

        // party_menu.c lists Summary, Switch, Item, Quit, then field moves; the item menu ends with Quit.
        private static string MenuOptionName(PartyContextMenu.Menu menu, int option, int options)
        {
            if (menu == PartyContextMenu.Menu.Item) return option == options - 1 ? "Quit" : $"Option {option + 1}";
            string[] first = options switch { 2 => new[] { "Summary", "Quit" }, 3 => new[] { "Summary", "Switch", "Quit" }, _ => new[] { "Summary", "Switch", "Item", "Quit" } };
            return option < first.Length ? first[option] : $"Field move {option - first.Length + 1}";
        }

        private CursorStopRow _selectedStop;
        public CursorStopRow SelectedStop
        {
            get => _selectedStop;
            set
            {
                if (ReferenceEquals(_selectedStop, value)) return;
                _selectedStop = value;
                Raise();
                CursorChanged?.Invoke();
            }
        }

        // The stops this set shows, then Stay.
        private List<int> _choiceStops = new List<int>();
        public List<string> CursorChoices { get; private set; } = new List<string>();

        public string StopName(int stop) => MenuSet is (PartyContextMenu.Menu menu, int options) ? MenuOptionName(menu, stop, options) : Flow.StopName(stop);
        public bool HasBackRows => _flow != null && MenuSet == null;

        private void FillCursor()
        {
            if (_flow == null) return;
            int selected = _selectedStop?.Stop ?? 0;
            if (MenuSet is (PartyContextMenu.Menu menu, int options))
            {
                _choiceStops = Enumerable.Range(0, options).Append(MenuStay).ToList();
                CursorChoices = _choiceStops.Select(s => s == MenuStay ? "Stay" : MenuOptionName(menu, s, options)).ToList();
                Raise(nameof(CursorChoices));
                CursorStops.Clear();
                for (int o = 0; o < options; o++)
                    CursorStops.Add(new CursorStopRow(this, o, HpBarLayoutEditorViewModel.Colours[o % HpBarLayoutEditorViewModel.Colours.Length]));
                _selectedStop = CursorStops.FirstOrDefault(r => r.Stop == selected) ?? CursorStops.FirstOrDefault();
                Raise(nameof(SelectedStop));
                RefreshCursor();
                return;
            }
            _choiceStops = Enumerable.Range(0, Flow.Stops).Where(s => _flow.Used(Set, s)).Append(Flow.Stay).ToList();
            CursorChoices = _choiceStops.Select(s => s == Flow.Stay ? "Stay" : s < Flow.Pokemon ? (s + 1).ToString() : Flow.StopName(s)).ToList();
            Raise(nameof(CursorChoices));
            CursorStops.Clear();
            int colour = 0;
            foreach (int stop in _choiceStops.Where(s => s != Flow.Stay))
                CursorStops.Add(new CursorStopRow(this, stop, HpBarLayoutEditorViewModel.Colours[colour++ % HpBarLayoutEditorViewModel.Colours.Length]));
            _selectedStop = CursorStops.FirstOrDefault(r => r.Stop == selected) ?? CursorStops.FirstOrDefault();
            Raise(nameof(SelectedStop));
            RefreshCursor();
        }

        private void RefreshCursor()
        {
            if (_flow == null) return;
            if (CursorStops.Count == 0) { FillCursor(); return; }
            foreach (CursorStopRow row in CursorStops) row.Refresh();
            foreach (CursorBackRow row in BackRows) row.Refresh();
            CursorStatus = (MenuSet != null ? _menu.WhyNotWrite(_menu.ToBytes()) : _flow.WhyNot()) ?? "";
            CursorChanged?.Invoke();
        }

        private string _cursorStatus = "";
        public string CursorStatus { get => _cursorStatus; private set { _cursorStatus = value; Raise(); } }

        public string BackRowsLabel => "Leaving the buttons";
        public string BackRowsTip => "First slot with a Pokémon wins";

        private int ChoiceOf(int stop) => Math.Max(0, _choiceStops.IndexOf(stop));

        public int ChoiceFor(int stop, CursorDirection direction) => ChoiceOf(TargetOf(stop, direction));

        public int TargetOf(int stop, CursorDirection direction) =>
            MenuSet is (PartyContextMenu.Menu menu, int options) ? _menu.Target(menu, options, stop, direction) : _flow.Target(Set, stop, direction);

        // party_context_menu.c reads Left and Right from one entry, and the item menu reads neither.
        public string FixedWhy(int stop, CursorDirection direction) => MenuSet switch
        {
            (PartyContextMenu.Menu.Item, _) when direction is CursorDirection.Left or CursorDirection.Right => "This menu only goes up and down",
            (PartyContextMenu.Menu.Pokemon, _) when direction == CursorDirection.Right => "Same as Left",
            _ => null,
        };

        /// <summary>Points one of the stop's arrows at another stop or at Stay.</summary>
        public void SetDirection(int stop, CursorDirection direction, int target)
        {
            if (MenuSet is (PartyContextMenu.Menu menu, int options))
            {
                if (FixedWhy(stop, direction) != null || _menu.Target(menu, options, stop, direction) == target) return;
                _menu.SetTarget(menu, options, stop, direction, target);
                RefreshCursor();
                Commit();
                return;
            }
            if (_flow == null || _flow.Target(Set, stop, direction) == target) return;
            _flow.SetTarget(Set, stop, direction, target);
            RefreshCursor();
            Commit();
        }

        public void SetDirectionChoice(int stop, CursorDirection direction, int choice)
        {
            if (choice >= 0 && choice < _choiceStops.Count) SetDirection(stop, direction, _choiceStops[choice]);
        }

        private static readonly string[] BackLabels = { "Down from left", "Down from right", "Up from left", "Up from right" };
        private static readonly List<string> SlotNames = Enumerable.Range(1, PartyCursorFlow.Pokemon).Select(n => n.ToString()).ToList();

        private CursorBackRow BackRow(int row) => new CursorBackRow(BackLabels[row],
            Enumerable.Range(0, PartyCursorFlow.BackLength).Select(i => new CursorBackSlot(SlotNames, () => BackSlot(row, i), v => SetBackSlot(row, i, v))));

        internal int BackSlot(int row, int i) => _flow.Back(row, i);

        internal void SetBackSlot(int row, int i, int slot)
        {
            if (_flow == null || _flow.Back(row, i) == slot) return;
            _flow.SetBack(row, i, slot);
            RefreshCursor();
            Commit();
        }

        /// <summary>In board pixels (two per screen pixel); Pokémon stops follow their panels.</summary>
        public (double X, double Y) StopAt(int stop)
        {
            if (MenuSet is (PartyContextMenu.Menu menu, int options))
            {
                (int fx, int fy, int fw, int fh) = _menu.Frame(_menu.OptionWindows(menu, options)[stop]);
                return ((fx * 8 + fw * 4) * Tile / 8, (fy * 8 + fh * 4) * Tile / 8);
            }
            (int x, int y) = stop < Flow.Pokemon && _panels != null && Set != Flow.Set.MultiBattle
                ? (_panels.Panel(stop).X * 8 + 4 * _panels.Width, _panels.Panel(stop).Y * 8 + 4 * _panels.Height)
                : _flow.Position(Set, stop);
            return (x * Tile / 8, y * Tile / 8);
        }
    }

    /// <summary>One row of pickers: a party back list or a grid row.</summary>
    public sealed class CursorBackRow
    {
        public CursorBackRow(string label, IEnumerable<CursorBackSlot> slots)
        {
            Label = label;
            foreach (CursorBackSlot slot in slots) Slots.Add(slot);
        }

        public string Label { get; }
        public ObservableCollection<CursorBackSlot> Slots { get; } = new ObservableCollection<CursorBackSlot>();

        public void Refresh()
        {
            foreach (CursorBackSlot slot in Slots) slot.Refresh();
        }
    }

    public sealed class CursorBackSlot : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly Func<int> _get;
        private readonly Action<int> _set;

        public CursorBackSlot(List<string> choices, Func<int> get, Action<int> set, double width = 56)
        {
            Choices = choices;
            _get = get;
            _set = set;
            Width = width;
        }

        public List<string> Choices { get; }
        public double Width { get; }

        public int Slot { get => _get(); set => _set(value); }

        public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Slot)));
    }
}

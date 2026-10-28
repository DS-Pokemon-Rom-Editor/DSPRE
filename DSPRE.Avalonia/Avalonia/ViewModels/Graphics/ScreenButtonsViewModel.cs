using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    /// <summary>Edits a touch screen's touch areas and arrow-key moves, page by page.</summary>
    public sealed class ScreenButtonsViewModel : ICursorEditor, DSPRE.Editors.IEditorWithUnsavedChanges, ISupportsUndo, IDetachable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        /// <summary>Board pixels per screen pixel.</summary>
        public const double Scale = 2;

        private readonly IScreenButtons _buttons;
        // Runs on the UI thread; the function it returns draws off it.
        private readonly Func<int, Func<byte[]>> _draw;
        private readonly PendingArchiveWrites _pending = new PendingArchiveWrites();
        private readonly PendingArchiveWrites.Target _tables;

        public ScreenButtonsViewModel() { if (!Design.IsDesignMode) return; }

        public ScreenButtonsViewModel(IScreenButtons buttons, string title, Func<int, Func<byte[]>> draw)
        {
            Title = title;
            _buttons = buttons;
            _draw = draw;
            if (_buttons == null) { CursorStatus = "Couldn't find this screen's buttons in the ROM."; return; }
            _tables = new PendingArchiveWrites.Target("buttons", _buttons.FileBytes, _buttons.WriteFile);
            foreach (string p in _buttons.PageNames) CursorSets.Add(p);
            Fill();
            _pending.Changed += OnPendingChanged;
        }

        public string Title { get; }
        public string UnsavedChangesDescription => Title + " buttons";

        // ── pages ──

        public string CursorSetLabel => "Page";
        public ObservableCollection<string> CursorSets { get; } = new ObservableCollection<string>();

        private int _page;
        public int CursorSetIndex
        {
            get => _page;
            set
            {
                if (value < 0 || value == _page || _buttons == null || value >= _buttons.PageNames.Count) return;
                _page = value;
                Raise();
                Raise(nameof(HasArt));
                Fill();
            }
        }

        // ── touch areas ──

        public ObservableCollection<TouchAreaRow> Areas { get; } = new ObservableCollection<TouchAreaRow>();

        private static string ColourOf(int i) => HpBarLayoutEditorViewModel.Colours[i % HpBarLayoutEditorViewModel.Colours.Length];

        private void Fill()
        {
            Areas.Clear();
            string[] buttons = _buttons.ButtonsOf(_page);
            for (int b = 0; b < buttons.Length; b++) Areas.Add(new TouchAreaRow(this, b, buttons[b], ColourOf(b)));
            int selected = _selectedStop?.Stop ?? 0;
            IReadOnlyList<(int Value, string Name)> choices = _buttons.Choices(_page);
            _choices = choices.Select(c => c.Value).ToList();
            CursorChoices = choices.Select(c => c.Name).ToList();
            Raise(nameof(CursorChoices));
            CursorStops.Clear();
            for (int b = 0; b < _buttons.StopsOf(_page); b++)
                if (_buttons.HasCursor(_page, b)) CursorStops.Add(new CursorStopRow(this, b, ColourOf(b)));
            _selectedStop = CursorStops.FirstOrDefault(r => r.Stop == selected) ?? CursorStops.FirstOrDefault();
            Raise(nameof(SelectedStop));
            FillGrid();
            Refresh();
        }

        private bool _drawing, _again;

        // Draws one picture at a time; edits made during a draw queue one more.
        private async Task DrawAsync()
        {
            if (_drawing) { _again = true; return; }
            _drawing = true;
            try
            {
                do
                {
                    _again = false;
                    byte[] rgba = null;
                    try
                    {
                        Func<byte[]> work = _draw(_page);
                        rgba = await Task.Run(work);
                    }
                    catch (Exception ex) { AppLogger.WarnOnce("ScreenButtonsViewModel.Draw", ex); }
                    if (!_again) CursorPicture = rgba == null ? null : ImageConverter.FromRgba(rgba, 256, 192);
                } while (_again);
            }
            finally { _drawing = false; }
        }

        internal (int Top, int Bottom, int Left, int Right) AreaOf(int button) => _buttons.AreaOf(_page, button);

        internal string LockedWhy(int button) => _buttons.MoveLocked(_page, button);

        /// <summary>Pixels per step for the button.</summary>
        internal int StepOf(int button) => _buttons?.MoveStep(_page, button) ?? 1;

        public void MoveTo(TouchAreaRow row, int left, int top)
        {
            (int t, int _, int l, int _) = AreaOf(row.Button);
            if ((left == l && top == t) || !row.Open) return;
            _buttons.Move(_page, row.Button, left - l, top - t);
            Refresh();
        }

        public void SizeTo(TouchAreaRow row, int width, int height)
        {
            if (!row.Open) return;
            _buttons.Resize(_page, row.Button, width, height);
            Refresh();
        }

        public void Commit() => _pending.Import(() => { _pending.Stage(_tables, _buttons.ToBytes()); return null; });

        // ── fixing tiles ──

        public bool HasArt => (_buttons as IScreenArt)?.HasArt(_page) == true;

        private bool _fixTiles;
        public bool FixTiles { get => _fixTiles; set { _fixTiles = value; Raise(); } }

        private ushort? _picked;

        /// <summary>Copies the tile under x, y (screen pixels) when pick is set, otherwise pastes the copied tile there.</summary>
        public void SquareClick(double x, double y, bool pick)
        {
            if (!FixTiles || _buttons is not IScreenArt art || !art.HasArt(_page)) return;
            int tx = (int)(x / 8), ty = (int)(y / 8);
            if (pick) { _picked = art.SquareAt(_page, tx, ty); return; }
            if (_picked is not ushort square) return;
            art.SetSquare(_page, tx, ty, square);
            Commit();
            Refresh();
        }

        private void Refresh()
        {
            foreach (TouchAreaRow row in Areas) row.Refresh();
            foreach (CursorStopRow row in CursorStops) row.Refresh();
            foreach (CursorBackRow row in BackRows) row.Refresh();
            CursorStatus = _buttons?.WhyNot() ?? "";
            Raise(nameof(HasUnsavedChanges));
            CursorChanged?.Invoke();
            if (_buttons != null) _ = DrawAsync();
        }

        private void OnPendingChanged()
        {
            Raise(nameof(CanUndo));
            Raise(nameof(CanRedo));
            // A commit stages what the tables already hold, so only undo, redo and discard need a redraw.
            if (_pending.Read(_tables) is byte[] bytes && !bytes.AsSpan().SequenceEqual(_buttons.ToBytes()))
            {
                _buttons.FromBytes(bytes);
                Refresh();
            }
            else Raise(nameof(HasUnsavedChanges));
        }

        // ── cursor ──

        public ObservableCollection<CursorStopRow> CursorStops { get; } = new ObservableCollection<CursorStopRow>();
        public ObservableCollection<CursorBackRow> BackRows { get; } = new ObservableCollection<CursorBackRow>();
        public bool HasBackRows => BackRows.Count > 0;
        public string BackRowsLabel => _buttons is IScreenButtonRows rows ? rows.RowsLabel : "Grid";
        public string BackRowsTip => _buttons is IScreenButtonRows rows ? rows.RowsTip : "Arrows move one cell at a time";

        // On grid pages the arrows come from the cells, so the cells are edited here.
        private void FillGrid()
        {
            BackRows.Clear();
            if (_buttons is IScreenButtonGrid grid)
            {
                (int w, int h) = grid.GridOf(_page);
                List<string> names = _buttons.ButtonsOf(_page).ToList();
                int page = _page;
                for (int y = 0; y < h; y++)
                {
                    int row = y;
                    BackRows.Add(new CursorBackRow("Row " + (y + 1), Enumerable.Range(0, w).Select(x =>
                        new CursorBackSlot(names, () => grid.Cell(page, x, row), v => SetCell(grid, page, x, row, v), 120))));
                }
            }
            else if (_buttons is IScreenButtonRows rows)
            {
                int page = _page;
                IReadOnlyList<string> names = rows.RowNames(page);
                for (int r = 0; r < names.Count; r++)
                {
                    int row = r;
                    BackRows.Add(new CursorBackRow(names[r], Enumerable.Range(0, rows.RowLength(page)).Select(i =>
                        new CursorBackSlot(CursorChoices, () => ChoiceOf(rows.RowValue(page, row, i)), v => SetRowValue(rows, page, row, i, v), 120))));
                }
            }
            Raise(nameof(HasBackRows));
        }

        private void SetRowValue(IScreenButtonRows rows, int page, int row, int i, int choice)
        {
            if (page != _page || choice < 0 || choice >= _choices.Count || rows.RowValue(page, row, i) == _choices[choice]) return;
            rows.SetRowValue(page, row, i, _choices[choice]);
            Refresh();
            Commit();
        }

        private void SetCell(IScreenButtonGrid grid, int page, int x, int y, int button)
        {
            if (page != _page || button < 0 || grid.Cell(page, x, y) == button) return;
            grid.SetCell(page, x, y, button);
            Refresh();
            Commit();
        }
        public event Action CursorChanged;

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

        // The values the pickers offer, in CursorChoices order.
        private List<int> _choices = new List<int>();
        public List<string> CursorChoices { get; private set; } = new List<string>();

        public string StopName(int stop) => _buttons.StopName(_page, stop);

        // Pickers hold values; the arrows and the board use stops.
        private int ChoiceOf(int value) => Math.Max(0, _choices.IndexOf(value));

        public int ChoiceFor(int stop, CursorDirection direction) => ChoiceOf(_buttons.Target(_page, stop, direction));

        /// <summary>The stop the Cursor tab draws the arrow to, or -1 when the game picks it or ignores the arrow.</summary>
        public int TargetOf(int stop, CursorDirection direction) =>
            _buttons is IScreenButtonGrid || FixedWhy(stop, direction) == null ? _buttons.StopOf(_page, _buttons.Target(_page, stop, direction)) : -1;

        public string FixedWhy(int stop, CursorDirection direction) => _buttons?.Fixed(_page, stop, direction);

        public void SetDirection(int stop, CursorDirection direction, int target) => SetValue(stop, direction, _buttons.ValueOf(_page, target));

        public void SetDirectionChoice(int stop, CursorDirection direction, int choice)
        {
            if (choice >= 0 && choice < _choices.Count) SetValue(stop, direction, _choices[choice]);
        }

        private void SetValue(int stop, CursorDirection direction, int value)
        {
            if (FixedWhy(stop, direction) != null || _buttons.Target(_page, stop, direction) == value) return;
            _buttons.SetTarget(_page, stop, direction, value);
            Refresh();
            Commit();
        }

        public (double X, double Y) StopAt(int stop)
        {
            (double x, double y) = _buttons.CursorCentre(_page, stop);
            return (x * Scale, y * Scale);
        }

        private string _status = "";
        public string CursorStatus { get => _status; private set { _status = value; Raise(); } }

        private Bitmap _picture;
        public Bitmap CursorPicture { get => _picture; private set { _picture = value; Raise(); } }

        // ── saving and undo ──

        public bool HasUnsavedChanges => _buttons != null && _pending.IsDirty;

        public void SaveChanges() => _ = SaveChangesAsync();

        public async Task<bool> SaveChangesAsync()
        {
            if (_buttons == null) return true;
            try
            {
                if (_buttons.WhyNotWrite(_pending.Read(_tables)) is string why) throw new InvalidOperationException(why);
                _pending.Save();
            }
            catch (Exception e)
            {
                CrashReporter.LogHandled(e);
                await DialogHelper.ShowErrorAsync("The buttons weren't saved:\n" + e.Message, Title + " error");
                return false;
            }
            Raise(nameof(HasUnsavedChanges));
            SaveNotice.Saved(UnsavedChangesDescription);
            return true;
        }

        public void DiscardChanges()
        {
            if (_buttons == null) return;
            _pending.Discard();
        }

        public bool CanUndo => _pending.CanUndo;
        public bool CanRedo => _pending.CanRedo;
        public void Undo() => _pending.Undo();
        public void Redo() => _pending.Redo();

        public void Detach() => _pending.Changed -= OnPendingChanged;
    }

    /// <summary>One button's touch area, in screen pixels.</summary>
    public sealed class TouchAreaRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private readonly ScreenButtonsViewModel _owner;

        public TouchAreaRow(ScreenButtonsViewModel owner, int button, string name, string colour)
        {
            _owner = owner;
            Button = button;
            Name = name;
            Colour = colour;
        }

        public int Button { get; }
        public string Name { get; }
        public string Colour { get; }
        public string Locked => _owner.LockedWhy(Button);
        public int Step => _owner.StepOf(Button);
        public bool Open => Locked == null;

        private (int Top, int Bottom, int Left, int Right) Area => _owner.AreaOf(Button);

        public int Across { get => Area.Left; set { _owner.MoveTo(this, value, Area.Top); _owner.Commit(); } }
        public int Down { get => Area.Top; set { _owner.MoveTo(this, Area.Left, value); _owner.Commit(); } }
        public int Wide { get => Area.Right - Area.Left; set { _owner.SizeTo(this, value, Tall); _owner.Commit(); } }
        public int Tall { get => Area.Bottom - Area.Top; set { _owner.SizeTo(this, Wide, value); _owner.Commit(); } }

        public double Left => Area.Left * ScreenButtonsViewModel.Scale;
        public double Top => Area.Top * ScreenButtonsViewModel.Scale;
        public double Width => Wide * ScreenButtonsViewModel.Scale;
        public double Height => Tall * ScreenButtonsViewModel.Scale;

        public void Refresh()
        {
            foreach (string n in new[] { nameof(Across), nameof(Down), nameof(Wide), nameof(Tall), nameof(Left), nameof(Top), nameof(Width), nameof(Height) })
                Raise(n);
        }
    }
}

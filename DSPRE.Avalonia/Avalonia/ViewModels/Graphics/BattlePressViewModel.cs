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
using static DSPRE.RomInfo;
using Rect = DSPRE.ROMFiles.BattlePressFrames.Rect;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    /// <summary>Shows and edits what each battle touch button draws at rest and in each press frame, square by square.</summary>
    public sealed class BattlePressViewModel : INotifyPropertyChanged, DSPRE.Editors.IEditorWithUnsavedChanges, ISupportsUndo, IDetachable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private const string Title = "Battle screen";

        /// <summary>Board pixels per screen pixel.</summary>
        public const int Zoom = 3;

        private const int Tile = 8, TileNumber = 0x3FF;

        // Picture is the page TouchTabPictures draws for the menu.
        private sealed record Menu(string Name, int Picture, Func<BattlePressFrames.Members, int> Member, string[] Buttons, Rect[] Rects);

        private static readonly Menu[] Menus =
        {
            new("Command", 0, m => m.Command, TouchButtonFit.Names, BattlePressFrames.CommandRects.Select((r, i) => i == 0 ? BattlePressFrames.FightReach : r).ToArray()),
            new("Fight", 1, m => m.Moves, new[] { "Move 1", "Move 2", "Move 3", "Move 4", "Cancel" }, BattlePressFrames.MoveRects),
            new("Target", 2, m => m.Target, new[] { "Bottom left", "Top right", "Bottom right", "Top left", "Cancel" }, BattlePressFrames.TargetRects),
            new("Yes or no", 3, m => m.YesNo, new[] { "Yes", "No" }, new[] { BattlePressFrames.YesRect, BattlePressFrames.NoRect }),
            new("Recorded battle", 6, m => m.Stop, new[] { "Stop" }, new[] { BattlePressFrames.StopRect }),
        };

        private const string NoFrames = "This screen file has no press frames.";

        private readonly PendingArchiveWrites _pending = new PendingArchiveWrites();
        private readonly BattlePressFrames.Members _panel;
        private readonly IDisposable _saved;
        private bool _ready;
        private ushort? _picked;

        public BattlePressViewModel() { if (!Design.IsDesignMode) return; }

        public BattlePressViewModel(BattlePressFrames.Members panel)
        {
            _panel = panel;
            _ready = BattlePressFrames.IsApplied;
            Frames.Add(new PressFrameRow(this, "Not pressed", BattlePressFrames.NormalPage, -1));
            Frames.Add(new PressFrameRow(this, "First press frame", BattlePressFrames.WhitePage, BattlePressFrames.OriginalWhite));
            Frames.Add(new PressFrameRow(this, "Second press frame", BattlePressFrames.PalePage, BattlePressFrames.OriginalPale));
            _pending.Changed += OnPendingChanged;
            AppEvents.RomPatchStateChanged += OnPatchStateChanged;
            _saved = AppEvents.WhenSaved(() => _ = DrawAsync(), DirNames.BattleBg);
            FillButtons();
        }

        public string UnsavedChangesDescription => Title + " press frames";

        public bool Ready => _ready;
        public bool NeedsPatch => !Ready;

        public void OpenPatch() => AvaloniaEditorLauncher.OpenPatchToolboxAt("buttonFrames");

        public IReadOnlyList<string> MenuNames { get; } = Menus.Select(m => m.Name).ToList();
        public ObservableCollection<string> Buttons { get; } = new ObservableCollection<string>();
        public ObservableCollection<PressFrameRow> Frames { get; } = new ObservableCollection<PressFrameRow>();

        private int _menu, _button;

        public int MenuIndex
        {
            get => _menu;
            set
            {
                if (value < 0 || value >= Menus.Length || value == _menu) return;
                _menu = value;
                Raise();
                FillButtons();
            }
        }

        public int ButtonIndex
        {
            get => _button;
            set
            {
                if (value < 0 || value >= Shown.Buttons.Length || value == _button) return;
                _button = value;
                Raise();
                _ = DrawAsync();
            }
        }

        private Menu Shown => Menus[_menu];
        private Rect Area => Shown.Rects[_button];
        private int Member => Shown.Member(_panel);

        private void FillButtons()
        {
            _button = 0;
            Buttons.Clear();
            foreach (string b in Shown.Buttons) Buttons.Add(b);
            Raise(nameof(ButtonIndex));
            _ = DrawAsync();
        }

        private string _status = "";
        public string Status { get => _status; private set { _status = value; Raise(); } }

        // ── reading and writing ──

        private ushort[][] ReadPages() => BattlePressFrames.ReadPages(GraphicAssets.Unsqueeze(_pending.ReadMember(DirNames.BattleBg, Member)));

        private void Change(Action<ushort[][]> change)
        {
            int member = Member;
            byte[] stored = _pending.ReadMember(DirNames.BattleBg, member), plain = GraphicAssets.Unsqueeze(stored);
            ushort[][] pages = BattlePressFrames.ReadPages(plain);
            if (pages.Length < BattlePressFrames.Pages) { Status = NoFrames; return; }
            change(pages);
            byte[] screen = BattlePressFrames.WithPages(plain, pages);
            byte marker = GraphicAssets.SqueezeMarker(stored);
            byte[] file = marker != 0 ? GraphicAssets.Squeeze(screen, marker) : screen;
            _pending.Change(step => step.Put(DirNames.BattleBg, member, file));
        }

        private void Fill(int page, Func<ushort[][], int, ushort> entry)
        {
            Rect r = Area;
            Change(pages =>
            {
                for (int y = r.Top; y <= r.Bottom; y++)
                    for (int x = r.Left; x <= r.Right; x++)
                    {
                        int square = y * BattlePressFrames.Columns + x;
                        pages[page][square] = entry(pages, square);
                    }
            });
        }

        internal void MatchRest(PressFrameRow frame) => Fill(frame.Page, (pages, square) => pages[BattlePressFrames.NormalPage][square]);

        internal void GameLook(PressFrameRow frame) => Fill(frame.Page, (pages, square) => pages[frame.Original][square]);

        /// <summary>Copies the square under x, y (board pixels) when pick is set, otherwise pastes the copied one there.</summary>
        public void SquareClick(PressFrameRow frame, double x, double y, bool pick)
        {
            Rect r = Area;
            int tx = r.Left + (int)(x / (Tile * Zoom)), ty = r.Top + (int)(y / (Tile * Zoom));
            if (tx < r.Left || ty < r.Top || tx > r.Right || ty > r.Bottom || !Ready) return;
            int square = ty * BattlePressFrames.Columns + tx;
            if (pick)
            {
                ushort[][] pages = ReadPages();
                if (frame.Page >= pages.Length) { Status = NoFrames; return; }
                _picked = pages[frame.Page][square];
                Status = $"Copied tile {_picked & TileNumber} with colour row {_picked >> 12}";
                return;
            }
            if (_picked is ushort entry) Change(pages => pages[frame.Page][square] = entry);
        }

        // ── pictures ──

        private bool _drawing, _again;

        private async Task DrawAsync()
        {
            if (!Ready || _panel == null) return;
            if (_drawing) { _again = true; return; }
            _drawing = true;
            try
            {
                do
                {
                    _again = false;
                    Menu menu = Shown;
                    Rect rect = Area;
                    PressFrameRow[] rows = Frames.ToArray();
                    (byte[] Rgba, int Width, int Height)[] drawn = await Task.Run(() =>
                    {
                        using (_pending.Reading()) return rows.Select(f => Crop(menu.Picture, rect, f.Page)).ToArray();
                    });
                    if (_again) continue;
                    for (int i = 0; i < rows.Length; i++)
                        rows[i].Picture = drawn[i].Rgba == null ? null : ImageConverter.FromRgba(drawn[i].Rgba, drawn[i].Width, drawn[i].Height);
                } while (_again);
            }
            catch (Exception ex) { AppLogger.WarnOnce("BattlePressViewModel.Draw", ex); }
            finally { _drawing = false; }
        }

        private static (byte[] Rgba, int Width, int Height) Crop(int picture, Rect r, int page)
        {
            byte[] whole = TouchTabPictures.Battle(picture, null, page == BattlePressFrames.NormalPage ? null : (r, page));
            if (whole == null) return (null, 0, 0);
            int cols = r.Right - r.Left + 1, rows = r.Bottom - r.Top + 1, width = cols * Tile * Zoom, height = rows * Tile * Zoom;
            byte[] outp = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int from = ((r.Top * Tile + y / Zoom) * 256 + r.Left * Tile + x / Zoom) * 4, to = (y * width + x) * 4;
                    Array.Copy(whole, from, outp, to, 4);
                    if (x % (Tile * Zoom) == 0 || y % (Tile * Zoom) == 0)
                        for (int c = 0; c < 3; c++) outp[to + c] = (byte)(outp[to + c] * 3 / 4);
                }
            return (outp, width, height);
        }

        private void OnPendingChanged()
        {
            Raise(nameof(CanUndo));
            Raise(nameof(CanRedo));
            Raise(nameof(HasUnsavedChanges));
            _ = DrawAsync();
        }

        private void OnPatchStateChanged(object sender, EventArgs e)
        {
            _ready = BattlePressFrames.IsApplied;
            Raise(nameof(Ready));
            Raise(nameof(NeedsPatch));
            _ = DrawAsync();
        }

        // ── saving and undo ──

        public bool HasUnsavedChanges => _pending.IsDirty;

        public void SaveChanges() => _ = SaveChangesAsync();

        public async Task<bool> SaveChangesAsync()
        {
            try { _pending.Save(); }
            catch (Exception e)
            {
                CrashReporter.LogHandled(e);
                await DialogHelper.ShowErrorAsync("The press frames weren't saved:\n" + e.Message, Title + " error");
                return false;
            }
            Raise(nameof(HasUnsavedChanges));
            SaveNotice.Saved(UnsavedChangesDescription);
            return true;
        }

        public void DiscardChanges() => _pending.Discard();

        public bool CanUndo => _pending.CanUndo;
        public bool CanRedo => _pending.CanRedo;
        public void Undo() => _pending.Undo();
        public void Redo() => _pending.Redo();

        public void Detach()
        {
            _pending.Changed -= OnPendingChanged;
            AppEvents.RomPatchStateChanged -= OnPatchStateChanged;
            _saved?.Dispose();
        }
    }

    /// <summary>One frame of the shown button.</summary>
    public sealed class PressFrameRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private readonly BattlePressViewModel _owner;
        private Bitmap _picture;

        public PressFrameRow(BattlePressViewModel owner, string name, int page, int original)
        {
            _owner = owner;
            Name = name;
            Page = page;
            Original = original;
        }

        public string Name { get; }
        public int Page { get; }
        public int Original { get; }
        public bool IsPress => Original >= 0;

        public Bitmap Picture
        {
            get => _picture;
            set
            {
                _picture = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Picture)));
            }
        }

        public void MatchRest() => _owner.MatchRest(this);
        public void GameLook() => _owner.GameLook(this);
    }
}

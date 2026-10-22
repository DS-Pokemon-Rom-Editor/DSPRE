using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public sealed class HpBarPartRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public BattleGaugeLayout.Part Part { get; init; }
        public string Name => BattleGaugeLayout.NameOf(Part);
        public int W { get; init; }
        public int H { get; init; }
        public string Colour { get; init; }

        private int _x, _y;
        public int X { get => _x; set { _x = value; Raise(); Raise(nameof(Left)); Raise(nameof(Column)); } }
        public int Y { get => _y; set { _y = value; Raise(); Raise(nameof(Top)); Raise(nameof(Row)); } }

        /// <summary>Set by the editor, so typing a square moves the part the same way a drag does.</summary>
        public Action<HpBarPartRow, int, int> MoveTo;

        public int Column { get => _x; set => MoveTo?.Invoke(this, value, _y); }
        public int Row { get => _y; set => MoveTo?.Invoke(this, _x, value); }

        private string _problem;
        public string Problem { get => _problem; set { _problem = value; Raise(); Raise(nameof(HasProblem)); Raise(nameof(Outline)); Raise(nameof(Fill)); Raise(nameof(Flagged)); } }
        public bool HasProblem => _problem != null;

        private bool _clashes;
        /// <summary>The game will draw it over another part or off the bar's art: allowed, but it shows in battle.</summary>
        public bool Clashes { get => _clashes; set { _clashes = value; Raise(); Raise(nameof(Outline)); Raise(nameof(Fill)); Raise(nameof(Flagged)); } }

        public string Outline => _problem != null ? "#FF3030" : _clashes ? "#FF9800" : Colour;

        // An outline alone gets lost among the parts' colours.
        public string Fill => _problem != null ? "#88FF3030" : _clashes ? "#88FF9800" : "#22FFFFFF";

        public bool Flagged => _problem != null || _clashes;

        public double Left => X * HpBarLayoutEditorViewModel.Square;
        public double Top => Y * HpBarLayoutEditorViewModel.Square;
        public double Width => W * HpBarLayoutEditorViewModel.Square;
        public double Height => H * HpBarLayoutEditorViewModel.Square;
    }

    /// <summary>
    /// Moves the parts the battle writes into an HP bar (name, level, HP numbers, gauges, status and the rest) to
    /// wherever a design puts them, and moves the bars themselves on screen. Everything it changes is in the battle
    /// overlay, staged until Save like the Graphics window's imports.
    /// </summary>
    public sealed class HpBarLayoutEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo, IDetachable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        /// <summary>How many screen pixels one square of the bar takes on the canvas.</summary>
        public const double Square = 40;
        public const double CanvasWidth = BattleGaugeLayout.Columns * Square, CanvasHeight = BattleGaugeLayout.Rows * Square;

        internal static readonly string[] Colours =
        {
            "#4FC3F7", "#FFB74D", "#AED581", "#F06292", "#BA68C8", "#FFF176", "#4DB6AC", "#E57373", "#90A4AE", "#A1887F", "#81C784", "#64B5F6",
        };

        private static readonly PendingArchiveWrites.Target OverlayTarget =
            new("battle-overlay", BattleGaugePieces.ReadOverlayCopy, b => BattleGaugePieces.WriteOverlay(b));

        private readonly PendingArchiveWrites _pending = new();
        private readonly Dictionary<BattleGaugePieces.Bar, BattleGaugeLayout> _drafts = new();

        public HpBarLayoutEditorViewModel()
        {
            _pending.Changed += () =>
            {
                _drafts.Clear();
                LoadBar();
                foreach (string n in new[] { nameof(HasUnsavedChanges), nameof(CanUndo), nameof(CanRedo), nameof(CanSave) }) Raise(n);
            };
            foreach (BattleGaugeComposer.Kind kind in Enum.GetValues<BattleGaugeComposer.Kind>()) BarNames.Add(BattleGaugeComposer.NameOf(kind));
            if (global::Avalonia.Controls.Design.IsDesignMode) return;
            AppEvents.RomPatchStateChanged += OnPatchStateChanged;
            LoadBar();
        }

        public void Detach() => AppEvents.RomPatchStateChanged -= OnPatchStateChanged;

        public bool CanClearTextBackground => HpBarTextPatchOffer.CanOffer;
        public string ClearTextBackgroundHelp => HpBarTextPatchOffer.Tip;

        public void OpenTextBackgroundPatch() => HpBarTextPatchOffer.Open();

        private void OnPatchStateChanged(object sender, EventArgs e)
        {
            Raise(nameof(CanClearTextBackground));
            Redraw();
        }

        public ObservableCollection<string> BarNames { get; } = new();

        private int _barIndex;
        public int BarIndex
        {
            get => _barIndex;
            set { if (value < 0 || value == _barIndex) return; _barIndex = value; Raise(); LoadBar(); }
        }

        public BattleGaugeComposer.Kind Kind => (BattleGaugeComposer.Kind)_barIndex;
        private BattleGaugePieces.Bar Bar => BattleGaugeComposer.BarOf(Kind);

        public int LayoutIndex => BattleObjects.Find(BattleGaugeComposer.GraphicOf(Kind), "As it appears");

        private byte[] Overlay => _pending.Read(OverlayTarget);

        private BattleGaugeLayout Layout
        {
            get
            {
                if (_drafts.TryGetValue(Bar, out BattleGaugeLayout draft)) return draft;
                byte[] overlay = Overlay;
                if (overlay == null) return null;
                using (BattleGaugePieces.StagedOverlay(overlay)) return _drafts[Bar] = BattleGaugeLayout.Read(Bar);
            }
        }

        public ObservableCollection<HpBarPartRow> Parts { get; } = new();

        private void LoadBar()
        {
            Parts.Clear();
            BattleGaugeLayout layout = Layout;
            if (layout != null)
            {
                int c = 0;
                foreach (BattleGaugeLayout.Part part in BattleGaugeLayout.PartsOf(Bar))
                {
                    if (!layout.At.TryGetValue(part, out (int X, int Y) at)) continue;
                    (int w, int h) = BattleGaugeLayout.SizeOf(part);
                    Parts.Add(new HpBarPartRow
                    {
                        Part = part, W = w, H = h, X = at.X, Y = at.Y, Colour = Colours[c++ % Colours.Length],
                        MoveTo = (row, x, y) => { Move(row, x, y); Commit(); },
                    });
                }
            }
            Recheck();
            Redraw();
            foreach (string n in new[] { nameof(ScreenX), nameof(ScreenY), nameof(LayoutIndex) }) Raise(n);
        }

        private void Recheck()
        {
            BattleGaugeLayout layout = Layout;
            if (layout == null) return;
            (Func<int, bool> empty, Func<int, bool> plain) = TileLooks();
            HashSet<BattleGaugeLayout.Part> clashes = layout.Clashes(empty, plain, SavedLayout());
            foreach (HpBarPartRow row in Parts)
            {
                row.Problem = layout.Problem(row.Part);
                row.Clashes = clashes.Contains(row.Part);
            }
            Problems = string.Join("\n", Parts.Where(p => p.HasProblem).Select(p => p.Problem).Distinct());
            Raise(nameof(CanSave));
            Raise(nameof(SaveHelp));
        }

        // The bar as the ROM has it, whose uncovered art the parts are checked against.
        private BattleGaugeLayout SavedLayout()
        {
            try { return BattleGaugeLayout.Read(Bar); }
            catch (Exception ex) { AppLogger.WarnOnce("HpBarLayoutEditorViewModel.SavedLayout", ex); return null; }
        }

        // Tiles of the bar's drawing that are empty, and ones that are one colour all over.
        private (Func<int, bool> Empty, Func<int, bool> Plain) TileLooks()
        {
            try
            {
                int drawing = BattleObjects.Find(BattleGaugeComposer.GraphicOf(Kind), "Drawing");
                byte[] ncgr;
                using (_pending.Reading())
                    ncgr = drawing >= 0 ? GraphicAssets.Unsqueeze(new ScriptNarc(RomInfo.DirNames.BattleObj).Get(drawing)) : null;
                if (ncgr == null) return (null, null);
                int tilesAt = NitroBgCodec.ReadTileHeader(ncgr).TilesAt;
                ReadOnlySpan<byte> Tile(int tile)
                {
                    int at = tilesAt + tile * BattleGaugePieces.TileBytes;
                    return at >= 0 && at + BattleGaugePieces.TileBytes <= ncgr.Length ? ncgr.AsSpan(at, BattleGaugePieces.TileBytes) : default;
                }
                // Two pixels to a byte, so a one-colour tile repeats one byte whose halves match.
                bool Plain(int tile)
                {
                    ReadOnlySpan<byte> t = Tile(tile);
                    return t.Length > 0 && (t[0] >> 4) == (t[0] & 0xF) && t.IndexOfAnyExcept(t[0]) < 0;
                }
                return (tile => Tile(tile) is { Length: > 0 } t && t.IndexOfAnyExcept((byte)0) < 0, Plain);
            }
            catch (Exception ex) { AppLogger.WarnOnce("HpBarLayoutEditorViewModel.TileLooks", ex); return (null, null); }
        }

        private string _problems = "";
        public string Problems { get => _problems; private set { _problems = value; Raise(); Raise(nameof(HasProblems)); } }
        public bool HasProblems => !string.IsNullOrEmpty(_problems);

        /// <summary>Moves a part while it is dragged; the change only becomes a step when <see cref="Commit"/> runs.</summary>
        public void Move(HpBarPartRow row, int x, int y)
        {
            BattleGaugeLayout layout = Layout;
            if (layout == null || row == null) return;
            x = Math.Clamp(x, 0, BattleGaugeLayout.Columns - row.W);
            y = Math.Clamp(y, 0, BattleGaugeLayout.Rows - row.H);
            if (row.X == x && row.Y == y) return;
            row.X = x;
            row.Y = y;
            layout.At[row.Part] = (x, y);
            Recheck();
            Redraw();
        }

        /// <summary>Makes the layout as it stands one undo step, when nothing about it breaks the game's rules.</summary>
        public void Commit()
        {
            BattleGaugeLayout layout = Layout;
            if (layout == null || layout.Problems().Any()) return;
            byte[] copy = (byte[])Overlay.Clone();
            string trouble = layout.Write(copy);
            if (trouble != null) { StatusText = trouble; return; }
            if (copy.AsSpan().SequenceEqual(Overlay)) return;
            _pending.Import(() => { _pending.Stage(OverlayTarget, copy); return null; });
            StatusText = "Moved.";
        }

        public int ScreenX
        {
            get => Layout?.ScreenX ?? 0;
            set { if (Layout == null || Layout.ScreenX == value) return; Layout.ScreenX = Math.Clamp(value, -64, 320); Raise(); Commit(); }
        }

        public int ScreenY
        {
            get => Layout?.ScreenY ?? 0;
            set { if (Layout == null || Layout.ScreenY == value) return; Layout.ScreenY = Math.Clamp(value, -32, 224); Raise(); Commit(); }
        }

        private Bitmap _barPicture;
        public Bitmap BarPicture { get => _barPicture; private set { _barPicture = value; Raise(); } }

        public ObservableCollection<HpBarShot> Shots { get; } = new();

        private void Redraw()
        {
            byte[] overlay = Overlay;
            if (overlay == null) return;
            GraphicAssets.Archive archive = GraphicAssets.All.FirstOrDefault(a => a.Dir == RomInfo.DirNames.BattleObj);
            using (_pending.Reading())
            using (BattleGaugePieces.StagedOverlay(DraftOverlay(overlay)))
            {
                try
                {
                    GraphicAssets.Preview bar = archive == null ? null : GraphicAssets.Render(archive, LayoutIndex);
                    BarPicture = bar?.Rgba == null ? null : ImageConverter.FromRgba(bar.Rgba, bar.Width, bar.Height);

                    BattleGaugeTextRenderer.Reset();
                    bool doubles = Kind is not (BattleGaugeComposer.Kind.PlayerSingle or BattleGaugeComposer.Kind.OpponentSingle);
                    bool numbers = Kind is BattleGaugeComposer.Kind.PlayerNear or BattleGaugeComposer.Kind.PlayerFar;
                    Shots.Clear();
                    foreach (bool asNumbers in numbers ? new[] { false, true } : new[] { false })
                    {
                        List<BattleScreenRenderer.Piece> pieces = new BattleScreenRenderer().Build(new BattleScreenRenderer.Options
                        {
                            TerrainId = 2, PokemonName = SampleName, Level = 42, Gender = BattleGaugeText.Gender.Male, HealthLeft = 0.45,
                            Doubles = doubles, NumbersInsteadOfBar = asNumbers,
                        });
                        byte[] top = BattleScreenRenderer.Flatten(pieces, touch: false);
                        Shots.Add(new HpBarShot
                        {
                            Label = asNumbers ? "After Start" : "In battle",
                            Picture = ImageConverter.FromRgba(top, BattleScreenRenderer.ScreenWidth, BattleScreenRenderer.ScreenHeight),
                        });
                    }
                }
                catch (Exception ex) { StatusText = "The preview could not be drawn: " + ex.Message; }
                finally { BattleGaugeTextRenderer.Reset(); }
            }
        }

        /// <summary>The working overlay with the current bar's draft written in, when the draft is valid, for the preview.</summary>
        private byte[] DraftOverlay(byte[] overlay)
        {
            BattleGaugeLayout layout = _drafts.GetValueOrDefault(Bar);
            if (layout == null || layout.Problems().Any()) return overlay;
            byte[] copy = (byte[])overlay.Clone();
            return layout.Write(copy) == null ? copy : overlay;
        }

        private string _sampleName = "PIDGEY";
        public string SampleName { get => _sampleName; set { _sampleName = value; Raise(); Redraw(); } }

        public string ImportFitted(string path, HpBarFit.Plan plan)
        {
            GraphicAssets.Archive archive = GraphicAssets.All.FirstOrDefault(a => a.Dir == RomInfo.DirNames.BattleObj);
            BattleGaugePieces.Found found = BattleGaugePieces.Locate();
            if (archive == null || found == null) return "The battle graphics could not be read.";
            Commit();
            string trouble = _pending.Import(() =>
            {
                byte[] copy = (byte[])Overlay.Clone();
                using (BattleGaugePieces.StagedOverlay(copy))
                {
                    int bytes = found.TileCount * BattleGaugePieces.TileBytes;
                    return HpBarImport.Apply(archive, LayoutIndex, path, plan,
                        () => copy.AsSpan(found.SheetAt, bytes).ToArray(),
                        sheet => { Array.Copy(sheet, 0, copy, found.SheetAt, bytes); _pending.Stage(OverlayTarget, copy); },
                        out _);
                }
            });
            return trouble;
        }

        /// <summary>The working overlay, for previewing an import against unsaved moves.</summary>
        public IDisposable Staging() => BattleGaugePieces.StagedOverlay(DraftOverlay(Overlay));

        public bool CanSave => HasUnsavedChanges && !HasProblems;
        public string SaveHelp => HasProblems ? "Fix the parts marked in red first" : null;

        private string _status = "";
        public string StatusText { get => _status; set { _status = value; Raise(); } }

        public bool HasUnsavedChanges => _pending.IsDirty;
        public string UnsavedChangesDescription => "HP bar layout";
        public bool CanUndo => _pending.CanUndo;
        public bool CanRedo => _pending.CanRedo;
        public void Undo() => _pending.Undo();
        public void Redo() => _pending.Redo();

        public void SaveChanges()
        {
            _pending.Save();
            BattleGaugeText.Reset();
            BattleGaugeTextRenderer.Reset();
            SaveNotice.Saved(UnsavedChangesDescription);
            StatusText = "Saved.";
        }

        public void DiscardChanges() => _pending.Discard();
    }

    public sealed class HpBarShot
    {
        public string Label { get; init; }
        public Bitmap Picture { get; init; }
    }
}

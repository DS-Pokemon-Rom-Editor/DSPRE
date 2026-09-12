using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using DSPRE.Avalonia.Data;
using DSPRE.Editors;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    /// <summary>One frame of a sequence, as a row that can be edited.</summary>
    public sealed class CellFrameRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        internal Action<CellFrameRow> Changed;

        public int Number { get; init; }
        public string Title => "Frame " + (Number + 1);

        private int _cell;
        public int Cell
        {
            get => _cell;
            set { if (_cell == value) return; _cell = value; Raise(); Changed?.Invoke(this); }
        }

        private int _delay;
        public int Delay
        {
            get => _delay;
            set { if (_delay == value) return; _delay = Math.Clamp(value, 0, 9999); Raise(); Changed?.Invoke(this); }
        }

        public string SharedNote { get; internal set; }
        public bool IsShared => !string.IsNullOrEmpty(SharedNote);

        internal void Quietly(int cell, int delay)
        {
            _cell = cell; _delay = delay;
            Raise(nameof(Cell)); Raise(nameof(Delay));
            Raise(nameof(SharedNote)); Raise(nameof(IsShared));
        }
    }

    /// <summary>One piece of a drawing, as a row whose position can be edited.</summary>
    public sealed class CellPieceRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        internal Action<CellPieceRow> Changed;

        public int Number { get; init; }
        public string Size { get; init; } = "";
        public string Title => $"Piece {Number + 1}  ({Size})";

        private int _x;
        public int Across
        {
            get => _x;
            set { if (_x == value) return; _x = value; Raise(); Changed?.Invoke(this); }
        }

        private int _y;
        public int Down
        {
            get => _y;
            set { if (_y == value) return; _y = value; Raise(); Changed?.Invoke(this); }
        }

        internal void Quietly(int x, int y)
        {
            _x = x; _y = y;
            Raise(nameof(Across)); Raise(nameof(Down));
        }
    }

    /// <summary>One sequence in the file, as a row in the list.</summary>
    public sealed class CellSequenceRow
    {
        public int Number { get; init; }
        public string Name { get; init; }
        public int Frames { get; init; }
        public int Element { get; init; }
        public uint PlayMode { get; init; }

        public string Title => $"{Number}  {(string.IsNullOrWhiteSpace(Name) ? "(no name)" : Name)}";

        public string Detail
        {
            get
            {
                string kind = Element switch
                {
                    1 => "turn and stretch",
                    2 => "shift",
                    _ => "frame order",
                };
                string mode = PlayMode switch
                {
                    2 => "loops",
                    3 => "backwards",
                    4 => "loops backwards",
                    _ => "once",
                };
                return $"{Frames} frame{(Frames == 1 ? "" : "s")}, {kind}, {mode}";
            }
        }
    }

    /// <summary>
    /// Editing a cell animation: which drawing each frame shows and how long it is held, with the sequence
    /// played back at the speed the game plays it.
    ///
    /// Two things about these files shape the whole window. Several frames usually share one stored result,
    /// so changing a drawing is offered as "just this frame" by default. And sequences are reached by
    /// number in the games' own code, so they cannot be added, removed or reordered.
    /// </summary>
    public sealed class CellAnimationEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        private readonly DirNames _dir;
        private readonly int _animation, _cells, _sprites, _palette, _paletteRow, _sharedSheet;
        private NanrFile _file;
        private List<DsBgScreen.Oam[]> _banks = new();
        private byte[] _characters = Array.Empty<byte>();
        private ushort[] _colours = Array.Empty<ushort>();

        private NcerFile _layout;
        private bool _layoutTouched;
        private byte _animationMarker, _layoutMarker;

        // Which piece the last move touched. A number box commits on every keystroke, so typing 40 arrives
        // as 4 then 40; without this, one undo would step back to 4 instead of where the piece started.
        private int _lastMoveCell = -1, _lastMovePiece = -1;

        // Undo carries both files: moving a piece changes the layout, everything else the animation.
        private readonly record struct Step(byte[] Animation, byte[] Layout);
        private readonly Stack<Step> _undo = new();
        private readonly Stack<Step> _redo = new();
        private bool _loading = true, _filling, _fillingPieces;

        public CellAnimationEditorViewModel() { }   // for the designer

        /// <param name="sharedSheet">
        /// A sheet the game loads into sprite memory ahead of this one, or -1. Some screens share a sheet
        /// of figures, and their cells count tiles from where that sheet ends, so without it every tile
        /// number lands in the wrong place.
        /// </param>
        public CellAnimationEditorViewModel(DirNames dir, int animation, int cells, int sprites,
                                            int palette, int paletteRow, string what, int sharedSheet = -1)
        {
            _dir = dir; _animation = animation; _cells = cells; _sprites = sprites;
            _palette = palette; _paletteRow = paletteRow; _sharedSheet = sharedSheet;
            Subject = what;
            Load();
            _loading = false;
            FillSequences();
        }

        /// <summary>What is being edited, for the window's title bar.</summary>
        public string Subject { get; } = "Cell animation";

        public ObservableCollection<CellSequenceRow> Sequences { get; } = new();
        public ObservableCollection<CellFrameRow> Frames { get; } = new();

        private int _sequence = -1;
        public int SelectedSequence
        {
            get => _sequence;
            set { if (Set(ref _sequence, value)) { Stop(); FillFrames(); } }
        }

        private int _jump;
        /// <summary>A file can hold hundreds of sequences, so there has to be a way to go straight to one.</summary>
        public int JumpTo
        {
            get => _jump;
            set
            {
                if (!Set(ref _jump, value)) return;
                if (value >= 0 && value < Sequences.Count) SelectedSequence = value;
            }
        }

        private Bitmap _preview;
        public Bitmap Preview { get => _preview; private set => Set(ref _preview, value); }

        private string _status = "";
        public string StatusText { get => _status; private set => Set(ref _status, value); }

        public string FileNote => _file == null
            ? "This file could not be read."
            : $"{_file.Sequences.Count} sequences, {_file.Sequences.Sum(s => s.Frames.Count)} frames"
              + (_file.HasExtendedData ? ", with an extended block that is kept as it is" : "");

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => Subject;

        private bool _dirty;
        private bool Dirty
        {
            get => _dirty;
            set { if (Set(ref _dirty, value)) OnPropertyChanged(nameof(HasUnsavedChanges)); }
        }

        // ── loading ──────────────────────────────────────────────────────────────────

        private void Load()
        {
            try
            {
                var narc = new ScriptNarc(_dir);
                if (!narc.Available) { StatusText = "This game does not have that archive."; return; }

                // These files are kept squeezed down in the ROM. How each was stored is remembered, so
                // saving puts it back the same way rather than as plain bytes the game cannot read.
                byte[] stored = narc.Get(_animation);
                _animationMarker = GraphicAssets.SqueezeMarker(stored);
                byte[] raw = GraphicAssets.Unsqueeze(stored);

                _file = NanrFile.Read(raw);
                if (_file == null) { StatusText = "That file is not a cell animation."; return; }

                if (_cells >= 0)
                {
                    byte[] held = narc.Get(_cells);
                    _layoutMarker = GraphicAssets.SqueezeMarker(held);
                    byte[] layout = GraphicAssets.Unsqueeze(held);
                    _layout = NcerFile.Read(layout);
                    _banks = DsBgScreen.ReadCells(layout);
                }
                if (_sprites >= 0) _characters = Sheet(narc, _sprites, _sharedSheet);
                if (_palette >= 0)
                {
                    var all = DsBgScreen.ReadColours(NitroBgCodec.Inflate(narc.Get(_palette)));
                    _colours = DsBgScreen.Row(all, Math.Max(0, _paletteRow));
                }
                OnPropertyChanged(nameof(FileNote));
            }
            catch (Exception ex)
            {
                AppLogger.Error("Cell animation load: " + ex.Message);
                StatusText = "That file could not be read. " + ex.Message;
            }
        }

        private void FillSequences()
        {
            Sequences.Clear();
            if (_file == null) return;
            for (int i = 0; i < _file.Sequences.Count; i++)
            {
                var s = _file.Sequences[i];
                Sequences.Add(new CellSequenceRow
                {
                    Number = i, Name = s.Name, Frames = s.Frames.Count,
                    Element = s.ElementType, PlayMode = s.PlayMode,
                });
            }
            if (Sequences.Count > 0) SelectedSequence = 0;
        }

        private void FillFrames()
        {
            if (_file == null) return;
            _filling = true;
            foreach (var row in Frames) row.Changed = null;
            Frames.Clear();

            if (_sequence >= 0 && _sequence < _file.Sequences.Count)
            {
                var s = _file.Sequences[_sequence];
                for (int i = 0; i < s.Frames.Count; i++)
                {
                    int shared = _file.SharedWith(_sequence, i);
                    var row = new CellFrameRow
                    {
                        Number = i,
                        SharedNote = shared > 0
                            ? $"This drawing is shared with {shared} other frame{(shared == 1 ? "" : "s")}"
                            : null,
                    };
                    row.Quietly(_file.CellOf(_sequence, i), s.Frames[i].Delay);
                    row.Changed = FrameEdited;
                    Frames.Add(row);
                }
            }
            _filling = false;
            _frame = 0;

            // The pieces panel opens on whatever drawing this sequence starts with.
            _pieceCell = Frames.Count > 0 ? Frames[0].Cell : 0;
            OnPropertyChanged(nameof(PieceCell));
            OnPropertyChanged(nameof(PieceHeading));
            FillPieces();
            Draw();
        }

        // ── editing ──────────────────────────────────────────────────────────────────

        private bool _everywhere;
        /// <summary>Whether changing a shared drawing changes it for every frame that shares it.</summary>
        public bool ChangeEverywhere { get => _everywhere; set => Set(ref _everywhere, value); }

        private void FrameEdited(CellFrameRow row)
        {
            if (_loading || _filling || _file == null) return;

            // A drawing number past the end of the layout is a sprite the game cannot find, so it is
            // refused with the count rather than written and discovered later.
            if (row.Cell >= _banks.Count && _banks.Count > 0)
            {
                StatusText = $"This layout holds {_banks.Count} drawings, numbered 0 to {_banks.Count - 1}.";
                _filling = true;
                row.Quietly(_file.CellOf(_sequence, row.Number),
                            _file.Sequences[_sequence].Frames[row.Number].Delay);
                _filling = false;
                return;
            }

            Remember();
            _lastMoveCell = -1;
            _lastMovePiece = -1;

            _file.SetDelay(_sequence, row.Number, row.Delay);
            if (row.Cell != _file.CellOf(_sequence, row.Number))
                _file.SetCell(_sequence, row.Number, row.Cell, _everywhere);

            Dirty = true;
            RefreshSharing();
            Draw();
            StatusText = _everywhere ? "Changed for every frame that shares it." : "Changed for this frame.";
        }

        // Splitting a shared drawing changes how many frames share it, so the notes have to follow.
        private void RefreshSharing()
        {
            if (_file == null) return;
            _filling = true;
            for (int i = 0; i < Frames.Count; i++)
            {
                int shared = _file.SharedWith(_sequence, i);
                Frames[i].SharedNote = shared > 0
                    ? $"This drawing is shared with {shared} other frame{(shared == 1 ? "" : "s")}"
                    : null;
                Frames[i].Quietly(_file.CellOf(_sequence, i), _file.Sequences[_sequence].Frames[i].Delay);
            }
            _filling = false;
        }

        // ── moving the pieces of a drawing ───────────────────────────────────────────

        public ObservableCollection<CellPieceRow> Pieces { get; } = new();

        private int _pieceCell;
        /// <summary>Which drawing's pieces the panel is moving.</summary>
        public int PieceCell
        {
            get => _pieceCell;
            set
            {
                if (!Set(ref _pieceCell, value)) return;
                OnPropertyChanged(nameof(PieceHeading));
                FillPieces();
            }
        }

        public bool CanMovePieces => _layout != null && _layout.Cells.Count > 0;

        public string PieceHeading
        {
            get
            {
                if (_layout == null) return "Pieces";
                var cell = _pieceCell >= 0 && _pieceCell < _layout.Cells.Count ? _layout.Cells[_pieceCell] : null;
                int n = cell?.Pieces.Count ?? 0;
                return $"Drawing {_pieceCell}: {n} piece{(n == 1 ? "" : "s")}";
            }
        }

        private void FillPieces()
        {
            _fillingPieces = true;
            foreach (var row in Pieces) row.Changed = null;
            Pieces.Clear();

            var cell = _layout != null && _pieceCell >= 0 && _pieceCell < _layout.Cells.Count
                     ? _layout.Cells[_pieceCell] : null;
            if (cell != null)
                for (int i = 0; i < cell.Pieces.Count; i++)
                {
                    var p = cell.Pieces[i];
                    var row = new CellPieceRow { Number = i, Size = $"{p.Width}x{p.Height}" };
                    row.Quietly(p.X, p.Y);
                    row.Changed = PieceMoved;
                    Pieces.Add(row);
                }

            _fillingPieces = false;
            _lastMoveCell = -1;
            _lastMovePiece = -1;
            OnPropertyChanged(nameof(CanMovePieces));
            OnPropertyChanged(nameof(PieceHeading));
        }

        private void PieceMoved(CellPieceRow row)
        {
            if (_loading || _fillingPieces || _layout == null) return;

            // The snapshot is taken before the move but only kept if the move happened, so a refused one
            // leaves nothing on the undo stack.
            var before = Now();
            string trouble = _layout.Move(_pieceCell, row.Number, row.Across, row.Down);
            if (trouble != null)
            {
                StatusText = trouble;
                var p = _layout.PieceAt(_pieceCell, row.Number);
                if (p != null)
                {
                    _fillingPieces = true;
                    row.Quietly(p.X, p.Y);
                    _fillingPieces = false;
                }
                return;
            }

            // Keystrokes on one piece collapse into a single step; a move of anything else starts a new one.
            bool sameAsLast = _lastMoveCell == _pieceCell && _lastMovePiece == row.Number && _undo.Count > 0;
            if (!sameAsLast) Remember(before);
            _lastMoveCell = _pieceCell;
            _lastMovePiece = row.Number;
            _layoutTouched = true;

            // The preview draws from the cells as read, so they are read again from the patched bytes.
            _banks = DsBgScreen.ReadCells(_layout.Write());
            Dirty = true;
            Draw();
            OnPropertyChanged(nameof(PieceHeading));
            StatusText = $"Moved piece {row.Number + 1} to {row.Across}, {row.Down}.";
        }

        // ── undo ─────────────────────────────────────────────────────────────────────

        private Step Now() => new(_file?.Write(), _layout?.Write());

        private void Remember() => Remember(Now());

        private void Remember(Step before)
        {
            if (before.Animation == null && before.Layout == null) return;
            _undo.Push(before);
            _redo.Clear();
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }

        public void Undo() => StepAcross(_undo, _redo, "Put back.");
        public void Redo() => StepAcross(_redo, _undo, "Done again.");

        private void StepAcross(Stack<Step> from, Stack<Step> to, string said)
        {
            if (from.Count == 0) return;
            to.Push(Now());
            var step = from.Pop();

            if (step.Animation != null) _file = NanrFile.Read(step.Animation);
            if (step.Layout != null)
            {
                _layout = NcerFile.Read(step.Layout);
                _banks = DsBgScreen.ReadCells(step.Layout);
            }

            Dirty = true;
            FillFrames();
            StatusText = said;
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(FileNote));
        }

        /// <summary>Writes the file back. The ROM save packs it.</summary>
        public void SaveChanges()
        {
            if (_file == null) return;
            try
            {
                byte[] animation = Ready(_file.Write(), _animationMarker, out string trouble);
                if (trouble != null) { StatusText = trouble; return; }

                byte[] positions = null;
                if (_layoutTouched && _layout != null && _cells >= 0)
                {
                    positions = Ready(_layout.Write(), _layoutMarker, out trouble);
                    if (trouble != null) { StatusText = trouble; return; }
                }

                var narc = new ScriptNarc(_dir);
                narc.Put(_animation, animation);
                if (positions != null) narc.Put(_cells, positions);

                Dirty = false;
                StatusText = "Saved.";
            }
            catch (Exception ex)
            {
                AppLogger.Error("Cell animation save: " + ex.Message);
                StatusText = "That could not be saved. " + ex.Message;
            }
        }

        /// <summary>
        /// A file that was squeezed down goes back squeezed, checked the same way the graphics painter
        /// checks its own. Both files are made ready before either is written, so a refusal cannot leave
        /// one half of an edit saved.
        /// </summary>
        private static byte[] Ready(byte[] plain, byte marker, out string trouble)
        {
            trouble = null;
            if (marker == 0x11)
            {
                trouble = "This file is squeezed down in a way DSPRE cannot put back yet, so nothing "
                        + "was saved.";
                return null;
            }
            if (marker == 0) return plain;

            byte[] packed = GraphicAssets.Squeeze(plain, marker);
            if (packed == null)
                trouble = "This file could not be squeezed back down, so nothing was saved.";
            return packed;
        }

        public void DiscardChanges()
        {
            Stop();
            _undo.Clear();
            _redo.Clear();
            _layoutTouched = false;
            Load();
            Dirty = false;
            FillSequences();
        }

        // ── playing ──────────────────────────────────────────────────────────────────

        private DispatcherTimer _timer;
        private readonly Stopwatch _clock = new();
        private int _frame, _ticksRun, _held;

        public bool Playing => _timer != null;
        public string PlayLabel => Playing ? "Stop" : "Play";

        public void TogglePlay()
        {
            if (Playing) Stop(); else Play();
        }

        private void Play()
        {
            if (_file == null || Frames.Count == 0) return;
            _frame = 0; _held = 0; _ticksRun = 0;
            _clock.Restart();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(5) };
            _timer.Tick += Beat;
            _timer.Start();
            OnPropertyChanged(nameof(Playing));
            OnPropertyChanged(nameof(PlayLabel));
        }

        public void Stop()
        {
            if (_timer == null) return;
            _timer.Stop();
            _timer.Tick -= Beat;
            _timer = null;
            _clock.Stop();
            OnPropertyChanged(nameof(Playing));
            OnPropertyChanged(nameof(PlayLabel));
        }

        // The games run these at sixty ticks a second. A hitch should not fast-forward the animation, so
        // the clock is clamped rather than allowed to run up a debt.
        private void Beat(object sender, EventArgs e)
        {
            if (_file == null || Frames.Count == 0) { Stop(); return; }

            int due = (int)(_clock.Elapsed.TotalSeconds * 60);
            if (due - _ticksRun > 4) _ticksRun = due - 1;

            bool moved = false;
            while (_ticksRun < due)
            {
                _ticksRun++;
                _held++;
                int hold = Math.Max(1, Frames[_frame].Delay);
                if (_held < hold) continue;

                _held = 0;
                var s = _file.Sequences[_sequence];
                bool loops = s.PlayMode == 2 || s.PlayMode == 4;
                _frame++;
                if (_frame >= Frames.Count)
                {
                    if (loops) _frame = 0;
                    else { _frame = Frames.Count - 1; Stop(); }
                }
                moved = true;
            }
            if (moved) Draw();
        }

        /// <summary>Which frame is showing, so the strip can mark it.</summary>
        public int PlayingFrame => _frame;

        // ── drawing ──────────────────────────────────────────────────────────────────

        private void Draw()
        {
            if (_file == null || _sequence < 0 || Frames.Count == 0) { Preview = null; return; }
            try
            {
                var rgba = new byte[DsBgScreen.Width * DsBgScreen.Height * 4];
                int at = Math.Clamp(_frame, 0, Frames.Count - 1);
                int cell = Frames[at].Cell;
                // Several animations hold one drawing and move it, so the frame's own shift has to be
                // applied or they look frozen.
                var (sx, sy) = _file.ShiftOf(_sequence, at);
                var (degrees, scaleX, scaleY) = _file.TurnOf(_sequence, at);
                if (_banks.Count > 0 && _characters.Length > 0 && cell >= 0 && cell < _banks.Count)
                    DsBgScreen.DrawCellTurned(rgba, _banks[cell], _characters, _ => _colours,
                                              DsBgScreen.Width / 2 + sx, DsBgScreen.Height / 2 + sy,
                                              degrees, scaleX, scaleY);

                var wb = new WriteableBitmap(new global::Avalonia.PixelSize(DsBgScreen.Width, DsBgScreen.Height),
                                             new global::Avalonia.Vector(96, 96),
                                             PixelFormat.Rgba8888, AlphaFormat.Unpremul);
                using (var fb = wb.Lock())
                    System.Runtime.InteropServices.Marshal.Copy(rgba, 0, fb.Address, rgba.Length);
                Preview = wb;
                OnPropertyChanged(nameof(PlayingFrame));
            }
            catch (Exception ex)
            {
                AppLogger.Error("Cell animation draw: " + ex.Message);
            }
        }

        /// <summary>How many cell banks the paired layout holds, which is the limit on a drawing number.</summary>
        public int CellCount => _banks.Count;

        // Sprite memory holds the shared sheet first, then this screen's own, and the cells count tiles
        // across both. Laying them out the same way here is what puts the tiles back where the cells expect.
        private static byte[] Sheet(ScriptNarc narc, int own, int shared)
        {
            byte[] mine = DsBgScreen.ReadCharacters(NitroBgCodec.Inflate(narc.Get(own)));
            if (shared < 0) return mine;

            byte[] first = DsBgScreen.ReadCharacters(NitroBgCodec.Inflate(narc.Get(shared)));
            if (first.Length == 0) return mine;

            var both = new byte[first.Length + mine.Length];
            first.CopyTo(both, 0);
            mine.CopyTo(both, first.Length);
            return both;
        }
    }
}

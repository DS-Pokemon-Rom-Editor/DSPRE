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

        /// <summary>How the sequence stores its frames, which decides what this frame can carry.</summary>
        public int Element { get; init; }
        public bool CanTurn => Element == 1;
        public bool CanShift => Element != 0;

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

        private double _turn;
        public double Turn
        {
            get => _turn;
            set { if (_turn == value) return; _turn = value; Raise(); Changed?.Invoke(this); }
        }

        private double _stretchAcross = 1, _stretchDown = 1;
        public double StretchAcross
        {
            get => _stretchAcross;
            set { if (_stretchAcross == value) return; _stretchAcross = value; Raise(); Changed?.Invoke(this); }
        }
        public double StretchDown
        {
            get => _stretchDown;
            set { if (_stretchDown == value) return; _stretchDown = value; Raise(); Changed?.Invoke(this); }
        }

        private int _shiftAcross, _shiftDown;
        public int ShiftAcross
        {
            get => _shiftAcross;
            set { if (_shiftAcross == value) return; _shiftAcross = value; Raise(); Changed?.Invoke(this); }
        }
        public int ShiftDown
        {
            get => _shiftDown;
            set { if (_shiftDown == value) return; _shiftDown = value; Raise(); Changed?.Invoke(this); }
        }

        public string SharedNote { get; internal set; }
        public bool IsShared => !string.IsNullOrEmpty(SharedNote);

        internal void Quietly(int cell, int delay, double turn, double stretchAcross, double stretchDown,
                              int shiftAcross, int shiftDown)
        {
            _cell = cell; _delay = delay;
            _turn = turn; _stretchAcross = stretchAcross; _stretchDown = stretchDown;
            _shiftAcross = shiftAcross; _shiftDown = shiftDown;
            Raise(nameof(Cell)); Raise(nameof(Delay)); Raise(nameof(Turn));
            Raise(nameof(StretchAcross)); Raise(nameof(StretchDown));
            Raise(nameof(ShiftAcross)); Raise(nameof(ShiftDown));
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
    public sealed class CellSequenceRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public int Number { get; init; }
        public string Name { get; init; }
        public int Element { get; init; }

        private int _frames;
        public int Frames { get => _frames; init => _frames = value; }

        private uint _playMode;
        public uint PlayMode { get => _playMode; init => _playMode = value; }

        // Editing a sequence changes what this row says about it. The row is updated in place rather than
        // replaced, since replacing the selected item would knock the selection off it.
        internal void Freshen(int frames, uint playMode)
        {
            _frames = frames; _playMode = playMode;
            Raise(nameof(Frames)); Raise(nameof(PlayMode)); Raise(nameof(Detail));
        }

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

        private readonly ArchiveFiles _source;
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

        // The same, for the frame whose numbers were last typed into.
        private int _lastEditFrame = -1;

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
        /// <param name="poketchApp">
        /// The Pokétch application this animation belongs to, or -1. Given one, the sprite is drawn inside
        /// that application's casing and screen rather than on an empty background, and the run is stepped
        /// at the speed the Pokétch steps it.
        /// </param>
        public CellAnimationEditorViewModel(DirNames dir, int animation, int cells, int sprites,
                                            int palette, int paletteRow, string what, int sharedSheet = -1,
                                            int poketchApp = -1)
            : this(ArchiveFiles.Mapped(dir), animation, cells, sprites, palette, paletteRow, what, sharedSheet, poketchApp)
        {
        }

        public CellAnimationEditorViewModel(ArchiveFiles source, int animation, int cells, int sprites,
                                            int palette, int paletteRow, string what, int sharedSheet = -1,
                                            int poketchApp = -1)
        {
            _source = source; _animation = animation; _cells = cells; _sprites = sprites;
            _palette = palette; _paletteRow = paletteRow; _sharedSheet = sharedSheet;
            Subject = what;

            _poketchApp = poketchApp;
            if (poketchApp >= 0) TicksPerFrame = 2;

            Load();
            _loading = false;
            FillSequences();
        }

        private readonly int _poketchApp = -1;
        private PoketchScreen _casing;
        private PoketchApps.App _app;

        /// <summary>Whether this animation came from a Pokétch application, which it is then drawn inside.</summary>
        public bool InPoketch => _app != null && _casing != null;

        /// <summary>The application this animation belongs to, for the way back to it.</summary>
        public string PoketchAppName => _app?.Name;

        private bool _inCasing = true;
        /// <summary>
        /// Whether to draw the sprite inside the Pokétch or on its own. On is how a player sees it; off is
        /// how to see a frame that the casing would cover.
        /// </summary>
        public bool InCasing
        {
            get => _inCasing;
            set { if (Set(ref _inCasing, value)) Draw(); }
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
                var narc = _source;
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

                // Opened from a Pokétch application, the preview is drawn inside that application's own
                // screen, so a frame is seen where a player would see it rather than floating on nothing.
                if (_poketchApp >= 0)
                {
                    _app = PoketchApps.All.FirstOrDefault(a => a.Id == _poketchApp);
                    _casing = PoketchScreen.Load();
                }

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
                    var row = new CellFrameRow
                    {
                        Number = i,
                        Element = s.ElementType,
                        SharedNote = ShareNote(i),
                    };
                    Fill(row);
                    row.Changed = FrameEdited;
                    Frames.Add(row);
                }

                // The sequence's own settings, put on screen without writing them back.
                _playMode = (int)Math.Clamp(s.PlayMode, 1, 4) - 1;
                _loopStart = Math.Clamp(s.LoopStartFrame + 1, 1, Math.Max(1, s.Frames.Count));
                _animationType = Math.Clamp((int)s.AnimationType, 1, 2) - 1;
            }
            _filling = false;
            _lastEditFrame = -1;
            _frame = 0; _shown = 0; _reverse = false;
            OnPropertyChanged(nameof(PlayModeIndex));
            OnPropertyChanged(nameof(CanLoop));
            OnPropertyChanged(nameof(LoopStart));
            OnPropertyChanged(nameof(FrameCount));
            OnPropertyChanged(nameof(AnimationTypeIndex));
            OnPropertyChanged(nameof(AnimationTypeWarning));
            OnPropertyChanged(nameof(HasAnimationTypeWarning));

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
                Restore(row);
                return;
            }

            // A number box commits on every keystroke, so keystrokes on one frame collapse into one step.
            bool sameAsLast = _lastEditFrame == row.Number && _undo.Count > 0;
            var before = Now();

            _file.SetDelay(_sequence, row.Number, row.Delay);
            if (row.Cell != _file.CellOf(_sequence, row.Number))
                _file.SetCell(_sequence, row.Number, row.Cell, _everywhere);

            string trouble = null;
            if (row.CanTurn)
            {
                var (turn, across, down) = _file.TurnOf(_sequence, row.Number);
                if (Differs(turn, row.Turn) || Differs(across, row.StretchAcross)
                                            || Differs(down, row.StretchDown))
                    trouble = _file.SetTurn(_sequence, row.Number, row.Turn,
                                            row.StretchAcross, row.StretchDown, _everywhere);
            }
            if (trouble == null && row.CanShift)
            {
                var (across, down) = _file.ShiftOf(_sequence, row.Number);
                if (across != row.ShiftAcross || down != row.ShiftDown)
                    trouble = _file.SetShift(_sequence, row.Number, row.ShiftAcross, row.ShiftDown,
                                             _everywhere);
            }
            if (trouble != null)
            {
                StatusText = trouble;
                Restore(row);
                return;
            }

            if (!sameAsLast) Remember(before);
            _lastEditFrame = row.Number;
            _lastMoveCell = -1;
            _lastMovePiece = -1;

            Dirty = true;
            RefreshSharing();
            Draw();
            StatusText = _everywhere ? "Changed for every frame that shares it." : "Changed for this frame.";
        }

        // A turn read back out of the file has been through the hardware's own units, so it never lands on
        // exactly what was typed. Only a real difference counts as an edit.
        private static bool Differs(double a, double b) => Math.Abs(a - b) > 0.01;

        // Everything a row shows, taken from the file.
        private void Fill(CellFrameRow row)
        {
            var (across, down) = _file.ShiftOf(_sequence, row.Number);
            var (turn, stretchAcross, stretchDown) = _file.TurnOf(_sequence, row.Number);
            row.Quietly(_file.CellOf(_sequence, row.Number),
                        _file.Sequences[_sequence].Frames[row.Number].Delay,
                        turn, stretchAcross, stretchDown, across, down);
        }

        private void Restore(CellFrameRow row)
        {
            _filling = true;
            Fill(row);
            _filling = false;
        }

        // What a frame shares with the others pointing at the same stored result.
        private string ShareNote(int frame)
        {
            int shared = _file.SharedWith(_sequence, frame);
            if (shared <= 0) return null;
            string what = _file.Sequences[_sequence].ElementType switch
            {
                1 => "drawing, turn and position",
                2 => "drawing and position",
                _ => "drawing",
            };
            return $"This {what} is shared with {shared} other frame{(shared == 1 ? "" : "s")}";
        }

        // Splitting a shared result changes how many frames share it, so the notes have to follow.
        private void RefreshSharing()
        {
            if (_file == null) return;
            _filling = true;
            for (int i = 0; i < Frames.Count; i++)
            {
                Frames[i].SharedNote = ShareNote(i);
                Fill(Frames[i]);
            }
            _filling = false;
        }

        // ── the sequence itself ──────────────────────────────────────────────────────

        /// <summary>The four ways the hardware can run a sequence.</summary>
        public IReadOnlyList<string> PlayModes { get; } =
            new[] { "Once", "Loops", "Backwards", "Loops backwards" };

        private int _playMode;
        public int PlayModeIndex
        {
            get => _playMode;
            set
            {
                if (!Set(ref _playMode, value)) return;
                OnPropertyChanged(nameof(CanLoop));
                if (_loading || _filling || _file == null || value < 0 || value >= PlayModes.Count) return;

                Stop();
                Remember();
                _file.SetPlayMode(_sequence, value + 1);
                Dirty = true;
                if (_sequence >= 0 && _sequence < Sequences.Count)
                    Sequences[_sequence].Freshen(Frames.Count, _file.Sequences[_sequence].PlayMode);
                StatusText = value switch
                {
                    1 => "This sequence now loops.",
                    2 => "This sequence now plays backwards.",
                    3 => "This sequence now loops backwards.",
                    _ => "This sequence now plays once.",
                };
            }
        }

        /// <summary>Anything but running once has a frame it comes back to.</summary>
        public bool CanLoop => _playMode > 0;

        private int _loopStart = 1;
        public int LoopStart
        {
            get => _loopStart;
            set
            {
                if (!Set(ref _loopStart, value)) return;
                if (_loading || _filling || _file == null) return;

                Stop();
                Remember();
                _file.SetLoopStart(_sequence, value - 1);
                Dirty = true;
                StatusText = $"It now comes back to frame {value}.";
            }
        }

        /// <summary>How many frames this sequence has, which bounds the frame it comes back to.</summary>
        public int FrameCount => Frames.Count;

        /// <summary>What a sequence's frames index.</summary>
        public IReadOnlyList<string> AnimationTypes { get; } = new[] { "Cell banks", "Multi-cell banks" };

        private int _animationType;
        public int AnimationTypeIndex
        {
            get => _animationType;
            set
            {
                if (!Set(ref _animationType, value)) return;
                OnPropertyChanged(nameof(AnimationTypeWarning));
                OnPropertyChanged(nameof(HasAnimationTypeWarning));
                if (_loading || _filling || _file == null || value < 0 || value >= AnimationTypes.Count) return;

                Stop();
                Remember();
                string trouble = _file.SetAnimationType(_sequence, value + 1);
                if (trouble != null) { StatusText = trouble; return; }
                Dirty = true;
                StatusText = value == 1
                    ? "This sequence now draws from multi-cell banks."
                    : "This sequence now draws from cell banks.";
            }
        }

        public bool HasAnimationTypeWarning => _animationType == 1;

        /// <summary>Said out loud because a sequence set this way draws nothing without multi-cell data.</summary>
        public string AnimationTypeWarning => _animationType != 1 ? null
            : "Everything in these games indexes cell banks. Set this way, the sequence only draws if the "
            + "game has multi-cell data for it.";

        /// <summary>
        /// Adds a sequence on the end, copied from the last one. Only the end, because the games reach a
        /// sequence by its number and one put in the middle would renumber the rest.
        /// </summary>
        public void AddSequence()
        {
            if (_file == null) return;
            Stop();
            var before = Now();
            string trouble = _file.AddSequence();
            if (trouble != null) { StatusText = trouble; return; }

            Remember(before);
            Dirty = true;
            FillSequences();
            SelectedSequence = _file.Sequences.Count - 1;
            OnPropertyChanged(nameof(FileNote));
            StatusText = $"Added sequence {_file.Sequences.Count - 1} on the end.";
        }

        /// <summary>Takes the last sequence off.</summary>
        public void RemoveLastSequence()
        {
            if (_file == null) return;
            Stop();
            var before = Now();
            string trouble = _file.RemoveLastSequence();
            if (trouble != null) { StatusText = trouble; return; }

            Remember(before);
            Dirty = true;
            FillSequences();
            OnPropertyChanged(nameof(FileNote));
            StatusText = "Took the last sequence off.";
        }

        /// <summary>Copies a frame and puts the copy straight after it.</summary>
        public void AddFrameAfter(int number)
        {
            if (_file == null || _sequence < 0) return;
            Stop();
            var before = Now();
            string trouble = _file.AddFrame(_sequence, number);
            if (trouble != null) { StatusText = trouble; return; }

            Remember(before);
            Dirty = true;
            FillFrames();
            if (_sequence < Sequences.Count)
                Sequences[_sequence].Freshen(Frames.Count, _file.Sequences[_sequence].PlayMode);
            OnPropertyChanged(nameof(FileNote));
            StatusText = $"Added a frame after frame {number + 1}.";
        }

        /// <summary>Takes a frame out of the sequence.</summary>
        public void RemoveFrameAt(int number)
        {
            if (_file == null || _sequence < 0) return;
            Stop();
            var before = Now();
            string trouble = _file.RemoveFrame(_sequence, number);
            if (trouble != null) { StatusText = trouble; return; }

            Remember(before);
            Dirty = true;
            FillFrames();
            if (_sequence < Sequences.Count)
                Sequences[_sequence].Freshen(Frames.Count, _file.Sequences[_sequence].PlayMode);
            OnPropertyChanged(nameof(FileNote));
            StatusText = $"Took out frame {number + 1}.";
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
            _lastEditFrame = -1;
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
            RefreshSequenceRows();
            StatusText = said;
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            OnPropertyChanged(nameof(FileNote));
        }

        // Stepping back can reach any sequence, not just the chosen one, so every row is refreshed.
        private void RefreshSequenceRows()
        {
            if (_file == null) return;
            for (int i = 0; i < Sequences.Count && i < _file.Sequences.Count; i++)
                Sequences[i].Freshen(_file.Sequences[i].Frames.Count, _file.Sequences[i].PlayMode);
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

                var files = new Dictionary<int, byte[]> { [_animation] = animation };
                if (positions != null) files[_cells] = positions;
                _source.Put(files);

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
            if (_file == null || Frames.Count == 0 || _sequence < 0 || _sequence >= _file.Sequences.Count) return;

            // A backwards run starts at the last frame and travels down towards the loop start, so starting
            // it at the first frame would play it the wrong way round.
            uint mode = _file.Sequences[_sequence].PlayMode;
            _reverse = mode == 3 || mode == 4;
            _frame = _reverse ? Frames.Count - 1 : 0;
            _shown = _frame;
            _held = 0; _ticksRun = 0;
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

        // Which way a backwards run is currently travelling, and the frame actually on screen. A frame held
        // for no time is stepped through without ever being shown, so the one before it stays up.
        private bool _reverse;
        private int _shown;

        /// <summary>
        /// Runs the sequence the way the hardware does: a zero hold is passed straight over and never
        /// shown, running once stops at the end, looping returns to the loop start, and the backwards
        /// modes turn round at each end. The clock is clamped so a hitch does not fast-forward.
        /// </summary>
        private void Beat(object sender, EventArgs e)
        {
            if (_file == null || Frames.Count == 0 || _sequence < 0 || _sequence >= _file.Sequences.Count)
            { Stop(); return; }

            int due = (int)(_clock.Elapsed.TotalSeconds * 60 * TicksPerFrame);
            if (due - _ticksRun > 4 * TicksPerFrame) _ticksRun = due - 1;

            var s = _file.Sequences[_sequence];
            bool loops = s.PlayMode == 2 || s.PlayMode == 4;
            bool backwards = s.PlayMode == 3 || s.PlayMode == 4;
            int loopStart = Math.Clamp(s.LoopStartFrame, 0, Frames.Count - 1);
            int was = _shown;

            while (_ticksRun < due && Playing)
            {
                _ticksRun++;
                _held++;

                // The step count stops a sequence of nothing but zero holds from spinning here.
                for (int steps = 0; Playing && _held >= Frames[_frame].Delay && steps <= Frames.Count; steps++)
                {
                    _held = 0;
                    _frame += _reverse ? -1 : 1;

                    if (_frame >= Frames.Count || _frame < loopStart)
                    {
                        if (backwards)
                        {
                            // Turn round at each end, and give up only on arriving back at the start.
                            bool atStart = _frame < loopStart;
                            _reverse = !_reverse;
                            _frame = Math.Clamp(_frame, loopStart, Frames.Count - 1);
                            if (atStart && !loops) { Stop(); break; }
                        }
                        else if (loops) _frame = loopStart;
                        else { _frame = Frames.Count - 1; Stop(); break; }
                    }

                    // Only a frame with a hold of its own is ever put on screen.
                    if (Frames[_frame].Delay > 0) { _shown = _frame; break; }
                }
            }
            if (_shown != was) Draw();
        }

        /// <summary>Animation frames per screen refresh. The Pokétch drives its own at two.</summary>
        public int TicksPerFrame { get; set; } = 1;

        /// <summary>Which frame is showing, so the strip can mark it.</summary>
        public int PlayingFrame => _shown;

        // ── drawing ──────────────────────────────────────────────────────────────────

        private void Draw()
        {
            if (_file == null || _sequence < 0 || Frames.Count == 0) { Preview = null; return; }
            try
            {
                int at = Math.Clamp(_shown, 0, Frames.Count - 1);
                int cell = Frames[at].Cell;
                // Several animations hold one drawing and move it, so the frame's own shift has to be
                // applied or they look frozen.
                var (sx, sy) = _file.ShiftOf(_sequence, at);
                var (degrees, scaleX, scaleY) = _file.TurnOf(_sequence, at);

                if (InPoketch && _inCasing)
                {
                    // Seeing the frame is the point of this window, so one with no recorded position is
                    // drawn in the middle and said to be.
                    var slots = _app.SpriteSlots ?? new[] { PoketchScreen.MiddleOfScreen };
                    Preview = ToBitmap(_casing.RenderApp(
                        false, 0, false, _app.Tiles, _app.Arrangement, _app.Sprites, _app.Cells, cell,
                        slots, _app.Fills,
                        new PoketchScreen.Motion(degrees, scaleX, scaleY, sx, sy)));
                    OnPropertyChanged(nameof(PlayingFrame));
                    if (_app.SpriteSlots == null && string.IsNullOrEmpty(StatusText))
                        StatusText = "Where this screen puts its sprite is in the game's code, so the frame "
                                   + "is drawn in the middle.";
                    return;
                }

                var rgba = new byte[DsBgScreen.Width * DsBgScreen.Height * 4];
                if (_banks.Count > 0 && _characters.Length > 0 && cell >= 0 && cell < _banks.Count)
                    DsBgScreen.DrawCellTurned(rgba, _banks[cell], _characters, _ => _colours,
                                              DsBgScreen.Width / 2 + sx, DsBgScreen.Height / 2 + sy,
                                              degrees, scaleX, scaleY);

                Preview = ToBitmap(rgba);
                OnPropertyChanged(nameof(PlayingFrame));
            }
            catch (Exception ex)
            {
                AppLogger.Error("Cell animation draw: " + ex.Message);
            }
        }

        private static Bitmap ToBitmap(byte[] rgba)
        {
            var wb = new WriteableBitmap(new global::Avalonia.PixelSize(DsBgScreen.Width, DsBgScreen.Height),
                                         new global::Avalonia.Vector(96, 96),
                                         PixelFormat.Rgba8888, AlphaFormat.Unpremul);
            using (var fb = wb.Lock())
                System.Runtime.InteropServices.Marshal.Copy(rgba, 0, fb.Address, rgba.Length);
            return wb;
        }

        /// <summary>Opens the Bottom Screen editor on the application this animation belongs to.</summary>
        public void BackToPoketch()
        {
            if (!InPoketch) return;
            Stop();
            AvaloniaEditorLauncher.OpenBottomScreenEditor(_poketchApp);
        }

        /// <summary>How many cell banks the paired layout holds, which is the limit on a drawing number.</summary>
        public int CellCount => _banks.Count;

        // Sprite memory holds the shared sheet first, then this screen's own, and the cells count tiles
        // across both. Laying them out the same way here is what puts the tiles back where the cells expect.
        private static byte[] Sheet(ArchiveFiles narc, int own, int shared)
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

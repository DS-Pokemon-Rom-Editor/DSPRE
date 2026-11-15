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
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    /// <summary>
    /// Editing a cell animation: the drawing and hold of each frame, played back at game speed.
    /// Sequences are reached by number in game code, so they are only added or removed at the end.
    /// </summary>
    public sealed partial class CellAnimationEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
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

        // Typing 40 arrives as 4 then 40. Tracked so one undo goes back to where the piece started.
        private int _lastMoveCell = -1, _lastMovePiece = -1;

        // The same, for the frame whose numbers were last typed into.
        private int _lastEditFrame = -1;

        // Undo carries both files: moving a piece changes the layout, everything else the animation.
        private sealed record Step(byte[] Animation, byte[] Layout);
        private readonly UndoHistory<Step> _history = new UndoHistory<Step>();
        private bool _loading = true, _filling, _fillingPieces;

        public CellAnimationEditorViewModel() { }   // for the designer

        /// <param name="sharedSheet">
        /// A sheet the game loads into sprite memory ahead of this one, or -1. Some screens share a sheet
        /// of figures, and their cells count tiles from where that sheet ends, so without it every tile
        /// number lands in the wrong place.
        /// </param>
        /// <param name="poketchApp">
        /// The Pokétch application this animation belongs to, or -1. Given one, the sprite is drawn inside its
        /// casing and stepped at the Pokétch's speed.
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

        /// <summary>True when opened from a Pokétch application, so it is drawn inside it.</summary>
        public bool InPoketch => _app != null && _casing != null;

        /// <summary>The application this animation belongs to, for the way back to it.</summary>
        public string PoketchAppName => _app?.Name;

        private bool _inCasing = true;
        /// <summary>On draws inside the Pokétch. Off shows frames the casing would cover.</summary>
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
            ? "This file couldn't be read."
            : $"{_file.Sequences.Count} sequences, {_file.Sequences.Sum(s => s.Frames.Count)} frames"
              + (_file.HasExtendedData ? ", with an extended block that is kept as it is" : "");

        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;
        // Compared as bytes, so undoing back to the saved file counts as clean.
        public bool HasUnsavedChanges => _file != null && (!Same(_file.Write(), _savedAnimation) || (_layout != null && !Same(_layout.Write(), _savedLayout)));
        public string UnsavedChangesDescription => Subject;

        private byte[] _savedAnimation, _savedLayout;
        private static bool Same(byte[] a, byte[] b) => a != null && b != null && a.AsSpan().SequenceEqual(b);
        private void MarkSaved() { _savedAnimation = _file?.Write(); _savedLayout = _layout?.Write(); }
        private bool Dirty { set => OnPropertyChanged(nameof(HasUnsavedChanges)); }

        public ArchiveFiles Source => _source;
        public int Animation => _animation;
    }
}

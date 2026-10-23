using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DSPRE.Avalonia.Data;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    public enum SpriteEditTool { Pencil, Eyedropper }

    /// <summary>Class sprites or back sprites.</summary>
    public sealed class TrainerSpriteSet
    {
        public DirNames Archive { get; private init; }
        public bool IsBack => Archive == DirNames.TrainerBackGraphics;
        public int NameDigits { get; private init; }
        public string Title { get; private init; }
        public string Noun { get; private init; }
        public string ItemLabel { get; private init; }
        public Func<int, List<string>> Names { get; private init; }

        /// <summary>The naming screen's icons: one drawing, frames picked per animation, files packed.</summary>
        public bool NamingScreen { get; private init; }

        public static readonly TrainerSpriteSet Classes = new()
        {
            Archive = DirNames.TrainerGraphics, NameDigits = 3,
            Title = "Trainer sprite editor", Noun = "class", ItemLabel = "Class",
            Names = _ => GetTrainerClassNames().ToList(),
        };

        public static readonly TrainerSpriteSet Backs = new()
        {
            Archive = DirNames.TrainerBackGraphics, NameDigits = 2,
            Title = "Trainer back sprite editor", Noun = "back sprite", ItemLabel = "Sprite",
            Names = count => DSPRE.ROMFiles.TrainerBackSprites.Names(count),
        };

        public static readonly TrainerSpriteSet NamingIcons = new()
        {
            Archive = DirNames.NameInputGraphics, NameDigits = 2, NamingScreen = true,
            Title = "Naming screen", Noun = "icon", ItemLabel = "Icon",
            Names = _ => DSPRE.ROMFiles.NamingScreenIcons.All.Select(i => i.Name).ToList(),
        };

        public string FileStem(int id) => id.ToString("D" + NameDigits);
    }

    /// <summary>One swatch in the palette strip.</summary>
    public class PaletteSwatchViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public int Index { get; }
        public IBrush Brush { get; }
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { if (_isSelected == value) return; _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
        }
        public PaletteSwatchViewModel(int index, System.Drawing.Color color)
        {
            Index = index;
            Brush = new SolidColorBrush(global::Avalonia.Media.Color.FromRgb(color.R, color.G, color.B));
        }
    }

    /// <summary>One clickable frame thumbnail in the strip.</summary>
    public class FrameThumbnailViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public int Index { get; }
        public int Part { get; }
        public int Local { get; }
        public string Caption { get; }
        public bool HasCaption => Caption != null;
        private Bitmap _image;
        public Bitmap Image
        {
            get => _image;
            set { if (_image == value) return; _image = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Image))); }
        }
        public FrameThumbnailViewModel(int index, Bitmap image, int part = 0, int local = -1, string caption = null)
        {
            Index = index; _image = image; Part = part; Local = local < 0 ? index : local; Caption = caption;
        }
    }

    /// <summary>One pose in a frame's pose picker: an NCER cell.</summary>
    public class AnimCellChoiceViewModel
    {
        public int Index { get; }
        public string Label { get; }
        public Bitmap Thumbnail { get; }
        public AnimCellChoiceViewModel(int index, string label, Bitmap thumbnail) { Index = index; Label = label; Thumbnail = thumbnail; }
    }

    /// <summary>One frame of a sequence: a pose and how long it holds. Edits write into the model and notify the owner.</summary>
    public class AnimFrameRowViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private readonly AnimFrameDataJson _model;
        private readonly Action _onChanged;
        private readonly Func<int, Bitmap> _renderThumbnail;

        public AnimFrameRowViewModel(AnimFrameDataJson model, Action onChanged, Func<int, Bitmap> renderThumbnail)
        {
            _model = model;
            _onChanged = onChanged;
            _renderThumbnail = renderThumbnail;
            _thumbnail = renderThumbnail(model.CellIndex);
        }

        public AnimFrameDataJson Model => _model;

        public int Delay
        {
            get => _model.FrameDelay;
            set
            {
                int clamped = Math.Max(1, value);
                if (_model.FrameDelay == clamped) return;
                _model.FrameDelay = clamped;
                OnPropertyChanged();
                _onChanged();
            }
        }

        public int CellIndex
        {
            get => _model.CellIndex;
            set
            {
                if (value < 0 || _model.CellIndex == value) return;
                _model.CellIndex = value;
                OnPropertyChanged();
                Thumbnail = _renderThumbnail(value);
                _onChanged();
            }
        }

        private Bitmap _thumbnail;
        public Bitmap Thumbnail { get => _thumbnail; private set { _thumbnail = value; OnPropertyChanged(); } }
    }

    /// <summary>One entry in the sequence picker.</summary>
    public class AnimSequenceChoiceViewModel
    {
        public AnimSequenceJson Model { get; }
        public int Index { get; }
        public string DisplayName { get; }
        public AnimSequenceChoiceViewModel(AnimSequenceJson model, int index)
        {
            Model = model;
            Index = index;
            int n = model.FrameData.Count;
            DisplayName = $"Sequence {index} ({n} frame{(n == 1 ? "" : "s")})";
        }
    }

    /// <summary>
    /// Pixel editor for a trainer sprite. Plat/HGSS paint on the composited frame and write each stroke into the
    /// cell tiles under it, so frames sharing tiles change together; DP has no cells and edits the flat sheet.
    /// </summary>
    public class TrainerSpriteEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        // Composited-canvas fixed logical size (OAM offsets are relative to its center), generous
        // enough to fit any trainer-class sprite without clipping, matching the size convention
        // TrainerEditorViewModel already renders class sprites at (96) with a little headroom.
        private const int CanvasSize = 128;

        private System.Drawing.Color[][] _pal;
        private DrawingFile _tile;
        private NcerFile _sprite; // null on DP (no NCER), flat-sheet fallback mode

        // Descrambling zeroes the seed word, so it is kept to scramble again on save.
        private ushort? _scrambleSeed;
        private string _tilesPath;

        // hg-engine path: cell geometry read from *_cell.json instead of the compiled narc, same nitrogfx
        // bug as TrainerClassSpriteRenderer works around (see HgEngineTrainerGraphicsSource). Only the
        // geometry source changes; painted pixels still go into _tile/_pal as before.
        private bool _cellsFromSource;

        private int BankCount => _bankMap?.Length ?? _sprite?.Cells.Count ?? 0;
        private NcerFile.Cell GetBank(int i) => _sprite.Cells[MapBank(i)];

        // The naming screen shows only the cells one icon's animation uses.
        private int[] _bankMap;
        private int MapBank(int frame) => _bankMap != null && frame >= 0 && frame < _bankMap.Length ? _bankMap[frame] : frame;
        private DSPRE.RawImage GetCompositedRawImage(int bankIndex, int width, int height, int[] drawIndex, bool trans = true) =>
            CellRenderer.Draw(_sprite, MapBank(bankIndex), _tile.ToBytes(), Bgr555(_pal), width, height, trans, drawIndex);

        private static ushort[] Bgr555(System.Drawing.Color[][] rows) =>
            rows.SelectMany(r => r).Select(c => NitroBgCodec.ToBgr555(c.R, c.G, c.B)).ToArray();

        /// <summary>A palette file's colours in the rows it declares, as the swatches edit them.</summary>
        private static System.Drawing.Color[][] ReadPaletteRows(byte[] nclr) =>
            NitroBgCodec.ReadColourRows(nclr).Select(r => r.Select(c =>
            {
                (byte red, byte green, byte blue) = NitroBgCodec.Colour(c);
                return System.Drawing.Color.FromArgb(red, green, blue);
            }).ToArray()).ToArray();

        private static DrawingFile ReadDrawing(byte[] ncgr) =>
            DrawingFile.Read(ncgr) ?? throw new InvalidDataException("The drawing has no pixel section.");

        private static NcerFile ReadCells(byte[] ncer) =>
            NcerFile.Read(ncer) ?? throw new InvalidDataException("The cell file couldn't be read.");

        // ── Mode A: composited cell editing (Plat/HGSS) ─────────────────────────
        private sealed class EditCell
        {
            public int Width, Height;
            public int DstX, DstY;
            public bool FlipX, FlipY;
            public int PaletteBank;
            public int ByteStart, ByteLen;
        }
        private readonly List<EditCell> _cells = new();
        private int _selectedFrameIndex = -1;
        private int _selectedStripFrame = -1;

        // HGSS Ethan and Lyra are two drawings on one palette (see TrainerBackSprites.LinkedSet); each is a part.
        private sealed class SpritePart
        {
            public int Entry;
            public string Label;
            public DrawingFile Tile;
            public System.Drawing.Color[][] Pal;
            public NcerFile Sprite;
            public string TilesPath, PalPath;

            // A sheet import can change the cells and animations too; they wait here until saved.
            public byte[] Ncgr, Ncer, Nanr;

            // Per scan half, the frame it shows and that frame as it was when last read or saved; null where none.
            public ScanHalf[] Scan;
        }
        private readonly List<SpritePart> _parts = new();
        private int _activePart;
        private readonly List<int> _entryIds = new();

        public bool HasLinkedSets => _parts.Count > 1;
        public string SelectedSetLabel => HasLinkedSets ? _parts[_activePart].Label : null;

        private void Activate(int part)
        {
            SpritePart p = _parts[part];
            _activePart = part;
            _tile = p.Tile; _pal = p.Pal; _sprite = p.Sprite;
            _tilesPath = p.TilesPath; _palPath = p.PalPath;
        }
        private int _activePaletteBank = -1;

        // ── Mode B: flat tile-sheet editing (DP fallback) ───────────────────────
        private int[] _flatIndices;
        private int _flatWidth, _flatHeight;

        public bool IsFlatSheetMode => _sprite == null;

        private const int MaxZoom = 24;
        private int _zoom = 4;
        private bool _zoomChosen;

        /// <summary>Screen pixels across one sprite pixel. Once the user zooms, switching sprites keeps it.</summary>
        public int ZoomFactor
        {
            get => _zoom;
            private set
            {
                if (_zoomChosen || _zoom == value) return;
                _zoom = value;
                RaiseZoom();
            }
        }

        public bool CanZoomIn => _zoom < MaxZoom;
        public bool CanZoomOut => _zoom > 1;
        public void ZoomIn() => ChooseZoom(_zoom < 4 ? _zoom + 1 : _zoom + 2);
        public void ZoomOut() => ChooseZoom(_zoom <= 4 ? _zoom - 1 : _zoom - 2);

        private void ChooseZoom(int zoom)
        {
            zoom = Math.Clamp(zoom, 1, MaxZoom);
            _zoomChosen = true;
            if (zoom == _zoom) return;
            _zoom = zoom;
            RaiseZoom();
            if (BankCount > 0) RebuildCompositedCanvas();
            else if (_flatIndices != null && _pal != null) RebuildFlatCanvas();
        }

        private void RaiseZoom()
        {
            OnPropertyChanged(nameof(ZoomFactor));
            OnPropertyChanged(nameof(CanZoomIn));
            OnPropertyChanged(nameof(CanZoomOut));
            OnPropertyChanged(nameof(GridOn));
        }

        private bool _showGrid = true;
        public bool ShowGrid
        {
            get => _showGrid;
            set { if (Set(ref _showGrid, value)) OnPropertyChanged(nameof(GridOn)); }
        }

        // Below four screen pixels a line between every pixel covers the drawing.
        public bool GridOn => _showGrid && _zoom >= 4;

        public int FrameCount => BankCount;
        public int SelectedFrameIndex
        {
            get => _selectedStripFrame;
            set
            {
                if (_selectedStripFrame == value) return;
                _selectedStripFrame = value;
                OnPropertyChanged();
                FrameThumbnailViewModel thumb = value >= 0 && value < FrameThumbnails.Count ? FrameThumbnails[value] : null;
                if (thumb != null && thumb.Part != _activePart) { Activate(thumb.Part); _activePaletteBank = -1; }
                _selectedFrameIndex = thumb?.Local ?? value;
                LoadFrame(_selectedFrameIndex);
                OnPropertyChanged(nameof(SelectedSetLabel));
            }
        }

        public ObservableCollection<FrameThumbnailViewModel> FrameThumbnails { get; } = new();
        public bool HasFrames => FrameThumbnails.Count > 0;

        public ObservableCollection<string> ClassNames { get; } = new();
        public int SelectedClassIndex
        {
            get => _entryIds.IndexOf(_trClassID);
            set
            {
                if (value < 0 || value >= _entryIds.Count) return;
                value = _entryIds[value];
                if (value == _trClassID) return;
                if (HasUnsavedChanges && _trClassID >= 0)
                {
                    // Snap the list back to the class still loaded until the user has answered.
                    int requested = value;
                    OnPropertyChanged(nameof(SelectedClassIndex));
                    _ = SwitchClassAsync(requested);
                    return;
                }
                if (Set(ref _trClassID, value)) Load(value);
            }
        }

        private async Task SwitchClassAsync(int requested)
        {
            // Covers both halves of this editor's dirty state: the painted sprite and the animation
            // JSON, since HasUnsavedChanges is the OR of the two.
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(this, null, "trainer class")) return;
            if (Set(ref _trClassID, requested, nameof(SelectedClassIndex))) Load(requested);
        }

        public ObservableCollection<PaletteSwatchViewModel> PaletteSwatches { get; } = new();

        private int _selectedSwatchIndex;
        public int SelectedSwatchIndex
        {
            get => _selectedSwatchIndex;
            set { Set(ref _selectedSwatchIndex, value); foreach (PaletteSwatchViewModel s in PaletteSwatches) s.IsSelected = s.Index == value; }
        }

        private SpriteEditTool _selectedTool = SpriteEditTool.Pencil;
        public SpriteEditTool SelectedTool
        {
            get => _selectedTool;
            set { if (Set(ref _selectedTool, value)) { OnPropertyChanged(nameof(IsPencil)); OnPropertyChanged(nameof(IsEyedropper)); } }
        }
        public bool IsPencil { get => _selectedTool == SpriteEditTool.Pencil; set { if (value) SelectedTool = SpriteEditTool.Pencil; } }
        public bool IsEyedropper { get => _selectedTool == SpriteEditTool.Eyedropper; set { if (value) SelectedTool = SpriteEditTool.Eyedropper; } }

        private Bitmap _canvasBitmap;
        public Bitmap CanvasBitmap { get => _canvasBitmap; private set => Set(ref _canvasBitmap, value); }

        private string _statusText = "";
        public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

        private bool _dirty;
        public bool HasUnsavedChanges
        {
            get => _dirty || AnimJsonDirty || _romAnimDirty;
            private set { Set(ref _dirty, value); if (value) EditCommitted(); }
        }

        // ── Undo for pixels, palettes and sheet imports. The Animations tab's text box keeps its own. ──
        private sealed class PartState
        {
            public DrawingFile Tile;
            public NcerFile Sprite;
            public byte[] Tiles;
            public System.Drawing.Color[][] Pal;
            public byte[] Ncgr, Ncer, Nanr;
        }

        private sealed class SpriteState
        {
            public PartState[] Parts;
            public int[] Flat;
            public System.Drawing.Color[][] ActivePal;
        }

        private readonly UndoHistory<SpriteState> _history = new UndoHistory<SpriteState>(100);
        private SpriteState _lastState, _savedState;
        private DateTime _lastCommit = DateTime.MinValue;
        private bool _stroking, _strokeEdited, _restoring;
        // The group of the change being committed, and of the last step; one group's changes are one step.
        private object _editGroup, _lastGroup;

        private static System.Drawing.Color[][] CopyPal(System.Drawing.Color[][] pal) =>
            pal?.Select(b => (System.Drawing.Color[])b.Clone()).ToArray();

        private SpriteState TakeState() => new SpriteState
        {
            Parts = _parts.Select(p => new PartState
            {
                Tile = p.Tile, Sprite = p.Sprite, Tiles = (byte[])p.Tile?.Tiles?.Clone(), Pal = CopyPal(p.Pal),
                Ncgr = p.Ncgr, Ncer = p.Ncer, Nanr = p.Nanr,
            }).ToArray(),
            Flat = (int[])_flatIndices?.Clone(),
            ActivePal = CopyPal(_pal),
        };

        private static bool SamePal(System.Drawing.Color[][] a, System.Drawing.Color[][] b) =>
            a == null || b == null ? a == b
            : a.Length == b.Length && a.Zip(b).All(z => z.First.Length == z.Second.Length
                && z.First.Zip(z.Second).All(c => c.First.ToArgb() == c.Second.ToArgb()));

        private static bool SameState(SpriteState a, SpriteState b) =>
            a != null && b != null && a.Parts.Length == b.Parts.Length
            && a.Parts.Zip(b.Parts).All(z => z.First.Tile == z.Second.Tile && z.First.Sprite == z.Second.Sprite
                && (z.First.Tiles ?? Array.Empty<byte>()).AsSpan().SequenceEqual(z.Second.Tiles ?? Array.Empty<byte>())
                && SamePal(z.First.Pal, z.Second.Pal))
            && (a.Flat ?? Array.Empty<int>()).AsSpan().SequenceEqual(b.Flat ?? Array.Empty<int>())
            && SamePal(a.ActivePal, b.ActivePal);

        private void ResetSteps()
        {
            _lastState = _savedState = TakeState();
            _history.Reset(_lastState);
            _stroking = _strokeEdited = false;
            RaiseSteps();
        }

        private void RaiseSteps() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        /// <summary>A pointer drag on the canvas starts; everything it paints is one undo step.</summary>
        public void BeginStroke() { _stroking = true; _strokeEdited = false; }

        public void EndStroke()
        {
            if (!_stroking) return;
            _stroking = false;
            if (_strokeEdited) { _strokeEdited = false; Commit(coalesce: false); }
        }

        private void EditCommitted()
        {
            object group = _editGroup;
            _editGroup = null;
            if (_restoring || _lastState == null) return;
            if (_stroking) { _strokeEdited = true; return; }
            Commit(group != null ? ReferenceEquals(group, _lastGroup) : (DateTime.UtcNow - _lastCommit).TotalMilliseconds < 500, group);
        }

        private void Commit(bool coalesce, object group = null)
        {
            SpriteState now = TakeState();
            if (SameState(now, _lastState)) return;
            _history.Capture(now, coalesce && _history.CanUndo);
            _lastState = now;
            _lastCommit = DateTime.UtcNow;
            _lastGroup = group;
            RaiseSteps();
        }

        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;

        public void Undo() { if (_history.CanUndo) { _lastGroup = null; Restore(_history.Undo()); } }
        public void Redo() { if (_history.CanRedo) { _lastGroup = null; Restore(_history.Redo()); } }

        private static void PutPal(System.Drawing.Color[][] pal, System.Drawing.Color[][] colours)
        {
            if (pal == null || colours == null) return;
            for (int b = 0; b < Math.Min(pal.Length, colours.Length); b++)
                for (int i = 0; i < Math.Min(pal[b].Length, colours[b].Length); i++) pal[b][i] = colours[b][i];
        }

        private void Restore(SpriteState state)
        {
            bool layoutChanged = false;
            for (int i = 0; i < Math.Min(_parts.Count, state.Parts.Length); i++)
            {
                SpritePart part = _parts[i];
                PartState was = state.Parts[i];
                layoutChanged |= part.Sprite != was.Sprite || part.Tile != was.Tile;
                part.Tile = was.Tile; part.Sprite = was.Sprite;
                part.Ncgr = was.Ncgr; part.Ncer = was.Ncer; part.Nanr = was.Nanr;
                if (part.Tile != null && was.Tiles != null)
                {
                    if (part.Tile.Tiles != null && part.Tile.Tiles.Length == was.Tiles.Length) Array.Copy(was.Tiles, part.Tile.Tiles, was.Tiles.Length);
                    else part.Tile.SetTiles((byte[])was.Tiles.Clone());
                }
                PutPal(part.Pal, was.Pal);
            }
            if (state.Flat != null) _flatIndices = (int[])state.Flat.Clone();
            PutPal(_pal, state.ActivePal);

            _lastState = state;
            _restoring = true;
            try
            {
                _dirty = !SameState(state, _savedState);
                _paletteDirty = _dirty;
            }
            finally { _restoring = false; }
            OnPropertyChanged(nameof(HasUnsavedChanges));

            if (IsFlatSheetMode)
            {
                BuildPaletteSwatches(Math.Max(0, _activePaletteBank));
                RebuildFlatCanvas();
            }
            else if (layoutChanged)
            {
                // The same refresh a sheet import does, since the cells may differ.
                Activate(Math.Min(_activePart, _parts.Count - 1));
                BuildFrameThumbnails();
                _activePaletteBank = -1;
                _selectedStripFrame = -1;
                SelectedFrameIndex = 0;
                OnPropertyChanged(nameof(FrameCount));
                RebuildAnimCellChoices();
                StopAnimPreview();
                _romAnim = OpenRomAnimations();
                OnPropertyChanged(nameof(CanEditAnimFrames));
                SetAnimJsonTextSilent(AnimationJsonOf(_romAnim ?? OpenFrames(0).Animations) ?? "");
                OnPropertyChanged(nameof(HasAnimation));
            }
            else
            {
                if (_parts.Count > 0) Activate(_activePart);
                if (_activePaletteBank >= 0) BuildPaletteSwatches(_activePaletteBank);
                RebuildCompositedCanvas();
                BuildFrameThumbnails();
                RebuildTopBar();
            }
            RaiseSteps();
        }

        // Kept apart from _dirty so an animation-only save leaves the pixel files and their scan copy alone.
        private bool _romAnimDirty;
        public string UnsavedChangesDescription => $"{_set.Title} ({_set.Noun} {_trClassID})";

        private readonly TrainerSpriteSet _set = TrainerSpriteSet.Classes;
        public string Title => _set.Title;
        public string ItemLabel => _set.ItemLabel;
        public DirNames Archive => _set.Archive;
        public void SaveChanges()
        {
            if (SaveAll() == null) SaveNotice.Saved(UnsavedChangesDescription);
        }

        public Task<bool> SaveChangesAsync()
        {
            string error = SaveAll();
            if (error != null) return Task.FromException<bool>(new InvalidOperationException(error));
            SaveNotice.Saved(UnsavedChangesDescription);
            return Task.FromResult(!HasUnsavedChanges);
        }

        /// <summary>Saves whichever of the sprite and its animation JSON have edits. Null on success.</summary>
        public string SaveAll()
        {
            if (_dirty)
            {
                string error = Save();
                if (error != null) return error;
            }
            if (_romAnimDirty)
            {
                string error = SaveRomAnimations();
                if (error != null) return error;
            }
            return AnimJsonDirty ? SaveAnimJson() : null;
        }

        /// <summary>Discarding re-reads the class from disk, since paint goes straight into the in-memory
        /// buffers.</summary>
        public void DiscardChanges() => Load(_trClassID);

        // ── Animations tab: NNN_anim.json read/written straight from the linked hg-engine checkout's
        // source (this is the raw-text half; the structured sequence/frame editor further down keeps
        // this text in sync both ways).
        private string _animJsonPath;

        // Created in the editor and not written yet.
        private bool _animJsonCreated;

        private string _animJsonText = "";
        public string AnimJsonText
        {
            get => _animJsonText;
            set
            {
                if (!Set(ref _animJsonText, value)) return;
                if (CanEditAnimJson) AnimJsonDirty = true;
                if (!_syncingAnimText) TryRebuildAnimModelFromText();
            }
        }

        private bool _animJsonDirty;
        public bool AnimJsonDirty
        {
            get => _animJsonDirty;
            private set { if (Set(ref _animJsonDirty, value)) OnPropertyChanged(nameof(HasUnsavedChanges)); }
        }

        private string _animJsonStatusText = "";
        public string AnimJsonStatusText { get => _animJsonStatusText; private set => Set(ref _animJsonStatusText, value); }

        public bool CanEditAnimJson => HgEngineProject.IsActive;

        // Outside hg-engine only each frame's cell and hold change in the ROM's animation file.
        private NanrFile _romAnim;
        public bool CanEditAnimFrames => CanEditAnimJson || _romAnim != null;

        private NanrFile OpenRomAnimations()
        {
            if (_set.NamingScreen || _parts.Count == 0 || IsFlatSheetMode) return null;
            try
            {
                SpritePart p = _parts[0];
                return NanrFile.Read(p.Nanr ?? File.ReadAllBytes(EntryPath(TrainerGraphicsLayout.AnimationEntry(p.Entry))));
            }
            catch (Exception ex)
            {
                AppLogger.Error("TrainerSpriteEditorViewModel: animation file couldn't be read: " + ex.Message);
                return null;
            }
        }

        // A changed frame gets a new result entry, so frames sharing the old one keep it.
        private void ApplyRomAnimEdits()
        {
            if (_romAnim == null || _animRoot == null || _parts.Count == 0) return;
            bool changed = false;
            for (int s = 0; s < Math.Min(_romAnim.Sequences.Count, _animRoot.Sequences.Count); s++)
            {
                List<NanrFile.Frame> frames = _romAnim.Sequences[s].Frames;
                List<AnimFrameDataJson> model = _animRoot.Sequences[s].FrameData;
                for (int f = 0; f < Math.Min(frames.Count, model.Count); f++)
                {
                    if (_romAnim.CellOf(s, f) != model[f].CellIndex) { _romAnim.SetCell(s, f, model[f].CellIndex); changed = true; }
                    if (Math.Max(1, (int)frames[f].Delay) != model[f].FrameDelay) { _romAnim.SetDelay(s, f, model[f].FrameDelay); changed = true; }
                }
            }
            if (!changed) return;
            _romAnimDirty = true;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private string SaveRomAnimations()
        {
            try
            {
                SpritePart p = _parts[0];
                ArchiveMembers.Put(_set.Archive, TrainerGraphicsLayout.AnimationEntry(p.Entry), _romAnim.Write());
                p.Nanr = null;
                _romAnimDirty = false;
                OnPropertyChanged(nameof(HasUnsavedChanges));
                StatusText = "Saved.";
                return null;
            }
            catch (Exception ex)
            {
                StatusText = "Couldn't save: " + ex.Message;
                return ex.Message;
            }
        }

        public bool HasAnimJsonFile => _animJsonPath != null && File.Exists(_animJsonPath);
        /// <summary>True when there is an animation to show, from hg-engine's JSON or read from the ROM.</summary>
        public bool HasAnimation => HasAnimJsonFile || _animJsonCreated || (!CanEditAnimJson && !string.IsNullOrEmpty(AnimJsonText));

        private void SetAnimJsonTextSilent(string text)
        {
            _animJsonText = text;
            OnPropertyChanged(nameof(AnimJsonText));
            TryRebuildAnimModelFromText();
        }

        private void LoadAnimJson(int trClassID)
        {
            _animJsonCreated = false;
            _animJsonPath = HgEngineProject.IsActive
                ? HgEngineTrainerGraphicsSource.Stem(_set.IsBack, trClassID) + "_anim.json"
                : null;
            OnPropertyChanged(nameof(CanEditAnimJson));
            OnPropertyChanged(nameof(HasAnimJsonFile));

            _romAnim = _animJsonPath == null ? OpenRomAnimations() : null;
            _romAnimDirty = false;
            OnPropertyChanged(nameof(CanEditAnimFrames));
            if (_animJsonPath == null)
            {
                SetAnimJsonTextSilent((_romAnim != null ? AnimationJsonOf(_romAnim) : RomAnimationJson(trClassID)) ?? "");
                AnimJsonStatusText = "";
            }
            else if (File.Exists(_animJsonPath))
            {
                try
                {
                    SetAnimJsonTextSilent(File.ReadAllText(_animJsonPath));
                    AnimJsonStatusText = "";
                }
                catch (Exception ex)
                {
                    SetAnimJsonTextSilent("");
                    AnimJsonStatusText = "Couldn't read: " + ex.Message;
                }
            }
            else
            {
                SetAnimJsonTextSilent("");
                AnimJsonStatusText = "";
            }
            AnimJsonDirty = false;
            OnPropertyChanged(nameof(HasAnimation));
            OnPropertyChanged(nameof(CanCreateAnimJson));
        }

        // The ROM's compiled animation as the JSON model, for showing and playing without a checkout.
        private string RomAnimationJson(int id)
        {
            try
            {
                TrainerClassSpriteRenderer renderer = new TrainerClassSpriteRenderer();
                renderer.Load(id, _set.Archive);
                if (renderer.SequenceCount == 0) return null;
                AnimJsonRoot root = new AnimJsonRoot();
                for (int seq = 0; seq < renderer.SequenceCount; seq++)
                {
                    AnimSequenceJson model = new AnimSequenceJson { AnimationType = 1, PlaybackMode = 2 };
                    foreach ((int bank, int duration) in renderer.Sequence(seq))
                        model.FrameData.Add(new AnimFrameDataJson { CellIndex = bank, FrameDelay = Math.Max(1, duration) });
                    root.Sequences.Add(model);
                }
                return root.Serialize();
            }
            catch (Exception ex)
            {
                AppLogger.Error("TrainerSpriteEditorViewModel: animation couldn't be read: " + ex.Message);
                return null;
            }
        }

        /// <summary>Returns null on success, error message on failure. Validates the text parses as JSON
        /// before writing.</summary>
        public string SaveAnimJson()
        {
            if (_animJsonPath == null) return "No hg-engine checkout is linked.";
            try
            {
                using (JsonDocument.Parse(AnimJsonText)) { }
            }
            catch (JsonException ex)
            {
                return "Invalid JSON: " + ex.Message;
            }
            try
            {
                // The shared writer replaces the file atomically and keeps its line endings and cached text.
                HgEngineFileCache.WriteText(_animJsonPath, AnimJsonText);
                _animJsonCreated = false;
                AnimJsonDirty = false;
                AnimJsonStatusText = "Saved.";
                OnPropertyChanged(nameof(HasAnimJsonFile));
                OnPropertyChanged(nameof(HasAnimation));
                OnPropertyChanged(nameof(CanCreateAnimJson));
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>Seeds a minimal, valid single-pose anim.json (matching the shape hg-engine's own
        /// single-frame classes use) for a class that doesn't have one yet.</summary>
        public string CreateAnimJson()
        {
            if (_animJsonPath == null) return "No hg-engine checkout is linked.";
            const string template = """
                {
                	"labelEnabled":	true,
                	"uaatEnabled":	false,
                	"sequenceCount":	1,
                	"frameCount":	1,
                	"sequences":	[{
                			"frameCount":	1,
                			"loopStartFrame":	0,
                			"animationElement":	0,
                			"animationType":	1,
                			"playbackMode":	2,
                			"frameData":	[{
                					"frameDelay":	4,
                					"resultId":	0
                				}]
                		}],
                	"animationResults":	[{
                			"resultType":	0,
                			"index":	0
                		}],
                	"resultCount":	1,
                	"labels":	["CellAnime0"],
                	"labelCount":	1
                }
                """;
            // Held in the editor like any other edit, so Discard can still take it back.
            SetAnimJsonTextSilent(template);
            _animJsonCreated = true;
            AnimJsonDirty = true;
            AnimJsonStatusText = "Not saved yet.";
            OnPropertyChanged(nameof(HasAnimation));
            OnPropertyChanged(nameof(CanCreateAnimJson));
            return null;
        }

        // ── Structured animation editor: sequences/frames built from AnimJsonText, kept in sync with it
        // both ways, like the Cards and Script tabs in Move animations & battle scripts.
        private AnimJsonRoot _animRoot;
        private bool _syncingAnimText;   // guards the structured-model <-> AnimJsonText echo loop

        public ObservableCollection<AnimSequenceChoiceViewModel> AnimSequenceChoices { get; } = new();
        private bool _refillingSequences;

        private void RefillSequenceChoices()
        {
            _refillingSequences = true;
            try
            {
                AnimSequenceChoices.Clear();
                for (int i = 0; i < (_animRoot?.Sequences.Count ?? 0); i++)
                    AnimSequenceChoices.Add(new AnimSequenceChoiceViewModel(_animRoot.Sequences[i], i));
            }
            finally { _refillingSequences = false; }
        }

        private AnimSequenceChoiceViewModel _selectedAnimSequence;
        public AnimSequenceChoiceViewModel SelectedAnimSequence
        {
            get => _selectedAnimSequence;
            set
            {
                // Refilling the choices makes the picker push null back; that is not the user's choice.
                if (_refillingSequences) return;
                if (!Set(ref _selectedAnimSequence, value)) return;
                OnPropertyChanged(nameof(HasSelectedAnimSequence));
                StopAnimPreview();
                RebuildAnimFrameRows();
            }
        }
        public bool HasSelectedAnimSequence => SelectedAnimSequence != null;
        public bool CanCreateAnimJson => CanEditAnimJson && !HasAnimation;

        public ObservableCollection<AnimFrameRowViewModel> AnimFrameRows { get; } = new();
        public bool HasAnimFrameRows => AnimFrameRows.Count > 0;

        public ObservableCollection<AnimCellChoiceViewModel> AnimCellChoices { get; } = new();

        private string _animModelStatusText = "";
        public string AnimModelStatusText { get => _animModelStatusText; private set => Set(ref _animModelStatusText, value); }

        private bool _animPreviewPlaying;
        public bool AnimPreviewPlaying { get => _animPreviewPlaying; private set => Set(ref _animPreviewPlaying, value); }

        private Bitmap _animPreviewBitmap;
        public Bitmap AnimPreviewBitmap { get => _animPreviewBitmap; private set => Set(ref _animPreviewBitmap, value); }

        private int _animPreviewFrameNumber;
        public int AnimPreviewFrameNumber { get => _animPreviewFrameNumber; private set => Set(ref _animPreviewFrameNumber, value); }

        private CancellationTokenSource _animPreviewCts;

        private void RebuildAnimCellChoices()
        {
            AnimCellChoices.Clear();
            for (int i = 0; i < BankCount; i++)
            {
                string name = GetBank(i).Name;
                string label = string.IsNullOrWhiteSpace(name) ? $"Cell {i}" : $"{name} ({i})";
                AnimCellChoices.Add(new AnimCellChoiceViewModel(i, label, RenderAnimCellThumbnail(i)));
            }
        }

        private Bitmap RenderAnimCellThumbnail(int cellIndex, int size = 72)
        {
            if (BankCount == 0 || cellIndex < 0 || cellIndex >= BankCount || _tile == null || _pal == null) return null;
            try
            {
                RawImage raw = GetCompositedRawImage(cellIndex, size, size, null);
                return ImageConverter.ToAvaloniaBitmap(raw);
            }
            catch (Exception ex) { AppLogger.WarnOnce("TrainerSpriteEditorViewModel.RenderAnimCellThumbnail", ex); return null; }
        }

        /// <summary>Re-parses AnimJsonText into the structured model. On a parse error, leaves whatever
        /// structure is already showing untouched (so a mid-typo keystroke in the JSON tab doesn't blank
        /// the Editor tab) and surfaces the error in <see cref="AnimModelStatusText"/> instead.</summary>
        private void TryRebuildAnimModelFromText()
        {
            if (string.IsNullOrWhiteSpace(AnimJsonText))
            {
                _animRoot = null;
                _selectedAnimSequence = null;
                RefillSequenceChoices();
                OnPropertyChanged(nameof(SelectedAnimSequence));
                OnPropertyChanged(nameof(HasSelectedAnimSequence));
                AnimFrameRows.Clear();
                OnPropertyChanged(nameof(HasAnimFrameRows));
                AnimModelStatusText = "";
                return;
            }
            try
            {
                _animRoot = AnimJsonRoot.Parse(AnimJsonText);
                AnimModelStatusText = "";
            }
            catch (Exception ex)
            {
                AnimModelStatusText = "JSON has an error, showing the last valid structure: " + ex.Message;
                return; // keep whatever AnimSequenceChoices/AnimFrameRows already have
            }

            int keepIndex = SelectedAnimSequence?.Index ?? 0;
            RefillSequenceChoices();

            AnimSequenceChoiceViewModel restore = AnimSequenceChoices.FirstOrDefault(s => s.Index == keepIndex) ?? AnimSequenceChoices.FirstOrDefault();
            if (!ReferenceEquals(restore, _selectedAnimSequence))
                SelectedAnimSequence = restore;   // triggers RebuildAnimFrameRows via the setter
            else
                RebuildAnimFrameRows();            // same selection, but its frames may have changed
        }

        private void RebuildAnimFrameRows()
        {
            AnimFrameRows.Clear();
            if (SelectedAnimSequence != null)
            {
                foreach (AnimFrameDataJson frame in SelectedAnimSequence.Model.FrameData)
                    AnimFrameRows.Add(new AnimFrameRowViewModel(frame, SyncAnimModelToText, i => RenderAnimCellThumbnail(i)));
            }
            if (!AnimPreviewPlaying) AnimPreviewBitmap = AnimFrameRows.Count > 0 ? AnimFrameRows[0].Thumbnail : null;
            OnPropertyChanged(nameof(HasAnimFrameRows));
        }

        /// <summary>Called whenever a frame/sequence edit mutates <see cref="_animRoot"/>; re-serializes
        /// it back into AnimJsonText without re-triggering a model rebuild.</summary>
        private void SyncAnimModelToText()
        {
            if (_animRoot == null) return;
            _syncingAnimText = true;
            AnimJsonText = _animRoot.Serialize();
            _syncingAnimText = false;
            ApplyRomAnimEdits();
            if (!AnimPreviewPlaying && AnimFrameRows.Count > 0) AnimPreviewBitmap = AnimFrameRows[0].Thumbnail;

            // Sequence picker labels show frame counts, so rebuild them after any add/remove.
            int keepIndex = SelectedAnimSequence?.Index ?? 0;
            RefillSequenceChoices();
            AnimSequenceChoiceViewModel restore = AnimSequenceChoices.FirstOrDefault(s => s.Index == keepIndex) ?? AnimSequenceChoices.FirstOrDefault();
            if (!ReferenceEquals(restore, _selectedAnimSequence)) _selectedAnimSequence = restore;
            OnPropertyChanged(nameof(SelectedAnimSequence));
            OnPropertyChanged(nameof(HasSelectedAnimSequence));
        }

        public void AddAnimFrame()
        {
            if (SelectedAnimSequence == null) return;
            StopAnimPreview();
            int copyFrom = AnimFrameRows.Count > 0 ? SelectedAnimSequence.Model.FrameData[^1].CellIndex : 0;
            SelectedAnimSequence.Model.FrameData.Add(new AnimFrameDataJson { FrameDelay = 4, CellIndex = copyFrom });
            RebuildAnimFrameRows();
            SyncAnimModelToText();
        }

        /// <summary>Returns an error message if the removal was refused (every sequence needs at least
        /// one frame), null on success.</summary>
        public string RemoveAnimFrame(AnimFrameRowViewModel row)
        {
            if (SelectedAnimSequence == null || row == null) return null;
            if (SelectedAnimSequence.Model.FrameData.Count <= 1)
                return "A sequence needs at least one frame. Remove the whole sequence instead if you don't want it.";
            StopAnimPreview();
            SelectedAnimSequence.Model.FrameData.Remove(row.Model);
            RebuildAnimFrameRows();
            SyncAnimModelToText();
            return null;
        }

        public void MoveAnimFrame(AnimFrameRowViewModel row, int direction)
        {
            if (SelectedAnimSequence == null || row == null) return;
            List<AnimFrameDataJson> list = SelectedAnimSequence.Model.FrameData;
            int i = list.IndexOf(row.Model);
            int j = i + direction;
            if (i < 0 || j < 0 || j >= list.Count) return;
            StopAnimPreview();
            (list[i], list[j]) = (list[j], list[i]);
            RebuildAnimFrameRows();
            SyncAnimModelToText();
        }

        public void AddAnimSequence()
        {
            if (_animRoot == null) return;
            StopAnimPreview();
            AnimSequenceJson seq = new AnimSequenceJson
            {
                AnimationType = 1,
                PlaybackMode = 2,
                FrameData = { new AnimFrameDataJson { FrameDelay = 4, CellIndex = 0 } },
            };
            _animRoot.Sequences.Add(seq);
            SyncAnimModelToText();
            SelectedAnimSequence = AnimSequenceChoices.LastOrDefault();
        }

        /// <summary>Returns an error message if the removal was refused (the file needs at least one
        /// sequence), null on success.</summary>
        public string RemoveAnimSequence()
        {
            if (_animRoot == null || SelectedAnimSequence == null) return null;
            if (_animRoot.Sequences.Count <= 1)
                return "This file needs at least one sequence.";
            StopAnimPreview();
            _animRoot.Sequences.Remove(SelectedAnimSequence.Model);
            SyncAnimModelToText();
            return null;
        }

        public void StopAnimPreview()
        {
            _animPreviewCts?.Cancel();
            _animPreviewCts = null;
            AnimPreviewPlaying = false;
        }

        /// <summary>Plays the selected sequence's frames once, in real per-frame timing, and stops on the
        /// last frame. Deliberately not a loop.</summary>
        public async Task PlayAnimPreviewOnceAsync()
        {
            if (SelectedAnimSequence == null || AnimFrameRows.Count == 0) return;
            StopAnimPreview();
            CancellationTokenSource cts = new CancellationTokenSource();
            _animPreviewCts = cts;
            AnimPreviewPlaying = true;
            try
            {
                for (int i = 0; i < AnimFrameRows.Count; i++)
                {
                    AnimFrameRowViewModel row = AnimFrameRows[i];
                    AnimPreviewBitmap = row.Thumbnail;
                    AnimPreviewFrameNumber = i + 1;
                    int ms = Math.Max(16, (int)(row.Delay * 1000.0 / 60.0));
                    await Task.Delay(ms, cts.Token);
                }
            }
            catch (OperationCanceledException) { /* user stopped or switched away, not an error */ }
            finally
            {
                if (ReferenceEquals(_animPreviewCts, cts)) { _animPreviewCts = null; AnimPreviewPlaying = false; }
            }
        }

        private int _trClassID;
        public bool Loaded => _tile != null;

        // ── Design-time constructor ────────────────────────────────────────────
        public TrainerSpriteEditorViewModel()
        {
            if (!Design.IsDesignMode) return;
            StatusText = "Design preview";
        }

        public TrainerSpriteEditorViewModel(int trClassID) : this(trClassID, TrainerSpriteSet.Classes) { }

        public TrainerSpriteEditorViewModel(int id, TrainerSpriteSet set)
        {
            _set = set;
            if (set.NamingScreen)
            {
                foreach (NamingScreenIcons.Icon icon in DSPRE.ROMFiles.NamingScreenIcons.All)
                {
                    _entryIds.Add(icon.Animation);
                    ClassNames.Add(icon.Name);
                }
                Load(_entryIds.Contains(id) ? id : _entryIds[0]);
                return;
            }

            List<string> names = set.Names(SpriteCount());
            bool linking = set.IsBack && !HgEngineProject.IsActive;
            for (int i = 0; i < names.Count; i++)
            {
                if (linking && DSPRE.ROMFiles.TrainerBackSprites.IsLinkedSet(i)) continue;
                _entryIds.Add(i);
                ClassNames.Add($"[{set.FileStem(i)}] {names[i]}");
            }
            id = Math.Clamp(id, 0, Math.Max(0, names.Count - 1));
            if (linking && DSPRE.ROMFiles.TrainerBackSprites.IsLinkedSet(id)) id -= 15;
            Load(id);
        }

        public void RefreshNames()
        {
            if (_set.NamingScreen || _entryIds.Count == 0) return;
            List<string> names = _set.Names(SpriteCount());
            List<string> labels = _entryIds.Select(i => $"[{_set.FileStem(i)}] {(i < names.Count ? names[i] : "")}").ToList();
            DSPRE.Avalonia.Data.ListSync.Apply(ClassNames, labels);
        }

        private int SpriteCount()
        {
            try { return NarcAPI.Narc.MemberCount(GameDirs[_set.Archive].unpackedDir) / DSPRE.ROMFiles.TrainerBackSprites.FilesPerSprite; }
            catch (Exception ex) { AppLogger.WarnOnce("TrainerSpriteEditorViewModel.SpriteCount", ex); return 0; }
        }

        // ── Load ───────────────────────────────────────────────────────────────
        /// <summary>Loads the class. Returns null on success, otherwise the error.</summary>
        public string Load(int trClassID)
        {
            _trClassID = trClassID;
            _bankMap = null;
            if (_set.NamingScreen) return LoadNamingIcon(trClassID);
            try
            {
                string dir = RomInfo.GameDirs[_set.Archive].unpackedDir;

                int paletteFileID = TrainerGraphicsLayout.ColoursEntry(trClassID);
                string paletteFilename = paletteFileID.ToString("D4");
                _pal = ReadPaletteRows(File.ReadAllBytes(Path.Combine(dir, paletteFilename)));

                int tilesFileID = TrainerGraphicsLayout.DrawingEntry(trClassID);
                string tilesFilename = tilesFileID.ToString("D4");
                _tilesPath = Path.Combine(dir, tilesFilename);
                _tile = ReadDrawing(File.ReadAllBytes(_tilesPath));

                _sprite = null; _cellsFromSource = false;
                _sourcePngPath = null;
                if (TrainerGraphicsLayout.HasCells)
                {
                    if (HgEngineProject.IsActive)
                    {
                        string stem = HgEngineTrainerGraphicsSource.Stem(_set.IsBack, trClassID);
                        if (_pal.Length > 0 && Data.TrainerSpriteSourcePng.TryApply(stem + ".png", _tile, _pal[0])) _sourcePngPath = stem + ".png";
                        string cellPath = stem + "_cell.json";
                        if (File.Exists(cellPath))
                        {
                            if (HgEngineTrainerGraphicsSource.TryReadCells(cellPath, out NcerFile cells, out string cellError))
                            {
                                _sprite = cells;
                                _cellsFromSource = true;
                            }
                            else
                            {
                                AppLogger.Error("TrainerSpriteEditorViewModel: " + cellError);
                            }
                        }
                    }

                    if (!_cellsFromSource)
                        _sprite = ReadCells(File.ReadAllBytes(Path.Combine(dir, TrainerGraphicsLayout.CellsEntry(trClassID).ToString("D4"))));
                }

                _parts.Clear();
                _parts.Add(new SpritePart { Entry = trClassID, Tile = _tile, Pal = _pal, Sprite = _sprite, TilesPath = _tilesPath, PalPath = Path.Combine(dir, paletteFilename) });
                int linked = _set.IsBack && !_cellsFromSource && !HgEngineProject.IsActive ? DSPRE.ROMFiles.TrainerBackSprites.LinkedSet(trClassID) : -1;
                if (linked >= 0 && File.Exists(Path.Combine(dir, TrainerGraphicsLayout.CellsEntry(linked).ToString("D4"))))
                {
                    string Name(int entry) => entry.ToString("D4");
                    int lt = TrainerGraphicsLayout.DrawingEntry(linked), lp = TrainerGraphicsLayout.ColoursEntry(linked), lc = TrainerGraphicsLayout.CellsEntry(linked);
                    _parts[0].Label = "Double battles";
                    _parts.Add(new SpritePart
                    {
                        Entry = linked, Label = "Single battles",
                        Tile = ReadDrawing(File.ReadAllBytes(Path.Combine(dir, Name(lt)))),
                        Pal = ReadPaletteRows(File.ReadAllBytes(Path.Combine(dir, Name(lp)))),
                        Sprite = ReadCells(File.ReadAllBytes(Path.Combine(dir, Name(lc)))),
                        TilesPath = Path.Combine(dir, Name(lt)), PalPath = Path.Combine(dir, Name(lp)),
                    });
                }
                Activate(0);
                MapScanFrames();
                OnPropertyChanged(nameof(HasLinkedSets));
                OnPropertyChanged(nameof(SelectedClassIndex));

                if (BankCount > 0)
                {
                    ZoomFactor = 4;
                    BuildFrameThumbnails();
                    _activePaletteBank = -1;
                    // Force the property setter below to detect a change (and so actually rebuild
                    // cells/canvas/swatches) even on a reload where the frame index doesn't move,
                    // e.g. a discard while already on frame 0.
                    _selectedStripFrame = -1;
                    SelectedFrameIndex = 0; // triggers LoadFrame -> cells + canvas + swatches
                    StatusText = $"{Frames(FrameCount)}, {_tile.BitsPerPixel}bpp";
                }
                else
                {
                    // DP (or an NCER with no banks): flat tile-sheet fallback.
                    _sprite = null;
                    ZoomFactor = 12;
                    FrameThumbnails.Clear();
                    OnPropertyChanged(nameof(HasFrames));
                    LoadFlatSheet();
                    StatusText = $"{Capital(_set.Noun)} {trClassID}: {_flatWidth}×{_flatHeight} tile sheet, {_tile.BitsPerPixel}bpp";
                }

                OnPropertyChanged(nameof(IsFlatSheetMode));
                OnPropertyChanged(nameof(FrameCount));
                OnPropertyChanged(nameof(CanUseSheets));
                OnPropertyChanged(nameof(CanUseAnimationSheets));
                HasUnsavedChanges = false;
                _paletteDirty = false;
                _palPath = Path.Combine(dir, paletteFilename);
                ResetSteps();
                StopAnimPreview();
                RebuildAnimCellChoices();
                LoadAnimJson(trClassID);
                return null;
            }
            catch (Exception ex)
            {
                _tile = null; _pal = null; _sprite = null;
                StatusText = "Couldn't load: " + ex.Message;
                AppLogger.Error("TrainerSpriteEditorViewModel.Load failed: " + ex.Message);
                return ex.Message;
            }
        }

        private void BuildFrameThumbnails()
        {
            FrameThumbnails.Clear();
            int keep = _activePart;
            for (int p = 0; p < Math.Max(1, _parts.Count); p++)
            {
                if (_parts.Count > 0) Activate(p);
                for (int i = 0; i < BankCount; i++)
                {
                    global::Avalonia.Media.Imaging.Bitmap bmp = ImageConverter.ToAvaloniaBitmap(GetCompositedRawImage(i, 64, 64, null));
                    string caption = _parts.Count > 1 ? $"{_parts[p].Label} {i}" : null;
                    if (bmp != null) FrameThumbnails.Add(new FrameThumbnailViewModel(FrameThumbnails.Count, bmp, p, i, caption));
                }
            }
            if (_parts.Count > 0) Activate(keep);
            OnPropertyChanged(nameof(HasFrames));
        }

        private bool _thumbnailRefreshQueued;

        // Frames share tiles, so an edit to one pose can change others; one refresh per burst of strokes.
        private void QueueFrameThumbnailRefresh()
        {
            if (_thumbnailRefreshQueued || BankCount == 0) return;
            _thumbnailRefreshQueued = true;
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _thumbnailRefreshQueued = false;
                if (_tile == null || BankCount == 0) return;
                int keep = _activePart;
                foreach (FrameThumbnailViewModel thumb in FrameThumbnails)
                {
                    if (_parts.Count > 0) Activate(thumb.Part);
                    if (thumb.Local >= BankCount) continue;
                    global::Avalonia.Media.Imaging.Bitmap bmp = ImageConverter.ToAvaloniaBitmap(GetCompositedRawImage(thumb.Local, 64, 64, null));
                    if (bmp != null) thumb.Image = bmp;
                }
                if (_parts.Count > 0) Activate(keep);
            }, global::Avalonia.Threading.DispatcherPriority.Background);
        }

        // ── Mode A: per-frame cell geometry + composited canvas ────────────────
        private List<EditCell> CellsOf(int frameIndex)
        {
            List<EditCell> cells = new List<EditCell>();
            NcerFile.Cell bank = GetBank(frameIndex);
            int bpp = _tile.BitsPerPixel;
            // Laid in the order they are drawn, so a later cell is the one on top.
            foreach (int piece in CellRenderer.DrawOrder(bank))
            {
                NcerFile.Piece oam = bank.Pieces[piece];
                if (oam.Width == 0 || oam.Height == 0) continue;

                int byteStart = _sprite.CharacterOf(bank, oam) * 0x20;
                int byteLen = oam.Width * oam.Height * bpp / 8;
                if (byteStart < 0 || byteLen <= 0 || byteStart + byteLen > _tile.Tiles.Length)
                    continue; // malformed cell: skip so unrelated bytes aren't overwritten

                int bank_ = oam.Palette;
                if (bank_ >= _pal.Length) bank_ = 0; // matches CellRenderer's own clamp

                cells.Add(new EditCell
                {
                    Width = oam.Width,
                    Height = oam.Height,
                    DstX = CanvasSize / 2 + oam.X,
                    DstY = CanvasSize / 2 + oam.Y,
                    FlipX = oam.FlipH,
                    FlipY = oam.FlipV,
                    PaletteBank = bank_,
                    ByteStart = byteStart,
                    ByteLen = byteLen,
                });
            }

            return cells;
        }

        // A frame as palette indices on the canvas, cells drawn in order with 0 left see-through, as the game blits them.
        private int[] FrameIndices(int frameIndex)
        {
            int[] canvas = new int[CanvasSize * CanvasSize];
            foreach (EditCell c in CellsOf(frameIndex))
            {
                int[] idx = DecodeCell(c);
                for (int ly = 0; ly < c.Height; ly++)
                    for (int lx = 0; lx < c.Width; lx++)
                    {
                        int v = idx[ly * c.Width + lx];
                        if (v == 0) continue;
                        int x = c.DstX + (c.FlipX ? c.Width - 1 - lx : lx);
                        int y = c.DstY + (c.FlipY ? c.Height - 1 - ly : ly);
                        if (x >= 0 && x < CanvasSize && y >= 0 && y < CanvasSize) canvas[y * CanvasSize + x] = v;
                    }
            }
            return canvas;
        }

        private void LoadFrame(int frameIndex)
        {
            if (BankCount == 0 || frameIndex < 0 || frameIndex >= BankCount) return;

            _cells.Clear();
            _cells.AddRange(CellsOf(frameIndex));

            RebuildCompositedCanvas();

            // Default the palette strip to the first cell's bank so it's never empty, even before
            // the user has hovered/clicked anywhere.
            int firstBank = _cells.Count > 0 ? _cells[0].PaletteBank : 0;
            if (firstBank != _activePaletteBank)
                BuildPaletteSwatches(firstBank);
        }

        private void RebuildCompositedCanvas()
        {
            if (BankCount == 0 || _tile == null || _pal == null) return;
            RawImage raw = GetCompositedRawImage(_selectedFrameIndex, CanvasSize, CanvasSize, null);
            CanvasBitmap = ImageConverter.ToAvaloniaBitmap(ZoomRaw(raw, ZoomFactor));
        }

        private static DSPRE.RawImage ZoomRaw(DSPRE.RawImage src, int zoom)
        {
            if (zoom <= 1) return src;
            RawImage dst = new DSPRE.RawImage(src.Width * zoom, src.Height * zoom);
            for (int y = 0; y < src.Height; y++)
            {
                for (int x = 0; x < src.Width; x++)
                {
                    int si = (y * src.Width + x) * 4;
                    byte b = src.Bgra[si], g = src.Bgra[si + 1], r = src.Bgra[si + 2], a = src.Bgra[si + 3];
                    for (int dy = 0; dy < zoom; dy++)
                    {
                        int drow = (y * zoom + dy) * dst.Width;
                        for (int dx = 0; dx < zoom; dx++)
                        {
                            int di = (drow + x * zoom + dx) * 4;
                            dst.Bgra[di] = b; dst.Bgra[di + 1] = g; dst.Bgra[di + 2] = r; dst.Bgra[di + 3] = a;
                        }
                    }
                }
            }
            return dst;
        }

        /// Finds which cell owns composited-canvas pixel (x,y). Topmost drawn (last in draw order)
        /// non-transparent hit wins, matching what's visually on top; falls back to any cell whose
        /// bounds contain the point (even if transparent there) so painting into empty regions works.
        private EditCell HitTest(int x, int y)
        {
            for (int i = _cells.Count - 1; i >= 0; i--)
            {
                EditCell c = _cells[i];
                if (x < c.DstX || x >= c.DstX + c.Width || y < c.DstY || y >= c.DstY + c.Height) continue;
                CellLocal(c, x, y, out int lx, out int ly);
                if (ReadCellIndex(c, lx, ly) != 0) return c;
            }
            for (int i = _cells.Count - 1; i >= 0; i--)
            {
                EditCell c = _cells[i];
                if (x >= c.DstX && x < c.DstX + c.Width && y >= c.DstY && y < c.DstY + c.Height) return c;
            }
            return null;
        }

        private static void CellLocal(EditCell c, int x, int y, out int lx, out int ly)
        {
            int rawX = x - c.DstX, rawY = y - c.DstY;
            lx = c.FlipX ? c.Width - 1 - rawX : rawX;
            ly = c.FlipY ? c.Height - 1 - rawY : rawY;
        }

        private int[] DecodeCell(EditCell c)
        {
            byte[] slice = new byte[c.ByteLen];
            Array.Copy(_tile.Tiles, c.ByteStart, slice, 0, c.ByteLen);
            byte[] raster = _tile.Tiled ? TilesToRows(slice, c.Width, c.Height, _tile.BitsPerPixel) : slice;
            return UnpackIndices(raster, c.Width, c.Height, _tile.BitsPerPixel);
        }

        private void EncodeCell(EditCell c, int[] indices)
        {
            byte[] raster = PackIndices(indices, c.Width, c.Height, _tile.BitsPerPixel);
            byte[] native = _tile.Tiled ? RowsToTiles(raster, c.Width, c.Height, _tile.BitsPerPixel) : raster;
            Array.Copy(native, 0, _tile.Tiles, c.ByteStart, c.ByteLen);
        }

        private int ReadCellIndex(EditCell c, int lx, int ly)
        {
            if (lx < 0 || lx >= c.Width || ly < 0 || ly >= c.Height) return 0;
            return DecodeCell(c)[ly * c.Width + lx];
        }

        private static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
        private static string Frames(int n) => n == 1 ? "1 frame" : $"{n} frames";

        // ── Palette colours ─────────────────────────────────────────────────────
        private string _palPath;
        private bool _paletteDirty;

        /// <summary>The palette the swatches show, which follows the part of the sprite last painted.</summary>
        public int ActivePaletteBank => Math.Max(0, _activePaletteBank);

        public string PaletteTitle(int bank, int index) => $"{Capital(_set.Noun)} {_trClassID}, palette {bank}, colour {index}";

        public uint SwatchColor(int bank, int index)
        {
            System.Drawing.Color[] pal = _pal != null && bank >= 0 && bank < _pal.Length ? _pal[bank] : null;
            if (pal == null || index < 0 || index >= pal.Length) return 0xFF000000u;
            return 0xFF000000u | ((uint)pal[index].R << 16) | ((uint)pal[index].G << 8) | pal[index].B;
        }

        /// <summary>Sets a palette colour, rounded to 5 bits a channel.</summary>
        /// <param name="group">Changes passed the same group are one undo step.</param>
        public void SetSwatchColor(int bank, int index, uint argb, object group = null)
        {
            System.Drawing.Color[] pal = _pal != null && bank >= 0 && bank < _pal.Length ? _pal[bank] : null;
            if (pal == null || index < 0 || index >= pal.Length) return;
            int Channel(int shift) => (int)((argb >> shift) & 0xF8);
            System.Drawing.Color color = System.Drawing.Color.FromArgb(Channel(16), Channel(8), Channel(0));
            if (pal[index].ToArgb() == color.ToArgb()) return;
            pal[index] = color;
            foreach (SpritePart part in _parts)
                if (part.Pal != _pal && bank < part.Pal.Length && index < part.Pal[bank].Length)
                    part.Pal[bank][index] = color;
            _paletteDirty = true;
            _editGroup = group;
            HasUnsavedChanges = true;
            if (bank == ActivePaletteBank) BuildPaletteSwatches(bank);
            if (BankCount > 0) { RebuildCompositedCanvas(); BuildFrameThumbnails(); RebuildTopBar(); } else RebuildFlatCanvas();
        }

        private void BuildPaletteSwatches(int bankIndex)
        {
            _activePaletteBank = bankIndex;
            PaletteSwatches.Clear();
            System.Drawing.Color[] pal = _pal[bankIndex];
            int keep = SelectedSwatchIndex;
            for (int i = 0; i < pal.Length; i++)
                PaletteSwatches.Add(new PaletteSwatchViewModel(i, pal[i]));
            SelectedSwatchIndex = keep >= 0 && keep < pal.Length ? keep : 0;
        }

        // ── Mode B: flat tile-sheet fallback (DP, no NCER) ─────────────────────
        private void LoadFlatSheet()
        {
            _flatWidth = _tile.Width;
            _flatHeight = _tile.Height;

            _scrambleSeed = null;
            if (TrainerGraphicsLayout.PixelsAreScrambled && _tile.Tiles != null)
            {
                byte[] pixels = (byte[])_tile.Tiles.Clone();
                _scrambleSeed = SpriteScrambling.Seed(pixels, 0, pixels.Length);
                SpriteScrambling.Unscramble(pixels, 0, pixels.Length);
                _tile.SetTiles(pixels);
            }

            byte[] rasterBytes = _tile.Tiled ? TilesToRows(_tile.Tiles, _flatWidth, _flatHeight, _tile.BitsPerPixel) : _tile.Tiles;
            _flatIndices = UnpackIndices(rasterBytes, _flatWidth, _flatHeight, _tile.BitsPerPixel);

            BuildPaletteSwatches(0);
            RebuildFlatCanvas();
        }

        private void RebuildFlatCanvas()
        {
            RawImage raw = new DSPRE.RawImage(_flatWidth, _flatHeight);
            System.Drawing.Color[] pal = _pal[0];
            for (int y = 0; y < _flatHeight; y++)
                for (int x = 0; x < _flatWidth; x++)
                {
                    // Colour 0 is the background: kept in the file, shown see-through like the cell sprites.
                    int index = _flatIndices[y * _flatWidth + x];
                    System.Drawing.Color c = ColorAt(pal, index);
                    raw.SetPixel(x, y, c.R, c.G, c.B, index == 0 ? (byte)0 : (byte)255);
                }
            CanvasBitmap = ImageConverter.ToAvaloniaBitmap(ZoomRaw(raw, ZoomFactor));
        }

        private static System.Drawing.Color ColorAt(System.Drawing.Color[] pal, int index) =>
            index >= 0 && index < pal.Length ? pal[index] : System.Drawing.Color.Black;

        // ── Pointer interaction (canvas coordinates, already un-zoomed by the view) ────────────────
        public void HandlePointer(int x, int y)
        {
            if (BankCount > 0) HandlePointerComposited(x, y);
            else HandlePointerFlat(x, y);
        }

        private void HandlePointerComposited(int x, int y)
        {
            if (x < 0 || x >= CanvasSize || y < 0 || y >= CanvasSize) return;
            EditCell cell = HitTest(x, y);
            if (cell == null) return;

            if (cell.PaletteBank != _activePaletteBank)
                BuildPaletteSwatches(cell.PaletteBank);

            CellLocal(cell, x, y, out int lx, out int ly);
            if (lx < 0 || lx >= cell.Width || ly < 0 || ly >= cell.Height) return;

            if (SelectedTool == SpriteEditTool.Eyedropper)
            {
                SelectedSwatchIndex = DecodeCell(cell)[ly * cell.Width + lx];
                SelectedTool = SpriteEditTool.Pencil;
                return;
            }

            int[] indices = DecodeCell(cell);
            int pos = ly * cell.Width + lx;
            if (indices[pos] == SelectedSwatchIndex) return;
            indices[pos] = SelectedSwatchIndex;
            EncodeCell(cell, indices);

            RebuildCompositedCanvas();
            QueueFrameThumbnailRefresh();
            RebuildTopBar();
            HasUnsavedChanges = true;
        }

        private void HandlePointerFlat(int x, int y)
        {
            if (_flatIndices == null || x < 0 || x >= _flatWidth || y < 0 || y >= _flatHeight) return;

            if (SelectedTool == SpriteEditTool.Eyedropper)
            {
                SelectedSwatchIndex = _flatIndices[y * _flatWidth + x];
                SelectedTool = SpriteEditTool.Pencil;
                return;
            }

            int pos = y * _flatWidth + x;
            if (_flatIndices[pos] == SelectedSwatchIndex) return;
            _flatIndices[pos] = SelectedSwatchIndex;
            RebuildFlatCanvas();
            HasUnsavedChanges = true;
        }

        // ── Import / Export PNG ────────────────────────────────────────────────
        /// <summary>Imports a PNG the size of the canvas, checking each pixel against the palette bank of the
        /// cell a click there would hit. Returns null on success, otherwise the error.</summary>
        public string ImportPng(string filePath)
        {
            if (_tile == null) return "No sprite loaded.";
            try
            {
                DSPRE.RawImage import;
                using (FileStream fs = File.OpenRead(filePath))
                    import = ImageConverter.DecodeRawImage(fs);
                if (import == null) return "Image couldn't be decoded.";

                return BankCount > 0 ? ImportPngComposited(import) : ImportPngFlat(import);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private string ImportPngComposited(DSPRE.RawImage import)
        {
            if (import.Width != CanvasSize || import.Height != CanvasSize)
                return $"The PNG is {import.Width}×{import.Height} but the canvas is {CanvasSize}×{CanvasSize}. Export first to get a picture of the right size.";

            Dictionary<int, Dictionary<int, int>> lookups = new Dictionary<int, Dictionary<int, int>>();
            Dictionary<int, int> LookupFor(int bank)
            {
                if (lookups.TryGetValue(bank, out Dictionary<int, int> d)) return d;
                d = new Dictionary<int, int>();
                System.Drawing.Color[] pal = _pal[bank];
                for (int i = 0; i < pal.Length; i++)
                {
                    int key = (pal[i].R << 16) | (pal[i].G << 8) | pal[i].B;
                    if (!d.ContainsKey(key)) d[key] = i;
                }
                lookups[bank] = d;
                return d;
            }

            Dictionary<EditCell, int[]> perCell = new Dictionary<EditCell, int[]>();
            for (int y = 0; y < CanvasSize; y++)
            {
                for (int x = 0; x < CanvasSize; x++)
                {
                    EditCell cell = HitTest(x, y);
                    if (cell == null) continue; // background area, no cell to write into, ignore

                    if (!perCell.TryGetValue(cell, out int[] idxArr))
                        idxArr = perCell[cell] = DecodeCell(cell);

                    int i = (y * CanvasSize + x) * 4;
                    int key = (import.Bgra[i + 2] << 16) | (import.Bgra[i + 1] << 8) | import.Bgra[i];
                    int idx = 0;
                    if (!IsTransparent(import, i) && !LookupFor(cell.PaletteBank).TryGetValue(key, out idx))
                        return $"Pixel ({x},{y}) isn't one of that area's {_pal[cell.PaletteBank].Length} palette colours (bank {cell.PaletteBank}). Match the colours exactly, or paint it with the pencil.";

                    CellLocal(cell, x, y, out int lx, out int ly);
                    idxArr[ly * cell.Width + lx] = idx;
                }
            }

            foreach (KeyValuePair<EditCell, int[]> kv in perCell) EncodeCell(kv.Key, kv.Value);
            RebuildCompositedCanvas();
            QueueFrameThumbnailRefresh();
            RebuildTopBar();
            HasUnsavedChanges = true;
            return null;
        }

        /// <summary>
        /// Paints the selected frame over the same frame of the other set. Returns an error, or null;
        /// pixels the other pose has no cell under are reported in the status line.
        /// </summary>
        public string CopyFrameToOtherSet()
        {
            if (_parts.Count < 2 || _selectedFrameIndex < 0) return "There is no other set to copy to.";
            int local = _selectedFrameIndex, from = _activePart;
            int[] source = FrameIndices(local);
            bool[] covered = new bool[source.Length];
            Activate(1 - from);
            try
            {
                if (local >= BankCount) return $"The other set has no frame {local}.";
                foreach (EditCell c in CellsOf(local))
                {
                    int[] indices = new int[c.Width * c.Height];
                    for (int ly = 0; ly < c.Height; ly++)
                        for (int lx = 0; lx < c.Width; lx++)
                        {
                            int x = c.DstX + (c.FlipX ? c.Width - 1 - lx : lx);
                            int y = c.DstY + (c.FlipY ? c.Height - 1 - ly : ly);
                            if (x < 0 || x >= CanvasSize || y < 0 || y >= CanvasSize) continue;
                            indices[ly * c.Width + lx] = source[y * CanvasSize + x];
                            covered[y * CanvasSize + x] = true;
                        }
                    EncodeCell(c, indices);
                }
            }
            finally { Activate(from); }

            int left = 0;
            for (int i = 0; i < source.Length; i++) if (source[i] != 0 && !covered[i]) left++;
            QueueFrameThumbnailRefresh();
            HasUnsavedChanges = true;
            StatusText = left == 0
                ? $"Copied frame {local} to {_parts[1 - from].Label.ToLowerInvariant()}."
                : $"Copied frame {local} to {_parts[1 - from].Label.ToLowerInvariant()}; {left} pixels fall outside that pose and were left out.";
            return null;
        }

        // Export writes colour 0 as see-through pixels whose RGB is black, which would otherwise match a real black.
        private static bool IsTransparent(DSPRE.RawImage image, int i) => image.Bgra[i + 3] < 128;

        private string ImportPngFlat(DSPRE.RawImage import)
        {
            if (import.Width != _flatWidth || import.Height != _flatHeight)
                return $"The PNG is {import.Width}×{import.Height} but the sprite sheet is {_flatWidth}×{_flatHeight}.";

            System.Drawing.Color[] pal = _pal[0];
            Dictionary<int, int> lookup = new Dictionary<int, int>();
            for (int i = 0; i < pal.Length; i++)
            {
                int key = (pal[i].R << 16) | (pal[i].G << 8) | pal[i].B;
                if (!lookup.ContainsKey(key)) lookup[key] = i;
            }

            int[] newIndices = new int[_flatWidth * _flatHeight];
            for (int y = 0; y < _flatHeight; y++)
            {
                for (int x = 0; x < _flatWidth; x++)
                {
                    int i = (y * _flatWidth + x) * 4;
                    int key = (import.Bgra[i + 2] << 16) | (import.Bgra[i + 1] << 8) | import.Bgra[i];
                    int idx = 0;
                    if (!IsTransparent(import, i) && !lookup.TryGetValue(key, out idx))
                        return $"Pixel ({x},{y}) isn't one of this sprite's {pal.Length} palette colours. " +
                               "Match the PNG to the palette exactly, or paint it with the pencil.";
                    newIndices[y * _flatWidth + x] = idx;
                }
            }

            _flatIndices = newIndices;
            RebuildFlatCanvas();
            HasUnsavedChanges = true;
            return null;
        }

        public bool ExportPng(string filePath)
        {
            try
            {
                DSPRE.RawImage raw;
                if (BankCount > 0)
                {
                    // The file keeps colour 0 as a real colour; only the editor shows it see-through.
                    raw = GetCompositedRawImage(_selectedFrameIndex, CanvasSize, CanvasSize, null, trans: false);
                    System.Drawing.Color bg = ColorAt(_pal[0], 0);
                    for (int y = 0; y < CanvasSize; y++)
                        for (int x = 0; x < CanvasSize; x++)
                            if (raw.Bgra[(y * CanvasSize + x) * 4 + 3] == 0) raw.SetPixel(x, y, bg.R, bg.G, bg.B, 255);
                }
                else
                {
                    if (_flatIndices == null) return false;
                    raw = new DSPRE.RawImage(_flatWidth, _flatHeight);
                    System.Drawing.Color[] pal = _pal[0];
                    for (int y = 0; y < _flatHeight; y++)
                        for (int x = 0; x < _flatWidth; x++)
                        {
                            System.Drawing.Color c = ColorAt(pal, _flatIndices[y * _flatWidth + x]);
                            raw.SetPixel(x, y, c.R, c.G, c.B, 255);
                        }
                }
                ImageConverter.ToAvaloniaBitmap(raw).Save(filePath, PngBitmapEncoderOptions.Default);
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error("TrainerSpriteEditorViewModel.ExportPng failed: " + ex.Message);
                return false;
            }
        }

        // ── Naming screen ──────────────────────────────────────────────────────
        // The drawing and cells are stored packed: they are opened in memory and the drawing is packed back on save.
        private string _packedTilesPath;
        private byte _packedTilesMarker;
        private NanrFile _namingAnimations;
        private byte[] _topBarBackground;

        private string LoadNamingIcon(int animation)
        {
            try
            {
                string dir = RomInfo.GameDirs[_set.Archive].unpackedDir;
                string File4(int i) => Path.Combine(dir, i.ToString("D4"));

                int files = DSPRE.ROMFiles.NamingScreenIcons.SpriteTilesFile;
                _packedTilesPath = File4(files);
                byte[] packedTiles = File.ReadAllBytes(_packedTilesPath);
                _packedTilesMarker = Data.GraphicAssets.SqueezeMarker(packedTiles);
                _tilesPath = _packedTilesPath;
                _tile = ReadDrawing(Data.GraphicAssets.Unsqueeze(packedTiles));
                int colours = DSPRE.ROMFiles.NamingScreenIcons.SpriteColoursFile;
                _palPath = File4(colours);
                _pal = ReadPaletteRows(File.ReadAllBytes(_palPath));
                int cells = DSPRE.ROMFiles.NamingScreenIcons.SpriteCellsFile;
                _sprite = ReadCells(Data.GraphicAssets.Unsqueeze(File.ReadAllBytes(File4(cells))));
                _cellsFromSource = false;
                _sourcePngPath = null;
                _namingAnimations = NanrFile.Read(Data.GraphicAssets.Unsqueeze(File.ReadAllBytes(File4(DSPRE.ROMFiles.NamingScreenIcons.SpriteAnimationsFile))));
                _bankMap = FramesOfAnimation(animation);

                _parts.Clear();
                _parts.Add(new SpritePart { Entry = animation, Tile = _tile, Pal = _pal, Sprite = _sprite, TilesPath = _tilesPath, PalPath = _palPath });
                Activate(0);
                OnPropertyChanged(nameof(HasLinkedSets));
                OnPropertyChanged(nameof(SelectedClassIndex));

                ZoomFactor = 4;
                BuildFrameThumbnails();
                _activePaletteBank = -1;
                _selectedStripFrame = -1;
                SelectedFrameIndex = 0;
                OnPropertyChanged(nameof(IsFlatSheetMode));
                OnPropertyChanged(nameof(FrameCount));
                OnPropertyChanged(nameof(CanUseSheets));
                OnPropertyChanged(nameof(CanUseAnimationSheets));
                HasUnsavedChanges = false;
                _paletteDirty = false;
                ResetSteps();
                StatusText = Frames(FrameCount);
                _topBarFrame = 0; _topBarHold = 0;
                BuildTopBarBackground(dir);
                RebuildTopBar();
                StartTopBar();
                return null;
            }
            catch (Exception ex)
            {
                _tile = null; _pal = null; _sprite = null;
                StatusText = "Couldn't load: " + ex.Message;
                AppLogger.Error("TrainerSpriteEditorViewModel.LoadNamingIcon failed: " + ex.Message);
                return ex.Message;
            }
        }

        // Each cell the animation shows, once, in the order it first appears.
        private int[] FramesOfAnimation(int animation)
        {
            List<int> seen = new List<int>();
            if (_namingAnimations != null && animation < _namingAnimations.Sequences.Count)
                for (int f = 0; f < _namingAnimations.Sequences[animation].Frames.Count; f++)
                {
                    int cell = _namingAnimations.CellOf(animation, f);
                    if (cell >= 0 && cell < _sprite.Cells.Count && !seen.Contains(cell)) seen.Add(cell);
                }
            if (seen.Count == 0) seen.Add(0);
            return seen.ToArray();
        }

        public bool HasTopBar => _set.NamingScreen;
        public bool ShowsAnimationsTab => !_set.NamingScreen;

        private Bitmap _topBarPreview;
        public Bitmap TopBarPreview { get => _topBarPreview; private set => Set(ref _topBarPreview, value); }

        private string _sampleName = "Name";
        public string SampleName
        {
            get => _sampleName;
            set { if (Set(ref _sampleName, value ?? "")) RebuildTopBar(); }
        }

        private void BuildTopBarBackground(string dir)
        {
            _topBarBackground = null;
            try
            {
                byte[] Read(int i) => Data.GraphicAssets.Unsqueeze(File.ReadAllBytes(Path.Combine(dir, i.ToString("D4"))));
                NitroBgCodec.BgImage bg = NitroBgCodec.Composite(
                    Read(DSPRE.ROMFiles.NamingScreenIcons.BackgroundTilesFile),
                    Read(DSPRE.ROMFiles.NamingScreenIcons.BackgroundColoursFile),
                    Read(DSPRE.ROMFiles.NamingScreenIcons.TopScreenMapFile), transparentZero: false);
                if (bg != null && bg.Width == Data.DsBgScreen.Width && bg.Height >= TopBarHeight) _topBarBackground = bg.Rgba;
            }
            catch (Exception ex) { AppLogger.Error("Naming screen top bar: " + ex.Message); }
        }

        private const int TopBarHeight = 48;
        private int _topBarFrame, _topBarHold;
        private global::Avalonia.Threading.DispatcherTimer _topBarTimer;
        private DSPRE.ROMFiles.FieldFont _topBarFont;

        private void StartTopBar()
        {
            if (_topBarTimer != null) return;
            _topBarTimer = new global::Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.0 / 60) };
            _topBarTimer.Tick += (_, _) => StepTopBar();
            _topBarTimer.Start();
        }

        public void StopTopBar() { _topBarTimer?.Stop(); _topBarTimer = null; }

        private void StepTopBar()
        {
            if (_namingAnimations == null || _trClassID >= _namingAnimations.Sequences.Count) return;
            List<NanrFile.Frame> frames = _namingAnimations.Sequences[_trClassID].Frames;
            if (frames.Count < 2) return;
            if (++_topBarHold < Math.Max(1, (int)frames[_topBarFrame].Delay)) return;
            _topBarHold = 0;
            _topBarFrame = (_topBarFrame + 1) % frames.Count;
            RebuildTopBar();
        }

        // The top of the naming screen as the game draws it: its own background, the icon playing in place,
        // an underline for each letter and the sample name in the ROM's font.
        private void RebuildTopBar()
        {
            if (!_set.NamingScreen || _sprite == null) return;
            int w = Data.DsBgScreen.Width, h = Data.DsBgScreen.Height;
            byte[] rgba = new byte[w * h * 4];
            if (_topBarBackground != null) Array.Copy(_topBarBackground, rgba, rgba.Length);
            else for (int i = 3; i < rgba.Length; i += 4) rgba[i] = 255;

            IReadOnlyList<NamingScreenIcons.Icon> icons = DSPRE.ROMFiles.NamingScreenIcons.All;
            int letters = icons.FirstOrDefault(i => i.Animation == _trClassID)?.Letters ?? 7;
            int slotCell = _namingAnimations?.CellOf(DSPRE.ROMFiles.NamingScreenIcons.LetterSlotAnimation, 0) ?? -1;
            for (int i = 0; i < letters && slotCell >= 0; i++)
                BlitCell(rgba, slotCell, DSPRE.ROMFiles.NamingScreenIcons.NameX + i * DSPRE.ROMFiles.NamingScreenIcons.LetterStep, DSPRE.ROMFiles.NamingScreenIcons.NameY);

            int iconCell = _namingAnimations?.CellOf(_trClassID, _topBarFrame) ?? -1;
            bool gender = DSPRE.ROMFiles.NamingScreenIcons.IsGender(_trClassID);
            if (iconCell >= 0)
                BlitCell(rgba, iconCell,
                    gender ? DSPRE.ROMFiles.NamingScreenIcons.GenderX : DSPRE.ROMFiles.NamingScreenIcons.IconX,
                    gender ? DSPRE.ROMFiles.NamingScreenIcons.GenderY : DSPRE.ROMFiles.NamingScreenIcons.IconY);

            try { _topBarFont ??= DSPRE.ROMFiles.FieldFont.LoadSystemFont(); } catch (Exception ex) { AppLogger.Warn("Trainer sprite editor: system font: " + ex.Message); }
            for (int i = 0; i < _sampleName.Length && i < letters; i++)
            {
                string ch = _sampleName[i].ToString();
                int x = DSPRE.ROMFiles.NamingScreenIcons.NameX + i * DSPRE.ROMFiles.NamingScreenIcons.LetterStep - Data.DsBgScreen.MeasureText(_topBarFont, ch) / 2;
                Data.DsBgScreen.DrawText(rgba, _topBarFont, ch, x, DSPRE.ROMFiles.NamingScreenIcons.NameY - 15, 0x294A, 0x5EF7);
            }

            byte[] bgra = new byte[w * TopBarHeight * 4];
            for (int i = 0; i < bgra.Length; i += 4)
            {
                bgra[i] = rgba[i + 2]; bgra[i + 1] = rgba[i + 1]; bgra[i + 2] = rgba[i]; bgra[i + 3] = 255;
            }
            TopBarPreview = ImageConverter.ToAvaloniaBitmap(new DSPRE.RawImage(w, TopBarHeight, bgra));
        }

        // Draws a whole cell (any of the archive's, not just this icon's frames) with its origin at x, y.
        private void BlitCell(byte[] rgba, int cell, int x, int y)
        {
            if (cell < 0 || cell >= _sprite.Cells.Count) return;
            RawImage raw = CellRenderer.Draw(_sprite, cell, _tile.ToBytes(), Bgr555(_pal), CanvasSize, CanvasSize);
            int w = Data.DsBgScreen.Width, h = Data.DsBgScreen.Height;
            for (int py = 0; py < CanvasSize; py++)
                for (int px = 0; px < CanvasSize; px++)
                {
                    int s = (py * CanvasSize + px) * 4;
                    if (raw.Bgra[s + 3] == 0) continue;
                    int X = x - CanvasSize / 2 + px, Y = y - CanvasSize / 2 + py;
                    if (X < 0 || Y < 0 || X >= w || Y >= h) continue;
                    int d = (Y * w + X) * 4;
                    rgba[d] = raw.Bgra[s + 2]; rgba[d + 1] = raw.Bgra[s + 1]; rgba[d + 2] = raw.Bgra[s]; rgba[d + 3] = 255;
                }
        }

        // The scan copy (file 4) is two 80x80 poses for the Hall of Fame and the link rosters, scrambled like a
        // Pokemon sprite. Each half only takes the pixels edited in the frame it was drawn from.
        private const int ScanFrame = 80;

        private sealed class ScanCopy
        {
            public string Path;
            public byte[] File;
            public int DataOff, Size;
            public bool FromEnd;
            public byte[] Pixels;   // unscrambled, two pixels a byte
        }

        private ScanCopy ReadScan(int entry, out string error)
        {
            error = null;
            int id = TrainerGraphicsLayout.ScanEntry(entry);
            if (id < 0) return null;
            string path = Path.Combine(RomInfo.GameDirs[_set.Archive].unpackedDir, id.ToString("D4"));
            if (!File.Exists(path)) return null;

            byte[] file = File.ReadAllBytes(path);
            if (!(NitroBgCodec.ReadDrawingHeader(file) is NitroBgCodec.DrawingHeader head)) { error = "the two-frame copy of the sprite has no pixels"; return null; }
            int size = head.DataSize, dataOff = head.DataAt;
            int width = ScanFrame * 2, height = ScanFrame;
            if (head.TilesAcross * 8 != width || head.TilesDown * 8 != height || head.EightBit || size != width * height / 2)
            {
                error = $"the two-frame copy of the sprite isn't the usual {width}x{height} 16-colour picture";
                return null;
            }

            bool fromEnd = TrainerGraphicsLayout.ScanScrambledFromEnd;
            byte[] plain = (byte[])file.Clone();
            SpriteScrambling.Unscramble(plain, dataOff, size, fromEnd);
            byte[] pixels = new byte[size];
            Array.Copy(plain, dataOff, pixels, 0, size);
            return new ScanCopy { Path = path, File = file, DataOff = dataOff, Size = size, FromEnd = fromEnd, Pixels = pixels };
        }

        private static int ScanPixel(byte[] pixels, int pos) => (pos & 1) == 0 ? pixels[pos >> 1] & 0xF : pixels[pos >> 1] >> 4;

        private sealed class ScanHalf
        {
            public int Frame;
            public int[] Base;
        }

        // The middle 80x80 of a drawn frame, colour index within its palette.
        private static int[] ScanCrop(int[] canvas)
        {
            int margin = (CanvasSize - ScanFrame) / 2;
            int[] crop = new int[ScanFrame * ScanFrame];
            for (int y = 0; y < ScanFrame; y++)
                for (int x = 0; x < ScanFrame; x++)
                    crop[y * ScanFrame + x] = canvas[(y + margin) * CanvasSize + x + margin] & 0xF;
            return crop;
        }

        /// <summary>For each part, ties each scan half to the frame it is closest to. Run before any edit.</summary>
        private void MapScanFrames()
        {
            if (_set.NamingScreen) return;
            int keep = _activePart;
            for (int p = 0; p < _parts.Count; p++)
            {
                Activate(p);
                ScanCopy scan = BankCount > 0 ? ReadScan(_parts[p].Entry, out _) : null;
                ScanHalf[] halves = new ScanHalf[2];
                if (scan != null)
                {
                    try
                    {
                        TrainerSpriteFrames frames = OpenFrames(p);
                        List<int[]> crops = Enumerable.Range(0, frames.FrameCount).Select(f => ScanCrop(frames.Draw(f))).ToList();
                        for (int half = 0; half < 2; half++)
                        {
                            int inked = 0, best = int.MaxValue, bestFrame = -1;
                            for (int i = 0; i < ScanFrame * ScanFrame; i++)
                                if (ScanPixel(scan.Pixels, (i / ScanFrame) * ScanFrame * 2 + half * ScanFrame + i % ScanFrame) != 0) inked++;
                            for (int f = 0; f < crops.Count; f++)
                            {
                                int diff = 0;
                                for (int i = 0; i < ScanFrame * ScanFrame; i++)
                                    if (crops[f][i] != ScanPixel(scan.Pixels, (i / ScanFrame) * ScanFrame * 2 + half * ScanFrame + i % ScanFrame)) diff++;
                                if (diff < best) { best = diff; bestFrame = f; }
                            }
                            // A half further off than this is its own drawing and is never redrawn.
                            if (inked > 0 && bestFrame >= 0 && best <= inked / 20)
                                halves[half] = new ScanHalf { Frame = bestFrame, Base = crops[bestFrame] };
                        }
                    }
                    catch (Exception ex) { AppLogger.Error("TrainerSpriteEditorViewModel: scan copy couldn't be matched: " + ex.Message); }
                }
                _parts[p].Scan = halves;
                AppLogger.Debug($"Trainer sprite {_parts[p].Entry}: scan halves follow frames {halves[0]?.Frame ?? -1} and {halves[1]?.Frame ?? -1}.");
            }
            Activate(keep);
        }

        private string WriteScan(int entry)
        {
            ScanCopy scan = ReadScan(entry, out string error);
            if (scan == null) return error;
            ScanHalf[] halves = _parts.Count > 0 ? _parts[_activePart].Scan : null;
            if (halves == null) return null;

            TrainerSpriteFrames frames = OpenFrames(_activePart);
            byte[] pixels = (byte[])scan.Pixels.Clone();
            int[][] now = new int[2][];
            for (int half = 0; half < 2; half++)
            {
                ScanHalf h = halves[half];
                if (h == null || h.Frame >= frames.FrameCount) continue;
                now[half] = ScanCrop(frames.Draw(h.Frame));
                for (int i = 0; i < now[half].Length; i++)
                {
                    if (now[half][i] == h.Base[i]) continue;
                    int pos = (i / ScanFrame) * ScanFrame * 2 + half * ScanFrame + i % ScanFrame, v = now[half][i];
                    pixels[pos >> 1] = (byte)((pos & 1) == 0 ? (pixels[pos >> 1] & 0xF0) | v : (pixels[pos >> 1] & 0x0F) | (v << 4));
                }
            }
            if (!pixels.AsSpan().SequenceEqual(scan.Pixels))
            {
                ushort seed = SpriteScrambling.Seed(scan.File, scan.DataOff, scan.Size, scan.FromEnd);
                byte[] file = (byte[])scan.File.Clone();
                Array.Copy(pixels, 0, file, scan.DataOff, scan.Size);
                SpriteScrambling.Scramble(file, scan.DataOff, scan.Size, seed, scan.FromEnd);
                ArchiveMembers.Write(scan.Path, file);
            }
            for (int half = 0; half < 2; half++)
                if (now[half] != null) halves[half].Base = now[half];
            return null;
        }

        // hg-engine builds the scan copy from NNN_enc.png, reading only its pixel numbers.
        private string WriteScanPng(int entry, string png)
        {
            if (!File.Exists(png)) return null;
            ScanCopy scan = ReadScan(entry, out string error);
            if (scan == null) return error;
            byte[] file = File.ReadAllBytes(png);
            if (!IndexedPng.TryRead(file, out byte[] old, out uint[] colours, out int w, out int h)) return $"{Path.GetFileName(png)} isn't an indexed PNG.";
            if (w != ScanFrame * 2 || h != ScanFrame) return $"{Path.GetFileName(png)} is {w}x{h}, not the {ScanFrame * 2}x{ScanFrame} hg-engine builds the scan copy from.";
            byte[] indices = new byte[w * h];
            for (int i = 0; i < indices.Length; i++) indices[i] = (byte)ScanPixel(scan.Pixels, i);
            if (indices.AsSpan().SequenceEqual(old)) return null;
            PlainFiles.WriteBytes(png, IndexedPng.Write(indices, colours, w, h, file.Length > 24 && file[24] == 4 ? 4 : 8));
            return null;
        }

        // ── Sprite sheets ─────────────────────────────────────────────────────
        public bool CanUseSheets => _tile != null && !_set.NamingScreen && !HgEngineProject.IsActive && !_cellsFromSource;
        public bool CanUseAnimationSheets => CanUseSheets && !IsFlatSheetMode;

        private string EntryPath(int entry) => Path.Combine(RomInfo.GameDirs[_set.Archive].unpackedDir, entry.ToString("D4"));

        private Data.TrainerSpriteFrames OpenFrames(int part)
        {
            SpritePart p = _parts[part];
            TrainerSpriteFrames frames = Data.TrainerSpriteFrames.Read(
                p.Ncgr ?? File.ReadAllBytes(p.TilesPath),
                p.Ncer ?? File.ReadAllBytes(EntryPath(TrainerGraphicsLayout.CellsEntry(p.Entry))),
                p.Nanr ?? File.ReadAllBytes(EntryPath(TrainerGraphicsLayout.AnimationEntry(p.Entry))), out string why);
            if (frames == null) throw new InvalidOperationException(why);
            frames.ReplaceTiles(p.Tile.Tiles);
            return frames;
        }

        private Data.ISheetFrames OpenSheetFrames(int part) =>
            IsFlatSheetMode ? new FlatFrames((int[])_flatIndices.Clone(), _flatWidth, _flatHeight) : OpenFrames(part);

        private int SheetPartCount => IsFlatSheetMode ? 1 : Math.Max(1, _parts.Count);

        // Only the palettes the pieces use.
        private uint[][] SheetPalettes(IEnumerable<Data.ISheetFrames> frames)
        {
            int used = 1;
            foreach (ISheetFrames f in frames)
                for (int i = 0; i < f.FrameCount; i++)
                    foreach (int mask in f.PalettesOf(i))
                        for (int b = 0; b < 16; b++) if ((mask & (1 << b)) != 0) used = Math.Max(used, b + 1);
            used = Math.Min(used, _pal.Length);
            uint[][] result = new uint[used][];
            for (int b = 0; b < used; b++)
                result[b] = Enumerable.Range(0, 16).Select(i => SwatchColor(b, i)).ToArray();
            return result;
        }

        public string SheetFileName(int? animation) =>
            $"{_set.Noun} {_set.FileStem(_trClassID)} {(animation == null ? "frames" : (SheetLabel(_activePart) is string set ? set.ToLowerInvariant() + " " : "") + "animation " + animation)}.png";

        private string SheetLabel(int part) => SheetPartCount > 1 ? _parts[part].Label : null;

        /// <summary>One row per set. Returns why not, or null.</summary>
        public string ExportFramesSheet(string path)
        {
            try
            {
                List<ISheetFrames> sets = Enumerable.Range(0, SheetPartCount).Select(OpenSheetFrames).ToList();
                List<IReadOnlyList<TrainerSpriteSheet.Cell>> rows = sets.Select(f => (IReadOnlyList<Data.TrainerSpriteSheet.Cell>)Enumerable.Range(0, f.FrameCount)
                    .Select(i => new Data.TrainerSpriteSheet.Cell(f.Draw(i))).ToList()).ToList();
                (byte[] px, uint[] colours, int w, int h) = Data.TrainerSpriteSheet.Compose(rows, SheetPalettes(sets));
                PlainFiles.WriteBytes(path, IndexedPng.Write(px, colours, w, h));

                TrainerSpriteSheet.FramesFile file = new Data.TrainerSpriteSheet.FramesFile();
                for (int p = 0; p < sets.Count; p++)
                    file.Rows.Add(new Data.TrainerSpriteSheet.FramesRow
                    {
                        Set = SheetLabel(p),
                        Frames = Enumerable.Range(0, sets[p].FrameCount).Select(i => new Data.TrainerSpriteSheet.FrameJson { Frame = i, Blank = sets[p].IsBlank(i) }).ToList(),
                    });
                PlainFiles.WriteText(Path.ChangeExtension(path, ".json"), Data.TrainerSpriteSheet.WriteFrames(file));
                SaveNotice.Exported(path);
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        private const int AnimationSheetColumns = 8;

        public string ExportAnimationSheet(int animation, string path)
        {
            try
            {
                TrainerSpriteFrames frames = OpenFrames(_activePart);
                IReadOnlyList<TrainerSpriteFrames.Step> steps = frames.StepsOf(animation);
                if (steps.Count == 0) return $"This sprite has no animation {animation}.";
                List<TrainerSpriteSheet.Cell> cells = steps.Select(st => new Data.TrainerSpriteSheet.Cell(frames.Draw(st.Frame))).ToList();
                List<IReadOnlyList<TrainerSpriteSheet.Cell>> rows = cells.Chunk(AnimationSheetColumns).Select(r => (IReadOnlyList<Data.TrainerSpriteSheet.Cell>)r).ToList();
                (byte[] px, uint[] colours, int w, int h) = Data.TrainerSpriteSheet.Compose(rows, SheetPalettes(new[] { frames }));
                PlainFiles.WriteBytes(path, IndexedPng.Write(px, colours, w, h));

                bool shifts = frames.SequenceShifts(animation);
                TrainerSpriteSheet.StepsFile file = new Data.TrainerSpriteSheet.StepsFile
                {
                    Set = SheetLabel(_activePart),
                    Animation = animation,
                    Steps = steps.Select(st => new Data.TrainerSpriteSheet.StepJson { Frame = st.Frame, Hold = st.Hold, X = shifts ? st.X : null, Y = shifts ? st.Y : null }).ToList(),
                };
                PlainFiles.WriteText(Path.ChangeExtension(path, ".json"), Data.TrainerSpriteSheet.WriteSteps(file));
                SaveNotice.Exported(path);
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        }

        public List<TrainerSheetImportViewModel.AnimationChoice> AnimationSheetChoices(bool includeNew)
        {
            int count;
            try { count = OpenFrames(_activePart).Animations.Sequences.Count; }
            catch (Exception ex) { AppLogger.WarnOnce("TrainerSpriteEditorViewModel.AnimationSheetChoices", ex); return new(); }
            string Name(int i) => i switch
            {
                0 => "Standing",
                1 => _set.IsBack ? "Throwing the ball" : "Intro after sliding in",
                2 when !_set.IsBack => "While sliding in",
                _ => $"Animation {i}",
            };
            List<TrainerSheetImportViewModel.AnimationChoice> list = Enumerable.Range(0, count).Select(i => new TrainerSheetImportViewModel.AnimationChoice(i, $"{i}: {Name(i)}")).ToList();
            // A front plays 2 while sliding in and 1 on arrival; a back throws with 1.
            int most = _set.IsBack ? 2 : 3;
            if (includeNew && count < most) list.Add(new TrainerSheetImportViewModel.AnimationChoice(count, $"{count}: {Name(count)} (new)"));
            return list;
        }

        public TrainerSheetImportViewModel OpenSheetImport(bool animation, string pngPath, out string why)
        {
            why = null;
            try
            {
                TrainerSpriteSheet.Read sheet = Data.TrainerSpriteSheet.Open(File.ReadAllBytes(pngPath), out why);
                if (sheet == null) return null;
                Data.TrainerSpriteSheet.StepsFile steps = null;
                string json = Path.ChangeExtension(pngPath, ".json");
                if (animation && File.Exists(json))
                {
                    steps = Data.TrainerSpriteSheet.ReadSteps(File.ReadAllText(json), out string jsonWhy);
                    if (steps == null) { why = $"{Path.GetFileName(json)} couldn't be read: {jsonWhy}"; return null; }
                }

                List<TrainerSheetImportViewModel.Target> targets = new List<TrainerSheetImportViewModel.Target>();
                for (int p = 0; p < SheetPartCount; p++)
                {
                    int part = p;
                    targets.Add(new TrainerSheetImportViewModel.Target { Label = SheetLabel(p) ?? "", Open = () => OpenSheetFrames(part) });
                }
                uint[][] palettes = SheetPalettes(targets.Select(t => t.Open()));
                return new TrainerSheetImportViewModel(
                    animation ? TrainerSheetImportViewModel.Jobs.Animation : TrainerSheetImportViewModel.Jobs.Drawings,
                    targets, palettes, sheet, Path.GetFileName(pngPath), steps, animation ? AnimationSheetChoices(true) : new(), _activePart);
            }
            catch (Exception ex) { why = ex.Message; return null; }
        }

        // Nothing is written until Save.
        public void ApplySheetImport(TrainerSheetImportViewModel wizard)
        {
            TrainerSheetImportViewModel.Result result = wizard.Outcomes;
            if (result == null) return;
            int keep = _activePart;
            for (int p = 0; p < result.Frames.Count; p++)
            {
                switch (result.Frames[p])
                {
                    case FlatFrames flat:
                        _flatIndices = flat.Indices;
                        break;
                    case Data.TrainerSpriteFrames frames:
                        SpritePart part = _parts[p];
                        (part.Ncgr, part.Ncer, part.Nanr) = frames.Write();
                        part.Tile = ReadDrawing(part.Ncgr);
                        part.Sprite = ReadCells(part.Ncer);
                        break;
                }
            }

            if (result.Palettes != null)
            {
                for (int b = 0; b < result.Palettes.Length; b++)
                    for (int i = 0; i < 16; i++)
                    {
                        uint argb = result.Palettes[b][i];
                        System.Drawing.Color color = System.Drawing.Color.FromArgb((int)((argb >> 16) & 0xF8), (int)((argb >> 8) & 0xF8), (int)(argb & 0xF8));
                        if (b < _pal.Length && i < _pal[b].Length) _pal[b][i] = color;
                        foreach (SpritePart part in _parts)
                            if (b < part.Pal.Length && i < part.Pal[b].Length) part.Pal[b][i] = color;
                    }
                _paletteDirty = true;
            }

            HasUnsavedChanges = true;
            if (IsFlatSheetMode)
            {
                BuildPaletteSwatches(0);
                RebuildFlatCanvas();
                StatusText = wizard.Outcome;
                return;
            }

            Activate(Math.Min(keep, _parts.Count - 1));
            BuildFrameThumbnails();
            _activePaletteBank = -1;
            _selectedStripFrame = -1;
            SelectedFrameIndex = 0;
            OnPropertyChanged(nameof(FrameCount));
            RebuildAnimCellChoices();
            StopAnimPreview();
            _romAnim = OpenRomAnimations();
            OnPropertyChanged(nameof(CanEditAnimFrames));
            SetAnimJsonTextSilent(AnimationJsonOf(_romAnim ?? OpenFrames(0).Animations) ?? "");
            OnPropertyChanged(nameof(HasAnimation));
            StatusText = wizard.Outcome;
        }

        // One model frame per file frame, in order, so edits map back to the file.
        private static string AnimationJsonOf(NanrFile anims)
        {
            AnimJsonRoot root = new AnimJsonRoot();
            for (int seq = 0; seq < anims.Sequences.Count; seq++)
            {
                AnimSequenceJson model = new AnimSequenceJson { AnimationType = 1, PlaybackMode = 2 };
                for (int f = 0; f < anims.Sequences[seq].Frames.Count; f++)
                    model.FrameData.Add(new AnimFrameDataJson { CellIndex = anims.CellOf(seq, f), FrameDelay = Math.Max(1, (int)anims.Sequences[seq].Frames[f].Delay) });
                root.Sequences.Add(model);
            }
            return root.Sequences.Count == 0 ? null : root.Serialize();
        }

        private void WriteStagedSheetFiles(SpritePart part)
        {
            if (part.Ncer != null) ArchiveMembers.Write(EntryPath(TrainerGraphicsLayout.CellsEntry(part.Entry)), part.Ncer);
            if (part.Nanr != null) ArchiveMembers.Write(EntryPath(TrainerGraphicsLayout.AnimationEntry(part.Entry)), part.Nanr);
            part.Ncgr = part.Ncer = part.Nanr = null;
        }

        /// <summary>Diamond and Pearl's two frames in one flat drawing, side by side or stacked.</summary>
        private sealed class FlatFrames : Data.ISheetFrames
        {
            public int[] Indices { get; }
            private readonly int _w, _fw, _fh, _count;
            private readonly bool _across;
            private const int N = Data.TrainerSpriteFrames.Canvas;

            public FlatFrames(int[] indices, int w, int h)
            {
                Indices = indices; _w = w;
                _across = w == 2 * h;
                _count = _across || h == 2 * w ? 2 : 1;
                _fw = _across ? w / 2 : w;
                _fh = _count == 2 && !_across ? h / 2 : h;
            }

            public int FrameCount => _count;
            public bool CanChangeFrameCount => false;
            public bool IsBlank(int frame) => false;

            private (int X, int Y) Corner(int frame) => _across ? (frame * _fw, 0) : (0, frame * _fh);
            private int Left => (N - _fw) / 2;
            private int Top => (N - _fh) / 2;

            public int[] Draw(int frame)
            {
                int[] canvas = new int[N * N];
                (int cx, int cy) = Corner(frame);
                for (int y = 0; y < _fh && y < N; y++)
                    for (int x = 0; x < _fw && x < N; x++)
                        canvas[(Top + y) * N + Left + x] = Indices[(cy + y) * _w + cx + x];
                return canvas;
            }

            public int[] PalettesOf(int frame)
            {
                int[] canvas = new int[N * N];
                for (int y = 0; y < _fh && y < N; y++)
                    for (int x = 0; x < _fw && x < N; x++) canvas[(Top + y) * N + Left + x] = 1;
                return canvas;
            }

            public string SetDrawing(int frame, int[] canvas)
            {
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                        if (canvas[y * N + x] != 0 && (x < Left || x >= Left + _fw || y < Top || y >= Top + _fh))
                            return $"Frame {frame} draws outside the middle {_fw}x{_fh}, which is all a frame holds here.";
                (int cx, int cy) = Corner(frame);
                for (int y = 0; y < _fh; y++)
                    for (int x = 0; x < _fw; x++)
                        Indices[(cy + y) * _w + cx + x] = canvas[(Top + y) * N + Left + x] & 0xF;
                return null;
            }
        }

        // ── Save ──────────────────────────────────────────────────────────────
        // On a linked hg-engine project the drawing and colours come from, and go back to, the source PNG.
        private string _sourcePngPath;
        /// <summary>Saves the class. Returns null on success, otherwise the error.</summary>
        public string Save()
        {
            if (_tile == null) return "No sprite loaded.";
            try
            {
                if (BankCount == 0)
                {
                    // Composited-mode edits are already written in place into _tile.Tiles by
                    // EncodeCell as they happen; the flat-sheet fallback packs on save instead.
                    int bpp = _tile.BitsPerPixel;
                    byte[] rasterBytes = PackIndices(_flatIndices, _flatWidth, _flatHeight, bpp);
                    byte[] nativeBytes = _tile.Tiled ? RowsToTiles(rasterBytes, _flatWidth, _flatHeight, bpp) : rasterBytes;
                    if (_scrambleSeed.HasValue)
                        SpriteScrambling.Scramble(nativeBytes, 0, nativeBytes.Length, _scrambleSeed.Value);
                    _tile.SetTiles(nativeBytes);
                }

                if (_sourcePngPath != null)
                {
                    string sourceError = Data.TrainerSpriteSourcePng.Write(_sourcePngPath, _tile, _pal[0]);
                    if (sourceError != null) { StatusText = "Couldn't save: " + sourceError; return sourceError; }
                }

                int keep = _activePart;
                try
                {
                    for (int p = 0; p < Math.Max(1, _parts.Count); p++)
                    {
                        if (_parts.Count > 0) Activate(p);
                        // The built archive is kept in step too, so other previews show the edit before a compile.
                        // The naming screen's drawing is stored packed, so it is packed again on the way out.
                        ArchiveMembers.Write(_tilesPath, _packedTilesPath != null
                            ? Data.GraphicAssets.Squeeze(_tile.ToBytes(), _packedTilesMarker)
                            : _tile.ToBytes());
                        if (_parts.Count > 0) WriteStagedSheetFiles(_parts[p]);
                        if (BankCount > 0 && !_set.NamingScreen)
                        {
                            int entry = _parts.Count > 0 ? _parts[p].Entry : _trClassID;
                            string scanError = WriteScan(entry);
                            if (scanError == null && _sourcePngPath != null) scanError = WriteScanPng(entry, _sourcePngPath[..^4] + "_enc.png");
                            if (scanError != null) { StatusText = "Couldn't save: " + scanError; return scanError; }
                        }
                        if (_paletteDirty || _sourcePngPath != null)
                        {
                            byte[] nclr = File.ReadAllBytes(_palPath);
                            uint[] colours = _pal.SelectMany(bank => bank.Select(c => 0xFF000000u | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B)).ToArray();
                            string error = Data.GraphicAssets.PatchPalette(ref nclr, colours);
                            if (error != null) { StatusText = "Couldn't save: " + error; return error; }
                            ArchiveMembers.Write(_palPath, nclr);
                        }
                    }
                    _paletteDirty = false;
                }
                finally { if (_parts.Count > 0) Activate(keep); }

                if (BankCount > 0) BuildFrameThumbnails();

                HasUnsavedChanges = false;
                _savedState = _lastState = TakeState();
                StatusText = _sourcePngPath != null ? "Saved. Compile the ROM to apply it." : "Saved.";
                return null;
            }
            catch (Exception ex)
            {
                StatusText = "Couldn't save: " + ex.Message;
                AppLogger.Error("TrainerSpriteEditorViewModel.Save failed: " + ex.Message);
                return ex.Message;
            }
        }

        // ── Palette-index <-> packed byte helpers ──────────────────────────────
        // A 16-colour byte holds the left pixel in its low half.
        private static int[] UnpackIndices(byte[] data, int width, int height, int bpp)
        {
            int count = width * height;
            int[] indices = new int[count];
            switch (bpp)
            {
                case 4:
                    for (int i = 0; i < count && i / 2 < data.Length; i++)
                        indices[i] = (i & 1) == 0 ? data[i / 2] & 0x0F : data[i / 2] >> 4;
                    break;
                case 8:
                    for (int i = 0; i < count && i < data.Length; i++)
                        indices[i] = data[i];
                    break;
                default:
                    throw new NotSupportedException($"Sprites with {bpp} bits per pixel can't be edited.");
            }
            return indices;
        }

        private static byte[] PackIndices(int[] indices, int width, int height, int bpp)
        {
            int count = width * height;
            switch (bpp)
            {
                case 4:
                {
                    byte[] result = new byte[(count + 1) / 2];
                    for (int i = 0; i < count; i += 2)
                    {
                        int lo = indices[i] & 0xF;
                        int hi = (i + 1 < count ? indices[i + 1] : 0) & 0xF;
                        result[i / 2] = (byte)(lo | (hi << 4));
                    }
                    return result;
                }
                case 8:
                {
                    byte[] result = new byte[count];
                    for (int i = 0; i < count; i++) result[i] = (byte)(indices[i] & 0xFF);
                    return result;
                }
                default:
                    throw new NotSupportedException($"Sprites with {bpp} bits per pixel can't be edited.");
            }
        }

        // Where byte b of row y of tile (tx, ty) sits when the picture is stored row by row; one tile row is bpp bytes.
        private static int RowByteAt(int tx, int ty, int y, int b, int tilesX, int bpp) => ((ty * 8 + y) * tilesX + tx) * bpp + b;

        /// <summary>Bytes stored tile after tile, laid out row after row.</summary>
        private static byte[] TilesToRows(byte[] tiled, int width, int height, int bpp)
        {
            byte[] rows = new byte[tiled.Length];
            int tilesX = width / 8, pos = 0;
            for (int ty = 0; ty < height / 8; ty++)
                for (int tx = 0; tx < tilesX; tx++)
                    for (int y = 0; y < 8; y++)
                        for (int b = 0; b < bpp; b++)
                        {
                            int at = RowByteAt(tx, ty, y, b, tilesX, bpp);
                            if (at >= rows.Length || pos >= tiled.Length) continue;
                            rows[at] = tiled[pos++];
                        }
            return rows;
        }

        /// <summary>The inverse of <see cref="TilesToRows"/>.</summary>
        private static byte[] RowsToTiles(byte[] rows, int width, int height, int bpp)
        {
            byte[] tiled = new byte[rows.Length];
            int tilesX = width / 8, pos = 0;
            for (int ty = 0; ty < height / 8; ty++)
                for (int tx = 0; tx < tilesX; tx++)
                    for (int y = 0; y < 8; y++)
                        for (int b = 0; b < bpp; b++)
                        {
                            int at = RowByteAt(tx, ty, y, b, tilesX, bpp);
                            if (at >= tiled.Length || pos >= tiled.Length) continue;
                            tiled[pos++] = rows[at];
                        }
            return tiled;
        }
    }
}

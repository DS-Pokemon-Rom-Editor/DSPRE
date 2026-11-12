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

        /// <summary>Naming screen icons: one drawing, frames picked per animation, files packed.</summary>
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

    /// <summary>One frame of a sequence: a pose and how long it holds.</summary>
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

    /// <summary>Trainer sprite pixel editor: cells on Pt and HGSS, a flat sheet on DP.</summary>
    public partial class TrainerSpriteEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        // Fits any trainer class sprite. OAM offsets are relative to its centre.
        private const int CanvasSize = 128;

        private System.Drawing.Color[][] _pal;
        private DrawingFile _tile;
        private NcerFile _sprite; // null on DP (no NCER), flat-sheet fallback mode

        // Descrambling zeroes the seed word, so it is kept to scramble again on save.
        private ushort? _scrambleSeed;
        private string _tilesPath;

        // hg-engine: cells come from *_cell.json, since nitrogfx breaks negative-X OAMs in the narc.
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

        // HGSS Ethan and Lyra are two drawings on one palette. Each is a part.
        private sealed class SpritePart
        {
            public int Entry;
            public string Label;
            public DrawingFile Tile;
            public System.Drawing.Color[][] Pal;
            public NcerFile Sprite;
            public string TilesPath, PalPath;
            public int PalEntry;

            // A sheet import can change the cells and animations too; they wait here until saved.
            public byte[] Ncgr, Ncer, Nanr;

            // Per scan half, its frame and that frame as last read or saved. Null where none.
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
            _tilesPath = p.TilesPath; _palPath = p.PalPath; _palEntry = p.PalEntry;
        }
        private int _activePaletteBank = -1;

        private int[] _flatIndices;
        private int _flatWidth, _flatHeight;

        public bool IsFlatSheetMode => _sprite == null;

        private const int MaxZoom = 24;
        private int _zoom = 4;
        private bool _zoomChosen;

        /// <summary>Screen pixels per sprite pixel, kept once the user has zoomed.</summary>
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
            ListSync.Apply(ClassNames, labels);
        }

        private int SpriteCount()
        {
            try { return NarcAPI.Narc.MemberCount(GameDirs[_set.Archive].unpackedDir) / DSPRE.ROMFiles.TrainerBackSprites.FilesPerSprite; }
            catch (Exception ex) { AppLogger.WarnOnce("TrainerSpriteEditorViewModel.SpriteCount", ex); return 0; }
        }
    }
}

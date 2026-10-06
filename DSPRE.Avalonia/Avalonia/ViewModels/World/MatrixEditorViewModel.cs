using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.World
{
    /// <summary>
    /// Avalonia port of the WinForms <c>MatrixEditor</c>. Edits a map matrix's three
    /// W×H grids (map IDs always, header IDs and altitudes as optional sections) via
    /// the paintable <c>MatrixGridControl</c>. Supports selecting a matrix, adding the
    /// optional sections, and save / import / export.
    /// </summary>
    public class MatrixEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private Window _owner;
        private bool _suppress;
        private GameMatrix _matrix;

        public event EventHandler MatrixLoaded;

        public ObservableCollection<string> MatrixNames { get; } = new ObservableCollection<string>();

        public int Width => _matrix?.width ?? 0;
        public int Height => _matrix?.height ?? 0;

        // New cells get no map; shrinking drops the cut-off cells, which undo brings back.
        public decimal SizeWidth { get => Width; set => Resize((int)value, Height); }
        public decimal SizeHeight { get => Height; set => Resize(Width, (int)value); }
        public bool HasMatrix => _matrix != null;

        /// <summary>Why this size would overrun the game's matrix buffer, or empty.</summary>
        public string SizeWarning
        {
            get
            {
                if (_matrix == null) return "";
                bool patched = false;
                try { patched = RomPatchState.flag_MatrixExpansionApplied || (gameFamily == GameFamilies.HGSS && PatchToolboxLogic.CheckFilesMatrixExpansionApplied()); }
                catch { }
                int max = MatrixMaxCells(patched), cells = _matrix.width * _matrix.height;
                if (cells <= max) return "";
                string fix = gameFamily == GameFamilies.HGSS && !patched ? " Apply Expand Matrix 0 in the ROM Patch Toolbox to double it." : "";
                return $"{cells} cells: the game holds at most {max}, so a bigger matrix corrupts memory.{fix}";
            }
        }
        public bool HasSizeWarning => SizeWarning.Length > 0;

        private void Resize(int width, int height)
        {
            if (_matrix == null || _suppress) return;
            width = Math.Clamp(width, 1, 255);
            height = Math.Clamp(height, 1, 255);
            if (width == _matrix.width && height == _matrix.height) return;
            _matrix.ResizeMatrix(height, width);
            RebuildLegend();
            MarkDirty();
            StatusText = $"Matrix {_selectedIndex} is now {width}×{height} (unsaved).";
            RaiseLoaded();
        }
        public bool HasHeaders => _matrix?.hasHeadersSection ?? false;
        public bool HasHeights => _matrix?.hasHeightsSection ?? false;
        public bool CanAddHeaders => _matrix != null && !_matrix.hasHeadersSection;
        public bool CanAddHeights => _matrix != null && !_matrix.hasHeightsSection;

        // Cell accessors used by the grid controls (col = x, row = y → array[row, col]).
        public int GetMap(int c, int r) => _matrix.maps[r, c];

        /// <summary>The header being worked on, whose cells the grid outlines and centres on; -1 for none.</summary>
        private int _focusHeader = -1;
        public int FocusHeader { get => _focusHeader; set { if (_focusHeader == value) return; _focusHeader = value; OnPropertyChanged(); } }

        /// <summary>Which header a cell belongs to; without a header section only the focus header's matrix counts.</summary>
        public int HeaderOfCell(int c, int r)
        {
            if (_matrix == null || c < 0 || r < 0 || c >= _matrix.width || r >= _matrix.height) return -1;
            if (_matrix.hasHeadersSection) return _matrix.headers[r, c];
            if (_matrix.maps[r, c] == GameMatrix.EMPTY || _focusHeader < 0) return -1;
            try { return MapHeader.GetMapHeader((ushort)_focusHeader)?.matrixID == _selectedIndex ? _focusHeader : -1; }
            catch { return -1; }
        }

        // ── What kind of place each cell is ─────────────────────────────────────────
        public enum PlaceKind { None, OutOfBounds, Town, Route, Forest, Sea, Lake, Park, Cave, Underground, Building }

        public static readonly (PlaceKind Kind, string Name, global::Avalonia.Media.Color Colour)[] Kinds =
        {
            (PlaceKind.Town, "Town / City", global::Avalonia.Media.Color.FromRgb(0xB0, 0x3A, 0x2E)),
            (PlaceKind.Route, "Route", global::Avalonia.Media.Color.FromRgb(0x3B, 0x8A, 0x3E)),
            (PlaceKind.Forest, "Forest", global::Avalonia.Media.Color.FromRgb(0x1B, 0x5A, 0x2C)),
            (PlaceKind.Park, "Park", global::Avalonia.Media.Color.FromRgb(0x8A, 0x7A, 0x1C)),
            (PlaceKind.Sea, "Sea route", global::Avalonia.Media.Color.FromRgb(0x1D, 0x5B, 0xA6)),
            (PlaceKind.Lake, "Lake", global::Avalonia.Media.Color.FromRgb(0x2B, 0x84, 0xB8)),
            (PlaceKind.Cave, "Cave", global::Avalonia.Media.Color.FromRgb(0x6E, 0x47, 0x2A)),
            (PlaceKind.Underground, "Underground", global::Avalonia.Media.Color.FromRgb(0x5A, 0x37, 0x8C)),
            (PlaceKind.Building, "Building", global::Avalonia.Media.Color.FromRgb(0x55, 0x5E, 0x6E)),
            (PlaceKind.OutOfBounds, "Out of bounds", global::Avalonia.Media.Color.FromRgb(0x2A, 0x2D, 0x32)),
            (PlaceKind.None, "Other", global::Avalonia.Media.Color.FromRgb(0x3E, 0x44, 0x4C)),
        };

        private readonly Dictionary<int, PlaceKind> _kindOfHeader = new();
        private int _headerOfWholeMatrix = -1;

        /// <summary>
        /// A header's kind of place from its map type; outdoor ones are refined by name popup style, and header 0
        /// is the catch-all for scenery the player can't reach.
        /// </summary>
        public static PlaceKind KindOf(int id, MapHeader h)
        {
            if (h == null) return PlaceKind.None;
            if (id == 0) return PlaceKind.OutOfBounds;
            int type = h is HeaderHGSS hg ? hg.locationType : h.locationSpecifier;
            int popup = h is HeaderPt pt ? pt.areaIcon : h is HeaderHGSS hgp ? hgp.areaIcon : 0;
            switch (type)
            {
                case 1: return PlaceKind.Town;
                case 2:
                    // Banner styles as in PokeDatabase.Area: 5 Forest, 6 Water, 7 Park, 8 Lake.
                    return popup switch { 5 => PlaceKind.Forest, 6 => PlaceKind.Sea, 7 => PlaceKind.Park, 8 => PlaceKind.Lake, _ => PlaceKind.Route };
                case 3: return PlaceKind.Cave;
                case 4: case 5: return PlaceKind.Building;
                case 6: return PlaceKind.Underground;
                default: return PlaceKind.None;
            }
        }

        private int HeaderForColour(int c, int r)
        {
            if (_matrix == null || _matrix.maps[r, c] == GameMatrix.EMPTY) return -1;
            return _matrix.hasHeadersSection ? _matrix.headers[r, c] : _headerOfWholeMatrix;
        }

        /// <summary>The colour of the kind of place a cell is, or null for a cell with no map.</summary>
        public global::Avalonia.Media.Color? CellColour(int c, int r)
        {
            int h = HeaderForColour(c, r);
            if (h < 0) return null;
            if (!_kindOfHeader.TryGetValue(h, out var kind))
            {
                try { kind = KindOf(h, MapHeader.GetMapHeader((ushort)h)); } catch { kind = PlaceKind.None; }
                _kindOfHeader[h] = kind;
            }
            foreach (var k in Kinds) if (k.Kind == kind) return k.Colour;
            return null;
        }

        /// <summary>The kinds present in the shown matrix, for the legend.</summary>
        public System.Collections.ObjectModel.ObservableCollection<LegendEntry> Legend { get; } = new();
        public sealed class LegendEntry
        {
            public string Name { get; init; }
            public global::Avalonia.Media.IBrush Brush { get; init; }
        }

        private void RebuildLegend()
        {
            _kindOfHeader.Clear();
            _headerOfWholeMatrix = -1;
            if (_matrix != null && !_matrix.hasHeadersSection)
            {
                // A matrix without a header section belongs to whichever header uses it.
                try
                {
                    for (int h = 0; h < GetHeaderCount(); h++)
                        if (MapHeader.GetMapHeader((ushort)h)?.matrixID == _selectedIndex) { _headerOfWholeMatrix = h; break; }
                }
                catch { }
            }
            var seen = new HashSet<global::Avalonia.Media.Color>();
            for (int r = 0; r < Height; r++)
                for (int c = 0; c < Width; c++)
                    if (CellColour(c, r) is global::Avalonia.Media.Color col) seen.Add(col);
            Legend.Clear();
            foreach (var k in Kinds)
                if (seen.Contains(k.Colour))
                    Legend.Add(new LegendEntry { Name = k.Name, Brush = new global::Avalonia.Media.SolidColorBrush(k.Colour) });
        }

        /// <summary>Opens a header cell's header; the Maps workspace replaces it to switch its own header.</summary>
        public Action<int> OpenHeader { get; set; } = id => AvaloniaEditorLauncher.OpenHeaderEditor(id);

        private bool _paintMode;
        public bool PaintMode { get => _paintMode; set { if (_paintMode == value) return; _paintMode = value; OnPropertyChanged(); } }
        public void SetMap(int c, int r, int v) => _matrix.maps[r, c] = (ushort)v;
        public int GetHeader(int c, int r) => _matrix.headers[r, c];
        public void SetHeader(int c, int r, int v) => _matrix.headers[r, c] = (ushort)v;
        public int GetHeight(int c, int r) => _matrix.altitudes[r, c];
        public void SetHeight(int c, int r, int v) => _matrix.altitudes[r, c] = (byte)v;

        // ── Selected cell (for "Set spawn to selection") ─────────────────────────────────
        private int _selCol, _selRow;
        public int SelCol => _selCol;
        public int SelRow => _selRow;
        public void SetSelectedCell(int c, int r) { _selCol = c; _selRow = r; }
        public bool InBounds => _matrix != null && _selCol >= 0 && _selRow >= 0 && _selCol < _matrix.width && _selRow < _matrix.height;

        /// <summary>
        /// The header the selected cell belongs to. Without a header section that is the header using this
        /// matrix, or null when none or several do.
        /// </summary>
        public ushort? SpawnHeaderNumber
        {
            get
            {
                if (!InBounds) return null;
                if (_matrix.hasHeadersSection) return (ushort)_matrix.headers[_selRow, _selCol];
                var users = new List<int>();
                try
                {
                    for (int h = 0; h < GetHeaderCount(); h++)
                        if (MapHeader.GetMapHeader((ushort)h)?.matrixID == _selectedIndex) users.Add(h);
                }
                catch { return null; }
                if (_focusHeader >= 0 && users.Contains(_focusHeader)) return (ushort)_focusHeader;
                return users.Count == 1 ? (ushort)users[0] : null;
            }
        }

        private decimal _mapPaint, _headerPaint, _heightPaint;
        public decimal MapPaint { get => _mapPaint; set => Set(ref _mapPaint, value); }
        public decimal HeaderPaint { get => _headerPaint; set => Set(ref _headerPaint, value); }
        public decimal HeightPaint { get => _heightPaint; set => Set(ref _heightPaint, value); }

        private string _cellInfo = "";
        public string CellInfo { get => _cellInfo; set => Set(ref _cellInfo, value); }
        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        // ── Dirty tracking ───────────────────────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => $"Matrix {_selectedIndex}";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); if (_selectedIndex >= 0) LoadMatrix(_selectedIndex); }
        public void MarkDirty() { _undo?.Record(); if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        // ── Undo / redo ────────────────────────────────────────────────────────────
        private ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() { _undo?.Undo(); SyncDirtyWithUndo(); }
        public void Redo() { _undo?.Redo(); SyncDirtyWithUndo(); }
        private void SyncDirtyWithUndo()
        {
            if (_undo == null || _undo.IsDirty == _dirty) return;
            _dirty = _undo.IsDirty; OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void StartUndo()
        {
            _undo = _matrix == null ? null : new ByteStateUndo(() => _matrix.ToByteArray(), state =>
            {
                int? id = _matrix.id;
                GameMatrix restored;
                using (var ms = new MemoryStream(state)) restored = new GameMatrix(ms);
                _matrix = id is int keep ? new GameMatrix(restored, keep) : restored;
                RebuildLegend();
                MarkDirty();
                StatusText = $"Matrix {_selectedIndex} is {_matrix.width}×{_matrix.height}.";
                RaiseLoaded();
            }, () => { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); });
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        }
        private void SetClean() { _undo?.MarkSaved(); if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        private int _selectedIndex = -1;
        public int SelectedMatrixIndex
        {
            get => _selectedIndex;
            set
            {
                if (RecordSwitchGuard.IsSnappingBack) return;
                if (value == _selectedIndex) return;
                if (_dirty && !_suppress && value >= 0 && _selectedIndex >= 0)
                {
                    // Snap the list back to the matrix still loaded, so the answer decides where we
                    // end up rather than the click already having moved us.
                    int requested = value;
                    RecordSwitchGuard.SnapBack(() => _selectedIndex, v => _selectedIndex = v, () => OnPropertyChanged(nameof(SelectedMatrixIndex)));
                    _ = SwitchMatrixAsync(requested);
                    return;
                }
                if (Set(ref _selectedIndex, value) && !_suppress && value >= 0) LoadMatrix(value);
            }
        }

        private async Task SwitchMatrixAsync(int requested)
        {
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(this, _owner, "matrix")) return;
            SetClean();
            if (Set(ref _selectedIndex, requested, nameof(SelectedMatrixIndex))) LoadMatrix(requested);
        }

        public MatrixEditorViewModel() { if (Design.IsDesignMode) MatrixNames.Add("Matrix 0"); }
        public MatrixEditorViewModel(bool _) { AppEvents.MatrixSaved += OnSavedElsewhere; }

        /// <summary>For a standalone window closing; the Maps workspace's instance lives for the session.</summary>
        public void Detach() => AppEvents.MatrixSaved -= OnSavedElsewhere;

        // Another open copy of this matrix saved: show it, unless this copy holds its own edits.
        private void OnSavedElsewhere(object sender, int id)
        {
            if (ReferenceEquals(sender, this) || id != _selectedIndex) return;
            if (_dirty) { StatusText = $"Matrix {id} was saved in another window. Save or discard here to see it."; return; }
            LoadMatrix(id);
            StatusText = $"Matrix {id} was saved in another window and reloaded.";
        }
        public int InitialIndex { get; set; }

        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.matrices });
                int count = Filesystem.GetMatrixCount();
                _suppress = true;
                MatrixNames.Clear();
                for (int i = 0; i < count; i++) MatrixNames.Add(new GameMatrix(i).ToString());
                _suppress = false;
                StatusText = $"{count} matrices.";
                if (count > 0) SelectedMatrixIndex = Math.Min(Math.Max(0, InitialIndex), count - 1);
            }
            catch (Exception ex)
            {
                StatusText = "Error: " + ex.Message;
                await DialogHelper.ShowError($"Failed to set up Matrix Editor:\n{ex.Message}", "Matrix Editor");
            }
        }

        private void LoadMatrix(int index)
        {
            try
            {
                _matrix = new GameMatrix(index);
                RebuildLegend();
                SetClean();
                StartUndo();
                StatusText = $"Loaded matrix {index} ({Width}×{Height}).";
                RaiseLoaded();
            }
            catch (Exception ex)
            {
                _ = DialogHelper.ShowError($"Failed to load matrix {index}:\n{ex.Message}", "Matrix Editor");
            }
        }

        private void RaiseLoaded()
        {
            OnPropertyChanged(nameof(Width)); OnPropertyChanged(nameof(Height));
            OnPropertyChanged(nameof(SizeWidth)); OnPropertyChanged(nameof(SizeHeight)); OnPropertyChanged(nameof(HasMatrix));
            OnPropertyChanged(nameof(SizeWarning)); OnPropertyChanged(nameof(HasSizeWarning));
            OnPropertyChanged(nameof(HasHeaders)); OnPropertyChanged(nameof(HasHeights));
            OnPropertyChanged(nameof(CanAddHeaders)); OnPropertyChanged(nameof(CanAddHeights));
            OnPropertyChanged(nameof(UnsavedChangesDescription));
            MatrixLoaded?.Invoke(this, EventArgs.Empty);
        }

        public void AddHeaderSection()
        {
            if (_matrix == null || _matrix.hasHeadersSection) return;
            _matrix.hasHeadersSection = true;
            MarkDirty();
            RaiseLoaded();
        }

        public void AddHeightsSection()
        {
            if (_matrix == null || _matrix.hasHeightsSection) return;
            _matrix.hasHeightsSection = true;
            MarkDirty();
            RaiseLoaded();
        }

        public void Save()
        {
            if (_matrix == null || _selectedIndex < 0) return;
            _matrix.SaveToFileDefaultDir(_selectedIndex, showSuccessMessage: false);
            SetClean();
            SaveNotice.Saved(UnsavedChangesDescription);
            StatusText = $"Saved matrix {_selectedIndex}.";
            AppEvents.RaiseMatrixSaved(this, _selectedIndex);
        }

        public async Task ImportAsync()
        {
            if (_selectedIndex < 0) return;
            var filter = new FilePickerFileType("Matrix file") { Patterns = new[] { "*.mtx", "*.bin", "*.*" } };
            string path = await DialogHelper.OpenFile(_owner, "Import matrix", new[] { filter });
            if (path == null) return;
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read)) _matrix = new GameMatrix(fs);
                MarkDirty();
                StatusText = "Imported matrix (unsaved).";
                RaiseLoaded();
            }
            catch (Exception ex) { await DialogHelper.ShowError($"Import failed:\n{ex.Message}", "Import Error"); }
        }

        public async Task ExportAsync()
        {
            if (_matrix == null) return;
            var filter = new FilePickerFileType("Matrix file") { Patterns = new[] { "*.mtx" } };
            string path = await DialogHelper.SaveFile(_owner, "Export matrix", new[] { filter }, $"matrix_{_selectedIndex:D4}.mtx");
            if (path == null) return;
            try { File.WriteAllBytes(path, _matrix.ToByteArray()); StatusText = "Exported."; }
            catch (Exception ex) { await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error"); }
        }
    }
}

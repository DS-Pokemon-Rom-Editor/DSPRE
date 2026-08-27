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

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// Avalonia port of the WinForms <c>MatrixEditor</c>. Edits a map matrix's three
    /// W×H grids (map IDs always, header IDs and altitudes as optional sections) via
    /// the paintable <c>MatrixGridControl</c>. Supports selecting a matrix, adding the
    /// optional sections, and save / import / export.
    /// </summary>
    public class MatrixEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
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
        public bool HasHeaders => _matrix?.hasHeadersSection ?? false;
        public bool HasHeights => _matrix?.hasHeightsSection ?? false;
        public bool CanAddHeaders => _matrix != null && !_matrix.hasHeadersSection;
        public bool CanAddHeights => _matrix != null && !_matrix.hasHeightsSection;

        // Cell accessors used by the grid controls (col = x, row = y → array[row, col]).
        public int GetMap(int c, int r) => _matrix.maps[r, c];
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
        public ushort SpawnHeaderNumber => (InBounds && _matrix.hasHeadersSection) ? (ushort)_matrix.headers[_selRow, _selCol] : (ushort)0;

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
        public void MarkDirty() { if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        private int _selectedIndex = -1;
        public int SelectedMatrixIndex
        {
            get => _selectedIndex;
            set { if (Set(ref _selectedIndex, value) && !_suppress && value >= 0) LoadMatrix(value); }
        }

        public MatrixEditorViewModel() { if (Design.IsDesignMode) MatrixNames.Add("Matrix 0"); }
        public MatrixEditorViewModel(bool _) { }
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
                SetClean();
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
            StatusText = $"Saved matrix {_selectedIndex}.";
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

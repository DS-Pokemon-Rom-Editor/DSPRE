using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// Avalonia editor for AreaData files: the records that tie a map area to its texture packs.
    /// Each entry sets the map tileset and buildings tileset (the NSBTX pack indices), plus the
    /// dynamic-texture/area-type/light fields. This is the data the map &amp; event 3D views resolve
    /// to pick the correct textures; it had no Avalonia editor before (was only in WinForms NSBTX).
    /// </summary>
    public class AreaDataEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        // NOTE: Set() does NOT mark dirty; only the actual area-field edits below do (via Dirty()). Otherwise
        // selecting a different area or updating StatusText would falsely flag unsaved changes (and saving,
        // which sets StatusText after SetClean, would immediately re-dirty the editor).
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private bool _suppress;
        private AreaData _area;

        public ObservableCollection<string> AreaNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> AreaTypes { get; } = new ObservableCollection<string> { "Indoor", "Outdoor" };
        public bool IsHGSS => gameFamily == GameFamilies.HGSS;

        private int _selectedIndex = -1;
        public int SelectedIndex { get => _selectedIndex; set { if (Set(ref _selectedIndex, value) && !_suppress && value >= 0) LoadArea(value); } }

        /// <summary>Area data entry to select once loaded (e.g. from the Header editor's "Open" button).</summary>
        public int InitialIndex { get; set; }

        private decimal _mapTileset, _buildingsTileset, _dynamicTextureType, _lightType;
        public decimal MapTileset { get => _mapTileset; set { if (Set(ref _mapTileset, value) && _area != null) { _area.mapTileset = (ushort)value; if (!_suppress) Dirty(); } } }
        public decimal BuildingsTileset { get => _buildingsTileset; set { if (Set(ref _buildingsTileset, value) && _area != null) { _area.buildingsTileset = (ushort)value; if (!_suppress) Dirty(); } } }
        public decimal DynamicTextureType { get => _dynamicTextureType; set { if (Set(ref _dynamicTextureType, value) && _area != null) { if (IsHGSS) _area.dynamicTextureType = (ushort)value; else _area.unknown1 = (ushort)value; if (!_suppress) Dirty(); } } }
        public decimal LightType { get => _lightType; set { if (Set(ref _lightType, value) && _area != null) { _area.lightType = (ushort)value; if (!_suppress) Dirty(); } } }

        private int _areaTypeIndex;
        public int AreaTypeIndex { get => _areaTypeIndex; set { if (Set(ref _areaTypeIndex, value) && _area != null) { _area.areaType = (byte)(value == 0 ? 0 : 1); if (!_suppress) Dirty(); } } }

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => $"Area data {_selectedIndex}";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); if (_selectedIndex >= 0) LoadArea(_selectedIndex); }
        // RecordUndoSnapshot runs BEFORE the _dirty short-circuit so EVERY edit is captured (not just the first).
        private void Dirty() { RecordUndoSnapshot(); if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        // ── Undo / redo (ISupportsUndo) ────────────────────────────────────────
        private readonly UndoHistory<byte[]> _history = new();
        private DateTime _lastCaptureUtc = DateTime.MinValue;
        private const int CoalesceMs = 500;

        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;
        public void Undo() { if (_history.CanUndo) ApplyState(_history.Undo()); }
        public void Redo() { if (_history.CanRedo) ApplyState(_history.Redo()); }
        private void RaiseUndoState() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private void ApplyState(byte[] bytes)
        {
            if (bytes == null) return;
            _area = new AreaData(new MemoryStream(bytes));
            _suppress = true;
            PopulateFromArea();
            _suppress = false;
            _dirty = _history.IsDirty;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            RaiseUndoState();
        }

        private void RecordUndoSnapshot()
        {
            if (_suppress || _area == null) return;
            bool coalesce = (DateTime.UtcNow - _lastCaptureUtc).TotalMilliseconds < CoalesceMs;
            _history.Capture(_area.ToByteArray(), coalesce);
            _lastCaptureUtc = DateTime.UtcNow;
            RaiseUndoState();
        }

        /// <summary>Pushes <see cref="_area"/> into the bound fields. Caller guards with _suppress.</summary>
        private void PopulateFromArea()
        {
            MapTileset = _area.mapTileset;
            BuildingsTileset = _area.buildingsTileset;
            DynamicTextureType = IsHGSS ? _area.dynamicTextureType : _area.unknown1;
            LightType = _area.lightType;
            AreaTypeIndex = _area.areaType == 0 ? 0 : 1;
        }

        public AreaDataEditorViewModel() { }
        public AreaDataEditorViewModel(bool _) { }

        public async Task SetupAsync(Window owner)
        {
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.areaData });
                int count = Directory.GetFiles(gameDirs[DirNames.areaData].unpackedDir).Length;
                AreaNames.Clear();
                for (int i = 0; i < count; i++) AreaNames.Add("Area Data " + i);
                StatusText = $"{count} area data entries.";
                if (count > 0) SelectedIndex = Math.Min(Math.Max(0, InitialIndex), count - 1);
            }
            catch (Exception ex)
            {
                StatusText = "Error: " + ex.Message;
                await DialogHelper.ShowError($"Failed to set up Area Data Editor:\n{ex.Message}", "Area Data");
            }
        }

        private void LoadArea(int index)
        {
            try
            {
                _area = new AreaData((byte)index);
                _suppress = true;
                PopulateFromArea();
                _suppress = false;
                SetClean();
                _history.Reset(_area.ToByteArray());   // loaded state is the clean undo baseline for this area
                _lastCaptureUtc = DateTime.MinValue;
                RaiseUndoState();
                StatusText = $"Loaded area data {index}.";
                OnPropertyChanged(nameof(UnsavedChangesDescription));
            }
            catch (Exception ex) { _ = DialogHelper.ShowError($"Failed to load area data {index}:\n{ex.Message}", "Area Data"); }
        }

        public void Save()
        {
            if (_area == null || _selectedIndex < 0) return;
            try { _area.SaveToFileDefaultDir(_selectedIndex, showSuccessMessage: false); SetClean(); _history.MarkSaved(); RaiseUndoState(); StatusText = $"Saved area data {_selectedIndex}."; }
            catch (Exception ex) { _ = DialogHelper.ShowError($"Save failed:\n{ex.Message}", "Area Data"); }
        }
    }
}

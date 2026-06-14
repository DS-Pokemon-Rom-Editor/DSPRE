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
    /// Avalonia editor for AreaData files — the records that tie a map area to its texture packs.
    /// Each entry sets the map tileset and buildings tileset (the NSBTX pack indices), plus the
    /// dynamic-texture/area-type/light fields. This is the data the map &amp; event 3D views resolve
    /// to pick the correct textures; it had no Avalonia editor before (was only in WinForms NSBTX).
    /// </summary>
    public class AreaDataEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); if (!_suppress) Dirty(); return true; }

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
        public decimal MapTileset { get => _mapTileset; set { if (Set(ref _mapTileset, value) && _area != null) _area.mapTileset = (ushort)value; } }
        public decimal BuildingsTileset { get => _buildingsTileset; set { if (Set(ref _buildingsTileset, value) && _area != null) _area.buildingsTileset = (ushort)value; } }
        public decimal DynamicTextureType { get => _dynamicTextureType; set { if (Set(ref _dynamicTextureType, value) && _area != null) { if (IsHGSS) _area.dynamicTextureType = (ushort)value; else _area.unknown1 = (ushort)value; } } }
        public decimal LightType { get => _lightType; set { if (Set(ref _lightType, value) && _area != null) _area.lightType = (ushort)value; } }

        private int _areaTypeIndex;
        public int AreaTypeIndex { get => _areaTypeIndex; set { if (Set(ref _areaTypeIndex, value) && _area != null) _area.areaType = (byte)(value == 0 ? 0 : 1); } }

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => $"Area data {_selectedIndex}";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); if (_selectedIndex >= 0) LoadArea(_selectedIndex); }
        private void Dirty() { if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        public AreaDataEditorViewModel() { }
        public AreaDataEditorViewModel(bool _) { }

        public async Task SetupAsync(Window owner)
        {
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.areaData });
                int count = Directory.GetFiles(gameDirs[DirNames.areaData].unpackedDir).Length;
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
                MapTileset = _area.mapTileset;
                BuildingsTileset = _area.buildingsTileset;
                DynamicTextureType = IsHGSS ? _area.dynamicTextureType : _area.unknown1;
                LightType = _area.lightType;
                AreaTypeIndex = _area.areaType == 0 ? 0 : 1;
                _suppress = false;
                SetClean();
                StatusText = $"Loaded area data {index}.";
                OnPropertyChanged(nameof(UnsavedChangesDescription));
            }
            catch (Exception ex) { _ = DialogHelper.ShowError($"Failed to load area data {index}:\n{ex.Message}", "Area Data"); }
        }

        public void Save()
        {
            if (_area == null || _selectedIndex < 0) return;
            try { _area.SaveToFileDefaultDir(_selectedIndex, showSuccessMessage: false); SetClean(); StatusText = $"Saved area data {_selectedIndex}."; }
            catch (Exception ex) { _ = DialogHelper.ShowError($"Save failed:\n{ex.Message}", "Area Data"); }
        }
    }
}

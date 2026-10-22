using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Data;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.World
{
    /// <summary>
    /// Avalonia editor for AreaData files: the records that tie a map area to its texture packs.
    /// Each entry sets the map tileset and buildings tileset (the NSBTX pack indices), plus the
    /// dynamic-texture/area-type/light fields. This is the data the map &amp; event 3D views resolve
    /// to pick the correct textures; it had no Avalonia editor before (was only in WinForms NSBTX).
    /// </summary>
    public class AreaDataEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo, IDetachable
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
        public bool IsHGSS => GameFamily == GameFamilies.HGSS;

        private int _selectedIndex = -1;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (RecordSwitchGuard.IsSnappingBack) return;
                if (value == _selectedIndex) return;
                if (_dirty && !_suppress && value >= 0 && _selectedIndex >= 0)
                {
                    // Snap the list back to the area still loaded until the user has answered.
                    int requested = value;
                    RecordSwitchGuard.SnapBack(() => _selectedIndex, v => _selectedIndex = v, () => OnPropertyChanged(nameof(SelectedIndex)));
                    _ = SwitchAreaAsync(requested);
                    return;
                }
                if (Set(ref _selectedIndex, value) && !_suppress && value >= 0) LoadArea(value);
            }
        }

        private async Task SwitchAreaAsync(int requested)
        {
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(this, null, "area")) return;
            SetClean();
            // Discarding can drop the entry being switched to when it was added and not saved.
            if (requested >= AreaNames.Count) requested = AreaNames.Count - 1;
            if (Set(ref _selectedIndex, requested, nameof(SelectedIndex))) LoadArea(requested);
        }

        /// <summary>Area data entry to select once loaded (e.g. from the Header editor's "Open" button).</summary>
        public int InitialIndex { get; set; }

        private decimal _mapTileset, _buildingsTileset, _thirdField, _lightType;
        public decimal MapTileset { get => _mapTileset; set { if (Set(ref _mapTileset, value) && _area != null) { _area.MapTileset = (ushort)value; OnPropertyChanged(nameof(MapTexturePlaces)); if (!_suppress) Dirty(); } } }
        public decimal BuildingsTileset { get => _buildingsTileset; set { if (Set(ref _buildingsTileset, value) && _area != null) { _area.BuildingsTileset = (ushort)value; OnPropertyChanged(nameof(BuildingTexturePlaces)); if (!_suppress) Dirty(); } } }

        // Texture packs have no names, so each is shown by the places whose area data uses it.
        public string MapTexturePlaces => PlacesUsingPack((int)_mapTileset, buildings: false);
        public string BuildingTexturePlaces => PlacesUsingPack((int)_buildingsTileset, buildings: true);

        // Per area data entry, its two packs as saved and the places of the headers that use it. Read once.
        private (int Map, int Buildings)[] _areaPacks;
        private List<string>[] _areaPlaces;

        private void ReadPackUsers()
        {
            if (_areaPacks != null) return;
            int count = AreaNames.Count;
            _areaPacks = new (int, int)[count];
            _areaPlaces = new List<string>[count];
            IReadOnlyList<string> labels = HeaderLabels.Friendly();
            Dictionary<int, List<ushort>> byArea = ProjectIndex.HeadersByRef(RefKind.AreaData);
            for (int i = 0; i < count; i++)
            {
                _areaPacks[i] = (-1, -1);
                try { AreaData a = new AreaData(new MemoryStream(_archive.Read(i))); _areaPacks[i] = (a.MapTileset, a.BuildingsTileset); }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }   // an area that can't be read shows no packs
                _areaPlaces[i] = new List<string>();
                if (!byArea.TryGetValue(i, out List<ushort> headers)) continue;
                foreach (ushort h in headers)
                {
                    // Friendly labels read "NNN Place"; the place alone is what tells packs apart.
                    string label = h < labels.Count ? labels[h] : "";
                    string place = label.Length > 4 ? label.Substring(4) : label;
                    if (place.Length > 0 && !_areaPlaces[i].Contains(place)) _areaPlaces[i].Add(place);
                }
            }
        }

        private string PlacesUsingPack(int pack, bool buildings)
        {
            if (AreaNames.Count == 0) return "";
            ReadPackUsers();
            List<string> places = new List<string>();
            for (int i = 0; i < _areaPacks.Length; i++)
            {
                // The entry open here counts with its unsaved value, the rest as saved.
                int used = i == _selectedIndex ? pack : (buildings ? _areaPacks[i].Buildings : _areaPacks[i].Map);
                if (used != pack) continue;
                foreach (string p in _areaPlaces[i]) if (!places.Contains(p)) places.Add(p);
            }
            if (places.Count == 0) return "No map uses it";
            // Location 0 is the game's placeholder place for unused headers; real places go first.
            string placeholder = null;
            try { List<string> names = GetLocationNames(); if (names != null && names.Count > 0) placeholder = names[0]?.Trim(); }
            catch (Exception e) when (e is IOException || e is InvalidOperationException) { }   // without location names no placeholder is moved last
            if (!string.IsNullOrEmpty(placeholder) && places.Remove(placeholder)) places.Add(placeholder);
            const int Shown = 3;
            return string.Join(", ", places.Take(Shown)) + (places.Count > Shown ? $" +{places.Count - Shown}" : "");
        }
        /// <summary>Third field of the area record. HGSS keeps the terrain animation index
        /// here; DP/Pt keeps a model set index that nothing in the game reads, so the label follows the game.</summary>
        public string ThirdFieldLabel => IsHGSS ? "Global NSBTA" : "Unused";
        public string ThirdFieldTip => IsHGSS
            ? "Area-wide texture animation, like moving water. 65535 is none."
            : "Diamond, Pearl and Platinum store a model set number here but never read it, so changing it does nothing.";

        public decimal ThirdField
        {
            get => _thirdField;
            set
            {
                if (!Set(ref _thirdField, value) || _area == null) return;
                if (IsHGSS) _area.GroundAnimation = (ushort)value; else _area.MovingModelSet = (ushort)value;
                OnPropertyChanged(nameof(ThirdFieldNote));
                if (!_suppress) Dirty();
            }
        }

        /// <summary>65535 is the game's way of saying there is no animation, so say that rather than the number.</summary>
        public string ThirdFieldNote => IsHGSS && _thirdField == 65535 ? "none" : "";
        public decimal LightType
        {
            get => _lightType;
            set
            {
                if (!Set(ref _lightType, value)) return;
                OnPropertyChanged(nameof(LightTypeIndex));
                OnPropertyChanged(nameof(LightTypeNote));
                if (_area != null) { _area.LightType = (ushort)value; if (!_suppress) Dirty(); }
            }
        }

        /// <summary>The open game's light names, renamable per project under World labels.</summary>
        public System.Collections.Generic.IReadOnlyList<string> LightTypeNames => Data.LabelStore.Get(Data.AreaLightTypes.LabelKey);
        public int LightTypeIndex
        {
            get => _lightType >= 0 && _lightType < LightTypeNames.Count ? (int)_lightType : -1;
            set { if (value >= 0) LightType = value; }
        }
        /// <summary>What the chosen light type does in this game.</summary>
        public string LightTypeNote => Data.AreaLightTypes.NoteFor((int)_lightType);

        private int _areaTypeIndex;
        public int AreaTypeIndex { get => _areaTypeIndex; set { if (Set(ref _areaTypeIndex, value) && _area != null) { _area.AreaType = (byte)(value == 0 ? 0 : 1); if (!_suppress) Dirty(); } } }

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        private bool _dirty;
        public bool HasUnsavedChanges => _dirty || _archive.IsDirty;
        public string UnsavedChangesDescription => DSPRE.Avalonia.Data.StagedArchive.Join($"Area data {_selectedIndex}", _dirty, _archive.Describe("area data", "area data"));
        public void SaveChanges() => Save();
        public void DiscardChanges()
        {
            // Added and removed entries go with the open entry's edits.
            _dirty = false;
            _archive.Discard();
            _removedAreas.Clear();
            ListAreas();
            if (_selectedIndex >= AreaNames.Count) _selectedIndex = AreaNames.Count - 1;
            OnPropertyChanged(nameof(SelectedIndex));
            AreaCountChanged();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            if (_selectedIndex >= 0) LoadArea(_selectedIndex);
        }

        // Added, removed and saved entries wait here until Save.
        private readonly StagedArchive _archive = new(DirNames.AreaData);

        private void ListAreas()
        {
            int count = _archive.Count;
            AreaNames.Clear();
            for (int i = 0; i < count; i++) AreaNames.Add("Area data " + i);
            OnPropertyChanged(nameof(RemoveTip));
        }
        // RecordUndoSnapshot runs BEFORE the _dirty short-circuit so EVERY edit is captured (not just the first).
        private void Dirty() { RecordUndoSnapshot(); if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        // Notifies even when only staged entries were waiting, so the title mark clears after a save.
        private void SetClean() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        // ── Undo / redo (ISupportsUndo) ────────────────────────────────────────
        private readonly UndoHistory<byte[]> _history = new();
        private DateTime _lastCaptureUtc = DateTime.MinValue;
        private const int CoalesceMs = 500;

        public bool CanUndo => _history.CanUndo || _removedAreas.Count > 0;
        public bool CanRedo => _history.CanRedo;
        public void Undo()
        {
            if (_history.CanUndo) ApplyState(_history.Undo());
            else if (_removedAreas.Count > 0) RestoreRemovedArea();
        }
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
            MapTileset = _area.MapTileset;
            BuildingsTileset = _area.BuildingsTileset;
            ThirdField = IsHGSS ? _area.GroundAnimation : _area.MovingModelSet;
            LightType = _area.LightType;
            AreaTypeIndex = _area.AreaType == 0 ? 0 : 1;
            OnPropertyChanged(nameof(MapTexturePlaces));
            OnPropertyChanged(nameof(BuildingTexturePlaces));
        }

        public AreaDataEditorViewModel() { }
        public AreaDataEditorViewModel(bool _) { AppEvents.AreaDataSaved += OnSavedElsewhere; }

        /// <summary>For a standalone window closing; the Maps workspace's instance lives for the session.</summary>
        public void Detach() => AppEvents.AreaDataSaved -= OnSavedElsewhere;

        // The Map editor edits areas too: show its save, unless this copy holds its own edits.
        private void OnSavedElsewhere(object sender, int id)
        {
            if (ReferenceEquals(sender, this) || id != _selectedIndex) return;
            if (_dirty) { StatusText = $"Area data {id} was saved in another window. Save or discard here to see it."; return; }
            LoadArea(id);
            StatusText = "";
        }

        public async Task SetupAsync(Window owner)
        {
            // The Maps workspace builds this before any ROM is open, when every game-dependent label reads as
            // Diamond/Pearl/Platinum; say them again for the game now open.
            OnPropertyChanged(nameof(IsHGSS));
            OnPropertyChanged(nameof(ThirdFieldLabel));
            OnPropertyChanged(nameof(ThirdFieldTip));
            OnPropertyChanged(nameof(ThirdFieldNote));
            OnPropertyChanged(nameof(LightTypeNames));
            OnPropertyChanged(nameof(LightTypeIndex));
            OnPropertyChanged(nameof(LightTypeNote));
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.AreaData });
                ListAreas();
                int count = AreaNames.Count;
                StatusText = $"{count} area data entries.";
                if (count > 0) SelectedIndex = Math.Min(Math.Max(0, InitialIndex), count - 1);
            }
            catch (Exception ex)
            {
                StatusText = "Error: " + ex.Message;
                await DialogHelper.ShowErrorAsync($"Failed to set up Area data editor:\n{ex.Message}", "Area data error");
            }
        }

        private void LoadArea(int index)
        {
            try
            {
                _area = new AreaData(new MemoryStream(_archive.Read(index) ?? throw new FileNotFoundException($"Area data {index} is missing.")));
                _suppress = true;
                PopulateFromArea();
                _suppress = false;
                SetClean();
                _history.Reset(_area.ToByteArray());   // loaded state is the clean undo baseline for this area
                _lastCaptureUtc = DateTime.MinValue;
                RaiseUndoState();
                StatusText = "";
                OnPropertyChanged(nameof(UnsavedChangesDescription));
            }
            catch (Exception ex) { _ = DialogHelper.ShowErrorAsync($"Failed to load area data {index}:\n{ex.Message}", "Area data error"); }
        }

        // ── Import / export of the shown entry ──────────────────────────────────────────
        private static readonly FilePickerFileType[] AreaFiles =
        {
            new FilePickerFileType("Area data") { Patterns = new[] { "*.bin" } },
            new FilePickerFileType("All files") { Patterns = new[] { "*" } },
        };

        public async Task ExportAsync(Window owner)
        {
            if (_area == null || _selectedIndex < 0) return;
            string path = await DialogHelper.SaveFileAsync(owner, "Export area data", AreaFiles, $"area_data_{_selectedIndex:D4}.bin");
            if (path == null) return;
            try { PlainFiles.WriteBytes(path, _area.ToByteArray()); SaveNotice.Exported(path); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                await DialogHelper.ShowErrorAsync($"Export failed:\n{ex.Message}", "Area data error");
            }
        }

        /// <summary>Puts an exported entry in place of the shown one, unsaved and undoable.</summary>
        public async Task ImportAsync(Window owner)
        {
            if (_area == null || _selectedIndex < 0) return;
            string path = await DialogHelper.OpenFileAsync(owner, "Import area data", AreaFiles);
            if (path == null) return;
            byte[] data;
            try { data = File.ReadAllBytes(path); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                await DialogHelper.ShowErrorAsync($"Import failed:\n{ex.Message}", "Area data error");
                return;
            }
            // Every game's entry is eight bytes; anything else is some other file.
            if (data.Length != _area.ToByteArray().Length)
            {
                await DialogHelper.ShowErrorAsync($"That file is {data.Length} bytes; an area data entry is {_area.ToByteArray().Length}.", "Area data error");
                return;
            }
            _area = new AreaData(new MemoryStream(data));
            _suppress = true;
            PopulateFromArea();
            _suppress = false;
            Dirty();
            SaveNotice.Imported($"area data {_selectedIndex}", path);
        }

        // ── Add / remove (the last entry only: headers refer to area data by number) ─────

        // Removed entries, newest last, so Ctrl+Z can put them back while no field edit is left to undo.
        private readonly Stack<byte[]> _removedAreas = new();

        public string RemoveTip => AreaNames.Count > 1
            ? $"Removes entry {AreaNames.Count - 1}, the last one"
            : "There has to be at least one entry.";

        /// <summary>Adds an entry at the end, a copy of entry 0. Headers hold area data numbers in one byte, so 256 is the most.</summary>
        public async Task AddAsync()
        {
            int count = AreaNames.Count;
            if (count >= 256) { await DialogHelper.ShowErrorAsync("Headers can only point at area data 0 to 255.", "Area data error"); return; }
            _archive.Add(_archive.Read(0));
            _removedAreas.Clear();
            AreaNames.Add("Area data " + count);
            SelectedIndex = count;
            StatusText = $"Added area data {count}, a copy of 0.";
            AreaCountChanged();
        }

        public async Task RemoveLastAsync()
        {
            int last = AreaNames.Count - 1;
            if (last <= 0) { await DialogHelper.ShowErrorAsync("There has to be at least one area data entry.", "Area data error"); return; }
            if (!await DialogHelper.AskYesNoAsync($"Remove area data {last}? Only the last entry can be removed. Ctrl+Z puts it back.", "Delete area data?"))
                return;
            if (_selectedIndex == last && _dirty) SetClean();
            _removedAreas.Push(_archive.Read(last));
            _archive.RemoveLast();
            if (_selectedIndex >= last) SelectedIndex = last - 1;
            AreaNames.RemoveAt(last);
            StatusText = $"Removed area data {last}.";
            AreaCountChanged();
        }

        private void RestoreRemovedArea()
        {
            int id = AreaNames.Count;
            _archive.Add(_removedAreas.Pop());
            AreaNames.Add("Area data " + id);
            SelectedIndex = id;
            StatusText = $"Put area data {id} back.";
            AreaCountChanged();
        }

        private void AreaCountChanged()
        {
            _areaPacks = null;
            OnPropertyChanged(nameof(RemoveTip));
            OnPropertyChanged(nameof(HasUnsavedChanges));
            RaiseUndoState();
        }

        /// <summary>Opens the texture editor on this entry's map or building pack.</summary>
        public void OpenTextures(bool buildings) => AvaloniaEditorLauncher.OpenNsbtxEditor(buildings, (int)(buildings ? _buildingsTileset : _mapTileset));

        public void Save()
        {
            if (_area == null || _selectedIndex < 0) return;
            try { _archive.Save(_selectedIndex, _area.ToByteArray()); if (_areaPacks != null && _selectedIndex < _areaPacks.Length) _areaPacks[_selectedIndex] = (_area.MapTileset, _area.BuildingsTileset); SetClean(); _history.MarkSaved(); RaiseUndoState(); StatusText = $"Saved area data {_selectedIndex}."; SaveNotice.Saved(UnsavedChangesDescription); AppEvents.RaiseAreaDataSaved(this, _selectedIndex); }
            catch (Exception ex) { _ = DialogHelper.ShowErrorAsync($"Save failed:\n{ex.Message}", "Area data error"); }
        }
    }
}

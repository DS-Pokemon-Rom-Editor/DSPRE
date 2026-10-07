using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using global::Avalonia.Collections;
using global::Avalonia.Controls;
using global::Avalonia.Threading;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Models;
using DSPRE.Editors;
using DSPRE.Resources;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

using DSPRE.Avalonia.Data;
using Avalonia.Platform.Storage;
namespace DSPRE.Avalonia.ViewModels.World
{
    /// <summary>
    /// A combo whose visible names map to non-contiguous raw IDs (camera angle,
    /// weather, music). Keeps the name list and the parallel raw-ID list together so
    /// the editor can sync a ComboBox with a NumericUpDown.
    /// </summary>
    public class MappedCombo
    {
        public ObservableCollection<string> Names { get; } = new ObservableCollection<string>();
        public List<int> Keys { get; } = new List<int>();

        public void Load<TKey>(Dictionary<TKey, string> dict) where TKey : struct, IConvertible
        {
            Names.Clear(); Keys.Clear();
            foreach (KeyValuePair<TKey, string> kv in dict) { Keys.Add(Convert.ToInt32(kv.Key)); Names.Add(kv.Value); }
        }
        /// <summary>Fills from a label category, value = position, updating in place so selections hold.</summary>
        public void LoadLabels(string category)
        {
            IReadOnlyList<string> labels = LabelStore.Get(category);
            ListSync.Apply(Names, labels);
            Keys.Clear();
            for (int i = 0; i < labels.Count; i++) Keys.Add(i);
        }

        public int IndexOf(int value) => Keys.IndexOf(value);
        public int KeyAt(int index) => index >= 0 && index < Keys.Count ? Keys[index] : -1;
    }

    /// <summary>
    /// Avalonia port of the WinForms <c>HeaderEditor</c>. Edits map headers: all
    /// common fields plus the game-family-specific ones (DP/Plat location specifier
    /// &amp; Plat area icon; HGSS area icon, world-map coords, follow mode, Kanto flag,
    /// location type). Camera/weather/music expose synced combo+numeric pairs with
    /// preview images. Save writes through <c>MapHeader.ToByteArray()</c> (which does
    /// the per-family bit-packing) to ARM9 or the dynamic-headers file.
    ///
    /// Not yet ported (cross-editor / peripheral): the "create associated files"
    /// prompt on add-header, the Advanced Header Search sub-form, and the
    /// open-wild/script/level-script/area-data navigation buttons.
    /// </summary>
    public class HeaderEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private Window _owner;
        private bool _suppress;
        private bool _dynamicHeaders;

        private MapHeader _header;
        private List<string> _internalNames = new List<string>();
        private List<string> _headerListNames = new List<string>();

        public ObservableCollection<string> LocationNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> AreaSettingsItems { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> AreaIconItems { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> FollowModeItems { get; } = new ObservableCollection<string> { "Unallowed", "Small only", "All" };

        public MappedCombo Camera { get; } = new MappedCombo();
        public MappedCombo Weather { get; } = new MappedCombo();
        public MappedCombo MusicDay { get; } = new MappedCombo();
        public MappedCombo MusicNight { get; } = new MappedCombo();

        // ── Family gating ───────────────────────────────────────────────────────────
        public bool IsHgss => gameFamily == GameFamilies.HGSS;
        public bool IsDp => gameFamily == GameFamilies.DP;
        public bool ShowAreaIcon { get; private set; }
        public bool ShowHgssOnly => IsHgss;
        public bool CanAddRemove { get; private set; }
        public decimal WildPokeMax { get; private set; } = 65535;

        // Widths of the packed header fields, so a box cannot hold a value the save would mask.
        public decimal BattleBackgroundMax => IsDp ? 15 : 31;
        public decimal WeatherMax => IsHgss ? 127 : 255;
        public decimal CameraMax => IsHgss ? 63 : 255;

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        // Context-strip identity for the Maps workspace: the location name (falling back to the internal
        // name) and the header number.
        public string SelectedHeaderTitle =>
            _locationNameIndex >= 0 && _locationNameIndex < LocationNames.Count && !string.IsNullOrWhiteSpace(LocationNames[_locationNameIndex])
                ? LocationNames[_locationNameIndex].Trim()
                : (_header != null ? _internalName : "-");
        public string SelectedHeaderSubtitle => _header != null ? $"header {_header.ID:D3}" : "";

        /// <summary>The currently loaded header's own ID, or -1 if none is loaded. Fed to the Maps
        /// workspace's embedded Map tab so its "This header" view can follow the sidebar selection.</summary>
        public int CurrentHeaderId => _header?.ID ?? -1;

        // ── Sidebar tree (location-grouped) ──────────────────────────────────────────
        // Bound directly to the TreeView's ItemsSource, so this MUST be typed HeaderTreeNode (the base
        // type), even though only HeaderTreeFolder instances are ever added at the root: Avalonia's
        // TreeView resolves a clicked item's container via ItemsSourceView.IndexOf against this
        // collection's IList.IndexOf(object), which does an unchecked (T)value cast before comparing;
        // clicking a HeaderTreeLeaf nested under a folder throws InvalidCastException there if T is
        // narrowed to HeaderTreeFolder, because the cast fails before it ever gets to "not found in this
        // list, must be nested deeper." Root-only, strongly-typed iteration should use _allTreeFolders.
        private readonly AvaloniaList<HeaderTreeNode> _treeFolders = new AvaloniaList<HeaderTreeNode>();
        public AvaloniaList<HeaderTreeNode> TreeFolders => _treeFolders;
        private List<HeaderTreeFolder> _allTreeFolders = new List<HeaderTreeFolder>();
        private IReadOnlyList<HeaderSearchEntry> _headerSearchIndex = Array.Empty<HeaderSearchEntry>();
        private List<int> _locationIndexByHeader = new List<int>();
        private Dictionary<HeaderTreeFolder, bool> _folderExpansionBeforeFilter;
        private bool _filterTreeActive;

        private CancellationTokenSource _treeFilterCancellation;
        private int _treeFilterGeneration;

        private HeaderTreeNode _selectedTreeNode;
        public HeaderTreeNode SelectedTreeNode
        {
            get => _selectedTreeNode;
            set
            {
                // Hiding the TreeView while a filter is active can make its binding briefly report
                // null. Keep the logical selection stable instead of reloading or losing the header.
                if (RecordSwitchGuard.IsSnappingBack) return;
                if (value == null && !_suppress && _selectedTreeNode != null) return;
                if (!_suppress && value is HeaderTreeLeaf pick && MustAskBeforeLeaving(pick.HeaderId))
                {
                    // Leave the tree on the loaded header until the user answers.
                    RecordSwitchGuard.SnapBack(() => _selectedTreeNode, v => _selectedTreeNode = v, () => OnPropertyChanged(nameof(SelectedTreeNode)), _ => null);
                    if (!_switchPending) _ = SwitchHeaderAsync(pick.HeaderId);
                    return;
                }
                if (!Set(ref _selectedTreeNode, value) || _suppress) return;
                if (value is HeaderTreeLeaf leaf) { SelectedHeaderId = leaf.HeaderId; LoadHeader(leaf.HeaderId); }
            }
        }

        private bool _switchPending;

        /// <summary>Set by the Maps workspace: whether a tab with unsaved edits would move to another file for this
        /// header, and the prompt for those tabs. The header only moves once every one of them has answered.</summary>
        public Func<ushort, bool> LinkedEditsWouldMove { get; set; }
        public Func<ushort, Task<bool>> ConfirmLinkedTabsAsync { get; set; }

        private bool MustAskBeforeLeaving(ushort id)
            => _header != null && id != _header.ID && (_dirty || (LinkedEditsWouldMove?.Invoke(id) ?? false));

        private async Task SwitchHeaderAsync(ushort id)
        {
            _switchPending = true;
            try
            {
                if (!await RecordSwitchGuard.ConfirmLeaveAsync(this, _owner, "header")) return;
                if (ConfirmLinkedTabsAsync != null && !await ConfirmLinkedTabsAsync(id)) return;
                SetClean();
                if (!string.IsNullOrWhiteSpace(TreeFilterText) && FindLeaf(id) is { IsVisible: false }) TreeFilterText = "";
                SelectHeader(id);
            }
            finally { _switchPending = false; }
        }

        private ushort _selectedHeaderId;
        public ushort SelectedHeaderId { get => _selectedHeaderId; private set => Set(ref _selectedHeaderId, value); }

        public bool IsFiltering => !string.IsNullOrWhiteSpace(_treeFilterText);

        private string _treeFilterText = "";
        public string TreeFilterText
        {
            get => _treeFilterText;
            set
            {
                if (!Set(ref _treeFilterText, value ?? string.Empty)) return;
                OnPropertyChanged(nameof(IsFiltering));
                ScheduleTreeRebuild();
            }
        }

        private bool _fuzzySearch;
        public bool FuzzySearch
        {
            get => _fuzzySearch;
            set { if (Set(ref _fuzzySearch, value)) ScheduleTreeRebuild(); }
        }

        // ── Common scalar fields ─────────────────────────────────────────────────────

        private string _internalName = "";
        public string InternalName
        {
            get => _internalName;
            set { if (Set(ref _internalName, value)) { UpdateInternalNameFeedback(); SetDirty(); } }
        }

        private IBrush _internalNameColor = StatusBrushes.Good;
        public IBrush InternalNameColor { get => _internalNameColor; set => Set(ref _internalNameColor, value); }
        private string _internalNameLen = "[ 0 ]";
        public string InternalNameLen { get => _internalNameLen; set => Set(ref _internalNameLen, value); }

        private decimal _matrixId; public decimal MatrixId { get => _matrixId; set { if (Set(ref _matrixId, value)) Apply(h => h.matrixID = (ushort)value); } }
        private decimal _areaDataId; public decimal AreaDataId { get => _areaDataId; set { if (Set(ref _areaDataId, value)) { Apply(h => h.areaDataID = (byte)value); UpdateWeatherRoom(); } } }
        private decimal _scriptFileId; public decimal ScriptFileId { get => _scriptFileId; set { if (Set(ref _scriptFileId, value)) Apply(h => h.scriptFileID = (ushort)value); } }
        private decimal _levelScriptId; public decimal LevelScriptId { get => _levelScriptId; set { if (Set(ref _levelScriptId, value)) Apply(h => h.levelScriptID = (ushort)value); } }
        private decimal _eventFileId; public decimal EventFileId { get => _eventFileId; set { if (Set(ref _eventFileId, value)) Apply(h => h.eventFileID = (ushort)value); } }
        private decimal _textArchiveId; public decimal TextArchiveId { get => _textArchiveId; set { if (Set(ref _textArchiveId, value)) Apply(h => h.textArchiveID = (ushort)value); } }
        private decimal _wildPokemon; public decimal WildPokemon { get => _wildPokemon; set { if (Set(ref _wildPokemon, value)) { Apply(h => h.wildPokemon = (ushort)value); OnPropertyChanged(nameof(CanOpenEncounters)); } } }
        private decimal _battleBackground; public decimal BattleBackground { get => _battleBackground; set { if (Set(ref _battleBackground, value)) Apply(h => h.battleBackground = (byte)value); } }

        // No-encounter sentinel (0xffff DPPt / 0xff HGSS): the wild editor clamps it to file 0, so the
        // "Open" affordance is disabled/no-op there rather than silently opening an unrelated file.
        private int NullEncounterId => IsHgss ? MapHeader.HGSS_NULL_ENCOUNTER_FILE_ID : MapHeader.DPPT_NULL_ENCOUNTER_FILE_ID;
        public bool CanOpenEncounters => _header != null && (int)_wildPokemon != NullEncounterId;
        /// <summary>The encounter number that means "no wild Pokémon" in the open game.</summary>
        public int NoEncountersId => NullEncounterId;

        // ── Camera (combo + numeric) ─────────────────────────────────────────────────
        private decimal _cameraValue;
        public decimal CameraValue
        {
            get => _cameraValue;
            set { if (Set(ref _cameraValue, value)) { Apply(h => h.cameraAngleID = (byte)value); SyncCameraCombo(); } }
        }
        private int _cameraComboIndex = -1;
        public int CameraComboIndex
        {
            get => _cameraComboIndex;
            set { if (Set(ref _cameraComboIndex, value) && !_suppress && value >= 0) CameraValue = Camera.KeyAt(value); }
        }

        // ── Weather (combo + numeric) ────────────────────────────────────────────────
        private decimal _weatherValue;
        public decimal WeatherValue
        {
            get => _weatherValue;
            set { if (Set(ref _weatherValue, value)) { Apply(h => h.weatherID = (byte)value); SyncWeatherCombo(); UpdateWeatherRoom(); } }
        }
        private int _weatherComboIndex = -1;
        public int WeatherComboIndex
        {
            get => _weatherComboIndex;
            set { if (Set(ref _weatherComboIndex, value) && !_suppress && value >= 0) WeatherValue = Weather.KeyAt(value); }
        }

        public bool WeatherRoomChecked => RomInfo.gameFamily == GameFamilies.Plat;

        private string _weatherRoomWarning;
        /// <summary>Set when the weather's background is estimated not to fit next to this header's area data.</summary>
        public string WeatherRoomWarning { get => _weatherRoomWarning; private set { if (Set(ref _weatherRoomWarning, value)) OnPropertyChanged(nameof(HasWeatherRoomWarning)); } }
        public bool HasWeatherRoomWarning => _weatherRoomWarning != null;

        private string _weatherRoomDetail;
        public string WeatherRoomDetail { get => _weatherRoomDetail; private set => Set(ref _weatherRoomDetail, value); }

        private void UpdateWeatherRoom()
        {
            OnPropertyChanged(nameof(WeatherRoomChecked));
            FieldWeatherRoom.Result room = FieldWeatherRoom.Check((int)_weatherValue, (int)_areaDataId);
            WeatherRoomDetail = room == null ? null
                : $"Background needs {room.Needed:N0} bytes. Area {(int)_areaDataId} leaves about {Math.Max(0, room.Free):N0}.";
            WeatherRoomWarning = room == null || room.Fits ? null : "May black-screen on door, Fly or save loads";
        }

        // ── Music day / night (combo + numeric) ──────────────────────────────────────
        private decimal _musicDayValue;
        public decimal MusicDayValue
        {
            get => _musicDayValue;
            set { if (Set(ref _musicDayValue, value)) { Apply(h => h.musicDayID = (ushort)value); SyncMusicCombo(MusicDay, (int)value, i => _musicDayComboIndex = i, nameof(MusicDayComboIndex)); OnPropertyChanged(nameof(DayPlayIcon)); } }
        }
        private int _musicDayComboIndex = -1;
        public int MusicDayComboIndex
        {
            get => _musicDayComboIndex;
            set { if (Set(ref _musicDayComboIndex, value) && !_suppress && value >= 0) MusicDayValue = MusicDay.KeyAt(value); }
        }

        private decimal _musicNightValue;
        public decimal MusicNightValue
        {
            get => _musicNightValue;
            set { if (Set(ref _musicNightValue, value)) { Apply(h => h.musicNightID = (ushort)value); SyncMusicCombo(MusicNight, (int)value, i => _musicNightComboIndex = i, nameof(MusicNightComboIndex)); OnPropertyChanged(nameof(NightPlayIcon)); } }
        }
        private int _musicNightComboIndex = -1;
        public int MusicNightComboIndex
        {
            get => _musicNightComboIndex;
            set { if (Set(ref _musicNightComboIndex, value) && !_suppress && value >= 0) MusicNightValue = MusicNight.KeyAt(value); }
        }

        // ── Location name ─────────────────────────────────────────────────────────────
        private int _locationNameIndex = -1;
        public int LocationNameIndex
        {
            get => _locationNameIndex;
            set { if (Set(ref _locationNameIndex, value)) { OnPropertyChanged(nameof(SelectedHeaderTitle)); if (!_suppress && value >= 0) ApplyLocationName(value); } }
        }

        // ── Area settings (DP/Plat = locationSpecifier; HGSS = locationType) ───────────
        private int _areaSettingsIndex = -1;
        public int AreaSettingsIndex
        {
            get => _areaSettingsIndex;
            set { if (Set(ref _areaSettingsIndex, value) && !_suppress && value >= 0) ApplyAreaSettings(value); }
        }

        // ── Area icon (Plat/HGSS) ─────────────────────────────────────────────────────
        private int _areaIconIndex = -1;
        public int AreaIconIndex
        {
            get => _areaIconIndex;
            set { if (Set(ref _areaIconIndex, value) && !_suppress && value >= 0) ApplyAreaIcon(value); }
        }
        private Bitmap _areaIconImage; public Bitmap AreaIconImage { get => _areaIconImage; set => Set(ref _areaIconImage, value); }

        // ── Flags ─────────────────────────────────────────────────────────────────────
        private bool _f0, _f1, _f2, _f3, _f4, _f5, _f6;
        public bool Flag0 { get => _f0; set { if (Set(ref _f0, value)) ApplyFlags(); } }
        public bool Flag1 { get => _f1; set { if (Set(ref _f1, value)) ApplyFlags(); } }
        public bool Flag2 { get => _f2; set { if (Set(ref _f2, value)) ApplyFlags(); } }
        public bool Flag3 { get => _f3; set { if (Set(ref _f3, value)) ApplyFlags(); } }
        public bool Flag4 { get => _f4; set { if (Set(ref _f4, value)) ApplyFlags(); } }
        public bool Flag5 { get => _f5; set { if (Set(ref _f5, value)) ApplyFlags(); } }
        public bool Flag6 { get => _f6; set { if (Set(ref _f6, value)) ApplyFlags(); } }

        // ── HGSS-only ─────────────────────────────────────────────────────────────────
        private decimal _worldmapX; public decimal WorldmapX { get => _worldmapX; set { if (Set(ref _worldmapX, value)) ApplyHgss(h => h.worldmapX = (byte)value); } }
        private decimal _worldmapY; public decimal WorldmapY { get => _worldmapY; set { if (Set(ref _worldmapY, value)) ApplyHgss(h => h.worldmapY = (byte)value); } }
        private decimal _momCallIntroParam; public decimal MomCallIntroParam { get => _momCallIntroParam; set { if (Set(ref _momCallIntroParam, value)) ApplyHgss(h => h.momCallIntroParam = (byte)value); } }
        private int _followModeIndex = -1;
        public int FollowModeIndex { get => _followModeIndex; set { if (Set(ref _followModeIndex, value)) ApplyHgss(h => h.followMode = (byte)Math.Max(0, value)); } }
        private bool _kantoFlag;
        public bool KantoFlag { get => _kantoFlag; set { if (Set(ref _kantoFlag, value)) { ApplyHgss(h => h.kantoFlag = value); OnPropertyChanged(nameof(JohtoFlag)); } } }
        public bool JohtoFlag => !_kantoFlag;

        // ── Dirty tracking ───────────────────────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => _header != null ? $"Header {_header.ID}" : "Header Editor";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); LoadHeader(SelectedHeaderId); }
        // RecordUndoSnapshot runs BEFORE the _dirty short-circuit so EVERY edit is captured (not just the first).
        private void SetDirty() { if (_suppress) return; RecordUndoSnapshot(); if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        // ── Undo / redo (ISupportsUndo) ────────────────────────────────────────
        private readonly DSPRE.Avalonia.UndoHistory<byte[]> _history = new();
        private DateTime _lastCaptureUtc = DateTime.MinValue;
        private const int CoalesceMs = 500;

        public bool CanUndo => _history.CanUndo || _removedHeaders.Count > 0;
        public bool CanRedo => _history.CanRedo;
        public void Undo()
        {
            if (_history.CanUndo) ApplyState(_history.Undo());
            else if (_removedHeaders.Count > 0) RestoreRemovedHeader();
        }
        public void Redo() { if (_history.CanRedo) ApplyState(_history.Redo()); }
        private void RaiseUndoState() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private void ApplyState(byte[] bytes)
        {
            if (bytes == null || _header == null) return;
            int headerLength = _header.ToByteArray().Length;
            _header = MapHeader.LoadFromByteArray(bytes.AsSpan(0, headerLength).ToArray(), _header.ID);
            PopulateFromHeader();   // manages _suppress itself
            _suppress = true;
            try { InternalName = System.Text.Encoding.UTF8.GetString(bytes, headerLength, bytes.Length - headerLength); }
            finally { _suppress = false; }
            _dirty = _history.IsDirty;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            RaiseUndoState();
        }

        // The internal name lives in its own file, not in the header record, so it rides along after the header bytes.
        private byte[] UndoState() => _header.ToByteArray().Concat(System.Text.Encoding.UTF8.GetBytes(_internalName ?? "")).ToArray();

        private void RecordUndoSnapshot()
        {
            if (_suppress || _header == null) return;
            bool coalesce = (DateTime.UtcNow - _lastCaptureUtc).TotalMilliseconds < CoalesceMs;
            _history.Capture(UndoState(), coalesce);
            _lastCaptureUtc = DateTime.UtcNow;
            RaiseUndoState();
        }

        // ── Constructors ────────────────────────────────────────────────────────────
        public HeaderEditorViewModel()
        {
            if (!Design.IsDesignMode) return;
            HeaderTreeFolder folder = new HeaderTreeFolder { DisplayName = "Jubilife City", IsExpanded = true };
            folder.Children.Add(new HeaderTreeLeaf { HeaderId = 3, DisplayName = "003 -   JUBILIFE_CITY" });
            TreeFolders.Add(folder);
        }

        public HeaderEditorViewModel(bool _) { AppEvents.HeaderSaved += OnSavedElsewhere; MusicPreview.Changed += OnMusicChanged; }

        private void OnMusicChanged() => Dispatcher.UIThread.Post(() => { OnPropertyChanged(nameof(DayPlayIcon)); OnPropertyChanged(nameof(NightPlayIcon)); });
        public string DayPlayIcon => MusicPreview.StartedBy((this, false)) ? "stop" : "play";
        public string NightPlayIcon => MusicPreview.StartedBy((this, true)) ? "stop" : "play";

        /// <summary>For a standalone window closing; the Maps workspace's instance lives for the session.</summary>
        public void Detach()
        {
            AppEvents.HeaderSaved -= OnSavedElsewhere;
            AppEvents.LabelsChanged -= OnLabelsChanged;
        }

        // Another open copy of this header saved: show it, unless this copy holds its own edits.
        private void OnSavedElsewhere(object sender, int id)
        {
            if (ReferenceEquals(sender, this) || _header == null || _header.ID != id) return;
            if (HasUnsavedChanges) { StatusText = $"Header {id} was saved in another window. Save or discard here to see it."; return; }
            LoadHeader((ushort)id);
            StatusText = $"Header {id} was saved in another window and reloaded.";
        }

        // ── Setup ─────────────────────────────────────────────────────────────────────
        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            if (!AvaloniaEditorLauncher.IsRomLoaded)
            {
                // The Maps workspace initializes with the main window; without a ROM there is
                // nothing to load (and the setup below would throw on null gameDirs paths).
                StatusText = "No ROM loaded.";
                return;
            }
            StatusText = "Loading headers…";
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.synthOverlay, DirNames.textArchives, DirNames.dynamicHeaders });

                _dynamicHeaders = MapHeader.UsesDynamicHeaders;
                CanAddRemove = _dynamicHeaders;
                OnPropertyChanged(nameof(CanAddRemove));

                _headerListNames = HeaderLists.GetHeaderListBoxNames();
                _internalNames = HeaderLists.GetInternalNames();

                BuildFamilyCombos();
                LoadLocationNames();

                LoadLocationIndices();
                SelectedHeaderId = FindInitialHeaderId();   // so only this header's folder opens on load
                RebuildTree();

                StatusText = $"Loaded {_headerListNames.Count} headers ({gameFamily}).";
                SelectHeader(SelectedHeaderId);
            }
            catch (FileNotFoundException)
            {
                await DialogHelper.ShowError(internalNamesPath + " doesn't exist.", "Couldn't read internal names");
            }
            catch (Exception ex)
            {
                AppLogger.Error("HeaderEditorViewModel.SetupAsync: " + ex);
                await DialogHelper.ShowError($"Failed to load headers:\n{ex.Message}", "Header Editor Error");
            }
        }

        private void BuildFamilyCombos()
        {
            AreaSettingsItems.Clear();
            AreaIconItems.Clear();
            switch (gameFamily)
            {
                case GameFamilies.DP:
                    Camera.LoadLabels(LabelStore.CameraKey);
                    MusicDay.Load(PokeDatabase.MusicDB.DPMusicDict);
                    MusicNight.Load(PokeDatabase.MusicDB.DPMusicDict);
                    Weather.LoadLabels(LabelStore.WeatherKey);
                    foreach (string s in PokeDatabase.MapType.DPPtValues) AreaSettingsItems.Add(s);
                    ShowAreaIcon = false;
                    WildPokeMax = 65535;
                    break;
                case GameFamilies.Plat:
                    Camera.LoadLabels(LabelStore.CameraKey);
                    MusicDay.Load(PokeDatabase.MusicDB.PtMusicDict);
                    MusicNight.Load(PokeDatabase.MusicDB.PtMusicDict);
                    Weather.LoadLabels(LabelStore.WeatherKey);
                    foreach (string s in PokeDatabase.MapType.DPPtValues) AreaSettingsItems.Add(s);
                    foreach (string s in PokeDatabase.Area.PtAreaIconValues) AreaIconItems.Add(s);
                    ShowAreaIcon = true;
                    WildPokeMax = 65535;
                    break;
                default:
                    Camera.LoadLabels(LabelStore.CameraKey);
                    MusicDay.Load(PokeDatabase.MusicDB.HGSSMusicDict);
                    MusicNight.Load(PokeDatabase.MusicDB.HGSSMusicDict);
                    Weather.LoadLabels(LabelStore.WeatherKey);
                    foreach (string s in PokeDatabase.Area.HGSSAreaProperties) AreaSettingsItems.Add(s);
                    foreach (string s in PokeDatabase.Area.HGSSAreaIconsDict.Values) AreaIconItems.Add(s);
                    ShowAreaIcon = true;
                    WildPokeMax = 255;
                    break;
            }
            AppEvents.LabelsChanged -= OnLabelsChanged;
            AppEvents.LabelsChanged += OnLabelsChanged;
            OnPropertyChanged(nameof(ShowAreaIcon));
            OnPropertyChanged(nameof(WildPokeMax));
            OnPropertyChanged(nameof(NoEncountersId));
            OnPropertyChanged(nameof(BattleBackgroundMax));
            OnPropertyChanged(nameof(WeatherMax));
            OnPropertyChanged(nameof(CameraMax));
        }

        private void LoadLocationNames()
        {
            LocationNames.Clear();
            foreach (string m in ReadLocationNames()) LocationNames.Add(m);
        }

        private List<string> ReadLocationNames()
        {
            try { return new TextArchive(locationNamesTextNumber).messages.ToList(); }
            catch { return new List<string>(); }
        }

        // ── Sidebar tree: grouping, search, selection ───────────────────────────────

        /// <summary>Reads each header's location-name index once (for grouping + initial selection).</summary>
        private void LoadLocationIndices()
        {
            int mystery = FindMysteryZoneIndex();
            _locationIndexByHeader = new List<int>(_headerListNames.Count);
            for (ushort id = 0; id < _headerListNames.Count; id++)
                _locationIndexByHeader.Add(
                    MapHeader.TryReadLocationNameIndex(id, _dynamicHeaders, out int idx) ? idx : mystery);
        }

        private int FindMysteryZoneIndex()
        {
            for (int i = 0; i < LocationNames.Count; i++)
                if (LocationNames[i] != null && LocationNames[i].Trim().EndsWith("Mystery Zone", StringComparison.OrdinalIgnoreCase))
                    return i;
            return 0;
        }

        /// <summary>Header id to open on load (set before SetupAsync; e.g. from a "Go to Header #N" jump). -1 = auto.</summary>
        public int InitialHeaderId { get; set; } = -1;

        private ushort FindInitialHeaderId()
        {
            if (InitialHeaderId >= 0 && InitialHeaderId < _locationIndexByHeader.Count)
                return (ushort)InitialHeaderId;

            // Open where a new game starts, which is far more useful than whichever header happens to sort
            // first.
            int start = FieldStartLocation.HeaderFor(gameFamily, _headerListNames);
            if (start >= 0 && start < _locationIndexByHeader.Count) return (ushort)start;

            int mystery = FindMysteryZoneIndex();
            for (ushort id = 0; id < _locationIndexByHeader.Count; id++)
                if (_locationIndexByHeader[id] != mystery)
                    return id;
            return 0;
        }

        /// <summary>
        /// Rebuilds <see cref="TreeFolders"/> from the header list, grouped by location (all "Route *"
        /// in one "Routes" bucket); folders and leaves come out ascending by ID. A non-empty
        /// <see cref="TreeFilterText"/> is projected into the flat filtered list; an empty one
        /// restores the tree and collapses all but the selected header's folder. Selection is preserved.
        /// </summary>
        private void RebuildTree()
        {
            CancelPendingTreeRebuild();
            _filterTreeActive = false;
            _folderExpansionBeforeFilter = null;
            TreeBuildInput input = CaptureTreeBuildInput();
            ApplyTreeBuild(BuildTreeStructure(input), input.Query, input.Fuzzy);
        }

        private void ScheduleTreeRebuild()
        {
            CancelPendingTreeRebuild();
            if (_headerSearchIndex.Count == 0 || _allTreeFolders.Count == 0) return;

            string query = (TreeFilterText ?? "").Trim();
            TreeFilterInput input = new TreeFilterInput(_headerSearchIndex, query, _fuzzySearch);
            if (!input.Filtering)
            {
                ApplyTreeFilter(input, new HashSet<ushort>());
                return;
            }

            int generation = unchecked(++_treeFilterGeneration);
            CancellationTokenSource cancellation = new CancellationTokenSource();
            _treeFilterCancellation = cancellation;
            _ = RebuildTreeAsync(cancellation, generation, input);
        }

        private TreeBuildInput CaptureTreeBuildInput()
        {
            return new TreeBuildInput
            {
                HeaderNames = _headerListNames.ToList(),
                LocationIndices = _locationIndexByHeader.ToList(),
                LocationNames = LocationNames.ToList(),
                Query = (TreeFilterText ?? "").Trim(),
                Fuzzy = _fuzzySearch
            };
        }

        private async Task RebuildTreeAsync(CancellationTokenSource cancellation, int generation, TreeFilterInput input)
        {
            CancellationToken token = cancellation.Token;
            try
            {
                HashSet<ushort> matches = await Task.Run(() => BuildTreeFilter(input, token), token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();

                Dispatcher.UIThread.Post(() =>
                {
                    if (token.IsCancellationRequested || generation != _treeFilterGeneration) return;
                    ApplyTreeFilter(input, matches);
                });
            }
            catch (OperationCanceledException)
            {
                // A newer filter value superseded this rebuild.
            }
            catch (Exception ex)
            {
                AppLogger.Error("Header tree filter failed: " + ex);
            }
            finally
            {
                if (ReferenceEquals(_treeFilterCancellation, cancellation))
                    _treeFilterCancellation = null;
                cancellation.Dispose();
            }
        }

        private static TreeBuildResult BuildTreeStructure(TreeBuildInput input)
        {
            Dictionary<string, HeaderTreeFolder> byName = new Dictionary<string, HeaderTreeFolder>(StringComparer.OrdinalIgnoreCase);
            List<HeaderTreeFolder> order = new List<HeaderTreeFolder>();
            List<HeaderSearchEntry> searchIndex = new List<HeaderSearchEntry>(input.HeaderNames.Count);

            for (int id = 0; id < input.HeaderNames.Count; id++)
            {
                ushort headerId = (ushort)id;
                string label = input.HeaderNames[id];
                string locationName = LocationNameFor(input, headerId);
                string folderName = FolderNameFor(locationName);

                if (!byName.TryGetValue(folderName, out HeaderTreeFolder folder))
                {
                    folder = new HeaderTreeFolder { DisplayName = folderName };
                    byName[folderName] = folder;
                    order.Add(folder);
                }
                HeaderTreeLeaf leaf = new HeaderTreeLeaf { HeaderId = headerId, DisplayName = label };
                folder.Children.Add(leaf);
                searchIndex.Add(new HeaderSearchEntry(headerId, label, locationName, folderName));
            }

            return new TreeBuildResult(
                order.OrderBy(f => f.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList(),
                searchIndex);
        }

        private static HashSet<ushort> BuildTreeFilter(TreeFilterInput input, CancellationToken cancellationToken)
        {
            HashSet<ushort> matches = new HashSet<ushort>();
            if (!input.Filtering) return matches;

            foreach (HeaderSearchEntry entry in input.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (HeaderMatchesFilter(input.Query, entry, input.Fuzzy))
                    matches.Add(entry.HeaderId);
            }
            return matches;
        }

        private void ApplyTreeBuild(TreeBuildResult result, string query, bool fuzzy)
        {
            _allTreeFolders = result.Folders;
            _headerSearchIndex = result.SearchIndex;
            _treeFolders.Clear();
            _treeFolders.AddRange(result.Folders);
            TreeFilterInput input = new TreeFilterInput(_headerSearchIndex, query, fuzzy);
            ApplyTreeFilter(input, BuildTreeFilter(input, CancellationToken.None));
        }

        private void ApplyTreeFilter(TreeFilterInput input, HashSet<ushort> matches)
        {
            _suppress = true;
            try
            {
                ushort keepHeaderId = SelectedHeaderId;
                if (input.Filtering)
                {
                    if (!_filterTreeActive)
                    {
                        _folderExpansionBeforeFilter = _allTreeFolders.ToDictionary(f => f, f => f.IsExpanded);
                        _filterTreeActive = true;
                    }

                    foreach (HeaderTreeFolder folder in _allTreeFolders)
                    {
                        bool hasMatch = false;
                        foreach (HeaderTreeLeaf leaf in folder.Children.OfType<HeaderTreeLeaf>())
                        {
                            bool visible = matches.Contains(leaf.HeaderId);
                            leaf.IsVisible = visible;
                            hasMatch |= visible;
                        }
                        folder.IsVisible = hasMatch;
                        folder.IsExpanded = hasMatch;
                    }

                    HeaderTreeNode logicalSelection = FindLeaf(keepHeaderId) ?? _selectedTreeNode;
                    if (logicalSelection != null) SelectedTreeNode = logicalSelection;
                }
                else
                {
                    foreach (HeaderTreeFolder folder in _allTreeFolders)
                    {
                        folder.IsVisible = true;
                        foreach (HeaderTreeLeaf leaf in folder.Children.OfType<HeaderTreeLeaf>())
                            leaf.IsVisible = true;

                        if (_filterTreeActive && _folderExpansionBeforeFilter != null
                            && _folderExpansionBeforeFilter.TryGetValue(folder, out bool wasExpanded))
                            folder.IsExpanded = wasExpanded;
                        else
                            folder.IsExpanded = folder.Children.OfType<HeaderTreeLeaf>().Any(l => l.HeaderId == keepHeaderId);
                    }
                    _filterTreeActive = false;
                    _folderExpansionBeforeFilter = null;
                    SelectedTreeNode = FindLeaf(keepHeaderId);
                }
            }
            finally
            {
                _suppress = false;
            }
        }

        private void CancelPendingTreeRebuild()
        {
            CancellationTokenSource cancellation = _treeFilterCancellation;
            _treeFilterCancellation = null;
            try { cancellation?.Cancel(); }
            catch (ObjectDisposedException) { }
            unchecked { _treeFilterGeneration++; }
        }

        private sealed class TreeBuildInput
        {
            public List<string> HeaderNames { get; set; }
            public List<int> LocationIndices { get; set; }
            public List<string> LocationNames { get; set; }
            public string Query { get; set; }
            public bool Fuzzy { get; set; }
        }

        private sealed class TreeBuildResult
        {
            public List<HeaderTreeFolder> Folders { get; }
            public IReadOnlyList<HeaderSearchEntry> SearchIndex { get; }

            public TreeBuildResult(List<HeaderTreeFolder> folders, IReadOnlyList<HeaderSearchEntry> searchIndex)
            {
                Folders = folders;
                SearchIndex = searchIndex;
            }
        }

        private sealed class TreeFilterInput
        {
            public IReadOnlyList<HeaderSearchEntry> Entries { get; }
            public string Query { get; }
            public bool Fuzzy { get; }
            public bool Filtering => Query.Length > 0;

            public TreeFilterInput(IReadOnlyList<HeaderSearchEntry> entries, string query, bool fuzzy)
            {
                Entries = entries;
                Query = query;
                Fuzzy = fuzzy;
            }
        }

        private sealed class HeaderSearchEntry
        {
            public ushort HeaderId { get; }
            public string Label { get; }
            public string LocationName { get; }
            public string FolderName { get; }
            public string IdText { get; }

            public HeaderSearchEntry(ushort headerId, string label, string locationName, string folderName)
            {
                HeaderId = headerId;
                Label = label ?? "";
                LocationName = locationName ?? "";
                FolderName = folderName ?? "";
                IdText = headerId.ToString();
            }
        }

        private static string LocationNameFor(TreeBuildInput input, ushort id)
        {
            int index = id < input.LocationIndices.Count ? input.LocationIndices[id] : -1;
            return index >= 0 && index < input.LocationNames.Count
                ? (input.LocationNames[index] ?? "").Trim()
                : "";
        }

        private static string FolderNameFor(string locationName)
        {
            if (locationName.StartsWith("Route ", StringComparison.OrdinalIgnoreCase)) return "Routes";
            return string.IsNullOrEmpty(locationName) ? "Unknown" : locationName;
        }

        private static bool HeaderMatchesFilter(string q, HeaderSearchEntry entry, bool fuzzy)
        {
            if (SearchMatch.Contains(entry.Label, q)
                || SearchMatch.Contains(entry.FolderName, q)
                || SearchMatch.Contains(entry.LocationName, q)
                || SearchMatch.Contains(entry.IdText, q))
                return true;
            return fuzzy && (SearchMatch.NearMiss(entry.LocationName, q) || SearchMatch.NearMiss(entry.Label, q));
        }

        public void ExpandAllFolders() { foreach (HeaderTreeNode f in TreeFolders) f.IsExpanded = true; }
        public void CollapseAllFolders() { foreach (HeaderTreeNode f in TreeFolders) f.IsExpanded = false; }

        /// <summary>Brings a header into view and selects it (initial load, Go-to, add/remove).</summary>
        public void SelectHeader(ushort headerId)
        {
            ExpandFolderContaining(headerId);
            _suppress = true;
            SelectedTreeNode = FindLeaf(headerId);
            _suppress = false;
            SelectedHeaderId = headerId;
            LoadHeader(headerId);
        }

        private void ExpandFolderContaining(ushort headerId)
        {
            foreach (HeaderTreeFolder folder in _allTreeFolders)
                if (folder.Children.OfType<HeaderTreeLeaf>().Any(l => l.HeaderId == headerId))
                {
                    folder.IsExpanded = true;
                    return;
                }
        }

        private HeaderTreeLeaf FindLeaf(ushort headerId)
        {
            foreach (HeaderTreeFolder folder in _allTreeFolders)
            {
                HeaderTreeLeaf leaf = folder.Children.OfType<HeaderTreeLeaf>().FirstOrDefault(l => l.HeaderId == headerId);
                if (leaf != null) return leaf;
            }
            return null;
        }

        /// <summary>Repoints a header to a new location folder and regroups (no-op if unchanged).</summary>
        private void UpdateHeaderLocationInTree(ushort id, int locIndex)
        {
            if (id >= _locationIndexByHeader.Count || _locationIndexByHeader[id] == locIndex) return;
            _locationIndexByHeader[id] = locIndex;
            RebuildTree();
        }

        private int CurrentHeaderLocationIndex()
        {
            switch (gameFamily)
            {
                case GameFamilies.DP: return ((HeaderDP)_header).locationName;
                case GameFamilies.Plat: return ((HeaderPt)_header).locationName;
                default: return ((HeaderHGSS)_header).locationName;
            }
        }

        /// <summary>
        /// Re-reads the location-name text archive and relabels folders, for when the archive was
        /// edited in another editor window. No-op (no flicker) when nothing changed.
        /// </summary>
        public void ReloadLocationNames()
        {
            if (_headerListNames.Count == 0) return;   // not set up yet
            List<string> fresh = ReadLocationNames();
            if (fresh.SequenceEqual(LocationNames)) return;   // archive untouched: leave the combo alone

            // Capture before touching the collection: clearing the ItemsSource makes the ComboBox
            // write SelectedIndex=-1 back through the binding, zapping _locationNameIndex.
            int keepLoc = _locationNameIndex;
            _suppress = true;
            LocationNames.Clear();
            foreach (string m in fresh) LocationNames.Add(m);
            _locationNameIndex = -1;
            LocationNameIndex = keepLoc < LocationNames.Count ? keepLoc : -1;   // suppressed: restores the combo without re-applying
            _suppress = false;
            RebuildTree();
        }

        // ── Load a header into the fields ───────────────────────────────────────────
        /// <summary>Plays the day or night music, or stops it when it is already playing.</summary>
        public void PlayMusic(bool night) => MusicPreview.Toggle((int)(night ? MusicNightValue : MusicDayValue), (this, night));

        private void LoadHeader(ushort headerId)
        {
            MusicPreview.Stop();   // the music belonged to the header being left
            if (headerId >= _headerListNames.Count) return;

            _header = MapHeader.GetMapHeader(headerId);
            if (_header == null) return;

            PopulateFromHeader();
            SetClean();
            _history.Reset(UndoState());   // loaded state is the clean undo baseline for this header
            _lastCaptureUtc = DateTime.MinValue;
            RaiseUndoState();
            StatusText = $"Header {_header.ID} loaded.";
            OnPropertyChanged(nameof(UnsavedChangesDescription));
            OnPropertyChanged(nameof(CurrentHeaderId));
            UpdateHeaderLocationInTree(headerId, CurrentHeaderLocationIndex());   // correct folder after reset/paste/import
        }

        /// <summary>Pushes the current <see cref="_header"/> into the editor fields (no ROM read).</summary>
        private void PopulateFromHeader()
        {
            if (_header == null) return;
            _suppress = true;
            try
            {
                InternalName = _header.ID < _internalNames.Count ? _internalNames[_header.ID] : "";
                MatrixId = _header.matrixID;
                AreaDataId = _header.areaDataID;
                ScriptFileId = _header.scriptFileID;
                LevelScriptId = _header.levelScriptID;
                EventFileId = _header.eventFileID;
                TextArchiveId = _header.textArchiveID;
                WildPokemon = _header.wildPokemon;
                BattleBackground = _header.battleBackground;
                CameraValue = _header.cameraAngleID;
                WeatherValue = _header.weatherID;
                UpdateWeatherRoom();
                MusicDayValue = _header.musicDayID;
                MusicNightValue = _header.musicNightID;

                switch (gameFamily)
                {
                    case GameFamilies.DP:
                        LocationNameIndex = ((HeaderDP)_header).locationName;
                        AreaSettingsIndex = FindAreaSettingsBySpecifier(_header.locationSpecifier);
                        break;
                    case GameFamilies.Plat:
                        LocationNameIndex = ((HeaderPt)_header).locationName;
                        AreaIconIndex = ((HeaderPt)_header).areaIcon;
                        AreaSettingsIndex = FindAreaSettingsBySpecifier(_header.locationSpecifier);
                        break;
                    default:
                        HeaderHGSS h = (HeaderHGSS)_header;
                        LocationNameIndex = h.locationName;
                        AreaIconIndex = h.areaIcon;
                        AreaSettingsIndex = h.locationType;
                        WorldmapX = h.worldmapX;
                        WorldmapY = h.worldmapY;
                        MomCallIntroParam = h.momCallIntroParam;
                        FollowModeIndex = h.followMode;
                        KantoFlag = h.kantoFlag;
                        break;
                }

                // IsHgss/IsDp/ShowHgssOnly are computed straight off the static gameFamily, so Avalonia's
                // binding never re-evaluates them on its own once the view is already visible; without
                // this, HGSS-only fields (Out Calls/In Calls/Radio, Mom's Call, world map, follow mode)
                // silently never show up even on a real HGSS ROM.
                OnPropertyChanged(nameof(IsHgss));
                OnPropertyChanged(nameof(IsDp));
                OnPropertyChanged(nameof(ShowHgssOnly));

                LoadFlags();
                SyncCameraCombo();
                SyncWeatherCombo();
                // Refilled combos drop their selection, and an unchanged value skips the setter's sync.
                SyncMusicCombo(MusicDay, (int)_musicDayValue, i => _musicDayComboIndex = i, nameof(MusicDayComboIndex));
                SyncMusicCombo(MusicNight, (int)_musicNightValue, i => _musicNightComboIndex = i, nameof(MusicNightComboIndex));
                UpdateAreaIconImage();
            }
            finally { _suppress = false; }
            OnPropertyChanged(nameof(CanOpenEncounters));   // refresh the Open-encounters guard on every (re)load
            OnPropertyChanged(nameof(SelectedHeaderTitle));
            OnPropertyChanged(nameof(SelectedHeaderSubtitle));
        }

        private int FindAreaSettingsBySpecifier(int specifier)
        {
            for (int i = 0; i < AreaSettingsItems.Count; i++)
            {
                string s = AreaSettingsItems[i];
                if (s.Length >= 4 && s[0] == '[' && int.TryParse(s.Substring(1, 3), out int n) && n == specifier)
                    return i;
            }
            return -1;
        }

        // ── Apply helpers (edit currentHeader, mark dirty) ───────────────────────────
        private void Apply(Action<MapHeader> set)
        {
            if (_header == null) return;
            set(_header);
            SetDirty();
        }
        private void ApplyHgss(Action<HeaderHGSS> set)
        {
            if (_header is HeaderHGSS h) { set(h); SetDirty(); }
        }

        private void ApplyLocationName(int index)
        {
            switch (gameFamily)
            {
                case GameFamilies.DP: Apply(h => ((HeaderDP)h).locationName = (ushort)index); break;
                case GameFamilies.Plat: Apply(h => ((HeaderPt)h).locationName = (byte)index); break;
                default: Apply(h => ((HeaderHGSS)h).locationName = (byte)index); break;
            }
            if (_header != null) UpdateHeaderLocationInTree(_header.ID, index);   // regroup live
        }

        private void ApplyAreaSettings(int index)
        {
            if (_header == null) return;
            if (gameFamily == GameFamilies.HGSS)
            {
                ((HeaderHGSS)_header).locationType = (byte)index;
                SetDirty();
            }
            else
            {
                string s = index >= 0 && index < AreaSettingsItems.Count ? AreaSettingsItems[index] : null;
                if (s != null && s.Length >= 4 && byte.TryParse(s.Substring(1, 3), out byte spec))
                {
                    _header.locationSpecifier = spec;
                    SetDirty();
                }
            }
        }

        private void ApplyAreaIcon(int index)
        {
            switch (gameFamily)
            {
                case GameFamilies.DP: break;
                case GameFamilies.Plat: Apply(h => ((HeaderPt)h).areaIcon = (byte)index); break;
                default: Apply(h => ((HeaderHGSS)h).areaIcon = (byte)index); break;
            }
            UpdateAreaIconImage();
        }

        private void ApplyFlags()
        {
            if (_header == null) return;
            byte v = 0;
            if (_f0) v |= 1 << 0;
            if (_f1) v |= 1 << 1;
            if (_f2) v |= 1 << 2;
            if (_f3) v |= 1 << 3;
            if (IsHgss)
            {
                if (_f4) v |= 1 << 4;
                if (_f5) v |= 1 << 5;
                if (_f6) v |= 1 << 6;
            }
            _header.flags = v;
            SetDirty();
        }

        private void LoadFlags()
        {
            byte v = _header.flags;
            Flag0 = (v & (1 << 0)) != 0;
            Flag1 = (v & (1 << 1)) != 0;
            Flag2 = (v & (1 << 2)) != 0;
            Flag3 = (v & (1 << 3)) != 0;
            Flag4 = (v & (1 << 4)) != 0;
            Flag5 = (v & (1 << 5)) != 0;
            Flag6 = (v & (1 << 6)) != 0;
        }

        // ── Combo / image sync ───────────────────────────────────────────────────────
        // A renamed camera or weather shows at once; the closed combos are poked on a later frame, since an
        // in-place relabel of the selected item otherwise leaves a stale display.
        private void OnLabelsChanged(object sender, EventArgs e)
        {
            Camera.LoadLabels(LabelStore.CameraKey);
            Weather.LoadLabels(LabelStore.WeatherKey);
            _cameraComboIndex = -1; OnPropertyChanged(nameof(CameraComboIndex));
            _weatherComboIndex = -1; OnPropertyChanged(nameof(WeatherComboIndex));
            Dispatcher.UIThread.Post(() => { SyncCameraCombo(); SyncWeatherCombo(); }, DispatcherPriority.Background);
        }

        private void SyncCameraCombo() { _cameraComboIndex = Camera.IndexOf((int)_cameraValue); OnPropertyChanged(nameof(CameraComboIndex)); }
        private void SyncWeatherCombo() { _weatherComboIndex = Weather.IndexOf((int)_weatherValue); OnPropertyChanged(nameof(WeatherComboIndex)); }
        private void SyncMusicCombo(MappedCombo combo, int value, Action<int> setBacking, string propName)
        { setBacking(combo.IndexOf(value)); OnPropertyChanged(propName); }

        private void UpdateAreaIconImage()
        {
            string name = null;
            switch (gameFamily)
            {
                case GameFamilies.DP: name = "dpareaicon"; break;
                case GameFamilies.Plat: if (_areaIconIndex >= 0) name = "areaicon0" + _areaIconIndex; break;
                default:
                    if (_areaIconIndex >= 0 && PokeDatabase.System.AreaPics.hgssAreaPicDict.TryGetValue(_areaIconIndex, out string n)) name = n;
                    break;
            }
            AreaIconImage = name != null ? ResImage(name) : null;
        }

        private static Bitmap ResImage(string name) => ResourceImages.GetBitmap(name);

        // ── Internal name feedback ───────────────────────────────────────────────────
        private void UpdateInternalNameFeedback()
        {
            int len = _internalName?.Length ?? 0;
            InternalNameColor = len > 13 ? StatusBrushes.Bad : len > 7 ? StatusBrushes.Warn : StatusBrushes.Good;
            InternalNameLen = $"[ {len} ]";
        }

        // ── Save ─────────────────────────────────────────────────────────────────────
        public void Save()
        {
            if (_header == null) return;
            MapHeader.Save(_header);

            UpdateCurrentInternalName();
            SetClean();
            SaveNotice.Saved(UnsavedChangesDescription);
            _history.MarkSaved();
            RaiseUndoState();
            StatusText = $"Header {_header.ID} saved.";
            AppEvents.RaiseHeaderSaved(this, _header.ID);
        }

        // ── Copy / paste / reset / import / export / go-to / quick-open ──────────────────
        // Kept across ROM loads, so it carries the family whose layout its bytes are in.
        private static (byte[] Bytes, GameFamilies Family) _clipboard;

        public void Copy()
        {
            if (_header == null) return;
            _clipboard = (_header.ToByteArray(), gameFamily);
            StatusText = $"Copied header {_header.ID}.";
        }

        public void Paste()
        {
            if (_header == null || _clipboard.Bytes == null) { StatusText = "Nothing to paste."; return; }
            if (_clipboard.Family != gameFamily) { StatusText = $"The copied header is from a {_clipboard.Family} ROM and can't be pasted here."; return; }
            MapHeader h = MapHeader.LoadFromByteArray(_clipboard.Bytes, (ushort)_header.ID, gameFamily);
            if (h == null) { StatusText = "Clipboard header is incompatible."; return; }
            _header = h;
            PopulateFromHeader();
            SetDirty();
            StatusText = "Pasted header (unsaved).";
        }

        public void Reset()
        {
            LoadHeader(SelectedHeaderId);
            StatusText = "Reverted to saved header.";
        }

        public async Task ImportAsync()
        {
            if (_header == null) return;
            FilePickerFileType filter = new global::Avalonia.Platform.Storage.FilePickerFileType("DSPRE header")
            { Patterns = new[] { "*.dsh", "*.bin", "*.*" } };
            string path = await DialogHelper.OpenFile(_owner, "Import header", new[] { filter });
            if (path == null) return;
            try
            {
                if (new FileInfo(path).Length > 48) throw new InvalidDataException();
                MapHeader h = MapHeader.LoadFromFile(path, (ushort)_header.ID, 0);
                if (h == null) throw new InvalidDataException();
                _header = h;
                PopulateFromHeader();
                SetDirty();
                StatusText = "Imported header (unsaved).";
            }
            catch (Exception ex) { await DialogHelper.ShowError($"Import failed: malformed or not a header file.\n{ex.Message}", "Import Error"); }
        }

        public async Task ExportAsync()
        {
            if (_header == null) return;
            FilePickerFileType filter = new global::Avalonia.Platform.Storage.FilePickerFileType("DSPRE header") { Patterns = new[] { "*.dsh" } };
            string path = await DialogHelper.SaveFile(_owner, "Export header", new[] { filter }, $"header_{_header.ID:D4}.dsh");
            if (path == null) return;
            try { File.WriteAllBytes(path, _header.ToByteArray()); StatusText = "Exported header."; }
            catch (Exception ex) { await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error"); }
        }

        /// <summary>Shows a header asked for from another editor.</summary>
        public void GoToHeader(int id)
        {
            if (id < 0 || id >= _headerListNames.Count) return;
            if (MustAskBeforeLeaving((ushort)id))
            {
                if (!_switchPending) _ = SwitchHeaderAsync((ushort)id);
                return;
            }
            if (!string.IsNullOrWhiteSpace(TreeFilterText)) TreeFilterText = "";
            SelectHeader((ushort)id);
        }

        // Jump to the related editor at this header's referenced file.
        /// <summary>
        /// Shows a linked file in the main view's tab of that name instead of a window, when this header is shown there.
        /// The tabs always show this header's own files, so the tab is already on the right one. Returns false to fall
        /// back to the editor window.
        /// </summary>
        public Func<string, bool> OpenInTab
        {
            get => _openInTab;
            set { _openInTab = value; OnPropertyChanged(nameof(OpenTips)); }
        }
        private Func<string, bool> _openInTab;

        // What each linked file is called, one name used for the field, the tab and the Open button.
        private static readonly (string Key, string Name, string Editor)[] LinkedFiles =
        {
            ("Matrix", "matrix", "Matrix editor"), ("AreaData", "area data", "Area Data editor"),
            ("Script", "script file", "Script editor"), ("LevelScript", "level script file", "Level Script editor"),
            ("Event", "event file", "Event editor"), ("Text", "text archive", "Text editor"),
            ("Encounters", "encounters", "Encounters editor"),
        };

        /// <summary>The Open buttons' tooltips: where the linked file opens, here or in its own window.</summary>
        public Dictionary<string, string> OpenTips
        {
            get
            {
                Dictionary<string, string> tips = new();
                foreach ((string key, string name, string editor) in LinkedFiles)
                    tips[key] = _openInTab != null ? $"Show the linked {name} in its tab" : $"Open the linked {name} in the {editor}";
                return tips;
            }
        }

        public void OpenMatrix() { if (_header != null && _openInTab?.Invoke("Matrix") != true) AvaloniaEditorLauncher.OpenMatrixEditor(_header.matrixID, _header.ID); }
        public void OpenAreaData() { if (_header != null && _openInTab?.Invoke("Area Data") != true) AvaloniaEditorLauncher.OpenAreaDataEditor(_header.areaDataID); }
        public void OpenEvents() { if (_header != null && _openInTab?.Invoke("Events") != true) AvaloniaEditorLauncher.OpenEventEditor(_header.eventFileID); }
        public void OpenScripts() { if (_header != null && _openInTab?.Invoke("Scripts") != true) AvaloniaEditorLauncher.OpenScriptEditor(_header.scriptFileID); }
        public void OpenLevelScripts() { if (_header != null && _openInTab?.Invoke("Level Scripts") != true) AvaloniaEditorLauncher.OpenLevelScriptEditor(_header.levelScriptID); }
        public void OpenTexts() { if (_header != null && _openInTab?.Invoke("Text") != true) AvaloniaEditorLauncher.OpenTextEditor(_header.textArchiveID); }
        public void OpenEncounters() { if (CanOpenEncounters && _openInTab?.Invoke("Encounters") != true) AvaloniaEditorLauncher.OpenWildEditor(_header.wildPokemon); }

        /// <summary>Opens the battle scenery this place fights on. Every other linked field on this row
        /// has a button to the thing it names; this one was a bare number.</summary>
        public void OpenBattleScenery() => AvaloniaEditorLauncher.OpenBattleSceneBrowser();

        private void UpdateCurrentInternalName()
        {
            ushort id = _header.ID;
            MapHeader.WriteInternalName(id, _internalName);

            if (id < _internalNames.Count) _internalNames[id] = _internalName;
            if (id < _headerListNames.Count) _headerListNames[id] = id.ToString("D3") + MapHeader.nameSeparator + _internalName;
            RebuildTree();   // refresh the leaf's label (and any active search)
        }

        // ── Add / remove header (dynamic-headers patch only; no associated files) ─────

        // Header 0 is the blank one (no encounters, 255s), so it stays the default source for a new header.
        public static readonly string[] CopyFromOptions = { "Duplicate header 0", "Duplicate selected header" };
        private const string CopyFromKey = "header.addCopiesSelected";

        public int CopyFromIndex
        {
            get => ProjectPrefs.Get(CopyFromKey, "false") == "true" ? 1 : 0;
            set
            {
                ProjectPrefs.Set(CopyFromKey, value == 1 ? "true" : "false");
                OnPropertyChanged(nameof(CopyFromIndex));
            }
        }

        /// <summary>The header Remove takes away. Headers are numbered by position, so only the last one can go.</summary>
        public string RemoveTip => _headerListNames.Count > 1
            ? $"Removes header {_headerListNames.Count - 1:D3}, the last one"
            : "There has to be at least one header.";

        // Removed headers, newest last, so Ctrl+Z can put them back while no field edit is left to undo.
        private readonly Stack<(byte[] Data, string Name)> _removedHeaders = new();

        public async Task AddHeaderAsync()
        {
            if (!_dynamicHeaders) return;
            const string newmap = "NEWMAP";
            int source = CopyFromIndex == 1 ? SelectedHeaderId : 0;
            // The selected header's unsaved edits are what the user sees, so a copy of it carries them.
            int newId = source == SelectedHeaderId && _dirty && _header != null
                ? MapHeader.RestoreDynamicHeader(_header.ToByteArray(), newmap)
                : MapHeader.AddDynamicHeader(newmap, source);
            _removedHeaders.Clear();   // a restore would land on a different number now
            RaiseUndoState();
            OnPropertyChanged(nameof(RemoveTip));

            _headerListNames.Add(newId.ToString("D3") + MapHeader.nameSeparator + newmap);
            _internalNames.Add(newmap);
            LoadLocationIndices();
            RebuildTree();
            SelectHeader((ushort)newId);

            await DialogHelper.ShowInfo(
                "New header added. (Creating associated Text/Script/Level-Script/Event files is not yet available in the Avalonia editor; add them from the respective editors.)",
                "Header added");
        }

        public async Task RemoveHeaderAsync()
        {
            if (!_dynamicHeaders) return;
            int lastIndex = _headerListNames.Count - 1;
            if (lastIndex <= 0)
            {
                await DialogHelper.ShowError("You must have at least one header!", "Can't delete last header");
                return;
            }

            string name = lastIndex < _internalNames.Count ? _internalNames[lastIndex] : "";
            if (!await DialogHelper.AskYesNo(
                    $"Remove header {lastIndex:D3} ({name})? Only the last header can be removed. It is deleted now, not on Save; Ctrl+Z puts it back.",
                    "Remove last header"))
                return;

            byte[] data = File.ReadAllBytes(Filesystem.GetDynamicHeaderPath(lastIndex));
            MapHeader.RemoveLastDynamicHeader();
            _removedHeaders.Push((data, name));

            _internalNames.RemoveAt(lastIndex);
            _headerListNames.RemoveAt(lastIndex);
            LoadLocationIndices();
            RebuildTree();
            SelectHeader(SelectedHeaderId >= lastIndex ? (ushort)(lastIndex - 1) : SelectedHeaderId);
            RaiseUndoState();
            OnPropertyChanged(nameof(RemoveTip));
        }

        private void RestoreRemovedHeader()
        {
            (byte[] data, string name) = _removedHeaders.Pop();
            int id = MapHeader.RestoreDynamicHeader(data, name);
            _headerListNames.Add(id.ToString("D3") + MapHeader.nameSeparator + name);
            _internalNames.Add(name);
            LoadLocationIndices();
            RebuildTree();
            SelectHeader((ushort)id);
            RaiseUndoState();
            OnPropertyChanged(nameof(RemoveTip));
        }
    }
}

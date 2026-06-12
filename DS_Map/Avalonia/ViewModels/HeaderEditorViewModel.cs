using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.Resources;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
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
            foreach (var kv in dict) { Keys.Add(Convert.ToInt32(kv.Key)); Names.Add(kv.Value); }
        }
        public int IndexOf(int value) => Keys.IndexOf(value);
        public int KeyAt(int index) => index >= 0 && index < Keys.Count ? Keys[index] : -1;
    }

    /// <summary>
    /// Avalonia port of the WinForms <c>HeaderEditor</c>. Edits map headers — all
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
    public class HeaderEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
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

        public ObservableCollection<string> HeaderItems { get; } = new ObservableCollection<string>();
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

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        // ── Common scalar fields ─────────────────────────────────────────────────────
        private int _selectedHeaderIndex = -1;
        public int SelectedHeaderIndex
        {
            get => _selectedHeaderIndex;
            set { if (Set(ref _selectedHeaderIndex, value) && !_suppress && value >= 0) LoadHeader(value); }
        }

        private string _internalName = "";
        public string InternalName
        {
            get => _internalName;
            set { if (Set(ref _internalName, value)) { UpdateInternalNameFeedback(); SetDirty(); } }
        }

        private IBrush _internalNameColor = Brushes.Green;
        public IBrush InternalNameColor { get => _internalNameColor; set => Set(ref _internalNameColor, value); }
        private string _internalNameLen = "[ 0 ]";
        public string InternalNameLen { get => _internalNameLen; set => Set(ref _internalNameLen, value); }

        private decimal _matrixId; public decimal MatrixId { get => _matrixId; set { if (Set(ref _matrixId, value)) Apply(h => h.matrixID = (ushort)value); } }
        private decimal _areaDataId; public decimal AreaDataId { get => _areaDataId; set { if (Set(ref _areaDataId, value)) Apply(h => h.areaDataID = (byte)value); } }
        private decimal _scriptFileId; public decimal ScriptFileId { get => _scriptFileId; set { if (Set(ref _scriptFileId, value)) Apply(h => h.scriptFileID = (ushort)value); } }
        private decimal _levelScriptId; public decimal LevelScriptId { get => _levelScriptId; set { if (Set(ref _levelScriptId, value)) Apply(h => h.levelScriptID = (ushort)value); } }
        private decimal _eventFileId; public decimal EventFileId { get => _eventFileId; set { if (Set(ref _eventFileId, value)) Apply(h => h.eventFileID = (ushort)value); } }
        private decimal _textArchiveId; public decimal TextArchiveId { get => _textArchiveId; set { if (Set(ref _textArchiveId, value)) Apply(h => h.textArchiveID = (ushort)value); } }
        private decimal _wildPokemon; public decimal WildPokemon { get => _wildPokemon; set { if (Set(ref _wildPokemon, value)) Apply(h => h.wildPokemon = (ushort)value); } }
        private decimal _battleBackground; public decimal BattleBackground { get => _battleBackground; set { if (Set(ref _battleBackground, value)) Apply(h => h.battleBackground = (byte)value); } }

        // ── Camera (combo + numeric + image) ─────────────────────────────────────────
        private decimal _cameraValue;
        public decimal CameraValue
        {
            get => _cameraValue;
            set { if (Set(ref _cameraValue, value)) { Apply(h => h.cameraAngleID = (byte)value); SyncCameraCombo(); UpdateCameraImage(); } }
        }
        private int _cameraComboIndex = -1;
        public int CameraComboIndex
        {
            get => _cameraComboIndex;
            set { if (Set(ref _cameraComboIndex, value) && !_suppress && value >= 0) CameraValue = Camera.KeyAt(value); }
        }
        private Bitmap _cameraImage; public Bitmap CameraImage { get => _cameraImage; set => Set(ref _cameraImage, value); }

        // ── Weather (combo + numeric + image) ────────────────────────────────────────
        private decimal _weatherValue;
        public decimal WeatherValue
        {
            get => _weatherValue;
            set { if (Set(ref _weatherValue, value)) { Apply(h => h.weatherID = (byte)value); SyncWeatherCombo(); UpdateWeatherImage(); } }
        }
        private int _weatherComboIndex = -1;
        public int WeatherComboIndex
        {
            get => _weatherComboIndex;
            set { if (Set(ref _weatherComboIndex, value) && !_suppress && value >= 0) WeatherValue = Weather.KeyAt(value); }
        }
        private Bitmap _weatherImage; public Bitmap WeatherImage { get => _weatherImage; set => Set(ref _weatherImage, value); }

        // ── Music day / night (combo + numeric) ──────────────────────────────────────
        private decimal _musicDayValue;
        public decimal MusicDayValue
        {
            get => _musicDayValue;
            set { if (Set(ref _musicDayValue, value)) { Apply(h => h.musicDayID = (ushort)value); SyncMusicCombo(MusicDay, (int)value, i => _musicDayComboIndex = i, nameof(MusicDayComboIndex)); } }
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
            set { if (Set(ref _musicNightValue, value)) { Apply(h => h.musicNightID = (ushort)value); SyncMusicCombo(MusicNight, (int)value, i => _musicNightComboIndex = i, nameof(MusicNightComboIndex)); } }
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
            set { if (Set(ref _locationNameIndex, value) && !_suppress && value >= 0) ApplyLocationName(value); }
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
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); if (_selectedHeaderIndex >= 0) LoadHeader(_selectedHeaderIndex); }
        private void SetDirty() { if (_suppress || _dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        // ── Constructors ────────────────────────────────────────────────────────────
        public HeaderEditorViewModel()
        {
            if (!Design.IsDesignMode) return;
            HeaderItems.Add("000 -   DESIGN");
        }

        public HeaderEditorViewModel(bool _) { }

        // ── Setup ─────────────────────────────────────────────────────────────────────
        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            StatusText = "Loading headers…";
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.synthOverlay, DirNames.textArchives, DirNames.dynamicHeaders });

                _dynamicHeaders = PatchToolboxDialog.flag_DynamicHeadersPatchApplied || PatchToolboxDialog.CheckFilesDynamicHeadersPatchApplied();
                CanAddRemove = _dynamicHeaders;
                OnPropertyChanged(nameof(CanAddRemove));

                _headerListNames = Helpers.getHeaderListBoxNames();
                _internalNames = Helpers.getInternalNames();

                BuildFamilyCombos();
                LoadLocationNames();

                HeaderItems.Clear();
                foreach (var n in _headerListNames) HeaderItems.Add(n);

                StatusText = $"Loaded {HeaderItems.Count} headers ({gameFamily}).";
                if (HeaderItems.Count > 0) SelectedHeaderIndex = 0;
            }
            catch (FileNotFoundException)
            {
                await DialogHelper.ShowError(internalNamesPath + " doesn't exist.", "Couldn't read internal names");
            }
            catch (Exception ex)
            {
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
                    Camera.Load(PokeDatabase.CameraAngles.DPPtCameraDict);
                    MusicDay.Load(PokeDatabase.MusicDB.DPMusicDict);
                    MusicNight.Load(PokeDatabase.MusicDB.DPMusicDict);
                    Weather.Load(PokeDatabase.Weather.DPWeatherDict);
                    foreach (var s in PokeDatabase.ShowName.DPShowNameValues) AreaSettingsItems.Add(s);
                    ShowAreaIcon = false;
                    WildPokeMax = 65535;
                    break;
                case GameFamilies.Plat:
                    Camera.Load(PokeDatabase.CameraAngles.DPPtCameraDict);
                    MusicDay.Load(PokeDatabase.MusicDB.PtMusicDict);
                    MusicNight.Load(PokeDatabase.MusicDB.PtMusicDict);
                    Weather.Load(PokeDatabase.Weather.PtWeatherDict);
                    foreach (var s in PokeDatabase.ShowName.PtShowNameValues) AreaSettingsItems.Add(s);
                    foreach (var s in PokeDatabase.Area.PtAreaIconValues) AreaIconItems.Add(s);
                    ShowAreaIcon = true;
                    WildPokeMax = 65535;
                    break;
                default:
                    Camera.Load(PokeDatabase.CameraAngles.HGSSCameraDict);
                    MusicDay.Load(PokeDatabase.MusicDB.HGSSMusicDict);
                    MusicNight.Load(PokeDatabase.MusicDB.HGSSMusicDict);
                    Weather.Load(PokeDatabase.Weather.HGSSWeatherDict);
                    foreach (var s in PokeDatabase.Area.HGSSAreaProperties) AreaSettingsItems.Add(s);
                    foreach (var s in PokeDatabase.Area.HGSSAreaIconsDict.Values) AreaIconItems.Add(s);
                    ShowAreaIcon = true;
                    WildPokeMax = 255;
                    break;
            }
            OnPropertyChanged(nameof(ShowAreaIcon));
            OnPropertyChanged(nameof(WildPokeMax));
        }

        private void LoadLocationNames()
        {
            LocationNames.Clear();
            try
            {
                var ta = new TextArchive(locationNamesTextNumber);
                foreach (var m in ta.messages) LocationNames.Add(m);
            }
            catch { /* leave empty */ }
        }

        // ── Load a header into the fields ───────────────────────────────────────────
        private void LoadHeader(int index)
        {
            if (index < 0 || index >= HeaderItems.Count) return;
            if (!ushort.TryParse(HeaderItems[index].Substring(0, 3), out ushort headerNumber)) return;

            _header = _dynamicHeaders
                ? MapHeader.LoadFromFile(gameDirs[DirNames.dynamicHeaders].unpackedDir + "\\" + headerNumber.ToString("D4"), headerNumber, 0)
                : MapHeader.LoadFromARM9(headerNumber);
            if (_header == null) return;

            PopulateFromHeader();
            SetClean();
            StatusText = $"Header {_header.ID} loaded.";
            OnPropertyChanged(nameof(UnsavedChangesDescription));
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
                        var h = (HeaderHGSS)_header;
                        LocationNameIndex = h.locationName;
                        AreaIconIndex = h.areaIcon;
                        AreaSettingsIndex = h.locationType;
                        WorldmapX = h.worldmapX;
                        WorldmapY = h.worldmapY;
                        FollowModeIndex = h.followMode;
                        KantoFlag = h.kantoFlag;
                        break;
                }

                LoadFlags();
                SyncCameraCombo(); UpdateCameraImage();
                SyncWeatherCombo(); UpdateWeatherImage();
                UpdateAreaIconImage();
            }
            finally { _suppress = false; }
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
        private void SyncCameraCombo() { _cameraComboIndex = Camera.IndexOf((int)_cameraValue); OnPropertyChanged(nameof(CameraComboIndex)); }
        private void SyncWeatherCombo() { _weatherComboIndex = Weather.IndexOf((int)_weatherValue); OnPropertyChanged(nameof(WeatherComboIndex)); }
        private void SyncMusicCombo(MappedCombo combo, int value, Action<int> setBacking, string propName)
        { setBacking(combo.IndexOf(value)); OnPropertyChanged(propName); }

        private void UpdateCameraImage()
        {
            string prefix = gameFamily == GameFamilies.DP ? "dpcamera" : gameFamily == GameFamilies.Plat ? "ptcamera" : "hgsscamera";
            CameraImage = ResImage(prefix + ((int)_cameraValue));
        }
        private void UpdateWeatherImage()
        {
            Dictionary<byte[], string> dict = gameFamily == GameFamilies.DP ? PokeDatabase.System.WeatherPics.dpWeatherImageDict
                : gameFamily == GameFamilies.Plat ? PokeDatabase.System.WeatherPics.ptWeatherImageDict
                : PokeDatabase.System.WeatherPics.hgssweatherImageDict;
            string name = null;
            foreach (var e in dict) if (Array.IndexOf(e.Key, (byte)_weatherValue) >= 0) { name = e.Value; break; }
            WeatherImage = name != null ? ResImage(name) : null;
        }
        private void UpdateAreaIconImage()
        {
            string name = null;
            switch (gameFamily)
            {
                case GameFamilies.DP: name = "dpareaicon"; break;
                case GameFamilies.Plat: if (_areaIconIndex >= 0) name = "areaicon0" + _areaIconIndex; break;
                default:
                    if (_areaIconIndex >= 0 && PokeDatabase.System.AreaPics.hgssAreaPicDict.TryGetValue(_areaIconIndex, out var n)) name = n;
                    break;
            }
            AreaIconImage = name != null ? ResImage(name) : null;
        }

        private static Bitmap ResImage(string name)
        {
            try
            {
                var img = global::DSPRE.Properties.Resources.ResourceManager.GetObject(name) as System.Drawing.Image;
                return ImageConverter.ToAvaloniaBitmap(img);
            }
            catch { return null; }
        }

        // ── Internal name feedback ───────────────────────────────────────────────────
        private void UpdateInternalNameFeedback()
        {
            int len = _internalName?.Length ?? 0;
            InternalNameColor = len > 13 ? Brushes.Red : len > 7 ? Brushes.DarkGoldenrod : Brushes.Green;
            InternalNameLen = $"[ {len} ]";
        }

        // ── Save ─────────────────────────────────────────────────────────────────────
        public void Save()
        {
            if (_header == null) return;
            if (_dynamicHeaders)
                DSUtils.WriteToFile(gameDirs[DirNames.dynamicHeaders].unpackedDir + "\\" + _header.ID.ToString("D4"),
                    _header.ToByteArray(), 0, 0, fmode: FileMode.Create);
            else
                ARM9.WriteBytes(_header.ToByteArray(), (uint)(headerTableOffset + MapHeader.length * _header.ID));

            UpdateCurrentInternalName();
            SetClean();
            StatusText = $"Header {_header.ID} saved.";
        }

        // ── Copy / paste / reset / import / export / go-to / quick-open ──────────────────
        private static byte[] _clipboard;

        public void Copy()
        {
            if (_header == null) return;
            _clipboard = _header.ToByteArray();
            StatusText = $"Copied header {_header.ID}.";
        }

        public void Paste()
        {
            if (_header == null || _clipboard == null) { StatusText = "Nothing to paste."; return; }
            var h = MapHeader.LoadFromByteArray(_clipboard, (ushort)_header.ID, gameFamily);
            if (h == null) { StatusText = "Clipboard header is incompatible."; return; }
            _header = h;
            PopulateFromHeader();
            SetDirty();
            StatusText = "Pasted header (unsaved).";
        }

        public void Reset()
        {
            if (_selectedHeaderIndex >= 0) { LoadHeader(_selectedHeaderIndex); StatusText = "Reverted to saved header."; }
        }

        public async Task ImportAsync()
        {
            if (_header == null) return;
            var filter = new global::Avalonia.Platform.Storage.FilePickerFileType("DSPRE header")
            { Patterns = new[] { "*.dsh", "*.bin", "*.*" } };
            string path = await DialogHelper.OpenFile(_owner, "Import header", new[] { filter });
            if (path == null) return;
            try
            {
                if (new FileInfo(path).Length > 48) throw new InvalidDataException();
                var h = MapHeader.LoadFromFile(path, (ushort)_header.ID, 0);
                if (h == null) throw new InvalidDataException();
                _header = h;
                PopulateFromHeader();
                SetDirty();
                StatusText = "Imported header (unsaved).";
            }
            catch (Exception ex) { await DialogHelper.ShowError($"Import failed — malformed or not a header file.\n{ex.Message}", "Import Error"); }
        }

        public async Task ExportAsync()
        {
            if (_header == null) return;
            var filter = new global::Avalonia.Platform.Storage.FilePickerFileType("DSPRE header") { Patterns = new[] { "*.dsh" } };
            string path = await DialogHelper.SaveFile(_owner, "Export header", new[] { filter }, $"header_{_header.ID:D4}.dsh");
            if (path == null) return;
            try { File.WriteAllBytes(path, _header.ToByteArray()); StatusText = "Exported header."; }
            catch (Exception ex) { await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error"); }
        }

        private decimal _goToValue;
        public decimal GoToValue { get => _goToValue; set => Set(ref _goToValue, value); }
        public void GoTo()
        {
            int n = (int)_goToValue;
            if (n >= 0 && n < HeaderItems.Count) SelectedHeaderIndex = n;
        }

        // Jump to the related editor at this header's referenced file.
        public void OpenMatrix() { if (_header != null) AvaloniaEditorLauncher.OpenMatrixEditor(_header.matrixID); }
        public void OpenEvents() { if (_header != null) AvaloniaEditorLauncher.OpenEventEditor(_header.eventFileID); }
        public void OpenScripts() { if (_header != null) AvaloniaEditorLauncher.OpenScriptEditor(_header.scriptFileID); }
        public void OpenLevelScripts() { if (_header != null) AvaloniaEditorLauncher.OpenLevelScriptEditor(_header.levelScriptID); }
        public void OpenTexts() { if (_header != null) AvaloniaEditorLauncher.OpenTextEditor(_header.textArchiveID); }

        private void UpdateCurrentInternalName()
        {
            ushort id = _header.ID;
            using (var writer = new DSUtils.EasyWriter(internalNamesPath, id * internalNameLength))
                writer.Write(StringToInternalName(_internalName));

            if (id < _internalNames.Count) _internalNames[id] = _internalName;
            string elem = id.ToString("D3") + MapHeader.nameSeparator + _internalName;
            if (id < _headerListNames.Count) _headerListNames[id] = elem;
            _suppress = true;
            if (id < HeaderItems.Count) HeaderItems[id] = elem;
            _suppress = false;
        }

        private byte[] StringToInternalName(string text)
        {
            text ??= "";
            return Encoding.ASCII.GetBytes(text.Substring(0, Math.Min(text.Length, internalNameLength)).PadRight(internalNameLength, '\0'));
        }

        // ── Add / remove header (dynamic-headers patch only; no associated files) ─────
        public async Task AddHeaderAsync()
        {
            if (!_dynamicHeaders) return;
            string dir = gameDirs[DirNames.dynamicHeaders].unpackedDir;
            int newId = GetHeaderCount();
            File.Copy(dir + "\\0000", dir + "\\" + newId.ToString("D4"));

            const string newmap = "NEWMAP";
            DSUtils.WriteToFile(internalNamesPath, StringToInternalName(newmap), (uint)newId * internalNameLength);

            string elem = newId.ToString("D3") + MapHeader.nameSeparator + newmap;
            HeaderItems.Add(elem);
            _headerListNames.Add(elem);
            _internalNames.Add(newmap);
            SelectedHeaderIndex = HeaderItems.Count - 1;

            await DialogHelper.ShowInfo(
                "New header added. (Creating associated Text/Script/Level-Script/Event files is not yet available in the Avalonia editor — add them from the respective editors.)",
                "Header added");
        }

        public async Task RemoveHeaderAsync()
        {
            if (!_dynamicHeaders) return;
            int lastIndex = HeaderItems.Count - 1;
            if (lastIndex <= 0)
            {
                await DialogHelper.ShowError("You must have at least one header!", "Can't delete last header");
                return;
            }
            if (_selectedHeaderIndex == lastIndex) SelectedHeaderIndex = lastIndex - 1;

            File.Delete(gameDirs[DirNames.dynamicHeaders].unpackedDir + "\\" + lastIndex.ToString("D4"));
            using (var ew = new DSUtils.EasyWriter(internalNamesPath)) ew.EditSize(-internalNameLength);

            HeaderItems.RemoveAt(lastIndex);
            _internalNames.RemoveAt(lastIndex);
            _headerListNames.RemoveAt(lastIndex);
        }
    }
}

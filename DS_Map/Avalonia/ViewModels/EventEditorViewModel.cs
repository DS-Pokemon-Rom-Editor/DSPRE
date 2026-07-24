using DSPRE.Avalonia;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using LibNDSFormats.NSBMD;
using LibNDSFormats.NSBTX;
using NSMBe4.DSFileSystem;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// Avalonia port of the WinForms <c>EventEditor</c> — core scope. Edits an event
    /// file's four event lists (Spawnables, Overworlds, Warps, Triggers): select an
    /// event to edit its position (map + matrix) and its type-specific fields, add/remove
    /// events, and save / import / export. The 3D map overlay with event markers and
    /// click-to-place are deferred (the renderer foundation is in place for later).
    /// </summary>
    public class EventEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private Window _owner;
        private bool _suppress;
        private EventFile _file;

        // ── 3D map view ────────────────────────────────────────────────────────────────
        private Dictionary<int, (ushort matrixId, byte areaId, ushort scriptFileId)> _eventToHeader; // event file → its header's matrix + area + paired script file
        private GameMatrix _matrix;
        private byte _areaDataId;
        private readonly Dictionary<long, byte[,]> _collisionCache = new Dictionary<long, byte[,]>();

        /// <summary>Raised after the 3D map is (re)loaded so the view can push the new model.</summary>
        public event EventHandler MapLoaded;
        /// <summary>Raised when the event markers change so the view can push the new marker mesh.</summary>
        public event EventHandler MarkersChanged;

        public NsbmdRenderModel Model3D { get; private set; }
        public float[] MarkerMesh { get; private set; }
        public int MarkerVertexCount { get; private set; }

        public List<NsbmdGlControl.SpriteInstance> Sprites { get; private set; }
        /// <summary>Raised when overworld sprites change so the view can push them to the GL control.</summary>
        public event EventHandler SpritesChanged;

        private string _mapInfo = "";
        public string MapInfo { get => _mapInfo; set => Set(ref _mapInfo, value); }

        public ObservableCollection<string> EventNames { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Spawnables { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Overworlds { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Warps { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> Triggers { get; } = new ObservableCollection<string>();

        // ── Overworld sprite/movement/orientation dropdowns (mirrors WinForms EventEditor) ──
        public ObservableCollection<string> OwSpriteEntries { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> OwMovementNames { get; } = new ObservableCollection<string>(DSPRE.Resources.PokeDatabase.EventEditor.Overworlds.movementTypesArray);
        public ObservableCollection<string> OwOrientationNames { get; } = new ObservableCollection<string> { "Up", "Down", "Left", "Right" };

        private global::Avalonia.Media.Imaging.Bitmap _owSpritePreview;
        public global::Avalonia.Media.Imaging.Bitmap OwSpritePreview { get => _owSpritePreview; private set => Set(ref _owSpritePreview, value); }
        public bool HasOwSpritePreview => _owSpritePreview != null;

        // ── Overworld "kind" radio group (Standard / Trainer / Item) — mirrors WinForms'
        // normalRadioButton/isTrainerRadioButton/isItemRadioButton: selecting a kind sets ow.type and
        // locks ow.scriptNumber to a value computed from the Trainer/Item dropdown (Script is only
        // free-form for Standard). ──────────────────────────────────────────────────────────────────
        private enum OwKind { Normal, Trainer, Item }
        private OwKind _owKind;

        public bool OwIsNormal { get => _owKind == OwKind.Normal; set { if (value) SetOwKind(OwKind.Normal); } }
        public bool OwIsTrainer { get => _owKind == OwKind.Trainer; set { if (value) SetOwKind(OwKind.Trainer); } }
        public bool OwIsItem { get => _owKind == OwKind.Item; set { if (value) SetOwKind(OwKind.Item); } }

        // Script is normally locked when Trainer/Item drives it — but if the current script number
        // doesn't resolve to any entry in that list (out-of-range/hand-edited data), unlock it so the
        // user isn't stuck looking at a value they can't change from either the dropdown or the field.
        public bool OwScriptEnabled => _owKind == OwKind.Normal || OwTrainerIndexOutOfRange || OwItemIndexOutOfRange;
        public bool OwTrainerFieldsVisible => _owKind == OwKind.Trainer;
        public bool OwItemFieldsVisible => _owKind == OwKind.Item;

        public ObservableCollection<string> OwTrainerEntries { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> OwItemEntries { get; } = new ObservableCollection<string>();

        private int _owTrainerIndex = -1;
        public int OwTrainerIndex
        {
            get => _owTrainerIndex;
            set { if (Set(ref _owTrainerIndex, value) && !_suppress && _ow != null && _owKind == OwKind.Trainer) RecomputeTrainerScript(); }
        }

        // The Script field's raw value can point past the end of the current trainer roster (a script
        // number that doesn't correspond to any known trainer, e.g. hand-edited data or a ROM-specific
        // reserved value) — show that clearly instead of a silently-blank dropdown.
        public bool OwTrainerIndexOutOfRange => _owKind == OwKind.Trainer && (_owTrainerIndex < 0 || _owTrainerIndex >= OwTrainerEntries.Count);

        private bool _owPartnerTrainer;
        public bool OwPartnerTrainer
        {
            get => _owPartnerTrainer;
            set { if (Set(ref _owPartnerTrainer, value) && !_suppress && _ow != null && _owKind == OwKind.Trainer) RecomputeTrainerScript(); }
        }

        private int _owItemIndex = -1;
        public int OwItemIndex
        {
            get => _owItemIndex;
            set { if (Set(ref _owItemIndex, value) && !_suppress && _ow != null && _owKind == OwKind.Item && value >= 0) ForceOwScript((ushort)(7000 + value)); }
        }

        public bool OwItemIndexOutOfRange => _owKind == OwKind.Item && (_owItemIndex < 0 || _owItemIndex >= OwItemEntries.Count);

        private void RaiseOwKindChanged()
        {
            OnPropertyChanged(nameof(OwIsNormal)); OnPropertyChanged(nameof(OwIsTrainer)); OnPropertyChanged(nameof(OwIsItem));
            OnPropertyChanged(nameof(OwTrainerFieldsVisible)); OnPropertyChanged(nameof(OwItemFieldsVisible));
            OnPropertyChanged(nameof(OwTrainerIndexOutOfRange)); OnPropertyChanged(nameof(OwItemIndexOutOfRange));
            OnPropertyChanged(nameof(OwScriptEnabled));
        }

        /// <summary>Sets OwScript's model value + observable field directly, bypassing the property's equality guard
        /// (WinForms always forces the script number when the type/trainer/item selection changes).</summary>
        private void ForceOwScript(ushort value)
        {
            if (_ow != null) _ow.scriptNumber = value;
            _owScript = value;
            OnPropertyChanged(nameof(OwScript));
            OnPropertyChanged(nameof(OwScriptIndex)); OnPropertyChanged(nameof(OwScriptIndexOutOfRange));
            Dirty();
        }

        private void RecomputeTrainerScript()
        {
            if (_owTrainerIndex < 0) return;
            int idx = _owTrainerIndex;
            ushort scriptNum = (ushort)(idx + (_owPartnerTrainer ? 4999 : 2999));
            if (idx > RomInfo.trainerFunnyScriptNumber - 1) scriptNum++;
            ForceOwScript(scriptNum);
        }

        private void SetOwKind(OwKind kind)
        {
            bool changed = _owKind != kind;
            _owKind = kind;
            RaiseOwKindChanged();
            if (!changed || _suppress || _ow == null) return;

            switch (kind)
            {
                case OwKind.Normal:
                    _ow.type = (ushort)Overworld.OwType.NORMAL;
                    ForceOwScript(0);
                    break;
                case OwKind.Item:
                    _ow.type = (ushort)Overworld.OwType.ITEM;
                    if (_owItemIndex < 0 && OwItemEntries.Count > 0) OwItemIndex = 0;
                    else ForceOwScript((ushort)(7000 + Math.Max(_owItemIndex, 0)));
                    break;
                case OwKind.Trainer:
                    _ow.type = (ushort)Overworld.OwType.TRAINER;
                    if (_owTrainerIndex < 0 && OwTrainerEntries.Count > 0) OwTrainerIndex = 0;
                    else RecomputeTrainerScript();
                    break;
            }
        }

        private void PopulateOwTrainerAndItemEntries()
        {
            OwTrainerEntries.Clear();
            try { foreach (string name in TrainerNames.GetAll()) OwTrainerEntries.Add(name); }
            catch (Exception ex) { AppLogger.Error("PopulateOwTrainerEntries: " + ex.Message); }

            OwItemEntries.Clear();
            try
            {
                string[] itemNames = RomInfo.GetItemNames();
                if (PatchToolboxLogic.CheckScriptsStandardizedItemNumbers())
                {
                    foreach (string name in itemNames) OwItemEntries.Add(name);
                }
                else
                {
                    var itemScript = new ScriptFile(RomInfo.itemScriptFileNumber);
                    foreach (var cont in itemScript.allScripts)
                    {
                        if (cont.commands.Count > 4) continue;
                        ushort qty = BitConverter.ToUInt16(cont.commands[1].cmdParams[1], 0);
                        ushort itemId = BitConverter.ToUInt16(cont.commands[0].cmdParams[1], 0);
                        string name = itemId < itemNames.Length ? itemNames[itemId] : ("Item " + itemId);
                        OwItemEntries.Add(qty + "x " + name);
                    }
                }
            }
            catch (Exception ex) { AppLogger.Error("PopulateOwItemEntries: " + ex.Message); }
        }

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        // ── Current selection ───────────────────────────────────────────────────────
        private Event _current;          // base for shared position props
        private Spawnable _spawn;
        private Overworld _ow;
        private Warp _warp;
        private Trigger _trig;

        private int _selSpawn = -1, _selOw = -1, _selWarp = -1, _selTrig = -1;
        public int SelectedSpawnableIndex { get => _selSpawn; set { if (Set(ref _selSpawn, value)) LoadSpawnable(value); } }
        public int SelectedOverworldIndex { get => _selOw; set { if (Set(ref _selOw, value)) LoadOverworld(value); } }
        public int SelectedWarpIndex { get => _selWarp; set { if (Set(ref _selWarp, value)) LoadWarp(value); } }
        public int SelectedTriggerIndex { get => _selTrig; set { if (Set(ref _selTrig, value)) LoadTrigger(value); } }

        public bool HasSpawn => _spawn != null;
        public bool HasOw => _ow != null;
        public bool HasWarp => _warp != null;
        public bool HasTrig => _trig != null;

        // ── Shared position (map + matrix) ──────────────────────────────────────────
        private decimal _xMap, _yMap, _zPos, _xMat, _yMat;
        public decimal XMap { get => _xMap; set { if (Set(ref _xMap, value) && !_suppress && _current != null) { _current.xMapPosition = (short)value; Dirty(); RefreshMarkers(); } } }
        public decimal YMap { get => _yMap; set { if (Set(ref _yMap, value) && !_suppress && _current != null) { _current.yMapPosition = (short)value; Dirty(); RefreshMarkers(); } } }
        // zPosition is a 16.16 fixed-point value (1 tile = 65536 units) — WinForms' owZPositionUpDown shows/edits
        // the whole-tile part only (-32768..32768, no sub-tile Z control from this field).
        public decimal ZPos { get => _zPos; set { if (Set(ref _zPos, value) && !_suppress && _current != null) { _current.zPosition = (int)(value * 65536m); Dirty(); RefreshMarkers(); } } }
        public decimal XMatrix { get => _xMat; set { if (Set(ref _xMat, value) && !_suppress && _current != null) { _current.xMatrixPosition = (ushort)value; Dirty(); DisplayMap(); } } }
        public decimal YMatrix { get => _yMat; set { if (Set(ref _yMat, value) && !_suppress && _current != null) { _current.yMatrixPosition = (ushort)value; Dirty(); DisplayMap(); } } }

        // ── Spawnable fields ────────────────────────────────────────────────────────
        private decimal _spScript, _spType, _spDir;
        public decimal SpScript
        {
            get => _spScript;
            set
            {
                if (Set(ref _spScript, value) && !_suppress && _spawn != null)
                {
                    _spawn.scriptNumber = (ushort)value; Dirty();
                    OnPropertyChanged(nameof(SpScriptIndex)); OnPropertyChanged(nameof(SpScriptIndexOutOfRange));
                }
            }
        }
        public decimal SpType { get => _spType; set { if (Set(ref _spType, value) && !_suppress && _spawn != null) { _spawn.type = (ushort)value; Dirty(); } } }
        public decimal SpDir { get => _spDir; set { if (Set(ref _spDir, value) && !_suppress && _spawn != null) { _spawn.dir = (ushort)value; Dirty(); } } }

        public int SpScriptIndex
        {
            get => IndexOfAvailableScript(_spScript);
            set { if (value >= 0 && value < _availableScriptIds.Count) SpScript = _availableScriptIds[value]; }
        }
        public bool SpScriptIndexOutOfRange => _spawn != null && SpScriptIndex < 0;

        // ── Overworld fields ────────────────────────────────────────────────────────
        private decimal _owId, _owSprite, _owMove, _owType, _owFlag, _owScript, _owOrient, _owSight, _owXr, _owYr;
        public decimal OwId { get => _owId; set { if (Set(ref _owId, value) && !_suppress && _ow != null) { _ow.owID = (ushort)value; Dirty(); } } }
        public decimal OwSprite
        {
            get => _owSprite;
            set { if (Set(ref _owSprite, value) && !_suppress && _ow != null) { _ow.overlayTableEntry = (ushort)value; Dirty(); RefreshMarkers(); UpdateOwSpritePreview(); } }
        }
        public decimal OwMovement { get => _owMove; set { if (Set(ref _owMove, value) && !_suppress && _ow != null) { _ow.movement = (ushort)value; Dirty(); } } }
        public decimal OwType { get => _owType; set { if (Set(ref _owType, value) && !_suppress && _ow != null) { _ow.type = (ushort)value; Dirty(); } } }
        public decimal OwFlag { get => _owFlag; set { if (Set(ref _owFlag, value) && !_suppress && _ow != null) { _ow.flag = (ushort)value; Dirty(); } } }
        public decimal OwScript
        {
            get => _owScript;
            set
            {
                if (Set(ref _owScript, value) && !_suppress && _ow != null)
                {
                    _ow.scriptNumber = (ushort)value; Dirty();
                    OnPropertyChanged(nameof(OwScriptIndex)); OnPropertyChanged(nameof(OwScriptIndexOutOfRange));
                }
            }
        }
        public int OwScriptIndex
        {
            get => IndexOfAvailableScript(_owScript);
            set { if (value >= 0 && value < _availableScriptIds.Count) OwScript = _availableScriptIds[value]; }
        }
        public bool OwScriptIndexOutOfRange => _ow != null && OwScriptIndex < 0;
        public decimal OwOrientation
        {
            get => _owOrient;
            set { if (Set(ref _owOrient, value) && !_suppress && _ow != null) { _ow.orientation = (ushort)value; Dirty(); RefreshMarkers(); UpdateOwSpritePreview(); } }
        }
        public decimal OwSight { get => _owSight; set { if (Set(ref _owSight, value) && !_suppress && _ow != null) { _ow.sightRange = (ushort)value; Dirty(); } } }
        public decimal OwXRange { get => _owXr; set { if (Set(ref _owXr, value) && !_suppress && _ow != null) { _ow.xRange = (ushort)value; Dirty(); } } }
        public decimal OwYRange { get => _owYr; set { if (Set(ref _owYr, value) && !_suppress && _ow != null) { _ow.yRange = (ushort)value; Dirty(); } } }

        // Dropdown-friendly wrappers: the sprite entry list is the sparse set of valid overlay-table
        // keys (RomInfo.overworldTableKeys), so its ComboBox index isn't the same as the raw value —
        // movement/orientation are plain 0..N index=value lists so they pass straight through.
        public int OwSpriteIndex
        {
            get => overworldTableKeys == null ? -1 : Array.IndexOf(overworldTableKeys, (uint)_owSprite);
            set
            {
                if (overworldTableKeys == null || value < 0 || value >= overworldTableKeys.Length) return;
                OwSprite = overworldTableKeys[value];
                OnPropertyChanged();
            }
        }
        public int OwMovementIndex { get => (int)_owMove; set => OwMovement = value; }
        public int OwOrientationIndex { get => (int)_owOrient; set => OwOrientation = value; }

        private void UpdateOwSpritePreview()
        {
            global::Avalonia.Media.Imaging.Bitmap bmp = null;
            if (_ow != null)
            {
                try
                {
                    var pix = OverworldSprites.Get(_ow.overlayTableEntry, _ow.orientation);
                    if (pix != null && pix.Width > 0 && pix.Height > 0)
                    {
                        var bgra = new byte[pix.Rgba.Length];
                        for (int i = 0; i < bgra.Length; i += 4)
                        {
                            bgra[i] = pix.Rgba[i + 2]; bgra[i + 1] = pix.Rgba[i + 1];
                            bgra[i + 2] = pix.Rgba[i]; bgra[i + 3] = pix.Rgba[i + 3];
                        }
                        bmp = ImageConverter.ToAvaloniaBitmap(new DSPRE.RawImage(pix.Width, pix.Height, bgra));
                    }
                }
                catch (Exception ex) { AppLogger.Error("UpdateOwSpritePreview: " + ex.Message); }
            }
            OwSpritePreview = bmp;
            OnPropertyChanged(nameof(HasOwSpritePreview));
        }

        // ── Warp fields ───────────────────────────────────────────────────────────────
        private decimal _warpHeader, _warpAnchor, _warpHeight;
        public decimal WarpHeader { get => _warpHeader; set { if (Set(ref _warpHeader, value) && !_suppress && _warp != null) { _warp.header = (ushort)value; Dirty(); } } }
        public decimal WarpAnchor { get => _warpAnchor; set { if (Set(ref _warpAnchor, value) && !_suppress && _warp != null) { _warp.anchor = (ushort)value; Dirty(); } } }
        public decimal WarpHeight { get => _warpHeight; set { if (Set(ref _warpHeight, value) && !_suppress && _warp != null) { _warp.height = (uint)value; Dirty(); } } }

        // ── Trigger fields ──────────────────────────────────────────────────────────
        private decimal _trScript, _trW, _trH, _trVarVal, _trVar;
        public decimal TrScript
        {
            get => _trScript;
            set
            {
                if (Set(ref _trScript, value) && !_suppress && _trig != null)
                {
                    _trig.scriptNumber = (ushort)value; Dirty();
                    OnPropertyChanged(nameof(TrScriptIndex)); OnPropertyChanged(nameof(TrScriptIndexOutOfRange));
                }
            }
        }
        public int TrScriptIndex
        {
            get => IndexOfAvailableScript(_trScript);
            set { if (value >= 0 && value < _availableScriptIds.Count) TrScript = _availableScriptIds[value]; }
        }
        public bool TrScriptIndexOutOfRange => _trig != null && TrScriptIndex < 0;
        public decimal TrWidth { get => _trW; set { if (Set(ref _trW, value) && !_suppress && _trig != null) { _trig.widthX = (ushort)value; Dirty(); } } }
        public decimal TrHeight { get => _trH; set { if (Set(ref _trH, value) && !_suppress && _trig != null) { _trig.heightY = (ushort)value; Dirty(); } } }
        public decimal TrVarValue { get => _trVarVal; set { if (Set(ref _trVarVal, value) && !_suppress && _trig != null) { _trig.expectedVarValue = (ushort)value; Dirty(); } } }
        public decimal TrVar { get => _trVar; set { if (Set(ref _trVar, value) && !_suppress && _trig != null) { _trig.variableWatched = (ushort)value; Dirty(); } } }

        // ── Dirty tracking ───────────────────────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => $"Event file {_selectedIndex}";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); if (_selectedIndex >= 0) LoadFile(_selectedIndex); }
        private void Dirty() { if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        private int _selectedIndex = -1;
        public int SelectedEventIndex
        {
            get => _selectedIndex;
            set { if (Set(ref _selectedIndex, value) && !_suppress && value >= 0) LoadFile(value); }
        }

        public int InitialIndex { get; set; }

        // ── 3D marker visibility toggles (per event type) ────────────────────────────────
        private bool _showOw = true, _showWarp = true, _showTrig = true, _showSpawn = true, _showGrid;
        public bool ShowOverworlds { get => _showOw; set { if (Set(ref _showOw, value)) RefreshMarkers(); } }
        public bool ShowWarps { get => _showWarp; set { if (Set(ref _showWarp, value)) RefreshMarkers(); } }
        public bool ShowTriggers { get => _showTrig; set { if (Set(ref _showTrig, value)) RefreshMarkers(); } }
        public bool ShowSpawnables { get => _showSpawn; set { if (Set(ref _showSpawn, value)) RefreshMarkers(); } }
        public bool ShowGrid { get => _showGrid; set { if (Set(ref _showGrid, value)) RefreshMarkers(); } }
        /// <summary>The tile-boundary grid overlay is only built for small matrices (≤4 cells) — it's an
        /// expensive per-tile collision scan that would otherwise repeat on every event add/move for a
        /// large header, so bigger matrices skip it rather than pay that cost every refresh. The
        /// checkbox is disabled instead of silently doing nothing so the limit is visible.</summary>
        public bool GridToggleEnabled => _matrix != null && _matrix.width * _matrix.height <= 4;

        private bool _stitchGrid = true;
        public bool StitchGrid { get => _stitchGrid; set { if (Set(ref _stitchGrid, value)) DisplayMap(); } }
        private NsbmdGeometry.MatrixStitchMode StitchMode => _stitchGrid ? NsbmdGeometry.MatrixStitchMode.Grid : NsbmdGeometry.MatrixStitchMode.Continuous;

        public EventEditorViewModel() { if (Design.IsDesignMode) EventNames.Add("Event 0"); }
        public EventEditorViewModel(bool _) { }

        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> {
                    DirNames.eventFiles, DirNames.maps, DirNames.exteriorBuildingModels,
                    DirNames.buildingTextures, DirNames.mapTextures, DirNames.matrices,
                    DirNames.areaData, DirNames.dynamicHeaders, DirNames.OWSprites,
                    // Needed by PopulateOwTrainerAndItemEntries below (Trainer/Item dropdowns).
                    DirNames.trainerProperties, DirNames.scripts, DirNames.textArchives });
                if (gameFamily == GameFamilies.HGSS)
                    DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.interiorBuildingModels });
                _eventToHeader = BuildEventHeaderLookup();
                // Overworld sprite lookups are populated during ROM/event setup in WinForms;
                // ensure they exist for the 3D sprite billboards here (SetOWtable must run first).
                try { if (ow3DSpriteDict == null) Set3DOverworldsDict(); } catch (Exception ex) { AppLogger.Error("Set3DOverworldsDict: " + ex.Message); }
                try { if (OverworldTable == null) { SetOWtable(); ReadOWTable(); } } catch (Exception ex) { AppLogger.Error("ReadOWTable: " + ex.Message); }
                OwSpriteEntries.Clear();
                if (overworldTableKeys != null)
                    foreach (uint key in overworldTableKeys) OwSpriteEntries.Add("OW Entry " + key);
                PopulateOwTrainerAndItemEntries();
                EventNames.Clear();
                int count = Filesystem.GetEventFileCount();
                for (int i = 0; i < count; i++) EventNames.Add("Event File " + i);
                StatusText = $"{count} event files.";
                if (count > 0) SelectedEventIndex = Math.Min(Math.Max(0, InitialIndex), count - 1);
            }
            catch (Exception ex)
            {
                StatusText = "Error: " + ex.Message;
                await DialogHelper.ShowError($"Failed to set up Event Editor:\n{ex.Message}", "Event Editor");
            }
        }

        private void LoadFile(int index)
        {
            try
            {
                _file = new EventFile(index);
                RefreshLists();
                ResolveMatrixForFile(index);
                SetClean();
                StatusText = $"Loaded event file {index}.";
                OnPropertyChanged(nameof(UnsavedChangesDescription));
            }
            catch (Exception ex) { _ = DialogHelper.ShowError($"Failed to load event file {index}:\n{ex.Message}", "Event Editor"); }
        }

        private void RefreshLists()
        {
            _suppress = true;
            Spawnables.Clear(); Overworlds.Clear(); Warps.Clear(); Triggers.Clear();
            if (_file != null)
            {
                for (int i = 0; i < _file.spawnables.Count; i++) Spawnables.Add($"Spawnable {i:D2}");
                for (int i = 0; i < _file.overworlds.Count; i++) Overworlds.Add($"Overworld {i:D2}");
                for (int i = 0; i < _file.warps.Count; i++) Warps.Add($"Warp {i:D2}");
                for (int i = 0; i < _file.triggers.Count; i++) Triggers.Add($"Trigger {i:D2}");
            }
            _suppress = false;
        }

        private void LoadPosition(Event e)
        {
            _current = e;
            if (e == null) return;
            XMap = e.xMapPosition; YMap = e.yMapPosition; ZPos = e.zPosition / 65536m;
            XMatrix = e.xMatrixPosition; YMatrix = e.yMatrixPosition;
        }

        private void LoadSpawnable(int i)
        {
            _spawn = (_file != null && i >= 0 && i < _file.spawnables.Count) ? _file.spawnables[i] : null;
            OnPropertyChanged(nameof(HasSpawn));
            if (_spawn == null) return;
            _suppress = true;
            LoadPosition(_spawn);
            SpScript = _spawn.scriptNumber; SpType = _spawn.type; SpDir = _spawn.dir;
            _suppress = false;
            OnPropertyChanged(nameof(SpScriptIndex)); OnPropertyChanged(nameof(SpScriptIndexOutOfRange));
            RefreshMarkers();
        }

        private void LoadOverworld(int i)
        {
            _ow = (_file != null && i >= 0 && i < _file.overworlds.Count) ? _file.overworlds[i] : null;
            OnPropertyChanged(nameof(HasOw));
            if (_ow == null) { UpdateOwSpritePreview(); return; }
            _suppress = true;
            LoadPosition(_ow);
            OwId = _ow.owID; OwSprite = _ow.overlayTableEntry; OwMovement = _ow.movement; OwType = _ow.type;
            OwFlag = _ow.flag; OwScript = _ow.scriptNumber; OwOrientation = _ow.orientation; OwSight = _ow.sightRange;
            OwXRange = _ow.xRange; OwYRange = _ow.yRange;
            OnPropertyChanged(nameof(OwSpriteIndex)); OnPropertyChanged(nameof(OwMovementIndex)); OnPropertyChanged(nameof(OwOrientationIndex));
            OnPropertyChanged(nameof(OwScriptIndex)); OnPropertyChanged(nameof(OwScriptIndexOutOfRange));

            // Derive the Standard/Trainer/Item radio selection + locked-script dropdown index from the
            // raw type/scriptNumber (mirrors WinForms' overworldsListBox_SelectedIndexChanged, but uses
            // the trainerFunnyScriptNumber-aware inverse — see NavigateToOverworldTarget in the WinForms
            // EventEditor — instead of WinForms' own display-only reverse mapping, which is off by one
            // past that threshold).
            if (_ow.type == (ushort)Overworld.OwType.TRAINER)
            {
                _owKind = OwKind.Trainer;
                bool partner = _ow.scriptNumber >= 4999;
                int idx = partner ? _ow.scriptNumber - 4999 : _ow.scriptNumber - 2999;
                if (idx > RomInfo.trainerFunnyScriptNumber - 1) idx--;
                // Out of range (past the end of the current trainer roster) → leave unselected (-1) rather
                // than clamping into a wrong trainer; OwTrainerIndexOutOfRange surfaces this in the UI.
                _owTrainerIndex = (idx >= 0 && idx < OwTrainerEntries.Count) ? idx : -1;
                _owPartnerTrainer = partner;
            }
            else if (_ow.type == (ushort)Overworld.OwType.ITEM || (_ow.scriptNumber >= 7000 && _ow.scriptNumber <= 8000))
            {
                _owKind = OwKind.Item;
                int itemIdx = _ow.scriptNumber - 7000;
                _owItemIndex = (itemIdx >= 0 && itemIdx < OwItemEntries.Count) ? itemIdx : -1;
            }
            else
            {
                _owKind = OwKind.Normal;
            }
            RaiseOwKindChanged();
            OnPropertyChanged(nameof(OwTrainerIndex)); OnPropertyChanged(nameof(OwPartnerTrainer)); OnPropertyChanged(nameof(OwItemIndex));
            OnPropertyChanged(nameof(OwTrainerIndexOutOfRange)); OnPropertyChanged(nameof(OwItemIndexOutOfRange)); OnPropertyChanged(nameof(OwScriptEnabled));

            _suppress = false;
            RefreshMarkers();
            UpdateOwSpritePreview();
        }

        private void LoadWarp(int i)
        {
            _warp = (_file != null && i >= 0 && i < _file.warps.Count) ? _file.warps[i] : null;
            OnPropertyChanged(nameof(HasWarp));
            if (_warp == null) return;
            _suppress = true;
            LoadPosition(_warp);
            WarpHeader = _warp.header; WarpAnchor = _warp.anchor; WarpHeight = _warp.height;
            _suppress = false;
            RefreshMarkers();
        }

        private void LoadTrigger(int i)
        {
            _trig = (_file != null && i >= 0 && i < _file.triggers.Count) ? _file.triggers[i] : null;
            OnPropertyChanged(nameof(HasTrig));
            if (_trig == null) return;
            _suppress = true;
            LoadPosition(_trig);
            TrScript = _trig.scriptNumber; TrWidth = _trig.widthX; TrHeight = _trig.heightY;
            TrVarValue = _trig.expectedVarValue; TrVar = _trig.variableWatched;
            _suppress = false;
            OnPropertyChanged(nameof(TrScriptIndex)); OnPropertyChanged(nameof(TrScriptIndexOutOfRange));
            RefreshMarkers();
        }

        // ── Add / remove ────────────────────────────────────────────────────────────
        public void AddSpawnable() { if (_file == null) return; _file.spawnables.Add(new Spawnable(0, 0)); RefreshLists(); Dirty(); SelectedSpawnableIndex = _file.spawnables.Count - 1; }
        public void RemoveSpawnable() { if (_file == null || _selSpawn < 0 || _selSpawn >= _file.spawnables.Count) return; _file.spawnables.RemoveAt(_selSpawn); RefreshLists(); Dirty(); SelectedSpawnableIndex = -1; RefreshMarkers(); }
        public void AddOverworld() { if (_file == null) return; _file.overworlds.Add(new Overworld(0, 0, 0)); RefreshLists(); Dirty(); SelectedOverworldIndex = _file.overworlds.Count - 1; }
        public void RemoveOverworld() { if (_file == null || _selOw < 0 || _selOw >= _file.overworlds.Count) return; _file.overworlds.RemoveAt(_selOw); RefreshLists(); Dirty(); SelectedOverworldIndex = -1; RefreshMarkers(); }
        public void AddWarp() { if (_file == null) return; _file.warps.Add(new Warp(0, 0)); RefreshLists(); Dirty(); SelectedWarpIndex = _file.warps.Count - 1; }
        public void RemoveWarp() { if (_file == null || _selWarp < 0 || _selWarp >= _file.warps.Count) return; _file.warps.RemoveAt(_selWarp); RefreshLists(); Dirty(); SelectedWarpIndex = -1; RefreshMarkers(); }
        public void AddTrigger() { if (_file == null) return; _file.triggers.Add(new Trigger(0, 0)); RefreshLists(); Dirty(); SelectedTriggerIndex = _file.triggers.Count - 1; }
        public void RemoveTrigger() { if (_file == null || _selTrig < 0 || _selTrig >= _file.triggers.Count) return; _file.triggers.RemoveAt(_selTrig); RefreshLists(); Dirty(); SelectedTriggerIndex = -1; RefreshMarkers(); }

        // ── Duplicate selected (copy ctors) ──────────────────────────────────────────────
        public void DuplicateSpawnable() { if (_file == null || _spawn == null) return; _file.spawnables.Add(new Spawnable(_spawn)); RefreshLists(); Dirty(); SelectedSpawnableIndex = _file.spawnables.Count - 1; }
        public void DuplicateOverworld() { if (_file == null || _ow == null) return; _file.overworlds.Add(new Overworld(_ow)); RefreshLists(); Dirty(); SelectedOverworldIndex = _file.overworlds.Count - 1; }
        public void DuplicateWarp() { if (_file == null || _warp == null) return; _file.warps.Add(new Warp(_warp)); RefreshLists(); Dirty(); SelectedWarpIndex = _file.warps.Count - 1; }
        public void DuplicateTrigger() { if (_file == null || _trig == null) return; _file.triggers.Add(new Trigger(_trig)); RefreshLists(); Dirty(); SelectedTriggerIndex = _file.triggers.Count - 1; }

        /// <summary>Follow the selected warp to its destination: switch to that header's event file.</summary>
        public void TestWarp()
        {
            if (_warp == null) return;
            try
            {
                var h = MapHeader.GetMapHeader(_warp.header);
                if (h == null) { StatusText = $"Destination header {_warp.header} not found."; return; }
                int dest = h.eventFileID;
                if (dest < 0 || dest >= EventNames.Count) { StatusText = $"Header {_warp.header} → event file {dest} (out of range)."; return; }
                SelectedEventIndex = dest;
                StatusText = $"Warp leads to header {_warp.header} → event file {dest}.";
            }
            catch (Exception ex) { StatusText = "Test warp failed: " + ex.Message; }
        }

        // ── Sort overworlds by OW id ─────────────────────────────────────────────────────
        public void SortOverworldsAsc() { if (_file == null) return; _file.overworlds.Sort((a, b) => a.owID.CompareTo(b.owID)); RefreshLists(); Dirty(); }
        public void SortOverworldsDesc() { if (_file == null) return; _file.overworlds.Sort((a, b) => b.owID.CompareTo(a.owID)); RefreshLists(); Dirty(); }

        // ── Event file add / remove ──────────────────────────────────────────────────────
        public void AddEventFile()
        {
            try
            {
                int newId = Filesystem.GetEventFileCount();
                new EventFile().SaveToFileDefaultDir(newId, showSuccessMessage: false);
                EventNames.Add("Event File " + newId);
                SelectedEventIndex = newId;
                StatusText = $"Added event file {newId}.";
            }
            catch (Exception ex) { _ = DialogHelper.ShowError($"Couldn't add event file:\n{ex.Message}", "Event Editor"); }
        }

        public async Task RemoveLastEventFileAsync()
        {
            int count = EventNames.Count;
            if (count == 0) return;
            int last = count - 1;
            if (!await DialogHelper.AskYesNo($"Delete the last event file ({last})?", "Confirm deletion")) return;
            try
            {
                System.IO.File.Delete(Path.Combine(gameDirs[DirNames.eventFiles].unpackedDir, last.ToString("D4")));
                if (_selectedIndex == last) SelectedEventIndex = last - 1;
                EventNames.RemoveAt(last);
                StatusText = $"Removed event file {last}.";
            }
            catch (Exception ex) { _ = DialogHelper.ShowError($"Couldn't remove event file:\n{ex.Message}", "Event Editor"); }
        }

        // ── 3D map view + event markers ─────────────────────────────────────────────────

        /// <summary>
        /// Builds the reverse map: event-file index → (matrix, area data) via the header that
        /// references it (<see cref="MapHeader.eventFileID"/>). This is the real ROM linkage
        /// the WinForms editor uses to pick the correct map + texture packs for an event file.
        /// </summary>
        private static Dictionary<int, (ushort, byte, ushort)> BuildEventHeaderLookup()
        {
            var lookup = new Dictionary<int, (ushort, byte, ushort)>();
            try
            {
                int headerCount = GetHeaderCount();
                for (ushort h = 0; h < headerCount; h++)
                {
                    try
                    {
                        var header = MapHeader.GetMapHeader(h);
                        if (header == null) continue;
                        if (!lookup.ContainsKey(header.eventFileID))
                            lookup[header.eventFileID] = (header.matrixID, header.areaDataID, header.scriptFileID);
                    }
                    catch { /* skip bad header */ }
                }
            }
            catch (Exception ex) { AppLogger.Error("Event→header lookup failed: " + ex.Message); }
            return lookup;
        }

        /// <summary>Resolves the matrix + area for the loaded event file, then renders the whole matrix.</summary>
        private void ResolveMatrixForFile(int eventIndex)
        {
            _matrix = null; _areaDataId = 0; _matrixId = -1;
            _collisionCache.Clear();
            int pairedScriptFileId = -1;
            try
            {
                if (_eventToHeader != null && _eventToHeader.TryGetValue(eventIndex, out var hdr))
                {
                    _matrixId = hdr.Item1;
                    _matrix = new GameMatrix(hdr.Item1);
                    _areaDataId = hdr.Item2;
                    pairedScriptFileId = hdr.Item3;
                }
            }
            catch (Exception ex) { AppLogger.Error("Matrix resolve failed: " + ex.Message); }
            OnPropertyChanged(nameof(GridToggleEnabled));
            PopulateAvailableScripts(pairedScriptFileId);
            DisplayMap();
        }

        // ── Scripts available to this event file (the header's paired script file) ────────
        /// <summary>The scripts defined in the header's paired script file (via <see cref="MapHeader.scriptFileID"/>),
        /// so Overworld/Trigger/Spawnable "Script" fields can be picked from a dropdown of what's actually
        /// callable here instead of a free-form number. Values are each script's <c>manualUserID</c> — the
        /// number these events' <c>scriptNumber</c> fields reference — not necessarily a plain 0..N-1 run.</summary>
        public ObservableCollection<string> AvailableScripts { get; } = new ObservableCollection<string>();
        private readonly List<uint> _availableScriptIds = new List<uint>();

        private void PopulateAvailableScripts(int scriptFileId)
        {
            AvailableScripts.Clear();
            _availableScriptIds.Clear();
            if (scriptFileId >= 0)
            {
                try
                {
                    var scriptFile = new ScriptFile(scriptFileId);
                    foreach (var container in scriptFile.allScripts)
                    {
                        _availableScriptIds.Add(container.manualUserID);
                        AvailableScripts.Add($"Script {container.manualUserID} ({container.commands?.Count ?? 0} cmds)");
                    }
                }
                catch (Exception ex) { AppLogger.Error("PopulateAvailableScripts: " + ex.Message); }
            }
            OnPropertyChanged(nameof(TrScriptIndex)); OnPropertyChanged(nameof(TrScriptIndexOutOfRange));
            OnPropertyChanged(nameof(SpScriptIndex)); OnPropertyChanged(nameof(SpScriptIndexOutOfRange));
            OnPropertyChanged(nameof(OwScriptIndex)); OnPropertyChanged(nameof(OwScriptIndexOutOfRange));
        }

        private int IndexOfAvailableScript(decimal rawValue) => _availableScriptIds.IndexOf((uint)rawValue);

        private int _matrixId = -1;

        /// <summary>
        /// Renders only the maps this event file's events actually sit on (the cells they
        /// occupy in the header's matrix), stitched together. This keeps the view focused on
        /// the maps the file belongs to rather than loading an entire (possibly world-sized)
        /// matrix. Each cell's tileset is resolved through its header section / the file's area.
        /// </summary>
        private void DisplayMap()
        {
            Model3D = null;
            try
            {
                if (_matrix == null)
                {
                    MapInfo = "No header references this event file, so there is no matrix to render.";
                    MapLoaded?.Invoke(this, EventArgs.Empty); RefreshMarkers(); return;
                }

                // Small matrices (interiors / routes / regions) render in full — exactly like the
                // map editor's working full-matrix view — so the per-cell stride is derived from the
                // whole matrix (correct true map size) and maps stitch seamlessly. Only a giant world
                // matrix falls back to the bounding box of the event's own cells (to avoid loading it all).
                int total = _matrix.width * _matrix.height;
                ISet<(int x, int y)> include = total <= 256 ? null : EventCells();

                Model3D = MatrixSceneBuilder.Build(_matrix, _areaDataId, gameFamily, areaForMap: null, includeCells: include, mode: StitchMode);
                string scope = include == null ? "full" : $"{include.Count}-cell region";
                MapInfo = Model3D != null
                    ? $"Matrix {_matrixId}  ·  {_matrix.width}×{_matrix.height} ({scope})  ·  area {_areaDataId}"
                    : $"Matrix {_matrixId}: no renderable maps.";
            }
            catch (Exception ex)
            {
                MapInfo = "Map render failed: " + ex.Message;
                AppLogger.Error("Event map render failed: " + ex.Message);
            }
            MapLoaded?.Invoke(this, EventArgs.Empty);
            RefreshMarkers();
        }

        /// <summary>
        /// The matrix cells to render for this event file: the bounding box spanning every cell its
        /// events occupy, so the maps between them are loaded too and stitch into one continuous
        /// surface (rather than just the exact occupied cells, which leaves holes where an event
        /// skips a cell). Capped so a stray far-flung event can't pull in a whole world matrix.
        /// </summary>
        private HashSet<(int x, int y)> EventCells()
        {
            var set = new HashSet<(int x, int y)>();
            if (_file == null || _matrix == null) return set;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            void Note(Event e)
            {
                int x = e.xMatrixPosition, y = e.yMatrixPosition;
                if (x < 0 || y < 0 || x >= _matrix.width || y >= _matrix.height) return;
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            }
            foreach (var e in _file.overworlds) Note(e);
            foreach (var e in _file.warps) Note(e);
            foreach (var e in _file.triggers) Note(e);
            foreach (var e in _file.spawnables) Note(e);
            if (maxX < minX) return set;   // no events

            const int MaxSpan = 12;        // cap the bounding box per axis (keeps loads sane)
            if (maxX - minX > MaxSpan) maxX = minX + MaxSpan;
            if (maxY - minY > MaxSpan) maxY = minY + MaxSpan;

            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    set.Add((x, y));
            return set;
        }

        // Per-type marker colours (RGB 0..1).
        private static (float r, float g, float b) MarkerColor(int type) => type switch
        {
            0 => (0.25f, 0.95f, 0.35f),   // overworld  → green
            1 => (1.00f, 0.62f, 0.10f),   // warp       → orange
            2 => (0.95f, 0.25f, 0.90f),   // trigger    → magenta
            _ => (0.20f, 0.85f, 0.95f),   // spawnable  → cyan
        };

        private const int MapTiles = 32;

        /// <summary>
        /// Rebuilds the event overlay: warps/triggers/spawnables as flat colour quads and
        /// overworlds as upright textured billboards (their real sprite). Everything is placed
        /// by matrix cell + tile + z so it sits where it will be in-game.
        /// </summary>
        public void RefreshMarkers()
        {
            MarkerMesh = null; MarkerVertexCount = 0;
            var sprites = new List<NsbmdGlControl.SpriteInstance>();
            var m = Model3D;
            if (_file != null && m != null && m.CellStrideX != 0)
            {
                float tileX = m.CellStrideX / MapTiles;
                float tileZ = m.CellStrideZ / MapTiles;

                (float x, float z) Cell(Event e) => EventCellRaw(m, e);
                // The map is a 3D model whose top plane is the walkable ground. Place each event on
                // the sampled surface at its tile (so it sits ON the plane, not at world-Y 0 which is
                // below it). For overworld/spawnable Y, the field is a height-lookup hint, not an additive lift.
                float EventY(float rawX, float rawZ, Event e) => EventSurfaceY(m, rawX, rawZ, e);

                (float x, float y, float z) Foot(Event e)
                {
                    var (rawX, rawZ) = Cell(e);
                    return m.ToNormalized(rawX, EventY(rawX, rawZ, e), rawZ);
                }

                var v = new List<float>(256);
                void Quad(Event e, (float r, float g, float b) col)
                {
                    bool sel = ReferenceEquals(e, _current);
                    var c = sel ? (1f, 1f, 1f) : col;
                    float half = (sel ? 0.46f : 0.40f);
                    var (rawX, rawZ) = Cell(e);
                    AddMarker(v, m, rawX, EventY(rawX, rawZ, e), rawZ, half * tileX, half * tileZ, c);
                }

                if (_showGrid && _matrix != null && _matrix.width * _matrix.height <= 4)
                {
                    int n = MapFile.mapSize;
                    float inset = tileX * 0.07f;
                    for (int gcy = 0; gcy < _matrix.height; gcy++)
                        for (int gcx = 0; gcx < _matrix.width; gcx++)
                        {
                            if (!m.TryCellPlacement(gcx, gcy, out var gp)) continue;
                            long key = ((long)gcy << 32) | (uint)gcx;
                            if (!_collisionCache.TryGetValue(key, out var col))
                            {
                                col = null;
                                try
                                {
                                    int map = _matrix.maps[gcy, gcx];
                                    if (map != GameMatrix.EMPTY) col = new MapFile(map, gameFamily, false, false).collisions;
                                }
                                catch { }
                                _collisionCache[key] = col;
                            }
                            if (col == null) continue;
                            byte oob = col[n - 1, n - 1];
                            for (int ty = 0; ty < n; ty++)
                                for (int tx = 0; tx < n; tx++)
                                {
                                    if (col[ty, tx] != oob) continue;
                                    float x0 = gp.OriginX + tx * tileX;
                                    float z0 = gp.OriginZ + ty * tileZ;
                                    float cx = x0 + tileX * 0.5f, cz = z0 + tileZ * 0.5f;
                                    if (!m.TryBdhcSurfaceY(gcx, gcy, cx, cz, 0f, out var yc)) yc = m.SurfaceY(cx, cz);
                                    yc += 0.01f;
                                    AddFlatQuad(v, m, x0 + inset, z0 + inset, x0 + tileX - inset, z0 + tileZ - inset, yc, (0.20f, 0.55f, 0.95f));
                                }
                        }
                }

                if (_showWarp) foreach (var e in _file.warps) Quad(e, MarkerColor(1));
                if (_showTrig) foreach (var e in _file.triggers) Quad(e, MarkerColor(2));
                if (_showSpawn) foreach (var e in _file.spawnables) Quad(e, MarkerColor(3));

                // Overworlds → real sprite billboards (foot anchored on the surface). Selected
                // overworlds also get a white ground ring so the selection is obvious.
                float spriteH = tileX * m.Scale * 1.6f;
                if (_showOw)
                    foreach (var ow in _file.overworlds)
                    {
                        bool sel = ReferenceEquals(ow, _current);
                        if (sel) Quad(ow, (1f, 1f, 1f));

                        var pix = OverworldSprites.Get(ow.overlayTableEntry, ow.orientation);
                        var foot = Foot(ow);
                        if (pix != null && pix.Width > 0 && pix.Height > 0)
                        {
                            float halfH = spriteH * 0.5f;
                            float halfW = halfH * (pix.Width / (float)pix.Height);
                            sprites.Add(new NsbmdGlControl.SpriteInstance
                            {
                                Cx = foot.x,
                                Cy = foot.y + halfH,
                                Cz = foot.z,
                                HalfW = halfW,
                                HalfH = halfH,
                                Rgba = pix.Rgba,
                                Width = pix.Width,
                                Height = pix.Height,
                            });
                        }
                        else Quad(ow, MarkerColor(0));   // fall back to a green quad if no sprite
                    }

                MarkerMesh = v.ToArray();
                MarkerVertexCount = v.Count / 8;
            }
            Sprites = sprites;
            MarkersChanged?.Invoke(this, EventArgs.Empty);
            SpritesChanged?.Invoke(this, EventArgs.Empty);
            GizmoTargetChanged?.Invoke(this, EventArgs.Empty);   // keep the move-gizmo on the selected event
        }

        // ── Event world placement (shared by markers + the move gizmo) ────────────────────
        private (float x, float z) EventCellRaw(NsbmdRenderModel m, Event e)
        {
            if (m.TryCellPlacement(e.xMatrixPosition, e.yMatrixPosition, out var p))
                return (p.OriginX + (e.xMapPosition + 0.5f) / MapTiles * p.Width,
                        p.OriginZ + (e.yMapPosition + 0.5f) / MapTiles * p.Height);
            return (m.CellBaseX + (e.xMatrixPosition + (e.xMapPosition + 0.5f) / MapTiles) * m.CellStrideX,
                    m.CellBaseZ + (e.yMatrixPosition + (e.yMapPosition + 0.5f) / MapTiles) * m.CellStrideZ);
        }

        private float EventSurfaceY(NsbmdRenderModel m, float rawX, float rawZ, Event e)
        {
            // zPosition is an FX32 height hint. BDHC/mesh lookup chooses the actual floor near it.
            float yHint = e.zPosition / 262144f;
            if (m.TryBdhcSurfaceY(e.xMatrixPosition, e.yMatrixPosition, rawX, rawZ, yHint, out var bdhcY)) return bdhcY;
            return e.zPosition == 0 ? m.SurfaceY(rawX, rawZ) : m.SurfaceY(rawX, rawZ, yHint);
        }

        // ── 3D edit mode (drag the selected event with the translate gizmo) ───────────────
        private bool _editMode3D;
        public bool EditMode3D
        {
            get => _editMode3D;
            set { if (Set(ref _editMode3D, value)) { OnPropertyChanged(nameof(EditMode3D)); EditModeChanged?.Invoke(this, EventArgs.Empty); } }
        }
        public event EventHandler EditModeChanged;
        public event EventHandler GizmoTargetChanged;
        public float ModelScale => Model3D?.Scale ?? 1f;

        public bool TrySelectedEventAnchorNorm(out float nx, out float ny, out float nz)
            => EventAnchorNorm(_current, out nx, out ny, out nz);

        private bool EventAnchorNorm(Event e, out float nx, out float ny, out float nz)
        {
            nx = ny = nz = 0f;
            var m = Model3D;
            if (m == null || e == null) return false;
            var (rx, rz) = EventCellRaw(m, e);
            float ry = EventSurfaceY(m, rx, rz, e);
            var (a, b, c) = m.ToNormalized(rx, ry, rz);
            nx = a; ny = b; nz = c;
            return true;
        }

        /// <summary>All visible event anchors (type 0=ow,1=warp,2=trigger,3=spawnable) in normalized
        /// space, so the view can pick the nearest one under the cursor.</summary>
        public IEnumerable<(int type, int index, float nx, float ny, float nz)> EventAnchorsNorm()
        {
            if (_file == null || Model3D == null) yield break;
            if (_showOw) for (int i = 0; i < _file.overworlds.Count; i++) if (EventAnchorNorm(_file.overworlds[i], out var x, out var y, out var z)) yield return (0, i, x, y, z);
            if (_showWarp) for (int i = 0; i < _file.warps.Count; i++) if (EventAnchorNorm(_file.warps[i], out var x, out var y, out var z)) yield return (1, i, x, y, z);
            if (_showTrig) for (int i = 0; i < _file.triggers.Count; i++) if (EventAnchorNorm(_file.triggers[i], out var x, out var y, out var z)) yield return (2, i, x, y, z);
            if (_showSpawn) for (int i = 0; i < _file.spawnables.Count; i++) if (EventAnchorNorm(_file.spawnables[i], out var x, out var y, out var z)) yield return (3, i, x, y, z);
        }

        public void SelectEvent(int type, int index)
        {
            switch (type)
            {
                case 0: SelectedOverworldIndex = index; break;
                case 1: SelectedWarpIndex = index; break;
                case 2: SelectedTriggerIndex = index; break;
                case 3: SelectedSpawnableIndex = index; break;
            }
        }

        // Event positions are INTEGER tiles (no fraction field), so a mouse drag accumulates sub-tile
        // movement here and only steps the tile when it crosses a whole-tile boundary — exactly how the
        // building gizmo carries its fraction. Reset at the start of each drag via BeginGizmoDrag().
        private float _dragAccumX, _dragAccumZ;
        public void BeginGizmoDrag() { _dragAccumX = 0f; _dragAccumZ = 0f; }

        /// <summary>Moves the selected event by a raw-space delta along one world axis (0=X,1=Y,2=Z).
        /// X/Z step the in-map tile in whole-tile increments (carrying the remainder), rolling over into
        /// the neighbouring matrix cell at the map edge; Y edits the event height (zPosition).</summary>
        public void NudgeSelectedEventRaw(int axis, float rawDelta)
        {
            var m = Model3D;
            if (m == null || _current == null || rawDelta == 0f) return;
            if (axis == 1)
            {
                long nz = _current.zPosition + (long)Math.Round(rawDelta * 262144f);
                _current.zPosition = (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, nz));
            }
            else if (m.TryCellPlacement(_current.xMatrixPosition, _current.yMatrixPosition, out var p))
            {
                if (axis == 0)
                {
                    float per = p.Width / MapTiles; if (per <= 0) return;     // raw units per tile
                    _dragAccumX += rawDelta / per;
                    int step = (int)_dragAccumX;                              // whole tiles to move now
                    if (step != 0) { _dragAccumX -= step; StepEventTile(ref step, isX: true); }
                }
                else
                {
                    float per = p.Height / MapTiles; if (per <= 0) return;
                    _dragAccumZ += rawDelta / per;
                    int step = (int)_dragAccumZ;
                    if (step != 0) { _dragAccumZ -= step; StepEventTile(ref step, isX: false); }
                }
            }
            _suppress = true;
            XMap = _current.xMapPosition; YMap = _current.yMapPosition; ZPos = _current.zPosition;
            XMatrix = _current.xMatrixPosition; YMatrix = _current.yMatrixPosition;
            _suppress = false;
            Dirty();
            RefreshMarkers();   // also raises GizmoTargetChanged
        }

        /// <summary>Moves the selected event by whole tiles along X / Z (for arrow keys), rolling over
        /// into the neighbouring matrix cell at the map edges.</summary>
        public void NudgeSelectedEventTiles(int dx, int dz)
        {
            if (_current == null) return;
            if (dx != 0) { int s = dx; StepEventTile(ref s, isX: true); }
            if (dz != 0) { int s = dz; StepEventTile(ref s, isX: false); }
            _suppress = true;
            XMap = _current.xMapPosition; YMap = _current.yMapPosition;
            XMatrix = _current.xMatrixPosition; YMatrix = _current.yMatrixPosition;
            _suppress = false;
            Dirty();
            RefreshMarkers();
        }

        public bool HasSelectedEvent => _current != null;

        /// <summary>Adds <paramref name="step"/> tiles to the event's X (or Y) position, rolling whole
        /// maps over into the neighbouring matrix cell at the 0/31 edges.</summary>
        private void StepEventTile(ref int step, bool isX)
        {
            int tile = isX ? _current.xMapPosition : _current.yMapPosition;
            int mat = isX ? _current.xMatrixPosition : _current.yMatrixPosition;
            tile += step;
            while (tile < 0) { mat--; tile += MapTiles; }
            while (tile >= MapTiles) { mat++; tile -= MapTiles; }
            if (mat < 0) { mat = 0; tile = 0; }
            if (isX) { _current.xMapPosition = (short)tile; _current.xMatrixPosition = (ushort)mat; }
            else { _current.yMapPosition = (short)tile; _current.yMatrixPosition = (ushort)mat; }
        }

        private static void AddMarker(List<float> v, NsbmdRenderModel m, float cx, float cy, float cz,
            float halfX, float halfZ, (float r, float g, float b) col)
        {
            var a = m.ToNormalized(cx - halfX, cy, cz - halfZ);
            var b = m.ToNormalized(cx + halfX, cy, cz - halfZ);
            var c = m.ToNormalized(cx + halfX, cy, cz + halfZ);
            var d = m.ToNormalized(cx - halfX, cy, cz + halfZ);
            void Vtx((float x, float y, float z) p) { v.Add(p.x); v.Add(p.y); v.Add(p.z); v.Add(0); v.Add(0); v.Add(col.r); v.Add(col.g); v.Add(col.b); }
            Vtx(a); Vtx(b); Vtx(c);
            Vtx(a); Vtx(c); Vtx(d);
        }

        private static void AddFlatQuad(List<float> v, NsbmdRenderModel m, float x0, float z0, float x1, float z1, float y,
            (float r, float g, float b) col)
        {
            var a = m.ToNormalized(x0, y, z0);
            var b = m.ToNormalized(x1, y, z0);
            var c = m.ToNormalized(x1, y, z1);
            var d = m.ToNormalized(x0, y, z1);
            void Vtx((float x, float y, float z) p) { v.Add(p.x); v.Add(p.y); v.Add(p.z); v.Add(0); v.Add(0); v.Add(col.r); v.Add(col.g); v.Add(col.b); }
            Vtx(a); Vtx(b); Vtx(c);
            Vtx(a); Vtx(c); Vtx(d);
        }

        // ── Save / import / export ─────────────────────────────────────────────────────
        public void Save()
        {
            if (_file == null || _selectedIndex < 0) return;
            _file.SaveToFileDefaultDir(_selectedIndex, showSuccessMessage: false);
            SetClean();
            StatusText = $"Saved event file {_selectedIndex}.";
        }

        public async Task ImportAsync()
        {
            if (_selectedIndex < 0) return;
            var filter = new FilePickerFileType("Event file") { Patterns = new[] { "*.ev", "*.bin", "*.*" } };
            string path = await DialogHelper.OpenFile(_owner, "Import event file", new[] { filter });
            if (path == null) return;
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read)) _file = new EventFile(fs);
                RefreshLists(); Dirty(); RefreshMarkers();
                StatusText = "Imported event file (unsaved).";
            }
            catch (Exception ex) { await DialogHelper.ShowError($"Import failed:\n{ex.Message}", "Import Error"); }
        }

        /// <summary>Builds a complete text dump of the current 3D scene — matrix layout, per-cell map
        /// placements (origin/size + the gap/overlap to each neighbour), and every event's exact computed
        /// world position — so a render screenshot can be correlated against the numbers to find stitching/
        /// placement issues. Paired with a PNG of the live render by the view.</summary>
        public string BuildDebugReport()
        {
            var sb = new System.Text.StringBuilder();
            var m = Model3D;
            sb.AppendLine("=== DSPRE Event Editor 3D Debug Dump ===");
            sb.AppendLine($"Timestamp:   {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Event file:  {_selectedIndex}");
            sb.AppendLine($"Matrix id:   {_matrixId}    Area data: {_areaDataId}");
            sb.AppendLine($"Stitch mode: {(_stitchGrid ? "Grid" : "Continuous")} (both now use the DS-true fixed 32-tile grid)");
            if (_matrix != null) sb.AppendLine($"Matrix size: {_matrix.width} x {_matrix.height}");
            sb.AppendLine("Units: 1 matrix cell = 32 tiles; Grid stride = 8.0 raw units (0.25/tile).");
            sb.AppendLine();

            if (m == null) { sb.AppendLine("(No 3D model is currently built.)"); return sb.ToString(); }

            sb.AppendLine("--- Model bounds (raw space) ---");
            sb.AppendLine($"Scale={m.Scale:F4}  Center=({m.Cx:F3}, {m.Cy:F3}, {m.Cz:F3})");
            sb.AppendLine($"Raw bounds  X[{m.RawMinX:F3}, {m.RawMaxX:F3}]  Y[{m.RawMinY:F3}, {m.RawMaxY:F3}]  Z[{m.RawMinZ:F3}, {m.RawMaxZ:F3}]");
            sb.AppendLine($"Map  bounds X[{m.MapMinX:F3}, {m.MapMaxX:F3}]  Z[{m.MapMinZ:F3}, {m.MapMaxZ:F3}]");
            sb.AppendLine($"Representative CellStride X={m.CellStrideX:F3} Z={m.CellStrideZ:F3}");
            sb.AppendLine($"DefaultSurfaceY (fallback) = {m.DefaultSurfaceY:F3}");
            sb.AppendLine($"BDHC cells: {m.CellBdhc?.Count ?? 0}");
            sb.AppendLine();

            sb.AppendLine("--- Per-cell map placement (raw space) ---");
            sb.AppendLine("  cell    map | originX  originZ |  width  height | rightGap bottomGap   (gap<0 = OVERLAP)");
            if (_matrix != null && m.CellPlacements != null)
            {
                for (int cy = 0; cy < _matrix.height; cy++)
                    for (int cx = 0; cx < _matrix.width; cx++)
                    {
                        int map; try { map = _matrix.maps[cy, cx]; } catch { continue; }
                        if (map == GameMatrix.EMPTY) continue;
                        if (!m.TryCellPlacement(cx, cy, out var p)) { sb.AppendLine($"({cx,2},{cy,2}) {map,5} | (no placement in scene)"); continue; }
                        string rg = m.TryCellPlacement(cx + 1, cy, out var pr) ? (pr.OriginX - (p.OriginX + p.Width)).ToString("F3") : "  -";
                        string bg = m.TryCellPlacement(cx, cy + 1, out var pb) ? (pb.OriginZ - (p.OriginZ + p.Height)).ToString("F3") : "  -";
                        sb.AppendLine($"({cx,2},{cy,2}) {map,5} | {p.OriginX,7:F3} {p.OriginZ,7:F3} | {p.Width,6:F3} {p.Height,6:F3} | {rg,8} {bg,9}");
                    }
            }
            sb.AppendLine();

            sb.AppendLine("--- Events (exact computed world position) ---");
            sb.AppendLine("type        idx | matrix  map(x,y) | z(fixed) yHint(raw) |   rawX    rawZ  surfaceY source");
            if (_file != null)
            {
                void Dump(string type, System.Collections.IEnumerable list)
                {
                    int i = 0;
                    foreach (Event e in list)
                    {
                        var (rx, rz) = EventCellRaw(m, e);
                        float yHint = e.zPosition / 262144f;
                        bool bdhc = m.TryBdhcSurfaceY(e.xMatrixPosition, e.yMatrixPosition, rx, rz, yHint, out var sy);
                        if (!bdhc) sy = EventSurfaceY(m, rx, rz, e);
                        sb.AppendLine($"{type,-10} {i,3} | ({e.xMatrixPosition},{e.yMatrixPosition})  ({e.xMapPosition,2},{e.yMapPosition,2}) | {e.zPosition,8} {yHint,7:F3} | {rx,7:F3} {rz,7:F3} {sy,8:F3} {(bdhc ? "bdhc" : "mesh")}");
                        i++;
                    }
                }
                Dump("overworld", _file.overworlds);
                Dump("warp", _file.warps);
                Dump("trigger", _file.triggers);
                Dump("spawnable", _file.spawnables);
            }

            return sb.ToString();
        }

        public async Task ExportAsync()
        {
            if (_file == null) return;
            var filter = new FilePickerFileType("Event file") { Patterns = new[] { "*.ev" } };
            string path = await DialogHelper.SaveFile(_owner, "Export event file", new[] { filter }, $"event_{_selectedIndex:D4}.ev");
            if (path == null) return;
            try { System.IO.File.WriteAllBytes(path, _file.ToByteArray()); StatusText = "Exported."; }
            catch (Exception ex) { await DialogHelper.ShowError($"Export failed:\n{ex.Message}", "Export Error"); }
        }
    }
}

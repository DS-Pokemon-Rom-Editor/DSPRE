using DSPRE.Avalonia;
using DSPRE.Avalonia.Gl;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using global::Avalonia.Controls;
using global::Avalonia.Platform.Storage;
using LibNDSFormats.NSBMD;
using LibNDSFormats.NSBTX;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;

using DSPRE.Avalonia.Data;
using System.Text;
namespace DSPRE.Avalonia.ViewModels.World
{
    /// <summary>Edits an event file's overworlds, warps, triggers and spawnables on a 3D map.</summary>
    public partial class EventEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo, IDetachable
    {
        /// <summary>Walking, the preview and drag gizmos are unfinished, so beta only.</summary>
        public bool ShowBetaFeatures => BetaEditors.Enabled;


        /// <summary>The header's level script file, so the preview runs what the map runs.</summary>
        public int LevelScriptId { get; set; } = -1;

        private bool _isCatchAllHeader;

        public bool IsCatchAllHeader
        {
            get => _isCatchAllHeader;
            private set { if (Set(ref _isCatchAllHeader, value)) OnPropertyChanged(nameof(CatchAllNote)); }
        }

        public string CatchAllNote => FieldCatchAllHeader.Explanation;

        public int TextArchiveId { get; set; } = -1;

        /// <summary>The gaps the map's messages leave for words the game fills in.</summary>
        public System.Collections.Generic.List<FieldStringVar> GatherStringVars()
        {
            List<FieldStringVar> none = new System.Collections.Generic.List<FieldStringVar>();
            if (TextArchiveId < 0) return none;

            System.Collections.Generic.List<string> lines;
            try { lines = new TextArchive(TextArchiveId).Messages; }
            catch (Exception ex) { AppLogger.WarnOnce("EventEditorViewModel.GatherStringVars", ex); return none; }
            if (lines == null) return none;

            // Which scripts show which message. An unreadable script file leaves the messages unmatched.
            Dictionary<int, List<int>> showsMessage = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<int>>();
            try
            {
                MapHeader header = _headerId < 0 ? null : MapHeader.GetMapHeader((ushort)_headerId);
                if (header != null)
                {
                    ScriptFile sf = new ScriptFile(header.ScriptFileID);
                    for (int i = 0; i < (sf.AllScripts?.Count ?? 0); i++)
                    {
                        int scriptNumber = i + 1;          // scripts are counted from one
                        foreach (ScriptCommand cmd in sf.AllScripts[i].Commands ?? new System.Collections.Generic.List<ScriptCommand>())
                        {
                            // A command's name carries its parameters ("Message 0x6"), so match on the first word.
                            string full = cmd?.Name ?? "";
                            int space = full.IndexOf(' ');
                            string name = space > 0 ? full.Substring(0, space) : full;
                            if (name.IndexOf("Message", System.StringComparison.Ordinal) < 0) continue;
                            if (name == "CloseMessage" || name == "OpenMessage"
                             || name == "GetCommonMessageArchive" || name == "FreezeMessage") continue;
                            if (cmd.CmdParams == null || cmd.CmdParams.Count == 0) continue;
                            byte[] raw = cmd.CmdParams[0];
                            if (raw == null || raw.Length == 0) continue;
                            int id = raw.Length >= 2 ? raw[0] | (raw[1] << 8) : raw[0];
                            if (id < 0 || id >= lines.Count) continue;
                            if (!showsMessage.TryGetValue(id, out List<int> who)) showsMessage[id] = who = new System.Collections.Generic.List<int>();
                            if (!who.Contains(scriptNumber)) who.Add(scriptNumber);
                        }
                    }
                }
            }
            catch (Exception ex) { AppLogger.Warn("Event editor: script messages: " + ex.Message); }

            List<(int, string)> numbered = new System.Collections.Generic.List<(int, string)>(lines.Count);
            for (int i = 0; i < lines.Count; i++) numbered.Add((i, lines[i]));

            return FieldStringVars.Gather(numbered,
                id => showsMessage.TryGetValue(id, out List<int> who) ? who : (System.Collections.Generic.IEnumerable<int>)new int[0]);
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private Window _owner;
        private bool _suppress;
        private EventFile _file;

        private Dictionary<int, (ushort matrixId, byte areaId, ushort scriptFileId, ushort headerId)> _eventToHeader;
        private GameMatrix _matrix;
        private byte _areaDataId;
        private readonly Dictionary<long, byte[,]> _collisionCache = new Dictionary<long, byte[,]>();

        public event EventHandler MapLoaded;
        public event EventHandler MarkersChanged;

        public NsbmdRenderModel Model3D { get; private set; }

        public bool KeepViewOnLoad { get; private set; }

        private bool _showOw = true, _showWarp = true, _showTrig = true, _showSpawn = true, _showGrid;

        private bool _flat2D = SettingsManager.Settings?.EventEditorFlat2D ?? false;
        public bool Flat2D
        {
            get => _flat2D;
            set
            {
                if (!Set(ref _flat2D, value)) return;
                if (SettingsManager.Settings != null)
                {
                    SettingsManager.Settings.EventEditorFlat2D = value;
                    SettingsManager.Save();
                }
                ViewModeChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler ViewModeChanged;
        public bool ShowOverworlds { get => _showOw; set { if (Set(ref _showOw, value)) RefreshMarkers(); } }
        public bool ShowWarps { get => _showWarp; set { if (Set(ref _showWarp, value)) RefreshMarkers(); } }
        public bool ShowTriggers { get => _showTrig; set { if (Set(ref _showTrig, value)) RefreshMarkers(); } }
        public bool ShowSpawnables { get => _showSpawn; set { if (Set(ref _showSpawn, value)) RefreshMarkers(); } }
        public bool ShowGrid { get => _showGrid; set { if (Set(ref _showGrid, value)) RefreshMarkers(); } }

        private bool _showTileGrid = SettingsManager.Settings?.EventEditorTileGrid ?? false;
        public bool ShowTileGrid
        {
            get => _showTileGrid;
            set
            {
                if (!Set(ref _showTileGrid, value) || SettingsManager.Settings == null) return;
                SettingsManager.Settings.EventEditorTileGrid = value;
                SettingsManager.Save();
            }
        }
        /// <summary>The tile grid scans every tile on each refresh, so it's limited to 4 cells.</summary>
        public bool GridToggleEnabled => _matrix != null && _matrix.Width * _matrix.Height <= 4;

        private bool _stitchGrid = true;
        public bool StitchGrid { get => _stitchGrid; set { if (Set(ref _stitchGrid, value)) DisplayMap(); } }
        private NsbmdGeometry.MatrixStitchMode StitchMode => _stitchGrid ? NsbmdGeometry.MatrixStitchMode.Grid : NsbmdGeometry.MatrixStitchMode.Continuous;

        public EventEditorViewModel() { if (Design.IsDesignMode) EventNames.Add("Event 0"); }
        public EventEditorViewModel(bool _)
        {
            AppEvents.EventFileSaved += OnSavedElsewhere;
            AppEvents.MapSaved += OnMapSavedElsewhere;
            _spritesSaved = AppEvents.WhenSaved(() => { UpdateOwSpritePreview(); RefreshMarkers(); }, DirNames.OWSprites);
        }

        private readonly IDisposable _spritesSaved;

        private void OnMapSavedElsewhere(object sender, int mapIndex)
        {
            if (_matrix?.Maps == null || Model3D == null || !_matrix.Maps.Cast<ushort>().Contains((ushort)mapIndex)) return;
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                DisplayMap();
                StatusText = $"Map {mapIndex} was saved in another window and redrawn.";
            });
        }

        // Another open copy of this event file saved: show it, unless this copy holds its own edits.
        private void OnSavedElsewhere(object sender, int id)
        {
            if (ReferenceEquals(sender, this) || id != _selectedIndex) return;
            if (_dirty) { _ = OfferReloadAsync(id); return; }
            ReloadKeepingSelection(id);
            StatusText = "";
        }

        private void ReloadKeepingSelection(int id)
        {
            int ow = _selOw, warp = _selWarp, trig = _selTrig, spawn = _selSpawn;
            LoadFile(id);
            if (ow >= 0) SelectedOverworldIndex = ow < Overworlds.Count ? ow : -1;
            if (warp >= 0) SelectedWarpIndex = warp < Warps.Count ? warp : -1;
            if (trig >= 0) SelectedTriggerIndex = trig < Triggers.Count ? trig : -1;
            if (spawn >= 0) SelectedSpawnableIndex = spawn < Spawnables.Count ? spawn : -1;
        }

        // Keeping these edits means saving them over what the other window wrote, so ask.
        private async Task OfferReloadAsync(int id)
        {
            bool reload = await DialogHelper.AskYesNoAsync(
                $"Event file {id} was saved in another window.\n\nLoad it and drop the changes made here?", "Event file changed", _owner);
            if (id != _selectedIndex) return;
            if (!reload) { StatusText = $"Event file {id} was saved in another window. Saving here will overwrite it."; return; }
            LoadFile(id);
            StatusText = "";
        }

        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            _headerNames = null;   // per ROM
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> {
                    DirNames.EventFiles, DirNames.Maps, DirNames.ExteriorBuildingModels,
                    DirNames.BuildingTextures, DirNames.MapTextures, DirNames.Matrices,
                    DirNames.AreaData, DirNames.DynamicHeaders, DirNames.OWSprites,
                    DirNames.TrainerProperties, DirNames.Scripts, DirNames.TextArchives });
                if (GameFamily == GameFamilies.HGSS)
                    DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.InteriorBuildingModels });
                _eventToHeader = BuildEventHeaderLookup();
                // The 3D billboards need the overworld table. SetOWtable has to come before ReadOWTable.
                try { if (Ow3DSpriteDict == null) Set3DOverworldsDict(); } catch (Exception ex) { AppLogger.Error("Set3DOverworldsDict: " + ex.Message); }
                try { if (OverworldTable == null) { SetOWtable(); ReadOWTable(); } } catch (Exception ex) { AppLogger.Error("ReadOWTable: " + ex.Message); }
                OwSpriteEntries.Clear();
                if (OverworldTableKeys != null)
                    foreach (uint key in OverworldTableKeys) OwSpriteEntries.Add(OverworldLabels.Of(key));
                PopulateOwEventTypes();
                PopulateOwTrainerAndItemEntries();
                ListEventFiles();
                int count = EventNames.Count;
                StatusText = $"{count} event files.";
                if (count > 0) SelectedEventIndex = Math.Min(Math.Max(0, InitialIndex), count - 1);
                if (InitialOverworldIndex >= 0 && InitialOverworldIndex < _file?.Overworlds.Count)
                    SelectedOverworldIndex = InitialOverworldIndex;
                if (InitialSpawnableIndex >= 0 && InitialSpawnableIndex < _file?.Spawnables.Count)
                    SelectEvent(TabSpawnables, InitialSpawnableIndex);
            }
            catch (Exception ex) when (AppLogger.Noted("EventEditorViewModel.SetupAsync", ex))
            {
                StatusText = "Error: " + ex.Message;
                await DialogHelper.ShowErrorAsync($"Couldn't set up the Event editor:\n{ex.Message}", "Event editor");
            }
        }

        private void LoadFile(int index)
        {
            try
            {
                PopulateHiddenItemEntries();
                using (MemoryStream ms = new MemoryStream(_archive.Read(index) ?? throw new FileNotFoundException($"Event file {index} is missing.")))
                    _file = new EventFile(ms) { ID = index };
                ClearSelection();
                RefreshLists();
                ResolveMatrixForFile(index);
                SetClean();
                StartUndo();
                StatusText = "";
                OnPropertyChanged(nameof(UnsavedChangesDescription));
            }
            catch (Exception ex) when (AppLogger.Noted("EventEditorViewModel.LoadFile", ex)) { _ = DialogHelper.ShowErrorAsync($"Couldn't load event file {index}:\n{ex.Message}", "Event editor"); }
        }

        private void RefreshLists()
        {
            _suppress = true;
            Spawnables.Clear(); Overworlds.Clear(); Warps.Clear(); Triggers.Clear();
            if (_file != null)
            {
                for (int i = 0; i < _file.Spawnables.Count; i++) Spawnables.Add($"Spawnable {i:D2}");
                for (int i = 0; i < _file.Overworlds.Count; i++) Overworlds.Add($"Overworld {i:D2}");
                for (int i = 0; i < _file.Warps.Count; i++) Warps.Add($"Warp {i:D2}");
                for (int i = 0; i < _file.Triggers.Count; i++) Triggers.Add($"Trigger {i:D2}");
            }
            _suppress = false;
        }

        private void LoadPosition(Event e)
        {
            _current = e;
            if (e == null) return;
            XMap = e.XMapPosition; YMap = e.YMapPosition; ZPos = ToDisplayZ(e.ZPosition);
            OnPropertyChanged(nameof(ZPosNote));
            XMatrix = e.XMatrixPosition; YMatrix = e.YMatrixPosition;
        }

        private void LoadSpawnable(int i)
        {
            _spawn = (_file != null && i >= 0 && i < _file.Spawnables.Count) ? _file.Spawnables[i] : null;
            OnPropertyChanged(nameof(HasSpawn));
            if (_spawn == null) { if (_current is Spawnable) _current = null; return; }
            _suppress = true;
            LoadPosition(_spawn);
            SpScript = _spawn.ScriptNumber; SpType = _spawn.Type; SpDir = _spawn.Dir;
            _suppress = false;
            OnPropertyChanged(nameof(SpScriptIndex)); OnPropertyChanged(nameof(SpScriptIndexOutOfRange)); OnPropertyChanged(nameof(SpScriptCommonInfo)); OnPropertyChanged(nameof(SpScriptHasCommonInfo)); OnPropertyChanged(nameof(SpScriptCommonName)); OnPropertyChanged(nameof(SpScriptGenericWarningVisible)); RaiseSpHidden();
            RefreshMarkers();
        }

        private void LoadOverworld(int i)
        {
            _ow = (_file != null && i >= 0 && i < _file.Overworlds.Count) ? _file.Overworlds[i] : null;
            OnPropertyChanged(nameof(HasOw));
            if (_ow == null) { if (_current is Overworld) _current = null; UpdateOwSpritePreview(); return; }
            _suppress = true;
            LoadPosition(_ow);
            OwId = _ow.OwID; OwSprite = _ow.OverlayTableEntry; OwMovement = _ow.Movement; OwType = _ow.Type;
            OwFlag = _ow.Flag; OwScript = _ow.ScriptNumber; OwOrientation = _ow.Orientation; OwSight = _ow.SightRange;
            OwXRange = _ow.XRange; OwYRange = _ow.YRange;
            OnPropertyChanged(nameof(OwSpriteIndex)); OnPropertyChanged(nameof(OwMovementIndex));
            OnPropertyChanged(nameof(OwMovementUnknown)); OnPropertyChanged(nameof(OwMovementUnknownNote)); OnPropertyChanged(nameof(OwOrientationIndex));
            OnPropertyChanged(nameof(OwScriptIndex)); OnPropertyChanged(nameof(OwScriptIndexOutOfRange));

            // Trainer scripts are 2999 + index (partners 4999 +), expanded rosters too.
            _owRawType = _ow.Type;
            _owNormalScript = null;
            if (KindOfType(_ow.Type) == OwKind.Trainer)
            {
                _owKind = OwKind.Trainer;
                bool partner = TrainerScripts.IsDouble(_ow.ScriptNumber);
                int idx = TrainerScripts.TrainerIdFor(_ow.ScriptNumber) ?? -1;
                // Past the roster stays unselected so it isn't clamped to the wrong trainer.
                _owTrainerIndex = (idx >= 0 && idx < OwTrainerEntries.Count) ? idx : -1;
                _owPartnerTrainer = partner;
            }
            else if (_ow.Type == (ushort)Overworld.OwType.ITEM
                     || (_ow.ScriptNumber >= GroundItemScriptsLogic.ItemScrMin && _ow.ScriptNumber < GroundItemScriptsLogic.ItemScrMax))
            {
                _owKind = OwKind.Item;
                int itemIdx = _ow.ScriptNumber - GroundItemScriptsLogic.ItemScrMin;
                _owItemIndex = (itemIdx >= 0 && itemIdx < OwItemEntries.Count) ? itemIdx : -1;
            }
            else
            {
                _owKind = OwKind.Normal;
            }
            RaiseOwKindChanged();
            OnPropertyChanged(nameof(OwTrainerIndex)); OnPropertyChanged(nameof(OwPartnerTrainer)); OnPropertyChanged(nameof(OwItemIndex));
            OnPropertyChanged(nameof(OwTrainerIndexOutOfRange)); OnPropertyChanged(nameof(OwTrainerIsDummy)); OnPropertyChanged(nameof(OwTrainerHasNoScript)); OnPropertyChanged(nameof(OwTrainerNoScriptNote)); OnPropertyChanged(nameof(OwItemIndexOutOfRange)); OnPropertyChanged(nameof(OwScriptEnabled));
            RaiseOwTypeInfoChanged(); OnPropertyChanged(nameof(OwParam1)); OnPropertyChanged(nameof(OwParam2));

            _suppress = false;
            RefreshMarkers();
            UpdateOwSpritePreview();
        }

        private void LoadWarp(int i)
        {
            _warp = (_file != null && i >= 0 && i < _file.Warps.Count) ? _file.Warps[i] : null;
            OnPropertyChanged(nameof(HasWarp));
            if (_warp == null) { if (_current is Warp) _current = null; return; }
            _suppress = true;
            LoadPosition(_warp);
            WarpHeader = _warp.Header; WarpAnchor = _warp.Anchor; WarpHeight = _warp.Height;
            _suppress = false;
            RefreshMarkers();
        }

        private void LoadTrigger(int i)
        {
            _trig = (_file != null && i >= 0 && i < _file.Triggers.Count) ? _file.Triggers[i] : null;
            OnPropertyChanged(nameof(HasTrig));
            if (_trig == null) { if (_current is Trigger) _current = null; return; }
            _suppress = true;
            LoadPosition(_trig);
            TrScript = _trig.ScriptNumber; TrWidth = _trig.WidthX; TrHeight = _trig.HeightY;
            TrVarValue = _trig.ExpectedVarValue; TrVar = _trig.VariableWatched;
            _suppress = false;
            OnPropertyChanged(nameof(TrScriptIndex)); OnPropertyChanged(nameof(TrScriptIndexOutOfRange)); OnPropertyChanged(nameof(TrScriptCommonInfo)); OnPropertyChanged(nameof(TrScriptHasCommonInfo)); OnPropertyChanged(nameof(TrScriptCommonName)); OnPropertyChanged(nameof(TrScriptGenericWarningVisible));
            RefreshMarkers();
        }
    }

    public sealed class EventImportChoice
    {
        public bool ClearFirst;
        public int[] Overworlds, Warps, Triggers, Spawnables;

        public static EventImportChoice All(EventFile f, bool clearFirst) => new EventImportChoice
        {
            ClearFirst = clearFirst,
            Overworlds = Enumerable.Range(0, f.Overworlds?.Count ?? 0).ToArray(),
            Warps = Enumerable.Range(0, f.Warps?.Count ?? 0).ToArray(),
            Triggers = Enumerable.Range(0, f.Triggers?.Count ?? 0).ToArray(),
            Spawnables = Enumerable.Range(0, f.Spawnables?.Count ?? 0).ToArray(),
        };
    }
}

using Avalonia.Controls;
using DSPRE.Avalonia;
using DSPRE.Editors.Utils;
using DSPRE.Resources;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    // ── Result / data types ───────────────────────────────────────────────────

    public class ScriptFileStats
    {
        public int ID { get; set; }
        public int Total { get; set; }
        public int Scripts { get; set; }
        public int Functions { get; set; }
        public int Actions { get; set; }
    }

    public class LevelScriptFileStats
    {
        public int ID { get; set; }
        public int Total { get; set; }
        public int MapChange { get; set; }
        public int ScreenReset { get; set; }
        public int LoadGame { get; set; }
        public int VariableValue { get; set; }
    }

    public class VariableUsageResult
    {
        public string FileType { get; set; }
        public int FileID { get; set; }
        public int UsageCount { get; set; }
    }

    public class FlagUsageResult
    {
        public string FileType { get; set; }
        public int FileID { get; set; }
        public string Details { get; set; }
        public int UsageCount { get; set; }
        public int EventIndex { get; set; }
    }

    public class ScriptFileReferenceResult
    {
        public string ReferenceType { get; set; }
        public int ReferenceID { get; set; }
        public string Field { get; set; }
    }

    public class ScriptIdUsageResult
    {
        public int EventFileID { get; set; }
        public string EventType { get; set; }
        public int EventIndex { get; set; }
        public string Details { get; set; }
    }

    public class HeaderWarpResult
    {
        public int EventFileID { get; set; }
        public int WarpIndex { get; set; }
        public string Position { get; set; }
        public int Anchor { get; set; }
    }

    public class HeaderOutgoingWarpResult
    {
        public int WarpIndex { get; set; }
        public string Position { get; set; }
        public int DestHeader { get; set; }
        public int DestAnchor { get; set; }
    }

    public class HeaderProperty
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class OwEntryUsageResult
    {
        public int EventFileID { get; set; }
        public int OwIndex { get; set; }
        public int OwID { get; set; }
        public string Position { get; set; }
        public int Movement { get; set; }
        public int ScriptNumber { get; set; }
    }

    public class TrainerUsageResult
    {
        /// <summary>Event file ID, script file ID, or -1 for Vs. Seeker rows.</summary>
        public int SourceId { get; set; }
        public string Type { get; set; }
        public int Index { get; set; }
        public string Details { get; set; }
    }

    // ── Main ViewModel ────────────────────────────────────────────────────────

    public class ResearchHelperViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        // ── Status / loading ──────────────────────────────────────────────────
        private string _statusText = "Ready";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; private set => Set(ref _isLoading, value); }

        private bool _dataLoaded;
        public bool DataLoaded { get => _dataLoaded; private set => Set(ref _dataLoaded, value); }

        // ── Tab 1: Scripts overview ───────────────────────────────────────────
        public ObservableCollection<ScriptFileStats> ScriptStats { get; } = new();

        // ── Tab 2: Level Scripts overview ─────────────────────────────────────
        public ObservableCollection<LevelScriptFileStats> LevelScriptStats { get; } = new();

        // ── Tab 3: Variable Watcher ───────────────────────────────────────────
        private string _varSearchText = "";
        public string VarSearchText { get => _varSearchText; set => Set(ref _varSearchText, value); }

        private bool _varHexMode;
        public bool VarHexMode { get => _varHexMode; set => Set(ref _varHexMode, value); }

        public ObservableCollection<VariableUsageResult> VariableResults { get; } = new();

        // ── Tab 4: Flag Watcher ───────────────────────────────────────────────
        private string _flagSearchText = "";
        public string FlagSearchText { get => _flagSearchText; set => Set(ref _flagSearchText, value); }

        private bool _flagHexMode;
        public bool FlagHexMode { get => _flagHexMode; set => Set(ref _flagHexMode, value); }

        public ObservableCollection<FlagUsageResult> FlagResults { get; } = new();

        // ── Tab 5a: File Watcher (which headers use a script file) ────────────
        private int _fileWatcherScriptFileId;
        public int FileWatcherScriptFileId { get => _fileWatcherScriptFileId; set => Set(ref _fileWatcherScriptFileId, value); }

        public ObservableCollection<ScriptFileReferenceResult> FileWatcherResults { get; } = new();

        // ── Tab 5b: ID Watcher (where a specific script ID is used) ──────────
        public ObservableCollection<string> ScriptFileEntries { get; } = new();

        private int _selectedScriptFileIndex = -1;
        public int SelectedScriptFileIndex
        {
            get => _selectedScriptFileIndex;
            set { if (Set(ref _selectedScriptFileIndex, value)) RefreshScriptIdEntries(); }
        }

        public ObservableCollection<string> ScriptIdEntries { get; } = new();

        private int _selectedScriptIdIndex = -1;
        public int SelectedScriptIdIndex { get => _selectedScriptIdIndex; set => Set(ref _selectedScriptIdIndex, value); }

        public ObservableCollection<ScriptIdUsageResult> ScriptIdResults { get; } = new();

        // ── Tab: Overworld Watcher ──────────────────────────────────────────────
        private int _owEntryIdSearch;
        public int OwEntryIdSearch { get => _owEntryIdSearch; set => Set(ref _owEntryIdSearch, value); }

        public ObservableCollection<OwEntryUsageResult> OwWatcherResults { get; } = new();

        // ── Tab: Trainer Watcher ────────────────────────────────────────────────
        public ObservableCollection<string> TrainerNamesList { get; } = new();

        private int _selectedTrainerIndex = -1;
        public int SelectedTrainerIndex { get => _selectedTrainerIndex; set => Set(ref _selectedTrainerIndex, value); }

        public ObservableCollection<TrainerUsageResult> TrainerWatcherResults { get; } = new();

        // ── Tab 6: Header Watcher ─────────────────────────────────────────────
        private int _headerSearchId;
        public int HeaderSearchId { get => _headerSearchId; set => Set(ref _headerSearchId, value); }

        public ObservableCollection<HeaderProperty> HeaderProperties { get; } = new();
        public ObservableCollection<HeaderWarpResult> IncomingWarps { get; } = new();
        public ObservableCollection<HeaderOutgoingWarpResult> OutgoingWarps { get; } = new();

        // ── Private caches ────────────────────────────────────────────────────
        private List<ScriptFile> _cachedScriptFiles = new();
        private List<LevelScriptFile> _cachedLevelScriptFiles = new();
        private List<EventFile> _cachedEventFiles = new();
        private Dictionary<int, ScriptFile> _scriptFileById = new();

        // ── Design-time constructor ───────────────────────────────────────────
        public ResearchHelperViewModel()
        {
            if (!Design.IsDesignMode) return;
            for (int i = 0; i < 5; i++)
                ScriptStats.Add(new ScriptFileStats { ID = i, Total = 10, Scripts = 5, Functions = 3, Actions = 2 });
            for (int i = 0; i < 3; i++)
                LevelScriptStats.Add(new LevelScriptFileStats { ID = i, Total = 4, MapChange = 1, ScreenReset = 1, LoadGame = 1, VariableValue = 1 });
            StatusText = "Design preview";
            _dataLoaded = true;
        }

        // ── Runtime constructor ───────────────────────────────────────────────
        public ResearchHelperViewModel(bool _) { }

        // ── Data loading ──────────────────────────────────────────────────────
        public async Task LoadAllDataAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            DataLoaded = false;
            StatusText = "Loading data...";

            ScriptStats.Clear();
            LevelScriptStats.Clear();
            _cachedScriptFiles.Clear();
            _cachedLevelScriptFiles.Clear();
            _cachedEventFiles.Clear();
            _scriptFileById.Clear();

            try
            {
                var (scripts, levelScripts, events) = await Task.Run(() =>
                {
                    var sc = new List<ScriptFile>();
                    var ls = new List<LevelScriptFile>();
                    var ev = new List<EventFile>();

                    int scriptCount = Filesystem.GetScriptCount();
                    for (int i = 0; i < scriptCount; i++)
                    {
                        try
                        {
                            var sf = new ScriptFile(i, readFunctions: true, readActions: true);
                            if (sf.isLevelScript)
                            {
                                try { ls.Add(new LevelScriptFile(i)); } catch { }
                            }
                            else
                            {
                                sc.Add(sf);
                            }
                        }
                        catch { }
                    }

                    int eventCount = Filesystem.GetEventFileCount();
                    for (int i = 0; i < eventCount; i++)
                    {
                        try { ev.Add(new EventFile(i)); } catch { }
                    }

                    return (sc, ls, ev);
                });

                _cachedScriptFiles = scripts;
                _cachedLevelScriptFiles = levelScripts;
                _cachedEventFiles = events;

                // Build stats
                foreach (var sf in _cachedScriptFiles)
                {
                    int total = (sf.allScripts?.Count ?? 0) + (sf.allFunctions?.Count ?? 0) + (sf.allActions?.Count ?? 0);
                    ScriptStats.Add(new ScriptFileStats
                    {
                        ID = sf.fileID,
                        Total = total,
                        Scripts = sf.allScripts?.Count ?? 0,
                        Functions = sf.allFunctions?.Count ?? 0,
                        Actions = sf.allActions?.Count ?? 0
                    });
                    _scriptFileById[sf.fileID] = sf;
                }

                foreach (var ls in _cachedLevelScriptFiles)
                {
                    int mapChange = 0, screenReset = 0, loadGame = 0, varValue = 0;
                    if (ls.bufferSet != null)
                    {
                        foreach (var t in ls.bufferSet)
                        {
                            switch (t.triggerType)
                            {
                                case LevelScriptTrigger.MAPCHANGE:   mapChange++;  break;
                                case LevelScriptTrigger.SCREENRESET: screenReset++; break;
                                case LevelScriptTrigger.LOADGAME:    loadGame++;   break;
                                case LevelScriptTrigger.VARIABLEVALUE: varValue++; break;
                            }
                        }
                    }
                    LevelScriptStats.Add(new LevelScriptFileStats
                    {
                        ID = ls.ID,
                        Total = ls.bufferSet?.Count ?? 0,
                        MapChange = mapChange,
                        ScreenReset = screenReset,
                        LoadGame = loadGame,
                        VariableValue = varValue
                    });
                }

                // Populate script file dropdown for ID watcher
                ScriptFileEntries.Clear();
                foreach (var sf in _cachedScriptFiles)
                    ScriptFileEntries.Add($"{sf.fileID}: Script File");

                if (ScriptFileEntries.Count > 0)
                    SelectedScriptFileIndex = 0;

                TrainerNamesList.Clear();
                foreach (var name in DSPRE.TrainerNames.GetAll()) TrainerNamesList.Add(name);
                if (TrainerNamesList.Count > 0) SelectedTrainerIndex = 0;

                DataLoaded = true;
                StatusText = $"Loaded {_cachedScriptFiles.Count} script files, {_cachedLevelScriptFiles.Count} level scripts, {_cachedEventFiles.Count} event files";
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading data: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ── Variable search ───────────────────────────────────────────────────
        public void SearchVariableUsage()
        {
            if (!DataLoaded) { StatusText = "Data not loaded yet."; return; }

            string text = VarSearchText.Trim();
            if (string.IsNullOrEmpty(text)) { StatusText = "Enter a variable number."; return; }

            if (!TryParseNumber(text, VarHexMode, out int varNum))
            {
                StatusText = "Invalid number format.";
                return;
            }

            VariableResults.Clear();
            StatusText = $"Searching for variable 0x{varNum:X}...";

            var commandInfoDict = RomInfo.GetScriptCommandInfoDict();

            foreach (var sf in _cachedScriptFiles)
            {
                int count = CountVariableInScriptFile(sf, varNum, commandInfoDict);
                if (count > 0) VariableResults.Add(new VariableUsageResult { FileType = "Script", FileID = sf.fileID, UsageCount = count });
            }

            foreach (var ls in _cachedLevelScriptFiles)
            {
                int count = CountVariableInLevelScript(ls, varNum);
                if (count > 0) VariableResults.Add(new VariableUsageResult { FileType = "Level Script", FileID = ls.ID, UsageCount = count });
            }

            foreach (var ev in _cachedEventFiles)
            {
                int count = CountVariableInEventFile(ev, varNum);
                if (count > 0) VariableResults.Add(new VariableUsageResult { FileType = "Event", FileID = ev.ID, UsageCount = count });
            }

            StatusText = $"Found {VariableResults.Count} files using variable 0x{varNum:X}";
        }

        public void ClearVariableResults() { VariableResults.Clear(); VarSearchText = ""; StatusText = "Variable search cleared"; }

        // ── Flag search ───────────────────────────────────────────────────────
        public void SearchFlagUsage()
        {
            if (!DataLoaded) { StatusText = "Data not loaded yet."; return; }

            string text = FlagSearchText.Trim();
            if (string.IsNullOrEmpty(text)) { StatusText = "Enter a flag number."; return; }

            if (!TryParseNumber(text, FlagHexMode, out int flagNum))
            {
                StatusText = "Invalid number format.";
                return;
            }

            FlagResults.Clear();
            StatusText = $"Searching for flag 0x{flagNum:X}...";

            foreach (var ev in _cachedEventFiles)
            {
                if (ev.overworlds == null) continue;
                for (int i = 0; i < ev.overworlds.Count; i++)
                {
                    var ow = ev.overworlds[i];
                    if (ow.flag == flagNum)
                        FlagResults.Add(new FlagUsageResult
                        {
                            FileType = "Event",
                            FileID = ev.ID,
                            Details = $"Overworld {i}: {ow}",
                            UsageCount = 1,
                            EventIndex = i
                        });
                }
            }

            foreach (var sf in _cachedScriptFiles)
            {
                int count = CountFlagInScriptFile(sf, flagNum);
                if (count > 0)
                    FlagResults.Add(new FlagUsageResult
                    {
                        FileType = "Script",
                        FileID = sf.fileID,
                        Details = $"{count} flag operation(s)",
                        UsageCount = count,
                        EventIndex = -1
                    });
            }

            StatusText = $"Found {FlagResults.Count} results for flag 0x{flagNum:X}";
        }

        public void ClearFlagResults() { FlagResults.Clear(); FlagSearchText = ""; StatusText = "Flag search cleared"; }

        // ── Script File Watcher ───────────────────────────────────────────────
        public void SearchScriptFileReferences()
        {
            int id = FileWatcherScriptFileId;
            FileWatcherResults.Clear();
            StatusText = $"Searching for references to script file {id}...";

            int headerCount = RomInfo.GetHeaderCount();
            for (ushort i = 0; i < headerCount; i++)
            {
                try
                {
                    var header = MapHeader.GetMapHeader(i);
                    if (header == null) continue;
                    if (header.scriptFileID == id)
                        FileWatcherResults.Add(new ScriptFileReferenceResult { ReferenceType = "Header", ReferenceID = i, Field = "scriptFileID" });
                    if (header.levelScriptID == id)
                        FileWatcherResults.Add(new ScriptFileReferenceResult { ReferenceType = "Header", ReferenceID = i, Field = "levelScriptID" });
                }
                catch { }
            }

            StatusText = $"Found {FileWatcherResults.Count} references to script file {id}";
        }

        // ── Script ID Watcher ─────────────────────────────────────────────────
        private void RefreshScriptIdEntries()
        {
            ScriptIdEntries.Clear();
            SelectedScriptIdIndex = -1;

            int idx = SelectedScriptFileIndex;
            if (idx < 0 || idx >= _cachedScriptFiles.Count) return;

            var sf = _cachedScriptFiles[idx];
            int count = sf.allScripts?.Count ?? 0;
            for (int i = 0; i < count; i++)
                ScriptIdEntries.Add($"Script {i + 1}");

            if (ScriptIdEntries.Count > 0) SelectedScriptIdIndex = 0;
        }

        public void SearchScriptIdUsage()
        {
            if (SelectedScriptFileIndex < 0 || SelectedScriptIdIndex < 0)
            { StatusText = "Select a script file and script ID."; return; }

            var sf = _cachedScriptFiles[SelectedScriptFileIndex];
            int scriptId = SelectedScriptIdIndex + 1; // 1-based

            ScriptIdResults.Clear();
            StatusText = $"Searching for Script {scriptId} in files associated with script file {sf.fileID}...";

            // Find event files linked to headers that use this script file
            var assocEventIds = new HashSet<int>();
            int headerCount = RomInfo.GetHeaderCount();
            for (ushort i = 0; i < headerCount; i++)
            {
                try
                {
                    var h = MapHeader.GetMapHeader(i);
                    if (h != null && h.scriptFileID == sf.fileID)
                        assocEventIds.Add(h.eventFileID);
                }
                catch { }
            }

            foreach (var ev in _cachedEventFiles)
            {
                if (!assocEventIds.Contains(ev.ID)) continue;

                if (ev.overworlds != null)
                    for (int i = 0; i < ev.overworlds.Count; i++)
                    {
                        var ow = ev.overworlds[i];
                        if (ow.scriptNumber == scriptId)
                            ScriptIdResults.Add(new ScriptIdUsageResult { EventFileID = ev.ID, EventType = "Overworld", EventIndex = i, Details = ow.ToString() });
                    }

                if (ev.spawnables != null)
                    for (int i = 0; i < ev.spawnables.Count; i++)
                    {
                        var sp = ev.spawnables[i];
                        if (sp.scriptNumber == scriptId)
                            ScriptIdResults.Add(new ScriptIdUsageResult { EventFileID = ev.ID, EventType = "Spawnable", EventIndex = i, Details = sp.ToString() });
                    }

                if (ev.triggers != null)
                    for (int i = 0; i < ev.triggers.Count; i++)
                    {
                        var tr = ev.triggers[i];
                        if (tr.scriptNumber == scriptId)
                            ScriptIdResults.Add(new ScriptIdUsageResult { EventFileID = ev.ID, EventType = "Trigger", EventIndex = i, Details = tr.ToString() });
                    }
            }

            StatusText = $"Found {ScriptIdResults.Count} uses of Script {scriptId}";
        }

        // ── Overworld Watcher ────────────────────────────────────────────────────
        public void SearchOverworldEntryUsage()
        {
            if (!DataLoaded) { StatusText = "Data not loaded yet."; return; }

            int owEntryId = OwEntryIdSearch;
            OwWatcherResults.Clear();
            StatusText = $"Searching for OW Entry ID {owEntryId}...";

            foreach (var eventFile in _cachedEventFiles)
            {
                if (eventFile.overworlds == null) continue;

                for (int i = 0; i < eventFile.overworlds.Count; i++)
                {
                    var ow = eventFile.overworlds[i];
                    if (ow.overlayTableEntry != owEntryId) continue;

                    OwWatcherResults.Add(new OwEntryUsageResult
                    {
                        EventFileID = eventFile.ID,
                        OwIndex = i,
                        OwID = ow.owID,
                        Position = $"Map ({ow.xMapPosition}, {ow.yMapPosition}) / Matrix ({ow.xMatrixPosition}, {ow.yMatrixPosition})",
                        Movement = ow.movement,
                        ScriptNumber = ow.scriptNumber
                    });
                }
            }

            StatusText = $"Found {OwWatcherResults.Count} overworld event(s) using OW Entry ID {owEntryId}";
        }

        public void ClearOwResults() { OwWatcherResults.Clear(); OwEntryIdSearch = 0; StatusText = "Overworld Watcher search cleared"; }

        public void NavigateToOwResult(OwEntryUsageResult result)
        {
            if (result == null) return;
            AvaloniaEditorLauncher.OpenEventEditorWithOverworld(result.EventFileID, result.OwIndex);
            StatusText = $"Opened Event File {result.EventFileID}, Overworld {result.OwIndex}";
        }

        // ── Trainer Watcher ──────────────────────────────────────────────────────
        public void SearchTrainerUsage()
        {
            if (!DataLoaded) { StatusText = "Data not loaded yet."; return; }
            if (SelectedTrainerIndex < 0) { StatusText = "Select a trainer."; return; }

            int trainerId = SelectedTrainerIndex;
            TrainerWatcherResults.Clear();
            StatusText = $"Searching for Trainer {trainerId} usage...";

            var commandInfoDict = RomInfo.GetScriptCommandInfoDict();
            foreach (var scriptFile in _cachedScriptFiles)
            {
                ScanContainersForTrainerParameter(scriptFile.allScripts, commandInfoDict, trainerId, scriptFile.fileID, "Script Command");
                ScanContainersForTrainerParameter(scriptFile.allFunctions, commandInfoDict, trainerId, scriptFile.fileID, "Function Command");
            }

            // Mirrors EventEditor.NavigateToOverworldTarget's decode formula.
            foreach (var eventFile in _cachedEventFiles)
            {
                if (eventFile.overworlds == null) continue;

                for (int i = 0; i < eventFile.overworlds.Count; i++)
                {
                    var ow = eventFile.overworlds[i];
                    if (ow.type != (ushort)Overworld.OwType.TRAINER) continue;

                    bool isPartner = ow.scriptNumber >= 4999;
                    int decodedId = ow.scriptNumber - (isPartner ? 4999 : 2999);
                    if (decodedId > RomInfo.trainerFunnyScriptNumber - 1) decodedId--;

                    if (decodedId != trainerId) continue;

                    TrainerWatcherResults.Add(new TrainerUsageResult
                    {
                        SourceId = eventFile.ID,
                        Type = isPartner ? "Overworld (Partner)" : "Overworld",
                        Index = i,
                        Details = ow.ToString()
                    });
                }
            }

            if (VsSeekerRematchTable.IsSupported)
            {
                var rows = VsSeekerRematchTable.ReadAll();
                for (int r = 0; r < rows.Count; r++)
                {
                    var row = rows[r];
                    if (row.EncounterTrainerId == trainerId)
                    {
                        TrainerWatcherResults.Add(new TrainerUsageResult
                        {
                            SourceId = -1,
                            Type = "Vs. Seeker Encounter",
                            Index = r,
                            Details = $"Row {r}: owns this rematch chain"
                        });
                    }
                    for (int s = 0; s < row.RematchTrainerIds.Length; s++)
                    {
                        if (row.RematchTrainerIds[s] != trainerId) continue;

                        TrainerWatcherResults.Add(new TrainerUsageResult
                        {
                            SourceId = -1,
                            Type = $"Vs. Seeker Rematch {(char)('A' + s)}",
                            Index = r,
                            Details = $"Row {r}: rematch {(char)('A' + s)} for encounter trainer {row.EncounterTrainerId}"
                        });
                    }
                }
            }

            StatusText = $"Found {TrainerWatcherResults.Count} use(s) of Trainer {trainerId}";
        }

        public void ClearTrainerResults() { TrainerWatcherResults.Clear(); StatusText = "Trainer Watcher search cleared"; }

        private void ScanContainersForTrainerParameter(List<ScriptCommandContainer> containers,
            Dictionary<ushort, ScriptCommandInfo> commandInfoDict, int trainerId, int sourceFileId, string typeLabel)
        {
            if (containers == null) return;

            foreach (var container in containers)
            {
                if (container.commands == null) continue;

                foreach (var cmd in container.commands)
                {
                    if (cmd.id == null || cmd.cmdParams == null) continue;
                    if (!commandInfoDict.TryGetValue(cmd.id.Value, out ScriptCommandInfo cmdInfo) || cmdInfo.ParameterTypes == null) continue;

                    for (int i = 0; i < cmd.cmdParams.Count && i < cmdInfo.ParameterTypes.Count; i++)
                    {
                        if (cmdInfo.ParameterTypes[i] != ScriptParameter.ParameterType.Trainer) continue;
                        if (GetParamValue(cmd.cmdParams[i]) != trainerId) continue;

                        TrainerWatcherResults.Add(new TrainerUsageResult
                        {
                            SourceId = sourceFileId,
                            Type = typeLabel,
                            Index = (int)container.manualUserID,
                            Details = $"{cmdInfo.Name} (param {i})"
                        });
                    }
                }
            }
        }

        public void NavigateToTrainerResult(TrainerUsageResult result)
        {
            if (result == null) return;

            if (result.Type.StartsWith("Vs. Seeker"))
            {
                AvaloniaEditorLauncher.OpenVsSeekerRematchEditor(result.Index);
                StatusText = "Opened Vs. Seeker Rematch Editor";
                return;
            }

            if (result.Type.StartsWith("Overworld"))
            {
                AvaloniaEditorLauncher.OpenEventEditorWithOverworld(result.SourceId, result.Index);
                StatusText = $"Opened Event File {result.SourceId}, Overworld {result.Index}";
            }
            else if (result.Type == "Script Command" || result.Type == "Function Command")
            {
                AvaloniaEditorLauncher.OpenScriptEditor(result.SourceId);
                StatusText = $"Opened Script File {result.SourceId}";
            }
        }

        // ── Header Watcher ────────────────────────────────────────────────────
        public void SearchHeaderInfo()
        {
            int id = HeaderSearchId;
            int headerCount = RomInfo.GetHeaderCount();

            if (id < 0 || id >= headerCount)
            { StatusText = $"Header ID must be 0–{headerCount - 1}."; return; }

            try
            {
                var header = MapHeader.GetMapHeader((ushort)id);
                if (header == null) { StatusText = $"Could not load header {id}."; return; }

                HeaderProperties.Clear();
                HeaderProperties.Add(new HeaderProperty { Name = "Header ID",          Value = header.ID.ToString() });
                HeaderProperties.Add(new HeaderProperty { Name = "Script File ID",      Value = header.scriptFileID.ToString() });
                HeaderProperties.Add(new HeaderProperty { Name = "Level Script ID",     Value = header.levelScriptID.ToString() });
                HeaderProperties.Add(new HeaderProperty { Name = "Event File ID",       Value = header.eventFileID.ToString() });
                HeaderProperties.Add(new HeaderProperty { Name = "Text Archive ID",     Value = header.textArchiveID.ToString() });
                HeaderProperties.Add(new HeaderProperty { Name = "Matrix ID",           Value = header.matrixID.ToString() });
                HeaderProperties.Add(new HeaderProperty { Name = "Area Data ID",        Value = header.areaDataID.ToString() });
                HeaderProperties.Add(new HeaderProperty { Name = "Camera Angle ID",     Value = header.cameraAngleID.ToString() });
                HeaderProperties.Add(new HeaderProperty { Name = "Music Day ID",        Value = header.musicDayID.ToString() });
                HeaderProperties.Add(new HeaderProperty { Name = "Music Night ID",      Value = header.musicNightID.ToString() });
                HeaderProperties.Add(new HeaderProperty { Name = "Weather ID",          Value = header.weatherID.ToString() });
                HeaderProperties.Add(new HeaderProperty { Name = "Wild Pokémon",        Value = header.wildPokemon.ToString() });
                HeaderProperties.Add(new HeaderProperty { Name = "Location Specifier",  Value = header.locationSpecifier.ToString() });
                HeaderProperties.Add(new HeaderProperty { Name = "Flags",               Value = $"0x{header.flags:X2}" });

                // Incoming warps
                IncomingWarps.Clear();
                foreach (var ev in _cachedEventFiles)
                {
                    if (ev.warps == null) continue;
                    for (int i = 0; i < ev.warps.Count; i++)
                    {
                        var w = ev.warps[i];
                        if (w.header == id)
                            IncomingWarps.Add(new HeaderWarpResult
                            {
                                EventFileID = ev.ID,
                                WarpIndex = i,
                                Position = $"({w.xMapPosition}, {w.yMapPosition})",
                                Anchor = w.anchor
                            });
                    }
                }

                // Outgoing warps
                OutgoingWarps.Clear();
                var selfEv = _cachedEventFiles.FirstOrDefault(e => e.ID == header.eventFileID);
                if (selfEv?.warps != null)
                {
                    for (int i = 0; i < selfEv.warps.Count; i++)
                    {
                        var w = selfEv.warps[i];
                        OutgoingWarps.Add(new HeaderOutgoingWarpResult
                        {
                            WarpIndex = i,
                            Position = $"({w.xMapPosition}, {w.yMapPosition})",
                            DestHeader = w.header,
                            DestAnchor = w.anchor
                        });
                    }
                }

                StatusText = $"Header {id}: {IncomingWarps.Count} incoming, {OutgoingWarps.Count} outgoing warps";
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
            }
        }

        // Navigate header watcher to another header (double-click warp row)
        public void NavigateToHeader(int id)
        {
            HeaderSearchId = id;
            SearchHeaderInfo();
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static bool TryParseNumber(string text, bool hexMode, out int result)
        {
            if (hexMode)
            {
                string hex = text;
                if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) hex = hex.Substring(2);
                return int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result);
            }
            return int.TryParse(text, out result);
        }

        private static int GetParamValue(byte[] data)
        {
            if (data == null || data.Length == 0) return 0;
            if (data.Length == 1) return data[0];
            return BitConverter.ToUInt16(data, 0);
        }

        private static int CountVariableInScriptFile(ScriptFile sf, int varNum, Dictionary<ushort, ScriptCommandInfo> dict)
        {
            int count = 0;
            if (sf.allScripts != null)
                foreach (var s in sf.allScripts) count += CountVariableInCommands(s.commands, varNum, dict);
            if (sf.allFunctions != null)
                foreach (var f in sf.allFunctions) count += CountVariableInCommands(f.commands, varNum, dict);
            return count;
        }

        private static int CountVariableInCommands(List<ScriptCommand> commands, int varNum, Dictionary<ushort, ScriptCommandInfo> dict)
        {
            int count = 0;
            if (commands == null) return 0;
            foreach (var cmd in commands)
            {
                if (cmd.id == null || cmd.cmdParams == null) continue;
                ScriptCommandInfo info = null;
                dict?.TryGetValue(cmd.id.Value, out info);
                var paramTypes = info?.ParameterTypes;
                for (int i = 0; i < cmd.cmdParams.Count; i++)
                {
                    byte[] p = cmd.cmdParams[i];
                    var pt = (paramTypes != null && i < paramTypes.Count)
                        ? paramTypes[i]
                        : ScriptParameter.ParameterType.Integer;
                    int val = GetParamValue(p);
                    if (pt == ScriptParameter.ParameterType.Variable || pt == ScriptParameter.ParameterType.Flex)
                    { if (val == varNum) count++; }
                    else if (p.Length >= 2 && val >= 0x4000 && val == varNum)
                        count++;
                }
            }
            return count;
        }

        private static int CountVariableInLevelScript(LevelScriptFile ls, int varNum)
        {
            int count = 0;
            if (ls.bufferSet == null) return 0;
            foreach (var t in ls.bufferSet)
                if (t is VariableValueTrigger vt && vt.variableToWatch == varNum) count++;
            return count;
        }

        private static int CountVariableInEventFile(EventFile ev, int varNum)
        {
            int count = 0;
            if (ev.triggers != null)
                foreach (var tr in ev.triggers)
                    if (tr.variableWatched == varNum) count++;
            return count;
        }

        private static int CountFlagInScriptFile(ScriptFile sf, int flagNum)
        {
            int count = 0;
            if (sf.allScripts != null)
                foreach (var s in sf.allScripts) count += CountFlagInCommands(s.commands, flagNum);
            if (sf.allFunctions != null)
                foreach (var f in sf.allFunctions) count += CountFlagInCommands(f.commands, flagNum);
            return count;
        }

        private static int CountFlagInCommands(List<ScriptCommand> commands, int flagNum)
        {
            int count = 0;
            if (commands == null) return 0;
            foreach (var cmd in commands)
            {
                if (cmd.cmdParams == null) continue;
                foreach (var p in cmd.cmdParams)
                    if (p.Length >= 2 && GetParamValue(p) == flagNum && flagNum < 0x4000)
                        count++;
            }
            return count;
        }
    }
}

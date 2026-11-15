using Avalonia.Controls;
using DSPRE.HgEngine;
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

namespace DSPRE.Avalonia.ViewModels.Tools
{
    public partial class ResearchHelperViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        private string _statusText = "Ready";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; private set => Set(ref _isLoading, value); }

        private bool _dataLoaded;
        public bool DataLoaded { get => _dataLoaded; private set => Set(ref _dataLoaded, value); }

        public ObservableCollection<ScriptFileStats> ScriptStats { get; } = new();

        public ObservableCollection<LevelScriptFileStats> LevelScriptStats { get; } = new();

        private string _varSearchText = "";
        public string VarSearchText { get => _varSearchText; set => Set(ref _varSearchText, value); }

        private bool _varHexMode;
        public bool VarHexMode { get => _varHexMode; set => Set(ref _varHexMode, value); }

        public ObservableCollection<VariableUsageResult> VariableResults { get; } = new();

        private string _flagSearchText = "";
        public string FlagSearchText { get => _flagSearchText; set => Set(ref _flagSearchText, value); }

        private bool _flagHexMode;
        public bool FlagHexMode { get => _flagHexMode; set => Set(ref _flagHexMode, value); }

        public ObservableCollection<FlagUsageResult> FlagResults { get; } = new();

        private int _fileWatcherScriptFileId;
        public int FileWatcherScriptFileId { get => _fileWatcherScriptFileId; set => Set(ref _fileWatcherScriptFileId, value); }

        public ObservableCollection<ScriptFileReferenceResult> FileWatcherResults { get; } = new();

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

        private int _owEntryIdSearch;
        public int OwEntryIdSearch { get => _owEntryIdSearch; set => Set(ref _owEntryIdSearch, value); }

        public ObservableCollection<OwEntryUsageResult> OwWatcherResults { get; } = new();

        public ObservableCollection<string> TrainerNamesList { get; } = new();

        private int _selectedTrainerIndex = -1;
        public int SelectedTrainerIndex { get => _selectedTrainerIndex; set => Set(ref _selectedTrainerIndex, value); }

        public ObservableCollection<TrainerUsageResult> TrainerWatcherResults { get; } = new();

        public ObservableCollection<string> ClassNamesList { get; } = new();

        private int _selectedClassIndex = -1;
        public int SelectedClassIndex { get => _selectedClassIndex; set => Set(ref _selectedClassIndex, value); }

        public ObservableCollection<TrainerUsageResult> ClassWatcherResults { get; } = new();

        private int _headerSearchId;
        public int HeaderSearchId { get => _headerSearchId; set => Set(ref _headerSearchId, value); }

        public ObservableCollection<HeaderProperty> HeaderProperties { get; } = new();
        public ObservableCollection<HeaderWarpResult> IncomingWarps { get; } = new();
        public ObservableCollection<HeaderOutgoingWarpResult> OutgoingWarps { get; } = new();

        private List<ScriptFile> _cachedScriptFiles = new();
        private List<LevelScriptFile> _cachedLevelScriptFiles = new();
        private List<EventFile> _cachedEventFiles = new();
        private Dictionary<int, ScriptFile> _scriptFileById = new();

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

        public ResearchHelperViewModel(bool _) { }
    }
}

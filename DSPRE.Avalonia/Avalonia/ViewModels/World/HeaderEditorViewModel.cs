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
    /// <summary>A combo whose names map to non-contiguous raw ids (camera, weather, music).</summary>
    public class MappedCombo
    {
        public ObservableCollection<string> Names { get; } = new ObservableCollection<string>();
        public List<int> Keys { get; } = new List<int>();

        public void Load<TKey>(Dictionary<TKey, string> dict) where TKey : struct, IConvertible
        {
            Names.Clear(); Keys.Clear();
            foreach (KeyValuePair<TKey, string> kv in dict) { Keys.Add(Convert.ToInt32(kv.Key)); Names.Add(kv.Value); }
        }
        /// <summary>Fills from a label category (value = position), in place so selections hold.</summary>
        public void LoadLabels(string category)
        {
            IReadOnlyList<string> labels = LabelStore.Get(category);
            ListSync.Apply(Names, labels);
            Keys.Clear();
            for (int i = 0; i < labels.Count; i++) Keys.Add(i);
        }

        public void LoadMusic() => Load(RomInfo.GameFamily switch
        {
            RomInfo.GameFamilies.DP => PokeDatabase.MusicDB.DPMusicDict,
            RomInfo.GameFamilies.Plat => PokeDatabase.MusicDB.PtMusicDict,
            _ => PokeDatabase.MusicDB.HGSSMusicDict,
        });

        public void LoadSequences()
        {
            Names.Clear(); Keys.Clear();
            SdatArchive sdat;
            try { sdat = SoundArchive.Load(); } catch (Exception e) when (e is System.IO.IOException || e is InvalidOperationException) { return; }
            if (sdat?.SeqNames == null) return;
            foreach (KeyValuePair<int, string> kv in sdat.SeqNames.OrderBy(kv => kv.Key))
            {
                if (kv.Value == null || kv.Value.StartsWith("SEQ_SE_", StringComparison.Ordinal) || kv.Value == SoundArchive.CrySequenceName) continue;
                Keys.Add(kv.Key);
                Names.Add(VsIntroNames.SequenceLabel(kv.Key, kv.Value));
            }
        }

        public int IndexOf(int value) => Keys.IndexOf(value);
        public int KeyAt(int index) => index >= 0 && index < Keys.Count ? Keys[index] : -1;
    }

    /// <summary>Edits map headers. Save writes to ARM9 or the dynamic headers file.</summary>
    public partial class HeaderEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo, IDetachable
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
        public ObservableCollection<string> FollowModeItems { get; } = new ObservableCollection<string> { "None", "Small only", "All" };

        public MappedCombo Camera { get; } = new MappedCombo();
        public MappedCombo Weather { get; } = new MappedCombo();
        public MappedCombo MusicDay { get; } = new MappedCombo();
        public MappedCombo MusicNight { get; } = new MappedCombo();

        public bool IsHgss => GameFamily == GameFamilies.HGSS;
        public bool IsDp => GameFamily == GameFamilies.DP;
        public bool ShowAreaIcon { get; private set; }
        public bool ShowHgssOnly => IsHgss;
        public bool CanAddRemove { get; private set; }
        public decimal WildPokeMax { get; private set; } = 65535;

        // Widths of the packed header fields, so a box cannot hold a value the save would mask.
        public decimal BattleBackgroundMax => IsDp ? 15 : 31;
        public IReadOnlyList<string> BattleBackgroundNames => DSPRE.Resources.BattleBackgrounds.Names();
        public decimal WeatherMax => IsHgss ? 127 : 255;
        public decimal CameraMax => IsHgss ? 63 : 255;

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        // For the Maps workspace's strip: the location name, else the internal name, and the number.
        public string SelectedHeaderTitle =>
            _locationNameIndex >= 0 && _locationNameIndex < LocationNames.Count && !string.IsNullOrWhiteSpace(LocationNames[_locationNameIndex])
                ? LocationNames[_locationNameIndex].Trim()
                : (_header != null ? _internalName : "-");
        public string SelectedHeaderSubtitle => _header != null ? $"header {_header.ID:D3}" : "";

        /// <summary>The loaded header's id, or -1. The Maps workspace's Map tab follows it.</summary>
        public int CurrentHeaderId => _header?.ID ?? -1;

        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => _header != null ? $"Header {_header.ID}" : "Headers";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); LoadHeader(SelectedHeaderId); }
        // The snapshot is taken before the dirty check, so every edit can be undone.
        private void SetDirty() { if (_suppress) return; RecordUndoSnapshot(); if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        public HeaderEditorViewModel()
        {
            if (!Design.IsDesignMode) return;
            HeaderTreeFolder folder = new HeaderTreeFolder { DisplayName = "Jubilife City", IsExpanded = true };
            folder.Children.Add(new HeaderTreeLeaf { HeaderId = 3, DisplayName = "003 -   JUBILIFE_CITY" });
            TreeFolders.Add(folder);
        }

        public HeaderEditorViewModel(bool _)
        {
            AppEvents.HeaderSaved += OnSavedElsewhere;
            AppEvents.RomPatchStateChanged += OnPatchStateChanged;
            MusicPreview.Changed += OnMusicChanged;
            DSPRE.GameCameraTable.Saved += OnPreviewSourceSaved;
            DSPRE.FlyTable.Saved += OnPreviewSourceSaved;
            AppEvents.MapSaved += OnMapSaved;
        }

        /// <summary>Raised after a save changes what the camera preview draws; true for a map.</summary>
        public event Action<bool> PreviewSourcesChanged;

        private void OnPreviewSourceSaved(object sender, EventArgs e) => Dispatcher.UIThread.Post(() => PreviewSourcesChanged?.Invoke(false));
        private void OnMapSaved(object sender, int map) => Dispatcher.UIThread.Post(() => PreviewSourcesChanged?.Invoke(true));

        // Add and Remove only turn on once the patch is applied, even with this window open.
        private void OnPatchStateChanged(object sender, EventArgs e)
        {
            if (_dynamicHeaders == MapHeader.UsesDynamicHeaders) return;
            _dynamicHeaders = MapHeader.UsesDynamicHeaders;
            CanAddRemove = _dynamicHeaders;
            OnPropertyChanged(nameof(CanAddRemove));
            OnPropertyChanged(nameof(OffersHeadersPatch));
            OnPropertyChanged(nameof(AddTip));
            OnPropertyChanged(nameof(RemoveTip));
        }

        public bool OffersHeadersPatch => !_dynamicHeaders && AvaloniaEditorLauncher.IsRomLoaded && PatchToolboxLogic.DynamicHeadersSupported;

        private void OnMusicChanged() => Dispatcher.UIThread.Post(() => { OnPropertyChanged(nameof(DayPlayIcon)); OnPropertyChanged(nameof(NightPlayIcon)); });
        public string DayPlayIcon => MusicPreview.StartedBy((this, false)) ? "stop" : "play";
        public string NightPlayIcon => MusicPreview.StartedBy((this, true)) ? "stop" : "play";

        /// <summary>For a standalone window closing; the Maps workspace's instance lives for the session.</summary>
        public void Detach()
        {
            AppEvents.HeaderSaved -= OnSavedElsewhere;
            AppEvents.RomPatchStateChanged -= OnPatchStateChanged;
            AppEvents.LabelsChanged -= OnLabelsChanged;
            MusicPreview.Changed -= OnMusicChanged;
            DSPRE.GameCameraTable.Saved -= OnPreviewSourceSaved;
            DSPRE.FlyTable.Saved -= OnPreviewSourceSaved;
            AppEvents.MapSaved -= OnMapSaved;
        }

        // Another open copy of this header saved: show it, unless this copy holds its own edits.
        private void OnSavedElsewhere(object sender, int id)
        {
            if (ReferenceEquals(sender, this) || _header == null || _header.ID != id) return;
            if (HasUnsavedChanges) { StatusText = $"Header {id} was saved in another window. Save or discard here to see it."; return; }
            LoadHeader((ushort)id);
            StatusText = "";
        }

        // Header 0 is the blank one (no encounters, 255s), so new headers copy it by default.
        public static readonly string[] CopyFromOptions = { "Duplicate header 0", "Duplicate selected header" };
        private const string CopyFromKey = "header.addCopiesSelected";

        public int CopyFromIndex
        {
            get => ProjectPrefs.GetBool(CopyFromKey, false) ? 1 : 0;
            set
            {
                ProjectPrefs.SetBool(CopyFromKey, value == 1);
                OnPropertyChanged(nameof(CopyFromIndex));
            }
        }

        // Headers are numbered by position, so only the last one can go.
        private const string NeedsDynamicHeaders = "Needs the Dynamic map headers patch";
        public string AddTip => _dynamicHeaders ? null : NeedsDynamicHeaders;
        public string RemoveTip => !_dynamicHeaders ? NeedsDynamicHeaders
            : _headerListNames.Count > 1 ? $"Removes header {_headerListNames.Count - 1:D3}, the last one"
            : "There has to be at least one header.";

        // Removed headers, newest last, so Ctrl+Z can put them back while no field edit is left to undo.
        private readonly List<(byte[] Data, string Name)> _removedHeaders = new();

        public async Task AddHeaderAsync()
        {
            if (!_dynamicHeaders) return;
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(this, _owner, "header", "Save them before adding a header?")) return;
            const string newmap = "NEWMAP";
            int source = CopyFromIndex == 1 ? SelectedHeaderId : 0;
            int newId = MapHeader.AddDynamicHeader(newmap, source);
            _removedHeaders.Clear();   // a restore would land on a different number now
            RaiseUndoState();
            OnPropertyChanged(nameof(RemoveTip));

            _headerListNames.Add(newId.ToString("D3") + MapHeader.NameSeparator + newmap);
            _internalNames.Add(newmap);
            LoadLocationIndices();
            RebuildTree();
            SelectHeader((ushort)newId);

            await GiveNewHeaderFilesAsync(newId);
        }

        private async Task GiveNewHeaderFilesAsync(int newId)
        {
            if (HgEngine.HgEngineProject.IsActive)
            {
                await DialogHelper.ShowInfoAsync($"Header {newId} added. Pick its script and text files below.", "Header added");
                return;
            }
            bool hgss = GameFamily == GameFamilies.HGSS;
            List<(string Label, bool IsChecked)> options = new List<(string, bool)>
            {
                ("New script file", true), ("New level script with no triggers", true), ("Empty event file", true), ("New text archive", true),
            };
            if (hgss) options.Add(("Headbutt file", true));
            (bool create, bool[] pick) = await DialogHelper.AskWithChecksAsync(
                $"Header {newId} is a copy, so it uses the copied header's files. Make new ones for it?",
                "Header added", "Create", "Keep the copied ones", options.ToArray());
            if (!create) return;

            List<string> made = new List<string>();
            try
            {
                if ((pick[0] || pick[1]) && !RotomTool.IsAvailable)
                    throw new InvalidOperationException("Rotom isn't available, so new script files can't be built.");
                int script = pick[0] ? NewProjectFiles.AddScript().Id : -1;
                if (script >= 0) made.Add($"script file {script}");
                int level = pick[1] ? NewProjectFiles.AddBlankLevelScript().Id : -1;
                if (level >= 0) made.Add($"level script {level}");
                // The header points at scripts only once built, or an open script tab reads a missing file.
                if (script >= 0 || level >= 0)
                {
                    RotomTool.Result built = await RotomTool.CompileProjectAsync();
                    if (!built.Success) throw new InvalidOperationException("The script build failed:\n" + RotomTool.FormatDetails(built));
                }
                if (script >= 0) ScriptFileId = script;
                if (level >= 0) LevelScriptId = level;
                if (pick[2]) { EventFileId = NewProjectFiles.AddEventFile(); made.Add($"event file {EventFileId}"); }
                if (pick[3]) { TextArchiveId = NewProjectFiles.AddTextArchive(sender: this); made.Add($"text archive {TextArchiveId}"); }
                // Headbutt files are numbered by header, so every one missing below the new header is made too.
                int filled = hgss && pick[4] ? NewProjectFiles.FillHeadbuttFiles(newId + 1) : 0;
                if (filled == 1) made.Add($"headbutt file {newId}");
                else if (filled > 1) made.Add($"headbutt files {newId + 1 - filled} to {newId} ({filled} were missing)");
                if (made.Count > 0) Save();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
            {
                await DialogHelper.ShowErrorAsync((made.Count > 0 ? $"Made {string.Join(", ", made)}, then: " : "") + ex.Message, "Header added");
                return;
            }
            if (made.Count > 0)
                await DialogHelper.ShowInfoAsync($"Header {newId} now has {string.Join(", ", made)}.", "Header added");
        }

        public async Task RemoveHeaderAsync()
        {
            if (!_dynamicHeaders) return;
            int lastIndex = _headerListNames.Count - 1;
            if (lastIndex <= 0)
            {
                await DialogHelper.ShowErrorAsync("There has to be at least one header.", "Header editor");
                return;
            }

            string name = lastIndex < _internalNames.Count ? _internalNames[lastIndex] : "";
            if (!await DialogHelper.AskYesNoAsync(
                    $"Remove header {lastIndex:D3} ({name}) now? Ctrl+Z puts it back.",
                    "Remove header?"))
                return;

            byte[] data = PlainFiles.ReadBytes(Filesystem.GetDynamicHeaderPath(lastIndex));
            MapHeader.RemoveLastDynamicHeader();
            _removedHeaders.Add((data, name));

            _internalNames.RemoveAt(lastIndex);
            _headerListNames.RemoveAt(lastIndex);
            LoadLocationIndices();
            RebuildTree();
            // Only the removed header loses its edits.
            if (SelectedHeaderId >= lastIndex) { SetClean(); SelectHeader((ushort)(lastIndex - 1)); }
            RaiseUndoState();
            OnPropertyChanged(nameof(RemoveTip));
        }

        private void RestoreRemovedHeader()
        {
            (byte[] data, string name) = _removedHeaders[^1];
            _removedHeaders.RemoveAt(_removedHeaders.Count - 1);
            int id = MapHeader.RestoreDynamicHeader(data, name);
            _headerListNames.Add(id.ToString("D3") + MapHeader.NameSeparator + name);
            _internalNames.Add(name);
            LoadLocationIndices();
            RebuildTree();
            SelectHeader((ushort)id);
            RaiseUndoState();
            OnPropertyChanged(nameof(RemoveTip));
        }
    }
}

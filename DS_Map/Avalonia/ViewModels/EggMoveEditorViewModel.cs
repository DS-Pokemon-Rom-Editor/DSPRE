using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;

namespace DSPRE.Avalonia.ViewModels
{
    public class EggMoveEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true;
        }

        // ----------------------------------------------------------------
        // Constants
        // ----------------------------------------------------------------

        private const int EGG_MOVE_OVERLAY_NUMBER = 5;
        private const int EGG_MOVES_SPECIES_CONSTANT = 20000;

        // ----------------------------------------------------------------
        // IEditorWithUnsavedChanges
        // ----------------------------------------------------------------

        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => "Egg Move Editor";
        void IEditorWithUnsavedChanges.SaveChanges() => _ = SaveCommand();
        public void DiscardChanges() => SetDirty(false);

        // ----------------------------------------------------------------
        // ROM data
        // ----------------------------------------------------------------

        private readonly string[] _monNames;
        private readonly string[] _moveNames;
        private List<EggMoveEntry> _eggMoveData = new();
        private bool _useSpecialFormat;
        private int _maxTableSize;
        private int _maxEggMoves = 16;

        // ----------------------------------------------------------------
        // Observable state
        // ----------------------------------------------------------------

        private string _title = "Egg Move Editor";
        public string Title { get => _title; private set => Set(ref _title, value); }

        public ObservableCollection<string> MonList    { get; } = new();
        public ObservableCollection<string> MoveList   { get; } = new();
        public ObservableCollection<string> MonNames   { get; } = new();
        public ObservableCollection<string> MoveNames  { get; } = new();
        public ObservableCollection<string> SearchResults { get; } = new();

        // ---- Selected Pokémon entry ----
        private int _selectedMonIndex = -1;
        public int SelectedMonIndex
        {
            get => _selectedMonIndex;
            set
            {
                if (!Set(ref _selectedMonIndex, value)) return;
                OnMonSelected(value);
            }
        }

        // ---- Selected move ----
        private int _selectedMoveIndex = -1;
        public int SelectedMoveIndex
        {
            get => _selectedMoveIndex;
            set
            {
                if (!Set(ref _selectedMoveIndex, value)) return;
                OnMoveSelected(value);
            }
        }

        // ---- ComboBox selections (Add/Replace pickers) ----
        private int _cbMonIndex = -1;
        public int CbMonIndex
        {
            get => _cbMonIndex;
            set { if (Set(ref _cbMonIndex, value)) UpdateMonStatus(); }
        }

        private int _cbMoveIndex = -1;
        public int CbMoveIndex
        {
            get => _cbMoveIndex;
            set { if (Set(ref _cbMoveIndex, value)) UpdateMoveStatus(); }
        }

        private int _cbReplaceeIndex = -1;
        public int CbReplaceeIndex { get => _cbReplaceeIndex; set => Set(ref _cbReplaceeIndex, value); }

        private int _cbReplacerIndex = -1;
        public int CbReplacerIndex { get => _cbReplacerIndex; set => Set(ref _cbReplacerIndex, value); }

        private int _cbDeleteAllIndex = -1;
        public int CbDeleteAllIndex { get => _cbDeleteAllIndex; set => Set(ref _cbDeleteAllIndex, value); }

        // ---- Status / labels ----
        private string _monStatusText = string.Empty;
        public string MonStatusText { get => _monStatusText; private set => Set(ref _monStatusText, value); }

        private string _moveStatusText = string.Empty;
        public string MoveStatusText { get => _moveStatusText; private set => Set(ref _moveStatusText, value); }

        private string _monCountText = string.Empty;
        public string MonCountText { get => _monCountText; private set => Set(ref _monCountText, value); }

        private string _moveCountText = string.Empty;
        public string MoveCountText { get => _moveCountText; private set => Set(ref _moveCountText, value); }

        private IBrush _moveCountBrush = Brushes.Transparent;
        public IBrush MoveCountBrush { get => _moveCountBrush; private set => Set(ref _moveCountBrush, value); }

        private string _listSizeText = string.Empty;
        public string ListSizeText { get => _listSizeText; private set => Set(ref _listSizeText, value); }

        private IBrush _listSizeBrush = Brushes.Transparent;
        public IBrush ListSizeBrush { get => _listSizeBrush; private set => Set(ref _listSizeBrush, value); }

        private string _entryIdText = string.Empty;
        public string EntryIdText { get => _entryIdText; private set => Set(ref _entryIdText, value); }

        private string _moveIdText = string.Empty;
        public string MoveIdText { get => _moveIdText; private set => Set(ref _moveIdText, value); }

        // ---- Button enables ----
        public bool CanAddMon     => CbMonIndex >= 0 && !_eggMoveData.Any(e => e.speciesID == CbMonIndex);
        public bool CanReplaceMon => CanAddMon && _selectedMonIndex >= 0;
        public bool CanDeleteMon  => _selectedMonIndex >= 0 && _selectedMonIndex < _eggMoveData.Count;
        public bool CanAddMove    => CbMoveIndex >= 0 && _selectedMonIndex >= 0 && _selectedMonIndex < _eggMoveData.Count
                                     && !_eggMoveData[_selectedMonIndex].moveIDs.Contains((ushort)CbMoveIndex);
        public bool CanReplaceMove => CanAddMove && _selectedMoveIndex >= 0;
        public bool CanDeleteMove  => _selectedMonIndex >= 0 && _selectedMoveIndex >= 0
                                      && _selectedMoveIndex < _eggMoveData[_selectedMonIndex].moveIDs.Count;

        // ---- Search ----
        private string _searchText = string.Empty;
        public string SearchText { get => _searchText; set => Set(ref _searchText, value); }

        private int _selectedSearchIndex = -1;
        public int SelectedSearchIndex { get => _selectedSearchIndex; set => Set(ref _selectedSearchIndex, value); }

        // ----------------------------------------------------------------
        // Constructor
        // ----------------------------------------------------------------

        public EggMoveEditorViewModel()
        {
            _monNames  = RomInfo.GetPokemonNames();
            _moveNames = RomInfo.GetAttackNames();

            foreach (var n in _monNames)  MonNames.Add(n);
            foreach (var n in _moveNames) MoveNames.Add(n);

            PopulateEggMoveData();
            RefreshMonList();
            UpdateEntryCountLabel();
            UpdateListSizeLabel();
        }

        // ----------------------------------------------------------------
        // Commands (called from code-behind)
        // ----------------------------------------------------------------

        public async Task SaveCommand()
        {
            int total = TotalSize;
            if (!_useSpecialFormat && total > _maxTableSize)
            {
                bool proceed = await DialogHelper.AskYesNo(
                    "The egg move data exceeds the maximum allowed size. " +
                    "Saving now will corrupt the game data. Do you want to proceed?",
                    "Warning");
                if (!proceed) return;
            }

            foreach (var entry in _eggMoveData)
            {
                if (entry.moveIDs.Count > _maxEggMoves)
                {
                    string monName = entry.speciesID < _monNames.Length ? _monNames[entry.speciesID] : $"SPECIES_{entry.speciesID}";
                    await DialogHelper.ShowInfo(
                        $"{monName} has more than the maximum allowed Egg Moves ({_maxEggMoves}). This may cause issues in-game.",
                        "Warning");
                }
            }

            SaveEggMoveData();
        }

        public void AddMonCommand()
        {
            if (CbMonIndex < 0) return;
            var entry = new EggMoveEntry(CbMonIndex, new List<ushort>());
            _eggMoveData.Add(entry);
            MonList.Add(MonLabel(CbMonIndex));
            SelectedMonIndex = _eggMoveData.Count - 1;
            UpdateEntryCountLabel();
            UpdateListSizeLabel();
            SetDirty(true);
        }

        public void ReplaceMonCommand()
        {
            if (CbMonIndex < 0 || _selectedMonIndex < 0) return;
            var entry = _eggMoveData[_selectedMonIndex];
            entry.speciesID = CbMonIndex;
            _eggMoveData[_selectedMonIndex] = entry;
            MonList[_selectedMonIndex] = MonLabel(CbMonIndex);
            SetDirty(true);
        }

        public void DeleteMonCommand()
        {
            if (_selectedMonIndex < 0 || _selectedMonIndex >= _eggMoveData.Count) return;
            _eggMoveData.RemoveAt(_selectedMonIndex);
            MonList.RemoveAt(_selectedMonIndex);
            SelectedMonIndex = Math.Min(_selectedMonIndex, _eggMoveData.Count - 1);
            UpdateEntryCountLabel();
            UpdateListSizeLabel();
            SetDirty(true);
        }

        public void AddMoveCommand()
        {
            if (CbMoveIndex < 0 || _selectedMonIndex < 0) return;
            var entry = _eggMoveData[_selectedMonIndex];
            entry.moveIDs.Add((ushort)CbMoveIndex);
            _eggMoveData[_selectedMonIndex] = entry;
            MoveList.Add(MoveLabel((ushort)CbMoveIndex));
            SelectedMoveIndex = entry.moveIDs.Count - 1;
            UpdateMoveCountLabel();
            UpdateListSizeLabel();
            SetDirty(true);
        }

        public void ReplaceMoveCommand()
        {
            if (CbMoveIndex < 0 || _selectedMonIndex < 0 || _selectedMoveIndex < 0) return;
            var entry = _eggMoveData[_selectedMonIndex];
            entry.moveIDs[_selectedMoveIndex] = (ushort)CbMoveIndex;
            _eggMoveData[_selectedMonIndex] = entry;
            MoveList[_selectedMoveIndex] = MoveLabel((ushort)CbMoveIndex);
            SetDirty(true);
        }

        public void DeleteMoveCommand()
        {
            if (_selectedMonIndex < 0 || _selectedMoveIndex < 0) return;
            var entry = _eggMoveData[_selectedMonIndex];
            entry.moveIDs.RemoveAt(_selectedMoveIndex);
            _eggMoveData[_selectedMonIndex] = entry;
            MoveList.RemoveAt(_selectedMoveIndex);
            SelectedMoveIndex = Math.Min(_selectedMoveIndex, entry.moveIDs.Count - 1);
            UpdateMoveCountLabel();
            UpdateListSizeLabel();
            SetDirty(true);
        }

        public async Task BulkReplaceCommand()
        {
            if (CbReplaceeIndex < 0 || CbReplacerIndex < 0)
            {
                await DialogHelper.ShowError("Please select valid moves for the bulk replace.", "Error");
                return;
            }
            ushort from = (ushort)CbReplaceeIndex, to = (ushort)CbReplacerIndex;
            int count = 0; var affected = new List<string>();
            foreach (var entry in _eggMoveData)
            {
                for (int i = 0; i < entry.moveIDs.Count; i++)
                {
                    if (entry.moveIDs[i] != from) continue;
                    entry.moveIDs[i] = to; count++;
                    affected.Add(entry.speciesID < _monNames.Length ? _monNames[entry.speciesID] : $"SPECIES_{entry.speciesID}");
                }
            }
            if (count > 0)
            {
                RefreshMoveList(_selectedMonIndex);
                SetDirty(true);
                await DialogHelper.ShowInfo($"Replaced {count} occurrence(s) of {_moveNames[from]} with {_moveNames[to]}.\nAffected: {string.Join(", ", affected)}", "Bulk Replace");
            }
            else
                await DialogHelper.ShowInfo($"No occurrences of {_moveNames[from]} were found.", "Bulk Replace");
        }

        public async Task BulkDeleteCommand()
        {
            if (CbDeleteAllIndex < 0)
            {
                await DialogHelper.ShowError("Please select a valid move to delete.", "Error");
                return;
            }
            ushort del = (ushort)CbDeleteAllIndex;
            int count = 0; var affected = new List<string>();
            foreach (var entry in _eggMoveData)
            {
                int before = entry.moveIDs.Count;
                entry.moveIDs.RemoveAll(m => m == del);
                int diff = before - entry.moveIDs.Count;
                if (diff <= 0) continue;
                count += diff;
                affected.Add(entry.speciesID < _monNames.Length ? _monNames[entry.speciesID] : $"SPECIES_{entry.speciesID}");
            }
            if (count > 0)
            {
                RefreshMoveList(_selectedMonIndex);
                UpdateMoveCountLabel();
                UpdateListSizeLabel();
                SetDirty(true);
                await DialogHelper.ShowInfo($"Deleted {count} occurrence(s) of {_moveNames[del]}.\nAffected: {string.Join(", ", affected)}", "Bulk Delete");
            }
            else
                await DialogHelper.ShowInfo($"No occurrences of {_moveNames[del]} were found.", "Bulk Delete");
        }

        public void SearchMonCommand()
        {
            SearchResults.Clear();
            if (string.IsNullOrWhiteSpace(SearchText)) return;
            string lower = SearchText.Trim().ToLower();
            foreach (var entry in _eggMoveData)
            {
                string name = entry.speciesID < _monNames.Length ? _monNames[entry.speciesID] : $"SPECIES_{entry.speciesID}";
                if (name.ToLower().Contains(lower)) SearchResults.Add(name);
            }
        }

        public void JumpToSearchResult()
        {
            if (SelectedSearchIndex < 0 || SelectedSearchIndex >= SearchResults.Count) return;
            string target = SearchResults[SelectedSearchIndex];
            for (int i = 0; i < MonList.Count; i++)
            {
                if (MonList[i] == target) { SelectedMonIndex = i; return; }
            }
        }

        public async Task ExportCommand(Window owner)
        {
            string path = await DialogHelper.SaveFile(owner, "Export Egg Move Data",
                new[] { DialogHelper.CsvFilter, DialogHelper.AllFilter }, "egg_moves.csv");
            if (path == null) return;
            bool ok = DocTool.ExportEggMoveDataToCSV(_eggMoveData, path, _monNames, _moveNames);
            if (ok) await DialogHelper.ShowInfo("Egg move data exported successfully.", "Export Complete");
            else    await DialogHelper.ShowError("Failed to export egg move data. Check the logs.", "Export Failed");
        }

        public async Task ImportCommand(Window owner)
        {
            string path = await DialogHelper.OpenFile(owner, "Import Egg Move Data",
                new[] { DialogHelper.CsvFilter, DialogHelper.AllFilter });
            if (path == null) return;
            bool ok = DocTool.ImportEggMoveDataFromCSV(ref _eggMoveData, path);
            if (ok)
            {
                RefreshMonList();
                UpdateEntryCountLabel();
                UpdateListSizeLabel();
                SetDirty(true);
                await DialogHelper.ShowInfo("Egg move data imported successfully.", "Import Complete");
            }
            else
                await DialogHelper.ShowError("Failed to import egg move data. Check the logs.", "Import Failed");
        }

        public async Task<bool> ConfirmCloseAsync()
        {
            if (!_dirty) return true;
            var result = await DialogHelper.AskYesNoCancel(
                "You have unsaved changes. Do you want to save them before exiting?",
                "Unsaved Changes");
            if (result == DialogHelper.MsgResult.Yes) { await SaveCommand(); return true; }
            return result == DialogHelper.MsgResult.No;
        }

        // ----------------------------------------------------------------
        // Private helpers
        // ----------------------------------------------------------------

        private int TotalSize
        {
            get
            {
                int s = 2; // end marker
                foreach (var e in _eggMoveData) s += e.GetSizeInBytes();
                return s;
            }
        }

        private void OnMonSelected(int idx)
        {
            if (idx < 0 || idx >= _eggMoveData.Count)
            {
                MoveList.Clear();
                RefreshButtonEnables();
                return;
            }
            CbMonIndex = _eggMoveData[idx].speciesID;
            RefreshMoveList(idx);
            UpdateMonStatus();
            UpdateMoveStatus();
            UpdateMoveCountLabel();
            EntryIdText = $"Entry Index: {idx}";
            RefreshButtonEnables();
        }

        private void OnMoveSelected(int idx)
        {
            if (_selectedMonIndex < 0 || idx < 0 || idx >= _eggMoveData[_selectedMonIndex].moveIDs.Count)
            {
                RefreshButtonEnables();
                return;
            }
            CbMoveIndex = _eggMoveData[_selectedMonIndex].moveIDs[idx];
            UpdateMoveStatus();
            MoveIdText = $"Move Index: {idx}";
            RefreshButtonEnables();
        }

        private void RefreshMonList()
        {
            MonList.Clear();
            foreach (var e in _eggMoveData) MonList.Add(MonLabel(e.speciesID));
        }

        private void RefreshMoveList(int monIdx)
        {
            MoveList.Clear();
            if (monIdx < 0 || monIdx >= _eggMoveData.Count) return;
            foreach (var id in _eggMoveData[monIdx].moveIDs) MoveList.Add(MoveLabel(id));
        }

        private string MonLabel(int id)  => id >= 0 && id < _monNames.Length  ? _monNames[id]  : $"SPECIES_{id}";
        private string MoveLabel(ushort id) => id < _moveNames.Length ? _moveNames[id] : $"MOVE_{id}";

        private void UpdateMonStatus()
        {
            OnPropertyChanged(nameof(CanAddMon));
            OnPropertyChanged(nameof(CanReplaceMon));
            MonStatusText = CbMonIndex < 0 ? "Invalid Pokémon selected."
                : _eggMoveData.Any(e => e.speciesID == CbMonIndex) ? "This Pokémon already has egg moves."
                : "This Pokémon can be added.";
        }

        private void UpdateMoveStatus()
        {
            OnPropertyChanged(nameof(CanAddMove));
            OnPropertyChanged(nameof(CanReplaceMove));
            if (CbMoveIndex < 0) { MoveStatusText = "Invalid move selected."; return; }
            if (_selectedMonIndex < 0) { MoveStatusText = "No Pokémon selected."; return; }
            ushort id = (ushort)CbMoveIndex;
            MoveStatusText = _eggMoveData[_selectedMonIndex].moveIDs.Contains(id)
                ? "Egg move already in list." : "Egg move can be added.";
        }

        private void UpdateEntryCountLabel() => MonCountText = $"Pokémon Count: {_eggMoveData.Count}";

        private void UpdateMoveCountLabel()
        {
            if (_selectedMonIndex < 0 || _selectedMonIndex >= _eggMoveData.Count)
            {
                MoveCountText = "Move Count: N/A";
                MoveCountBrush = Brushes.Transparent;
                return;
            }
            int cnt = _eggMoveData[_selectedMonIndex].moveIDs.Count;
            MoveCountText = $"Move Count: {cnt}";
            MoveCountBrush = cnt > _maxEggMoves ? Brushes.Red : cnt == _maxEggMoves ? Brushes.Orange : Brushes.Transparent;
        }

        private void UpdateListSizeLabel()
        {
            if (_useSpecialFormat)
            {
                ListSizeText  = "List Size: Special Format!";
                ListSizeBrush = Brushes.Green;
                return;
            }
            int total = TotalSize;
            ListSizeText  = $"List Size: {total} / {_maxTableSize} bytes";
            ListSizeBrush = total > _maxTableSize ? Brushes.Red : total == _maxTableSize ? Brushes.Orange : Brushes.Transparent;
        }

        private void RefreshButtonEnables()
        {
            OnPropertyChanged(nameof(CanAddMon));
            OnPropertyChanged(nameof(CanReplaceMon));
            OnPropertyChanged(nameof(CanDeleteMon));
            OnPropertyChanged(nameof(CanAddMove));
            OnPropertyChanged(nameof(CanReplaceMove));
            OnPropertyChanged(nameof(CanDeleteMove));
        }

        private void SetDirty(bool d)
        {
            _dirty = d;
            Title  = d ? "Egg Move Editor*" : "Egg Move Editor";
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        // ---- ROM I/O (ported 1:1 from original) ----

        private void PopulateEggMoveData()
        {
            try
            {
                EndianBinaryReader reader = GetEggDataReader();
                if (_useSpecialFormat) ReadEggMoveDataSpecial();
                else ReadEggMoveDataNormal(reader);
                reader?.Close();
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Failed to populate egg move data: {ex.Message}");
            }
        }

        private EndianBinaryReader GetEggDataReader()
        {
            if (RomInfo.gameFamily == RomInfo.GameFamilies.HGSS)
            {
                DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.eggMoves });
                var path = Path.Combine(RomInfo.gameDirs[RomInfo.DirNames.eggMoves].unpackedDir, "0000");
                _maxTableSize = 4126;
                return new EndianBinaryReader(File.OpenRead(path), Endianness.LittleEndian);
            }
            else
            {
                int offset = RomInfo.GetEggMoveTableOffset();
                _maxTableSize = 0xEEC;
                var reader = new EndianBinaryReader(File.OpenRead(OverlayUtils.GetPath(EGG_MOVE_OVERLAY_NUMBER)), Endianness.LittleEndian);
                reader.BaseStream.Seek(offset, SeekOrigin.Begin);
                int magic = reader.ReadInt32();
                int maxMoves = reader.ReadInt32();
                reader.BaseStream.Seek(-8, SeekOrigin.Current);
                if (magic == 4671301) { _useSpecialFormat = true; _maxEggMoves = maxMoves; _maxTableSize = ushort.MaxValue; }
                return reader;
            }
        }

        private void ReadEggMoveDataNormal(EndianBinaryReader reader)
        {
            int idx = -1;
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                ushort read = reader.ReadUInt16();
                if (read == 0xFFFF) break;
                if (read > EGG_MOVES_SPECIES_CONSTANT)
                {
                    _eggMoveData.Add(new EggMoveEntry(read - EGG_MOVES_SPECIES_CONSTANT, new List<ushort>()));
                    idx++;
                }
                else if (idx >= 0)
                {
                    var e = _eggMoveData[idx]; e.moveIDs.Add(read); _eggMoveData[idx] = e;
                }
            }
        }

        private void ReadEggMoveDataSpecial()
        {
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.eggMoves });
            string folder = RomInfo.gameDirs[RomInfo.DirNames.eggMoves].unpackedDir;
            foreach (var file in Directory.GetFiles(folder))
            {
                if (!int.TryParse(Path.GetFileName(file), out int speciesID)) continue;
                var moves = new List<ushort>();
                using var r = new EndianBinaryReader(File.OpenRead(file), Endianness.LittleEndian);
                while (r.BaseStream.Position < r.BaseStream.Length)
                {
                    ushort id = r.ReadUInt16();
                    if (id == 0xFFFF) break;
                    moves.Add(id);
                }
                _eggMoveData.Add(new EggMoveEntry(speciesID, moves));
            }
        }

        private void SaveEggMoveData()
        {
            try
            {
                if (RomInfo.gameFamily == RomInfo.GameFamilies.HGSS)
                {
                    var path = Path.Combine(RomInfo.gameDirs[RomInfo.DirNames.eggMoves].unpackedDir, "0000");
                    using var stream = File.OpenWrite(path);
                    using var w = new BinaryWriter(stream);
                    WriteNormal(w);
                    if (stream.Position < stream.Length) stream.SetLength(stream.Position);
                }
                else if (_useSpecialFormat) WriteSpecial();
                else
                {
                    int offset = RomInfo.GetEggMoveTableOffset();
                    using var stream = File.OpenWrite(OverlayUtils.GetPath(EGG_MOVE_OVERLAY_NUMBER));
                    using var w = new BinaryWriter(stream);
                    stream.Seek(offset, SeekOrigin.Begin);
                    WriteNormal(w);
                }
                SetDirty(false);
            }
            catch (Exception ex) { AppLogger.Error($"Failed to save egg move data: {ex.Message}"); }
        }

        private void WriteNormal(BinaryWriter w)
        {
            foreach (var e in _eggMoveData)
            {
                w.Write((ushort)(e.speciesID + EGG_MOVES_SPECIES_CONSTANT));
                foreach (var m in e.moveIDs) w.Write(m);
            }
            w.Write((ushort)0xFFFF);
        }

        private void WriteSpecial()
        {
            string folder = RomInfo.gameDirs[RomInfo.DirNames.eggMoves].unpackedDir;
            Directory.CreateDirectory(folder);
            var hasFile = new HashSet<int>();
            foreach (var e in _eggMoveData)
            {
                using var w = new BinaryWriter(File.OpenWrite(Path.Combine(folder, e.speciesID.ToString("D4"))));
                foreach (var m in e.moveIDs) w.Write(m);
                w.Write((ushort)0xFFFF);
                hasFile.Add(e.speciesID);
            }
            for (int i = 0; i < _monNames.Length; i++)
            {
                if (hasFile.Contains(i)) continue;
                using var w = new BinaryWriter(File.OpenWrite(Path.Combine(folder, i.ToString("D4"))));
                w.Write((ushort)0xFFFF);
            }
        }

        // ----------------------------------------------------------------
        // Static ROM read (for DocTool / other non-UI callers)
        // ----------------------------------------------------------------

        /// <summary>
        /// Reads egg move data directly from the ROM without creating a full ViewModel instance.
        /// Safe to call from any non-UI context once a ROM is loaded.
        /// </summary>
        public static List<EggMoveEntry> ReadFromRom()
        {
            const int overlayNum = EGG_MOVE_OVERLAY_NUMBER;
            var result = new List<EggMoveEntry>();
            bool useSpecial = false;

            EndianBinaryReader reader = null;
            try
            {
                if (RomInfo.gameFamily == RomInfo.GameFamilies.HGSS)
                {
                    DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.eggMoves });
                    var path = Path.Combine(RomInfo.gameDirs[RomInfo.DirNames.eggMoves].unpackedDir, "0000");
                    reader = new EndianBinaryReader(File.OpenRead(path), Endianness.LittleEndian);
                }
                else
                {
                    int offset = RomInfo.GetEggMoveTableOffset();
                    reader = new EndianBinaryReader(File.OpenRead(OverlayUtils.GetPath(overlayNum)), Endianness.LittleEndian);
                    reader.BaseStream.Seek(offset, SeekOrigin.Begin);
                    int magic    = reader.ReadInt32();
                    int maxMoves = reader.ReadInt32();
                    reader.BaseStream.Seek(-8, SeekOrigin.Current);
                    if (magic == 4671301) useSpecial = true;
                }

                if (useSpecial)
                {
                    reader?.Close();
                    DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.eggMoves });
                    string folder = RomInfo.gameDirs[RomInfo.DirNames.eggMoves].unpackedDir;
                    foreach (var file in Directory.GetFiles(folder))
                    {
                        if (!int.TryParse(Path.GetFileName(file), out int speciesID)) continue;
                        var moves = new List<ushort>();
                        using var r = new EndianBinaryReader(File.OpenRead(file), Endianness.LittleEndian);
                        while (r.BaseStream.Position < r.BaseStream.Length)
                        {
                            ushort id = r.ReadUInt16();
                            if (id == 0xFFFF) break;
                            moves.Add(id);
                        }
                        result.Add(new EggMoveEntry(speciesID, moves));
                    }
                }
                else
                {
                    int idx = -1;
                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        ushort read = reader.ReadUInt16();
                        if (read == 0xFFFF) break;
                        if (read > EGG_MOVES_SPECIES_CONSTANT)
                        {
                            result.Add(new EggMoveEntry(read - EGG_MOVES_SPECIES_CONSTANT, new List<ushort>()));
                            idx++;
                        }
                        else if (idx >= 0)
                        {
                            var e = result[idx]; e.moveIDs.Add(read); result[idx] = e;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"EggMoveEditorViewModel.ReadFromRom failed: {ex.Message}");
            }
            finally
            {
                reader?.Close();
            }

            return result;
        }
    }
}

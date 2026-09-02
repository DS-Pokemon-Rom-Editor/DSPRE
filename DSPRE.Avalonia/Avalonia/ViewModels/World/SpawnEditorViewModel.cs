using Avalonia.Controls;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.World
{
    public class SpawnEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }

        // ── Collections ────────────────────────────────────────────────────────
        public ObservableCollection<string> HeaderNames  { get; } = new();
        public ObservableCollection<string> DirectionNames { get; } =
            new() { "Up", "Down", "Left", "Right" };

        // ── Header ─────────────────────────────────────────────────────────────
        private int _selectedHeaderIndex;
        public int SelectedHeaderIndex
        {
            get => _selectedHeaderIndex;
            set
            {
                if (!Set(ref _selectedHeaderIndex, value)) return;
                if (!_isLoading) SetDirty();
                UpdateHeaderDependents(value);
            }
        }

        private string _locationName = "";
        public string LocationName { get => _locationName; private set => Set(ref _locationName, value); }

        // ── Matrix X/Y ─────────────────────────────────────────────────────────
        private int _matrixX;
        public int MatrixX
        {
            get => _matrixX;
            set { if (Set(ref _matrixX, value) && !_isLoading) SetDirty(); }
        }

        private int _matrixXMax = 255;
        public int MatrixXMax { get => _matrixXMax; private set => Set(ref _matrixXMax, value); }

        private int _matrixY;
        public int MatrixY
        {
            get => _matrixY;
            set { if (Set(ref _matrixY, value) && !_isLoading) SetDirty(); }
        }

        private int _matrixYMax = 255;
        public int MatrixYMax { get => _matrixYMax; private set => Set(ref _matrixYMax, value); }

        // ── Local Map X/Y ──────────────────────────────────────────────────────
        private int _localX;
        public int LocalX
        {
            get => _localX;
            set { if (Set(ref _localX, value) && !_isLoading) SetDirty(); }
        }

        private int _localY;
        public int LocalY
        {
            get => _localY;
            set { if (Set(ref _localY, value) && !_isLoading) SetDirty(); }
        }

        // ── Player direction ───────────────────────────────────────────────────
        private int _playerDirIndex;
        public int PlayerDirIndex
        {
            get => _playerDirIndex;
            set { if (Set(ref _playerDirIndex, value) && !_isLoading) SetDirty(); }
        }

        // ── Initial money ──────────────────────────────────────────────────────
        private decimal _initialMoney;
        public decimal InitialMoney
        {
            get => _initialMoney;
            set { if (Set(ref _initialMoney, value) && !_isLoading) SetDirty(); }
        }

        // ── Dirty ──────────────────────────────────────────────────────────────
        private bool _isDirty;
        private bool _isLoading;
        public bool HasUnsavedChanges => _isDirty;
        public string UnsavedChangesDescription => "Spawn Editor";

        private void SetDirty()  { _isDirty = true;  OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean()  { _isDirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        // ── All header names (full list for reset) ─────────────────────────────
        private List<string> _allHeaderNames = new();
        private List<string> _locationNames   = new();

        // ── Design-time constructor ────────────────────────────────────────────
        public SpawnEditorViewModel()
        {
            if (!Design.IsDesignMode) return;
            for (int i = 0; i < 8; i++) HeaderNames.Add($"{i:D4} Location {i}");
            _locationName  = "Twinleaf Town";
            _matrixXMax    = 10;
            _matrixYMax    = 10;
            _matrixX       = 2;
            _matrixY       = 1;
            _localX        = 3;
            _localY        = 5;
            _playerDirIndex = 1;
            _initialMoney  = 3000;
        }

        // ── Runtime constructor ────────────────────────────────────────────────
        public SpawnEditorViewModel(List<string> headerNames)
        {
            _allHeaderNames.AddRange(headerNames);
            _locationNames = RomInfo.GetLocationNames();
            foreach (var n in headerNames) HeaderNames.Add(n);
            LoadFromRom();
        }

        // ── Constructor from matrix editor (preset header + coords) ───────────
        public SpawnEditorViewModel(HashSet<string> filteredHeaders, List<string> allNames,
                                    ushort headerNumber = 0, int matrixX = 0, int matrixY = 0)
        {
            _allHeaderNames.AddRange(allNames);
            _locationNames = RomInfo.GetLocationNames();

            var display = (filteredHeaders == null || filteredHeaders.Count <= 1)
                ? (System.Collections.Generic.IEnumerable<string>)allNames
                : filteredHeaders;

            foreach (var n in display) HeaderNames.Add(n);

            _isLoading = true;
            // When filter is active, SelectedIndex=0; when showing all, jump to header
            _selectedHeaderIndex = (filteredHeaders == null || filteredHeaders.Count <= 1)
                ? Math.Min(headerNumber, HeaderNames.Count - 1)
                : 0;
            OnPropertyChanged(nameof(SelectedHeaderIndex));
            UpdateHeaderDependents(_selectedHeaderIndex);

            _matrixX = Math.Min(matrixX, _matrixXMax);
            _matrixY = Math.Min(matrixY, _matrixYMax);
            _playerDirIndex = 0; // default Up
            _initialMoney = 0;

            OnPropertyChanged(nameof(MatrixX));
            OnPropertyChanged(nameof(MatrixY));
            OnPropertyChanged(nameof(PlayerDirIndex));
            OnPropertyChanged(nameof(InitialMoney));
            _isLoading = false;
            SetClean();
        }

        // ── Load from ROM ──────────────────────────────────────────────────────
        public void LoadFromRom()
        {
            _isLoading = true;
            try
            {
                // Decompress money overlay if needed
                if (OverlayUtils.OverlayTable.IsDefaultCompressed(RomInfo.initialMoneyOverlayNumber) &&
                    OverlayUtils.IsCompressed(RomInfo.initialMoneyOverlayNumber))
                    OverlayUtils.Decompress(RomInfo.initialMoneyOverlayNumber);

                ushort headerNumber = BitConverter.ToUInt16(ARM9.ReadBytes(RomInfo.arm9spawnOffset, 2), 0);
                ushort globalX      = BitConverter.ToUInt16(ARM9.ReadBytes(RomInfo.arm9spawnOffset + 8, 2), 0);
                ushort globalY      = BitConverter.ToUInt16(ARM9.ReadBytes(RomInfo.arm9spawnOffset + 12, 2), 0);
                ushort playerDir    = BitConverter.ToUInt16(ARM9.ReadBytes(RomInfo.arm9spawnOffset + 16, 2), 0);

                // First update header index (triggers UpdateHeaderDependents to set MaxX/MaxY)
                _selectedHeaderIndex = Math.Min(headerNumber, HeaderNames.Count - 1);
                OnPropertyChanged(nameof(SelectedHeaderIndex));
                UpdateHeaderDependents(_selectedHeaderIndex);

                _matrixX = (int)Math.Min(globalX / 32, _matrixXMax);
                _matrixY = (int)Math.Min(globalY / 32, _matrixYMax);
                _localX  = globalX % 32;
                _localY  = globalY % 32;
                _playerDirIndex = Math.Min(playerDir, (ushort)3);

                string moneyPath = OverlayUtils.GetPath(RomInfo.initialMoneyOverlayNumber);
                _initialMoney = BitConverter.ToUInt32(DSUtils.ReadFromFile(moneyPath, RomInfo.initialMoneyOverlayOffset, 4), 0);
            }
            catch { /* leave defaults */ }
            finally
            {
                OnPropertyChanged(nameof(MatrixX));
                OnPropertyChanged(nameof(MatrixY));
                OnPropertyChanged(nameof(LocalX));
                OnPropertyChanged(nameof(LocalY));
                OnPropertyChanged(nameof(PlayerDirIndex));
                OnPropertyChanged(nameof(InitialMoney));
                _isLoading = false;
                SetClean();
            }
        }

        // ── Header-dependent updates ───────────────────────────────────────────
        private void UpdateHeaderDependents(int headerIndex)
        {
            if (Design.IsDesignMode || headerIndex < 0 || headerIndex >= HeaderNames.Count) return;
            try
            {
                ushort headerNumber = (ushort)headerIndex;
                MapHeader currentHeader;
                if (RomPatchState.flag_DynamicHeadersPatchApplied ||
                    PatchToolboxLogic.CheckFilesDynamicHeadersPatchApplied())
                    currentHeader = MapHeader.LoadFromFile(
                        Path.Combine(RomInfo.gameDirs[DirNames.dynamicHeaders].unpackedDir, headerNumber.ToString("D4")),
                        headerNumber, 0);
                else
                    currentHeader = MapHeader.LoadFromARM9(headerNumber);

                var matrix = new GameMatrix(currentHeader.matrixID);
                MatrixXMax = matrix.maps.GetLength(1) - 1;
                MatrixYMax = matrix.maps.GetLength(0) - 1;

                // Clamp existing values
                if (_matrixX > MatrixXMax) { _matrixX = MatrixXMax; OnPropertyChanged(nameof(MatrixX)); }
                if (_matrixY > MatrixYMax) { _matrixY = MatrixYMax; OnPropertyChanged(nameof(MatrixY)); }

                string loc = "";
                switch (RomInfo.gameFamily)
                {
                    case GameFamilies.DP:
                        loc = _locationNames.Count > ((HeaderDP)currentHeader).locationName
                            ? _locationNames[((HeaderDP)currentHeader).locationName] : "";
                        break;
                    case GameFamilies.Plat:
                        loc = _locationNames.Count > ((HeaderPt)currentHeader).locationName
                            ? _locationNames[((HeaderPt)currentHeader).locationName] : "";
                        break;
                    case GameFamilies.HGSS:
                        loc = _locationNames.Count > ((HeaderHGSS)currentHeader).locationName
                            ? _locationNames[((HeaderHGSS)currentHeader).locationName] : "";
                        break;
                }
                LocationName = loc;
            }
            catch { LocationName = ""; }
        }

        // ── Reset filter ───────────────────────────────────────────────────────
        public void ResetFilter()
        {
            if (HeaderNames.Count >= _allHeaderNames.Count) return;
            HeaderNames.Clear();
            foreach (var n in _allHeaderNames) HeaderNames.Add(n);
            SelectedHeaderIndex = 0;
        }

        // ── Save ───────────────────────────────────────────────────────────────
        public async Task<bool> SaveChangesAsync()
        {
            bool confirmed = await DialogHelper.AskYesNo(
                "This operation will overwrite:\n" +
                $"- 10 bytes of data at ARM9 offset 0x{RomInfo.arm9spawnOffset:X}\n" +
                $"- 4 bytes of data at Overlay{RomInfo.initialMoneyOverlayNumber} offset 0x{RomInfo.initialMoneyOverlayOffset:X}\n\nProceed?",
                "Confirmation Required");
            if (!confirmed) return false;

            ushort headerNumber = (ushort)SelectedHeaderIndex;
            ARM9.WriteBytes(BitConverter.GetBytes(headerNumber),             RomInfo.arm9spawnOffset);
            ARM9.WriteBytes(BitConverter.GetBytes((short)(_matrixX * 32 + _localX)), RomInfo.arm9spawnOffset + 8);
            ARM9.WriteBytes(BitConverter.GetBytes((short)(_matrixY * 32 + _localY)), RomInfo.arm9spawnOffset + 12);
            ARM9.WriteBytes(BitConverter.GetBytes((short)_playerDirIndex),    RomInfo.arm9spawnOffset + 16);

            string moneyPath = OverlayUtils.GetPath(RomInfo.initialMoneyOverlayNumber);
            DSUtils.WriteToFile(moneyPath, BitConverter.GetBytes((int)_initialMoney), RomInfo.initialMoneyOverlayOffset);

            SetClean();
            return true;
        }

        // IEditorWithUnsavedChanges sync wrapper
        public void SaveChanges() => _ = SaveChangesAsync();
        public void DiscardChanges() => LoadFromRom();
    }
}

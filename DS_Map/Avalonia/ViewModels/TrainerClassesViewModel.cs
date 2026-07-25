using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using global::Avalonia.Controls;
using DSPRE.Avalonia;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// Avalonia port of the trainer-class panel of the WinForms <c>TrainerEditor</c>: rename a trainer
    /// class and edit its "eye contact" encounter music (the SSEQ that plays when that class of trainer
    /// spots the player) — a small ARM9-backed table, separate from any single trainer's own data, so it
    /// gets its own popup rather than crowding the main Trainer Editor window.
    /// </summary>
    public class TrainerClassesViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private bool _suppress;
        private readonly Dictionary<byte, (uint entryOffset, ushort musicD, ushort? musicN)> _musicDict = new();

        /// <summary>Which file _musicDict's entryOffsets are relative to — the table may have been
        /// repointed into the synthetic overlay (e.g. by hand, following the "adding a new trainer
        /// class" community write-up). Mirrors TrainerEditor.cs's (WinForms) identical field.</summary>
        private bool _musicTableRepointed;

        public ObservableCollection<string> ClassNames { get; } = new();

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        private int _selectedIndex = -1;
        public int SelectedClassIndex
        {
            get => _selectedIndex;
            set { if (Set(ref _selectedIndex, value) && !_suppress && value >= 0) LoadClass(value); }
        }

        private string _className = "";
        public string ClassName { get => _className; set => Set(ref _className, value); }

        private decimal _musicMain;
        public decimal MusicMain { get => _musicMain; set => Set(ref _musicMain, value); }

        private decimal _musicAlt;
        public decimal MusicAlt { get => _musicAlt; set => Set(ref _musicAlt, value); }

        private bool _musicEnabled;
        public bool MusicEnabled { get => _musicEnabled; private set => Set(ref _musicEnabled, value); }

        private bool _musicAltEnabled;
        public bool MusicAltEnabled { get => _musicAltEnabled; private set => Set(ref _musicAltEnabled, value); }

        public TrainerClassesViewModel() { if (Design.IsDesignMode) ClassNames.Add("[000] Youngster"); }

        public TrainerClassesViewModel(int initialClass)
        {
            try
            {
                string[] names = GetTrainerClassNames();
                for (int i = 0; i < names.Length; i++) ClassNames.Add($"[{i:D3}] {names[i]}");

                SetupEncounterMusicTable();

                StatusText = $"{ClassNames.Count} trainer classes.";
                if (ClassNames.Count > 0)
                    SelectedClassIndex = Math.Min(Math.Max(0, initialClass), ClassNames.Count - 1);
            }
            catch (Exception ex)
            {
                StatusText = "Error: " + ex.Message;
                _ = DialogHelper.ShowError($"Failed to load trainer classes:\n{ex.Message}", "Trainer Classes");
            }
        }

        /// <summary>Mirrors the WinForms <c>SetupTrainerClassEncounterMusicTable</c>: a variable-size ARM9
        /// table, one entry per trainer class that HAS eye-contact music (not every class does).</summary>
        private void SetupEncounterMusicTable()
        {
            SetEncounterMusicTableOffsetToRAMAddress();

            uint tableStart = BitConverter.ToUInt32(ARM9.ReadBytes(encounterMusicTableOffsetToRAMAddress, 4), 0);
            _musicTableRepointed = tableStart >= synthOverlayLoadAddress;
            RomPatchState.flag_TrainerEncounterBGMTableRepointed = _musicTableRepointed;
            tableStart -= _musicTableRepointed ? synthOverlayLoadAddress : ARM9.address;

            uint tableSizeOffset = 10;
            if (gameFamily == GameFamilies.HGSS) tableSizeOffset += 2;

            byte entryCount = ARM9.ReadByte(encounterMusicTableOffsetToRAMAddress - tableSizeOffset);
            string tablePath = _musicTableRepointed ? Filesystem.expArmPath : arm9Path;
            using var reader = new DSUtils.EasyReader(tablePath, tableStart);
            for (int i = 0; i < entryCount; i++)
            {
                uint entryOffset = (uint)reader.BaseStream.Position;
                byte tclass = (byte)reader.ReadUInt16();
                ushort musicD = reader.ReadUInt16();
                ushort? musicN = gameFamily == GameFamilies.HGSS ? reader.ReadUInt16() : (ushort?)null;
                _musicDict[tclass] = (entryOffset, musicD, musicN);
            }
        }

        private void LoadClass(int index)
        {
            _suppress = true;
            ClassName = ClassNames[index].Substring(ClassNames[index].IndexOf(' ') + 1);

            if (_musicDict.TryGetValue((byte)index, out var entry))
            {
                MusicEnabled = true;
                MusicMain = entry.musicD;
                MusicAlt = entry.musicN ?? 0;
            }
            else
            {
                MusicEnabled = false;
                MusicMain = 0;
                MusicAlt = 0;
            }
            MusicAltEnabled = MusicEnabled && gameFamily == GameFamilies.HGSS;
            _suppress = false;
        }

        public void Save()
        {
            if (_selectedIndex < 0) return;
            byte idx = (byte)_selectedIndex;

            if (_musicDict.TryGetValue(idx, out var entry))
            {
                ushort main = (ushort)MusicMain;
                ushort alt = (ushort)MusicAlt;
                string tablePath = _musicTableRepointed ? Filesystem.expArmPath : arm9Path;
                DSUtils.WriteToFile(tablePath, BitConverter.GetBytes(main), entry.entryOffset + 2);
                if (gameFamily == GameFamilies.HGSS)
                    DSUtils.WriteToFile(tablePath, BitConverter.GetBytes(alt), entry.entryOffset + 4);
                _musicDict[idx] = (entry.entryOffset, main, gameFamily == GameFamilies.HGSS ? alt : entry.musicN);
            }

            int savedIndex = _selectedIndex;
            var ta = new TextArchive(trainerClassMessageNumber);
            ta.messages[savedIndex] = ClassName;
            ta.SaveToExpandedDir(trainerClassMessageNumber, showSuccessMessage: false);

            // Replacing the currently-selected item's text can make the ListBox re-fire its selection
            // (some containers get regenerated), reentrantly resetting _selectedIndex to -1 through the
            // SelectedClassIndex setter — _suppress only stops LoadClass from re-running, it doesn't stop
            // the field write. Restore the real index afterward rather than trusting it mid-call.
            _suppress = true;
            ClassNames[savedIndex] = $"[{savedIndex:D3}] {ClassName}";
            _selectedIndex = savedIndex;
            OnPropertyChanged(nameof(SelectedClassIndex));   // re-sync the ListBox if it reentrantly deselected
            _suppress = false;

            AppEvents.RaiseNamesChanged();
            StatusText = $"Trainer class {savedIndex} saved.";
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using global::Avalonia.Controls;
using global::Avalonia.Media.Imaging;
using DSPRE.Avalonia;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// Avalonia port of the trainer-class panel of the WinForms <c>TrainerEditor</c>: rename a trainer
    /// class and edit its "eye contact" encounter music (the SSEQ that plays when that class of trainer
    /// spots the player), a small ARM9-backed table, separate from any single trainer's own data, so it
    /// gets its own tab rather than crowding the main Trainer Editor window.
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
        private readonly TrainerClassSpriteRenderer _spriteRenderer = new();
        private int _spriteFrame;

        private readonly global::Avalonia.Threading.DispatcherTimer _animTimer;
        private int _playCountdown;
        private bool _isPlaying;
        public bool IsPlaying { get => _isPlaying; private set { if (Set(ref _isPlaying, value)) OnPropertyChanged(nameof(PlayButtonText)); } }
        public string PlayButtonText => IsPlaying ? "⏹ Stop" : "▶ Play animation";
        public bool CanPlayAnimation => _spriteRenderer.FrameCount > 1;

        public void TogglePlay()
        {
            if (IsPlaying) { IsPlaying = false; _spriteFrame = _spriteRenderer.DefaultFrame; RefreshSpritePreview(); return; }
            if (!CanPlayAnimation) return;
            _spriteFrame = 0;
            _playCountdown = _spriteRenderer.GetFrameDuration(0);
            IsPlaying = true;
            RefreshSpritePreview();
        }

        private void AnimTick()
        {
            if (!IsPlaying) return;
            if (--_playCountdown > 0) return;
            _spriteFrame = (_spriteFrame + 1) % Math.Max(1, _spriteRenderer.FrameCount);
            _playCountdown = _spriteRenderer.GetFrameDuration(_spriteFrame);
            RefreshSpritePreview();
        }

        /// <summary>Which file _musicDict's entryOffsets are relative to. The table may have been
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

        /// <summary>"Add Trainer Class" is only implemented for Platinum (English), see
        /// TrainerClassTableExpansion's doc comment for why.</summary>
        public bool IsExpansionSupported => TrainerClassTableExpansion.IsSupportedForCurrentRom;

        /// <summary>Gender editing: only known for Platinum (English) via TrainerClassTableExpansion, or hg-engine via source.</summary>
        public bool ShowGender => IsExpansionSupported || HgEngineProject.IsActive;

        /// <summary>Prize-multiplier editing: known for Plat/DP/HGSS (English) via TrainerClassTableExpansion,
        /// or hg-engine via source. Wider than <see cref="ShowGender"/> since the gender table's offsets
        /// are only confirmed for Platinum.</summary>
        public bool ShowPrizeMul => TrainerClassTableExpansion.IsPrizeMulSupportedForCurrentRom || HgEngineProject.IsActive;

        private bool _genderLoaded;
        public bool GenderLoaded { get => _genderLoaded; private set => Set(ref _genderLoaded, value); }

        private bool _prizeMulLoaded;
        public bool PrizeMulLoaded { get => _prizeMulLoaded; private set => Set(ref _prizeMulLoaded, value); }

        private int _genderIndex;
        public int GenderIndex { get => _genderIndex; set => Set(ref _genderIndex, value); }

        private int _prizeMultiplier;
        public int PrizeMultiplier { get => _prizeMultiplier; set => Set(ref _prizeMultiplier, value); }

        public bool CanEnableMusic => IsExpansionSupported && !MusicEnabled && _selectedIndex >= 0;

        private Bitmap _spritePreview;
        public Bitmap SpritePreview { get => _spritePreview; private set => Set(ref _spritePreview, value); }
        public bool HasSpritePreview => _spritePreview != null;

        public TrainerClassesViewModel() { if (Design.IsDesignMode) ClassNames.Add("[000] Youngster"); }

        public TrainerClassesViewModel(int initialClass)
        {
            _animTimer = new global::Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000.0 / 60) };
            _animTimer.Tick += (_, _) => AnimTick();
            _animTimer.Start();
            try
            {
                string[] names = GetTrainerClassNames();
                for (int i = 0; i < names.Length; i++) ClassNames.Add($"[{i:D3}] {names[i]}");

                // The eye-contact encounter-music table is found via a hardcoded vanilla ARM9 RAM
                // address (RomInfo.encounterMusicTableOffsetToRAMAddress) — meaningless on an hg-engine
                // ROM, whose ARM9 is a different compiled binary with no such table at that address.
                // Reading it there returns garbage that then overruns the file when treated as an entry
                // count, so this feature simply doesn't exist for hg-engine ROMs (mirrors IsExpansionSupported).
                if (!isHGE) SetupEncounterMusicTable();

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

            if (HgEngineProject.IsActive)
            {
                GenderLoaded = HgEngineTrainerClassTables.TryGetGender(index, out int hgeGender);
                if (GenderLoaded) GenderIndex = hgeGender;

                PrizeMulLoaded = HgEngineTrainerClassTables.TryGetPrizeMultiplier(index, out int hgePrize);
                if (PrizeMulLoaded) PrizeMultiplier = hgePrize;
            }
            else
            {
                GenderLoaded = IsExpansionSupported && TrainerClassTableExpansion.TryReadGender(index, out byte gender, out _);
                if (GenderLoaded) GenderIndex = gender;

                PrizeMulLoaded = TrainerClassTableExpansion.TryReadPrizeMul(index, out byte prizeMul, out _);
                if (PrizeMulLoaded) PrizeMultiplier = prizeMul;
            }

            IsPlaying = false;
            _spriteRenderer.Load(index);
            _spriteFrame = _spriteRenderer.DefaultFrame;
            RefreshSpritePreview();

            OnPropertyChanged(nameof(CanEnableMusic));
            OnPropertyChanged(nameof(CanPlayAnimation));
            _suppress = false;
        }

        /// <summary>Re-renders the (bigger) class-sprite preview shown at the top of this tab. Call
        /// after the sprite editor saves changes, since it edits the same NCGR/NCLR files on disk.</summary>
        public void RefreshSpritePreview()
        {
            if (_selectedIndex < 0) { SpritePreview = null; OnPropertyChanged(nameof(HasSpritePreview)); return; }
            _spriteRenderer.Load(_selectedIndex);
            SpritePreview = _spriteRenderer.HasSprite
                ? _spriteRenderer.Render(_spriteFrame, 144, 144)
                : null;
            OnPropertyChanged(nameof(HasSpritePreview));
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

            if (HgEngineProject.IsActive)
            {
                string hgeGenderErr = null, hgePrizeErr = null;
                if (GenderLoaded) HgEngineTrainerClassTables.TrySetGender(_selectedIndex, GenderIndex, out hgeGenderErr);
                if (PrizeMulLoaded) HgEngineTrainerClassTables.TrySetPrizeMultiplier(_selectedIndex, PrizeMultiplier, out hgePrizeErr);
                if (hgeGenderErr != null || hgePrizeErr != null)
                    _ = DialogHelper.ShowError($"Some fields failed to save:\n{hgeGenderErr}\n{hgePrizeErr}".Trim(), "Trainer Classes");
            }
            else
            {
                string genderErr = null, prizeErr = null;
                if (GenderLoaded) TrainerClassTableExpansion.TryWriteGender(_selectedIndex, (byte)GenderIndex, out genderErr);
                if (PrizeMulLoaded) TrainerClassTableExpansion.TryWritePrizeMul(_selectedIndex, (byte)PrizeMultiplier, out prizeErr);
                if (genderErr != null || prizeErr != null)
                    _ = DialogHelper.ShowError($"Some fields failed to save:\n{genderErr}\n{prizeErr}".Trim(), "Trainer Classes");
            }

            int savedIndex = _selectedIndex;
            var ta = new TextArchive(trainerClassMessageNumber);
            ta.messages[savedIndex] = ClassName;
            ta.SaveToExpandedDir(trainerClassMessageNumber, showSuccessMessage: false);

            // Replacing the currently-selected item's text can make the ListBox re-fire its selection
            // (some containers get regenerated), reentrantly resetting _selectedIndex to -1 through the
            // SelectedClassIndex setter; _suppress only stops LoadClass from re-running, it doesn't stop
            // the field write. Restore the real index afterward rather than trusting it mid-call.
            _suppress = true;
            ClassNames[savedIndex] = $"[{savedIndex:D3}] {ClassName}";
            _selectedIndex = savedIndex;
            OnPropertyChanged(nameof(SelectedClassIndex));   // re-sync the ListBox if it reentrantly deselected
            _suppress = false;

            AppEvents.RaiseNamesChanged();
            StatusText = $"Trainer class {savedIndex} saved.";
        }

        /// <summary>Adds an eye-contact music entry to the currently-selected class (which doesn't
        /// have one yet) instead of it just staying permanently disabled.</summary>
        public void EnableMusic(ushort musicMain, ushort musicNight)
        {
            if (!CanEnableMusic) return;
            if (!TrainerClassTableExpansion.AddEncounterMusicEntry((byte)_selectedIndex, musicMain, musicNight, out string error))
            {
                _ = DialogHelper.ShowError(error, "Trainer Classes");
                return;
            }

            _musicDict.Clear();
            SetupEncounterMusicTable();
            LoadClass(_selectedIndex);
            StatusText = "Eye-contact music enabled for this class.";
        }

        /// <summary>Adds a whole new trainer class (name/description/gender/prize multiplier, plus
        /// an optional initial music entry), then refreshes the list and selects it. Returns null on
        /// success, or an error message.</summary>
        public string AddTrainerClass(string name, string description, byte gender, byte prizeMultiplier,
            bool addMusic, ushort musicMain, ushort musicNight)
        {
            if (!TrainerClassTableExpansion.AddTrainerClass(name, description, gender, prizeMultiplier, addMusic, musicMain, musicNight, out string error))
                return error;

            string[] names = GetTrainerClassNames();
            _suppress = true;
            ClassNames.Clear();
            for (int i = 0; i < names.Length; i++) ClassNames.Add($"[{i:D3}] {names[i]}");
            _suppress = false;

            _musicDict.Clear();
            SetupEncounterMusicTable();

            StatusText = $"{ClassNames.Count} trainer classes.";
            AppEvents.RaiseNamesChanged();
            SelectedClassIndex = ClassNames.Count - 1;
            return null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Media.Imaging;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    /// <summary>
    /// Avalonia port of the trainer-class panel of the WinForms <c>TrainerEditor</c>: rename a trainer
    /// class and edit its "eye contact" encounter music (the SSEQ that plays when that class of trainer
    /// spots the player), a small ARM9-backed table, separate from any single trainer's own data, so it
    /// gets its own tab rather than crowding the main Trainer Editor window.
    /// </summary>
    public class TrainerClassesViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
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
            set
            {
                if (value == _selectedIndex) return;
                if (!_suppress && _pendingClass != null && _selectedIndex == PendingIndex) CapturePending();
                if (_dirty && !_suppress && value >= 0 && _selectedIndex >= 0)
                {
                    // Snap the list back to the class still loaded until the user has answered.
                    OnPropertyChanged(nameof(SelectedClassIndex));
                    _ = SwitchClassAsync(value);
                    return;
                }
                if (Set(ref _selectedIndex, value) && !_suppress && value >= 0) LoadClass(value);
            }
        }

        private async Task SwitchClassAsync(int requested)
        {
            if (!await ConfirmLeaveAsync()) return;
            if (Set(ref _selectedIndex, requested)) LoadClass(requested);
        }

        /// <summary>True once the loaded class has no unsaved edits, having asked to save or discard them.</summary>
        public Task<bool> ConfirmLeaveAsync() => RecordSwitchGuard.ConfirmLeaveAsync(this, null, "trainer class");

        // ── Unsaved changes ─────────────────────────────────────────────────
        private bool _dirty;

        // A music row added to the loaded class and not written yet.
        private bool _musicAdded;

        // A class added here and not written yet. It is the last list entry; Save adds it, Discard drops it.
        private sealed class PendingClass
        {
            public string Name, Description;
            public byte Gender, Prize;
            public bool AddMusic;
            public ushort MusicMain, MusicNight;
        }
        private PendingClass _pendingClass;
        private int PendingIndex => _pendingClass == null ? -1 : ClassNames.Count - 1;
        public bool CanAddClass => _pendingClass == null;

        public bool HasUnsavedChanges => _dirty || _pendingClass != null;

        public string UnsavedChangesDescription =>
            _pendingClass != null ? "New trainer class " + _pendingClass.Name
            : _selectedIndex >= 0 && _selectedIndex < ClassNames.Count ? "Trainer class " + ClassNames[_selectedIndex]
            : "Trainer class";

        public void SaveChanges() => Save();

        public void DiscardChanges()
        {
            ReloadMusicTable();
            SetClean();
            if (_pendingClass != null) DropPendingClass();
            if (_selectedIndex >= 0 && _selectedIndex < ClassNames.Count) LoadClass(_selectedIndex);
        }

        private void CapturePending()
        {
            _pendingClass.Name = ClassName;
            _pendingClass.Gender = (byte)GenderIndex;
            _pendingClass.Prize = (byte)PrizeMultiplier;
            _pendingClass.AddMusic = MusicEnabled;
            _pendingClass.MusicMain = (ushort)MusicMain;
            _pendingClass.MusicNight = (ushort)MusicAlt;
        }

        private void DropPendingClass()
        {
            int pending = PendingIndex;
            int keep = _selectedIndex == pending ? pending - 1 : _selectedIndex;
            _pendingClass = null;
            // Removing the selected row can clear the list's selection.
            _suppress = true;
            ClassNames.RemoveAt(pending);
            _selectedIndex = keep;
            _suppress = false;
            OnPropertyChanged(nameof(SelectedClassIndex));
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(CanAddClass));
        }

        private void MarkDirty()
        {
            if (_suppress || _dirty) return;
            _dirty = true;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void SetClean()
        {
            if (!_dirty) return;
            _dirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void ReloadMusicTable()
        {
            _musicDict.Clear();
            _musicAdded = false;
            if (!isHGE || _musicFromSource) SetupEncounterMusicTable();
        }

        private string _className = "";
        public string ClassName { get => _className; set { if (Set(ref _className, value)) MarkDirty(); } }

        private decimal _musicMain;
        public decimal MusicMain { get => _musicMain; set { if (Set(ref _musicMain, value)) MarkDirty(); } }

        private decimal _musicAlt;
        public decimal MusicAlt { get => _musicAlt; set { if (Set(ref _musicAlt, value)) MarkDirty(); } }

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
        public int GenderIndex { get => _genderIndex; set { if (Set(ref _genderIndex, value)) MarkDirty(); } }

        private int _prizeMultiplier;
        public int PrizeMultiplier { get => _prizeMultiplier; set { if (Set(ref _prizeMultiplier, value)) MarkDirty(); } }

        public bool CanEnableMusic => (IsExpansionSupported || _musicFromSource) && !MusicEnabled && _selectedIndex >= 0;

        // Without a linked checkout an hg-engine build would overwrite the sprite edits.
        public bool CanEditSprite => !isHGE || HgEngineProject.IsActive;

        private readonly bool _musicFromSource = HgEngineMusicTables.TablesInSource;

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

                // hg-engine repoints the eye-contact music table into its own code, so only its source can be read.
                if (!isHGE || _musicFromSource) SetupEncounterMusicTable();

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
            if (_musicFromSource)
            {
                foreach (var kv in HgEngineMusicTables.ReadEncounterMusic())
                    if (kv.Key is >= 0 and <= 255) _musicDict[(byte)kv.Key] = (0, (ushort)kv.Value.Johto, (ushort)kv.Value.Kanto);
                return;
            }
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
            if (_pendingClass != null && index == PendingIndex) { LoadPendingClass(); return; }

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
                GenderLoaded = false;
                if (IsExpansionSupported && TrainerClassTableExpansion.TryReadGender(index, out byte gender, out _))
                {
                    GenderLoaded = true;
                    GenderIndex = gender;
                }

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

        private void LoadPendingClass()
        {
            _suppress = true;
            ClassName = _pendingClass.Name;
            MusicEnabled = _pendingClass.AddMusic;
            MusicMain = _pendingClass.MusicMain;
            MusicAlt = _pendingClass.MusicNight;
            MusicAltEnabled = MusicEnabled && gameFamily == GameFamilies.HGSS;
            // The add writes a gender and a prize multiplier for the new class.
            GenderLoaded = true;
            GenderIndex = _pendingClass.Gender;
            PrizeMulLoaded = true;
            PrizeMultiplier = _pendingClass.Prize;
            IsPlaying = false;
            SpritePreview = null;
            OnPropertyChanged(nameof(HasSpritePreview));
            OnPropertyChanged(nameof(CanEnableMusic));
            OnPropertyChanged(nameof(CanPlayAnimation));
            _suppress = false;
        }

        /// <summary>Writes the loaded class, then adds the new class if one is waiting. False when any part
        /// failed, which stays unsaved.</summary>
        public bool Save()
        {
            if (_pendingClass != null && _selectedIndex == PendingIndex) CapturePending();
            if (_selectedIndex >= 0 && _selectedIndex != PendingIndex && !SaveLoadedClass()) return false;
            return _pendingClass == null || SavePendingClass();
        }

        private bool SavePendingClass()
        {
            var p = _pendingClass;
            if (!TrainerClassTableExpansion.AddTrainerClass(p.Name, p.Description, p.Gender, p.Prize, p.AddMusic, p.MusicMain, p.MusicNight, out string error))
            {
                StatusText = "The new trainer class was not added.";
                _ = DialogHelper.ShowError(error, "Add Trainer Class");
                return false;
            }

            _pendingClass = null;
            string[] names = GetTrainerClassNames();
            _suppress = true;
            ClassNames.Clear();
            for (int i = 0; i < names.Length; i++) ClassNames.Add($"[{i:D3}] {names[i]}");
            _selectedIndex = -1;
            _suppress = false;

            ReloadMusicTable();
            _dirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(CanAddClass));
            AppEvents.RaiseNamesChanged();
            SelectedClassIndex = ClassNames.Count - 1;
            StatusText = $"{ClassNames.Count} trainer classes.";
            return true;
        }

        private bool SaveLoadedClass()
        {
            if (_selectedIndex < 0) return false;
            byte idx = (byte)_selectedIndex;

            var failures = new List<string>();
            bool hasMusic = _musicDict.TryGetValue(idx, out var entry);
            if (hasMusic && _musicAdded && !_musicFromSource)
            {
                // The ROM table has no row for this class until the add, so the add carries the values.
                if (TrainerClassTableExpansion.AddEncounterMusicEntry(idx, (ushort)MusicMain, (ushort)MusicAlt, out string addErr)) ReloadMusicTable();
                else failures.Add(addErr);
            }
            else if (hasMusic && _musicFromSource)
            {
                ushort main = (ushort)MusicMain, alt = (ushort)MusicAlt;
                if (HgEngineMusicTables.TrySetEncounterMusic(idx, main, alt, out string musicErr)) { _musicDict[idx] = (0, main, alt); _musicAdded = false; }
                else failures.Add(musicErr);
            }
            else if (hasMusic)
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
                try
                {
                    if (GenderLoaded && !HgEngineTrainerClassTables.TrySetGender(_selectedIndex, GenderIndex, out string hgeGenderErr)) failures.Add(hgeGenderErr);
                    if (PrizeMulLoaded && !HgEngineTrainerClassTables.TrySetPrizeMultiplier(_selectedIndex, PrizeMultiplier, out string hgePrizeErr)) failures.Add(hgePrizeErr);
                }
                catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException) { failures.Add(ex.Message); }
            }
            else
            {
                string genderErr = null, prizeErr = null;
                if (GenderLoaded) TrainerClassTableExpansion.TryWriteGender(_selectedIndex, (byte)GenderIndex, out genderErr);
                if (PrizeMulLoaded) TrainerClassTableExpansion.TryWritePrizeMul(_selectedIndex, (byte)PrizeMultiplier, out prizeErr);
                if (genderErr != null) failures.Add(genderErr);
                if (prizeErr != null) failures.Add(prizeErr);
            }

            int savedIndex = _selectedIndex;
            if (!TrySaveClassName(savedIndex, ClassName, out string nameError))
            {
                failures.Add(nameError);
                StatusText = $"Trainer class {savedIndex} was not fully saved.";
                _ = DialogHelper.ShowError(string.Join("\n", failures), "Trainer Classes");
                return false;
            }

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
            if (failures.Count > 0)
            {
                StatusText = $"Trainer class {savedIndex} was not fully saved.";
                _ = DialogHelper.ShowError(string.Join("\n", failures), "Trainer Classes");
                return false;
            }
            SetClean();
            StatusText = $"Trainer class {savedIndex} saved.";
            return true;
        }

        // hg-engine rebuilds the class name archive from its text source, so the name has to go there.
        private static bool TrySaveClassName(int index, string name, out string error)
        {
            error = null;
            HgEngineOwnedFile owned = HgEngineProject.IsActive
                ? HgEngineOwnedFiles.Get(HgEngineOwnedFiles.ArchiveOf(DirNames.textArchives), trainerClassMessageNumber) : null;
            if (owned == null)
            {
                var ta = new TextArchive(trainerClassMessageNumber);
                ta.messages[index] = name;
                ta.SaveToExpandedDir(trainerClassMessageNumber, showSuccessMessage: false);
                return true;
            }
            if (owned.Ownership != HgEngineOwnership.EditableSource)
            {
                error = "hg-engine generates the class names, so they can't be renamed here.";
                return false;
            }
            if (!HgEngineOwnedFiles.TryReadLines(owned, out var lines, out error)) return false;
            if (index >= lines.Count) { error = $"The class name source has no line for class {index}."; return false; }
            if (lines[index] == name) return true;
            lines[index] = name;
            if (!HgEngineOwnedFiles.TryWriteLines(owned, lines, out error)) return false;
            // The same check the Text Editor runs, so a bad tag shows now rather than at build time.
            if (HgEngineBuild.TryValidateTextArchive(owned.RelPath, out string problems) && problems != null)
            {
                error = $"Saved {owned.RelPath}, but its build will reject it:\n{problems}";
                return false;
            }
            return true;
        }

        /// <summary>Adds an eye-contact music entry to the currently-selected class (which doesn't
        /// have one yet). It is written on Save, like the class's other fields.</summary>
        public void EnableMusic(ushort musicMain, ushort musicNight)
        {
            if (!CanEnableMusic) return;
            bool hgss = gameFamily == GameFamilies.HGSS;
            _musicDict[(byte)_selectedIndex] = (0, musicMain, hgss ? musicNight : (ushort?)null);
            _musicAdded = true;

            MusicEnabled = true;
            MusicAltEnabled = hgss;
            MusicMain = musicMain;
            MusicAlt = musicNight;
            MarkDirty();
            OnPropertyChanged(nameof(CanEnableMusic));
            StatusText = "Eye-contact music added. Save to keep it.";
        }

        /// <summary>Holds a new trainer class (name, description, gender, prize multiplier and an optional
        /// music entry) as the last list entry and selects it. Nothing is written until Save. Returns null,
        /// or why it can't be added.</summary>
        public string AddTrainerClass(string name, string description, byte gender, byte prizeMultiplier,
            bool addMusic, ushort musicMain, ushort musicNight)
        {
            if (_pendingClass != null) return "Save or discard the new trainer class first.";
            string refusal = TrainerClassTableExpansion.AddRefusal(name);
            if (refusal != null) return refusal;

            _pendingClass = new PendingClass
            {
                Name = name, Description = description ?? "", Gender = gender, Prize = prizeMultiplier,
                AddMusic = addMusic, MusicMain = musicMain, MusicNight = musicNight,
            };
            _suppress = true;
            ClassNames.Add($"[{ClassNames.Count:D3}] {name} (not saved)");
            _suppress = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(CanAddClass));
            SelectedClassIndex = PendingIndex;
            StatusText = "New trainer class added. Save to keep it.";
            return null;
        }
    }
}

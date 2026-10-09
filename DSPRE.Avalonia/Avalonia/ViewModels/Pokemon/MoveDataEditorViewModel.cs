using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using DSPRE.Resources;
using IEditorWithUnsavedChanges = global::DSPRE.Editors.IEditorWithUnsavedChanges;
using static DSPRE.MoveData;
using static DSPRE.RomInfo;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    // ── Per-flag observable item ──────────────────────────────────────────────
    public class FlagEntry : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public string Name { get; init; }

        private bool _isSet;
        public bool IsSet
        {
            get => _isSet;
            set { if (_isSet == value) return; _isSet = value; OnPropertyChanged(); }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    public class MoveDataEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, DSPRE.Avalonia.ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        // ─── hg-engine source banner ──────────────────────────────────────────────
        public string HgEngineBanner => DSPRE.HgEngine.HgEngineProject.BannerText;
        public bool ShowHgEngineBanner => HgEngineBanner != null;

        // Set when the move's Moves.c entry couldn't be read; saving would write the shown values over it.
        private string _sourceLoadError;
        public string SourceLoadError { get => _sourceLoadError; private set { if (Set(ref _sourceLoadError, value)) OnPropertyChanged(nameof(HasSourceLoadError)); } }
        public bool HasSourceLoadError => _sourceLoadError != null;

        // ── IEditorWithUnsavedChanges ─────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty || _pendingMove != null || _pendingImports.Count > 0;
        public string UnsavedChangesDescription =>
            _pendingMove != null ? $"New move {_pendingMove.DisplayName}"
            : _currentFile != null ? $"Move {_currentId} - {MoveNames[_currentId]}" : "Move Data Editor";
        void IEditorWithUnsavedChanges.SaveChanges() => _ = SaveCommand();
        async Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            await SaveCommand();
            return !HasUnsavedChanges;
        }
        public void DiscardChanges()
        {
            RevertCategories();
            if (_pendingMove == null && _pendingImports.Count == 0)
            {
                // The edits live in _currentFile, so reading the move again is what puts them back.
                if (_currentFile != null) LoadMove(_currentId); else SetClean();
                return;
            }
            int back = _pendingMove != null ? _returnIndex : _currentId;
            _pendingImports.Clear();
            Status = "";
            DropPendingMove();
            _selectedMoveIndex = back;
            OnPropertyChanged(nameof(SelectedMoveIndex));
            LoadMove(back);
        }

        // A new move and imported moves exist only here until Save; Discard drops them.
        private HgEngineMoveExpansion.PendingMove _pendingMove;
        private int _returnIndex;
        private readonly Dictionary<int, MoveData> _pendingImports = new();
        // Set while the list itself changes, so the selector's own index updates aren't taken as picks.
        private bool _syncingList;

        private string _status = string.Empty;
        public string Status { get => _status; private set => Set(ref _status, value); }

        // ── Move names / type names / battle sequences ─────────────────────────
        public ObservableCollection<string> MoveNames    { get; } = new();
        public ObservableCollection<string> TypeNames    { get; } = new();
        public ObservableCollection<string> SplitNames   { get; } = new();
        public ObservableCollection<string> RangeItems   { get; } = new();
        public ObservableCollection<string> BattleSeqItems { get; } = new();
        public ObservableCollection<string> ContestNames { get; } = new();
        public ObservableCollection<string> ContestEffectNames { get; } = new();
        public ObservableCollection<FlagEntry> Flags     { get; } = new();

        // ── Current move selection ─────────────────────────────────────────────
        public int MaxMoveIndex => Math.Max(0, MoveNames.Count - 1);
        private int _selectedMoveIndex;
        public int SelectedMoveIndex
        {
            get => _selectedMoveIndex;
            set
            {
                if (RecordSwitchGuard.IsSnappingBack) return;
                if (_syncingList || value == _selectedMoveIndex || value < 0 || value >= MoveNames.Count) return;
                if (_dirty || _pendingMove != null) { _ = ConfirmDiscardAsync(value); return; }
                _selectedMoveIndex = value;
                OnPropertyChanged();
                LoadMove(value);
            }
        }

        // ── Move fields ────────────────────────────────────────────────────────
        private int _typeIndex;
        public int TypeIndex { get => _typeIndex; set { if (Set(ref _typeIndex, value) && _currentFile != null) { _currentFile.movetype = (PokemonType)value; SetDirty(); } } }

        private int _splitIndex;
        public int SplitIndex { get => _splitIndex; set { if (Set(ref _splitIndex, value) && _currentFile != null) { _currentFile.split = (MoveSplit)value; SetDirty(); } } }

        private int _rangeIndex;
        public int RangeIndex { get => _rangeIndex; set { if (Set(ref _rangeIndex, value) && _currentFile != null) { _currentFile.target = AttackRangeDescriptions[value].value; SetDirty(); } } }

        private int _battleSeqIndex;
        public int BattleSeqIndex { get => _battleSeqIndex; set { if (Set(ref _battleSeqIndex, value) && _currentFile != null) { _currentFile.battleeffect = (ushort)value; SetDirty(); } } }

        private int _contestIndex;
        public int ContestIndex { get => _contestIndex; set { if (Set(ref _contestIndex, value) && _currentFile != null) { _currentFile.contestConditionType = (ContestCondition)value; SetDirty(); } } }

        private int _power;
        public int Power { get => _power; set { if (Set(ref _power, value) && _currentFile != null) { _currentFile.damage = (byte)value; SetDirty(); } } }

        private int _accuracy;
        public int Accuracy { get => _accuracy; set { if (Set(ref _accuracy, value) && _currentFile != null) { _currentFile.accuracy = (byte)value; SetDirty(); } } }

        private int _pp;
        public int PP { get => _pp; set { if (Set(ref _pp, value) && _currentFile != null) { _currentFile.pp = (byte)value; SetDirty(); } } }

        private int _priority;
        public int Priority { get => _priority; set { if (Set(ref _priority, value) && _currentFile != null) { _currentFile.priority = (sbyte)value; SetDirty(); } } }

        private int _sideEffectPct;
        // Vanilla ranges, widened for a move that already holds a value outside them so loading never changes it.
        public int AccuracyMax { get; private set; } = 100;
        public int SideEffectMax { get; private set; } = 100;
        public int PriorityMin { get; private set; } = -7;
        public int PriorityMax { get; private set; } = 5;

        private void SetLimits(int accMax, int effMax, int priMin, int priMax)
        {
            AccuracyMax = accMax; SideEffectMax = effMax; PriorityMin = priMin; PriorityMax = priMax;
            OnPropertyChanged(nameof(AccuracyMax)); OnPropertyChanged(nameof(SideEffectMax));
            OnPropertyChanged(nameof(PriorityMin)); OnPropertyChanged(nameof(PriorityMax));
        }

        public int SideEffectPct { get => _sideEffectPct; set { if (Set(ref _sideEffectPct, value) && _currentFile != null) { _currentFile.sideEffectProbability = (byte)value; SetDirty(); } } }

        private int _contestAppeal;
        public int ContestAppeal
        {
            get => _contestAppeal;
            set
            {
                // A combo with no selection reports -1; that is not a value to store.
                if (value < 0 || !Set(ref _contestAppeal, value)) return;
                OnPropertyChanged(nameof(ContestHearts));
                if (_currentFile != null) { _currentFile.contestAppeal = (byte)value; SetDirty(); }
            }
        }
        public int ContestHearts => _contestAppeal < MoveData.ContestEffectAppeal.Length ? MoveData.ContestEffectAppeal[_contestAppeal] / 10 : 0;
        public bool HasContestEffects => RomInfo.gameFamily != RomInfo.GameFamilies.HGSS;

        // Effect labels, plus plain numbers for any id past them so every stored byte can show.
        private void SyncContestEffects(int need)
        {
            List<string> labels = DSPRE.Avalonia.Data.LabelStore.Get("move_contest_effects").ToList();
            for (int i = labels.Count; i <= need; i++) labels.Add(i.ToString());
            DSPRE.Avalonia.Data.ListSync.Apply(ContestEffectNames, labels);
        }

        private string _description = string.Empty;
        public string Description
        {
            get => _description;
            set
            {
                value = (value ?? "").Replace("\r\n", "\n");
                if (Set(ref _description, value)) { RaiseTextChecks(); if (!_loading) SetDirty(); }
            }
        }

        // ── Description and TM bag text ────────────────────────────────────────
        // Kept with real line breaks here; the archives store them as \n.
        private EditableTextBank _descBank, _bagBank;
        // hg-engine keeps bag descriptions in one archive per item generation.
        private readonly Dictionary<int, EditableTextBank> _bagBanks = new();
        private int[] _machineMoves = Array.Empty<int>();
        private string _savedDescription = "", _bagText = "", _savedBagText = "";
        private int _bagItem = -1;

        public bool DescriptionReadOnly => _descBank?.ReadOnlyReason != null || _pendingMove != null;
        public string DescriptionReadOnlyReason => _pendingMove != null ? null : _descBank?.ReadOnlyReason;
        public bool HasBag => _bagItem >= 0;
        public string BagLabel { get; private set; } = "";
        public bool BagReadOnly => _bagBank?.ReadOnlyReason != null;
        public string BagText
        {
            get => _bagText;
            set
            {
                value = (value ?? "").Replace("\r\n", "\n");
                if (Set(ref _bagText, value)) { RaiseTextChecks(); if (!_loading) SetDirty(); }
            }
        }

        private static string Shown(string stored) => (stored ?? "").Replace("\\n", "\n");
        private static string Stored(string shown) => (shown ?? "").Replace("\n", "\\n");

        private void LoadTexts()
        {
            bool was = _loading;
            _loading = true;
            _savedDescription = _pendingMove == null && _descBank != null && _currentId < _descBank.Messages.Count
                ? Shown(_descBank.Messages[_currentId]) : "";
            Description = _savedDescription;
            int machine = Array.IndexOf(_machineMoves, _currentId);
            _bagItem = -1;
            if (machine >= 0 && _pendingMove == null && DSPRE.Editors.TmItemDescriptions.TryLocate(TMEditor.MachineItemId(machine), out int bank, out int line))
            {
                if (!_bagBanks.TryGetValue(bank, out _bagBank)) _bagBanks[bank] = _bagBank = new EditableTextBank(bank);
                if (line < _bagBank.Messages.Count) _bagItem = line;
            }
            BagLabel = machine >= 0 ? TMEditor.MachineLabelFromIndex(machine) : "";
            _savedBagText = _bagItem >= 0 ? Shown(_bagBank.Messages[_bagItem]) : "";
            BagText = _savedBagText;
            _loading = was;
            foreach (string n in new[] { nameof(DescriptionReadOnly), nameof(DescriptionReadOnlyReason), nameof(HasBag), nameof(BagLabel), nameof(BagReadOnly) })
                OnPropertyChanged(n);
            if (IsBagTab && !HasBag) PreviewTab = 0;
            RefreshPreview();
        }

        /// <summary>Writes the description and bag text when they changed; returns an error, or null.</summary>
        private string SaveTexts()
        {
            if (_descBank != null && _description != _savedDescription && _currentId < _descBank.Messages.Count)
            {
                _descBank.Messages[_currentId] = Stored(_description);
                if (_descBank.Save(this) is string error) return error;
                _savedDescription = _description;
            }
            if (_bagItem >= 0 && _bagText != _savedBagText)
            {
                _bagBank.Messages[_bagItem] = Stored(_bagText);
                if (_bagBank.Save(this) is string error) return error;
                _savedBagText = _bagText;
            }
            return null;
        }

        // The game prints these as stored, without wrapping, so anything past the box is cut off.
        public const int DescriptionWidth = 120, DescriptionLines = 5, BagLines = 3;
        private FieldFont _systemFont;
        private bool _systemFontTried;
        public FieldFont SystemFont
        {
            get
            {
                if (_systemFontTried) return _systemFont;
                _systemFontTried = true;
                try { _systemFont = FieldFontCharacters.Ready ? FieldFont.LoadSystemFont() : null; }
                catch (Exception ex) { AppLogger.Warn("Move Data Editor: system font not read: " + ex.Message); }
                return _systemFont;
            }
        }
        public string DescriptionWarning => Overflow(_description, DescriptionWidth, DescriptionLines);
        public string BagWarning => Overflow(_bagText, DSPRE.Avalonia.Data.MoveTextScreens.BagTextWidth, BagLines);
        public bool HasDescriptionWarning => DescriptionWarning != null;
        public bool HasBagWarning => BagWarning != null;

        private string Overflow(string text, int width, int maxLines)
        {
            List<string> lines = (text ?? "").Split('\n').ToList();
            if (lines.Count > 1 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
            if (lines.Count > maxLines) return $"Only {maxLines} lines show";
            if (SystemFont == null) return null;
            for (int i = 0; i < lines.Count; i++)
            {
                int over = SystemFont.Measure(lines[i], FieldFontCharacters.GlyphFor) - width;
                if (over > 0) return $"Line {i + 1} runs {over} px past the box";
            }
            return null;
        }

        private void RaiseTextChecks()
        {
            foreach (string n in new[] { nameof(DescriptionWarning), nameof(HasDescriptionWarning), nameof(BagWarning), nameof(HasBagWarning) })
                OnPropertyChanged(n);
            RefreshPreview();
        }

        // ── In-game preview ───────────────────────────────────────────────────
        private readonly DSPRE.Avalonia.Data.MoveTextScreens _screens = new();
        private int _previewTab;
        /// <summary>0 summary, 1 battle, 2 relearner, 3 the bag's TM pocket.</summary>
        public int PreviewTab
        {
            get => _previewTab;
            set { if (Set(ref _previewTab, value)) { OnPropertyChanged(nameof(IsBagTab)); RefreshPreview(); } }
        }
        public bool IsBagTab => _previewTab == 3;
        public global::Avalonia.Media.Imaging.Bitmap Preview { get; private set; }

        private void RefreshPreview()
        {
            if (_descBank == null) return;
            MoveTextScreens.Screen screen = (DSPRE.Avalonia.Data.MoveTextScreens.Screen)Math.Clamp(_previewTab, 0, 3);
            byte[] rgba = _screens.Render(screen, screen == DSPRE.Avalonia.Data.MoveTextScreens.Screen.Bag ? _bagText : _description, SystemFont);
            Preview = rgba == null ? null : DSPRE.Avalonia.ImageConverter.FromRgba(rgba, 256, 192);
            OnPropertyChanged(nameof(Preview));
        }

        private sealed record UndoState(byte[] Move, string Description, string Bag, bool Punching, bool Sound);
        private byte[] Snapshot() => DSPRE.Avalonia.UndoJson.Take(new UndoState(_currentFile.ToByteArray(), _description, _bagText, IsPunching, IsSound));

        // ── Ability based lists: the punching and sound moves the battle code keeps as tables ──
        private MoveCategoryTable _punching, _sound;
        private List<ushort> _savedPunching, _savedSound;
        public string AbilityWhyNot { get; private set; }
        public bool HasAbilityWhyNot => AbilityWhyNot != null;
        public bool HasPunchingList => _punching != null;
        public bool HasSoundList => _sound != null;

        /// <summary>Raised when a list still sits in the game's overlay; the view offers the expansion patch, then applies the tick.</summary>
        public event Action<MoveCategoryTable.Kind, bool> NeedsExpansion;

        /// <summary>Raised whenever either list's membership may have changed, for windows showing a list.</summary>
        public event Action ListsChanged;

        /// <summary>The moves on a list as held here now, unsaved ticks included, lowest id first.</summary>
        public List<(int Id, string Name)> ListedMoves(MoveCategoryTable.Kind kind)
        {
            MoveCategoryTable table = kind == MoveCategoryTable.Kind.Punching ? _punching : _sound;
            List<(int Id, string Name)> rows = new List<(int Id, string Name)>();
            if (table == null) return rows;
            foreach (ushort id in table.Moves.OrderBy(m => m))
                rows.Add((id, id < MoveNames.Count ? MoveNames[id] : $"Move {id}"));
            return rows;
        }

        public bool IsPunching
        {
            get => _punching != null && _punching.Contains(_currentId);
            set => SetListed(_punching, MoveCategoryTable.Kind.Punching, value, nameof(IsPunching), nameof(PunchingNote));
        }

        public bool IsSound
        {
            get => _sound != null && _sound.Contains(_currentId);
            set => SetListed(_sound, MoveCategoryTable.Kind.Sound, value, nameof(IsSound), nameof(SoundNote));
        }

        private void SetListed(MoveCategoryTable table, MoveCategoryTable.Kind kind, bool listed, string flag, string note)
        {
            if (table == null || _loading || _currentFile == null) return;
            if (listed == table.Contains(_currentId)) return;
            // The game's own list has no room to speak of, so every change goes to the moved list.
            if (!table.InExpansion) { OnPropertyChanged(flag); NeedsExpansion?.Invoke(kind, listed); return; }
            if (!table.Set(_currentId, listed))
            {
                OnPropertyChanged(flag);
                Status = $"The {MoveCategoryTable.NameOf(kind)} move list is full.";
                return;
            }
            OnPropertyChanged(flag);
            OnPropertyChanged(note);
            SetDirty();
            ListsChanged?.Invoke();
        }

        public string PunchingNote => _punching == null ? null : "Iron Fist boosts these. The trainer AI counts the boost itself.";
        public string SoundNote => _sound == null ? null : "Soundproof blocks these. The trainer AI keeps its own sound list; the guide shows how to edit it.";

        private void LoadCategories()
        {
            _punching = _sound = null;
            _savedPunching = _savedSound = null;
            AbilityWhyNot = null;
            if (HgEngineProject.IsActive) { AbilityWhyNot = "hg-engine keeps these lists in its own source."; }
            else
            {
                try
                {
                    if (MoveCategoryTable.WhyNot(MoveCategoryTable.Kind.Punching) is string why) AbilityWhyNot = why;
                    else
                    {
                        _punching = MoveCategoryTable.Load(MoveCategoryTable.Kind.Punching);
                        _sound = MoveCategoryTable.Load(MoveCategoryTable.Kind.Sound);
                        _savedPunching = _punching.Moves.ToList();
                        _savedSound = _sound.Moves.ToList();
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is InvalidOperationException)
                {
                    _punching = _sound = null;
                    AbilityWhyNot = ex.Message;
                }
            }
            RaiseCategories();
        }

        /// <summary>Shows the ticks as the lists hold them, after a declined or failed expansion.</summary>
        public void RefreshCategories() => RaiseCategories();

        private void RaiseCategories()
        {
            foreach (string n in new[] { nameof(IsPunching), nameof(IsSound), nameof(PunchingNote), nameof(SoundNote),
                                         nameof(AbilityWhyNot), nameof(HasAbilityWhyNot), nameof(HasPunchingList), nameof(HasSoundList) })
                OnPropertyChanged(n);
            ListsChanged?.Invoke();
        }

        private bool CategoriesChanged =>
            (_punching != null && !_punching.Moves.SequenceEqual(_savedPunching)) || (_sound != null && !_sound.Moves.SequenceEqual(_savedSound));

        private void RevertCategories()
        {
            if (_punching != null) { _punching.Moves.Clear(); _punching.Moves.AddRange(_savedPunching); }
            if (_sound != null) { _sound.Moves.Clear(); _sound.Moves.AddRange(_savedSound); }
            RaiseCategories();
        }

        /// <summary>Writes both lists when they changed; the move itself is already saved by then.</summary>
        private string SaveCategories()
        {
            if (!CategoriesChanged) return null;
            try
            {
                if (_punching != null && !_punching.Moves.SequenceEqual(_savedPunching)) { _punching.Save(); _savedPunching = _punching.Moves.ToList(); }
                if (_sound != null && !_sound.Moves.SequenceEqual(_savedSound)) { _sound.Save(); _savedSound = _sound.Moves.ToList(); }
                return null;
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is InvalidOperationException || ex is UnauthorizedAccessException)
            {
                return ex.Message;
            }
        }

        private string _title = "Move Data Editor";
        public string Title { get => _title; private set => Set(ref _title, value); }

        // ── Private state ──────────────────────────────────────────────────────
        private MoveData _currentFile;
        private int _currentId;
        private bool _loading;

        // ── Undo / redo (ISupportsUndo) ────────────────────────────────────────
        // Snapshots are the move file's bytes; consecutive edits within CoalesceMs collapse into one step.
        private readonly DSPRE.Avalonia.UndoHistory<byte[]> _history = new();
        private System.DateTime _lastCaptureUtc = System.DateTime.MinValue;
        private const int CoalesceMs = 500;

        public bool CanUndo => _history.CanUndo;
        public bool CanRedo => _history.CanRedo;
        public void Undo() { if (_history.CanUndo) ApplyState(_history.Undo()); }
        public void Redo() { if (_history.CanRedo) ApplyState(_history.Redo()); }

        private void RaiseUndoState() { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); }

        private void RecordUndoSnapshot()
        {
            if (_currentFile == null) return;
            bool coalesce = (System.DateTime.UtcNow - _lastCaptureUtc).TotalMilliseconds < CoalesceMs;
            _history.Capture(Snapshot(), coalesce);
            _lastCaptureUtc = System.DateTime.UtcNow;
            RaiseUndoState();
        }

        private void ApplyState(byte[] bytes)
        {
            if (bytes == null) return;
            UndoState state = DSPRE.Avalonia.UndoJson.Read<UndoState>(bytes);
            _loading = true;
            _currentFile = new MoveData(new MemoryStream(state.Move));
            PopulateFromCurrentFile();
            Description = state.Description;
            BagText = state.Bag;
            _punching?.Set(_currentId, state.Punching);
            _sound?.Set(_currentId, state.Sound);
            _loading = false;
            RaiseCategories();

            _dirty = _history.IsDirty;
            RefreshDirty();
            RaiseUndoState();
        }
        private Dictionary<string, int> _typeNameToId;
        private Dictionary<string, MoveSplit> _splitNameToEnum;
        private Dictionary<string, ushort> _rangeNameToValue;

        // ── Constructor ────────────────────────────────────────────────────────
        public MoveDataEditorViewModel()
        {
            if (Design.IsDesignMode)
            {
                // Provide dummy data so the UI renders
                for (int i = 1; i <= 10; i++) MoveNames.Add($"Dummy Move {i}");
                for (int i = 1; i <= 5; i++) TypeNames.Add($"Type {i}");
                SplitNames.Add("Physical"); SplitNames.Add("Special");
                RangeItems.Add("Single target");
                BattleSeqItems.Add("001 - Dummy Effect");
                ContestNames.Add("Cool");
                Flags.Add(new FlagEntry { Name = "Dummy Flag" });
                Description = "Design-time preview, no ROM loaded";
                Title = "Move Data Editor (Preview)";
                _selectedMoveIndex = 0;
                _typeIndex = 0;
                _splitIndex = 0;
                _rangeIndex = 0;
                _battleSeqIndex = 0;
                _contestIndex = 0;
                _power = 40;
                _accuracy = 100;
                _pp = 20;
                _priority = 0;
                _sideEffectPct = 0;
                _contestAppeal = 0;
                return;
            }
            _descBank = new EditableTextBank(moveDescriptionsTextNumbers);
            try { _machineMoves = TMEditor.ReadMachineMoves(); }
            catch (Exception ex) { AppLogger.Warn("Move Data Editor: TM moves not read: " + ex.Message); }

            string[] moveNames = GetAttackNames();
            string[] typeNames = GetTypeNames();
            string[] battleSeqFiles = GetBattleEffectSequenceFiles();
            string[] db = PokeDatabase.MoveData.battleSequenceDescriptions;

            foreach (string n in moveNames) MoveNames.Add(n);
            OnPropertyChanged(nameof(MaxMoveIndex));
            LoadCategories();
            foreach (string n in typeNames) TypeNames.Add(n);
            // Split / contest dropdowns come from the customisable LabelStore (Tools ▸ Edit Dropdown Labels).
            ReloadSplitContest();
            foreach ((ushort value, string name, string description) r in AttackRangeDescriptions) RangeItems.Add($"{r.name}: {r.description}");

            for (int i = 0; i < battleSeqFiles.Length; i++)
                BattleSeqItems.Add(i < db.Length && db[i] != null ? $"{i:D3} - {db[i]}" : $"{i:D3} - Undocumented");

            foreach (string flagName in Enum.GetNames(typeof(MoveFlags)).Skip(1))
            {
                FlagEntry entry = new FlagEntry { Name = flagName };
                entry.PropertyChanged += (_, __) => { if (!_loading && _currentFile != null) { RebuildFlagField(); SetDirty(); } };
                Flags.Add(entry);
            }

            BuildLookupDictionaries(typeNames);

            if (MoveNames.Count > 1)
            {
                _selectedMoveIndex = 1;
                LoadMove(1);
            }
        }

        private void ReloadSplitContest()
        {
            DSPRE.Avalonia.Data.LabelStore.Sync(SplitNames,   "move_split");
            DSPRE.Avalonia.Data.LabelStore.Sync(ContestNames, "move_contest_conditions");
            SyncContestEffects(_contestAppeal);
            AppEvents.LabelsChanged -= OnLabelsChanged; AppEvents.LabelsChanged += OnLabelsChanged;
            AppEvents.NamesChanged  -= OnNamesChanged;  AppEvents.NamesChanged  += OnNamesChanged;
            AppEvents.RomPatchStateChanged -= OnPatchStateChanged; AppEvents.RomPatchStateChanged += OnPatchStateChanged;
        }

        // A list moved by its toolbox patch is reread, unless ticks made here are still unsaved.
        private void OnPatchStateChanged(object sender, EventArgs e)
        {
            if (!CategoriesChanged) LoadCategories();
        }
        private void OnLabelsChanged(object sender, EventArgs e)
        {
            ReloadSplitContest();
            // Re-resolve the combos' displayed text after the label lists were replaced in place.
            Repoke(_splitIndex,   nameof(SplitIndex),   v => _splitIndex = v);
            Repoke(_contestIndex, nameof(ContestIndex), v => _contestIndex = v);
            Repoke(_contestAppeal, nameof(ContestAppeal), v => _contestAppeal = v);
        }
        private void OnNamesChanged(object sender, EventArgs e)
        {
            // Move names live in a ROM text archive; refresh when the Text editor saves.
            SyncNames();
        }

        /// <summary>The ROM's move names, with an unsaved new move shown at its id.</summary>
        private void SyncNames()
        {
            List<string> names = RomInfo.GetAttackNames().ToList();
            if (_pendingMove != null)
            {
                while (names.Count < _pendingMove.Id) names.Add("");
                names.Insert(_pendingMove.Id, _pendingMove.DisplayName + " (not saved)");
            }
            _syncingList = true;
            try { DSPRE.Avalonia.Data.ListSync.Apply(MoveNames, names); }
            finally { _syncingList = false; }
            OnPropertyChanged(nameof(SelectedMoveIndex));
        }

        private void DropPendingMove()
        {
            if (_pendingMove == null) return;
            _pendingMove = null;
            SyncNames();
        }

        private static MoveData Copy(MoveData move) => new MoveData(new MemoryStream(move.ToByteArray()));

        private void Repoke(int current, string name, Action<int> set)
        {
            if (current < 0) return;
            set(-1); OnPropertyChanged(name);
            global::Avalonia.Threading.Dispatcher.UIThread.Post(
                () => { set(current); OnPropertyChanged(name); },
                global::Avalonia.Threading.DispatcherPriority.Background);
        }
        /// <summary>Unsubscribes from app-wide events; call when the editor window closes.</summary>
        public void Detach() { AppEvents.LabelsChanged -= OnLabelsChanged; AppEvents.NamesChanged -= OnNamesChanged; AppEvents.RomPatchStateChanged -= OnPatchStateChanged; }

        // ── Commands ──────────────────────────────────────────────────────────

        public async Task SaveCommand()
        {
            if (_currentFile == null) return;
            if (HgEngineProject.IsActive && SourceLoadError != null)
            {
                await DialogHelper.ShowError($"Move {_currentId} was not saved.\n{SourceLoadError}", "Move Data Editor");
                return;
            }
            int id = _currentId;
            MoveData move = _currentFile;
            HgEngineMoveExpansion.PendingMove pending = _pendingMove;
            // The shown record wins over an import staged for the same move.
            Dictionary<int, MoveData> records = new Dictionary<int, MoveData>(_pendingImports) { [id] = move };
            string subject = pending != null ? pending.DisplayName : records.Count > 1 ? $"{records.Count} moves" : $"Move {id}";

            // The source is what the next sync rebuilds from, so a save that can't reach it is no save.
            if (HgEngineProject.IsActive)
            {
                (bool saved, string error) = await HgEngineSave.RunAsync(() => pending != null
                    ? (HgEngineMoveExpansion.TryCommitMove(pending, move, out string addError) ? null : addError)
                    : (HgEngineMoveSource.TryWriteMany(records, out string writeError) ? null : writeError));
                if (!saved)
                {
                    if (error != null) await DialogHelper.ShowError($"{subject} not saved.\n{error}", "Move Data Editor");
                    return;
                }
            }

            _pendingImports.Clear();
            Status = string.Empty;
            if (pending != null)
            {
                HgEngineMoveExpansion.CompleteAdd(pending);
                _pendingMove = null;
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.moveData });
                move.SaveToFileDefaultDir(id, showSuccessMessage: false);
                SyncNames();
                AppEvents.RaiseNamesChanged();
                _selectedMoveIndex = id;
                OnPropertyChanged(nameof(SelectedMoveIndex));
                LoadMove(id);   // reads back through the new define
            }
            else
            {
                foreach ((int importId, MoveData imported) in records)
                    if (importId != id) imported.SaveToFileDefaultDir(importId, showSuccessMessage: false);
                move.SaveToFileDefaultDir(id, showSuccessMessage: false);
                if (SaveTexts() is string textError)
                {
                    await DialogHelper.ShowError($"The text of move {id} was not saved.\n{textError}", "Move Data Editor");
                    return;
                }
                if (SaveCategories() is string listError)
                {
                    await DialogHelper.ShowError($"Move {id} was saved, but its punching or sound listing was not.\n{listError}", "Move Data Editor");
                    return;
                }
                _history.MarkSaved();   // current state is now the on-disk baseline (undo can still go past it)
            }
            SetClean();
            SaveNotice.Saved(UnsavedChangesDescription);
            RaiseUndoState();
        }

        /// <summary>On hg-engine the move comes from Moves.c; the built copy only fills a field the entry lacks,
        /// which the save then reports.</summary>
        private static MoveData LoadRecord(int id, out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) return new MoveData(id);
            string built = Path.Combine(gameDirs[DirNames.moveData].unpackedDir, id.ToString("D4"));
            MoveData move = File.Exists(built) ? new MoveData(id) : new MoveData(new MemoryStream(new byte[16]));
            HgEngineMoveSource.TryLoad(id, move, out error);
            return move;
        }

        /// <summary>hg-engine-only: shapes a brand new move and opens it for editing. Nothing is written
        /// until Save, and Discard drops it.</summary>
        public async Task AddNewMoveAsync(Window owner)
        {
            if (!HgEngineProject.IsActive) return;
            if (_pendingMove != null || _pendingImports.Count > 0)
            {
                string first = _pendingMove != null ? "Save or discard the new move first." : "Save or discard the imported moves first.";
                await DialogHelper.ShowError(first, "Add New Move", owner);
                return;
            }
            if (_dirty && !await DialogHelper.AskYesNo("There are unsaved changes to the current move. Discard and proceed?", "Unsaved Changes", owner))
                return;

            string name = await DialogHelper.PromptText("New move's display name:", "Add New Move", owner: owner);
            if (name == null) return;

            MoveData move = new MoveData(new MemoryStream(new byte[16]));
            if (!HgEngineMoveExpansion.TryPrepareMove(name, out HgEngineMoveExpansion.PendingMove pending, out string error)
                || !HgEngineMoveExpansion.TryReadTemplate(pending, move, out error))
            {
                await DialogHelper.ShowError($"Could not add the move:\n{error}", "Add New Move", owner);
                return;
            }

            _returnIndex = _selectedMoveIndex;
            _pendingMove = pending;
            SyncNames();

            _loading = true;
            _currentId = pending.Id;
            _currentFile = move;
            SourceLoadError = null;
            PopulateFromCurrentFile();
            _loading = false;
            _selectedMoveIndex = pending.Id;
            OnPropertyChanged(nameof(SelectedMoveIndex));

            _dirty = false;
            LoadTexts();
            _history.Reset(Snapshot());
            _lastCaptureUtc = System.DateTime.MinValue;
            RefreshDirty();
            RaiseUndoState();
        }

        public async Task ExportCommand(Window owner)
        {
            string path = await DialogHelper.SaveFile(owner, "Export Move Data to CSV",
                new[] { DialogHelper.CsvFilter, DialogHelper.AllFilter }, "MoveData.csv");
            if (path == null) return;

            try
            {
                string[] typeNames = GetTypeNames();
                // What is on disk, without this editor's unsaved moves.
                string[] names = RomInfo.GetAttackNames();
                SortedDictionary<int, MoveData> moves = new SortedDictionary<int, MoveData>();
                List<string> skipped = new List<string>();
                if (HgEngineProject.IsActive)
                {
                    string builtDir = gameDirs[DirNames.moveData].unpackedDir;
                    MoveData BuiltOrEmpty(int id) => File.Exists(Path.Combine(builtDir, id.ToString("D4"))) ? new MoveData(id) : new MoveData(new MemoryStream(new byte[16]));
                    if (!HgEngineMoveSource.TryLoadMany(Enumerable.Range(0, names.Length), BuiltOrEmpty, out moves, out skipped, out string loadError))
                    {
                        await DialogHelper.ShowError($"Error exporting: {loadError}", "Export Error");
                        return;
                    }
                }
                else
                {
                    for (int i = 0; i < names.Length; i++) moves[i] = new MoveData(i);
                }

                using (StreamWriter writer = new StreamWriter(path))
                {
                    writer.WriteLine("Move ID,Move Name,Move Type,Move Split,Power,Accuracy,Priority,Side Effect Probability,PP,Range");
                    foreach ((int i, MoveData move) in moves)
                    {
                        string typeStr  = (int)move.movetype < typeNames.Length ? typeNames[(int)move.movetype] : $"UnknownType_{(int)move.movetype}";
                        string rangeStr = MoveData.GetAttackRangeName(move.target);
                        writer.WriteLine($"{i},{names[i]},{typeStr},{move.split},{move.damage},{move.accuracy},{move.priority},{move.sideEffectProbability},{move.pp},{rangeStr}");
                    }
                }

                string message = $"Move data exported to:\n{path}";
                if (skipped.Count > 0)
                    message += $"\n\n{skipped.Count} move(s) skipped:\n" + string.Join("\n", skipped.Take(10))
                             + (skipped.Count > 10 ? $"\n...and {skipped.Count - 10} more" : "");
                await DialogHelper.ShowInfo(message, "Export Complete");
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError($"Error exporting: {ex.Message}", "Export Error");
            }
        }

        public async Task ImportCommand(Window owner)
        {
            if (_pendingMove != null)
            {
                await DialogHelper.ShowError("Save or discard the new move first.", "Import CSV", owner);
                return;
            }
            string path = await DialogHelper.OpenFile(owner, "Import Move Data from CSV",
                new[] { DialogHelper.CsvFilter, DialogHelper.AllFilter });
            if (path == null) return;

            string[] typeNamesArr = GetTypeNames();
            MoveDataImportResult result = ValidateAndParseCSV(path, typeNamesArr);

            // Build preview text
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Total rows read:  {result.TotalRowsRead}");
            sb.AppendLine($"Valid entries:    {result.ValidCount}");
            sb.AppendLine($"Errors:           {result.ErrorCount}");
            sb.AppendLine($"Warnings:         {result.Warnings.Count}");
            sb.AppendLine($"Name mismatches:  {result.UniqueNameMismatches.Count}");

            if (result.HasErrors)
            {
                sb.AppendLine("\nERRORS:");
                foreach (MoveImportError e in result.Errors) sb.AppendLine($"  {e}");
            }
            if (result.HasWarnings)
            {
                sb.AppendLine("\nWARNINGS:");
                foreach (MoveImportWarning w in result.Warnings) sb.AppendLine($"  {w}");
            }
            if (result.ValidCount == 0)
            {
                await DialogHelper.ShowError(sb.ToString(), "Import: No Valid Entries");
                return;
            }

            sb.AppendLine($"\n{result.ValidCount} move(s) will be imported. Save writes them. Proceed?");
            bool proceed = await DialogHelper.AskYesNo(sb.ToString(), "Confirm Import");
            if (!proceed) return;

            await StageImportedData(result.ValidEntries);
        }



        // ── Private helpers ────────────────────────────────────────────────────
        private void SetDirty() { if (_loading) return; _dirty = true; RefreshDirty(); RecordUndoSnapshot(); }
        private void SetClean() { _dirty = false; RefreshDirty(); }
        private void RefreshDirty() { Title = HasUnsavedChanges ? "● Move Data Editor" : "Move Data Editor"; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        private async Task ConfirmDiscardAsync(int newIndex)
        {
            bool discard = _pendingMove != null
                ? await DialogHelper.AskYesNo("The new move is not saved. Discard it and proceed?", "Unsaved Changes")
                : await RecordSwitchGuard.ConfirmLeaveAsync(this, null, "move");
            if (!discard) { RecordSwitchGuard.SnapBack(() => _selectedMoveIndex, v => _selectedMoveIndex = v, () => OnPropertyChanged(nameof(SelectedMoveIndex))); return; }
            _dirty = false;
            RevertCategories();
            if (_pendingMove != null)
            {
                // The list held the new move at its id, so a later pick sits one lower once it is gone.
                if (newIndex > _pendingMove.Id) newIndex--;
                DropPendingMove();
                newIndex = Math.Min(newIndex, MoveNames.Count - 1);
            }
            _selectedMoveIndex = newIndex;
            OnPropertyChanged(nameof(SelectedMoveIndex));
            LoadMove(newIndex);
        }

        private void LoadMove(int id)
        {
            _loading = true;
            _currentId   = id;
            string loadError = null;
            _currentFile = _pendingImports.TryGetValue(id, out MoveData imported) ? Copy(imported) : LoadRecord(id, out loadError);
            SourceLoadError = loadError;
            PopulateFromCurrentFile();
            LoadTexts();
            SetClean();
            _loading = false;
            RaiseCategories();

            // Loaded state is the clean baseline for undo on this move; switching moves starts fresh history.
            _history.Reset(Snapshot());
            _lastCaptureUtc = System.DateTime.MinValue;
            RaiseUndoState();
        }

        /// <summary>Pushes <see cref="_currentFile"/> into the bound fields. Caller must guard with _loading.</summary>
        private void PopulateFromCurrentFile()
        {
            // A box clamps its value when its limit drops, and the clamp would be written into this move,
            // so the limits only narrow once the new values are in.
            int acc = _currentFile.accuracy, eff = _currentFile.sideEffectProbability, pri = _currentFile.priority;
            SetLimits(Math.Max(Math.Max(100, acc), _accuracy), Math.Max(Math.Max(100, eff), _sideEffectPct),
                      Math.Min(Math.Min(-7, pri), _priority), Math.Max(Math.Max(5, pri), _priority));
            TypeIndex       = (int)_currentFile.movetype;
            SplitIndex      = (int)_currentFile.split;
            BattleSeqIndex  = (int)_currentFile.battleeffect;
            ContestIndex    = (int)_currentFile.contestConditionType;
            Power           = _currentFile.damage;
            Accuracy        = acc;
            PP              = _currentFile.pp;
            Priority        = pri;
            SideEffectPct   = eff;
            SyncContestEffects(_currentFile.contestAppeal);
            ContestAppeal   = _currentFile.contestAppeal;
            SetLimits(Math.Max(100, acc), Math.Max(100, eff), Math.Min(-7, pri), Math.Max(5, pri));

            // Range
            int rangeIdx = 0;
            for (int i = 0; i < AttackRangeDescriptions.Length; i++)
                if (AttackRangeDescriptions[i].value == _currentFile.target) { rangeIdx = i; break; }
            _rangeIndex = rangeIdx;
            OnPropertyChanged(nameof(RangeIndex));

            // Flags
            string[] flagNames = Enum.GetNames(typeof(MoveFlags)).Skip(1).ToArray();
            for (int i = 0; i < Flags.Count && i < flagNames.Length; i++)
                Flags[i].IsSet = (_currentFile.flagField & (1 << i)) != 0;
        }

        private void RebuildFlagField()
        {
            if (_currentFile == null) return;
            byte field = 0;
            for (int i = 0; i < Flags.Count; i++)
                if (Flags[i].IsSet) field |= (byte)(1 << i);
            _currentFile.flagField = field;
        }

        private void BuildLookupDictionaries(string[] typeNames)
        {
            _typeNameToId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < typeNames.Length; i++)
                if (!string.IsNullOrEmpty(typeNames[i]) && !_typeNameToId.ContainsKey(typeNames[i]))
                    _typeNameToId[typeNames[i]] = i;

            _splitNameToEnum = new Dictionary<string, MoveSplit>(StringComparer.OrdinalIgnoreCase);
            foreach (MoveSplit s in Enum.GetValues(typeof(MoveSplit)))
                _splitNameToEnum[s.ToString()] = s;

            _rangeNameToValue = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
            foreach ((ushort value, string name, string description) r in AttackRangeDescriptions)
                _rangeNameToValue[r.name] = r.value;
        }

        private MoveDataImportResult ValidateAndParseCSV(string filePath, string[] typeNames)
        {
            MoveDataImportResult result = new MoveDataImportResult();
            try
            {
                string[] lines = File.ReadAllLines(filePath);
                if (lines.Length == 0) { result.Errors.Add(new MoveImportError(0, "File is empty.")); return result; }

                string[] header = lines[0].Split(',');
                if (header.Length < 10 || !header[0].Trim().Equals("Move ID", StringComparison.OrdinalIgnoreCase))
                { result.Errors.Add(new MoveImportError(1, "Invalid CSV header.")); return result; }

                result.TotalRowsRead = lines.Length - 1;

                for (int i = 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;
                    string[] parts = ParseCSVLine(lines[i]);
                    if (parts.Length < 10) { result.Errors.Add(new MoveImportError(i + 1, $"Expected 10 columns, got {parts.Length}.")); continue; }

                    MoveRowValidationResult rowResult = ValidateRow(i + 1, parts, typeNames);
                    result.Warnings.AddRange(rowResult.Warnings);
                    result.NameMismatches.AddRange(rowResult.NameMismatches);
                    if (rowResult.IsValid) result.ValidEntries.Add(rowResult.Entry);
                    else result.Errors.AddRange(rowResult.Errors);
                }
            }
            catch (Exception ex) { result.Errors.Add(new MoveImportError(0, $"Failed to read file: {ex.Message}")); }
            return result;
        }

        private static string[] ParseCSVLine(string line)
        {
            List<string> list = new List<string>();
            StringBuilder cur  = new StringBuilder();
            bool inQ = false;
            foreach (char c in line)
            {
                if (c == '"')  inQ = !inQ;
                else if (c == ',' && !inQ) { list.Add(cur.ToString().Trim()); cur.Clear(); }
                else cur.Append(c);
            }
            list.Add(cur.ToString().Trim());
            return list.ToArray();
        }

        private MoveRowValidationResult ValidateRow(int lineNumber, string[] parts, string[] typeNames)
        {
            MoveRowValidationResult res = new MoveRowValidationResult { LineNumber = lineNumber };
            MoveDataImportEntry entry = new MoveDataImportEntry();

            if (!int.TryParse(parts[0].Trim(), out int moveId) || moveId < 0 || moveId >= MoveNames.Count)
            { res.Errors.Add(new MoveImportError(lineNumber, $"Invalid Move ID '{parts[0]}'.")); }
            else
            {
                entry.MoveID   = moveId;
                entry.MoveName = MoveNames[moveId];
                string csvName = parts[1].Trim();
                if (!csvName.Equals(MoveNames[moveId], StringComparison.OrdinalIgnoreCase))
                {
                    res.Warnings.Add(new MoveImportWarning(lineNumber, $"Name mismatch for ID {moveId}: ROM='{MoveNames[moveId]}', CSV='{csvName}'."));
                    res.NameMismatches.Add(new MoveNameMismatch(moveId, MoveNames[moveId], csvName, lineNumber));
                }
            }

            if (_typeNameToId.TryGetValue(parts[2].Trim(), out int typeId)) entry.MoveType = (PokemonType)typeId;
            else res.Errors.Add(new MoveImportError(lineNumber, $"Unknown type '{parts[2]}'."));

            if (_splitNameToEnum.TryGetValue(parts[3].Trim(), out MoveSplit split)) entry.Split = split;
            else res.Errors.Add(new MoveImportError(lineNumber, $"Unknown split '{parts[3]}'."));

            if (byte.TryParse(parts[4].Trim(), out byte power))   entry.Power = power;
            else res.Errors.Add(new MoveImportError(lineNumber, $"Invalid power '{parts[4]}'."));

            if (byte.TryParse(parts[5].Trim(), out byte acc))     entry.Accuracy = acc;
            else res.Errors.Add(new MoveImportError(lineNumber, $"Invalid accuracy '{parts[5]}'."));

            if (sbyte.TryParse(parts[6].Trim(), out sbyte prio))  entry.Priority = prio;
            else res.Errors.Add(new MoveImportError(lineNumber, $"Invalid priority '{parts[6]}'."));

            if (byte.TryParse(parts[7].Trim(), out byte fx))      entry.SideEffectProbability = fx;
            else res.Errors.Add(new MoveImportError(lineNumber, $"Invalid effect% '{parts[7]}'."));

            if (byte.TryParse(parts[8].Trim(), out byte pp))      entry.PP = pp;
            else res.Errors.Add(new MoveImportError(lineNumber, $"Invalid PP '{parts[8]}'."));

            if (_rangeNameToValue.TryGetValue(parts[9].Trim(), out ushort rng)) entry.Range = rng;
            else res.Errors.Add(new MoveImportError(lineNumber, $"Unknown range '{parts[9]}'."));

            res.Entry   = entry;
            res.IsValid = res.Errors.Count == 0;
            return res;
        }

        /// <summary>Imported values wait in memory, keyed by move id, until Save or Discard. The current move takes
        /// them directly and its undo history restarts from them.</summary>
        private async Task StageImportedData(List<MoveDataImportEntry> entries)
        {
            int staged = 0;
            bool currentChanged = false;
            List<string> failures = new List<string>();
            foreach (MoveDataImportEntry e in entries)
            {
                MoveData move;
                if (e.MoveID == _currentId && _currentFile != null)
                {
                    if (SourceLoadError != null) { failures.Add($"Move {e.MoveID}: {SourceLoadError}"); continue; }
                    move = _currentFile;
                    currentChanged = true;
                }
                else if (!_pendingImports.TryGetValue(e.MoveID, out move))
                {
                    try
                    {
                        // Effect, flags and contest aren't in the CSV, so on hg-engine they come from Moves.c.
                        move = LoadRecord(e.MoveID, out string loadError);
                        if (loadError != null) { failures.Add($"Move {e.MoveID}: {loadError}"); continue; }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error($"Failed to read move {e.MoveID}: {ex.Message}");
                        failures.Add($"Move {e.MoveID}: {ex.Message}");
                        continue;
                    }
                }
                move.movetype  = e.MoveType;
                move.split     = e.Split;
                move.damage    = e.Power;
                move.accuracy  = e.Accuracy;
                move.priority  = e.Priority;
                move.sideEffectProbability = e.SideEffectProbability;
                move.pp        = e.PP;
                move.target    = e.Range;
                if (!ReferenceEquals(move, _currentFile)) _pendingImports[e.MoveID] = move;
                staged++;
            }

            if (currentChanged)
            {
                _pendingImports[_currentId] = Copy(_currentFile);
                _loading = true;
                PopulateFromCurrentFile();
                _loading = false;
                _dirty = false;
                _history.Reset(Snapshot());
                _lastCaptureUtc = System.DateTime.MinValue;
                RaiseUndoState();
            }
            Status = _pendingImports.Count == 0 ? string.Empty
                : _pendingImports.Count == 1 ? "1 move imported, not saved" : $"{_pendingImports.Count} moves imported, not saved";
            RefreshDirty();

            if (failures.Count == 0) return;
            string listed = string.Join("\n", failures.Take(10)) + (failures.Count > 10 ? $"\n...and {failures.Count - 10} more" : "");
            await DialogHelper.ShowError($"{staged} move(s) imported. {failures.Count} were not:\n{listed}", "Import Incomplete");
        }
    }
}

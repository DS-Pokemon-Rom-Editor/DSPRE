using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using global::Avalonia.Controls;
using global::Avalonia.Media;
using global::Avalonia.Media.Imaging;
using DSPRE.Avalonia;
using DSPRE.Editors;
using DSPRE.HgEngine;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

using DSPRE.Avalonia.Data;
namespace DSPRE.Avalonia.ViewModels.Battle
{
    /// <summary>
    /// Avalonia port of the WinForms <c>TrainerMessageEditor</c> (battle messages).
    /// Edits the trainer-text table (which maps a trainer + a message trigger to a
    /// message in a shared text archive). The Scintilla message control is replaced by
    /// a plain multi-line TextBox. Save rewrites the whole table + offset file +
    /// message archive (a global operation, exactly like the original).
    /// </summary>
    public class BattleMessageEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        // ── Trigger types (copied from the WinForms editor) ─────────────────────────
        public enum TrainerMessageType : ushort
        {
            PRE_BATTLE = 0, DEFEAT = 1, POST_BATTLE = 2,
            PRE_DOUBLE_BATTLE_1 = 3, DOUBLE_BATTLE_DEFEAT_1 = 4, POST_DOUBLE_BATTLE_1 = 5, DOUBLE_BATTLE_NOT_ENOUGH_POKEMON_1 = 6,
            PRE_DOUBLE_BATTLE_2 = 7, DOUBLE_BATTLE_DEFEAT_2 = 8, POST_DOUBLE_BATTLE_2 = 9, DOUBLE_BATTLE_NOT_ENOUGH_POKEMON_2 = 10,
            NOT_MORNING_UNUSED = 11, NOT_NIGHT_UNUSED = 12, FIRST_DAMAGE = 13, ACTIVE_BATTLER_HALF_HP = 14,
            LAST_BATTLER = 15, LAST_BATTLER_HALF_HP = 16, REMATCH = 17, DOUBLE_BATTLE_REMATCH_1 = 18, DOUBLE_BATTLE_REMATCH_2 = 19,
            WIN = 20, WIN_DPPT = 100
        }

        private static readonly (TrainerMessageType type, string desc)[] Triggers =
        {
            (TrainerMessageType.PRE_BATTLE, "Pre-Battle in Overworld"),
            (TrainerMessageType.DEFEAT, "on Defeat (Player wins)"),
            (TrainerMessageType.POST_BATTLE, "Post-Battle in Overworld"),
            (TrainerMessageType.PRE_DOUBLE_BATTLE_1, "Pre-Double Battle (Trainer 1)"),
            (TrainerMessageType.DOUBLE_BATTLE_DEFEAT_1, "on Double Battle Defeat (Trainer 1)"),
            (TrainerMessageType.POST_DOUBLE_BATTLE_1, "Post-Double Battle (Trainer 1)"),
            (TrainerMessageType.DOUBLE_BATTLE_NOT_ENOUGH_POKEMON_1, "Not Enough Pokémon for Double Battle (Trainer 1)"),
            (TrainerMessageType.PRE_DOUBLE_BATTLE_2, "Pre-Double Battle (Trainer 2)"),
            (TrainerMessageType.DOUBLE_BATTLE_DEFEAT_2, "on Double Battle Defeat (Trainer 2)"),
            (TrainerMessageType.POST_DOUBLE_BATTLE_2, "Post-Double Battle (Trainer 2)"),
            (TrainerMessageType.DOUBLE_BATTLE_NOT_ENOUGH_POKEMON_2, "Not Enough Pokémon for Double Battle (Trainer 2)"),
            (TrainerMessageType.NOT_MORNING_UNUSED, "when not Morning (Unused)"),
            (TrainerMessageType.NOT_NIGHT_UNUSED, "when not Night (Unused)"),
            (TrainerMessageType.FIRST_DAMAGE, "on First Damage dealt (Unused)"),
            (TrainerMessageType.ACTIVE_BATTLER_HALF_HP, "when active Battler Half HP (Unused)"),
            (TrainerMessageType.LAST_BATTLER, "when Last Battler sent out"),
            (TrainerMessageType.LAST_BATTLER_HALF_HP, "when Last Battler Half HP"),
            (TrainerMessageType.REMATCH, "before Rematch"),
            (TrainerMessageType.DOUBLE_BATTLE_REMATCH_1, "on Double Battle Rematch (Trainer 1)"),
            (TrainerMessageType.DOUBLE_BATTLE_REMATCH_2, "on Double Battle Rematch (Trainer 2)"),
            (TrainerMessageType.WIN, "Victory (Trainer wins) - HG/SS"),
            (TrainerMessageType.WIN_DPPT, "Victory (Trainer wins) - DP/PT"),
        };

        private struct Entry { public int messageID; public uint trainerId; public ushort triggerId; }

        // ── hg-engine source-backed state ───────────────────────────────────────────────
        // hg-engine keeps each trainer's messages in its own `.text` array in data/Trainers.c, and its
        // build makes the message table only for trainers listed in sTrainerTextOrder.
        private const string TrainerDataHeader = "include/trainer_data.h";
        private const string TrainerClassHeader = "include/constants/trainerclass.h";
        public bool IsHgeActive => HgEngineProject.IsActive;
        private (int value, string name)[] _hgeTriggers = Array.Empty<(int, string)>();

        // Token is the trigger as the source spells it, so a name the headers don't define is written back as it was.
        private sealed class HgeMessage { public string Token; public int Value; public string Text; }
        private List<HgeMessage> _hgeCurrent = new List<HgeMessage>();
        private string _hgeLoadError;
        private HashSet<int> _hgeTextOrder;
        private int _hgeMaxMessages;

        // ── State ────────────────────────────────────────────────────────────────────
        private Window _owner;
        private bool _suppress;
        private TextArchive _archive;
        private Dictionary<uint, List<Entry>> _byTrainer = new Dictionary<uint, List<Entry>>();
        private List<Entry> _current = new List<Entry>();
        private int _currentTrainerId;
        private bool _currentIsDouble;

        private string TablePath => Path.Combine(gameDirs[DirNames.trainerTextTable].unpackedDir, "0000");
        private string OffsetPath => Path.Combine(gameDirs[DirNames.trainerTextOffset].unpackedDir, "0000");

        public ObservableCollection<string> Trainers { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> TriggerTypes { get; } = new ObservableCollection<string>(Triggers.Select(t => t.desc));
        public ObservableCollection<string> Entries { get; } = new ObservableCollection<string>();

        private int _selectedTrainerIndex = -1;
        public int SelectedTrainerIndex
        {
            get => _selectedTrainerIndex;
            set
            {
                if (value == _selectedTrainerIndex) return;
                if (_dirty && !_suppress && value >= 0 && _selectedTrainerIndex >= 0)
                {
                    // Snap the list back to the trainer still loaded until the user has answered.
                    int requested = value;
                    OnPropertyChanged(nameof(SelectedTrainerIndex));
                    _ = SwitchTrainerAsync(requested);
                    return;
                }
                if (Set(ref _selectedTrainerIndex, value) && !_suppress && value >= 0) LoadTrainer(value);
            }
        }

        private async Task SwitchTrainerAsync(int requested)
        {
            if (!await RecordSwitchGuard.ConfirmLeaveAsync(this, _owner, "trainer")) return;
            SetClean();
            if (Set(ref _selectedTrainerIndex, requested)) LoadTrainer(requested);
        }

        private int _selectedTriggerIndex = -1;
        public int SelectedTriggerIndex { get => _selectedTriggerIndex; set => Set(ref _selectedTriggerIndex, value); }

        private int _selectedEntryIndex = -1;
        public int SelectedEntryIndex
        {
            get => _selectedEntryIndex;
            set { if (Set(ref _selectedEntryIndex, value)) LoadEntry(value); }
        }

        private string _messageText = "";
        public string MessageText { get => _messageText; set => Set(ref _messageText, value); }

        private string _infoText = "";
        public string InfoText { get => _infoText; set => Set(ref _infoText, value); }
        private IBrush _infoColor = Brushes.Gray;
        public IBrush InfoColor { get => _infoColor; set => Set(ref _infoColor, value); }

        private string _statusText = "Not loaded";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        // Sprite
        private readonly TrainerClassSpriteRenderer _sprite = new TrainerClassSpriteRenderer();
        private Bitmap _classImage; public Bitmap ClassImage { get => _classImage; private set => Set(ref _classImage, value); }
        private decimal _frame; public decimal Frame { get => _frame; set { if (Set(ref _frame, value)) ClassImage = _sprite.Render((int)value, 96, 96); } }
        private decimal _frameMax; public decimal FrameMax { get => _frameMax; private set => Set(ref _frameMax, value); }
        public bool HasSprite => _sprite.HasSprite;

        // ── Dirty tracking ───────────────────────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => "Trainer Message Editor";
        public void SaveChanges() => _ = SaveAsync();
        async Task<bool> IEditorWithUnsavedChanges.SaveChangesAsync()
        {
            await SaveAsync();
            return !HasUnsavedChanges;
        }
        public void DiscardChanges()
        {
            _dirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            // Edits live only in memory until saved, so discarding reloads the trainer from source.
            if (IsHgeActive && _selectedTrainerIndex >= 0) LoadTrainer(_currentTrainerId);
        }
        private void SetDirty() { if (_dirty) return; _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        private void SetClean() { if (!_dirty) return; _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        // ── Constructors ────────────────────────────────────────────────────────────
        public BattleMessageEditorViewModel() { if (Design.IsDesignMode) Trainers.Add("[0]: Youngster Joey"); }
        public BattleMessageEditorViewModel(int initialTrainerId) { _initialTrainerId = initialTrainerId; }
        private readonly int _initialTrainerId;

        // ── Setup ─────────────────────────────────────────────────────────────────────
        public async Task SetupAsync(Window owner)
        {
            _owner = owner;
            StatusText = "Loading trainer messages…";
            try
            {
                if (IsHgeActive)
                {
                    DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.trainerGraphics });
                    var table = HgEngineSymbolTable.Load(TrainerDataHeader);
                    _hgeTriggers = table == null ? Array.Empty<(int, string)>()
                        : table.ByName.Where(kv => kv.Key.StartsWith("TRMSG_", StringComparison.Ordinal))
                            .Select(kv => (kv.Value, kv.Key)).OrderBy(t => t.Item1).ToArray();
                    _hgeMaxMessages = table != null && table.TryGetValue("TRAINER_SOURCE_MAX_TEXT_ENTRY_COUNT", out int max) ? max : 0;
                    TriggerTypes.Clear();
                    foreach (var t in _hgeTriggers) TriggerTypes.Add(t.name);
                    ReadHgeTextOrder();
                    foreach (string entry in DSPRE.TrainerNames.GetAll()) Trainers.Add(entry);
                }
                else
                {
                    DSUtils.TryUnpackNarcs(new List<DirNames> {
                        DirNames.textArchives, DirNames.trainerProperties, DirNames.trainerGraphics,
                        DirNames.trainerTextTable, DirNames.trainerTextOffset });
                    _archive = new TextArchive(trainerMessageTextNumber);
                    ReadTable();

                    var trainerNames = GetSimpleTrainerNames();
                    var classArchive = new TextArchive(trainerClassMessageNumber);
                    for (int i = 0; i < trainerNames.Length; i++)
                    {
                        int classId = GetTrainerClassOf(i);
                        string className = classId >= 0 && classId < classArchive.messages.Count ? classArchive.messages[classId] : "?";
                        Trainers.Add($"[{i}]: {className} {trainerNames[i]}");
                    }
                }

                StatusText = IsHgeActive && _hgeTriggers.Length == 0
                    ? "No TRMSG_ message triggers were found in include/trainer_data.h."
                    : $"Loaded messages for {Trainers.Count} trainers.";
                int start = _initialTrainerId >= 0 && _initialTrainerId < Trainers.Count ? _initialTrainerId : 0;
                if (Trainers.Count > 0) SelectedTrainerIndex = start;
            }
            catch (Exception ex)
            {
                StatusText = $"Error: {ex.Message}";
                await DialogHelper.ShowError($"Failed to load trainer messages:\n{ex.Message}", "Battle Message Editor");
            }
        }

        private void ReadTable()
        {
            var entries = new List<Entry>();
            try
            {
                using var reader = new DSUtils.EasyReader(TablePath);
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    int offset = (int)reader.BaseStream.Position;
                    ushort trainerId = reader.ReadUInt16();
                    ushort triggerId = reader.ReadUInt16();
                    entries.Add(new Entry { messageID = offset / 4, trainerId = trainerId, triggerId = triggerId });
                }
            }
            catch (Exception ex) { AppLogger.Error("ReadTable: " + ex.Message); }

            _byTrainer = entries.GroupBy(e => e.trainerId).ToDictionary(g => g.Key, g => g.ToList());
        }

        private void ReadHgeTextOrder()
        {
            if (HgEngineTrainerSource.TryReadTextOrder(out var listed, out string error)) _hgeTextOrder = listed;
            else { _hgeTextOrder = null; AppLogger.Error("Battle messages: " + error); }
        }

        private static int GetTrainerClassOf(int trainerId)
        {
            try
            {
                string path = Path.Combine(gameDirs[DirNames.trainerProperties].unpackedDir, trainerId.ToString("D4"));
                using var s = File.OpenRead(path);
                return new TrainerProperties((ushort)trainerId, s).trainerClass;
            }
            catch { return 0; }
        }

        // ── Trainer selection ──────────────────────────────────────────────────────────
        private void LoadTrainer(int trainerId)
        {
            // Persist current edits back to the dictionary first (vanilla only).
            if (!IsHgeActive && _current != null) _byTrainer[(uint)_currentTrainerId] = _current;

            _currentTrainerId = trainerId;
            if (IsHgeActive)
            {
                LoadHgeTrainer(trainerId);
            }
            else
            {
                try
                {
                    string path = Path.Combine(gameDirs[DirNames.trainerProperties].unpackedDir, trainerId.ToString("D4"));
                    using var s = File.OpenRead(path);
                    var trp = new TrainerProperties((ushort)trainerId, s);
                    _currentIsDouble = trp.doubleBattle;
                    ShowSprite(trp.trainerClass);
                }
                catch (Exception ex) { AppLogger.Error("LoadTrainer: " + ex.Message); }
                _current = _byTrainer.TryGetValue((uint)trainerId, out var list) ? list : new List<Entry>();
            }
            RefreshEntries();
        }

        private void ShowSprite(int trainerClass)
        {
            FrameMax = _sprite.Load(trainerClass);
            OnPropertyChanged(nameof(HasSprite));
            if (_frame > FrameMax) { _frame = 0; OnPropertyChanged(nameof(Frame)); }
            ClassImage = _sprite.Render((int)_frame, 96, 96);
        }

        private void LoadHgeTrainer(int trainerId)
        {
            _hgeCurrent = new List<HgeMessage>();
            _hgeLoadError = null;
            if (!HgEngineTrainerSource.TryLoad(trainerId, out var block, out string error)) { _hgeLoadError = error; return; }

            var data = FieldPathSegment.Field("data");
            ShowSprite(block.TryGetSymbol(new[] { data, FieldPathSegment.Field("trainerClass") }, TrainerClassHeader, out int trainerClass) ? trainerClass : 0);
            // An absent battle type is 0, a single battle.
            _currentIsDouble = block.TryGetSymbol(new[] { data, FieldPathSegment.Field("battleType") }, TrainerDataHeader, out int battleType) && battleType != 0;

            int number = 0;
            foreach (var msg in block.GetArrayElements(new[] { FieldPathSegment.Field("text") }))
            {
                number++;
                string token = msg.TryGetRaw(new[] { FieldPathSegment.Field("type") }, out string raw) ? raw : null;
                // C leaves a missing .type at 0.
                int value = token == null ? 0 : HgEngineSourceBlock.TryResolveToken(token, TrainerDataHeader, out int resolved) ? resolved : -1;
                if (!msg.TryGetString(new[] { FieldPathSegment.Field("text") }, out string text))
                {
                    _hgeLoadError ??= $"Message {number} of trainer {trainerId} has no text that can be read.";
                    text = "";
                }
                _hgeCurrent.Add(new HgeMessage { Token = token, Value = value, Text = text });
            }
        }

        private void RefreshEntries()
        {
            _suppress = true;
            Entries.Clear();
            if (IsHgeActive)
            {
                foreach (var m in _hgeCurrent) Entries.Add($"[{HgeLabel(m)}] {m.Text}");
            }
            else
            {
                foreach (var e in _current)
                {
                    string trigger = TriggerName(e.triggerId);
                    string text = e.messageID >= 0 && e.messageID < _archive.messages.Count ? _archive.messages[e.messageID] : "<invalid>";
                    Entries.Add($"[{trigger}] {text}");
                }
            }
            _suppress = false;
            CheckForMistakes();
        }

        private static string TriggerName(ushort triggerId)
            => Enum.IsDefined(typeof(TrainerMessageType), triggerId) ? ((TrainerMessageType)triggerId).ToString() : $"UNKNOWN({triggerId})";

        private static int TriggerComboIndex(ushort triggerId)
        {
            for (int i = 0; i < Triggers.Length; i++) if ((ushort)Triggers[i].type == triggerId) return i;
            return -1;
        }

        private string HgeTriggerName(int value)
        {
            foreach (var t in _hgeTriggers) if (t.value == value) return t.name;
            return null;
        }

        private string HgeLabel(HgeMessage m) => m.Token ?? HgeTriggerName(m.Value) ?? m.Value.ToString();

        private int HgeTriggerComboIndex(int value)
        {
            for (int i = 0; i < _hgeTriggers.Length; i++) if (_hgeTriggers[i].value == value) return i;
            return -1;
        }

        private void LoadEntry(int index)
        {
            _suppress = true;
            if (IsHgeActive)
            {
                if (index >= 0 && index < _hgeCurrent.Count)
                {
                    var m = _hgeCurrent[index];
                    SelectedTriggerIndex = HgeTriggerComboIndex(m.Value);
                    MessageText = DisplayText(m.Text);
                }
            }
            else if (index >= 0 && index < _current.Count)
            {
                var e = _current[index];
                SelectedTriggerIndex = TriggerComboIndex(e.triggerId);
                MessageText = DisplayText(e.messageID >= 0 && e.messageID < _archive.messages.Count ? _archive.messages[e.messageID] : "");
            }
            _suppress = false;
        }

        // ── Display / raw text conversion (mirrors Scintilla helpers) ──────────────────
        private static string DisplayText(string raw)
        {
            foreach (var b in new[] { "\\n", "\\r", "\\f" }) raw = raw.Replace(b, b + Environment.NewLine);
            return raw;
        }

        // A line break shown after \n, \r or \f markup is only for display; one typed anywhere else becomes \n.
        private static string RawText(string display)
        {
            string s = display.Replace("\r\n", "\n").Replace('\r', '\n');
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (c != '\n') { sb.Append(c); continue; }
                bool afterMarkup = sb.Length >= 2 && sb[^2] == '\\' && (sb[^1] == 'n' || sb[^1] == 'r' || sb[^1] == 'f');
                if (!afterMarkup) sb.Append("\\n");
            }
            return sb.ToString();
        }

        // ── Commands ────────────────────────────────────────────────────────────────────
        private bool HgeRefusesEdit(string title)
        {
            string why = _hgeLoadError != null ? "This trainer's messages could not be read from Trainers.c:\n" + _hgeLoadError
                : _hgeTriggers.Length == 0 ? "No TRMSG_ message triggers were found in include/trainer_data.h."
                : null;
            if (why == null) return false;
            _ = DialogHelper.ShowError(why, title);
            return true;
        }

        public void AddEntry()
        {
            if (_selectedTriggerIndex < 0) { _ = DialogHelper.ShowError("Select a message trigger type first.", "Add message"); return; }
            if (IsHgeActive)
            {
                if (HgeRefusesEdit("Add message") || _selectedTriggerIndex >= _hgeTriggers.Length) return;
                if (_hgeMaxMessages > 0 && _hgeCurrent.Count >= _hgeMaxMessages)
                { _ = DialogHelper.ShowError($"A trainer can have at most {_hgeMaxMessages} messages.", "Add message"); return; }
                var t = _hgeTriggers[_selectedTriggerIndex];
                _hgeCurrent.Add(new HgeMessage { Token = t.name, Value = t.value, Text = RawText(_messageText) });
            }
            else
            {
                int newId = _archive.messages.Count;
                _archive.messages.Add(RawText(_messageText));
                _current.Add(new Entry { messageID = newId, trainerId = (uint)_currentTrainerId, triggerId = (ushort)Triggers[_selectedTriggerIndex].type });
            }
            RefreshEntries();
            SetDirty();
        }

        public void DeleteEntry()
        {
            if (IsHgeActive)
            {
                if (HgeRefusesEdit("Delete message")) return;
                if (_selectedEntryIndex < 0 || _selectedEntryIndex >= _hgeCurrent.Count) return;
                _hgeCurrent.RemoveAt(_selectedEntryIndex);
            }
            else
            {
                if (_selectedEntryIndex < 0 || _selectedEntryIndex >= _current.Count) return;
                _current.RemoveAt(_selectedEntryIndex);
            }
            RefreshEntries();
            SetDirty();
        }

        public void EditTrigger()
        {
            if (_selectedTriggerIndex < 0) return;
            if (IsHgeActive)
            {
                if (HgeRefusesEdit("Change trigger") || _selectedTriggerIndex >= _hgeTriggers.Length) return;
                if (_selectedEntryIndex < 0 || _selectedEntryIndex >= _hgeCurrent.Count) return;
                var t = _hgeTriggers[_selectedTriggerIndex];
                _hgeCurrent[_selectedEntryIndex].Token = t.name;
                _hgeCurrent[_selectedEntryIndex].Value = t.value;
            }
            else
            {
                if (_selectedEntryIndex < 0 || _selectedEntryIndex >= _current.Count) return;
                var e = _current[_selectedEntryIndex];
                e.triggerId = (ushort)Triggers[_selectedTriggerIndex].type;
                _current[_selectedEntryIndex] = e;
            }
            RefreshEntries();
            SetDirty();
        }

        public void SaveMessage()
        {
            if (IsHgeActive)
            {
                if (HgeRefusesEdit("Save message")) return;
                if (_selectedEntryIndex < 0 || _selectedEntryIndex >= _hgeCurrent.Count) { _ = DialogHelper.ShowError("Select a message to overwrite.", "Save message"); return; }
                _hgeCurrent[_selectedEntryIndex].Text = RawText(_messageText);
                RefreshEntries();
                SetDirty();
                return;
            }
            if (_selectedEntryIndex < 0 || _selectedEntryIndex >= _current.Count) { _ = DialogHelper.ShowError("Select a message to overwrite.", "Save message"); return; }
            var e = _current[_selectedEntryIndex];
            if (e.messageID >= 0 && e.messageID < _archive.messages.Count)
            {
                _archive.messages[e.messageID] = RawText(_messageText);
                RefreshEntries();
                SetDirty();
            }
        }

        // ── hg-engine save: this one trainer's `.text` field, plus its place in sTrainerTextOrder ─────────
        private async Task SaveHgeMessagesAsync()
        {
            int trainerId = _currentTrainerId;
            string problem = _hgeLoadError != null ? "Its messages could not be read from Trainers.c:\n" + _hgeLoadError
                : _hgeMaxMessages <= 0 ? "TRAINER_SOURCE_MAX_TEXT_ENTRY_COUNT was not found in include/trainer_data.h."
                : _hgeCurrent.Count > _hgeMaxMessages ? $"A trainer can have at most {_hgeMaxMessages} messages."
                : null;
            if (problem != null) { await DialogHelper.ShowError($"Trainer {trainerId} messages were not saved. {problem}", "Save Error"); return; }

            var messages = _hgeCurrent.Select(m => new HgeMessage { Token = HgeLabel(m), Value = m.Value, Text = m.Text }).ToList();
            var (saved, error) = await HgEngineSave.RunAsync(() =>
            {
                var textPath = new[] { FieldPathSegment.Field("text") };
                HgEngineFieldWrite write;
                if (messages.Count == 0)
                {
                    write = HgEngineFieldWrite.Remove(textPath);
                }
                else
                {
                    if (!HgEngineTrainerSource.TryAddToTextOrder(trainerId, out string orderError)) return orderError;
                    write = new HgEngineFieldWrite(textPath, TextLiteral(messages));
                }
                return HgEngineWriter.TryWriteFields(HgEngineDomain.Trainers, trainerId, new[] { write }, out _, out string writeError,
                    allowInsert: true, allOrNothing: true) ? null : writeError;
            });
            if (!saved)
            {
                if (error != null) await DialogHelper.ShowError($"Trainer {trainerId} messages were not saved:\n{error}", "Save Error");
                return;
            }

            ReadHgeTextOrder();
            SetClean();
            SaveNotice.Saved(UnsavedChangesDescription);
            StatusText = $"Trainer {trainerId} messages saved to hg-engine source.";
            RefreshEntries();
        }

        // Laid out like the rest of Trainers.c.
        private static string TextLiteral(IEnumerable<HgeMessage> messages)
        {
            var sb = new StringBuilder("{");
            foreach (var m in messages)
            {
                sb.Append("\n            {");
                sb.Append("\n                .type = ").Append(m.Token).Append(',');
                sb.Append("\n                .text = ").Append(HgEngineTrainerSource.ToCStringLiteral(m.Text)).Append(',');
                sb.Append("\n            },");
            }
            return sb.Append("\n        }").ToString();
        }

        // ── Global save (rewrites table + offset + archive) ────────────────────────────
        public async Task SaveAsync()
        {
            if (IsHgeActive) { await SaveHgeMessagesAsync(); return; }

            if (_current != null) _byTrainer[(uint)_currentTrainerId] = _current;

            bool ok = await DialogHelper.AskYesNo(
                $"This sorts and writes ALL trainer text entries back to the ROM. Text archive {trainerMessageTextNumber} " +
                "will be overwritten entirely and unused messages will be lost.\n\nContinue?", "Confirm Save");
            if (!ok) return;

            try
            {
                var all = _byTrainer.SelectMany(kvp => kvp.Value).ToList();
                WriteTable(all);
                SetClean();
                StatusText = "Trainer messages saved.";
                // Reload to recompute message IDs/offsets.
                _archive = new TextArchive(trainerMessageTextNumber);
                ReadTable();
                LoadTrainer(_currentTrainerId);
            }
            catch (Exception ex)
            {
                await DialogHelper.ShowError($"Error writing trainer text table:\n{ex.Message}", "Save Error");
            }
        }

        private void WriteTable(List<Entry> entries)
        {
            using var writer = new DSUtils.EasyWriter(TablePath);
            using var offsetWriter = new DSUtils.EasyWriter(OffsetPath);

            var idToOffset = new Dictionary<uint, ushort>();
            var sorted = entries.OrderBy(e => e.trainerId).ThenBy(e => e.triggerId).ToList();
            var messages = new List<string>();

            foreach (var e in sorted)
            {
                if (!idToOffset.ContainsKey(e.trainerId)) idToOffset[e.trainerId] = (ushort)writer.BaseStream.Position;
                writer.Write((ushort)e.trainerId);
                writer.Write((ushort)e.triggerId);
                messages.Add(e.messageID >= 0 && e.messageID < _archive.messages.Count ? _archive.messages[e.messageID] : "ERROR");
            }

            var temp = new TextArchive(trainerMessageTextNumber, messages);
            temp.SaveToExpandedDir(trainerMessageTextNumber, false);

            foreach (var kvp in idToOffset)
            {
                offsetWriter.Seek((int)kvp.Key * 2, SeekOrigin.Begin);
                offsetWriter.Write(kvp.Value);
            }
        }

        // ── Validation warnings (ported) ────────────────────────────────────────────────
        private void CheckForMistakes()
        {
            if (IsHgeActive) { CheckHgeMistakes(); return; }
            if (_current == null || _archive == null) { Info("", Brushes.Gray); return; }

            if (_current.Any(e => e.trainerId != (uint)_currentTrainerId)) { Info("Error: Some entries have a trainer ID that does not match the selected trainer.", StatusBrushes.Bad); return; }

            var dups = _current.GroupBy(e => e.triggerId).Where(g => g.Count() > 1).Select(g => TriggerName(g.Key)).ToList();
            if (dups.Any()) { Info($"Warning: Duplicate message trigger types: {string.Join(", ", dups)}", StatusBrushes.Warn); return; }

            if (_current.Any(e => e.messageID >= 0 && e.messageID < _archive.messages.Count && string.IsNullOrWhiteSpace(_archive.messages[e.messageID])))
            { Info("Warning: One or more messages are empty.", StatusBrushes.Warn); return; }

            bool HasDoubleTriggers() => _current.Any(e => e.triggerId >= (ushort)TrainerMessageType.PRE_DOUBLE_BATTLE_1 && e.triggerId <= (ushort)TrainerMessageType.DOUBLE_BATTLE_NOT_ENOUGH_POKEMON_2);
            if (!_currentIsDouble && HasDoubleTriggers()) { Info("Warning: Single-battle trainer has double-battle message triggers.", StatusBrushes.Warn); return; }
            if (_currentIsDouble && _current.Count > 0 && !HasDoubleTriggers()) { Info("Warning: Double-battle trainer has no double-battle message triggers.", StatusBrushes.Warn); return; }

            Info("", Brushes.Gray);
        }

        private void CheckHgeMistakes()
        {
            if (_hgeLoadError != null) { Info("Error: " + _hgeLoadError, StatusBrushes.Bad); return; }
            if (_hgeMaxMessages > 0 && _hgeCurrent.Count > _hgeMaxMessages)
            { Info($"Error: A trainer can have at most {_hgeMaxMessages} messages.", StatusBrushes.Bad); return; }

            if (_hgeCurrent.Count > 0 && _hgeTextOrder != null && !_hgeTextOrder.Contains(_currentTrainerId))
            { Info("Warning: This trainer is not in sTrainerTextOrder, so the build leaves its messages out. Saving adds it.", StatusBrushes.Warn); return; }

            var unknown = _hgeCurrent.Where(m => m.Value < 0).Select(HgeLabel).Distinct().ToList();
            if (unknown.Any()) { Info($"Warning: {string.Join(", ", unknown)} is not defined in trainer_data.h.", StatusBrushes.Warn); return; }

            var dups = _hgeCurrent.GroupBy(m => m.Value).Where(g => g.Count() > 1).Select(g => HgeLabel(g.First())).ToList();
            if (dups.Any()) { Info($"Warning: Duplicate message trigger types: {string.Join(", ", dups)}", StatusBrushes.Warn); return; }

            if (_hgeCurrent.Any(m => string.IsNullOrWhiteSpace(m.Text))) { Info("Warning: One or more messages are empty.", StatusBrushes.Warn); return; }

            bool HasDoubleTriggers() => _hgeCurrent.Any(m => (HgeTriggerName(m.Value) ?? "").StartsWith("TRMSG_DBL_", StringComparison.Ordinal));
            if (!_currentIsDouble && HasDoubleTriggers()) { Info("Warning: Single-battle trainer has double-battle message triggers.", StatusBrushes.Warn); return; }
            if (_currentIsDouble && _hgeCurrent.Count > 0 && !HasDoubleTriggers()) { Info("Warning: Double-battle trainer has no double-battle message triggers.", StatusBrushes.Warn); return; }

            Info("", Brushes.Gray);
        }

        private void Info(string text, IBrush color) { InfoText = text; InfoColor = color; }
    }
}

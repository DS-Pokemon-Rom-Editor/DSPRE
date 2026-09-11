using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;
using Entry = DSPRE.PokegearPhoneBook.Entry;
using SortKey = DSPRE.PokegearPhoneBook.SortKey;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    /// <summary>Every field of every Pokégear contact, with the names the game shows for them.</summary>
    public class PokegearPhoneBookViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private readonly PokegearPhoneBook.Book _book;
        private readonly string[] _names = System.Array.Empty<string>();
        private readonly int[] _archives;
        private readonly string[] _locations = System.Array.Empty<string>();
        private readonly Dictionary<ushort, int> _rematchRowByTrainer = new();
        private readonly int _classNameCount;
        private readonly string[] _classNames = System.Array.Empty<string>();
        private readonly List<string> _phoneMessages;
        private List<int> _listed = new();
        private List<int> _sorted = new();
        private int _current = -1;
        private bool _suppress;
        private bool _dirty;

        public ObservableCollection<string> ContactLabels { get; } = new();
        public List<string> TypeChoices { get; } = PokegearPhoneBook.TypeNames.ToList();
        public List<string> TitleChoices { get; } = new();
        public List<string> TrainerChoices { get; } = new();
        public List<string> MapChoices { get; } = new();
        public List<string> ItemChoices { get; } = new();
        public List<string> GreetingChoices { get; } = new() { "Boy", "Girl", "Bill", "Kenji", "Man", "Woman", "Old man", "Old woman", "No greeting" };
        public List<string> WeekdayChoices { get; } = new() { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Never" };
        public List<string> TimeChoices { get; } = new() { "Morning", "Day", "Night", "Never" };
        public List<string> GroupChoices { get; } = new() { "Group 0 (50%)", "Group 1 (30%)", "Group 2 (20%)", "Never" };

        public string StatusText { get => _status; private set { _status = value; OnPropertyChanged(); } }
        private string _status = "";

        public string LoadError { get; }
        public bool HasLoadError => !string.IsNullOrEmpty(LoadError);

        // ── IEditorWithUnsavedChanges ──
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => "Pokégear Phone Book";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        public PokegearPhoneBookViewModel(int initialEntry = -1)
        {
            _book = PokegearPhoneBook.Load(out string error);
            if (_book == null)
            {
                LoadError = error;
                StatusText = error;
                return;
            }

            _names = PokegearContactArchives.Names(_book.Entries.Count);
            _archives = PokegearContactArchives.Find(_book.Entries.Count);
            _locations = _book.Entries.Select(e => LocationText(e.MapId)).ToArray();

            _classNames = SafeNames(GetTrainerClassNames);
            _classNameCount = _classNames.Length;
            if (pokegearPhoneMessageArchive >= 0)
            {
                try { _phoneMessages = new TextArchive(pokegearPhoneMessageArchive).messages; } catch { }
            }
            for (int t = 0; t < 256; t++)
            {
                string label = TitleText((byte)t);
                if (label.Length == 0) label = t == PokegearPhoneBook.TitleNone ? "(no title)" : "(blank)";
                TitleChoices.Add($"{t}: {label}");
            }

            string[] trainers = SafeNames(() => DSPRE.TrainerNames.GetAll());
            for (int i = 0; i < trainers.Length; i++) TrainerChoices.Add(i == 0 ? "0: (not a trainer)" : trainers[i]);

            var headers = HeaderLabels.Friendly();
            foreach (string h in headers) MapChoices.Add(h);

            string[] items = SafeNames(() => GetItemNames());
            for (int i = 0; i < items.Length; i++) ItemChoices.Add($"{i}: {items[i]}");

            if (PokegearRematchTable.IsSupported)
            {
                var rows = PokegearRematchTable.ReadAll();
                for (int r = 0; r < rows.Count; r++)
                    if (!rows[r].IsEmpty) _rematchRowByTrainer.TryAdd(rows[r].BaseTrainerId, r);
            }

            RebuildList();
            RebuildSortRows();
            int position = initialEntry >= 0 ? _listed.IndexOf(initialEntry) : -1;
            SelectedListIndex = position >= 0 ? position : (_listed.Count > 0 ? 0 : -1);
            UpdateStatus();
        }

        private static string[] SafeNames(System.Func<string[]> read)
        {
            try { return read() ?? System.Array.Empty<string>(); }
            catch { return System.Array.Empty<string>(); }
        }

        // ── Contact list ──

        private string _filterText = "";
        public string FilterText { get => _filterText; set { if (_filterText == value) return; _filterText = value; OnPropertyChanged(); RebuildList(); } }

        private int _selectedListIndex = -1;
        public int SelectedListIndex
        {
            get => _selectedListIndex;
            set
            {
                if (_selectedListIndex == value) return;
                _selectedListIndex = value;
                OnPropertyChanged();
                if (_suppress) return;
                _current = value >= 0 && value < _listed.Count ? _listed[value] : -1;
                LoadDetail();
                SyncSortSelection();
            }
        }

        public bool IsEntrySelected => _current >= 0;

        private void RebuildList()
        {
            if (_book == null) return;
            int keep = _current;
            _suppress = true;

            IEnumerable<int> order = Enumerable.Range(0, _book.Entries.Count);
            string filter = _filterText?.Trim();
            if (!string.IsNullOrEmpty(filter))
                order = order.Where(i => Label(i).Contains(filter, System.StringComparison.OrdinalIgnoreCase));

            _listed = order.ToList();
            ContactLabels.Clear();
            foreach (int i in _listed) ContactLabels.Add(Label(i));

            _selectedListIndex = _listed.IndexOf(keep);
            _suppress = false;
            OnPropertyChanged(nameof(SelectedListIndex));
        }

        private string Label(int index)
        {
            Entry e = _book.Entries[index];
            string title = TitleText(e.Title);
            return string.IsNullOrEmpty(title) ? $"{index}  {NameOf(index)}" : $"{index}  {NameOf(index)}  ·  {title}";
        }

        private string NameOf(int index) => index < _names.Length ? _names[index] : $"Contact {index}";

        private string TitleText(byte title)
        {
            string text = PokegearPhoneBook.TitleText(title, _classNames, _phoneMessages);
            // The phone titles' archive isn't known for every language.
            if (text.Length == 0 && _phoneMessages == null && title >= PokegearPhoneBook.FirstPhoneTitle
                && title < PokegearPhoneBook.FirstPhoneTitle + PokegearPhoneBook.PhoneTitleCount)
                return $"Phone title {title - PokegearPhoneBook.FirstPhoneTitle + 1}";
            return text;
        }

        private static string LocationText(ushort mapId)
        {
            try { return HeaderLabels.LocationNameOf(MapHeader.GetMapHeader(mapId)); }
            catch { return ""; }
        }

        private void SelectContact(int contact)
        {
            int position = _listed.IndexOf(contact);
            if (position < 0 && !string.IsNullOrEmpty(_filterText))
            {
                _filterText = "";
                OnPropertyChanged(nameof(FilterText));
                RebuildList();
                position = _listed.IndexOf(contact);
            }
            SelectedListIndex = position;
        }

        // ── Sort orders ──

        public ObservableCollection<string> SortRows { get; } = new();
        public int ContactCount => _book?.Entries.Count ?? 0;

        private SortKey CurrentSortKey => (SortKey)_sortTabIndex;

        private int _sortTabIndex;
        public int SortTabIndex
        {
            get => _sortTabIndex;
            set
            {
                if (value < (int)SortKey.Title || value > (int)SortKey.Location || _sortTabIndex == value) return;
                _sortTabIndex = value;
                OnPropertyChanged();
                RebuildSortRows();
            }
        }

        private int _sortSelectedIndex = -1;
        public int SortSelectedIndex
        {
            get => _sortSelectedIndex;
            set
            {
                if (_sortSelectedIndex == value) return;
                _sortSelectedIndex = value;
                OnPropertyChanged();
                if (_suppress || value < 0 || value >= _sorted.Count) return;
                SelectContact(_sorted[value]);
            }
        }

        public string AutoSortTip => CurrentSortKey switch
        {
            SortKey.Title => "Put this list in alphabetical order by title, then name",
            SortKey.Name => "Put this list in alphabetical order by name",
            _ => "Put this list in alphabetical order by place, then name",
        };

        public int TitleRank { get => Current?.TitleRank ?? 0; set => SetRank(SortKey.Title, value); }
        public int NameRank { get => Current?.NameRank ?? 0; set => SetRank(SortKey.Name, value); }
        public int LocationRank { get => Current?.LocationRank ?? 0; set => SetRank(SortKey.Location, value); }

        private void SetRank(SortKey key, int value)
        {
            if (_suppress || Current == null || value == Current.Rank(key)) return;
            _book.MoveInOrder(_current, key, System.Math.Clamp(value, 1, _book.Entries.Count));
            SortChanged($"Moved {NameOf(_current)} to {Current.Rank(key)} in {key.ToString().ToLowerInvariant()} order.");
        }

        /// <summary>Moves a row of the sort list shown, as dragging it does.</summary>
        public void MoveSortRow(int from, int to)
        {
            if (_book == null || from == to || from < 0 || to < 0 || from >= _sorted.Count || to >= _sorted.Count) return;
            var order = new List<int>(_sorted);
            int contact = order[from];
            order.RemoveAt(from);
            order.Insert(to, contact);
            _book.ReorderTo(CurrentSortKey, order);
            SortChanged($"Moved {NameOf(contact)} to {to + 1} in {CurrentSortKey.ToString().ToLowerInvariant()} order.");
        }

        public void AutoSort()
        {
            if (_book == null) return;
            switch (CurrentSortKey)
            {
                case SortKey.Title:
                    _book.RankBy(SortKey.Title, _book.Entries.Select(e => TitleText(e.Title)).ToList(), _names);
                    break;
                case SortKey.Name:
                    _book.RankBy(SortKey.Name, _names);
                    break;
                default:
                    _book.RankBy(SortKey.Location, _locations, _names);
                    break;
            }
            SortChanged($"Sorted the {CurrentSortKey.ToString().ToLowerInvariant()} order alphabetically.");
        }

        private void SortChanged(string message)
        {
            _dirty = true;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            RebuildSortRows();
            NotifyRanks();
            UpdateStatus(message);
        }

        private void NotifyRanks()
        {
            OnPropertyChanged(nameof(TitleRank));
            OnPropertyChanged(nameof(NameRank));
            OnPropertyChanged(nameof(LocationRank));
        }

        private void RebuildSortRows()
        {
            if (_book == null) return;
            SortKey key = CurrentSortKey;
            _suppress = true;
            _sorted = _book.InOrder(key);
            SortRows.Clear();
            foreach (int contact in _sorted) SortRows.Add(SortLabel(contact, key));
            _sortSelectedIndex = _sorted.IndexOf(_current);
            _suppress = false;
            OnPropertyChanged(nameof(SortSelectedIndex));
            OnPropertyChanged(nameof(AutoSortTip));
        }

        private void SyncSortSelection()
        {
            _suppress = true;
            _sortSelectedIndex = _sorted.IndexOf(_current);
            _suppress = false;
            OnPropertyChanged(nameof(SortSelectedIndex));
        }

        private string SortLabel(int contact, SortKey key)
        {
            int rank = _book.Entries[contact].Rank(key);
            string place = rank >= 1 && rank <= _book.Entries.Count ? rank.ToString() : "-";
            string text = key switch
            {
                SortKey.Title => $"{(TitleText(_book.Entries[contact].Title) is { Length: > 0 } t ? t : "(no title)")}  ·  {NameOf(contact)}",
                SortKey.Name => NameOf(contact),
                _ => $"{(string.IsNullOrEmpty(_locations[contact]) ? $"Map {_book.Entries[contact].MapId}" : _locations[contact])}  ·  {NameOf(contact)}",
            };
            return $"{place}.  {text}";
        }

        // ── Detail ──

        private Entry Current => _current >= 0 ? _book.Entries[_current] : null;

        public string Heading => Current == null ? ""
            : _archives != null && _current < _archives.Length
                ? $"{NameOf(_current)}  ·  contact {_current}  ·  text archive {_archives[_current]}"
                : $"{NameOf(_current)}  ·  contact {_current}";

        private int _typeIndex = -1, _titleIndex = -1, _trainerIndex = -1, _mapIndex = -1, _giftIndex = -1,
            _greetingIndex = -1, _weekdayIndex = -1, _timeIndex = -1, _groupIndex = -1;
        private int _localScript;

        public int TypeIndex { get => _typeIndex; set => SetField(ref _typeIndex, value, (e, v) => e.Type = (byte)v); }
        public int TitleIndex { get => _titleIndex; set => SetField(ref _titleIndex, value, (e, v) => e.Title = (byte)v); }
        public int TrainerIndex { get => _trainerIndex; set => SetField(ref _trainerIndex, value, (e, v) => e.TrainerId = (ushort)v); }
        public int MapIndex { get => _mapIndex; set => SetField(ref _mapIndex, value, (e, v) => e.MapId = (ushort)v); }
        public int GiftIndex { get => _giftIndex; set => SetField(ref _giftIndex, value, (e, v) => e.Gift = (ushort)v); }
        public int GreetingIndex
        {
            get => _greetingIndex;
            set => SetField(ref _greetingIndex, value, (e, v) => e.Greeting = v == PokegearPhoneBook.GreetingSetCount ? PokegearPhoneBook.NoGreeting : (byte)v);
        }
        public int WeekdayIndex { get => _weekdayIndex; set => SetField(ref _weekdayIndex, value, (e, v) => e.Weekday = (byte)v); }
        public int TimeIndex { get => _timeIndex; set => SetField(ref _timeIndex, value, (e, v) => e.TimeOfDay = (byte)v); }
        public int GroupIndex
        {
            get => _groupIndex;
            // Any value past the last group never rings, so an existing one is kept as it is.
            set => SetField(ref _groupIndex, value, (e, v) =>
            {
                if (v < PokegearPhoneBook.RandomGroupCount || e.RandomGroup < PokegearPhoneBook.RandomGroupCount) e.RandomGroup = (byte)v;
            });
        }
        public int LocalScript { get => _localScript; set => SetField(ref _localScript, System.Math.Clamp(value, 0, ushort.MaxValue), (e, v) => e.LocalScript = (ushort)v); }

        public bool IsTrainerType => Current?.Type == PokegearPhoneBook.TypeTrainer;
        public bool UsesSchedule => Current != null && (Current.Type == PokegearPhoneBook.TypeTrainer || Current.Type == PokegearPhoneBook.TypeGymLeader);

        public string TypeNote => Current == null ? ""
            : PokegearPhoneBook.RingsAtRandom(Current.Type) ? "Can call at random." : "Only calls when a script or event starts the call.";

        public int RematchRow => Current != null && _rematchRowByTrainer.TryGetValue(Current.TrainerId, out int row) ? row : -1;
        public bool HasRematchRow => RematchRow >= 0;

        /// <summary>Settings the game handles badly, one per line.</summary>
        public string Problems
        {
            get
            {
                Entry e = Current;
                if (e == null) return "";
                var lines = new List<string>();

                if (e.Id != _current)
                    lines.Add($"The stored ID is {e.Id} but this is contact {_current}. The game reads both, so they must match.");

                if (e.Title < PokegearPhoneBook.TitleNone && e.Title >= _classNameCount)
                    lines.Add($"Trainer class {e.Title} has no name, so the title is blank.");
                else if (e.Title >= PokegearPhoneBook.FirstPhoneTitle + PokegearPhoneBook.PhoneTitleCount)
                    lines.Add("Titles above 207 are blank.");

                if (e.TrainerId != 0)
                {
                    int first = _book.FindByTrainer(e.TrainerId);
                    if (first >= 0 && first != _current)
                        lines.Add($"{NameOf(first)} (contact {first}) calls the same trainer and is found first, so this contact never gets that trainer's rematches.");
                    if (_rematchRowByTrainer.Count > 0 && !_rematchRowByTrainer.ContainsKey(e.TrainerId))
                        lines.Add("No rematch row starts with this trainer, so its rematch battles never start.");
                }
                else if (e.Type == PokegearPhoneBook.TypeTrainer)
                {
                    lines.Add("A trainer contact without a trainer can't be registered after a battle, only by a script.");
                }

                if (e.RandomGroup >= PokegearPhoneBook.RandomGroupCount && PokegearPhoneBook.RingsAtRandom(e.Type))
                    lines.Add($"Random group {e.RandomGroup} is never picked, so this contact never calls at random.");

                if (_book.Entries.Count != PokegearPhoneBook.GameContactCount)
                    lines.Add($"The phone book has {_book.Entries.Count} contacts; the game is built for {PokegearPhoneBook.GameContactCount}.");

                return string.Join("\n", lines);
            }
        }

        public bool HasProblems => Problems.Length > 0;

        private void LoadDetail()
        {
            _suppress = true;
            Entry e = Current;
            _typeIndex = e == null || e.Type >= TypeChoices.Count ? -1 : e.Type;
            _titleIndex = e?.Title ?? -1;
            _trainerIndex = e == null || e.TrainerId >= TrainerChoices.Count ? -1 : e.TrainerId;
            _mapIndex = e == null || e.MapId >= MapChoices.Count ? -1 : e.MapId;
            _giftIndex = e == null || e.Gift >= ItemChoices.Count ? -1 : e.Gift;
            _greetingIndex = e == null ? -1
                : e.Greeting == PokegearPhoneBook.NoGreeting ? PokegearPhoneBook.GreetingSetCount
                : e.Greeting < PokegearPhoneBook.GreetingSetCount ? e.Greeting : -1;
            _weekdayIndex = e == null || e.Weekday > PokegearPhoneBook.NeverWeekday ? -1 : e.Weekday;
            _timeIndex = e == null || e.TimeOfDay > PokegearPhoneBook.NeverTimeOfDay ? -1 : e.TimeOfDay;
            _groupIndex = e == null ? -1 : System.Math.Min((int)e.RandomGroup, PokegearPhoneBook.RandomGroupCount);
            _localScript = e?.LocalScript ?? 0;
            _suppress = false;

            foreach (string p in new[]
            {
                nameof(TypeIndex), nameof(TitleIndex), nameof(TrainerIndex), nameof(MapIndex), nameof(GiftIndex),
                nameof(GreetingIndex), nameof(WeekdayIndex), nameof(TimeIndex), nameof(GroupIndex), nameof(LocalScript),
            }) OnPropertyChanged(p);
            NotifyRanks();
            DetailChanged();
        }

        private void DetailChanged()
        {
            foreach (string p in new[]
            {
                nameof(IsEntrySelected), nameof(Heading), nameof(IsTrainerType), nameof(UsesSchedule), nameof(TypeNote),
                nameof(RematchRow), nameof(HasRematchRow), nameof(Problems), nameof(HasProblems),
            }) OnPropertyChanged(p);
        }

        private void SetField(ref int field, int value, System.Action<Entry, int> apply, [CallerMemberName] string name = null)
        {
            if (field == value) return;
            // A combo reports -1 while its list is being replaced; that is not an edit.
            if (value < 0 && !_suppress) { OnPropertyChanged(name); return; }
            field = value;
            OnPropertyChanged(name);
            if (_suppress || Current == null) return;

            apply(Current, value);
            _dirty = true;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            if (name == nameof(MapIndex)) _locations[_current] = LocationText(Current.MapId);
            if (name is nameof(TitleIndex) or nameof(MapIndex)) RebuildSortRows();

            int position = _selectedListIndex;
            if (position >= 0 && position < ContactLabels.Count)
            {
                // Replacing the item clears the list's selection, so put it back.
                _suppress = true;
                ContactLabels[position] = Label(_current);
                _selectedListIndex = position;
                _suppress = false;
                OnPropertyChanged(nameof(SelectedListIndex));
            }
            DetailChanged();
            UpdateStatus();
        }

        public void Save()
        {
            if (_book == null) return;
            if (!PokegearPhoneBook.Save(_book, out string error))
            {
                AppMessages.Error(error, "Pokégear Phone Book");
                return;
            }
            _dirty = false;
            SaveNotice.Saved(UnsavedChangesDescription);
            OnPropertyChanged(nameof(HasUnsavedChanges));
            UpdateStatus("Saved.");
        }

        private void UpdateStatus(string message = null)
        {
            if (_book == null) return;
            StatusText = message ?? $"{_book.Entries.Count} contacts.{(_dirty ? " Unsaved changes." : "")}";
        }
    }
}

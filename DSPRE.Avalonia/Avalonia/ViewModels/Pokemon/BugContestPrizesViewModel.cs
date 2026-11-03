using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>The Bug-Catching Contest prizes for each place, unpatched or with the prize table patch.</summary>
    public class BugContestPrizesViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private BugContestPrizes _prizes;
        private byte[] _saved;
        private ByteStateUndo _undo;
        private bool _listening;

        public string[] ItemNames { get; private set; } = Array.Empty<string>();
        public string WhyNotLoaded { get; private set; } = "";
        public bool Loaded => _prizes != null;
        public bool Patched => _prizes?.Patched == true;
        public bool Unpatched => _prizes != null && !_prizes.Patched;

        public BugContestPrizesViewModel() { }

        public void Setup()
        {
            if (Loaded) return;
            if (!_listening) { AppEvents.RomPatchStateChanged += OnPatchStateChanged; _listening = true; }
            ItemNames = GetItemNames();
            _prizes = BugContestPrizes.TryRead(out string why);
            if (_prizes == null) { WhyNotLoaded = why; Raise(nameof(WhyNotLoaded)); return; }
            _saved = TakeState();
            StartUndo();
            RaiseAll();
        }

        public void Detach()
        {
            if (_listening) AppEvents.RomPatchStateChanged -= OnPatchStateChanged;
            _listening = false;
        }

        // The patch copies the saved prizes into its table. Unsaved edits carry over as slots.
        private void OnPatchStateChanged(object sender, EventArgs e)
        {
            if (_prizes == null || _prizes.Patched || !BugContestPrizes.IsApplied) return;
            BugContestPrizes moved = BugContestPrizes.TryRead(out _);
            if (moved == null) return;
            BugContestPrizes edited = _prizes;
            bool dirty = HasUnsavedChanges;
            _prizes = moved;
            _saved = TakeState();
            if (dirty)
            {
                moved.Slots = SlotItems(edited).ToList();
                moved.RollCount = moved.Slots.Count;
                moved.Second = edited.Second; moved.Third = edited.Third; moved.Consolation = edited.Consolation;
            }
            StartUndo();
            RaiseAll();
        }

        public void RefreshNames()
        {
            if (!Loaded) return;
            ItemNames = GetItemNames();
            RaiseAll();
        }

        private void StartUndo() => _undo = new ByteStateUndo(TakeState, ApplyState, () => { Raise(nameof(CanUndo)); Raise(nameof(CanRedo)); });

        // ── The draw: one slot per roll ─────────────────────────────────────────────
        private static IEnumerable<int> SlotItems(BugContestPrizes p) => p.Patched ? p.Slots
            : Enumerable.Range(p.FirstStart, p.FirstRunLength).Concat(Enumerable.Range(p.SecondStart, p.RollCount - p.FirstRunLength));

        private int SlotCount => _prizes == null ? 0 : _prizes.Patched ? _prizes.Slots.Count : _prizes.RollCount;

        public sealed record DrawSlot(int Item, string Name, string Chance, bool SecondGroup, bool First);
        public sealed record StripCell(int Item, string Number, string Name, bool Inside, bool SecondGroup);

        public DrawSlot[] Draw
        {
            get
            {
                if (_prizes == null) return Array.Empty<DrawSlot>();
                string chance = Percent(1);
                return SlotItems(_prizes).Select((id, k) => new DrawSlot(id, Name(id), chance, !_prizes.Patched && k >= _prizes.FirstRunLength, k == 0)).ToArray();
            }
        }

        private string Percent(int slots) => SlotCount == 0 ? "" : $"{100.0 * slots / SlotCount:0.#}%";

        public string DrawSummary => SlotCount == 1
            ? "After the National Dex, first place always wins this prize."
            : $"After the National Dex, first place wins one of these {SlotCount} prizes at random, each 1 in {SlotCount}.";

        public string BeforeDexText => _prizes == null ? "" : $"Before the National Dex, first place always wins {Name(SlotItems(_prizes).First())}.";

        /// <summary>Items in more than one slot, with their whole chance.</summary>
        public string RepeatsText
        {
            get
            {
                if (!Patched) return "";
                IEnumerable<string> lines = _prizes.Slots.GroupBy(i => i).Where(g => g.Count() > 1)
                    .Select(g => $"{Name(g.Key)}: {g.Count()} slots, {Percent(g.Count())} in all.");
                return string.Join("\n", lines);
            }
        }

        public bool HasRepeats => RepeatsText.Length > 0;

        // ── Unpatched: two groups counted up from a starting item ───────────────────
        public int FirstStart { get => _prizes?.FirstStart ?? 0; set => Change(p => p.FirstStart = value, Unpatched && value >= 0); }
        public int FirstCount { get => _prizes?.FirstRunLength ?? 0; set => Change(p => { int other = p.RollCount - p.FirstRunLength; p.FirstRunLength = value; p.RollCount = value + other; }, Unpatched && value >= 1); }
        public int SecondStart { get => _prizes?.SecondStart ?? 0; set => Change(p => p.SecondStart = value, Unpatched && value >= 0); }
        public int SecondCount { get => _prizes == null || _prizes.Patched ? 0 : _prizes.RollCount - _prizes.FirstRunLength; set => Change(p => p.RollCount = p.FirstRunLength + value, Unpatched && value >= 0); }
        public bool HasSecondGroup => SecondCount > 0;

        public StripCell[] FirstStrip => Strip(FirstStart, FirstCount, false);
        public StripCell[] SecondStrip => HasSecondGroup ? Strip(SecondStart, SecondCount, true) : Array.Empty<StripCell>();

        // Shows one greyed neighbour on each side of the group.
        private StripCell[] Strip(int start, int count, bool second)
        {
            if (_prizes == null || _prizes.Patched) return Array.Empty<StripCell>();
            List<StripCell> cells = new List<StripCell>();
            for (int id = start - 1; id <= start + count; id++)
                if (id > 0 && id < ItemNames.Length) cells.Add(new StripCell(id, $"#{id}", Name(id), id >= start && id < start + count, second));
            return cells.ToArray();
        }

        // ── Patched: a picker per slot ───────────────────────────────────────────────
        public sealed class SlotRow : INotifyPropertyChanged
        {
            private readonly BugContestPrizesViewModel _owner;
            public int Index { get; }
            public SlotRow(BugContestPrizesViewModel owner, int index) { _owner = owner; Index = index; }
            public event PropertyChangedEventHandler PropertyChanged;
            public string Label => $"Slot {Index + 1}";
            public int Item { get => _owner.SlotItem(Index); set => _owner.SetSlot(Index, value); }
            public string Chance => _owner.Percent(1);
            public string[] ItemNames => _owner.ItemNames;
            public bool CanRemove => _owner.SlotCount > 1;
            internal void Refresh()
            {
                foreach (string n in new[] { nameof(Item), nameof(Chance), nameof(CanRemove), nameof(ItemNames) })
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
            }
        }

        public ObservableCollection<SlotRow> SlotRows { get; } = new ObservableCollection<SlotRow>();

        private int SlotItem(int k) => Patched && k < _prizes.Slots.Count ? _prizes.Slots[k] : 0;
        private void SetSlot(int k, int item) => Change(p => p.Slots[k] = item, Patched && item >= 0 && k < _prizes.Slots.Count && _prizes.Slots[k] != item);

        public bool CanAddSlot => Patched && _prizes.Slots.Count < BugContestPrizes.Capacity;
        public string CapacityText => $"up to {BugContestPrizes.Capacity} slots";

        public void AddSlot() => Change(p => { p.Slots.Add(p.Slots[^1]); p.RollCount = p.Slots.Count; }, CanAddSlot);

        public void RemoveSlot(SlotRow row) =>
            Change(p => { p.Slots.RemoveAt(row.Index); p.RollCount = p.Slots.Count; }, Patched && row != null && row.Index < _prizes.Slots.Count && _prizes.Slots.Count > 1);

        private void SyncSlotRows()
        {
            int want = Patched ? _prizes.Slots.Count : 0;
            while (SlotRows.Count > want) SlotRows.RemoveAt(SlotRows.Count - 1);
            while (SlotRows.Count < want) SlotRows.Add(new SlotRow(this, SlotRows.Count));
            foreach (SlotRow r in SlotRows) r.Refresh();
        }

        // ── Every other place ────────────────────────────────────────────────────────
        public int Second { get => _prizes?.Second ?? 0; set => Change(p => p.Second = value, value >= 0); }
        public int Third { get => _prizes?.Third ?? 0; set => Change(p => p.Third = value, value >= 0); }
        public int Consolation { get => _prizes?.Consolation ?? 0; set => Change(p => p.Consolation = value, value >= 0); }

        private string Name(int id) => id >= 0 && id < ItemNames.Length ? ItemNames[id] : $"Item {id}";

        public string Problem => _prizes?.Problem(ItemNames.Length) ?? "";
        public bool HasProblem => Problem.Length > 0;

        // Every change here is a pick or a click, so each one is its own undo step.
        private void Change(Action<BugContestPrizes> set, bool valid)
        {
            if (_prizes == null || !valid) return;
            set(_prizes);
            RaiseAll();
            _undo?.Record(new object());
        }

        private void RaiseAll()
        {
            SyncSlotRows();
            foreach (string n in new[] { nameof(ItemNames), nameof(Loaded), nameof(Patched), nameof(Unpatched), nameof(Draw), nameof(DrawSummary),
                         nameof(BeforeDexText), nameof(RepeatsText), nameof(HasRepeats), nameof(FirstStart), nameof(FirstCount), nameof(SecondStart),
                         nameof(SecondCount), nameof(HasSecondGroup), nameof(FirstStrip), nameof(SecondStrip), nameof(CanAddSlot), nameof(Second),
                         nameof(Third), nameof(Consolation), nameof(Problem), nameof(HasProblem), nameof(HasUnsavedChanges) })
                Raise(n);
        }

        // ── Undo ────────────────────────────────────────────────────────────────────
        private byte[] TakeState() => ByteStateUndo.Pack(w =>
        {
            w.Write(_prizes.RollCount); w.Write(_prizes.FirstRunLength); w.Write(_prizes.FirstStart); w.Write(_prizes.SecondStart);
            w.Write(_prizes.Second); w.Write(_prizes.Third); w.Write(_prizes.Consolation);
            w.Write(_prizes.Patched ? _prizes.Slots.Count : -1);
            if (_prizes.Patched) foreach (int item in _prizes.Slots) w.Write(item);
        });

        private void ApplyState(byte[] state) => ByteStateUndo.Unpack(state, r =>
        {
            _prizes.RollCount = r.ReadInt32(); _prizes.FirstRunLength = r.ReadInt32(); _prizes.FirstStart = r.ReadInt32(); _prizes.SecondStart = r.ReadInt32();
            _prizes.Second = r.ReadInt32(); _prizes.Third = r.ReadInt32(); _prizes.Consolation = r.ReadInt32();
            int count = r.ReadInt32();
            if (count >= 0) { _prizes.Slots = new List<int>(); for (int k = 0; k < count; k++) _prizes.Slots.Add(r.ReadInt32()); }
            RaiseAll();
        });

        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();

        // ── Saving ──────────────────────────────────────────────────────────────────
        public bool HasUnsavedChanges => _prizes != null && !TakeState().AsSpan().SequenceEqual(_saved);
        public string UnsavedChangesDescription => "Bug Contest prizes";

        public void SaveChanges() => DialogHelper.Watch(SaveChangesAsync());

        public async Task<bool> SaveChangesAsync()
        {
            if (!HasUnsavedChanges) return true;
            if (HasProblem) { await DialogHelper.ShowErrorAsync(Problem, "Bug Contest prizes"); return false; }
            try { _prizes.Write(ItemNames.Length); }
            catch (Exception e) when (e is InvalidOperationException || e is InvalidDataException || e is IOException || e is UnauthorizedAccessException)
            {
                await DialogHelper.ShowErrorAsync("The prizes weren't saved:\n" + e.Message, "Bug Contest prizes");
                return false;
            }
            _saved = TakeState();
            _undo?.MarkSaved();
            Raise(nameof(HasUnsavedChanges));
            SaveNotice.Saved(UnsavedChangesDescription);
            return true;
        }

        public void DiscardChanges()
        {
            if (!HasUnsavedChanges) return;
            BugContestPrizes onDisk = BugContestPrizes.TryRead(out string why);
            if (onDisk == null)
            {
                AppLogger.Warn("Bug Contest prizes not reloaded: " + why);
                ApplyState(_saved);
                return;
            }
            _prizes = onDisk;
            _saved = TakeState();
            StartUndo();
            RaiseAll();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DSPRE.Editors;
using DSPRE.HgEngine;

namespace DSPRE.Avalonia.ViewModels.Shell
{
    /// <summary>One patch shown in the table, with what it does and where it lands.</summary>
    public class PatchRow
    {
        public HgEnginePatchEntry Entry { get; init; }

        public string List { get; init; }
        public string Binary => Entry.BinaryName;
        public string Symbol => Entry.Kind == HgEnginePatchKind.ByteReplacement
            ? string.Join(" ", Entry.Bytes.Select(b => b.ToString("X2")))
            : Entry.Symbol;
        public string Address => $"0x{Entry.Address:X8}";
        public string Offset { get; init; }
        public string Size { get; init; }
        public string Does => Entry.Describes;

        /// <summary>Added here and not written to its list yet.</summary>
        public bool IsPending { get; init; }

        /// <summary>Set when another entry writes over the same bytes, which is nearly always a mistake.</summary>
        public string Clash { get; set; }
        public bool HasClash => !string.IsNullOrEmpty(Clash);
    }

    /// <summary>
    /// hg-engine's patch lists, shown by what they do. Added patches stay in memory until Save edits the
    /// checkout's list in place, keeping its comments.
    /// </summary>
    public class HgEnginePatchesViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private readonly Func<List<HgEnginePatchList>> _readAll;
        private List<HgEnginePatchList> _lists = new();

        // Entries added or changed since the last save, shown as not saved.
        private readonly HashSet<HgEnginePatchEntry> _pending = new();

        // One step per add, change or delete, each knowing how to take itself back and redo itself.
        private sealed record Step(HgEnginePatchList List, Action Undo, Action Redo);
        private readonly Stack<Step> _done = new(), _undone = new();
        public bool CanUndo => _done.Count > 0;
        public bool CanRedo => _undone.Count > 0;

        public void Undo()
        {
            if (_done.Count == 0) return;
            Step step = _done.Pop();
            step.Undo();
            _undone.Push(step);
            Stepped();
        }

        public void Redo()
        {
            if (_undone.Count == 0) return;
            Step step = _undone.Pop();
            step.Redo();
            _done.Push(step);
            Stepped();
        }

        private void Did(Step step)
        {
            step.Redo();
            _done.Push(step);
            _undone.Clear();
            Stepped();
        }

        private void Stepped()
        {
            OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); OnPropertyChanged(nameof(HasUnsavedChanges));
            Rebuild();
        }

        private void ForgetSteps()
        {
            _done.Clear(); _undone.Clear();
            OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo));
        }

        public ObservableCollection<PatchRow> Rows { get; } = new();
        public ObservableCollection<string> Binaries { get; } = new();
        public ObservableCollection<string> ListNames { get; } = new();

        public bool IsAvailable => _readAll != null;

        private string _statusText = "";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        private int _binaryFilter;
        public int BinaryFilter { get => _binaryFilter; set { if (Set(ref _binaryFilter, value)) Rebuild(); } }

        // ── Adding one ──────────────────────────────────────────────────────
        public ObservableCollection<string> NewListChoices { get; } = new();

        private int _newList;
        public int NewList { get => _newList; set { if (Set(ref _newList, value)) OnPropertyChanged(nameof(NewNeedsBytes)); } }

        public bool NewNeedsBytes => NewList >= 0 && NewList < _lists.Count
            && _lists[NewList].Kind == HgEnginePatchKind.ByteReplacement;

        private string _newBinary = "arm9";
        public string NewBinary { get => _newBinary; set => Set(ref _newBinary, value); }

        private string _newSymbol = "";
        public string NewSymbol { get => _newSymbol; set => Set(ref _newSymbol, value); }

        private string _newAddress = "";
        public string NewAddress { get => _newAddress; set => Set(ref _newAddress, value); }

        private string _newRegister = "0";
        public string NewRegister { get => _newRegister; set => Set(ref _newRegister, value); }

        private string _newBytes = "";
        public string NewBytes { get => _newBytes; set => Set(ref _newBytes, value); }

        public HgEnginePatchesViewModel()
            : this(HgEngineProject.IsActive ? new Func<List<HgEnginePatchList>>(HgEnginePatchList.ReadAll) : null) { }

        internal HgEnginePatchesViewModel(Func<List<HgEnginePatchList>> readAll)
        {
            _readAll = readAll;
            if (!IsAvailable)
            {
                StatusText = "Link an hg-engine checkout to see the patches it applies.";
                return;
            }

            _lists = _readAll();
            foreach (HgEnginePatchList l in _lists) { ListNames.Add(l.FileName); NewListChoices.Add(l.FileName); }
            if (_lists.Count > 0) _newList = 0;

            Rebuild();
        }

        private void Rebuild()
        {
            Rows.Clear();

            List<(HgEnginePatchList List, HgEnginePatchEntry Entry)> all = _lists.SelectMany(l => l.Entries.Where(e => e.Parsed).Select(e => (List: l, Entry: e))).ToList();

            RebuildBinaryChoices(all.Select(x => x.Entry.OverlayNumber).Distinct().OrderBy(n => n).ToList());

            int? wanted = BinaryFilter <= 0 ? null : OverlayForChoice(BinaryFilter);

            foreach ((HgEnginePatchList list, HgEnginePatchEntry entry) in all
                .Where(x => wanted == null || x.Entry.OverlayNumber == wanted)
                .OrderBy(x => x.Entry.OverlayNumber).ThenBy(x => x.Entry.Address))
            {
                long offset = entry.FileOffset(OverlayRam);
                bool pending = _pending.Contains(entry);
                Rows.Add(new PatchRow
                {
                    Entry = entry,
                    List = pending ? list.FileName + " (not saved)" : list.FileName,
                    Offset = offset < 0 ? "" : $"0x{offset:X}",
                    Size = entry.Length.ToString(),
                    IsPending = pending,
                });
            }

            MarkClashes();
            int clashes = Rows.Count(r => r.HasClash);
            StatusText = $"{Rows.Count} patch(es)" +
                (clashes > 0 ? $", {clashes} running into another" : "") +
                (_pending.Count > 0 ? $", {_pending.Count} not saved" : "") + ".";
        }

        private void RebuildBinaryChoices(List<int> overlays)
        {
            if (Binaries.Count > 0) return;
            Binaries.Add("Everything");
            foreach (int n in overlays) Binaries.Add(n < 0 ? "arm9" : $"overlay {n}");
            _overlayByChoice = overlays;
        }

        private List<int> _overlayByChoice = new();
        private int OverlayForChoice(int choice) =>
            choice - 1 >= 0 && choice - 1 < _overlayByChoice.Count ? _overlayByChoice[choice - 1] : -1;

        private static long OverlayRam(int overlayNumber)
        {
            try { return OverlayUtils.OverlayTable.GetRAMAddress(overlayNumber); }
            catch { return 0; }
        }

        /// <summary>
        /// Flags patches starting inside another. Same-address pairs are skipped, since hg-engine stacks them on
        /// purpose and repeats lines across #ifdef branches.
        /// </summary>
        private void MarkClashes()
        {
            foreach (IGrouping<int, PatchRow> group in Rows.Where(r => r.Offset.Length > 0).GroupBy(r => r.Entry.OverlayNumber))
            {
                List<(PatchRow Row, long Start, int Len)> ordered = group
                    .Select(r => (Row: r,
                        Start: Convert.ToInt64(r.Offset.Substring(2), 16),
                        Len: int.TryParse(r.Size, out int l) ? l : 0))
                    .OrderBy(x => x.Start).ToList();

                for (int i = 1; i < ordered.Count; i++)
                {
                    (PatchRow Row, long Start, int Len) prev = ordered[i - 1];
                    (PatchRow Row, long Start, int Len) here = ordered[i];
                    if (here.Start == prev.Start) continue;
                    if (here.Start >= prev.Start + prev.Len) continue;

                    here.Row.Clash = $"starts inside {prev.Row.Symbol} at {prev.Row.Offset}";
                    prev.Row.Clash ??= $"{here.Row.Symbol} at {here.Row.Offset} starts inside this";
                }
            }
        }

        // ── Editing one ─────────────────────────────────────────────────────
        private PatchRow _selectedRow;
        /// <summary>The row picked in the table; its values fill the fields so they can be changed.</summary>
        public PatchRow SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (!Set(ref _selectedRow, value)) return;
                OnPropertyChanged(nameof(HasSelection));
                if (value == null) return;
                HgEnginePatchEntry e = value.Entry;
                int listIndex = _lists.FindIndex(l => l.Entries.Contains(e));
                if (listIndex >= 0) NewList = listIndex;
                NewBinary = e.OverlayNumber < 0 ? "arm9" : e.OverlayNumber.ToString("D4");
                NewSymbol = e.Symbol;
                NewAddress = e.Address.ToString("X8");
                NewRegister = e.Register < 0 ? "" : e.Register.ToString();
                NewBytes = string.Join(" ", e.Bytes.Select(b => b.ToString("X2")));
            }
        }
        public bool HasSelection => _selectedRow != null;

        /// <summary>Reads the fields into an entry's values, or says why they can't be used.</summary>
        private string ReadFields(HgEnginePatchKind kind, out int overlay, out long at, out int register, out List<byte> bytes)
        {
            overlay = -1; at = 0; register = -1; bytes = new List<byte>();
            if (!HgEngineClaimedRanges.TryBinary((NewBinary ?? "").Trim(), out overlay))
                return "The binary has to be arm9 or an overlay number like 0012.";
            string address = (NewAddress ?? "").Trim().Replace("0x", "", StringComparison.OrdinalIgnoreCase);
            if (!long.TryParse(address, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out at))
                return "The address has to be hex, like 02078384.";
            if (kind == HgEnginePatchKind.ByteReplacement)
            {
                foreach (string token in (NewBytes ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!byte.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                        return $"'{token}' is not a hex byte.";
                    bytes.Add(b);
                }
                if (bytes.Count == 0) return "Give at least one byte to write.";
            }
            else if (string.IsNullOrWhiteSpace(NewSymbol))
                return "Name the routine or table this points at.";
            // An empty register on a hook keeps the whole-routine form the list already used.
            if (kind == HgEnginePatchKind.Hook && string.IsNullOrWhiteSpace(NewRegister)) return null;
            return HgEnginePatchList.TryParseRegister(kind, NewRegister, out register, out string registerError) ? null : registerError;
        }

        /// <summary>Changes the selected patch to the values in the fields.</summary>
        public string ChangeSelected()
        {
            HgEnginePatchEntry entry = _selectedRow?.Entry;
            if (entry == null) return "Pick a patch in the table first.";
            HgEnginePatchList list = _lists.FirstOrDefault(l => l.Entries.Contains(entry));
            if (list == null) return "That patch is no longer in its list.";
            string why = ReadFields(list.Kind, out int overlay, out long at, out int register, out List<byte> bytes);
            if (why != null) return why;

            (bool Parsed, int OverlayNumber, string Symbol, long Address, int Register, IReadOnlyList<byte> Bytes, string RawLine, bool Pending) before = (entry.Parsed, entry.OverlayNumber, entry.Symbol, entry.Address, entry.Register, entry.Bytes, entry.RawLine, Pending: _pending.Contains(entry));
            HgEnginePatchEntry trial = new HgEnginePatchEntry { Kind = list.Kind, RawLine = entry.RawLine };
            string symbol = NewSymbol?.Trim();
            HgEnginePatchList.Change(trial, overlay, symbol, at, register, bytes);
            string problem = HgEnginePatchList.Problem(trial);
            if (problem != null) return problem;
            if (trial.RawLine == entry.RawLine) return null;

            Did(new Step(list,
                Undo: () =>
                {
                    (entry.Parsed, entry.OverlayNumber, entry.Symbol, entry.Address, entry.Register, entry.Bytes, entry.RawLine) =
                        (before.Parsed, before.OverlayNumber, before.Symbol, before.Address, before.Register, before.Bytes, before.RawLine);
                    if (!before.Pending) _pending.Remove(entry);
                },
                Redo: () =>
                {
                    HgEnginePatchList.Change(entry, overlay, symbol, at, register, bytes);
                    _pending.Add(entry);
                }));
            StatusText = $"Changed in {list.FileName}, not saved yet. {StatusText}";
            return null;
        }

        /// <summary>Takes the selected patch out of its list.</summary>
        public string DeleteSelected()
        {
            HgEnginePatchEntry entry = _selectedRow?.Entry;
            if (entry == null) return "Pick a patch in the table first.";
            HgEnginePatchList list = _lists.FirstOrDefault(l => l.Entries.Contains(entry));
            if (list == null) return "That patch is no longer in its list.";
            int at = list.Entries.IndexOf(entry);
            bool wasPending = _pending.Contains(entry);
            Did(new Step(list,
                Undo: () => { list.Entries.Insert(Math.Min(at, list.Entries.Count), entry); if (wasPending) _pending.Add(entry); },
                Redo: () => { list.Entries.Remove(entry); _pending.Remove(entry); }));
            SelectedRow = null;
            StatusText = $"Removed from {list.FileName}, not saved yet. {StatusText}";
            return null;
        }

        public string AddPatch()
        {
            if (NewList < 0 || NewList >= _lists.Count) return "Pick a list first.";
            HgEnginePatchList list = _lists[NewList];

            if (!HgEngineClaimedRanges.TryBinary((NewBinary ?? "").Trim(), out int overlay))
                return "The binary has to be arm9 or an overlay number like 0012.";

            string address = (NewAddress ?? "").Trim().Replace("0x", "", StringComparison.OrdinalIgnoreCase);
            if (!long.TryParse(address, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long at))
                return "The address has to be hex, like 02078384.";

            List<byte> bytes = new List<byte>();
            if (list.Kind == HgEnginePatchKind.ByteReplacement)
            {
                foreach (string token in (NewBytes ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!byte.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                        return $"'{token}' is not a hex byte.";
                    bytes.Add(b);
                }
                if (bytes.Count == 0) return "Give at least one byte to write.";
            }
            else if (string.IsNullOrWhiteSpace(NewSymbol))
            {
                return "Name the routine or table this points at.";
            }

            if (!HgEnginePatchList.TryParseRegister(list.Kind, NewRegister, out int register, out string registerError))
                return registerError;

            HgEnginePatchEntry added = list.Add(overlay, NewSymbol?.Trim(), at, register, bytes);
            string problem = HgEnginePatchList.Problem(added);
            list.Entries.Remove(added);
            if (problem != null) return problem;

            Did(new Step(list,
                Undo: () => { list.Entries.Remove(added); _pending.Remove(added); },
                Redo: () => { if (!list.Entries.Contains(added)) list.Entries.Add(added); _pending.Add(added); }));
            StatusText = $"Added to {list.FileName}, not saved yet. {StatusText}";
            return null;
        }

        /// <summary>Writes every list holding added patches. Null on success, otherwise why not.</summary>
        public string Save()
        {
            if (!IsAvailable) return "No hg-engine checkout is linked.";
            if (_done.Count == 0) return null;

            foreach (HgEnginePatchList list in _done.Select(s => s.List).Distinct().ToList())
            {
                if (!list.Save(out string error))
                {
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                    Rebuild();
                    return error;
                }
            }

            _lists = _readAll();
            _pending.Clear();
            ForgetSteps();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            Rebuild();
            return null;
        }

        public string Reload()
        {
            if (!IsAvailable) return "No hg-engine checkout is linked.";
            DiscardChanges();
            return null;
        }

        // ── Unsaved changes ─────────────────────────────────────────────────
        public bool HasUnsavedChanges => _done.Count > 0;

        public string UnsavedChangesDescription => _done.Count == 1
            ? "1 hg-engine patch change"
            : $"{_done.Count} hg-engine patch changes";

        public void SaveChanges() => Save();

        public Task<bool> SaveChangesAsync()
        {
            string error = Save();
            return error != null
                ? Task.FromException<bool>(new InvalidOperationException(error))
                : Task.FromResult(!HasUnsavedChanges);
        }

        public void DiscardChanges()
        {
            if (!IsAvailable) return;
            _lists = _readAll();
            _pending.Clear();
            ForgetSteps();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            Rebuild();
        }
    }
}

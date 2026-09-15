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

namespace DSPRE.Avalonia.ViewModels.Tools
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
    /// hg-engine's own patch lists, shown as what they do rather than as four columns of hex. The lists
    /// are the checkout's, so an added patch stays here until Save, which writes the list in place and
    /// leaves every comment alone.
    /// </summary>
    public class HgEnginePatchesViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private readonly Func<List<HgEnginePatchList>> _readAll;
        private List<HgEnginePatchList> _lists = new();

        // Entries added since the lists were last read, held only in memory.
        private readonly HashSet<HgEnginePatchEntry> _pending = new();

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
            foreach (var l in _lists) { ListNames.Add(l.FileName); NewListChoices.Add(l.FileName); }
            if (_lists.Count > 0) _newList = 0;

            Rebuild();
        }

        private void Rebuild()
        {
            Rows.Clear();

            var all = _lists.SelectMany(l => l.Entries.Where(e => e.Parsed).Select(e => (List: l, Entry: e))).ToList();

            RebuildBinaryChoices(all.Select(x => x.Entry.OverlayNumber).Distinct().OrderBy(n => n).ToList());

            int? wanted = BinaryFilter <= 0 ? null : OverlayForChoice(BinaryFilter);

            foreach (var (list, entry) in all
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
        /// A patch running into the middle of another is worth seeing. Two at the same address are not:
        /// hg-engine writes bytes and hooks at one spot on purpose, and the same line appears more than
        /// once when it sits in alternative #ifdef branches, which are not evaluated here.
        /// </summary>
        private void MarkClashes()
        {
            foreach (var group in Rows.Where(r => r.Offset.Length > 0).GroupBy(r => r.Entry.OverlayNumber))
            {
                var ordered = group
                    .Select(r => (Row: r,
                        Start: Convert.ToInt64(r.Offset.Substring(2), 16),
                        Len: int.TryParse(r.Size, out int l) ? l : 0))
                    .OrderBy(x => x.Start).ToList();

                for (int i = 1; i < ordered.Count; i++)
                {
                    var prev = ordered[i - 1];
                    var here = ordered[i];
                    if (here.Start == prev.Start) continue;
                    if (here.Start >= prev.Start + prev.Len) continue;

                    here.Row.Clash = $"starts inside {prev.Row.Symbol} at {prev.Row.Offset}";
                    prev.Row.Clash ??= $"{here.Row.Symbol} at {here.Row.Offset} starts inside this";
                }
            }
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

            var bytes = new List<byte>();
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

            var added = list.Add(overlay, NewSymbol?.Trim(), at, register, bytes);
            // Refused now rather than when Save writes the list.
            string problem = HgEnginePatchList.Problem(added);
            if (problem != null)
            {
                list.Entries.Remove(added);
                return problem;
            }

            _pending.Add(added);
            OnPropertyChanged(nameof(HasUnsavedChanges));
            Rebuild();
            StatusText = $"Added to {list.FileName}, not saved yet. {StatusText}";
            return null;
        }

        /// <summary>Writes every list holding added patches. Null on success, otherwise why not.</summary>
        public string Save()
        {
            if (!IsAvailable) return "No hg-engine checkout is linked.";
            if (_pending.Count == 0) return null;

            foreach (var list in _lists.Where(l => l.Entries.Any(_pending.Contains)).ToList())
            {
                if (!list.Save(out string error))
                {
                    OnPropertyChanged(nameof(HasUnsavedChanges));
                    Rebuild();
                    return error;
                }
                _pending.ExceptWith(list.Entries);
            }

            _lists = _readAll();
            _pending.Clear();
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
        public bool HasUnsavedChanges => _pending.Count > 0;

        public string UnsavedChangesDescription => _pending.Count == 1
            ? "1 added hg-engine patch"
            : $"{_pending.Count} added hg-engine patches";

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
            OnPropertyChanged(nameof(HasUnsavedChanges));
            Rebuild();
        }
    }
}

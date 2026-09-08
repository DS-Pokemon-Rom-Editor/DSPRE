using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
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

        /// <summary>Set when another entry writes over the same bytes, which is nearly always a mistake.</summary>
        public string Clash { get; set; }
        public bool HasClash => !string.IsNullOrEmpty(Clash);
    }

    /// <summary>
    /// hg-engine's own patch lists, shown as what they do rather than as four columns of hex. The lists
    /// are the checkout's, so this reads and writes them in place and leaves every comment alone.
    /// </summary>
    public class HgEnginePatchesViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private List<HgEnginePatchList> _lists = new();

        public ObservableCollection<PatchRow> Rows { get; } = new();
        public ObservableCollection<string> Binaries { get; } = new();
        public ObservableCollection<string> ListNames { get; } = new();

        public bool IsAvailable => HgEngineProject.IsActive;

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
        {
            if (!IsAvailable)
            {
                StatusText = "Link an hg-engine checkout to see the patches it applies.";
                return;
            }

            _lists = HgEnginePatchList.ReadAll();
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
                Rows.Add(new PatchRow
                {
                    Entry = entry,
                    List = list.FileName,
                    Offset = offset < 0 ? "" : $"0x{offset:X}",
                    Size = entry.Length.ToString(),
                });
            }

            MarkClashes();
            int clashes = Rows.Count(r => r.HasClash);
            StatusText = $"{Rows.Count} patch(es)" +
                (clashes > 0 ? $", {clashes} running into another" : "") + ".";
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

            int register = -1;
            if (list.Kind is HgEnginePatchKind.Hook or HgEnginePatchKind.ArmHook)
            {
                if (!int.TryParse((NewRegister ?? "").Trim(), out register)) register = -1;
            }

            list.Add(overlay, NewSymbol?.Trim(), at, register, bytes);
            if (!list.Save(out string error)) return error;

            _lists = HgEnginePatchList.ReadAll();
            Rebuild();
            StatusText = $"Added to {list.FileName}. {StatusText}";
            return null;
        }

        public string Reload()
        {
            if (!IsAvailable) return "No hg-engine checkout is linked.";
            _lists = HgEnginePatchList.ReadAll();
            Rebuild();
            return null;
        }
    }
}

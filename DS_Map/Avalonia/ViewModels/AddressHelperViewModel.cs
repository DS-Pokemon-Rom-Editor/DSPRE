using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using static DSPRE.DSUtils;

namespace DSPRE.Avalonia.ViewModels
{
    public class AddressHelperViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private int _overlaysSize;

        private const int ARM9LoadAddress = 0x02000000;

        // ── Bound properties ──────────────────────────────────────────────

        private string _addressInput = string.Empty;
        public string AddressInput
        {
            get => _addressInput;
            set { _addressInput = value; OnPropertyChanged(); }
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            private set { _statusMessage = value; OnPropertyChanged(); }
        }

        public ObservableCollection<AddressRow> Results { get; } = new ObservableCollection<AddressRow>();

        // ── Command ───────────────────────────────────────────────────────

        public void SearchCommand()
        {
            Results.Clear();
            StatusMessage = string.Empty;

            if (Design.IsDesignMode)
            {
                _overlaysSize = 0;
                Results.Add(new AddressRow("Overlay 42", "0x1234"));
                Results.Add(new AddressRow("ARM9", "0x0A2C"));
                Results.Add(new AddressRow("SynthOVL", "0x00F8"));
                StatusMessage = "Design‑time preview (no actual ROM loaded)";
                return;
            }

            try
            {
                _overlaysSize = OverlayUtils.OverlayTable.GetNumberOfOverlays();
                int addr = Convert.ToInt32(AddressInput.Trim(), 16);

                foreach (int ovl in GetOverlayNumbersFromAddress(addr))
                    Results.Add(new AddressRow("Overlay " + ovl, GetOffsetInOverlay(addr, ovl)));

                bool inArm9 = addr >= ARM9LoadAddress
                           && addr < OverlayUtils.OverlayTable.GetRAMAddress(0);
                if (inArm9)
                {
                    Results.Clear();
                    Results.Add(new AddressRow("ARM9", $"0x{(addr - ARM9LoadAddress):X4}"));
                }

                if (addr >= RomInfo.synthOverlayLoadAddress)
                    Results.Add(new AddressRow("SynthOVL", $"0x{addr - RomInfo.synthOverlayLoadAddress:X4}"));
            }
            catch
            {
                StatusMessage = "No overlay found for that address.";
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private List<int> GetOverlayNumbersFromAddress(int address)
        {
            var list = new List<int>();
            for (int i = 0; i < _overlaysSize - 1; i++)
            {
                uint ramAddr = OverlayUtils.OverlayTable.GetRAMAddress(i);
                if (ramAddr >= address && address < ramAddr + OverlayUtils.OverlayTable.GetUncompressedSize(i))
                    list.Add(i);
            }
            return list;
        }

        private static string GetOffsetInOverlay(int address, int ovlNumber)
            => $"0x{OverlayUtils.OverlayTable.GetRAMAddress(ovlNumber) - address:X4}";
    }

    public class AddressRow
    {
        public string Location { get; }
        public string Offset { get; }
        public AddressRow(string location, string offset) { Location = location; Offset = offset; }
    }
}

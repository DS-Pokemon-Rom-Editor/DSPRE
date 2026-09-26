using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Items
{
    /// <summary>Each berry's size, firmness, growth and flavours.</summary>
    public class BerryDataEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private List<BerryData> _berries = new List<BerryData>();
        private List<byte[]> _saved = new List<byte[]>();

        public List<string> BerryNames { get; } = new List<string>();
        public string[] FirmnessNames => BerryData.Firmness;
        public bool LooksUnread => !BerryData.GameReadsLooks;

        public BerryDataEditorViewModel() { }

        public BerryDataEditorViewModel(bool load)
        {
            if (!load) return;
            _berries = BerryData.LoadAll();
            _saved = _berries.Select(b => b.ToBytes()).ToList();
            string[] items = GetItemNames();
            for (int b = 0; b < BerryData.Count; b++)
            {
                int item = BerryData.FirstBerryItem + b;
                BerryNames.Add(item < items.Length ? items[item] : $"Berry {b + 1}");
            }
            _selected = 0;
        }

        private int _selected = -1;
        public int SelectedBerry
        {
            get => _selected;
            set { if (value < 0 || value >= _berries.Count || value == _selected) return; _selected = value; RaiseFields(); }
        }

        private BerryData Current => _selected >= 0 && _selected < _berries.Count ? _berries[_selected] : null;

        private decimal Get(Func<BerryData, int> read) => Current == null ? 0 : read(Current);
        private void Set(Action<BerryData> write) { if (Current == null) return; write(Current); RaiseFields(); }

        public decimal SizeMm { get => Get(b => b.SizeMm); set => Set(b => b.SizeMm = (ushort)Math.Clamp(value, 0, ushort.MaxValue)); }
        public int FirmnessIndex { get => Current == null ? -1 : Current.FirmnessLevel - 1; set { if (value >= 0 && value < 5) Set(b => b.FirmnessLevel = (byte)(value + 1)); } }
        public decimal Yield { get => Get(b => b.Yield); set => Set(b => b.Yield = Byte(value)); }
        public decimal HoursPerStage { get => Get(b => b.HoursPerStage); set => Set(b => b.HoursPerStage = Byte(value)); }
        public decimal Drain { get => Get(b => b.Drain); set => Set(b => b.Drain = Byte(value)); }
        public decimal Spicy { get => Get(b => b.Flavour[0]); set => Set(b => b.Flavour[0] = Byte(value)); }
        public decimal Dry { get => Get(b => b.Flavour[1]); set => Set(b => b.Flavour[1] = Byte(value)); }
        public decimal Sweet { get => Get(b => b.Flavour[2]); set => Set(b => b.Flavour[2] = Byte(value)); }
        public decimal Bitter { get => Get(b => b.Flavour[3]); set => Set(b => b.Flavour[3] = Byte(value)); }
        public decimal Sour { get => Get(b => b.Flavour[4]); set => Set(b => b.Flavour[4] = Byte(value)); }
        public decimal Smoothness { get => Get(b => b.Smoothness); set => Set(b => b.Smoothness = Byte(value)); }

        private static byte Byte(decimal v) => (byte)Math.Clamp(v, 0, 255);

        public string Problem => Current?.Problem() ?? "";
        public bool HasProblem => Problem.Length > 0;

        private void RaiseFields()
        {
            foreach (var n in new[] { nameof(SelectedBerry), nameof(SizeMm), nameof(FirmnessIndex), nameof(Yield), nameof(HoursPerStage),
                                      nameof(Drain), nameof(Spicy), nameof(Dry), nameof(Sweet), nameof(Bitter), nameof(Sour),
                                      nameof(Smoothness), nameof(Problem), nameof(HasProblem), nameof(HasUnsavedChanges) })
                Raise(n);
        }

        private bool BerryChanged(int b) => !_berries[b].ToBytes().AsSpan().SequenceEqual(_saved[b]);

        public bool HasUnsavedChanges => Enumerable.Range(0, _berries.Count).Any(BerryChanged);
        public string UnsavedChangesDescription => "Berry data";

        public void SaveChanges() => _ = SaveChangesAsync();

        public async Task<bool> SaveChangesAsync()
        {
            var changed = Enumerable.Range(0, _berries.Count).Where(BerryChanged).ToList();
            foreach (int b in changed)
                if (_berries[b].Problem() is string p) { await DialogHelper.ShowError($"{BerryNames[b]}: {p}", "Berry Data"); return false; }
            try
            {
                foreach (int b in changed) { _berries[b].Save(b); _saved[b] = _berries[b].ToBytes(); }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException)
            {
                await DialogHelper.ShowError("The berry data was not saved:\n" + e.Message, "Berry Data");
                return false;
            }
            Raise(nameof(HasUnsavedChanges));
            SaveNotice.Saved(UnsavedChangesDescription);
            return true;
        }

        public void DiscardChanges()
        {
            for (int b = 0; b < _berries.Count; b++) _berries[b] = new BerryData(_saved[b]);
            RaiseFields();
        }
    }
}

using System;
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
    /// <summary>The chance of each wild encounter slot, per encounter method.</summary>
    public class EncounterSlotOddsViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private EncounterSlotOdds _odds;
        private byte[] _saved;

        public ObservableCollection<MethodViewModel> Methods { get; } = new ObservableCollection<MethodViewModel>();

        public EncounterSlotOddsViewModel() { }

        public EncounterSlotOddsViewModel(bool load)
        {
            if (!load) return;
            _odds = EncounterSlotOdds.Load();
            _saved = _odds.Snapshot();
            foreach (var m in _odds.Methods) Methods.Add(new MethodViewModel(this, m));
        }

        public sealed class MethodViewModel : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private readonly EncounterSlotOddsViewModel _o;
            internal readonly EncounterSlotOdds.Method Method;
            public string Name => Method.Name;
            public ObservableCollection<SlotViewModel> Slots { get; } = new ObservableCollection<SlotViewModel>();

            public MethodViewModel(EncounterSlotOddsViewModel owner, EncounterSlotOdds.Method m)
            {
                _o = owner; Method = m;
                for (int i = 0; i < m.Percents.Length; i++) Slots.Add(new SlotViewModel(this, i));
            }

            public string Total => $"Total {Method.Percents.Sum()}%";
            public bool IsOff => Method.Percents.Sum() != 100;
            public string Note => Name == "Walking" && gameFamily != GameFamilies.HGSS
                ? "The Poké Radar uses these odds too" : null;

            internal void Changed()
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Total)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsOff)));
                _o.Changed();
            }

            internal void Refresh()
            {
                foreach (var s in Slots) s.Refresh();
                Changed();
            }
        }

        public sealed class SlotViewModel : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private readonly MethodViewModel _m;
            private readonly int _i;
            public SlotViewModel(MethodViewModel m, int i) { _m = m; _i = i; }
            public string Label => $"Slot {_i + 1}";
            public decimal Percent
            {
                get => _m.Method.Percents[_i];
                set
                {
                    int v = (int)Math.Clamp(value, 0, 100);
                    if (v == _m.Method.Percents[_i]) return;
                    _m.Method.Percents[_i] = v;
                    _m.Changed();
                }
            }
            internal void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Percent)));
        }

        public string Problem => _odds?.Problem() ?? "";
        public bool HasProblem => Problem.Length > 0;

        internal void Changed()
        {
            foreach (var n in new[] { nameof(Problem), nameof(HasProblem), nameof(HasUnsavedChanges) }) Raise(n);
        }

        public bool HasUnsavedChanges => _odds != null && !_odds.Snapshot().AsSpan().SequenceEqual(_saved);
        public string UnsavedChangesDescription => "Encounter slot odds";

        public void SaveChanges() => _ = SaveChangesAsync();

        public async Task<bool> SaveChangesAsync()
        {
            if (_odds == null) return true;
            if (HasProblem) { await DialogHelper.ShowError(Problem, "Encounter Slot Odds"); return false; }
            try { _odds.Save(); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException)
            {
                await DialogHelper.ShowError("The slot odds were not saved:\n" + e.Message, "Encounter Slot Odds");
                return false;
            }
            _saved = _odds.Snapshot();
            Changed();
            SaveNotice.Saved(UnsavedChangesDescription);
            return true;
        }

        public void DiscardChanges()
        {
            if (_odds == null) return;
            _odds.Restore(_saved);
            foreach (var m in Methods) m.Refresh();
            Changed();
        }
    }
}

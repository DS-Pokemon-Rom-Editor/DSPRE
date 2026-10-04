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
    public class EncounterSlotOddsViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private EncounterSlotOdds _odds;
        private byte[] _saved;

        private ByteStateUndo _undo;
        private void StartUndo() => _undo = new ByteStateUndo(() => _odds.Snapshot(), b => { _odds.Restore(b); foreach (var m in Methods) m.Refresh(); Changed(); }, () => { Raise(nameof(CanUndo)); Raise(nameof(CanRedo)); });
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();

        public ObservableCollection<MethodViewModel> Methods { get; } = new ObservableCollection<MethodViewModel>();

        public EncounterSlotOddsViewModel() { }

        public EncounterSlotOddsViewModel(bool load)
        {
            if (!load) return;
            _odds = EncounterSlotOdds.Load();
            _saved = _odds.Snapshot();
            foreach (var m in _odds.Methods) Methods.Add(new MethodViewModel(this, m));
            StartUndo();
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

            // Alternating shades so neighbouring slots stay apart in the bar.
            private static readonly global::Avalonia.Media.Color[] Shades =
            {
                global::Avalonia.Media.Color.FromRgb(0x3A, 0x7B, 0xC8), global::Avalonia.Media.Color.FromRgb(0x2E, 0x9E, 0x8F),
                global::Avalonia.Media.Color.FromRgb(0x7A, 0x5C, 0xC0), global::Avalonia.Media.Color.FromRgb(0xC8, 0x7B, 0x2E),
            };
            internal static global::Avalonia.Media.Color ShadeOf(int slot) => Shades[slot % Shades.Length];

            public System.Collections.Generic.IReadOnlyList<DSPRE.Avalonia.Controls.BarPart> Parts =>
                Method.Percents.Select((p, i) => new DSPRE.Avalonia.Controls.BarPart { Value = p, Label = (i + 1).ToString(), Colour = ShadeOf(i) }).ToList();

            internal void Changed()
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Total)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsOff)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Parts)));
                foreach (var slot in Slots) slot.Refresh();
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
            public System.Collections.Generic.IReadOnlyList<DSPRE.Avalonia.Controls.BarPart> Bar => new[]
            {
                new DSPRE.Avalonia.Controls.BarPart { Value = _m.Method.Percents[_i], Colour = MethodViewModel.ShadeOf(_i) },
                new DSPRE.Avalonia.Controls.BarPart { Value = Math.Max(0, 100 - _m.Method.Percents[_i]), Colour = global::Avalonia.Media.Colors.Transparent },
            };
            internal void Refresh()
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Percent)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Bar)));
            }
        }

        public string Problem => _odds?.Problem() ?? "";
        public bool HasProblem => Problem.Length > 0;

        internal void Changed()
        {
            foreach (var n in new[] { nameof(Problem), nameof(HasProblem), nameof(HasUnsavedChanges) }) Raise(n);
            _undo?.Record();
        }

        public bool HasUnsavedChanges => _odds != null && !_odds.Snapshot().AsSpan().SequenceEqual(_saved);
        public string UnsavedChangesDescription => "Encounter slot odds";

        public void SaveChanges() => _ = SaveChangesAsync();

        public async Task<bool> SaveChangesAsync()
        {
            if (_odds == null) return true;
            if (HasProblem) { await DialogHelper.ShowError(Problem, "Encounter Slot Odds"); return false; }
            if (DSPRE.HgEngine.HgEngineProject.IsActive)
            {
                // The rolls are rewritten in encounter_check.c, which may drop comments inside them.
                var (saved, error) = await HgEngineSave.RunAsync(() => { _odds.Save(); return null; });
                if (!saved)
                {
                    if (error != null) await DialogHelper.ShowError("The slot odds were not saved:\n" + error, "Encounter Slot Odds");
                    return false;
                }
            }
            else
            {
                try { _odds.Save(); }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException)
                {
                    await DialogHelper.ShowError("The slot odds were not saved:\n" + e.Message, "Encounter Slot Odds");
                    return false;
                }
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
            StartUndo();
            Changed();
        }
    }
}

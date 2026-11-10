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
    public class EncounterSlotOddsViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo, IDetachable
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private EncounterSlotOdds _odds;
        private byte[] _saved;

        private ByteStateUndo _undo;
        private void StartUndo() => _undo = new ByteStateUndo(() => _odds.Snapshot(), b => { _odds.Restore(b); foreach (MethodViewModel m in Methods) m.Refresh(); Changed(); }, () => { Raise(nameof(CanUndo)); Raise(nameof(CanRedo)); });
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
            foreach (EncounterSlotOdds.Method m in _odds.Methods) Methods.Add(new MethodViewModel(this, m));
            StartUndo();
            CheckHooks();
            // A fix saved in the hooks editor clears the warning here.
            DSPRE.HgEngine.HgEngineHookList.Saved += OnHooksSaved;
        }

        private void OnHooksSaved() => global::Avalonia.Threading.Dispatcher.UIThread.Post(CheckHooks);
        public void Detach() => DSPRE.HgEngine.HgEngineHookList.Saved -= OnHooksSaved;

        // A hooks file that puts the slot hooks in arm9 never applies them, so these odds never run. hg-engine pull request 1039 fixes it.
        private string _hookWarning;
        public string HookWarning => _hookWarning;
        public bool HasHookWarning => _hookWarning != null;
        public string FirstMislabelledHook { get; private set; }

        // "line 654", "lines 654 to 658" for a run, otherwise "lines 654, 656 and 658".
        private static string LineList(System.Collections.Generic.List<int> lines)
        {
            if (lines.Count == 1) return $"line {lines[0]}";
            bool run = lines.Zip(lines.Skip(1)).All(p => p.Second == p.First + 1);
            return run ? $"lines {lines[0]} to {lines[^1]}"
                : $"lines {string.Join(", ", lines.Take(lines.Count - 1))} and {lines[^1]}";
        }

        public void CheckHooks()
        {
            System.Collections.Generic.List<(string Symbol, int Line)> wrong = DSPRE.HgEngine.HgEngineProject.IsActive
                ? DSPRE.HgEngine.HgEngineSlotOdds.MislabelledHooks() : new System.Collections.Generic.List<(string, int)>();
            FirstMislabelledHook = wrong.Count > 0 ? wrong[0].Symbol : null;
            _hookWarning = wrong.Count == 0 ? null
                : "This checkout's hooks file puts the slot hooks in arm9 instead of overlay 2, so these odds never run. " +
                  $"Change arm9 to 0002 on {LineList(wrong.Select(w => w.Line).ToList())} of the hooks file, or in the hooks editor. " +
                  "This pull request fixes it:";
            Raise(nameof(HookWarning));
            Raise(nameof(HasHookWarning));
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
            public string Note => Name == "Walking" && GameFamily != GameFamilies.HGSS
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
                foreach (SlotViewModel slot in Slots) slot.Refresh();
                _o.Changed();
            }

            internal void Refresh()
            {
                foreach (SlotViewModel s in Slots) s.Refresh();
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
            foreach (string n in new[] { nameof(Problem), nameof(HasProblem), nameof(HasUnsavedChanges) }) Raise(n);
            _undo?.Record();
        }

        public bool HasUnsavedChanges => _odds != null && !_odds.Snapshot().AsSpan().SequenceEqual(_saved);
        public string UnsavedChangesDescription => "Encounter slot odds";

        public void SaveChanges() => DialogHelper.Watch(SaveChangesAsync());

        public async Task<bool> SaveChangesAsync()
        {
            if (_odds == null) return true;
            if (HasProblem) { await DialogHelper.ShowErrorAsync(Problem, "Encounter slot odds"); return false; }
            if (DSPRE.HgEngine.HgEngineProject.IsActive)
            {
                // The rolls are rewritten in encounter_check.c, which may drop comments inside them.
                (bool saved, string error) = await HgEngineSave.RunAsync(() => { _odds.Save(); return null; });
                if (!saved)
                {
                    if (error != null) await DialogHelper.ShowErrorAsync("The slot odds weren't saved:\n" + error, "Encounter slot odds");
                    return false;
                }
            }
            else
            {
                try { _odds.Save(); }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException || e is InvalidDataException)
                {
                    await DialogHelper.ShowErrorAsync("The slot odds weren't saved:\n" + e.Message, "Encounter slot odds");
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
            foreach (MethodViewModel m in Methods) m.Refresh();
            StartUndo();
            Changed();
        }
    }
}

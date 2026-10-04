using DSPRE.Editors;
using DSPRE.HgEngine;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>hg-engine's per-ability battle flags from data/AbilityFlags.c.</summary>
    public class AbilityFlagsEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        // What each of hg-engine's flags stops or allows; a flag a checkout adds shows its own name.
        private static readonly Dictionary<string, string> Labels = new()
        {
            ["ignoredByMoldBreaker"] = "Ignored by Mold Breaker",
            ["disabledWhenTransformed"] = "Off while transformed",
            ["disabledByNeutralizingGas"] = "Off under Neutralizing Gas",
            ["failsTrace"] = "Trace can't copy it",
            ["failsSwap"] = "Skill Swap fails",
            ["failsSuppress"] = "Gastro Acid and Mummy can't replace it",
            ["failsReceiver"] = "Receiver can't copy it",
            ["failsEntrainment"] = "Entrainment fails",
            ["failsRolePlay"] = "Role Play fails",
        };

        public sealed class AbilityRow
        {
            public int Id { get; init; }
            public string Name { get; init; }
            public string Display => $"{Id:D3} {Name}";
        }

        public sealed class FlagItem : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            internal Action<FlagItem> Changed;
            public string Key { get; init; }
            public string Label { get; init; }
            private bool _set;
            public bool IsSet
            {
                get => _set;
                set { if (_set == value) return; _set = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSet))); Changed?.Invoke(this); }
            }
            internal void Show(bool set) { _set = set; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSet))); }
        }

        private readonly List<AbilityRow> _all = new();
        public ObservableCollection<AbilityRow> Abilities { get; } = new();
        public ObservableCollection<FlagItem> Flags { get; } = new();

        private Dictionary<int, HashSet<string>> _saved = new(), _current = new();

        private string _status = "";
        public string StatusText { get => _status; private set { _status = value; OnPropertyChanged(); } }

        private string _filter = "";
        public string Filter { get => _filter; set { _filter = value ?? ""; OnPropertyChanged(); ApplyFilter(); } }

        private AbilityRow _selected;
        public AbilityRow Selected
        {
            get => _selected;
            set { _selected = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSelection)); ShowFlags(); }
        }
        public bool HasSelection => _selected != null;

        public AbilityFlagsEditorViewModel() { }

        public AbilityFlagsEditorViewModel(bool load)
        {
            if (!load) return;
            foreach (string key in HgEngineAbilityFlags.FlagNames())
                Flags.Add(new FlagItem { Key = key, Label = Labels.TryGetValue(key, out string l) ? l : Humanize(key), Changed = OnFlagChanged });

            string[] names = Array.Empty<string>();
            try { names = RomInfo.GetAbilityNames(); } catch (Exception e) when (e is System.IO.IOException || e is ArgumentException) { }
            var table = HgEngineSymbolTable.Load("include/constants/ability.h");
            for (int id = 0; table != null && table.TryGetNameWithPrefix(id, "ABILITY_", out string symbol); id++)
                _all.Add(new AbilityRow { Id = id, Name = id < names.Length && names[id].Trim().Length > 0 ? names[id] : Humanize(symbol.Substring(8).ToLowerInvariant()) });
            Load();
        }

        private void Load()
        {
            if (!HgEngineAbilityFlags.TryRead(out var flags, out string error)) { StatusText = error; return; }
            _saved = flags.ToDictionary(kv => kv.Key, kv => new HashSet<string>(kv.Value));
            _current = flags.ToDictionary(kv => kv.Key, kv => new HashSet<string>(kv.Value));
            StatusText = $"{_all.Count} abilities, {_saved.Count(kv => kv.Value.Count > 0)} with flags · {HgEngineAbilityFlags.RelPath}";
            ApplyFilter();
            ShowFlags();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            _undo = new ByteStateUndo(TakeState, ApplyState, () => { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); });
            OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo));
        }

        private ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();

        private byte[] TakeState() => ByteStateUndo.Pack(w =>
        {
            var ids = _current.Where(kv => kv.Value.Count > 0).Select(kv => kv.Key).OrderBy(i => i).ToList();
            w.Write(ids.Count);
            foreach (int id in ids)
            {
                w.Write(id);
                var flags = _current[id].OrderBy(f => f, StringComparer.Ordinal).ToList();
                w.Write(flags.Count);
                foreach (string f in flags) w.Write(f);
            }
        });

        // Selects the ability the step changed, so the undone flag is in view.
        private void ApplyState(byte[] state)
        {
            var next = new Dictionary<int, HashSet<string>>();
            ByteStateUndo.Unpack(state, r =>
            {
                for (int n = r.ReadInt32(), i = 0; i < n; i++)
                {
                    int id = r.ReadInt32();
                    var set = new HashSet<string>();
                    for (int m = r.ReadInt32(), j = 0; j < m; j++) set.Add(r.ReadString());
                    next[id] = set;
                }
            });
            int changed = next.Keys.Concat(_current.Keys).Distinct()
                .Where(id => !Same(next.TryGetValue(id, out var a) ? a : null, _current.TryGetValue(id, out var b) ? b : null))
                .DefaultIfEmpty(-1).First();
            _current = next;
            var row = Abilities.FirstOrDefault(a => a.Id == changed);
            if (row != null && row != _selected) Selected = row; else ShowFlags();
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void ApplyFilter()
        {
            var keep = _selected;
            Abilities.Clear();
            foreach (var a in _all.Where(a => _filter.Length == 0 || a.Display.Contains(_filter, StringComparison.OrdinalIgnoreCase))) Abilities.Add(a);
            Selected = keep != null && Abilities.Contains(keep) ? keep : Abilities.FirstOrDefault();
        }

        private void ShowFlags()
        {
            var set = _selected != null && _current.TryGetValue(_selected.Id, out var s) ? s : null;
            foreach (var f in Flags) f.Show(set?.Contains(f.Key) == true);
        }

        private void OnFlagChanged(FlagItem item)
        {
            if (_selected == null) return;
            if (!_current.TryGetValue(_selected.Id, out var set)) _current[_selected.Id] = set = new HashSet<string>();
            if (item.IsSet) set.Add(item.Key); else set.Remove(item.Key);
            OnPropertyChanged(nameof(HasUnsavedChanges));
            _undo?.Record();
        }

        private static bool Same(HashSet<string> a, HashSet<string> b) => (a?.Count ?? 0) == 0 ? (b?.Count ?? 0) == 0 : b != null && a.SetEquals(b);

        private Dictionary<int, HashSet<string>> Changes() =>
            _current.Where(kv => !Same(kv.Value, _saved.TryGetValue(kv.Key, out var s) ? s : null)).ToDictionary(kv => kv.Key, kv => new HashSet<string>(kv.Value));

        public bool HasUnsavedChanges => Changes().Count > 0;
        public string UnsavedChangesDescription => "Ability Flags";

        public void SaveChanges() => _ = SaveChangesAsync();

        public async Task<bool> SaveChangesAsync()
        {
            var changes = Changes();
            if (changes.Count == 0) return true;
            var (saved, error) = await HgEngineSave.RunAsync(() => HgEngineAbilityFlags.TryWrite(changes, out string writeError) ? null : writeError);
            if (!saved)
            {
                if (error != null) await DialogHelper.ShowError($"The ability flags were not saved:\n{error}", "Ability Flags");
                return false;
            }
            foreach (var (id, set) in changes) _saved[id] = new HashSet<string>(set);
            OnPropertyChanged(nameof(HasUnsavedChanges));
            SaveNotice.Saved(UnsavedChangesDescription);
            return true;
        }

        public void DiscardChanges() => Load();

        private static string Humanize(string key)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in key.Replace('_', ' '))
            {
                if (char.IsUpper(c) && sb.Length > 0 && sb[^1] != ' ') sb.Append(' ');
                sb.Append(sb.Length == 0 ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }
}

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
    /// <summary>hg-engine's small source tables: Moon Ball species, Camouflage, Secret Power, type-boosting hold effects, mints.</summary>
    public class HgEngineTablesViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges, ISupportsUndo
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public sealed class TableRow : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            internal Action Changed;
            public string Key { get; init; }
            public string KeyText { get; init; }
            public bool Editable { get; init; }
            public IReadOnlyList<string> Choices { get; init; }
            public string Tip => Editable ? null : "Depends on a build option";
            /// <summary>The picked species', item's or type's icon, or null for a table of other values.</summary>
            internal Func<int, object> IconOf;
            public global::Avalonia.Media.IImage Icon => IconOf?.Invoke(_index) as global::Avalonia.Media.IImage;
            public bool HasIcon => IconOf != null;
            private int _index;
            public int Index
            {
                get => _index;
                set { if (value < 0 || _index == value) return; Show(value); Changed?.Invoke(); }
            }
            internal void Show(int index)
            {
                _index = index;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Index)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
            }
        }

        public ObservableCollection<string> Tables { get; } = new();
        public ObservableCollection<TableRow> Rows { get; } = new();

        private readonly List<HgEngineSourceTables.Info> _infos = new();
        private readonly Dictionary<int, List<string>> _names = new(), _shown = new();
        private Dictionary<int, List<HgEngineSourceTables.Row>> _saved = new(), _current = new();

        private int _table = -1;
        public int SelectedTable { get => _table; set { if (_table == value || value < 0) return; _table = value; OnPropertyChanged(); ShowRows(); } }

        public bool IsList => _table >= 0 && _infos[_table].IsList;

        private string _status = "";
        public string StatusText { get => _status; private set { _status = value; OnPropertyChanged(); } }

        public HgEngineTablesViewModel() { }

        public HgEngineTablesViewModel(bool load)
        {
            if (!load) return;
            foreach (HgEngineSourceTables.Info info in HgEngineSourceTables.All.Where(i => HgEngineSourceTables.WhyNot(i) == null))
            {
                _infos.Add(info);
                Tables.Add(info.Label);
                List<string> names = HgEngineSourceTables.Choices(info);
                _names[_infos.Count - 1] = names;
                _shown[_infos.Count - 1] = Shown(info, names);
                _icons[_infos.Count - 1] = Icons(info, names);
            }
            Load();
        }

        private readonly Dictionary<int, Func<int, object>> _icons = new();

        // Species, items and types show their icon beside the pick, as elsewhere in DSPRE.
        private static Func<int, object> Icons(HgEngineSourceTables.Info info, List<string> names)
        {
            DSPRE.Avalonia.Controls.GameIconConverter icons = info.Prefix switch
            {
                "SPECIES_" => DSPRE.Avalonia.Controls.GameIconConverter.Pokemon,
                "ITEM_" => DSPRE.Avalonia.Controls.GameIconConverter.Item,
                "TYPE_" => DSPRE.Avalonia.Controls.GameIconConverter.Type,
                _ => null,
            };
            HgEngineSymbolTable table = icons == null ? null : HgEngineSymbolTable.Load(info.Header);
            if (table == null) return null;
            int[] ids = names.Select(n => table.TryGetValue(n, out int id) ? id : -1).ToArray();
            return index => index >= 0 && index < ids.Length && ids[index] >= 0
                ? icons.Convert(ids[index], typeof(object), null, System.Globalization.CultureInfo.InvariantCulture) : null;
        }

        // Species and items show the project's own names, as the other editors do; the rest are tidied macro names.
        private static List<string> Shown(HgEngineSourceTables.Info info, List<string> names)
        {
            string[] project = info.Prefix switch { "SPECIES_" => RomInfo.GetPokemonNames(), "ITEM_" => RomInfo.GetItemNames(), _ => null };
            HgEngineSymbolTable table = project == null ? null : HgEngineSymbolTable.Load(info.Header);
            return names.Select(n => table != null && table.TryGetValue(n, out int id) && id >= 0 && id < project.Length && !string.IsNullOrWhiteSpace(project[id])
                ? project[id] : Pretty(n, info.Prefix)).ToList();
        }

        private void Load()
        {
            _saved.Clear();
            for (int t = 0; t < _infos.Count; t++)
                if (HgEngineSourceTables.TryRead(_infos[t], out List<HgEngineSourceTables.Row> rows, out string error)) _saved[t] = rows;
                else StatusText = error;
            _current = _saved.ToDictionary(kv => kv.Key, kv => kv.Value.ToList());
            _undo = new ByteStateUndo(TakeState, ApplyState, () => { OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo)); });
            if (_table < 0 && _infos.Count > 0) SelectedTable = 0; else ShowRows();
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(CanRedo));
        }

        // "TYPE_FIGHTING" shows as "Fighting".
        private static string Pretty(string name, string prefix)
        {
            string rest = name.StartsWith(prefix, StringComparison.Ordinal) ? name.Substring(prefix.Length) : name;
            string words = rest.Replace('_', ' ').ToLowerInvariant();
            return words.Length == 0 ? name : char.ToUpperInvariant(words[0]) + words.Substring(1);
        }

        private static string KeyText(string key, string prefix) =>
            prefix != null && key.StartsWith(prefix, StringComparison.Ordinal) ? Pretty(key, prefix) : key;

        private void ShowRows()
        {
            Rows.Clear();
            OnPropertyChanged(nameof(IsList));
            if (_table < 0 || !_current.TryGetValue(_table, out List<HgEngineSourceTables.Row> rows)) return;
            List<string> names = _names[_table];
            for (int i = 0; i < rows.Count; i++)
            {
                int at = i;
                TableRow row = new TableRow
                {
                    Key = rows[i].Key, KeyText = KeyText(rows[i].Key, _infos[_table].KeyPrefix), Editable = !rows[i].Conditional, Choices = _shown[_table],
                    IconOf = _icons.TryGetValue(_table, out Func<int, object> icon) ? icon : null,
                };
                row.Show(names.IndexOf(rows[i].Value));
                row.Changed = () => SetValue(at, names[row.Index]);
                Rows.Add(row);
            }
            StatusText = $"{rows.Count} rows · {_infos[_table].File}";
        }

        private void SetValue(int at, string value)
        {
            List<HgEngineSourceTables.Row> rows = _current[_table];
            rows[at] = rows[at] with { Value = value };
            Changed();
        }

        public void AddRow()
        {
            if (!IsList || !_current.TryGetValue(_table, out List<HgEngineSourceTables.Row> rows) || _names[_table].Count == 0) return;
            rows.Add(new HgEngineSourceTables.Row("", _names[_table][0], false));
            ShowRows();
            Changed();
        }

        public void RemoveRow(TableRow row)
        {
            int at = Rows.IndexOf(row);
            if (!IsList || at < 0 || !row.Editable) return;
            _current[_table].RemoveAt(at);
            ShowRows();
            Changed();
        }

        private void Changed()
        {
            OnPropertyChanged(nameof(HasUnsavedChanges));
            _undo?.Record();
        }

        private ByteStateUndo _undo;
        public bool CanUndo => _undo?.CanUndo == true;
        public bool CanRedo => _undo?.CanRedo == true;
        public void Undo() => _undo?.Undo();
        public void Redo() => _undo?.Redo();

        private byte[] TakeState() => ByteStateUndo.Pack(w =>
        {
            w.Write(_current.Count);
            foreach ((int t, List<HgEngineSourceTables.Row> rows) in _current.OrderBy(kv => kv.Key))
            {
                w.Write(t);
                w.Write(rows.Count);
                foreach (HgEngineSourceTables.Row r in rows) { w.Write(r.Key); w.Write(r.Value); w.Write(r.Conditional); }
            }
        });

        private void ApplyState(byte[] state)
        {
            Dictionary<int, List<HgEngineSourceTables.Row>> next = new Dictionary<int, List<HgEngineSourceTables.Row>>();
            ByteStateUndo.Unpack(state, r =>
            {
                for (int n = r.ReadInt32(), i = 0; i < n; i++)
                {
                    int t = r.ReadInt32();
                    List<HgEngineSourceTables.Row> rows = new List<HgEngineSourceTables.Row>();
                    for (int m = r.ReadInt32(), j = 0; j < m; j++) rows.Add(new HgEngineSourceTables.Row(r.ReadString(), r.ReadString(), r.ReadBoolean()));
                    next[t] = rows;
                }
            });
            _current = next;
            ShowRows();
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private List<int> ChangedTables() =>
            _current.Where(kv => !_saved.TryGetValue(kv.Key, out List<HgEngineSourceTables.Row> s) || !s.SequenceEqual(kv.Value)).Select(kv => kv.Key).ToList();

        public bool HasUnsavedChanges => ChangedTables().Count > 0;
        public string UnsavedChangesDescription => "hg-engine tables";

        public void SaveChanges() => DialogHelper.Watch(SaveChangesAsync());

        public async Task<bool> SaveChangesAsync()
        {
            List<int> changed = ChangedTables();
            if (changed.Count == 0) return true;
            Dictionary<int, List<HgEngineSourceTables.Row>> rows = changed.ToDictionary(t => t, t => _current[t].ToList());
            (bool saved, string error) = await HgEngineSave.RunAsync(() =>
            {
                foreach ((int t, List<HgEngineSourceTables.Row> r) in rows)
                    if (!HgEngineSourceTables.TryWrite(_infos[t], r, out string writeError)) return writeError;
                return null;
            });
            if (!saved)
            {
                if (error != null) await DialogHelper.ShowErrorAsync($"The tables weren't saved:\n{error}", "hg-engine tables");
                return false;
            }
            foreach ((int t, List<HgEngineSourceTables.Row> r) in rows) _saved[t] = r;
            OnPropertyChanged(nameof(HasUnsavedChanges));
            SaveNotice.Saved(UnsavedChangesDescription);
            return true;
        }

        public void DiscardChanges() => Load();
    }
}

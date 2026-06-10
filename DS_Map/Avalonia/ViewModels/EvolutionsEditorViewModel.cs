using Avalonia.Controls;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>One of the 7 evolution slots shown in the Evolutions tab.</summary>
    public class EvolutionRowViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // Name arrays injected by parent ViewModel so ParamLabel can show actual names
        internal string[] ItemNames;
        internal string[] MoveNames;
        internal string[] PokemonNames;

        private int _methodIndex;
        public int MethodIndex
        {
            get => _methodIndex;
            set { if (_methodIndex != value) { _methodIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(ParamLabel)); OnPropertyChanged(nameof(IsParamEnabled)); OnPropertyChanged(nameof(ParamMaximum)); Changed?.Invoke(); } }
        }

        private int _targetIndex;
        public int TargetIndex
        {
            get => _targetIndex;
            set { if (_targetIndex != value) { _targetIndex = value; OnPropertyChanged(); Changed?.Invoke(); } }
        }

        private int _param;
        public int Param
        {
            get => _param;
            set { if (_param != value) { _param = value; OnPropertyChanged(); OnPropertyChanged(nameof(ParamLabel)); Changed?.Invoke(); } }
        }

        public string ParamLabel
        {
            get
            {
                if (_methodIndex < 0 || _methodIndex >= Enum.GetValues<EvolutionMethod>().Length)
                    return string.Empty;
                var method = (EvolutionMethod)_methodIndex;
                if (!EvolutionFile.evoDescriptions.TryGetValue(method, out var meaning))
                    return string.Empty;
                switch (meaning)
                {
                    case EvolutionParamMeaning.FromLevel:
                        return $"From Level: {_param}";
                    case EvolutionParamMeaning.ItemName:
                        if (ItemNames != null && _param >= 0 && _param < ItemNames.Length)
                            return $"({ItemNames[_param]})";
                        return $"(Item #{_param})";
                    case EvolutionParamMeaning.MoveName:
                        if (MoveNames != null && _param >= 0 && _param < MoveNames.Length)
                            return $"({MoveNames[_param]})";
                        return $"(Move #{_param})";
                    case EvolutionParamMeaning.PokemonName:
                        if (PokemonNames != null && _param >= 0 && _param < PokemonNames.Length)
                            return $"({PokemonNames[_param]})";
                        return $"(Pokémon #{_param})";
                    case EvolutionParamMeaning.BeautyValue:
                        return $"Beauty >= {_param}";
                    default:
                        return string.Empty;
                }
            }
        }

        public decimal ParamMaximum
        {
            get
            {
                if (_methodIndex < 0 || _methodIndex >= Enum.GetValues<EvolutionMethod>().Length)
                    return 65535;
                var method = (EvolutionMethod)_methodIndex;
                if (!EvolutionFile.evoDescriptions.TryGetValue(method, out var meaning))
                    return 65535;
                switch (meaning)
                {
                    case EvolutionParamMeaning.FromLevel:
                        return 100;
                    case EvolutionParamMeaning.ItemName:
                        return ItemNames != null ? ItemNames.Length - 1 : 65535;
                    case EvolutionParamMeaning.MoveName:
                        return MoveNames != null ? MoveNames.Length - 1 : 65535;
                    case EvolutionParamMeaning.PokemonName:
                        return PokemonNames != null ? PokemonNames.Length - 1 : 65535;
                    case EvolutionParamMeaning.BeautyValue:
                        return 255;
                    default:
                        return 65535;
                }
            }
        }

        public bool IsParamEnabled
        {
            get
            {
                if (_methodIndex < 0 || _methodIndex >= Enum.GetValues<EvolutionMethod>().Length)
                    return false;
                var method = (EvolutionMethod)_methodIndex;
                if (!EvolutionFile.evoDescriptions.TryGetValue(method, out var meaning))
                    return false;
                return meaning != EvolutionParamMeaning.Ignored;
            }
        }

        // Fired whenever any field changes so parent VM can mark dirty
        public Action Changed;
    }

    public class EvolutionsEditorViewModel : INotifyPropertyChanged, IEditorWithUnsavedChanges
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(f, v)) return false;
            f = v; OnPropertyChanged(n); return true;
        }
        // ─── Lists ────────────────────────────────────────────────────────────────
        public ObservableCollection<string> MethodNames { get; } = new();
        public ObservableCollection<string> PokemonNames { get; } = new();

        // 7 evolution slots
        public ObservableCollection<EvolutionRowViewModel> EvoRows { get; } = new();

        // ─── Dirty tracking ───────────────────────────────────────────────────────
        private bool _dirty;
        public bool HasUnsavedChanges => _dirty;
        public string UnsavedChangesDescription => $"Evolutions (Mon {_currentId})";
        public void SaveChanges() => Save();
        public void DiscardChanges() { _dirty = false; OnPropertyChanged(nameof(HasUnsavedChanges)); }

        private int _currentId = -1;
        private EvolutionFile _current;
        private bool _loading;

        // ─── Design-time constructor ──────────────────────────────────────────────
        public EvolutionsEditorViewModel()
        {
            if (!Design.IsDesignMode) return;

            foreach (var n in Enum.GetNames<EvolutionMethod>()) MethodNames.Add(n);
            for (int i = 0; i < 10; i++) PokemonNames.Add($"Pokémon {i}");

            for (int i = 0; i < EvolutionFile.numEvolutions; i++)
            {
                EvoRows.Add(new EvolutionRowViewModel { MethodIndex = 0, TargetIndex = 0, Param = 0 });
            }
        }

        // ─── Runtime constructor ──────────────────────────────────────────────────
        public EvolutionsEditorViewModel(string[] pokemonNames)
        {
            string[] itemNames = RomInfo.GetItemNames();
            string[] moveNames = RomInfo.GetAttackNames();

            foreach (var n in Enum.GetNames<EvolutionMethod>()) MethodNames.Add(n);
            foreach (var n in pokemonNames) PokemonNames.Add(n);

            for (int i = 0; i < EvolutionFile.numEvolutions; i++)
            {
                var row = new EvolutionRowViewModel
                {
                    ItemNames    = itemNames,
                    MoveNames    = moveNames,
                    PokemonNames = pokemonNames,
                    MethodIndex  = 0,
                    TargetIndex  = 0,
                    Param        = 0
                };
                row.Changed = () => { if (!_loading) SetDirty(); };
                EvoRows.Add(row);
            }
        }

        // ─── Load ─────────────────────────────────────────────────────────────────
        public void LoadMon(int id)
        {
            _loading = true;
            try
            {
                _currentId = id;
                _current = id > 0 ? new EvolutionFile(id) : new EvolutionFile();
                if (_current.data == null)
                    _current.data = new EvolutionData[EvolutionFile.numEvolutions];

                for (int i = 0; i < EvolutionFile.numEvolutions; i++)
                {
                    var row = EvoRows[i];
                    var d = i < _current.data.Length ? _current.data[i] : default;
                    row.MethodIndex = (int)d.method;
                    row.Param       = d.param;
                    row.TargetIndex = d.target >= 0 ? d.target : 0;
                }

                _dirty = false;
                OnPropertyChanged(nameof(HasUnsavedChanges));
            }
            finally { _loading = false; }
        }

        // ─── Save ─────────────────────────────────────────────────────────────────
        public void Save()
        {
            if (_currentId < 0 || _current == null) return;

            var newFile = new EvolutionFile();
            var data = new System.Collections.Generic.List<EvolutionData>();

            foreach (var row in EvoRows)
            {
                var method = (EvolutionMethod)row.MethodIndex;
                var ed = new EvolutionData
                {
                    method = method,
                    param  = (short)row.Param,
                    target = (short)row.TargetIndex
                };
                if (ed.isValid()) data.Add(ed);
            }

            newFile.data = data.ToArray();
            newFile.SaveToFileDefaultDir(_currentId, showSuccessMessage: false);
            _current = newFile;
            _dirty = false;
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private void SetDirty()
        {
            if (!_dirty) { _dirty = true; OnPropertyChanged(nameof(HasUnsavedChanges)); }
        }
    }
}

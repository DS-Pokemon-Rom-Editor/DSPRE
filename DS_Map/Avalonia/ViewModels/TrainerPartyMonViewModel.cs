using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using global::Avalonia.Media.Imaging;
using DSPRE.Avalonia;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// One slot of a trainer's party (up to 6). Holds species/form/level/moves/item/
    /// gender/ability/IV/ball-seal. The ability list is rebuilt per selected species
    /// (Default + the species' two abilities, mirroring the WinForms editor). Raises
    /// <see cref="Changed"/> on any edit so the parent can mark itself dirty.
    /// </summary>
    public class TrainerPartyMonViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler Changed;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        public int Slot { get; }
        private bool _suppress;

        // Shared (owned by the parent VM)
        public ObservableCollection<string> PokemonNames { get; }
        public ObservableCollection<string> MoveNames { get; }
        public ObservableCollection<string> ItemNames { get; }
        public ObservableCollection<string> GenderItems { get; } = new ObservableCollection<string> { "Default Gender", "Male", "Female" };
        public ObservableCollection<string> AbilityItems { get; } = new ObservableCollection<string>();

        private readonly string[] _abilityNames;
        private readonly (int abi1, int abi2)[] _abilities;
        private readonly bool _abilityEditable;

        public TrainerPartyMonViewModel(int slot,
            ObservableCollection<string> pokeNames, ObservableCollection<string> moveNames, ObservableCollection<string> itemNames,
            string[] abilityNames, (int abi1, int abi2)[] abilities, bool abilityEditable, bool genderVisible, bool formVisible, bool ballEnabled)
        {
            Slot = slot;
            PokemonNames = pokeNames; MoveNames = moveNames; ItemNames = itemNames;
            _abilityNames = abilityNames; _abilities = abilities; _abilityEditable = abilityEditable;
            GenderVisible = genderVisible; FormVisible = formVisible; BallEnabled = ballEnabled;
            AbilityEnabled = abilityEditable;
        }

        // ── Visibility / enablement ─────────────────────────────────────────────────
        private bool _isVisible = true;
        public bool IsVisible { get => _isVisible; set => Set(ref _isVisible, value); }
        public bool GenderVisible { get; }
        public bool FormVisible { get; }
        private bool _ballEnabled;
        public bool BallEnabled { get => _ballEnabled; set => Set(ref _ballEnabled, value); }
        private bool _movesEnabled;
        public bool MovesEnabled { get => _movesEnabled; set => Set(ref _movesEnabled, value); }
        private bool _itemEnabled;
        public bool ItemEnabled { get => _itemEnabled; set => Set(ref _itemEnabled, value); }
        private bool _abilityEnabled;
        public bool AbilityEnabled { get => _abilityEnabled; set => Set(ref _abilityEnabled, value); }

        // ── Fields ──────────────────────────────────────────────────────────────────
        private int _speciesIndex = -1;
        public int SpeciesIndex
        {
            get => _speciesIndex;
            set { if (Set(ref _speciesIndex, value)) { RebuildAbilities(); UpdateIcon(); Touch(); } }
        }

        private decimal _formId;
        public decimal FormId { get => _formId; set { if (Set(ref _formId, value)) Touch(); } }

        private decimal _level = 1;
        public decimal Level { get => _level; set { if (Set(ref _level, value)) Touch(); } }

        private int _move1 = -1; public int Move1 { get => _move1; set { if (Set(ref _move1, value)) Touch(); } }
        private int _move2 = -1; public int Move2 { get => _move2; set { if (Set(ref _move2, value)) Touch(); } }
        private int _move3 = -1; public int Move3 { get => _move3; set { if (Set(ref _move3, value)) Touch(); } }
        private int _move4 = -1; public int Move4 { get => _move4; set { if (Set(ref _move4, value)) Touch(); } }

        private int _itemIndex = -1; public int ItemIndex { get => _itemIndex; set { if (Set(ref _itemIndex, value)) Touch(); } }
        private int _genderIndex; public int GenderIndex { get => _genderIndex; set { if (Set(ref _genderIndex, value)) Touch(); } }
        private int _abilityIndex; public int AbilityIndex { get => _abilityIndex; set { if (Set(ref _abilityIndex, value)) Touch(); } }
        private decimal _difficulty; public decimal Difficulty { get => _difficulty; set { if (Set(ref _difficulty, value)) Touch(); } }
        private decimal _ballSeals; public decimal BallSeals { get => _ballSeals; set { if (Set(ref _ballSeals, value)) Touch(); } }

        private Bitmap _pokemonIcon;
        public Bitmap PokemonIcon { get => _pokemonIcon; set => Set(ref _pokemonIcon, value); }

        private void Touch() { if (!_suppress) Changed?.Invoke(this, EventArgs.Empty); }

        // ── Load from model (suppress change events) ────────────────────────────────
        public void Load(int species, int form, int level, int[] moves, int item, int genderIndex, int abilityIndex, int difficulty, int ballSeals)
        {
            _suppress = true;
            SpeciesIndex = species;
            FormId = form;
            Level = level;
            if (moves != null && moves.Length >= 4) { Move1 = moves[0]; Move2 = moves[1]; Move3 = moves[2]; Move4 = moves[3]; }
            else { Move1 = Move2 = Move3 = Move4 = 0; }
            ItemIndex = item;
            GenderIndex = genderIndex;
            AbilityIndex = abilityIndex;
            Difficulty = difficulty;
            BallSeals = ballSeals;
            _suppress = false;
        }

        private void RebuildAbilities()
        {
            AbilityItems.Clear();
            if (_speciesIndex < 0 || _speciesIndex >= _abilities.Length) return;
            var ab = _abilities[_speciesIndex];
            string a1 = ab.abi1 >= 0 && ab.abi1 < _abilityNames.Length ? _abilityNames[ab.abi1] : "?";
            string a2 = ab.abi2 >= 0 && ab.abi2 < _abilityNames.Length ? _abilityNames[ab.abi2] : "?";

            if (!_abilityEditable)
            {
                // DPPt: ability not editable — show ability 1 three times (matches WinForms padding).
                AbilityItems.Add(a1); AbilityItems.Add(a1); AbilityItems.Add(a1);
            }
            else
            {
                AbilityItems.Add("Default Ability");
                AbilityItems.Add(a1);
                AbilityItems.Add(a2);
            }
        }

        private void UpdateIcon()
        {
            try
            {
                if (_speciesIndex <= 0) { PokemonIcon = null; return; }
                using var gdi = DSUtils.GetPokePic(_speciesIndex, 56, 56);
                PokemonIcon = ImageConverter.ToAvaloniaBitmap(gdi);
            }
            catch { PokemonIcon = null; }
        }
    }
}

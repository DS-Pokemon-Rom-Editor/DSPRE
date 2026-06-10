using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DSPRE;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// One party slot in the DV Calculator. The DV (Difficulty Value) drives the IVs
    /// and, together with gender/ability flags and trainer id/class, the Pokémon's
    /// nature (computed by the existing static <c>DVCalculator</c> engine).
    /// </summary>
    public class DVCalcSlotViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler Changed;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        public int Index { get; }
        public int PokeId { get; }
        public int Level { get; }
        public bool Active { get; }
        public bool FlagsEditable { get; }
        public string PokeLabel { get; }

        public ObservableCollection<string> AbilityOptions { get; } = new ObservableCollection<string> { "No Flag", "Force Ability 1", "Force Ability 2" };
        public ObservableCollection<string> GenderOptions { get; } = new ObservableCollection<string> { "No Flag", "Force Male", "Force Female" };

        private int _abilityIndex; public int AbilityIndex { get => _abilityIndex; set { if (Set(ref _abilityIndex, value)) Changed?.Invoke(this, EventArgs.Empty); } }
        private int _genderIndex; public int GenderIndex { get => _genderIndex; set { if (Set(ref _genderIndex, value)) Changed?.Invoke(this, EventArgs.Empty); } }
        private decimal _dv; public decimal DV { get => _dv; set { if (Set(ref _dv, value)) Changed?.Invoke(this, EventArgs.Empty); } }

        private string _iv = ""; public string IV { get => _iv; set => Set(ref _iv, value); }
        private string _nature = ""; public string Nature { get => _nature; set => Set(ref _nature, value); }

        public DVCalcSlotViewModel(int index, int pokeId, int level, int genderIndex, int abilityIndex, int dv, bool active, bool flagsEditable, string pokeLabel)
        {
            Index = index; PokeId = pokeId; Level = level; Active = active; FlagsEditable = flagsEditable; PokeLabel = pokeLabel;
            _genderIndex = genderIndex; _abilityIndex = abilityIndex; _dv = dv;
        }
    }

    /// <summary>
    /// Avalonia port of the WinForms <c>DVCalc</c> form. Wraps the existing static
    /// <c>DVCalculator</c> engine to show each party Pokémon's resulting nature/IVs for
    /// a chosen DV, and lets the user browse the DV→nature table. On confirm it reports
    /// the updated (DV, gender, ability) per slot back to the Trainer Editor.
    /// </summary>
    public class DVCalcViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private readonly ushort _trainerId;
        private readonly byte _trainerClass;

        public string TrainerLabel { get; }
        public string ClassLabel { get; }
        public ObservableCollection<DVCalcSlotViewModel> Slots { get; } = new ObservableCollection<DVCalcSlotViewModel>();

        public bool Confirmed { get; private set; }

        private bool _maleTrainer = true;
        public bool MaleTrainer { get => _maleTrainer; set { if (Set(ref _maleTrainer, value)) { OnPropertyChanged(nameof(FemaleTrainer)); UpdateNatures(); } } }
        public bool FemaleTrainer { get => !_maleTrainer; set { MaleTrainer = !value; } }

        public DVCalcViewModel() { }

        /// <param name="party">Active party slots: (pokeId, level, genderIndex, abilityIndex, dv).</param>
        public DVCalcViewModel(ushort trainerId, byte trainerClass, IReadOnlyList<(int pokeId, int level, int genderIndex, int abilityIndex, int dv)> party)
        {
            _trainerId = trainerId;
            _trainerClass = trainerClass;
            bool flagsEditable = gameFamily == GameFamilies.HGSS || AIBackportEnabled;

            try { _maleTrainer = DVCalculator.TrainerClassGender.GetTrainerClassGender(trainerClass); }
            catch { _maleTrainer = true; }

            try { TrainerLabel = $"Trainer: [{trainerId}] {GetSimpleTrainerNames()[trainerId]}"; } catch { TrainerLabel = $"Trainer: [{trainerId}]"; }
            try { ClassLabel = $"Class: [{trainerClass}] {GetTrainerClassNames()[trainerClass]}"; } catch { ClassLabel = $"Class: [{trainerClass}]"; }

            string[] pokeNames = GetPokemonNames();
            for (int i = 0; i < party.Count; i++)
            {
                var p = party[i];
                string label = (p.pokeId >= 0 && p.pokeId < pokeNames.Length ? pokeNames[p.pokeId] : "?") + " Lv. " + p.level;
                var slot = new DVCalcSlotViewModel(i, p.pokeId, p.level, p.genderIndex, p.abilityIndex, p.dv, true, flagsEditable, label);
                slot.Changed += (s, e) => UpdateNatures();
                Slots.Add(slot);
            }
            UpdateNatures();
        }

        public void UpdateNatures()
        {
            DVCalculator.ResetGenderMod(_maleTrainer);
            foreach (var slot in Slots)
            {
                try
                {
                    byte ratio = new PokemonPersonalData(slot.PokeId).genderVec;
                    uint pid = DVCalculator.generatePID(_trainerId, _trainerClass, (uint)slot.PokeId, (byte)slot.Level,
                        ratio, slot.GenderIndex, slot.AbilityIndex, (byte)slot.DV);
                    slot.Nature = DVCalculator.Natures[DVCalculator.getNatureFromPID(pid)];
                }
                catch { slot.Nature = "?"; }
                slot.IV = ((int)(slot.DV * 31 / 255)).ToString();
            }
        }

        /// <summary>Builds the DV→nature/IV table for the given slot (optionally only the highest DV per nature).</summary>
        public List<DVIVNatureTriplet> GenerateTriplets(int index, bool highestOnly)
        {
            DVCalculator.ResetGenderMod(_maleTrainer);
            if (gameFamily == GameFamilies.HGSS || AIBackportEnabled)
            {
                for (int i = 0; i < index && i < Slots.Count; i++)
                {
                    byte r = new PokemonPersonalData(Slots[i].PokeId).genderVec;
                    DVCalculator.UpdateGenderMod((ushort)Slots[i].PokeId, r, Slots[i].GenderIndex, Slots[i].AbilityIndex);
                }
            }

            var slot = Slots[index];
            var triplets = DVCalculator.getAllNatures(_trainerId, _trainerClass, (uint)slot.PokeId, (byte)slot.Level,
                new PokemonPersonalData(slot.PokeId).genderVec, slot.GenderIndex, slot.AbilityIndex);

            if (highestOnly) DVCalculator.filterHighestDV(ref triplets);
            return triplets;
        }

        public void Confirm() => Confirmed = true;
    }
}

using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DSPRE.Avalonia.ViewModels.Trainers
{
    /// <summary>Backing model for the "Add Trainer Class…" dialog. Pure input collection,
    /// <see cref="TrainerClassesViewModel.AddTrainerClass"/> does the actual write once the dialog
    /// closes with <see cref="Confirmed"/>. Platinum-only (see IsExpansionSupported gating on the
    /// button that opens this), so there's no HGSS "night music" variant to collect here.</summary>
    public class AddTrainerClassViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private string _className = "";
        public string ClassName { get => _className; set => Set(ref _className, value); }

        private string _description = "";
        public string Description { get => _description; set => Set(ref _description, value); }

        private int _genderIndex;
        public int GenderIndex { get => _genderIndex; set => Set(ref _genderIndex, value); }

        private int _prizeMultiplier = 1;
        public int PrizeMultiplier { get => _prizeMultiplier; set => Set(ref _prizeMultiplier, value); }

        private bool _addMusic;
        public bool AddMusic { get => _addMusic; set => Set(ref _addMusic, value); }

        private decimal _musicMain;
        public decimal MusicMain { get => _musicMain; set => Set(ref _musicMain, value); }

        private string _statusText = "";
        public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

        public bool Confirmed { get; private set; }
        public void Confirm() => Confirmed = true;
    }
}

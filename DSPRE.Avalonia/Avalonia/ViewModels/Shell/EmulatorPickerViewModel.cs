using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DSPRE.Avalonia.ViewModels.Shell
{
    /// <summary>Picks the emulator Build and Run starts, and whether to stop asking.</summary>
    public class EmulatorPickerViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public string[] KindNames { get; } = Emulators.All.Select(Emulators.DisplayName).ToArray();

        private int _kindIndex;
        public int KindIndex
        {
            get => _kindIndex;
            set
            {
                if (value < 0 || value == _kindIndex) return;
                _kindIndex = value;
                OnPropertyChanged();
                string known = Emulators.PathFor(Kind);
                if (!string.IsNullOrEmpty(known)) Path = known;
            }
        }

        public EmulatorKind Kind => Emulators.All[_kindIndex];

        private string _path = "";
        public string Path
        {
            get => _path;
            set
            {
                if (_path == value) return;
                _path = value ?? "";
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanRun));
                StatusText = "";
                if (Emulators.Guess(_path) is EmulatorKind guessed && guessed != Kind)
                {
                    _kindIndex = System.Array.IndexOf(Emulators.All, guessed);
                    OnPropertyChanged(nameof(KindIndex));
                }
            }
        }

        private bool _makePreferred = true;
        public bool MakePreferred { get => _makePreferred; set { _makePreferred = value; OnPropertyChanged(); } }

        private string _statusText = "";
        public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }

        public bool CanRun => !string.IsNullOrWhiteSpace(_path);

        public bool Confirmed { get; private set; }

        public EmulatorPickerViewModel()
        {
            var known = Emulators.All.FirstOrDefault(k => Emulators.Exists(Emulators.PathFor(k)));
            _kindIndex = System.Array.IndexOf(Emulators.All, known);
            _path = Emulators.PathFor(known) ?? "";
        }

        public bool TryConfirm()
        {
            if (!Emulators.Exists(_path))
            {
                StatusText = "That file doesn't exist.";
                return false;
            }
            Emulators.Remember(Kind, _path, _makePreferred);
            Confirmed = true;
            return true;
        }
    }
}

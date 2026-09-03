using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>
    /// Lets the user say which give-a-Pokemon command hands the starter over, for a romhack that added
    /// its own. The list is what the sources found; the file and script boxes are for a starter that
    /// has been moved somewhere the pair rule cannot recognise.
    /// </summary>
    public class StarterCommandDialogViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T field, T value, [CallerMemberName] string n = null)
        {
            if (Equals(field, value)) return false;
            field = value; OnPropertyChanged(n); return true;
        }

        public sealed class Row
        {
            public StarterRotomSource.Match Command;
            public string Text => Command.Where + ": " + Command.Summary;
        }

        public ObservableCollection<Row> Candidates { get; } = new();

        private StarterRotomSource.Check _verified;

        private int _selectedIndex = -1;
        public int SelectedIndex
        {
            get => _selectedIndex;
            // Picking from the list is a fresh answer, so whatever Verify last found stops counting.
            set { if (Set(ref _selectedIndex, value)) { _verified = null; VerdictText = null; } }
        }

        private int _fileId;
        public int FileId { get => _fileId; set => Set(ref _fileId, value); }

        private string _container = "";
        public string ContainerName { get => _container; set => Set(ref _container, value); }

        private string _verdict;
        public string VerdictText { get => _verdict; private set { Set(ref _verdict, value); OnPropertyChanged(nameof(HasVerdict)); } }
        public bool HasVerdict => !string.IsNullOrEmpty(_verdict);

        /// <summary>Set when the user went ahead with a script that picks its own species.</summary>
        public bool SpeciesIsOutOfOurHands { get; private set; }

        /// <summary>What the editor should use, or null when the dialog was cancelled.</summary>
        public StarterRotomSource.Match Chosen { get; private set; }

        public StarterCommandDialogViewModel() { }

        public StarterCommandDialogViewModel(StarterRotomSource.Match current)
        {
            foreach (var m in StarterRotomSource.FindAll()) Candidates.Add(new Row { Command = m });
            if (current != null)
            {
                SelectedIndex = Candidates.ToList().FindIndex(r => r.Command.Key == current.Key);
                FileId = current.FileId;
                ContainerName = current.Container ?? "";
            }
        }

        /// <summary>Checks the file and script the user typed, and says what is there.</summary>
        public void Verify()
        {
            var check = StarterRotomSource.Verify(FileId, ContainerName);
            VerdictText = check.Message;
            _verified = check;
        }

        /// <summary>
        /// Takes whatever the user settled on: the checked location when there is one, otherwise the
        /// row they picked out of the list.
        /// </summary>
        public void Accept()
        {
            if (_verified != null && _verified.Found != null)
            {
                Chosen = _verified.Found;
                SpeciesIsOutOfOurHands = _verified.Verdict == StarterRotomSource.Verdict.SpeciesIsItsOwn;
                return;
            }
            if (SelectedIndex >= 0 && SelectedIndex < Candidates.Count)
                Chosen = Candidates[SelectedIndex].Command;
        }
    }
}

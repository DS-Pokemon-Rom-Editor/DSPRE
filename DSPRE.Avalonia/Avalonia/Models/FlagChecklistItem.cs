using System.ComponentModel;

namespace DSPRE.Avalonia.Models
{
    /// <summary>
    /// A flag checklist row in the Trainer flag bulk editor; clicks go through the view, since a click sets the whole
    /// selection to the opposite of this row's tick.
    /// </summary>
    public sealed class FlagChecklistItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public int Index { get; init; }
        public string Name { get; init; }
        /// <summary>Shown above this row when it starts a group.</summary>
        public string Heading { get; init; }
        /// <summary>The move a machine row teaches, or 0.</summary>
        public int Move { get; init; }

        private bool? _isChecked;
        public bool? IsChecked
        {
            get => _isChecked;
            private set
            {
                if (_isChecked == value) return;
                _isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            }
        }

        public void SetChecked(bool? value) => IsChecked = value;
    }
}

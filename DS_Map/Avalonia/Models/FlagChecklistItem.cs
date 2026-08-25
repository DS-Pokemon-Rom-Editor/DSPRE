using System.ComponentModel;

namespace DSPRE.Avalonia.Models
{
    /// <summary>Right-hand flag checklist row for the Trainer Flag Bulk Editor (By Trainer mode).
    /// Purely display state driven by the ViewModel: <see cref="IsChecked"/> is bound one-way and
    /// updated via <see cref="SetChecked"/>; the actual toggle-on-click logic lives in the ViewModel,
    /// wired through the view's Click handler rather than a two-way binding, since the desired click
    /// semantics ("always sets the whole selection to the opposite of Checked") don't match Avalonia's
    /// built-in three-state cycle.</summary>
    public sealed class FlagChecklistItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public int Index { get; init; }
        public string Name { get; init; }

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

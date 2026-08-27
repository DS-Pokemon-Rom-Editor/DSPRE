using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace DSPRE.Avalonia.Models
{
    /// <summary>
    /// Tree nodes for the TM/HM Bulk Editor (evolution-family parent, per-species leaves). Same
    /// "suppress while cascading" discipline as <see cref="TrainerFlagTreeNode"/>, kept as its own
    /// type rather than reused because species IDs need <c>int</c>, not the trainer tree's <c>byte</c>
    /// class ID (species count regularly exceeds 255 once alt forms are counted).
    /// </summary>
    public abstract class SpeciesFamilyTreeNode : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed class SpeciesGroupNode : SpeciesFamilyTreeNode
    {
        public int FamilyRootId { get; init; }
        public ObservableCollection<SpeciesLeafNode> Children { get; } = new();
        public Action<SpeciesGroupNode> OnCheckedChanged { get; set; }

        private string _displayName;
        public string DisplayName { get => _displayName; set { if (_displayName != value) { _displayName = value; Raise(nameof(DisplayName)); } } }

        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value) return;
                _isChecked = value;
                Raise(nameof(IsChecked));
                OnCheckedChanged?.Invoke(this);
            }
        }

        public void SetCheckedSilent(bool value)
        {
            if (_isChecked == value) return;
            _isChecked = value;
            Raise(nameof(IsChecked));
        }
    }

    public sealed class SpeciesLeafNode : SpeciesFamilyTreeNode
    {
        public int SpeciesId { get; init; }
        public string DisplayName { get; init; }
        public Action<SpeciesLeafNode> OnCheckedChanged { get; set; }

        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value) return;
                _isChecked = value;
                Raise(nameof(IsChecked));
                OnCheckedChanged?.Invoke(this);
            }
        }

        public void SetCheckedSilent(bool value)
        {
            if (_isChecked == value) return;
            _isChecked = value;
            Raise(nameof(IsChecked));
        }
    }
}

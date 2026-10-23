using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace DSPRE.Avalonia.Models
{
    /// <summary>TM/HM bulk editor tree nodes, separate from <see cref="TrainerFlagTreeNode"/> because species numbers pass 255.</summary>
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

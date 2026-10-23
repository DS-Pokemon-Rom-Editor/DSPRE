using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace DSPRE.Avalonia.Models
{
    /// <summary>Trainer flag bulk editor tree nodes; the view model ignores OnCheckedChanged while it makes a change itself.</summary>
    public abstract class TrainerFlagTreeNode : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed class TrainerFlagGroupNode : TrainerFlagTreeNode
    {
        public byte ClassId { get; init; }
        public ObservableCollection<TrainerFlagLeafNode> Children { get; } = new();
        public Action<TrainerFlagGroupNode> OnCheckedChanged { get; set; }

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

        /// <summary>Sets the backing field without firing the callback (for programmatic summary updates).</summary>
        public void SetCheckedSilent(bool value)
        {
            if (_isChecked == value) return;
            _isChecked = value;
            Raise(nameof(IsChecked));
        }
    }

    public sealed class TrainerFlagLeafNode : TrainerFlagTreeNode
    {
        public int TrainerId { get; init; }
        public string DisplayName { get; init; }
        public Action<TrainerFlagLeafNode> OnCheckedChanged { get; set; }

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

        /// <summary>Sets the backing field without firing the callback (for group-driven cascades).</summary>
        public void SetCheckedSilent(bool value)
        {
            if (_isChecked == value) return;
            _isChecked = value;
            Raise(nameof(IsChecked));
        }
    }
}

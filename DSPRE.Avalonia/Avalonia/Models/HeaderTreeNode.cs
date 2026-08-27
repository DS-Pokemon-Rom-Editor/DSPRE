using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DSPRE.Avalonia.Models
{
    /// <summary>
    /// Node types for the Header Editor's location-grouped sidebar tree. <see cref="IsExpanded"/>
    /// notifies so the ViewModel can drive folder expansion and visibility (Expand/Collapse All,
    /// search, initial selection); display data is set when the tree is (re)built.
    /// </summary>
    public abstract class HeaderTreeNode : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public string DisplayName { get; init; }

        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible == value) return;
                _isVisible = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsVisible)));
            }
        }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value) return;
                _isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }
    }

    public sealed class HeaderTreeFolder : HeaderTreeNode
    {
        public ObservableCollection<HeaderTreeNode> Children { get; } = new ObservableCollection<HeaderTreeNode>();
        public int Count => Children.Count;
    }

    public sealed class HeaderTreeLeaf : HeaderTreeNode
    {
        public ushort HeaderId { get; init; }
    }
}

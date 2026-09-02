using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DSPRE.Avalonia.Models;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>Avalonia port of the WinForms <c>CopyMachinesForm</c>: pick a source Pokémon and a set
    /// of target Pokémon (individuals or whole evolution families) to copy TM/HM compatibility to.</summary>
    public class CopyMachinesDialogViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (EqualityComparer<T>.Default.Equals(f, v)) return false; f = v; OnPropertyChanged(n); return true; }

        private readonly HashSet<int> _selectedTargetIds = new();
        private bool _suppress;

        public ObservableCollection<string> SpeciesNames { get; } = new();
        public ObservableCollection<SpeciesFamilyTreeNode> TargetTree { get; } = new();

        private int _sourceIndex;
        public int SourceIndex { get => _sourceIndex; set => Set(ref _sourceIndex, value); }

        public bool Confirmed { get; private set; }
        public IReadOnlyCollection<int> SelectedTargetIds => _selectedTargetIds;

        public CopyMachinesDialogViewModel(string[] pokemonNames, IReadOnlyList<List<int>> families, int preselectedSourceId, System.Func<int, string> labelFor)
        {
            foreach (var name in pokemonNames) SpeciesNames.Add(name);
            SourceIndex = preselectedSourceId >= 0 && preselectedSourceId < SpeciesNames.Count ? preselectedSourceId : 0;

            foreach (var fam in families)
            {
                if (fam.Count == 1)
                {
                    TargetTree.Add(new SpeciesLeafNode { SpeciesId = fam[0], DisplayName = labelFor(fam[0]), OnCheckedChanged = OnLeafChecked });
                }
                else
                {
                    var group = new SpeciesGroupNode { FamilyRootId = fam[0], DisplayName = $"{labelFor(fam[0])} family", OnCheckedChanged = OnGroupChecked };
                    foreach (var id in fam)
                        group.Children.Add(new SpeciesLeafNode { SpeciesId = id, DisplayName = labelFor(id), OnCheckedChanged = OnLeafChecked });
                    TargetTree.Add(group);
                }
            }
        }

        private void OnLeafChecked(SpeciesLeafNode leaf)
        {
            if (leaf.IsChecked) _selectedTargetIds.Add(leaf.SpeciesId);
            else _selectedTargetIds.Remove(leaf.SpeciesId);

            if (_suppress) return;
            foreach (var node in TargetTree)
                if (node is SpeciesGroupNode group && group.Children.Contains(leaf))
                {
                    int total = group.Children.Count;
                    int checkedCount = 0;
                    foreach (var c in group.Children) if (c.IsChecked) checkedCount++;
                    group.SetCheckedSilent(checkedCount == total);
                }
        }

        private void OnGroupChecked(SpeciesGroupNode group)
        {
            if (_suppress) return;
            _suppress = true;
            foreach (var child in group.Children) child.IsChecked = group.IsChecked;
            _suppress = false;
        }

        public void Accept() => Confirmed = true;
    }
}

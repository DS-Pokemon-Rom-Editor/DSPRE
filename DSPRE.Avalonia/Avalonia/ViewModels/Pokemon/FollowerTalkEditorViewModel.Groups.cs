using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;
using Slot = DSPRE.ROMFiles.FollowerTalk.Slot;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    public partial class FollowerTalkEditorViewModel
    {
        public ObservableCollection<SpeciesRow> SpeciesRows { get; } = new ObservableCollection<SpeciesRow>();

        public sealed class SpeciesRow : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private readonly FollowerTalkEditorViewModel _o;
            public int Species { get; }
            public SpeciesRow(FollowerTalkEditorViewModel o, int species) { _o = o; Species = species; }
            public string Name => $"{Species:D3} {_o.SpeciesName(Species)}";
            public override string ToString() => Name;
            public decimal Group { get => _o.GroupOf(Species); set => _o.SetGroup(Species, (int)Math.Clamp(value, 0, 255)); }
            internal void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Group)));
        }
    }
}

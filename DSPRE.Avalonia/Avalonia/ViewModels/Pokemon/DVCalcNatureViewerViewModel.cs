using System.Collections.Generic;
using System.Collections.ObjectModel;
using DSPRE;

namespace DSPRE.Avalonia.ViewModels.Pokemon
{
    /// <summary>Backing VM for the DV→IV→Nature table dialog. Double-clicking a row reports its DV.</summary>
    public class DVCalcNatureViewerViewModel
    {
        public ObservableCollection<DVIVNatureTriplet> Rows { get; } = new ObservableCollection<DVIVNatureTriplet>();
        public DVIVNatureTriplet SelectedRow { get; set; }
        public int SelectedDV { get; private set; } = -1;

        public DVCalcNatureViewerViewModel() { }

        public DVCalcNatureViewerViewModel(IEnumerable<DVIVNatureTriplet> rows)
        {
            foreach (var r in rows) Rows.Add(r);
        }

        public bool ConfirmSelection()
        {
            if (SelectedRow == null) return false;
            SelectedDV = SelectedRow.DV;
            return true;
        }
    }
}

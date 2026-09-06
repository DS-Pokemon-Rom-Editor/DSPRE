using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public sealed class OverworldProfilePickerViewModel
    {
        public OverworldProfilePickerViewModel(IEnumerable<OverworldGraphicsProfileOption> profiles)
        {
            Profiles = new ObservableCollection<OverworldGraphicsProfileOption>(profiles);
            SelectedProfile = Profiles.Count > 0 ? Profiles[0] : null;
        }

        public ObservableCollection<OverworldGraphicsProfileOption> Profiles { get; }
        public OverworldGraphicsProfileOption SelectedProfile { get; set; }
    }
}

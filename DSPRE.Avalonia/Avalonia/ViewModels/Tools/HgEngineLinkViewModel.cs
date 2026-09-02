using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using DSPRE.HgEngine;

namespace DSPRE.Avalonia.ViewModels.Tools
{
    /// <summary>Backs the "Link hg-engine checkout…" dialog: link/unlink a WSL hg-engine checkout and
    /// toggle whether it's active, so the Pokémon/Trainer/Item/Move/Wild-Encounter editors read and
    /// write its data/*.c source instead of the packed ROM.</summary>
    public class HgEngineLinkViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public bool IsLinked => HgEngineProject.IsLinked;

        public string StatusText => IsLinked
            ? $"Linked to {HgEngineProject.WslDistro}:{HgEngineProject.RepoPathPosix}"
            : "No hg-engine checkout linked for this project.";

        /// <summary>hg-engine's own `make` hard-requires a rom.nds at the checkout root; surfaced here so
        /// a missing one is caught while linking, not mid-Compile-ROM.</summary>
        public bool ShowRomNdsWarning => IsLinked && !HgEngineProject.HasRomNds;

        public bool Enabled
        {
            get => HgEngineProject.Enabled;
            set
            {
                if (!IsLinked || value == HgEngineProject.Enabled) return;
                HgEngineProject.SetEnabled(value);
                RaiseAll();
                AppEvents.RaiseHgEngineLinkChanged();
            }
        }

        public async Task BrowseAsync(Window owner)
        {
            string path = await DialogHelper.OpenFolder(owner,
                "Select your hg-engine checkout (a WSL folder, e.g. \\\\wsl.localhost\\Ubuntu\\home\\you\\hg-engine)");
            if (string.IsNullOrEmpty(path)) return;

            if (!HgEngineProject.TryLink(path, out string error))
            {
                await DialogHelper.ShowError(error, "Couldn't link hg-engine checkout", owner);
                return;
            }
            RaiseAll();
            AppEvents.RaiseHgEngineLinkChanged();
        }

        public void Unlink()
        {
            if (!IsLinked) return;
            HgEngineProject.Unlink();
            RaiseAll();
            AppEvents.RaiseHgEngineLinkChanged();
        }

        private void RaiseAll()
        {
            OnPropertyChanged(nameof(IsLinked));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(Enabled));
            OnPropertyChanged(nameof(ShowRomNdsWarning));
        }
    }
}

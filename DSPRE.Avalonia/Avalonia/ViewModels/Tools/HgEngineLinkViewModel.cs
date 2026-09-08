using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using DSPRE.HgEngine;

namespace DSPRE.Avalonia.ViewModels.Tools
{
    /// <summary>Backs the "Link hg-engine checkout…" dialog: link/unlink an hg-engine checkout and
    /// toggle whether it's active, so the Pokémon/Trainer/Item/Move/Wild-Encounter editors read and
    /// write its data/*.c source instead of the packed ROM.</summary>
    public class HgEngineLinkViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public bool IsLinked => HgEngineProject.IsLinked;

        public string StatusText => IsLinked
            ? $"Linked to {HgEngineProject.RepoRootWindows}  (builds with {ShellLabel})"
            : "No hg-engine checkout linked for this project.";

        private static string ShellLabel => HgEngineProject.Shell == HgEngineShell.Msys2
            ? "MSYS2"
            : string.IsNullOrWhiteSpace(HgEngineProject.WslDistro) ? "WSL" : "WSL: " + HgEngineProject.WslDistro;

        /// <summary>Shown when the checkout is on a Windows drive but builds through WSL, which works
        /// but reads every file across the /mnt boundary.</summary>
        public bool ShowMountSpeedNote => IsLinked && HgEngineProject.BuildCrossesMountBoundary;

        public string MountSpeedNote =>
            "This checkout is on a Windows drive but builds through WSL, so make runs over /mnt and "
            + "will be noticeably slower than a checkout inside WSL. Building it with MSYS2 instead, "
            + "or moving the checkout into WSL, avoids that.";

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

        /// <summary>
        /// A checkout inside WSL can only build one way, so it is not worth a question. One on a
        /// Windows drive genuinely can go either way: hg-engine's README documents an MSYS2 setup, and
        /// WSL can still reach the folder through /mnt. Only the user knows which toolchain they set up.
        /// </summary>
        public static async Task<HgEngineShell?> AskShellAsync(string path)
        {
            if (HgEngineProject.IsWslPath(path)) return HgEngineShell.Wsl;

            var choice = await DialogHelper.AskThreeWay(
                path + "\n\nThis checkout is on a Windows drive. Which toolchain builds it?\n\n"
                + "MSYS2 is the setup hg-engine's README describes for Windows, and builds natively.\n\n"
                + "WSL also works, reaching the folder through /mnt, but make will be noticeably slower.",
                "How is this checkout built?", "MSYS2", "WSL");

            if (choice == DialogHelper.MsgResult.Yes) return HgEngineShell.Msys2;
            if (choice == DialogHelper.MsgResult.No) return HgEngineShell.Wsl;
            return null;
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
            OnPropertyChanged(nameof(ShowMountSpeedNote));
            OnPropertyChanged(nameof(MountSpeedNote));
        }
    }
}

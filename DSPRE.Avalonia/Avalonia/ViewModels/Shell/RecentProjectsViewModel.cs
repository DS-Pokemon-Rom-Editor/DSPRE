using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DSPRE.Avalonia.ViewModels.Shell
{
    /// <summary>The recent projects the start panel lists, split into project folders and ROM files.</summary>
    public class RecentProjectsViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        /// <summary>One recent entry: its own name first, since many projects sit in look-alike folders.</summary>
        public sealed class RecentEntry
        {
            public string Path { get; init; }
            public string Name => System.IO.Path.GetFileName(Path.TrimEnd('\\', '/'));
            public string Where => System.IO.Path.GetDirectoryName(Path.TrimEnd('\\', '/')) ?? "";
            // Screen readers and UI automation read a row by this.
            public override string ToString() => Path;
        }

        /// <summary>Extracted and hg-engine project folders.</summary>
        public ObservableCollection<RecentEntry> RecentFolders { get; } = new();
        /// <summary>ROM files, kept for extracting afresh, as when testing on a clean base ROM.</summary>
        public ObservableCollection<RecentEntry> RecentRoms { get; } = new();

        public bool HasRecents => RecentFolders.Count + RecentRoms.Count > 0;
        public bool HasRecentFolders => RecentFolders.Count > 0;
        public bool HasRecentRoms => RecentRoms.Count > 0;

        public RecentProjectsViewModel()
        {
            if (global::Avalonia.Controls.Design.IsDesignMode)
            {
                Add(@"C:\hacks\HeartGold (USA)_DSPRE_contents");
                Add(@"C:\hacks\HeartGold (USA).nds");
                return;
            }
            Reload();
        }

        /// <summary>Reads the list again from the settings, as after a project was opened or removed.</summary>
        public void Reload()
        {
            RecentFolders.Clear();
            RecentRoms.Clear();
            List<string> recents = SettingsManager.Settings?.RecentProjects;
            if (recents != null)
                foreach (string r in recents) Add(r);
            OnPropertyChanged(nameof(HasRecents));
            OnPropertyChanged(nameof(HasRecentFolders));
            OnPropertyChanged(nameof(HasRecentRoms));
        }

        private void Add(string path)
        {
            RecentEntry entry = new RecentEntry { Path = path };
            if (path.EndsWith(".nds", System.StringComparison.OrdinalIgnoreCase)) RecentRoms.Add(entry);
            else RecentFolders.Add(entry);
        }
    }
}

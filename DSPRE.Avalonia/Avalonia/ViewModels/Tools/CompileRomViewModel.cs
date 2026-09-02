using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using DSPRE.HgEngine;

namespace DSPRE.Avalonia.ViewModels.Tools
{
    /// <summary>Runs hg-engine's real `make` build (ASM hooks + every data domain, not just the isolated
    /// per-domain targets Phases 1-2 use) and streams its output to a live log panel.</summary>
    public class CompileRomViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public ObservableCollection<string> LogLines { get; } = new();

        private bool _isRunning;
        public bool IsRunning
        {
            get => _isRunning;
            set { if (_isRunning != value) { _isRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanClose)); } }
        }
        public bool CanClose => !IsRunning;

        private string _resultText = "";
        public string ResultText
        {
            get => _resultText;
            set { if (_resultText != value) { _resultText = value; OnPropertyChanged(); } }
        }

        private bool _succeeded;
        public bool Succeeded
        {
            get => _succeeded;
            set { if (_succeeded != value) { _succeeded = value; OnPropertyChanged(); OnPropertyChanged(nameof(ResultBrush)); } }
        }

        public IBrush ResultBrush => Succeeded ? Brushes.LimeGreen : Brushes.OrangeRed;

        public async Task RunAsync()
        {
            IsRunning = true;
            LogLines.Clear();
            ResultText = "";
            Succeeded = false;

            string ndsPath = Path.Combine(HgEngineProject.RepoPathUnc, "test.nds");
            DateTime? beforeWriteTimeUtc = File.Exists(ndsPath) ? File.GetLastWriteTimeUtc(ndsPath) : (DateTime?)null;

            bool ok = await Task.Run(() => HgEngineBuild.RunFullBuild(
                line => Dispatcher.UIThread.Post(() => LogLines.Add(line)),
                out _));

            bool ndsProduced = File.Exists(ndsPath) &&
                (beforeWriteTimeUtc == null || File.GetLastWriteTimeUtc(ndsPath) > beforeWriteTimeUtc);

            if (ok && ndsProduced)
            {
                Succeeded = true;
                ResultText = "Build succeeded: " + ndsPath;
            }
            else if (ok)
            {
                ResultText = "make reported success, but test.nds wasn't produced or updated at " + ndsPath + ".";
            }
            else
            {
                ResultText = "Build failed. See the log above for details.";
            }

            IsRunning = false;
        }
    }
}

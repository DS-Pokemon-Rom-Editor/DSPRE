using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using DSPRE.HgEngine;

namespace DSPRE.Avalonia.ViewModels.Shell
{
    /// <summary>Runs hg-engine's full `make` build and streams its output to a live log panel.</summary>
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

        private string _buildRom;

        /// <summary>The ROM make writes; test.nds unless a build name was chosen.</summary>
        public string BuildRom
        {
            get => _buildRom;
            set { _buildRom = value; OnPropertyChanged(); OnPropertyChanged(nameof(Command)); }
        }

        public string Command => HgEngineProject.IsLinked ? HgEngineBuild.FullBuildCommand(BuildRom) : "make";

        /// <summary>Raised after each batch of lines is added, so the view scrolls once per batch.</summary>
        public event Action LinesAdded;

        // A parallel make prints faster than the UI can add lines one at a time, so they are added in batches.
        private readonly ConcurrentQueue<string> _pendingLines = new();

        private void FlushLines()
        {
            if (_pendingLines.IsEmpty) return;
            while (_pendingLines.TryDequeue(out string line)) LogLines.Add(line);
            LinesAdded?.Invoke();
        }

        public async Task RunAsync()
        {
            IsRunning = true;
            LogLines.Clear();
            ResultText = "";
            Succeeded = false;
            DispatcherTimer flush = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, (_, _) => FlushLines());
            flush.Start();
            // The window can't close while this runs, so every way out has to clear it.
            try
            {
                string ndsPath = Path.Combine(HgEngineProject.RepoPathUnc, HgEngineProject.IsSafeBuildRomName(BuildRom) ? BuildRom : "test.nds");
                DateTime? beforeWriteTimeUtc = PlainFiles.WrittenAt(ndsPath);

                bool ok = await Task.Run(() => HgEngineBuild.RunFullBuild(
                    _pendingLines.Enqueue,
                    out _, BuildRom));

                bool ndsProduced = PlainFiles.WrittenAt(ndsPath) is DateTime written &&
                    (beforeWriteTimeUtc == null || written > beforeWriteTimeUtc);

                if (ok && ndsProduced)
                {
                    Succeeded = true;
                    ResultText = "Build succeeded: " + ndsPath;
                }
                else if (ok)
                {
                    ResultText = "make reported success, but the ROM wasn't produced or updated at " + ndsPath + ".";
                }
                else
                {
                    ResultText = "Build failed. See the log above for details.";
                }
            }
            catch (Exception ex) when (AppLogger.Noted("CompileRomViewModel.RunAsync", ex))
            {
                ResultText = "Build failed: " + ex.Message;
            }
            finally
            {
                flush.Stop();
                FlushLines();
                IsRunning = false;
            }
        }
    }
}

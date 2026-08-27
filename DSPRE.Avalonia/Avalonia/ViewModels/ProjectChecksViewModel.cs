using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>Drives the Project Checks window: a validation report + a where-used reverse lookup,
    /// both built from <see cref="ProjectIndex"/> (the map-header cross-references).</summary>
    public class ProjectChecksViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void On([CallerMemberName] string n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        private bool Set<T>(ref T f, T v, [CallerMemberName] string n = null)
        { if (Equals(f, v)) return false; f = v; On(n); return true; }

        // ── Validation tab ─────────────────────────────────────────────────────
        public ObservableCollection<ValidationIssue> Issues { get; } = new();
        private string _validationStatus = "Click “Run” to scan headers, evolutions and trainers for broken references.";
        public string ValidationStatus { get => _validationStatus; private set => Set(ref _validationStatus, value); }

        private bool _isRunning;
        /// <summary>True while a scan is in flight; the view binds the Run button's IsEnabled to its inverse.</summary>
        public bool IsRunning { get => _isRunning; private set => Set(ref _isRunning, value); }

        public async void RunValidation()
        {
            if (_isRunning) return;          // ignore re-clicks while scanning
            IsRunning = true;
            Issues.Clear();
            ValidationStatus = "Scanning…";
            try
            {
                // The scan reads many ROM files; run it off the UI thread so the window stays responsive.
                var found = await Task.Run(() => ProjectIndex.Validate());
                foreach (var i in found) Issues.Add(i);
                ValidationStatus = found.Count == 0
                    ? "No problems found. Every reference points at something that exists. ✓"
                    : $"{found.Count} issue(s) found.";
            }
            catch (Exception ex) { ValidationStatus = "Validation failed: " + ex.Message; }
            finally { IsRunning = false; }
        }

        // ── Where-used tab ─────────────────────────────────────────────────────
        public ObservableCollection<string> RefKinds { get; } = new();
        public ObservableCollection<string> FindResults { get; } = new();

        private int _refKindIndex;
        public int RefKindIndex { get => _refKindIndex; set => Set(ref _refKindIndex, value); }

        private decimal _lookupId;
        public decimal LookupId { get => _lookupId; set => Set(ref _lookupId, value); }

        private string _findStatus = "Pick a file type and id, then Find which headers reference it.";
        public string FindStatus { get => _findStatus; private set => Set(ref _findStatus, value); }

        public ProjectChecksViewModel()
        {
            foreach (var n in Enum.GetNames<RefKind>()) RefKinds.Add(n);
        }

        public void Find()
        {
            FindResults.Clear();
            try
            {
                var kind = (RefKind)_refKindIndex;
                var headers = ProjectIndex.HeadersUsing(kind, (int)_lookupId);
                foreach (var h in headers) FindResults.Add($"Header {h}");
                FindStatus = headers.Count == 0
                    ? $"No header references {kind} {(int)_lookupId}."
                    : $"{headers.Count} header(s) reference {kind} {(int)_lookupId}.";
            }
            catch (Exception ex) { FindStatus = "Lookup failed: " + ex.Message; }
        }
    }
}

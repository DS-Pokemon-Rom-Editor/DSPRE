using System.ComponentModel;
using System.Runtime.CompilerServices;
using global::Avalonia.Controls;
using DSPRE.Avalonia;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels
{
    /// <summary>
    /// ViewModel for the Avalonia <c>MainWindowView</c> shell — the in-progress
    /// replacement for the WinForms main window.
    ///
    /// For now it hosts the editors that have already been ported to Avalonia
    /// <see cref="UserControl"/>s as embedded tabs (currently the Camera Editor),
    /// and exposes ROM state so the menu can launch the remaining editors (which
    /// still open as standalone Avalonia windows) only when a ROM is loaded.
    /// </summary>
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        // ── Embedded editor sub-VMs ────────────────────────────────────────────
        public HeaderEditorViewModel HeaderVM { get; }
        public CameraEditorViewModel CameraVM { get; }

        // ── ROM state ──────────────────────────────────────────────────────────
        public bool IsRomLoaded => AvaloniaEditorLauncher.IsRomLoaded;

        public string Title =>
            IsRomLoaded
                ? $"DSPRE — {GetGameDisplayName()} (Avalonia preview)"
                : "DSPRE (Avalonia preview)";

        // ── Design-time constructor ────────────────────────────────────────────
        public MainWindowViewModel()
        {
            HeaderVM = new HeaderEditorViewModel();
            CameraVM = new CameraEditorViewModel();
        }

        // ── Runtime constructor ────────────────────────────────────────────────
        public MainWindowViewModel(bool runtime)
        {
            HeaderVM = new HeaderEditorViewModel(runtime);
            CameraVM = new CameraEditorViewModel(runtime);
        }
    }
}

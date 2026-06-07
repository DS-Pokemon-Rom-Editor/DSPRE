using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WinForms = System.Windows.Forms;

namespace DSPRE
{
    /// <summary>
    /// Avalonia Application entry point.
    /// On startup it shows the existing WinForms MainProgram as the main window.
    /// Both Avalonia (Win32 backend) and WinForms share the same Win32 message pump,
    /// so they co-exist on the same STA thread without conflicts.
    /// </summary>
    public class AvaloniaApp : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Prevent Avalonia from shutting down when an editor window closes.
                // The process lifetime is controlled exclusively by the WinForms main form.
                desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                // Build and show the WinForms main form.
                // Velopack and directory setup were already run before Avalonia started.
                var mainProgram = new MainProgram();
                CrashReporter.Initialize(mainProgram);

                // Show the WinForms form independently — it owns its own Win32 HWND.
                // Avalonia's Win32 backend pumps the same message loop so both stay alive.
                mainProgram.Show();

                // Hook WinForms FormClosed → shut down Avalonia lifetime so the process exits cleanly.
                mainProgram.FormClosed += (_, _) => desktop.Shutdown();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}

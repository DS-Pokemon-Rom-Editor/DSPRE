using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using DSPRE.Avalonia.Data;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// The one place an editor says a save worked.
    ///
    /// Editors save through their own buttons rather than a shared command, so there is no single
    /// call site to hook. What they do share is a window, so the notice is drawn into the active
    /// window's overlay layer instead of needing a status bar, a bound property, or any XAML at all.
    /// An editor with no StatusText gets the same confirmation as one that has it.
    ///
    /// A brief overlay rather than a message box on purpose: saving is the common case and often
    /// repeated, and a dialog that has to be dismissed after every save trains people to click
    /// through it. The underlying save calls pass showSuccessMessage:false for the same reason.
    /// </summary>
    public static class SaveNotice
    {
        private const int VisibleMs = 2200;

        /// <summary>"Saved Matrix 3", "Saved Learnset (Mon 25)" and so on. Give the subject, not the
        /// verb; the word Saved is added here so every editor phrases it the same way.</summary>
        public static void Saved(string subject) => Show(Compose(subject));

        /// <summary>
        /// Builds the confirmation line. Editors pass their UnsavedChangesDescription, which is
        /// already a per-editor human phrase, so the wording stays in one place per editor instead of
        /// being invented twice. A blank subject falls back rather than rendering "Saved ".
        /// </summary>
        public static string Compose(string subject)
            => string.IsNullOrWhiteSpace(subject) ? "Saved." : "Saved " + subject.Trim();

        /// <summary>A confirmation that is not a save, for the few editors that write by another
        /// name (applying a patch, exporting, repacking).</summary>
        public static void Show(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            if (Dispatcher.UIThread.CheckAccess()) Present(message);
            else Dispatcher.UIThread.Post(() => Present(message));
        }

        private static Border _showing;
        private static OverlayLayer _showingIn;
        private static DispatcherTimer _life;

        private static void Present(string message)
        {
            try
            {
                var window = ActiveWindow();
                var layer = window == null ? null : OverlayLayer.GetOverlayLayer(window);
                if (layer == null) return;

                // Only ever one notice on screen. A composite editor saves several children in a row,
                // and stacked toasts would sit on top of each other; the last message is also the one
                // worth reading, since it names the whole save rather than a part of it.
                Clear();

                _showing = Build(message);
                _showingIn = layer;
                layer.Children.Add(_showing);

                _life = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(VisibleMs) };
                _life.Tick += (_, _) => Clear();
                _life.Start();
            }
            catch (Exception ex)
            {
                // A confirmation that fails must never take the save down with it.
                AppLogger.Error("Save notice failed: " + ex.Message);
            }
        }

        private static void Clear()
        {
            _life?.Stop();
            _life = null;
            if (_showing != null) _showingIn?.Children.Remove(_showing);
            _showing = null;
            _showingIn = null;
        }

        private static Border Build(string message) => new Border
        {
            Background = Theme("Editor.StatusBg", new SolidColorBrush(Color.FromArgb(0xF0, 0x20, 0x20, 0x20))),
            BorderBrush = StatusBrushes.Good,
            BorderThickness = new Thickness(0, 0, 0, 2),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(14, 8, 14, 8),
            Margin = new Thickness(0, 0, 0, 28),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsHitTestVisible = false,   // never let a confirmation swallow the next click
            Child = new TextBlock
            {
                Text = message,
                Foreground = Theme("Editor.Text", Brushes.White),
                FontSize = 13,
            },
        };

        private static IBrush Theme(string key, IBrush fallback)
        {
            var app = Application.Current;
            return app != null && app.TryGetResource(key, app.ActualThemeVariant, out object found) && found is IBrush b
                ? b
                : fallback;
        }

        private static Window ActiveWindow()
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                return null;

            foreach (var w in desktop.Windows)
                if (w.IsActive) return w;

            return desktop.MainWindow;
        }
    }
}

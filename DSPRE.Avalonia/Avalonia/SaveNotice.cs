using System;
using System.IO;
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
    /// The one place an editor says a save worked: a short notice on the active window, since a dialog after every save
    /// just gets clicked through.
    /// </summary>
    public static class SaveNotice
    {
        private const int VisibleMs = 2200;
        // Long enough to reach the Show file button; hovering the notice keeps it up.
        private const int FileVisibleMs = 6000;

        /// <summary>"Saved Matrix 3", "Saved Learnset (Mon 25)" and so on. Give the subject, not the
        /// verb; the word Saved is added here so every editor phrases it the same way.</summary>
        public static void Saved(string subject) => Show(Compose(subject));

        /// <summary>Builds the line from the editor's UnsavedChangesDescription; a blank subject gets a fallback.</summary>
        public static string Compose(string subject)
            => string.IsNullOrWhiteSpace(subject) ? "Saved." : "Saved " + subject.Trim();

        /// <summary>A confirmation that is not a save, for the few editors that write by another
        /// name (applying a patch, exporting, repacking).</summary>
        public static void Show(string message) => Post(message, null);

        /// <summary>"Exported egg_moves.csv", with a Show file button that opens its folder.</summary>
        public static void Exported(string path) => Post("Exported " + Path.GetFileName(path), path);

        /// <summary>"Exported 590 building models to buildings", with an Open folder button. Give what went out, not the verb.</summary>
        public static void ExportedTo(string what, string folder)
            => Post($"Exported {what.Trim()} to {Path.GetFileName(folder.TrimEnd('\\', '/'))}", folder, opensFolder: true);

        /// <summary>"Imported Bulbasaur's egg moves from egg_moves.csv". Give what came in, not the verb; the file is left out when unknown.</summary>
        public static void Imported(string what, string path)
        {
            string file = string.IsNullOrEmpty(path) ? null : Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(what)) Post(file == null ? null : "Imported " + file, null);
            else Post(file == null ? "Imported " + what.Trim() : $"Imported {what.Trim()} from {file}", null);
        }

        private static void Post(string message, string file, bool opensFolder = false)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            if (Dispatcher.UIThread.CheckAccess()) Present(message, file, opensFolder);
            else Dispatcher.UIThread.Post(() => Present(message, file, opensFolder));
        }

        private static Border _showing;
        private static OverlayLayer _showingIn;
        private static DispatcherTimer _life;

        private static void Present(string message, string file, bool opensFolder)
        {
            try
            {
                Window window = ActiveWindow();
                OverlayLayer layer = window == null ? null : OverlayLayer.GetOverlayLayer(window);
                if (layer == null) return;

                // One notice at a time: a composite editor saves several children, and the last message names the whole
                // save.
                Clear();

                _showing = Build(message, file, opensFolder);
                _showingIn = layer;
                layer.Children.Add(_showing);
                Place(_showing, window.ClientSize);

                _life = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(file == null ? VisibleMs : FileVisibleMs) };
                _life.Tick += (_, _) => Clear();
                _life.Start();
                if (file != null)
                {
                    DispatcherTimer life = _life;
                    _showing.PointerEntered += (_, _) => life.Stop();
                    _showing.PointerExited += (_, _) => life.Start();
                }
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

        private static Border Build(string message, string file, bool opensFolder)
        {
            TextBlock text = new TextBlock
            {
                Text = message,
                Foreground = Theme("Editor.Text", Brushes.White),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Control child = text;
            if (file != null)
            {
                string label = opensFolder ? "Open folder" : Directory.Exists(file) ? "Show folder" : "Show file";
                Button show = new Button { Content = label, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(12, 0, 0, 0) };
                show.Click += (_, _) =>
                {
                    if (opensFolder) SystemShell.OpenWithDefaultApp(file);
                    else SystemShell.RevealInFileManager(file);
                    Clear();
                };
                child = new StackPanel { Orientation = Orientation.Horizontal, Children = { text, show } };
            }
            return Frame(child, file != null);
        }

        private static Border Frame(Control child, bool clickable) => new Border
        {
            Background = Theme("Editor.StatusBg", new SolidColorBrush(Color.FromArgb(0xF0, 0x20, 0x20, 0x20))),
            BorderBrush = StatusBrushes.Good,
            BorderThickness = new Thickness(0, 0, 0, 2),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(14, 8, 14, 8),
            // A plain confirmation never swallows the next click; one with a button has to take its own.
            IsHitTestVisible = clickable,
            Child = child,
        };

        // The overlay layer is a canvas, which ignores alignment and may not be laid out yet, so the notice is put at
        // the bottom centre of the window by hand.
        private static void Place(Control notice, Size area)
        {
            notice.Measure(Size.Infinity);
            Size size = notice.DesiredSize;
            Canvas.SetLeft(notice, Math.Max(0, (area.Width - size.Width) / 2));
            Canvas.SetTop(notice, Math.Max(0, area.Height - size.Height - 28));
        }

        private static IBrush Theme(string key, IBrush fallback)
        {
            Application app = Application.Current;
            return app != null && app.TryGetResource(key, app.ActualThemeVariant, out object found) && found is IBrush b
                ? b
                : fallback;
        }

        private static Window ActiveWindow()
        => OwnerWindow.Current;
    }
}

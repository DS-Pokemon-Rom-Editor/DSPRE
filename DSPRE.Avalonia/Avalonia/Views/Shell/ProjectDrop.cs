using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace DSPRE.Avalonia.Views.Shell
{
    /// <summary>Opens a .nds or project folder dropped on a window, the same way as a recent project.</summary>
    internal static class ProjectDrop
    {
        /// <param name="before">Runs just before opening, so a window that is not the main one can close first.</param>
        public static void Attach(Window window, Func<MainWindowView> main, Action before = null)
        {
            DragDrop.SetAllowDrop(window, true);
            window.AddHandler(DragDrop.DragOverEvent, (_, e) =>
                e.DragEffects = PathOf(e) != null && main() != null ? DragDropEffects.Copy : DragDropEffects.None);
            window.AddHandler(DragDrop.DropEvent, async (_, e) =>
            {
                string path = PathOf(e);
                MainWindowView m = main();
                if (path == null || m == null) return;
                e.Handled = true;
                before?.Invoke();
                await m.OpenRecentAsync(path);
            });
        }

        // One .nds file or one folder; anything else is not something DSPRE opens.
        private static string PathOf(DragEventArgs e)
        {
            List<IStorageItem> items = e.DataTransfer?.TryGetFiles()?.ToList();
            if (items == null || items.Count != 1) return null;
            string path = items[0].TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) return null;
            if (System.IO.Directory.Exists(path)) return path;
            return System.IO.File.Exists(path) && path.EndsWith(".nds", StringComparison.OrdinalIgnoreCase) ? path : null;
        }
    }
}

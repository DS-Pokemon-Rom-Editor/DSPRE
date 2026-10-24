using System;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// App-wide notifications so open editors can refresh shared data without being coupled to each
    /// other. The Text editor raises <see cref="NamesChanged"/> after saving; the Label editor raises
    /// <see cref="LabelsChanged"/> after customising a dropdown. Editors subscribe and reload their
    /// combo sources (preserving the current selection). Handlers run on whatever thread raises the
    /// event; marshal to the UI thread in the subscriber if needed.
    /// </summary>
    public static class AppEvents
    {
        /// <summary>ROM text names (Pokémon / items / moves / abilities / trainers / locations) may have changed.</summary>
        public static event EventHandler NamesChanged;

        /// <summary>A customisable dropdown-label category was edited (see <see cref="Data.LabelStore"/>).</summary>
        public static event EventHandler LabelsChanged;

        /// <summary>A patch was applied; editors gated on a patch check their state again.</summary>
        public static event EventHandler RomPatchStateChanged;

        /// <summary>The game banner (icon / titles) was edited; the main window refreshes its icon.</summary>
        public static event EventHandler BannerChanged;

        /// <summary>The linked hg-engine checkout, or whether it's on, changed; the menu checks which source-backed editors open.</summary>
        public static event EventHandler HgEngineLinkChanged;

        /// <summary>A member of a mapped archive was written, so previews built from it can redraw.</summary>
        public static event EventHandler<RomInfo.DirNames> ArchiveMemberSaved;
        public static void RaiseArchiveMemberSaved(RomInfo.DirNames dir) => ArchiveMemberSaved?.Invoke(null, dir);

        /// <summary>Runs <paramref name="redraw"/> on the UI thread after members of <paramref name="dirs"/> are saved
        /// elsewhere, until disposed. A save that writes many members redraws once.</summary>
        public static IDisposable WhenSaved(Action redraw, params RomInfo.DirNames[] dirs) => new SavedWatch(redraw, d => Array.IndexOf(dirs, d) >= 0);

        /// <summary>The same, for a preview that reads different archives as you use it.</summary>
        public static IDisposable WhenSaved(Action redraw, Func<RomInfo.DirNames, bool> reads) => new SavedWatch(redraw, reads);

        private sealed class SavedWatch : IDisposable
        {
            private readonly Action _redraw;
            private readonly Func<RomInfo.DirNames, bool> _reads;
            private int _queued;

            public SavedWatch(Action redraw, Func<RomInfo.DirNames, bool> reads) { _redraw = redraw; _reads = reads; ArchiveMemberSaved += OnSaved; }

            private void OnSaved(object sender, RomInfo.DirNames dir)
            {
                if (!_reads(dir) || System.Threading.Interlocked.Exchange(ref _queued, 1) == 1) return;
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    System.Threading.Interlocked.Exchange(ref _queued, 0);
                    if (!_disposed) _redraw();
                }, global::Avalonia.Threading.DispatcherPriority.Background);
            }

            private bool _disposed;
            public void Dispose() { _disposed = true; ArchiveMemberSaved -= OnSaved; }
        }

        /// <summary>Class intros (trainer class metadata patch) were saved in the VS intro editor.</summary>
        public static event EventHandler ClassIntrosSaved;
        public static void RaiseClassIntrosSaved() => ClassIntrosSaved?.Invoke(null, EventArgs.Empty);

        /// <summary>Counts <see cref="NamesChanged"/> raises, so a view that was hidden can tell it missed one.</summary>
        public static int NamesVersion { get; private set; }
        // Header labels name places, so they are rebuilt whenever names or a header change.
        public static void RaiseNamesChanged() { NamesVersion++; HeaderLabels.Forget(); NamesChanged?.Invoke(null, EventArgs.Empty); }
        public static void RaiseLabelsChanged() => LabelsChanged?.Invoke(null, EventArgs.Empty);
        public static void RaiseRomPatchStateChanged() => RomPatchStateChanged?.Invoke(null, EventArgs.Empty);
        public static void RaiseBannerChanged() => BannerChanged?.Invoke(null, EventArgs.Empty);
        public static void RaiseHgEngineLinkChanged() => HgEngineLinkChanged?.Invoke(null, EventArgs.Empty);

        /// <summary>A map file was written; the sender is the editor that wrote it.</summary>
        public static event EventHandler<int> MapSaved;
        public static void RaiseMapSaved(object sender, int mapIndex) => MapSaved?.Invoke(sender, mapIndex);

        /// <summary>A header, event file or matrix was written; the sender is the editor that wrote it.</summary>
        public static event EventHandler<int> HeaderSaved, EventFileSaved, MatrixSaved;
        public static void RaiseHeaderSaved(object sender, int id) { HeaderLabels.Forget(); HeaderSaved?.Invoke(sender, id); }
        public static void RaiseEventFileSaved(object sender, int id) => EventFileSaved?.Invoke(sender, id);
        public static void RaiseMatrixSaved(object sender, int id) => MatrixSaved?.Invoke(sender, id);
        public static event EventHandler<int> AreaDataSaved;
        public static void RaiseAreaDataSaved(object sender, int id) => AreaDataSaved?.Invoke(sender, id);

        /// <summary>A personal data file (or its TM121+ compatibility) was written; the sender is the editor that wrote it.</summary>
        public static event EventHandler<int> PersonalDataSaved;
        public static void RaisePersonalDataSaved(object sender, int id) => PersonalDataSaved?.Invoke(sender, id);

        /// <summary>A level-script binary was written; the sender is the editor that wrote it.</summary>
        public static event EventHandler<int> LevelScriptSaved;
        public static void RaiseLevelScriptSaved(object sender, int id) => LevelScriptSaved?.Invoke(sender, id);

        /// <summary>A Rotom script source was written; the sender is the editor that wrote it. The path is
        /// the file, or null when every source was regenerated.</summary>
        public static event EventHandler<string> ScriptSourceSaved;
        public static void RaiseScriptSourceSaved(object sender, string path) => ScriptSourceSaved?.Invoke(sender, path);
    }
}

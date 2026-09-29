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

        /// <summary>A ROM Patch Toolbox patch was applied; editors gating a feature on a patch flag
        /// (e.g. Map Editor's Building Rotation fields) should re-check their state.</summary>
        public static event EventHandler RomPatchStateChanged;

        /// <summary>The game banner (icon / titles) was edited; the main window refreshes its icon.</summary>
        public static event EventHandler BannerChanged;

        /// <summary>The linked hg-engine checkout (or its enabled state) changed; the menu re-checks
        /// which of the 5 source-backed editors are unblocked.</summary>
        public static event EventHandler HgEngineLinkChanged;

        public static void RaiseNamesChanged() => NamesChanged?.Invoke(null, EventArgs.Empty);
        public static void RaiseLabelsChanged() => LabelsChanged?.Invoke(null, EventArgs.Empty);
        public static void RaiseRomPatchStateChanged() => RomPatchStateChanged?.Invoke(null, EventArgs.Empty);
        public static void RaiseBannerChanged() => BannerChanged?.Invoke(null, EventArgs.Empty);
        public static void RaiseHgEngineLinkChanged() => HgEngineLinkChanged?.Invoke(null, EventArgs.Empty);

        /// <summary>A map file was written; the sender is the editor that wrote it.</summary>
        public static event EventHandler<int> MapSaved;
        public static void RaiseMapSaved(object sender, int mapIndex) => MapSaved?.Invoke(sender, mapIndex);

        /// <summary>A header, event file or matrix was written; the sender is the editor that wrote it.</summary>
        public static event EventHandler<int> HeaderSaved, EventFileSaved, MatrixSaved;
        public static void RaiseHeaderSaved(object sender, int id) => HeaderSaved?.Invoke(sender, id);
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

using System;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// App-wide notifications so open editors can refresh shared data without being coupled to each
    /// other. The Text editor raises <see cref="NamesChanged"/> after saving; the Label editor raises
    /// <see cref="LabelsChanged"/> after customising a dropdown. Editors subscribe and reload their
    /// combo sources (preserving the current selection). Handlers run on whatever thread raises the
    /// event — marshal to the UI thread in the subscriber if needed.
    /// </summary>
    public static class AppEvents
    {
        /// <summary>ROM text names (Pokémon / items / moves / abilities / trainers / locations) may have changed.</summary>
        public static event EventHandler NamesChanged;

        /// <summary>A customisable dropdown-label category was edited (see <see cref="Data.LabelStore"/>).</summary>
        public static event EventHandler LabelsChanged;

        /// <summary>A ROM Patch Toolbox patch was applied — editors gating a feature on a patch flag
        /// (e.g. Map Editor's Building Rotation fields) should re-check their state.</summary>
        public static event EventHandler RomPatchStateChanged;

        /// <summary>The game banner (icon / titles) was edited — the main window refreshes its icon.</summary>
        public static event EventHandler BannerChanged;

        public static void RaiseNamesChanged() => NamesChanged?.Invoke(null, EventArgs.Empty);
        public static void RaiseLabelsChanged() => LabelsChanged?.Invoke(null, EventArgs.Empty);
        public static void RaiseRomPatchStateChanged() => RomPatchStateChanged?.Invoke(null, EventArgs.Empty);
        public static void RaiseBannerChanged() => BannerChanged?.Invoke(null, EventArgs.Empty);
    }
}

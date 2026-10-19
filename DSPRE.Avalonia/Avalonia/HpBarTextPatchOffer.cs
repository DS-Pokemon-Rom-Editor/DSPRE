using DSPRE.ROMFiles;

namespace DSPRE.Avalonia
{
    /// <summary>
    /// Sends the HP bar editors to the clear battle text background patch in the ROM Patch Toolbox. It changes the game's
    /// code, so the editors only preview it and never apply it themselves.
    /// </summary>
    internal static class HpBarTextPatchOffer
    {
        public const string Tip =
            "The \"Clear battle text background\" patch removes the white box behind the name, level and HP in battle.";

        public static bool CanOffer => !RomInfo.isHGE && BattleGaugeTextPatch.SupportsCurrentRom && !BattleGaugeTextPatch.IsApplied;

        public static void Open() => AvaloniaEditorLauncher.OpenPatchToolboxAt("hpBarText");
    }
}

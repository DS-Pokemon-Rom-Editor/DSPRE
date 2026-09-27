using System.Threading.Tasks;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia
{
    /// <summary>Offers the ROM Patch Toolbox's ARM9 expansion when a table editor needs more room than the game has.</summary>
    internal static class Arm9ExpansionOffer
    {
        /// <summary>True when the expansion is in place, after asking and applying the toolbox patch if needed.</summary>
        public static async Task<bool> EnsureAsync(string need, string title)
        {
            if (SyntheticOverlaySpace.Available()) return true;
            if (PatchToolboxLogic.Arm9ExpansionWhyNot() is string why)
            {
                await DialogHelper.ShowInfo($"{need} needs the ARM9 expansion. {why}", title);
                return false;
            }
            if (!await DialogHelper.AskYesNo($"{need} needs the ARM9 expansion, which isn't applied to this ROM yet. Apply it now?", title))
                return false;
            PatchToolboxLogic.ApplyARM9ExpansionPatch();
            return SyntheticOverlaySpace.Available();
        }
    }
}

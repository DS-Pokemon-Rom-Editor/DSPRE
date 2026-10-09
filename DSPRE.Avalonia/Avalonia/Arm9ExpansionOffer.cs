using System.Threading.Tasks;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia
{
    /// <summary>Sends a table editor that needs more room than the game has to the ARM9 expansion in the ROM Patch Toolbox.</summary>
    internal static class Arm9ExpansionOffer
    {
        /// <summary>True when the expansion is already in place; otherwise points at the toolbox patch and returns false.</summary>
        public static async Task<bool> EnsureAsync(string need, string title)
        {
            if (SyntheticOverlaySpace.Available()) return true;
            if (PatchToolboxLogic.Arm9ExpansionWhyNot() is string why)
            {
                await DialogHelper.ShowInfo($"{need} needs the ARM9 expansion. {why}", title);
                return false;
            }
            await PatchHandover.OfferAsync("arm9", "Expand ARM9 (synthetic overlay)", need, title);
            return false;
        }
    }
}

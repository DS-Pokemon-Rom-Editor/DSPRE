using System;
using System.IO;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// Stops a write landing where hg-engine's build patches. Every DSPRE write to arm9 or an overlay
    /// funnels through DSUtils.WriteToFile, so asking once here covers the item table, marts, starters,
    /// the trainer class expansion, the Patch Toolbox, the script command table and the trainer roster
    /// without each of them growing its own check.
    /// </summary>
    public static class HgEngineWriteGuard
    {
        /// <summary>Raised instead of writing, so the UI layer decides how to say it.</summary>
        public static Action<string> OnRefused;

        public static bool Refuses(string filePath, long offset, int length)
        {
            if (!HgEngineProject.IsActive || string.IsNullOrEmpty(filePath)) return false;
            if (!TryIdentify(filePath, out int overlayNumber)) return false;

            HgEngineClaim claim = HgEngineClaimedRanges.Claiming(overlayNumber, offset, length);
            if (claim == null) return false;

            string binary = overlayNumber < 0 ? "arm9" : $"overlay {overlayNumber}";
            string message =
                $"hg-engine's build writes over this part of {binary}, so the change would be undone by "
                + $"the next compile. It patches {claim} there.";

            AppLogger.Warn($"Refused write to {binary} at 0x{offset:X}: {claim}");
            OnRefused?.Invoke(message);
            return true;
        }

        /// <summary>Whether a path is the open project's arm9 or one of its overlays.</summary>
        internal static bool TryIdentify(string filePath, out int overlayNumber)
        {
            overlayNumber = 0;

            string name = Path.GetFileName(filePath);
            if (string.IsNullOrEmpty(name)) return false;

            if (name.Equals("arm9.bin", StringComparison.OrdinalIgnoreCase))
            {
                overlayNumber = -1;
                return true;
            }

            // ds-rom names them ovNNN.bin, ndstool overlay_NNNN.bin.
            string stem = Path.GetFileNameWithoutExtension(name);
            foreach (string prefix in new[] { "overlay_", "ov" })
            {
                if (!stem.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                string digits = stem.Substring(prefix.Length);
                return digits.Length > 0 && digits.All(char.IsDigit) && int.TryParse(digits, out overlayNumber);
            }
            return false;
        }
    }
}

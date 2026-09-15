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
            if (string.IsNullOrEmpty(filePath)) return false;
            // Refused with or without a checkout: nothing on an hg-engine ROM loads this archive's expansion.
            string synthetic = HgEngineSyntheticOverlay.ExpansionRefusal();
            if (synthetic != null && IsSyntheticOverlayMember(filePath))
            {
                AppLogger.Warn($"Refused write to the synthetic overlay at 0x{offset:X}: {filePath}");
                OnRefused?.Invoke(synthetic);
                return true;
            }

            if (!HgEngineProject.IsActive) return false;
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

        internal static bool IsSyntheticOverlayMember(string filePath)
        {
            try
            {
                string dir = Filesystem.synthOverlay;
                string parent = Path.GetDirectoryName(Path.GetFullPath(filePath));
                return !string.IsNullOrEmpty(dir) && parent != null
                    && string.Equals(parent.TrimEnd('\\', '/'), Path.GetFullPath(dir).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException
                || ex is NullReferenceException || ex is System.Collections.Generic.KeyNotFoundException)
            {
                return false;
            }
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

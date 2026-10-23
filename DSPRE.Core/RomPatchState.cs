using DSPRE.Resources.ROMToolboxDB;

namespace DSPRE
{
    /// <summary>
    /// Which ROM patch toolbox patches the loaded ROM has.
    /// </summary>
    public static class RomPatchState
    {
        // Must not throw during type init (that would poison the whole class): before a ROM is
        // loaded, RomInfo.gameFamily isn't set and the DB lookup fails. ResetFlags() re-evaluates
        // this on every ROM (re)load.
        public static uint ExpandedARMfileID = SafeExpandedARMfileID();

        private static uint SafeExpandedARMfileID()
        {
            try
            {
                return ToolboxDB.SyntheticOverlayFileNumbersDB.TryGetValue(RomInfo.GameFamily, out uint id) ? id : 0u;
            }
            catch
            {
                return 0u; // RomInfo/ToolboxDB not initialized yet (no ROM loaded)
            }
        }

        public static bool FlagStandardizedItems { get; set; } = false;
        public static bool FlagArm9Expanded { get; set; } = false;
        public static bool FlagBDHCamPatchApplied { get; set; } = false;
        public static bool FlagBuildingRotationPatchApplied { get; set; } = false;
        public static bool FlagDynamicHeadersPatchApplied { get; set; } = false;
        public static bool FlagMatrixExpansionApplied { get; set; } = false;

        public static bool FlagTrainerNamesExpanded { get; set; } = false;

        /// <summary>Whether hzla's PlatPatches overworld sprite expansion (marker "OWTBLXPANDV1") is in the ROM;
        /// Platinum only, and DSPRE only detects it.</summary>
        public static bool FlagOverworldSpriteExpansionApplied { get; set; } = false;
        public static uint OverworldExpansionUsedCount { get; set; } = 0;
        public static uint OverworldExpansionCapacity { get; set; } = 0;

        public static readonly int ExpandedTrainerNameLength = 12;

        /// <summary>
        /// Resets all static patch flags to their default values.
        /// Call this when switching ROMs to ensure patch status is re-evaluated.
        /// </summary>
        public static void ResetFlags()
        {
            FlagStandardizedItems = false;
            FlagArm9Expanded = false;
            FlagBDHCamPatchApplied = false;
            FlagBuildingRotationPatchApplied = false;
            FlagDynamicHeadersPatchApplied = false;
            FlagMatrixExpansionApplied = false;
            FlagTrainerNamesExpanded = false;
            FlagOverworldSpriteExpansionApplied = false;
            OverworldExpansionUsedCount = 0;
            OverworldExpansionCapacity = 0;

            // The static field initializer only runs once, so re-evaluate when the game family changes.
            try
            {
                if (ToolboxDB.SyntheticOverlayFileNumbersDB.ContainsKey(RomInfo.GameFamily))
                {
                    ExpandedARMfileID = ToolboxDB.SyntheticOverlayFileNumbersDB[RomInfo.GameFamily];
                }
            }
            catch
            {
                // Ignore if RomInfo not fully initialized yet
            }
        }
    }
}

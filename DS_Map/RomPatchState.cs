using DSPRE.Resources.ROMToolboxDB;

namespace DSPRE
{
    /// <summary>
    /// Static ROM-patch state (which Patch Toolbox patches are applied to the loaded ROM).
    /// Extracted from the WinForms <c>PatchToolboxDialog</c> so the core (Filesystem, MapHeader, …)
    /// can query it without a UI dependency; the dialog keeps forwarding members.
    /// </summary>
    public static class RomPatchState
    {
        // Must not throw during type init (that would poison the whole class): before a ROM is
        // loaded, RomInfo.gameFamily isn't set and the DB lookup fails. ResetFlags() re-evaluates
        // this on every ROM (re)load.
        public static uint expandedARMfileID = SafeExpandedARMfileID();

        private static uint SafeExpandedARMfileID()
        {
            try
            {
                return ToolboxDB.syntheticOverlayFileNumbersDB.TryGetValue(RomInfo.gameFamily, out uint id) ? id : 0u;
            }
            catch
            {
                return 0u; // RomInfo/ToolboxDB not initialized yet (no ROM loaded)
            }
        }

        public static bool flag_standardizedItems { get; set; } = false;
        public static bool flag_arm9Expanded { get; set; } = false;
        public static bool flag_BDHCamPatchApplied { get; set; } = false;
        public static bool flag_BuildingRotationPatchApplied { get; set; } = false;
        public static bool flag_DynamicHeadersPatchApplied { get; set; } = false;
        public static bool flag_MatrixExpansionApplied { get; set; } = false;

        public static bool flag_MainComboTableRepointed { get; set; } = false;
        public static bool flag_TrainerClassBattleTableRepointed { get; set; } = false;
        public static bool flag_PokemonBattleTableRepointed { get; set; } = false;
        public static bool flag_TrainerNamesExpanded { get; set; } = false;

        public static readonly int expandedTrainerNameLength = 12;

        /// <summary>
        /// Resets all static patch flags to their default values.
        /// Call this when switching ROMs to ensure patch status is re-evaluated.
        /// </summary>
        public static void ResetFlags()
        {
            flag_standardizedItems = false;
            flag_arm9Expanded = false;
            flag_BDHCamPatchApplied = false;
            flag_BuildingRotationPatchApplied = false;
            flag_DynamicHeadersPatchApplied = false;
            flag_MatrixExpansionApplied = false;
            flag_MainComboTableRepointed = false;
            flag_TrainerClassBattleTableRepointed = false;
            flag_PokemonBattleTableRepointed = false;
            flag_TrainerNamesExpanded = false;

            // The static field initializer only runs once, so re-evaluate when the game family changes.
            try
            {
                if (ToolboxDB.syntheticOverlayFileNumbersDB.ContainsKey(RomInfo.gameFamily))
                {
                    expandedARMfileID = ToolboxDB.syntheticOverlayFileNumbersDB[RomInfo.gameFamily];
                }
            }
            catch
            {
                // Ignore if RomInfo not fully initialized yet
            }
        }
    }
}

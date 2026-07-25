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

        /// <summary>Whether the trainer-class "eye contact" encounter-music table
        /// (sTrainerEncounterBGMs) has been repointed into the synthetic overlay, e.g. by hand,
        /// following the community "adding a new trainer class" write-up. Both TrainerEditor.cs
        /// (WinForms) and TrainerClassesViewModel.cs (Avalonia) set this whenever they resolve the
        /// table's pointer, so reads/writes go to the right file instead of crashing/corrupting.</summary>
        public static bool flag_TrainerEncounterBGMTableRepointed { get; set; } = false;

        /// <summary>Whether hzla's PlatPatches "overworld sprites" expansion (marker "OWTBLXPANDV1"
        /// in the synthetic-overlay file) has been detected on the loaded ROM. DSPRE only detects
        /// this patch, it never applies it. Platinum-only.</summary>
        public static bool flag_OverworldSpriteExpansionApplied { get; set; } = false;
        public static uint overworldExpansionUsedCount { get; set; } = 0;
        public static uint overworldExpansionCapacity { get; set; } = 0;

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
            flag_TrainerEncounterBGMTableRepointed = false;
            flag_OverworldSpriteExpansionApplied = false;
            overworldExpansionUsedCount = 0;
            overworldExpansionCapacity = 0;

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

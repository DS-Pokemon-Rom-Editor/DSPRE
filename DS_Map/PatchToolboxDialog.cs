using System;
using System.IO;
using System.Windows.Forms;
using DSPRE.ROMFiles;
using System.Collections.Generic;
using DSPRE.Resources.ROMToolboxDB;
using DSPRE.Resources;
using static DSPRE.RomInfo;
using static DSPRE.Resources.ROMToolboxDB.ToolboxDB;

namespace DSPRE
{
    public partial class PatchToolboxDialog : Form
    {
        // Patch state lives in the core RomPatchState class; these forward for the many existing call sites.
        public static uint expandedARMfileID { get => RomPatchState.expandedARMfileID; set => RomPatchState.expandedARMfileID = value; }

        public static bool flag_standardizedItems { get => RomPatchState.flag_standardizedItems; private set => RomPatchState.flag_standardizedItems = value; }
        public static bool flag_arm9Expanded { get => RomPatchState.flag_arm9Expanded; private set => RomPatchState.flag_arm9Expanded = value; }
        public static bool flag_BDHCamPatchApplied { get => RomPatchState.flag_BDHCamPatchApplied; private set => RomPatchState.flag_BDHCamPatchApplied = value; }
        public static bool flag_BuildingRotationPatchApplied { get => RomPatchState.flag_BuildingRotationPatchApplied; private set => RomPatchState.flag_BuildingRotationPatchApplied = value; }
        public static bool flag_DynamicHeadersPatchApplied { get => RomPatchState.flag_DynamicHeadersPatchApplied; private set => RomPatchState.flag_DynamicHeadersPatchApplied = value; }
        public static bool flag_MatrixExpansionApplied { get => RomPatchState.flag_MatrixExpansionApplied; private set => RomPatchState.flag_MatrixExpansionApplied = value; }

        public static bool flag_MainComboTableRepointed { get => RomPatchState.flag_MainComboTableRepointed; set => RomPatchState.flag_MainComboTableRepointed = value; }
        public static bool flag_TrainerClassBattleTableRepointed { get => RomPatchState.flag_TrainerClassBattleTableRepointed; set => RomPatchState.flag_TrainerClassBattleTableRepointed = value; }
        public static bool flag_PokemonBattleTableRepointed { get => RomPatchState.flag_PokemonBattleTableRepointed; set => RomPatchState.flag_PokemonBattleTableRepointed = value; }
        public static bool flag_TrainerNamesExpanded { get => RomPatchState.flag_TrainerNamesExpanded; set => RomPatchState.flag_TrainerNamesExpanded = value; }
        public static bool flag_TrainerEncounterBGMTableRepointed { get => RomPatchState.flag_TrainerEncounterBGMTableRepointed; set => RomPatchState.flag_TrainerEncounterBGMTableRepointed = value; }

        public static readonly int expandedTrainerNameLength = RomPatchState.expandedTrainerNameLength;

        /// <summary>
        /// Resets all static patch flags to their default values.
        /// Call this when switching ROMs to ensure patch status is re-evaluated.
        /// </summary>
        public static void ResetFlags() => RomPatchState.ResetFlags();

        #region Constructor

        public PatchToolboxDialog()
        {
            InitializeComponent();

            // This is the WinForms dialog — make sure the shared apply-logic uses WinForms prompts
            // (the Avalonia toolbox swaps in native dialogs; the hooks are process-global statics).
            UseWinFormsPrompts();

            CheckStandardizedItems();

            if (ARM9PatchData.arm9ExpansionCodeDB.ContainsKey("branchString" + "_" + RomInfo.gameFamily + "_" + RomInfo.gameLanguage))
            {
                CheckARM9ExpansionApplied();
            }
            else
            {
                DisableARM9patch("Unsupported");
            }

            bool bdhCamPatchSupported = BDHCAMPatchData.SupportsCurrentRom();
            bool buildingRotationPatchSupported = BuildingRotationPatchData.SupportsCurrentRom();
            bool bdhCamPatchBlockedByProjectFormat = RomInfo.gameFamily == GameFamilies.HGSS && !RomInfo.IsDsRomProject && bdhCamPatchSupported;
            bool buildingRotationPatchBlockedByProjectFormat = !RomInfo.IsDsRomProject && buildingRotationPatchSupported;

            // ScriptCommand repoint patches are only compatible with English and Spanish versions of HGSS
            if (RomInfo.gameFamily != GameFamilies.HGSS
                || (RomInfo.gameLanguage != GameLanguages.English && RomInfo.gameLanguage != GameLanguages.Spanish))
            {
                DisableScrcmdRepointPatch("Unsupported");
            }

            if (bdhCamPatchBlockedByProjectFormat)
            {
                DisableBDHCamPatch("Convert to\nds-rom");
            }
            else if (!bdhCamPatchSupported)
            {
                DisableBDHCamPatch("Unsupported");
            }

            if (buildingRotationPatchBlockedByProjectFormat)
            {
                DisableBuildingRotationPatch("Convert to\nds-rom");
            }
            else if (!buildingRotationPatchSupported)
            {
                DisableBuildingRotationPatch("Unsupported");
            }

            CheckExpandedTrainerNamesPatchApplied();

            switch (RomInfo.gameFamily)
            {
                case GameFamilies.DP:
                    DisableDynamicHeadersPatch("Unsupported");
                    DisableMatrixExpansionPatch("Unsupported");
                    DisableScrcmdRepointPatch("Unsupported");
                    DisableKillTextureAnimationsPatch("Unsupported");
                    break;

                case GameFamilies.Plat:
                    DisableMatrixExpansionPatch("Unsupported");
                    DisableScrcmdRepointPatch("Unsupported");
                    DisableKillTextureAnimationsPatch("Unsupported");

                    if (!bdhCamPatchBlockedByProjectFormat && bdhCamPatchSupported)
                    {
                        CheckBDHCamPatchApplied();
                    }
                    if (!buildingRotationPatchBlockedByProjectFormat && buildingRotationPatchSupported)
                    {
                        CheckBuildingRotationPatchApplied();
                    }
                    CheckDynamicHeadersPatchApplied();
                    break;

                case GameFamilies.HGSS:
                    if (RomInfo.gameLanguage == GameLanguages.English || RomInfo.gameLanguage == GameLanguages.Spanish)
                    {
                        if (!bdhCamPatchBlockedByProjectFormat && bdhCamPatchSupported)
                        {
                            CheckBDHCamPatchApplied();
                        }
                        CheckMatrixExpansionApplied();
                        CheckScrcmdRepointPatchApplied();
                    }
                    else
                    {
                        DisableMatrixExpansionPatch("Unsupported");
                        DisableScrcmdRepointPatch("Unsupported");
                    }

                    if (!buildingRotationPatchBlockedByProjectFormat && buildingRotationPatchSupported)
                    {
                        CheckBuildingRotationPatchApplied();
                    }

                    CheckDynamicHeadersPatchApplied();
                    break;
            }
        }

        #region Patch Disable

        private void DisableBuildingRotationPatch(string reason)
        {
            buildingRotationButton.Enabled = false;
            buildingRotationLBL.Enabled = false;
            buildingRotationBetaLBL.Enabled = false;
            buildingRotationTextLBL.Enabled = false;
            buildingRotationButton.Text = reason;
        }

        private void DisableBDHCamPatch(string reason)
        {
            BDHCamPatchButton.Enabled = false;
            BDHCamPatchLBL.Enabled = false;
            BDHCamPatchTextLBL.Enabled = false;
            BDHCamPatchButton.Text = reason;
        }

        private void EnableBDHCamPatchAfterArm9Expansion()
        {
            if (!BDHCAMPatchData.SupportsCurrentRom())
            {
                DisableBDHCamPatch("Unsupported");
                return;
            }

            if (RomInfo.gameFamily == GameFamilies.HGSS && !RomInfo.IsDsRomProject)
            {
                DisableBDHCamPatch("Convert to\nds-rom");
                return;
            }

            if (PatchToolboxDialog.flag_BDHCamPatchApplied || PatchToolboxDialog.CheckFilesBDHCamPatchApplied())
            {
                PatchToolboxDialog.flag_BDHCamPatchApplied = true;
                BDHCamCB.Visible = true;
                DisableBDHCamPatch("Already applied");
                return;
            }

            BDHCamPatchButton.Text = "Apply Patch";
            BDHCamPatchButton.Enabled = true;
            BDHCamPatchLBL.Enabled = true;
            BDHCamPatchTextLBL.Enabled = true;
        }

        private void EnableBuildingRotationPatchAfterArm9Expansion()
        {
            if (!RomInfo.IsDsRomProject)
            {
                DisableBuildingRotationPatch("Convert to\nds-rom");
                return;
            }

            if (!BuildingRotationPatchData.SupportsCurrentRom())
            {
                DisableBuildingRotationPatch("Unsupported");
                return;
            }

            if (PatchToolboxDialog.flag_BuildingRotationPatchApplied || PatchToolboxDialog.CheckFilesBuildingRotationPatchApplied())
            {
                PatchToolboxDialog.flag_BuildingRotationPatchApplied = true;
                buildingRotationCB.Visible = true;
                DisableBuildingRotationPatch("Already applied");
                return;
            }

            buildingRotationButton.Text = "Apply Patch";
            buildingRotationButton.Enabled = true;
            buildingRotationLBL.Enabled = true;
            buildingRotationBetaLBL.Enabled = true;
            buildingRotationTextLBL.Enabled = true;
        }

        private void EnableScrcmdRepointPatchAfterArm9Expansion()
        {
            if (RomInfo.gameFamily != GameFamilies.HGSS
                || (RomInfo.gameLanguage != GameLanguages.English && RomInfo.gameLanguage != GameLanguages.Spanish))
            {
                DisableScrcmdRepointPatch("Unsupported");
                return;
            }

            if (PatchToolboxLogic.IsScrcmdRepointApplied())
            {
                repointScrcmdCB.Visible = true;
                DisableScrcmdRepointPatch("Already applied");
                return;
            }

            repointScrcmdButton.Text = "Apply Patch";
            repointScrcmdButton.Enabled = true;
            repointScrcmdLBL.Enabled = true;
            repointScrcmdTextLBL.Enabled = true;
            label1.Enabled = true;
        }

        private void DisableARM9patch(string reason)
        {
            applyARM9ExpansionButton.Enabled = false;
            arm9expansionTextLBL.Enabled = false;
            arm9expansionLBL.Enabled = false;
            applyARM9ExpansionButton.Text = reason;
        }

        private void DisableDynamicHeadersPatch(string reason)
        {
            applyDynamicHeadersButton.Enabled = false;
            dynamicHeadersTextLBL.Enabled = false;
            dynamicHeadersLBL.Enabled = false;
            applyDynamicHeadersButton.Text = reason;
        }

        private void DisableMatrixExpansionPatch(string reason)
        {
            expandMatrixButton.Enabled = false;
            matrixExpansionLBL.Enabled = false;
            matrixExpansionTextLBL.Enabled = false;
            expandMatrixButton.Text = reason;
        }

        private void DisableStandardizeItemsPatch(string reason)
        {
            applyItemStandardizeButton.Enabled = false;
            standardizePatchLBL.Enabled = false;
            standardizePatchTextLBL.Enabled = false;
            applyItemStandardizeButton.Text = reason;
        }

        private void DisableScrcmdRepointPatch(string reason)
        {
            repointScrcmdButton.Enabled = false;
            repointScrcmdLBL.Enabled = false;
            repointScrcmdTextLBL.Enabled = false;
            label1.Enabled = false;
            repointScrcmdButton.Text = reason;
        }

        private void DisableKillTextureAnimationsPatch(string reason)
        {
            disableTextureAnimationsButton.Enabled = false;
            disableTextureAnimationsLBL.Enabled = false;
            disableTextureAnimationsTextLBL.Enabled = false;
            disableTextureAnimationsButton.Text = reason;
        }

        private void DisableTrainerNameExpansionPatch(string reason)
        {
            expandTrainerNamesButton.Enabled = false;
            expandTrainerNamesLBL.Enabled = false;
            expandTrainerNamesTextLBL.Enabled = false;
            expandTrainerNamesButton.Text = reason;
        }

        #endregion Patch Disable

        #endregion Constructor

        #region Patch

        // File-state checks live in the core PatchToolboxLogic; these forward for existing call sites.
        private static bool CheckFilesArm9ExpansionApplied() => PatchToolboxLogic.CheckFilesArm9ExpansionApplied();
        public static bool CheckFilesBDHCamPatchApplied() => PatchToolboxLogic.CheckFilesBDHCamPatchApplied();
        public static bool CheckFilesBuildingRotationPatchApplied() => PatchToolboxLogic.CheckFilesBuildingRotationPatchApplied();
        public static bool CheckFilesMatrixExpansionApplied() => PatchToolboxLogic.CheckFilesMatrixExpansionApplied();
        public static bool CheckScriptsStandardizedItemNumbers() => PatchToolboxLogic.CheckScriptsStandardizedItemNumbers();

        /// <summary>Route the shared apply-logic prompts to WinForms dialogs (this dialog never shows Avalonia UI).</summary>
        private static void UseWinFormsPrompts()
        {
            PatchToolboxLogic.ConfirmYesNo = (msg, title) => MessageBox.Show(msg, title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
            PatchToolboxLogic.ShowInfo = (msg, title) => MessageBox.Show(msg, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            PatchToolboxLogic.ShowError = (msg, title) => MessageBox.Show(msg, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            PatchToolboxLogic.PickSyntheticOverlayOffset = (patchName, filePath, defaultOffset, expectedBytes, loadAddress) =>
            {
                using (var dlg = new SyntheticOverlayOffsetDialog(patchName, filePath, defaultOffset, expectedBytes, loadAddress))
                    return dlg.ShowDialog() == DialogResult.OK ? dlg.SelectedOffset : (uint?)null;
            };
        }

        public bool CheckStandardizedItems()
        {
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.scripts });

            if (!PatchToolboxDialog.flag_standardizedItems)
            {
                if (!PatchToolboxDialog.CheckScriptsStandardizedItemNumbers())
                {
                    return false;
                }
            }

            itemNumbersCB.Visible = true;
            PatchToolboxDialog.flag_standardizedItems = true;

            DisableStandardizeItemsPatch("Already applied");
            return true;
        }

        public bool CheckMatrixExpansionApplied()
        {
            if (!PatchToolboxDialog.flag_MatrixExpansionApplied)
            {
                if (!PatchToolboxDialog.CheckFilesMatrixExpansionApplied())
                {
                    return false;
                }
            }

            DisableMatrixExpansionPatch("Already applied");
            PatchToolboxDialog.flag_MatrixExpansionApplied = true;
            expandedMatrixCB.Visible = true;
            return true;
        }

        public string backupSuffix = ".backup";

        private bool CheckARM9ExpansionApplied()
        {
            if (!PatchToolboxDialog.flag_arm9Expanded)
            {
                if (!PatchToolboxDialog.CheckFilesArm9ExpansionApplied())
                {
                    return false;
                }
            }

            PatchToolboxDialog.flag_arm9Expanded = true;
            arm9patchCB.Visible = true;
            DisableARM9patch("Already applied");

            switch (RomInfo.gameFamily)
            {
                case GameFamilies.Plat:
                case GameFamilies.HGSS:
                    EnableBDHCamPatchAfterArm9Expansion();
                    break;
            }

            EnableBuildingRotationPatchAfterArm9Expansion();
            EnableScrcmdRepointPatchAfterArm9Expansion();

            return true;
        }

        public bool CheckDynamicHeadersPatchApplied()
        {
            if (!flag_DynamicHeadersPatchApplied)
            {
                if (!PatchToolboxDialog.CheckFilesDynamicHeadersPatchApplied())
                {
                    return false;
                }
            }

            PatchToolboxDialog.flag_DynamicHeadersPatchApplied = true;
            dynamicHeadersPatchCB.Visible = true;

            DisableDynamicHeadersPatch("Already applied");
            return true;
        }

        public static bool CheckFilesDynamicHeadersPatchApplied() => PatchToolboxLogic.CheckFilesDynamicHeadersPatchApplied();

        public bool CheckBDHCamPatchApplied()
        {
            if (!CheckARM9ExpansionApplied())
            {
                DisableBDHCamPatch("ARM9 not expanded!");
                return false;
            }

            if (!PatchToolboxDialog.flag_BDHCamPatchApplied)
            {
                if (!PatchToolboxDialog.CheckFilesBDHCamPatchApplied())
                {
                    return false;
                }
            }
            PatchToolboxDialog.flag_BDHCamPatchApplied = true;
            BDHCamCB.Visible = true;

            DisableBDHCamPatch("Already applied");
            return true;
        }

        public bool CheckBuildingRotationPatchApplied()
        {
            if (!RomInfo.IsDsRomProject)
            {
                DisableBuildingRotationPatch("Convert to\nds-rom");
                return false;
            }

            if (!BuildingRotationPatchData.SupportsCurrentRom())
            {
                DisableBuildingRotationPatch("Unsupported");
                return false;
            }

            if (!CheckARM9ExpansionApplied())
            {
                DisableBuildingRotationPatch("ARM9 not expanded!");
                return false;
            }

            if (!PatchToolboxDialog.flag_BuildingRotationPatchApplied)
            {
                if (!PatchToolboxDialog.CheckFilesBuildingRotationPatchApplied())
                {
                    return false;
                }
            }

            PatchToolboxDialog.flag_BuildingRotationPatchApplied = true;
            buildingRotationCB.Visible = true;
            DisableBuildingRotationPatch("Already applied");

            if (EditorPanels.mapEditor != null && EditorPanels.mapEditor.mapEditorIsReady)
            {
                EditorPanels.mapEditor.RefreshBuildingRotationPatchState();
            }

            return true;
        }

        public void CheckScrcmdRepointPatchApplied()
        {
            if (!PatchToolboxDialog.flag_arm9Expanded && !PatchToolboxDialog.CheckFilesArm9ExpansionApplied())
            {
                DisableScrcmdRepointPatch("ARM9 not expanded!");
                return;
            }

            if (!PatchToolboxLogic.IsScrcmdRepointApplied())
            {
                return;
            }

            repointScrcmdCB.Visible = true;
            DisableScrcmdRepointPatch("Already applied");
        }

        public void CheckExpandedTrainerNamesPatchApplied()
        {
            if (flag_TrainerNamesExpanded)
            {
                DisableTrainerNameExpansionPatch("Already\nApplied");
            }
            else
            {
                if (RomInfo.trainerNameLenOffset < 0)
                {
                    DisableTrainerNameExpansionPatch("Unsupported");
                }
                else
                {
                    if (RomInfo.trainerNameMaxLen > TrainerFile.defaultNameLen)
                    {
                        DisableTrainerNameExpansionPatch("Already\nApplied");
                        PatchToolboxDialog.flag_TrainerNamesExpanded = true;
                    }
                }
            }
        }

        #endregion Patch

        #region Button Actions

        private void SentenceCasePatchButton_Click(object sender, EventArgs e)
        {
            // Apply-logic lives in the shared static PatchToolboxLogic so the Avalonia toolbox runs identical code.
            PatchToolboxLogic.ApplySentenceCasePatch();
        }

        private void BDHCAMPatchButton_Click(object sender, EventArgs e)
        {
            if (PatchToolboxLogic.ApplyBDHCamPatch())
            {
                DisableBDHCamPatch("Already applied");
                BDHCamCB.Visible = true;
            }
        }

        private void BuildingRotationButton_Click(object sender, EventArgs e)
        {
            if (PatchToolboxLogic.ApplyBuildingRotationPatch())
            {
                DisableBuildingRotationPatch("Already applied");
                buildingRotationCB.Visible = true;

                if (EditorPanels.mapEditor != null && EditorPanels.mapEditor.mapEditorIsReady)
                {
                    EditorPanels.mapEditor.RefreshBuildingRotationPatchState();
                }
            }
        }

        private void ApplyItemStandardizeButton_Click(object sender, EventArgs e)
        {
            if (PatchToolboxLogic.ApplyItemStandardizePatch())
            {
                DisableStandardizeItemsPatch("Already applied");
                itemNumbersCB.Visible = true;
            }
        }

        private void ApplyARM9ExpansionButton_Click(object sender, EventArgs e)
        {
            if (PatchToolboxLogic.ApplyARM9ExpansionPatch())
            {
                DisableARM9patch("Already applied");
                arm9patchCB.Visible = true;

                switch (RomInfo.gameFamily)
                {
                    case GameFamilies.Plat:
                    case GameFamilies.HGSS:
                        EnableBDHCamPatchAfterArm9Expansion();
                        break;
                }

                EnableBuildingRotationPatchAfterArm9Expansion();
                EnableScrcmdRepointPatchAfterArm9Expansion();
            }
        }

        private void expandMatrixButton_Click(object sender, EventArgs e)
        {
            if (PatchToolboxLogic.ApplyMatrixExpansionPatch())
            {
                DisableMatrixExpansionPatch("Already applied");
                expandedMatrixCB.Visible = true;
            }
        }

        private void dynamicHeadersButton_Click(object sender, EventArgs e)
        {
            if (PatchToolboxLogic.ApplyDynamicHeadersPatch())
            {
                DisableDynamicHeadersPatch("Already applied");
                dynamicHeadersPatchCB.Visible = true;
            }
        }

        private void disableDynamicTexturesButton_Click(object sender, EventArgs e)
        {
            if (PatchToolboxLogic.ApplyDisableDynamicTexturesPatch())
            {
                DisableKillTextureAnimationsPatch("Already applied");
                disableTextureAnimationsCB.Visible = true;
            }
        }

        private void expandTrainerNamesButton_Click(object sender, EventArgs e)
        {
            if (PatchToolboxLogic.ApplyExpandTrainerNamesPatch())
            {
                DisableTrainerNameExpansionPatch("Already applied");
                expandTrainerNamesCB.Visible = true;
            }
        }

        // Moves the in-game ScrCommands table + count into the synthetic overlay. Apply-logic lives in
        // the shared PatchToolboxLogic (see ApplyScrcmdRepointPatch), so WinForms and Avalonia run
        // identical code — including the synthetic-overlay offset picker.
        private void applyCustomCommands(object sender, EventArgs e)
        {
            if (PatchToolboxLogic.ApplyScrcmdRepointPatch())
            {
                repointScrcmdCB.Visible = true;
                DisableScrcmdRepointPatch("Already applied");
            }
        }

        #endregion Button Actions

        #region Error Messsages

        private void AlreadyApplied()
        {
            MessageBox.Show("This patch has already been applied.", "Can't reapply patch", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        #endregion Error Messsages
    }
}

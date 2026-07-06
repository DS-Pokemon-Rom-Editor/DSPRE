using System;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Windows.Forms;
using System.Linq;
using DSPRE.ROMFiles;
using System.Collections.Generic;
using DSPRE.Resources.ROMToolboxDB;
using DSPRE.Resources;
using static DSPRE.RomInfo;
using System.Threading.Tasks;
using static DSPRE.Resources.ROMToolboxDB.ToolboxDB;
using static NSMBe4.ROM;

namespace DSPRE
{
    public partial class PatchToolboxDialog : Form
    {
        // Patch state lives in the core RomPatchState class; these forward for the many existing call sites.
        public static uint expandedARMfileID { get => RomPatchState.expandedARMfileID; set => RomPatchState.expandedARMfileID = value; }

        public static bool flag_standardizedItems { get => RomPatchState.flag_standardizedItems; private set => RomPatchState.flag_standardizedItems = value; }
        public static bool flag_arm9Expanded { get => RomPatchState.flag_arm9Expanded; private set => RomPatchState.flag_arm9Expanded = value; }
        public static bool flag_BDHCamPatchApplied { get => RomPatchState.flag_BDHCamPatchApplied; private set => RomPatchState.flag_BDHCamPatchApplied = value; }
        public static bool flag_DynamicHeadersPatchApplied { get => RomPatchState.flag_DynamicHeadersPatchApplied; private set => RomPatchState.flag_DynamicHeadersPatchApplied = value; }
        public static bool flag_MatrixExpansionApplied { get => RomPatchState.flag_MatrixExpansionApplied; private set => RomPatchState.flag_MatrixExpansionApplied = value; }

        public static bool flag_MainComboTableRepointed { get => RomPatchState.flag_MainComboTableRepointed; set => RomPatchState.flag_MainComboTableRepointed = value; }
        public static bool flag_TrainerClassBattleTableRepointed { get => RomPatchState.flag_TrainerClassBattleTableRepointed; set => RomPatchState.flag_TrainerClassBattleTableRepointed = value; }
        public static bool flag_PokemonBattleTableRepointed { get => RomPatchState.flag_PokemonBattleTableRepointed; set => RomPatchState.flag_PokemonBattleTableRepointed = value; }
        public static bool flag_TrainerNamesExpanded { get => RomPatchState.flag_TrainerNamesExpanded; set => RomPatchState.flag_TrainerNamesExpanded = value; }

        public static bool overlay1MustBeRestoredFromBackup { get => RomPatchState.overlay1MustBeRestoredFromBackup; private set => RomPatchState.overlay1MustBeRestoredFromBackup = value; }

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

            // The "Repoint Table" button had no handler in the designer — wire it to the shared patch.
            repointScrcmdButton.Click += RepointScrcmdButton_Click;

            CheckStandardizedItems();

            if (ARM9PatchData.arm9ExpansionCodeDB.ContainsKey("branchString" + "_" + RomInfo.gameFamily + "_" + RomInfo.gameLanguage))
            {
                CheckARM9ExpansionApplied();
            }
            else
            {
                DisableARM9patch("Unsupported\nlanguage");
            }

            // BDHCam routine and ScriptCommand repoint patches are only compatible with English and Spanish versions of HGSS and Platinum
            if ( (RomInfo.gameFamily != GameFamilies.HGSS && RomInfo.gameFamily != GameFamilies.Plat) 
                || ( RomInfo.gameLanguage != GameLanguages.English && RomInfo.gameLanguage != GameLanguages.Spanish))
            {
                DisableBDHCamPatch("Unsupported\nlanguage");
                DisableScrcmdRepointPatch("Unsupported\nlanguage");
            }            

            CheckExpandedTrainerNamesPatchApplied();

            switch (RomInfo.gameFamily)
            {
                case GameFamilies.DP:
                    DisableOverlay1patch("Unsupported");
                    DisableDynamicHeadersPatch("Unsupported");
                    DisableMatrixExpansionPatch("Unsupported");
                    DisableScrcmdRepointPatch("Unsupported");
                    DisableKillTextureAnimationsPatch("Unsupported");
                    break;

                case GameFamilies.Plat:
                    DisableOverlay1patch("Unsupported");
                    DisableMatrixExpansionPatch("Unsupported");
                    DisableScrcmdRepointPatch("Unsupported");
                    DisableKillTextureAnimationsPatch("Unsupported");

                    if (RomInfo.gameLanguage == GameLanguages.English || RomInfo.gameLanguage == GameLanguages.Spanish)
                    {
                        CheckBDHCamPatchApplied();
                    }
                    CheckDynamicHeadersPatchApplied();
                    break;

                case GameFamilies.HGSS:
                    if (!OverlayUtils.OverlayTable.IsDefaultCompressed(1))
                    {
                        DisableOverlay1patch("Already applied");
                        overlay1CB.Visible = true;
                    }

                    if (RomInfo.gameLanguage == GameLanguages.English || RomInfo.gameLanguage == GameLanguages.Spanish)
                    {
                        CheckBDHCamPatchApplied();
                        CheckMatrixExpansionApplied();
                        CheckScrcmdRepointPatchApplied();
                    }
                    else
                    {
                        DisableMatrixExpansionPatch("Unsupported\nlanguage");
                        DisableScrcmdRepointPatch("Unsupported\nlanguage");
                    }

                    CheckDynamicHeadersPatchApplied();
                    break;
            }
        }

        #region Patch Disable

        private void DisableOverlay1patch(string reason)
        {
            overlay1uncomprButton.Enabled = false;
            overlay1uncompressedLBL.Enabled = false;
            overlay1patchtextLBL.Enabled = false;
            overlay1uncomprButton.Text = reason;
        }

        private void DisableBDHCamPatch(string reason)
        {
            BDHCamPatchButton.Enabled = false;
            BDHCamPatchLBL.Enabled = false;
            BDHCamPatchTextLBL.Enabled = false;
            BDHCamARM9requiredLBL.Enabled = false;
            BDHCamPatchButton.Text = reason;
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
            scrcmdARM9requiredLBL.Enabled = false;
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
        public static bool CheckFilesMatrixExpansionApplied() => PatchToolboxLogic.CheckFilesMatrixExpansionApplied();
        public static bool CheckScriptsStandardizedItemNumbers() => PatchToolboxLogic.CheckScriptsStandardizedItemNumbers();

        /// <summary>Route the shared apply-logic prompts to WinForms dialogs (this dialog never shows Avalonia UI).</summary>
        private static void UseWinFormsPrompts()
        {
            PatchToolboxLogic.ConfirmYesNo = (msg, title) => MessageBox.Show(msg, title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
            PatchToolboxLogic.ShowInfo = (msg, title) => MessageBox.Show(msg, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            PatchToolboxLogic.ShowError = (msg, title) => MessageBox.Show(msg, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            PatchToolboxLogic.PickCustomCommandFile = () =>
            {
                using (OpenFileDialog of = new OpenFileDialog { Filter = "Custom Script Command File (*.scrcmd)|*.scrcmd" })
                    return of.ShowDialog() == DialogResult.OK ? of.FileName : null;
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
                    BDHCamARM9requiredLBL.Visible = false;
                    BDHCamPatchButton.Enabled = true;
                    BDHCamPatchLBL.Enabled = true;
                    BDHCamPatchTextLBL.Enabled = true;
                    break;
            }

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
                BDHCamARM9requiredLBL.Visible = true;
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

        public void CheckScrcmdRepointPatchApplied()
        {
            //throw new NotImplementedException();
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
            // If the user accepts the "configure Overlay1 uncompressed first" recommendation and it
            // succeeds, refresh the Overlay1 patch UI here (same as the old inline handler call).
            if (PatchToolboxLogic.ApplyBDHCamPatch(() => { DisableOverlay1patch("Already applied"); overlay1CB.Visible = true; }))
            {
                DisableBDHCamPatch("Already applied");
                BDHCamCB.Visible = true;
            }
        }

        private void overlay1uncomprButton_Click(object sender, EventArgs e)
        {
            if (ConfigureOverlay1Uncompressed())
            {
                DisableOverlay1patch("Already applied");
                overlay1CB.Visible = true;
            }
        }

        public static bool ConfigureOverlay1Uncompressed() => PatchToolboxLogic.ConfigureOverlay1Uncompressed();

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
                        BDHCamPatchButton.Text = "Apply Patch";
                        BDHCamPatchButton.Enabled = true;
                        BDHCamPatchLBL.Enabled = true;
                        BDHCamPatchTextLBL.Enabled = true;
                        BDHCamARM9requiredLBL.Visible = false;
                        break;
                }
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

        #region Mikelan's custom commands

        // Repoint the script command table into the expanded ARM9 file. Wired in the constructor
        // (the "Repoint Table" button had no handler before). The apply-logic + custom-command
        // install now live in the shared PatchToolboxLogic (see ApplyScrcmdRepointPatch /
        // InstallCustomScriptCommand), so WinForms and Avalonia run identical code.
        private void RepointScrcmdButton_Click(object sender, EventArgs e)
        {
            if (PatchToolboxLogic.ApplyScrcmdRepointPatch())
            {
                DisableScrcmdRepointPatch("Already applied");
            }
        }

        #endregion Mikelan's custom commands

        #endregion Button Actions

        #region Error Messsages

        private void AlreadyApplied()
        {
            MessageBox.Show("This patch has already been applied.", "Can't reapply patch", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        #endregion Error Messsages
    }
}

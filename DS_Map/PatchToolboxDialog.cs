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
        public static uint expandedARMfileID = ToolboxDB.syntheticOverlayFileNumbersDB[RomInfo.gameFamily];

        public static bool flag_standardizedItems { get; private set; } = false;
        public static bool flag_arm9Expanded { get; private set; } = false;
        public static bool flag_BDHCamPatchApplied { get; private set; } = false;
        public static bool flag_BuildingRotationPatchApplied { get; private set; } = false;
        public static bool flag_DynamicHeadersPatchApplied { get; private set; } = false;
        public static bool flag_MatrixExpansionApplied { get; private set; } = false;

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
            // Reset expandedARMfileID to null to force re-evaluation on next access
            // Note: This is set in the static field initializer which runs once,
            // so we need to update it when game family changes
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

        #region Constructor

        public PatchToolboxDialog()
        {
            InitializeComponent();

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
            bool bdhCamPatchBlockedByProjectFormat = bdhCamPatchSupported && IsHgssLegacyOverlay1BDHCamPatch();
            bool buildingRotationPatchBlockedByProjectFormat = !RomInfo.IsDsRomProject && buildingRotationPatchSupported;

            // ScriptCommand repoint patches are only compatible with English and Spanish versions of HGSS
            if ((RomInfo.gameFamily != GameFamilies.HGSS && RomInfo.gameFamily != GameFamilies.Plat)
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
            else if (buildingRotationPatchSupported)
            {
                CheckBuildingRotationPatchApplied();
            }
            else
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

                    CheckDynamicHeadersPatchApplied();
                    break;
            }
        }

        #region Patch Disable

        private void DisableBuildingRotationPatch(string reason)
        {
            buildingRotationButton.Enabled = false;
            buildingRotationLBL.Enabled = false;
            buildingRotationARM9requiredLBL.Enabled = false;
            buildingRotationTextLBL.Enabled = false;
            buildingRotationButton.Text = reason;
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

        private static bool CheckFilesArm9ExpansionApplied()
        {
            ARM9PatchData data = new ARM9PatchData();

            byte[] branchCode = DSUtils.HexStringToByteArray(data.branchString);
            byte[] branchCodeRead = ARM9.ReadBytes(data.branchOffset, data.branchString.Length / 3 + 1); //Read branchCode
            if (branchCodeRead.Length != branchCode.Length || !branchCodeRead.SequenceEqual(branchCode))
                return false;

            byte[] initCode = DSUtils.HexStringToByteArray(data.initString);
            byte[] initCodeRead = ARM9.ReadBytes(data.initOffset, data.initString.Length / 3 + 1); //Read initCode
            if (initCodeRead.Length != initCode.Length || !initCodeRead.SequenceEqual(initCode))
                return false;

            return true;
        }

        public static bool CheckFilesBDHCamPatchApplied()
        {
            if (!BDHCAMPatchData.SupportsCurrentRom())
            {
                return false;
            }

            BDHCAMPatchData data = new BDHCAMPatchData();
            if (IsHgssLegacyOverlay1Patch(data.overlayNumber))
            {
                return false;
            }

            byte[] branchCode = DSUtils.HexStringToByteArray(data.branchString);
            byte[] branchCodeRead = ARM9.ReadBytes(data.branchOffset, branchCode.Length);

            if (branchCode.Length != branchCodeRead.Length || !branchCode.SequenceEqual(branchCodeRead))
            {
                return false;
            }

            string overlayFilePath = OverlayUtils.GetPath(data.overlayNumber);

            if (!TryGetBDHCamSubroutineOffset(data, out uint subroutineOffset))
            {
                return false;
            }

            byte[] subroutineRead = DSUtils.ReadFromFile(Filesystem.expArmPath, subroutineOffset, data.subroutine.Length);
            if (data.subroutine.Length != subroutineRead.Length || !data.subroutine.SequenceEqual(subroutineRead))
                return false;

            return true;
        }

        private static byte[] BuildBDHCamOverlayTrampoline(uint subroutineOffset, uint entryOffset)
        {
            byte[] trampoline = new byte[8];
            trampoline[0] = 0x00;
            trampoline[1] = 0x4B;
            trampoline[2] = 0x18;
            trampoline[3] = 0x47;
            Array.Copy(BitConverter.GetBytes(synthOverlayLoadAddress + subroutineOffset + entryOffset), 0, trampoline, 4, 4);
            return trampoline;
        }

        private static byte[] BuildThumbBl(uint sourceAddress, uint targetAddress)
        {
            int offset = unchecked((int)(targetAddress - (sourceAddress + 4)));
            ushort first = (ushort)(0xF000 | ((offset >> 12) & 0x07FF));
            ushort second = (ushort)(0xF800 | ((offset >> 1) & 0x07FF));
            return new byte[] {
                (byte)(first & 0xFF),
                (byte)(first >> 8),
                (byte)(second & 0xFF),
                (byte)(second >> 8)
            };
        }

        private static byte[] BuildBuildingRotationPayload(BuildingRotationPatchData data, uint payloadAddress)
        {
            byte[] payload = (byte[])data.payload.Clone();
            byte[] branchBytes = BuildThumbBl(
                payloadAddress + BuildingRotationPatchData.payloadInternalBranchOffset,
                data.rotationMatrixFunctionAddress);
            Array.Copy(branchBytes, 0, payload, BuildingRotationPatchData.payloadInternalBranchOffset, branchBytes.Length);
            return payload;
        }

        private static bool TryGetThumbBlTarget(uint sourceAddress, byte[] branchBytes, out uint targetAddress)
        {
            targetAddress = 0;
            if (branchBytes == null || branchBytes.Length != 4)
            {
                return false;
            }

            ushort first = BitConverter.ToUInt16(branchBytes, 0);
            ushort second = BitConverter.ToUInt16(branchBytes, 2);
            if ((first & 0xF800) != 0xF000 || (second & 0xF800) != 0xF800)
            {
                return false;
            }

            int offset = ((first & 0x07FF) << 12) | ((second & 0x07FF) << 1);
            if ((offset & 0x00400000) != 0)
            {
                offset |= unchecked((int)0xFF800000);
            }

            targetAddress = unchecked((uint)((int)(sourceAddress + 4) + offset));
            return true;
        }

        private static bool TryGetBDHCamSubroutineOffset(BDHCAMPatchData data, out uint subroutineOffset)
        {
            subroutineOffset = 0;

            string overlayFilePath = OverlayUtils.GetPath(data.overlayNumber);
            byte[] trampoline1 = DSUtils.ReadFromFile(overlayFilePath, data.overlayOffset1, 8);
            byte[] trampoline2 = DSUtils.ReadFromFile(overlayFilePath, data.overlayOffset2, 8);

            if (!HasBDHCamTrampolinePrefix(trampoline1) || !HasBDHCamTrampolinePrefix(trampoline2))
            {
                return false;
            }

            uint target1 = BitConverter.ToUInt32(trampoline1, 4);
            uint target2 = BitConverter.ToUInt32(trampoline2, 4);

            if (target1 < synthOverlayLoadAddress + BDHCAMPatchData.overlayEntryOffset1
                || target2 < synthOverlayLoadAddress + BDHCAMPatchData.overlayEntryOffset2)
            {
                return false;
            }

            uint offset1 = target1 - synthOverlayLoadAddress - BDHCAMPatchData.overlayEntryOffset1;
            uint offset2 = target2 - synthOverlayLoadAddress - BDHCAMPatchData.overlayEntryOffset2;

            if (offset1 != offset2)
            {
                return false;
            }

            subroutineOffset = offset1;
            return true;
        }

        private static bool HasBDHCamTrampolinePrefix(byte[] trampoline)
        {
            return trampoline != null
                && trampoline.Length == 8
                && trampoline[0] == 0x00
                && trampoline[1] == 0x4B
                && trampoline[2] == 0x18
                && trampoline[3] == 0x47;
        }

        private static bool IsHgssLegacyOverlay1Patch(byte overlayNumber)
        {
            return RomInfo.gameFamily == GameFamilies.HGSS && !RomInfo.IsDsRomProject && overlayNumber == 1;
        }

        private static bool IsHgssLegacyOverlay1BDHCamPatch()
        {
            return IsHgssLegacyOverlay1Patch(new BDHCAMPatchData().overlayNumber);
        }

        public static bool CheckFilesMatrixExpansionApplied()
        {
            foreach (KeyValuePair<uint[], string> kv in ToolboxDB.matrixExpansionDB)
            {
                foreach (uint offset in kv.Key)
                {
                    int languageOffset = 0;
                    if (RomInfo.romID == "IPKE" || RomInfo.romID == "IPGE" || RomInfo.romID == "IPGS")
                    {
                        languageOffset = +8;
                    }

                    byte[] read = ARM9.ReadBytes((uint)(offset - ARM9.address + languageOffset), kv.Value.Length / 3 + 1);
                    byte[] code = DSUtils.HexStringToByteArray(kv.Value);
                    if (read.Length != code.Length || !read.SequenceEqual(code))
                        return false;
                }
            }
            return true;
        }

        public static bool CheckScriptsStandardizedItemNumbers()
        {
            ScriptFile itemScript = new ScriptFile(RomInfo.itemScriptFileNumber);
            if (itemScript.allScripts.Count - 1 < new TextArchive(RomInfo.itemNamesTextNumber).messages.Count)
            {
                return false;
            }

            for (ushort i = 0; i < itemScript.allScripts.Count - 1; i++)
            {
                if (BitConverter.ToUInt16(itemScript.allScripts[i].commands[0].cmdParams[1], 0) != i || BitConverter.ToUInt16(itemScript.allScripts[i].commands[1].cmdParams[1], 0) != 1)
                {
                    return false;
                }
            }
            return true;
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

        public static bool CheckFilesDynamicHeadersPatchApplied()
        {
            DynamicHeadersPatchData data = new DynamicHeadersPatchData();
            ushort initValue = BitConverter.ToUInt16(ARM9.ReadBytes(data.initOffset, 0x2), 0);
            return initValue == 0xB500;
        }

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

            if (!PatchToolboxDialog.flag_arm9Expanded && !PatchToolboxDialog.CheckFilesArm9ExpansionApplied())
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
            return true;
        }

        public static bool CheckFilesBuildingRotationPatchApplied()
        {
            if (!RomInfo.IsDsRomProject || !BuildingRotationPatchData.SupportsCurrentRom())
            {
                return false;
            }

            BuildingRotationPatchData data = new BuildingRotationPatchData();
            string overlayFilePath = OverlayUtils.GetPath(data.overlayNumber);

            byte[] hookBytes = DSUtils.ReadFromFile(overlayFilePath, data.hookOverlayOffset, 4);
            if (!TryGetThumbBlTarget(data.hookRuntimeAddress, hookBytes, out uint targetAddress))
            {
                return false;
            }

            if (targetAddress < synthOverlayLoadAddress)
            {
                return false;
            }

            uint payloadOffset = targetAddress - synthOverlayLoadAddress;
            if (!File.Exists(Filesystem.expArmPath))
            {
                return false;
            }

            long fileLength = new FileInfo(Filesystem.expArmPath).Length;
            if ((long)payloadOffset + data.payload.Length > fileLength)
            {
                return false;
            }

            byte[] payloadRead = DSUtils.ReadFromFile(Filesystem.expArmPath, payloadOffset, data.payload.Length);
            return payloadRead.SequenceEqual(BuildBuildingRotationPayload(data, targetAddress));
        }

        public void CheckScrcmdRepointPatchApplied()
        {
            if (!PatchToolboxDialog.flag_arm9Expanded && !PatchToolboxDialog.CheckFilesArm9ExpansionApplied())
            {
                scrcmdARM9requiredLBL.Visible = true;
                DisableScrcmdRepointPatch("ARM9 not expanded!");
                return;
            }

            if (GetCommandTableOffset() < 0 || !CheckScrcmdCommandCountPointerValid())
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
            DialogResult d;
            d = MessageBox.Show("Confirming this process will apply the following changes:\n\n" +
                "- Every Pokémon name will be converted to Sentence Case." + "\n\n" +
                "Do you wish to continue?",
                "Confirm to proceed", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (d == DialogResult.Yes)
            {
                Parallel.ForEach(RomInfo.pokemonNamesTextNumbers, ID =>
                {
                    TextArchive pokeName = new TextArchive(ID);
                    Parallel.For(1, pokeName.messages.Count, i =>
                    {
                        if (pokeName.messages[i].Length <= 1)
                        {
                            i++;
                        }

                        pokeName.messages[i] = pokeName.messages[i].Replace(PokeDatabase.System.pokeNames[(ushort)i].ToUpper(), PokeDatabase.System.pokeNames[(ushort)i]);
                    });
                    pokeName.SaveToExpandedDir(ID, showSuccessMessage: false);
                });
                //sentenceCaseCB.Visible = true;
                MessageBox.Show("Pokémon names have been converted to Sentence Case.", "Operation successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("No changes have been made.", "Operation canceled", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BDHCAMPatchButton_Click(object sender, EventArgs e)
        {
            BDHCAMPatchData data = new BDHCAMPatchData();

            using (var offsetDialog = new SyntheticOverlayOffsetDialog(
                "Dynamic Cameras",
                Filesystem.expArmPath,
                BDHCAMPatchData.BDHCamSubroutineOffset,
                data.subroutine,
                synthOverlayLoadAddress))
            {
                if (offsetDialog.ShowDialog(this) != DialogResult.OK)
                {
                    MessageBox.Show("No changes have been made.", "Operation canceled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                uint subroutineOffset = offsetDialog.SelectedOffset;
                byte[] overlayCode1 = BuildBDHCamOverlayTrampoline(subroutineOffset, BDHCAMPatchData.overlayEntryOffset1);
                byte[] overlayCode2 = BuildBDHCamOverlayTrampoline(subroutineOffset, BDHCAMPatchData.overlayEntryOffset2);

                var d2 = MessageBox.Show("This process will apply the following changes:\n\n" +
                "- Backup ARM9 file (arm9.bin" + backupSuffix + " will be created)." + "\n\n" +
                "- Backup Overlay" + data.overlayNumber + " file (overlay" + data.overlayNumber + ".bin" + backupSuffix + " will be created)." + "\n\n" +
                "- Replace " + (data.branchString.Length / 3 + 1) + " bytes of data at arm9 offset 0x" + data.branchOffset.ToString("X") + " with " + '\n' + data.branchString + "\n\n" +
                "- Replace " + overlayCode1.Length + " bytes of data at overlay" + data.overlayNumber + " offset 0x" + data.overlayOffset1.ToString("X") + "." + "\n\n" +
                "- Replace " + overlayCode2.Length + " bytes of data at overlay" + data.overlayNumber + " offset 0x" + data.overlayOffset2.ToString("X") + "." + "\n\n" +
                "- Modify file #" + expandedARMfileID + " inside " + '\n' + RomInfo.gameDirs[DirNames.synthOverlay].unpackedDir + '\n' + "to insert the BDHCAM routine (any data between 0x" + subroutineOffset.ToString("X") + " and 0x" + (subroutineOffset + (uint)data.subroutine.Length - 1).ToString("X") + " will be overwritten)." + "\n\n" +
                "Do you wish to continue?",
                "Confirm to proceed", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (d2 == DialogResult.Yes)
                {
                    File.Copy(RomInfo.arm9Path, RomInfo.arm9Path + backupSuffix, overwrite: true);
                    string overlayBackupPath = OverlayUtils.GetPath(data.overlayNumber);
                    File.Copy(overlayBackupPath, overlayBackupPath + backupSuffix, overwrite: true);

                    try
                    {
                        ARM9.WriteBytes(DSUtils.HexStringToByteArray(data.branchString), data.branchOffset); //Write new branchOffset

                        /* Write to overlayfile */
                        string overlayFilePath = OverlayUtils.GetPath(data.overlayNumber);

                        DSUtils.WriteToFile(overlayFilePath, overlayCode1, data.overlayOffset1); //Write new overlayCode1
                        DSUtils.WriteToFile(overlayFilePath, overlayCode2, data.overlayOffset2); //Write new overlayCode2

                        /*Write Expanded ARM9 File*/
                        DSUtils.WriteToFile(Filesystem.expArmPath, data.subroutine, subroutineOffset);
                    }
                    catch
                    {
                        MessageBox.Show("Operation failed. It is strongly advised that you restore the arm9 and overlay from their respective backups.", "Something went wrong",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    DisableBDHCamPatch("Already applied");
                    PatchToolboxDialog.flag_BDHCamPatchApplied = true;
                    BDHCamCB.Visible = true;

                    MessageBox.Show(
                        "The Dynamic Cameras patch has been applied.\n\n" +
                        "Synthetic overlay offset: 0x" + subroutineOffset.ToString("X"),
                        "Operation successful.",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("No changes have been made.", "Operation canceled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void BuildingRotationButton_Click(object sender, EventArgs e)
        {
            if (!CheckARM9ExpansionApplied())
            {
                MessageBox.Show("Apply the ARM9 Expansion patch before applying the building rotation patch.", "ARM9 Expansion Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                DisableBuildingRotationPatch("ARM9 not expanded!");
                return;
            }

            BuildingRotationPatchData data = new BuildingRotationPatchData();

            using (var offsetDialog = new SyntheticOverlayOffsetDialog(
                "Building rotation routine",
                Filesystem.expArmPath,
                data.defaultPayloadOffset,
                data.payload,
                synthOverlayLoadAddress))
            {
                if (offsetDialog.ShowDialog(this) != DialogResult.OK)
                {
                    MessageBox.Show("No changes have been made.", "Operation canceled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                uint payloadOffset = offsetDialog.SelectedOffset;
                uint payloadAddress = synthOverlayLoadAddress + payloadOffset;
                byte[] branchBytes = BuildThumbBl(data.hookRuntimeAddress, payloadAddress);
                byte[] payloadBytes = BuildBuildingRotationPayload(data, payloadAddress);

                DialogResult result = MessageBox.Show("This process will apply the following changes:\n\n" +
                    "- Backup Overlay " + data.overlayNumber + " file (overlay" + data.overlayNumber + ".bin" + backupSuffix + " will be created).\n\n" +
                    "- Replace 4 bytes at Overlay " + data.overlayNumber + " offset 0x" + data.hookOverlayOffset.ToString("X") + " with a branch to the building rotation routine.\n\n" +
                    "- Modify file #" + expandedARMfileID + " inside " + '\n' + RomInfo.gameDirs[DirNames.synthOverlay].unpackedDir + '\n' +
                    "to insert the building rotation routine at offset 0x" + payloadOffset.ToString("X") + " (runtime address 0x" + payloadAddress.ToString("X8") + ").\n\n" +
                    "This enables the existing building rotation values to be used when placing buildings.\n\n" +
                    "Do you wish to continue?",
                    "Confirm to proceed", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                {
                    MessageBox.Show("No changes have been made.", "Operation canceled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                string overlayFilePath = OverlayUtils.GetPath(data.overlayNumber);
                File.Copy(overlayFilePath, overlayFilePath + backupSuffix, overwrite: true);

                try
                {
                    DSUtils.WriteToFile(overlayFilePath, branchBytes, data.hookOverlayOffset);
                    DSUtils.WriteToFile(Filesystem.expArmPath, payloadBytes, payloadOffset);
                }
                catch
                {
                    MessageBox.Show("Operation failed. It is strongly advised that you restore the Overlay " + data.overlayNumber + " backup.", "Something went wrong",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                DisableBuildingRotationPatch("Already applied");
                PatchToolboxDialog.flag_BuildingRotationPatchApplied = true;
                buildingRotationCB.Visible = true;
                if (EditorPanels.mapEditor.mapEditorIsReady)
                {
                    EditorPanels.mapEditor.RefreshBuildingRotationPatchState();
                }

                MessageBox.Show(
                    "The building rotation patch has been applied.\n\n" +
                    "Synthetic overlay offset: 0x" + payloadOffset.ToString("X"),
                    "Operation successful.",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void ApplyItemStandardizeButton_Click(object sender, EventArgs e)
        {
            DialogResult d = MessageBox.Show("This process will apply the following changes:\n\n" +
                "- Item scripts will be rearranged to follow the natural, ascending index order.\n\n" +
                "- Any unsaved change to the current Event File will be discarded.\n\n",
                "Confirm to proceed", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (d == DialogResult.Yes)
            {
                DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.scripts });
                DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.eventFiles });

                if (PatchToolboxDialog.flag_standardizedItems)
                {
                    AlreadyApplied();
                }
                else
                {
                    // Load item script file data
                    ScriptFile itemScriptFile = new ScriptFile(RomInfo.itemScriptFileNumber);

                    // Create map for: script no. -> vanilla item
                    int[] vanillaItemsArray = new int[itemScriptFile.allScripts.Count - 1];

                    for (int i = 0; i < itemScriptFile.allScripts.Count - 1; i++)
                    {
                        vanillaItemsArray[i] = BitConverter.ToInt16(itemScriptFile.allScripts[i].commands[0].cmdParams[1], 0);
                    }
                    ;

                    // Parse all event files and fix instances of ground items according to the new order
                    int cnt = Filesystem.GetEventFileCount();
                    (int itemScrMin, int itemScrMax) = (7000, 8000);

                    for (int i = 0; i < cnt; i++)
                    {
                        bool dirty = false;

                        EventFile eventFile = new EventFile(i);

                        for (int j = 0; j < eventFile.overworlds.Count; j++)
                        {
                            // If ow is marked as an item, or in the rare case it is not but script no. falls within item script range:
                            bool isItem = eventFile.overworlds[j].type == (ushort)Overworld.OwType.ITEM
                                          || (eventFile.overworlds[j].scriptNumber >= itemScrMin
                                          && eventFile.overworlds[j].scriptNumber <= itemScrMax);

                            if (isItem)
                            {
                                int itemScriptID = eventFile.overworlds[j].scriptNumber - (itemScrMin - 1);
                                eventFile.overworlds[j].scriptNumber = (ushort)(itemScrMin + vanillaItemsArray[itemScriptID - 1]);
                                dirty = true;
                            }
                        }

                        // Save event file
                        if (dirty)
                        {
                            eventFile.SaveToFileDefaultDir(i, showSuccessMessage: false);
                        }
                    };

                    //Distortion world - turnback cave Griseous Orb fix
                    if (gameFamily.Equals(GameFamilies.Plat))
                    {
                        string ow9path = OverlayUtils.GetPath(9);
                        int ow9offs = 0x8E20 + 10;

                        int itemScriptID;

                        using (DSUtils.EasyReader ewr = new DSUtils.EasyReader(ow9path, ow9offs))
                        {
                            itemScriptID = ewr.ReadUInt16() - (itemScrMin - 1);
                        }

                        using (DSUtils.EasyWriter ewr = new DSUtils.EasyWriter(ow9path, ow9offs))
                        {
                            ewr.Write((ushort)(itemScrMin + vanillaItemsArray[itemScriptID - 1]));
                        }
                    }

                    // Sort scripts in the Script File according to item indices
                    int itemCount = new TextArchive(RomInfo.itemNamesTextNumber).messages.Count;
                    ScriptCommandContainer executeGive = new ScriptCommandContainer((uint)itemCount + 1, itemScriptFile.allScripts[itemScriptFile.allScripts.Count - 1]);

                    itemScriptFile.allScripts.Clear();

                    for (ushort i = 0; i < itemCount; i++)
                    {
                        List<ScriptCommand> cmdList = new List<ScriptCommand> {
                            new ScriptCommand("SetVar 0x8008 " + i),
                            new ScriptCommand("SetVar 0x8009 0x1"),
                            new ScriptCommand("Jump Function_#1")
                        };

                        itemScriptFile.allScripts.Add(new ScriptCommandContainer((ushort)(i + 1), ScriptFile.ContainerTypes.Script, commandList: cmdList));
                    }

                    itemScriptFile.allScripts.Add(executeGive);
                    itemScriptFile.allFunctions[0].usedScriptID = itemCount + 1;

                    itemScriptFile.SaveToFileDefaultDir(RomInfo.itemScriptFileNumber, showSuccessMessage: false);
                    MessageBox.Show("Operation successful.", "Process completed.", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    DisableStandardizeItemsPatch("Already applied");

                    itemNumbersCB.Visible = true;
                    PatchToolboxDialog.flag_standardizedItems = true;
                }
            }
            else
            {
                MessageBox.Show("No changes have been made.", "Operation canceled", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void ApplyARM9ExpansionButton_Click(object sender, EventArgs e)
        {
            // TODO: Check the languages studff
            ARM9PatchData data = new ARM9PatchData();

            DialogResult d = MessageBox.Show("Confirming this process will apply the following changes:\n\n" +
                    "- Backup ARM9 file (arm9.bin" + backupSuffix + " will be created)." + "\n\n" +
                    "- Replace " + (data.branchString.Length / 3 + 1) + " bytes of data at arm9 offset 0x" + data.branchOffset.ToString("X") + " with " + '\n' + data.branchString + "\n\n" +
                    "- Replace " + (data.initString.Length / 3 + 1) + " bytes of data at arm9 offset 0x" + data.initOffset.ToString("X") + " with " + '\n' + data.initString + "\n\n" +
                    "- Modify file #" + expandedARMfileID + " inside " + '\n' + RomInfo.gameDirs[DirNames.synthOverlay].unpackedDir + '\n' + " to accommodate for 88KB of data (no backup)." + "\n\n" +
                    "If you do not understand the implications of these changes and how they can affect your game do NOT continue. You can and will break the game if you do not know what you are doing here.\n\n" +
                    "Do you wish to continue?",
                    "Confirm to proceed", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);


            if (d == DialogResult.Yes)
            {
                File.Copy(RomInfo.arm9Path, RomInfo.arm9Path + backupSuffix, overwrite: true);

                try
                {
                    ARM9.WriteBytes(DSUtils.HexStringToByteArray(data.branchString), data.branchOffset); //Write new branchOffset
                    ARM9.WriteBytes(DSUtils.HexStringToByteArray(data.initString), data.initOffset); //Write new initOffset

                    string fullFilePath = Filesystem.expArmPath;

                    // Do a file size check first just in case the file is already expanded so we don't nuke existing data
                    if (File.Exists(fullFilePath))
                    {
                        FileInfo fi = new FileInfo(fullFilePath);
                        if (fi.Length >= 0x16000)
                        {
                            MessageBox.Show("The synthetic Overlay already exists. " +
                                "This may be due to a previous application of the ARM9 expansion patch. " +
                                "No changes have been made to the file to avoid data loss.\n\n" +
                                "Double check to make sure this is correct!", "Synthetic Overlay Exists", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            File.Delete(fullFilePath);
                            using (BinaryWriter f = new BinaryWriter(File.Create(fullFilePath)))
                            {
                                for (int i = 0; i < 0x16000; i++)
                                    f.Write((byte)0x00);
                            }
                        }
                    }                    

                    DisableARM9patch("Already applied");
                    arm9patchCB.Visible = true;
                    PatchToolboxDialog.flag_arm9Expanded = true;

                    switch (RomInfo.gameFamily)
                    {
                        case GameFamilies.Plat:
                        case GameFamilies.HGSS:
                            if (BDHCAMPatchData.SupportsCurrentRom() && !IsHgssLegacyOverlay1BDHCamPatch())
                            {
                                BDHCamPatchButton.Text = "Apply Patch";
                                BDHCamPatchButton.Enabled = true;
                                BDHCamPatchLBL.Enabled = true;
                                BDHCamPatchTextLBL.Enabled = true;
                                BDHCamARM9requiredLBL.Visible = false;
                            }
                            break;
                    }

                    if (RomInfo.IsDsRomProject && BuildingRotationPatchData.SupportsCurrentRom() && !CheckFilesBuildingRotationPatchApplied())
                    {
                        buildingRotationButton.Text = "Apply Patch";
                        buildingRotationButton.Enabled = true;
                        buildingRotationLBL.Enabled = true;
                        buildingRotationARM9requiredLBL.Enabled = true;
                        buildingRotationTextLBL.Enabled = true;
                    }

                    if (RomInfo.gameFamily == GameFamilies.HGSS
                        && (RomInfo.gameLanguage == GameLanguages.English || RomInfo.gameLanguage == GameLanguages.Spanish))
                    {
                        repointScrcmdButton.Text = "Apply Patch";
                        repointScrcmdButton.Enabled = true;
                        repointScrcmdLBL.Enabled = true;
                        repointScrcmdTextLBL.Enabled = true;
                        scrcmdARM9requiredLBL.Visible = false;
                    }

                    MessageBox.Show("The ARM9's usable memory has been expanded.", "Operation successful.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch
                {
                    MessageBox.Show("Operation failed. It is strongly advised that you restore the arm9 backup (arm9.bin" + backupSuffix + ").", "Something went wrong",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                if (d != DialogResult.OK)
                {
                    MessageBox.Show("No changes have been made.", "Operation canceled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }                    
            }
        }

        private void expandMatrixButton_Click(object sender, EventArgs e)
        {
            string listOfChanges = "";
            int languageOffset = 0;

            if (RomInfo.romID == "IPKE" || RomInfo.romID == "IPGE" || RomInfo.romID == "IPGS")
            {
                languageOffset = +8;
            }

            foreach (KeyValuePair<uint[], string> kv in ToolboxDB.matrixExpansionDB)
            {
                listOfChanges += " - Replace " + (kv.Value.Length / 3 + 1) + " bytes of data at arm9 offset";
                if (kv.Key.Length > 1)
                    listOfChanges += "s";

                for (int i = 0; i < kv.Key.Length; i++)
                {
                    listOfChanges += " 0x" + (kv.Key[i] - ARM9.address + languageOffset).ToString("X");

                    if (i < kv.Key.Length - 1)
                        listOfChanges += ",";
                }
                listOfChanges += " with " + '\n' + kv.Value + "\n\n";
            }

            DialogResult d;
            d = MessageBox.Show("Confirming this process will apply the following changes:\n\n" +
                listOfChanges +
                "Do you wish to continue?",
                "Confirm to proceed", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (d == DialogResult.Yes)
            {
                try
                {
                    foreach (KeyValuePair<uint[], string> kv in ToolboxDB.matrixExpansionDB)
                    {
                        foreach (uint offset in kv.Key)
                        {
                            ARM9.WriteBytes(DSUtils.HexStringToByteArray(kv.Value), (uint)(offset - ARM9.address + languageOffset));
                        }
                    }
                }
                catch
                {
                    MessageBox.Show("Operation failed. It is strongly advised that you restore the arm9 backup (arm9.bin" + backupSuffix + ").", "Something went wrong",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                DisableMatrixExpansionPatch("Already applied");
                expandedMatrixCB.Visible = true;
                PatchToolboxDialog.flag_MatrixExpansionApplied = true;
                MessageBox.Show("Matrix 0 can now be freely expanded up to twice its size.", "Operation successful.", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("No changes have been made.", "Operation canceled", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void dynamicHeadersButton_Click(object sender, EventArgs e)
        {
            DynamicHeadersPatchData data = new DynamicHeadersPatchData();
            var headersDir = RomInfo.gameDirs[DirNames.dynamicHeaders];

            bool specialCase = RomInfo.gameFamily == GameFamilies.HGSS && RomInfo.gameLanguage != GameLanguages.Japanese && RomInfo.gameLanguage != GameLanguages.Spanish;
            string specialCaseChanges = "";

            if (specialCase)
            {
                specialCaseChanges = "- Replace " + (data.specialCaseData1.Length / 3 + 1) + " bytes of data at arm9 offset 0x" + (data.specialCaseOffset1 + data.pointerDiff).ToString("X") + " with " + '\n' + data.specialCaseData1 + "\n\n" +
                    "- Replace " + (data.specialCaseData2.Length / 3 + 1) + " bytes of data at arm9 offset 0x" + (data.specialCaseOffset2 + data.pointerDiff).ToString("X") + " with " + '\n' + data.specialCaseData2 + "\n\n" +
                    "- Replace " + (data.specialCaseData3.Length / 3 + 1) + " bytes of data at arm9 offset 0x" + (data.specialCaseOffset3 + data.pointerDiff).ToString("X") + " with " + '\n' + data.specialCaseData3 + "\n\n";
            }

            DialogResult d;
            d = MessageBox.Show("Confirming this process will apply the following changes:\n\n" +
                "- Backup ARM9 file (arm9.bin" + backupSuffix + " will be created)." + "\n\n" +
                "- NARC file at " + headersDir.packedDir + " will become the new header container." + "\n\n" +
                "- The default ARM9 header table will be split into multiple files (one per header), each one saved into NARC" + headersDir.packedDir + " upon saving the ROM." + "\n\n" +
                "- Replace " + (data.initString.Length / 3 + 1) + " bytes of data at arm9 offset 0x" + data.initOffset.ToString("X") + " with " + '\n' + data.initString + "\n\n" +
                "- Neutralize instances of (HeaderID * 0x18) so the base offset which the data is read from is always 0x0." + "\n\n" +
                "- Change pointers to header fields, from(ARM9_HEADER_TABLE_OFFSET + n) to simply(0 + n)" + "\n\n" +
                specialCaseChanges +
                "Do you wish to continue?",
                "Confirm to proceed", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (d == DialogResult.Yes)
            {
                File.Copy(RomInfo.arm9Path, RomInfo.arm9Path + backupSuffix, overwrite: true);

                try
                {
                    /* Write main routine (HG USA):

                     00 B5		        push (lr)
                     01 1C		        mov r1, r0
                     32 20*		        mov r0, #0x32
                     00 22		        mov r2, #0x0
                     CC F7 58 F9**	    bl 0x02007524	@Load_Memory
                     03 1C		        mov r3, r0
                     DF F7 49 FC**	    bl 0x0201AB0C	@Free_Memory
                     00 BD		        pop, pc

                    *FOR PLATINUM (all languages):
                     94 20		        mov r0, #0x94

                    **BRANCHES FOR OTHER VERSIONS/LANGUAGES:

                     HG ESP (IPKS):
                     CC F7 5C F9	    bl 0x02007524	@Load_Memory
                     DF F7 4D FC	    bl 0x0201AB0C	@Free_Memory

                     HG JAP (IPKJ) and SS JAP (IPGJ):
                     CC F7 08 FB	    bl 0x0200743C	@Load_Memory
                     DF F7 C7 FC	    bl 0x0201A7C0	@Free_Memory

                     Plat USA (CPUE):
                     CC F7 48 FD	    bl 0x02006AC0	@Load_Memory
                     DE F7 C7 F8	    bl 0x020181C4	@Free_Memory

                     Plat ESP (CPUS), ITA (CPUI), FRA (CPUF), GER (CPUD):
                     CC F7 00 FD	    bl 0x02006AD4	@Load_Memory
                     CC F7 74 FC	    bl 0x02018234	@Free_Memory

                     Plat JAP (CPUJ):
                     CC F7 0A FF	    bl 0x02006A00	@Load_Memory
                     DE F7 3D F9	    bl 0x02017E6C	@Free_Memory
                     */

                    ARM9.WriteBytes(DSUtils.HexStringToByteArray(data.initString), data.initOffset);

                    /* - Neutralize instances of (HeaderID * 0x18) so the base offset which the data is read from is always 0x0:

                            Replace this:
                            18 21       mov r1, #0x18
                            41 43       mul r1, r0

                            with this:
                            19 00       lsl r1, r3, 0
                            C0 46       nop

                      - Change pointers to header fields, from (ARM9_HEADER_TABLE_OFFSET + n) to simply (0 + n)

                       * for ESP HG (IPKS): subtract 0x8 from every reference offset
                       * for JAP HG (IPKJ) and SS (IPGJ): subtract 0x448 from every reference offset
                       * for Plat ESP, ITA, FRA, GER, JAP: add 0xA4 to every reference offset
                       * for Plat JAP: subtract 0x444 from every reference offset

                     */

                    foreach (Tuple<uint, uint> reference in DynamicHeadersPatchData.dynamicHeadersPointersDB[RomInfo.gameFamily])
                    {
                        ARM9.WriteBytes(DSUtils.HexStringToByteArray(data.REFERENCE_STRING), (uint)(reference.Item1 + data.pointerDiff));
                        uint pointerValue = BitConverter.ToUInt32(ARM9.ReadBytes((uint)(reference.Item2 + data.pointerDiff), 4), 0) - RomInfo.headerTableOffset - ARM9.address;
                        ARM9.WriteBytes(BitConverter.GetBytes(pointerValue), (uint)(reference.Item2 + data.pointerDiff));
                    }

                    if (specialCase)
                    {
                        /*  Special case: at 0x3B522 (non-JAP and non-Spanish HG offset) there is an instruction
                            between the (mov r1, #0x18) and (mul r1, r0) commands, so we must handle this separately */

                        ARM9.WriteBytes(DSUtils.HexStringToByteArray(data.specialCaseData1), (uint)(data.specialCaseOffset1 + data.pointerDiff));
                        ARM9.WriteBytes(DSUtils.HexStringToByteArray(data.specialCaseData2), (uint)(data.specialCaseOffset2 + data.pointerDiff));
                        ARM9.WriteBytes(DSUtils.HexStringToByteArray(data.specialCaseData3), (uint)(data.specialCaseOffset3 + data.pointerDiff));
                    }

                    // Clear the dynamic headers directory in 'unpacked'
                    Directory.Delete(headersDir.unpackedDir, true);
                    Directory.CreateDirectory(headersDir.unpackedDir);

                    /* Now move the headers data from arm9 to the new directory. Upon saving the ROM,
                       the data will be packed into a NARC and replace a/0/5/0 in HGSS or
                       debug/cb_edit/d_test.narc in Platinum */

                    int headerCount = RomInfo.GetHeaderCount();
                    for (int i = 0; i < headerCount; i++)
                    {
                        byte[] headerData = MapHeader.LoadFromARM9((ushort)i).ToByteArray();
                        DSUtils.WriteToFile(headersDir.unpackedDir + "\\" + i.ToString("D4"), headerData);
                    }

                    DisableDynamicHeadersPatch("Already applied");
                    dynamicHeadersPatchCB.Visible = true;
                    PatchToolboxDialog.flag_DynamicHeadersPatchApplied = true;

                    MessageBox.Show("The headers are now dynamically allocated in memory.", "Operation successful.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch
                {
                    MessageBox.Show("Operation failed. It is strongly advised that you restore the arm9 backup (arm9.bin" + backupSuffix + ").", "Something went wrong",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("No changes have been made.", "Operation canceled", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void disableDynamicTexturesButton_Click(object sender, EventArgs e)
        {
            DialogResult d;
            d = MessageBox.Show("Applying this patch will set the Dynamic Textures field of all AreaData files to 0xFFFF.\n\n" +
                "Are you sure you want to proceed?",
                "Confirm to proceed", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (d == DialogResult.Yes)
            {
                DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { DirNames.areaData });

                string[] adFiles = Directory.GetFiles(gameDirs[DirNames.areaData].unpackedDir);
                foreach (string s in adFiles)
                {
                    AreaData a = new AreaData(new FileStream(s, FileMode.Open))
                    {
                        dynamicTextureType = 0xFFFF
                    };
                    a.SaveToFile(s, showSuccessMessage: false);
                }

                DisableKillTextureAnimationsPatch("Already applied");
                disableTextureAnimationsCB.Visible = true;
                MessageBox.Show("Texture Animations have been disabled in every AreaData.", "Operation successful.", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("No changes have been made.", "Operation canceled", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void expandTrainerNamesButton_Click(object sender, EventArgs e)
        {
            // Pearl        USA     ARM9 at 0x6AC32     // TODO: Verify
            // Pearl        Spain   ARM9 at 0x6AC8E     // TODO: Verify
            // Diamond      USA     ARM9 at 0x6AC32
            // Diamond      Spain   ARM9 at 0x6AC8E
            // Platinum     USA     ARM9 at 0x791DE
            // Platinum     Spain   ARM9 at 0x7927E
            // HeartGold    USA     ARM9 at 0x7342E
            // HeartGold    Spain   ARM9 at 0x73426
            // SoulSilver   USA     ARM9 at 0x7342E
            // SoulSilver   Spain   ARM9 at 0x7342E     // TODO: Verify

            DialogResult d = MessageBox.Show($"Applying this patch will set the Trainer Name max length to {PatchToolboxDialog.expandedTrainerNameLength - 1} usable characters.\n" +
                "Are you sure you want to proceed?",
                "Confirm to proceed", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (d == DialogResult.Yes)
            {
                try
                {
                    using (ARM9.Writer wr = new ARM9.Writer(RomInfo.trainerNameLenOffset))
                    {
                        wr.Write((byte)PatchToolboxDialog.expandedTrainerNameLength);
                    }

                    PatchToolboxDialog.flag_TrainerNamesExpanded = true;
                    DisableTrainerNameExpansionPatch("Already applied");
                    expandTrainerNamesCB.Visible = true;
                    MessageBox.Show("Trainer Names have been extended.", "Operation successful.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (IOException)
                {
                    MessageBox.Show("ARM9 could not be written.", "Operation canceled", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("No changes have been made.", "Operation canceled", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        #region ScrCommands table repoint patch

        private const uint ScrcmdBlockDefaultOffset = 0x200;
        private const uint ScrcmdOriginalCommandCount = 0x355;
        private const int ScrcmdOriginalTableLength = (int)(4 * ScrcmdOriginalCommandCount);
        private const uint ScrcmdCountOffsetInBlock = 0x04;
        private const uint ScrcmdTableMarkerOffsetInBlock = 0x08;
        private const uint ScrcmdTableOffsetInBlock = 0x0C;
        private const uint ScrcmdCountMarker = 0x4E554F43; // "COUN"
        private const uint ScrcmdTableMarker = 0x4C424154; // "TABL"

        private void applyCustomCommands(object sender, EventArgs e)
        {
            int expTableOffset = GetCommandTableOffset();

            if (expTableOffset >= 0)
            {
                AlreadyApplied();
                return;
            }

            byte[] commandTablePayload = BuildCommandTablePayload();
            string expandedPath = RomInfo.gameDirs[DirNames.synthOverlay].unpackedDir + "\\0000";
            uint blockOffset;
            using (SyntheticOverlayOffsetDialog offsetDialog = new SyntheticOverlayOffsetDialog(
                "Script command table block",
                expandedPath,
                ScrcmdBlockDefaultOffset,
                commandTablePayload,
                synthOverlayLoadAddress))
            {
                if (offsetDialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                blockOffset = offsetDialog.SelectedOffset;
                DialogResult result = MessageBox.Show(
                    "This process will apply the following changes:\n\n" +
                    "- Backup ARM9 file (arm9.bin" + backupSuffix + " will be created).\n\n" +
                    "- Write the moved ScrCommands block to synthetic overlay offset 0x" + blockOffset.ToString("X") + ".\n\n" +
                    "- Update the ARM9 ScrCommands table pointer.\n\n" +
                    "- Update the ARM9 ScrCommands count pointer.\n\n" +
                    "Do you wish to continue?",
                    "Confirm to proceed",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                {
                    MessageBox.Show("No changes have been made.", "Operation canceled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                File.Copy(RomInfo.arm9Path, RomInfo.arm9Path + backupSuffix, overwrite: true);
                RepointCommandTable(blockOffset, commandTablePayload);
            }

            repointScrcmdCB.Visible = true;
            DisableScrcmdRepointPatch("Already applied");

            MessageBox.Show(
                "The ScrCommands table patch has been applied.\n\n" +
                "This does not add new commands or update DSPRE's JSON script-command metadata.\n\n" +
                "Synthetic overlay offset: 0x" + blockOffset.ToString("X") +
                " (count: 0x" + (blockOffset + ScrcmdCountOffsetInBlock).ToString("X") +
                ", table: 0x" + (blockOffset + ScrcmdTableOffsetInBlock).ToString("X") + ")",
                "ScrCommands Table Moved",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private int GetCommandTableOffset()
        { // Checks if command table is repointed IN THE EXPANDED ARM9 FILE, returns pointer inside this file
            try
            {
                int pointerOffset = GetCustomScrcmdDBInt("pointerOffset");
                using (ARM9.Reader r = new ARM9.Reader(pointerOffset))
                {
                    uint cmdTable = r.ReadUInt32();
                    if (cmdTable < synthOverlayLoadAddress)
                    {
                        return -1;
                    }

                    uint offset = cmdTable - synthOverlayLoadAddress;
                    if (File.Exists(Filesystem.expArmPath))
                    {
                        long fileLength = new FileInfo(Filesystem.expArmPath).Length;
                        if (offset >= ScrcmdTableOffsetInBlock &&
                            (long)offset + ScrcmdOriginalTableLength <= fileLength &&
                            CheckScrcmdBlockMarkers((int)(offset - ScrcmdTableOffsetInBlock)))
                        {
                            return (int)offset; // Table position inside the expanded arm9 file
                        }
                    }
                }
            } catch {
                return -1;
            }
            return -1; // No table in expanded arm9 file
        }

        private bool CheckScrcmdCommandCountPointerValid()
        {
            try
            {
                int tableOffset = GetCommandTableOffset();
                if (tableOffset < 0)
                {
                    return false;
                }

                uint expectedCountPointer = synthOverlayLoadAddress + (uint)tableOffset - ScrcmdTableOffsetInBlock + ScrcmdCountOffsetInBlock;
                using (ARM9.Reader r = new ARM9.Reader(GetCustomScrcmdDBInt("commandCountPointerOffset")))
                {
                    return r.ReadUInt32() == expectedCountPointer;
                }
            } catch {
                return false;
            }
        }

        private bool CheckScrcmdBlockMarkers(int blockOffset)
        {
            string expandedPath = RomInfo.gameDirs[DirNames.synthOverlay].unpackedDir + "\\0000";
            if (!File.Exists(expandedPath))
            {
                return false;
            }

            using (BinaryReader reader = new BinaryReader(new FileStream(expandedPath, FileMode.Open, FileAccess.Read)))
            {
                if (blockOffset < 0 || blockOffset + (long)ScrcmdTableOffsetInBlock > reader.BaseStream.Length)
                {
                    return false;
                }

                reader.BaseStream.Position = blockOffset;
                uint countMarker = reader.ReadUInt32();
                reader.BaseStream.Position = blockOffset + (long)ScrcmdTableMarkerOffsetInBlock;
                uint tableMarker = reader.ReadUInt32();
                return countMarker == ScrcmdCountMarker && tableMarker == ScrcmdTableMarker;
            }
        }

        private static int GetCustomScrcmdDBInt(string keyPrefix)
        {
            ResourceManager customcmdDB = new ResourceManager("DSPRE.Resources.ROMToolboxDB.CustomScrCmdDB", Assembly.GetExecutingAssembly());
            string value = customcmdDB.GetString(keyPrefix + "_" + RomInfo.gameVersion + "_" + RomInfo.gameLanguage);
            if (value == null)
            {
                throw new NotSupportedException();
            }

            return int.Parse(value);
        }

        private byte[] BuildCommandTablePayload()
        {
            byte[] originalTable = DSUtils.ReadFromFile(RomInfo.arm9Path, GetCustomScrcmdDBInt("originalTableOffset"), ScrcmdOriginalTableLength);
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(ScrcmdCountMarker);
                writer.Write(ReadScrcmdCommandCount());
                writer.Write(ScrcmdTableMarker);
                writer.Write(originalTable);
                return stream.ToArray();
            }
        }

        private uint ReadScrcmdCommandCount()
        {
            using (ARM9.Reader reader = new ARM9.Reader(GetCustomScrcmdDBInt("commandCountOffset")))
            {
                return reader.ReadUInt32();
            }
        }

        private void RepointCommandTable(uint blockOffset, byte[] commandTablePayload)
        {
            string expandedPath = RomInfo.gameDirs[DirNames.synthOverlay].unpackedDir + "\\0000";

            using (BinaryWriter expArmWriter = new BinaryWriter(new FileStream(expandedPath, FileMode.Open)))
            {
                expArmWriter.BaseStream.Position = blockOffset;
                expArmWriter.Write(commandTablePayload);
            }

            WriteCommandTablePointer(blockOffset + ScrcmdTableOffsetInBlock);
            WriteCommandCountPointer(blockOffset + ScrcmdCountOffsetInBlock);
        }

        private void WriteCommandTablePointer(uint tableOffset)
        {
            using (ARM9.Writer wr = new ARM9.Writer())
            {
                wr.BaseStream.Position = GetCustomScrcmdDBInt("pointerOffset");
                wr.Write(synthOverlayLoadAddress + tableOffset);
            }
        }

        private void WriteCommandCountPointer(uint countOffset)
        {
            using (ARM9.Writer wr = new ARM9.Writer())
            {
                wr.BaseStream.Position = GetCustomScrcmdDBInt("commandCountPointerOffset");
                wr.Write(synthOverlayLoadAddress + countOffset);
            }
        }

        #endregion ScrCommands table repoint patch

        #endregion Button Actions

        #region Error Messsages

        private void AlreadyApplied()
        {
            MessageBox.Show("This patch has already been applied.", "Can't reapply patch", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        #endregion Error Messsages
    }
}

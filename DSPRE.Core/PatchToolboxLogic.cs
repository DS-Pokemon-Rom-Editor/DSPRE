using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Threading.Tasks;
using DSPRE.ROMFiles;
using DSPRE.Resources;
using DSPRE.Resources.ROMToolboxDB;
using static DSPRE.RomInfo;
using static DSPRE.Resources.ROMToolboxDB.ToolboxDB;
using System.Text;
using System.Globalization;

namespace DSPRE
{
    /// <summary>
    /// Shared, UI-agnostic apply-logic for the ROM Patch Toolbox: both the WinForms
    /// <see cref="PatchToolboxDialog"/> and the native Avalonia Patch Toolbox call byte-for-byte
    /// identical patch code (no ROM-writing divergence). Core, no UI-toolkit dependency.
    ///
    /// All user prompts go through the pluggable <see cref="ConfirmYesNo"/> / <see cref="ShowInfo"/> /
    /// <see cref="ShowError"/> / <see cref="PickSyntheticOverlayOffset"/> hooks (defaults route through
    /// <see cref="AppMessages"/>; each shell installs its own dialogs, WinForms via
    /// <c>PatchToolboxDialog.UseWinFormsPrompts()</c>, Avalonia via <c>PatchDialogs.Install()</c>).
    /// The methods set the shared <see cref="RomPatchState"/> flags and return whether the patch was
    /// applied so each shell can refresh its own button/status UI.
    /// </summary>
    public static class PatchToolboxLogic
    {
        private const string BackupSuffix = ".backup";

        // ── Prompt hooks (pluggable so each shell shows native dialogs) ───────────────────────────
        /// <summary>Yes/No confirmation. Returns true for Yes. Default = <see cref="AppMessages"/>.</summary>
        public static Func<string, string, bool> ConfirmYesNo = (msg, title) => AppMessages.Confirm(msg, title);
        /// <summary>Informational message. Default = <see cref="AppMessages"/>.</summary>
        public static Action<string, string> ShowInfo = (msg, title) => AppMessages.Info(msg, title);
        /// <summary>Error message. Default = <see cref="AppMessages"/>.</summary>
        public static Action<string, string> ShowError = (msg, title) => AppMessages.Error(msg, title);
        /// <summary>
        /// Ask the user for the synthetic-overlay file offset a payload (<paramref name="expectedBytes"/>
        /// long) should be written to, showing the affected file range / runtime address / whether the
        /// range already contains data. Returns null if cancelled (or headless, default is a no-op so a
        /// synthetic-overlay patch never silently overwrites data without a real UI to confirm it).
        /// Args: patchName, synthetic-overlay file path, default offset, expected payload bytes, load address.
        /// </summary>
        public static Func<string, string, uint, byte[], uint, uint?> PickSyntheticOverlayOffset =
            (patchName, filePath, defaultOffset, expectedBytes, loadAddress) => null;

        // ── Synthetic-overlay ARM9 helpers (Thumb BL encode/decode, payload/range status) ──────────

        /// <summary>Encodes a Thumb BL instruction (4 bytes) from <paramref name="sourceAddress"/> to <paramref name="targetAddress"/>.</summary>
        public static byte[] BuildThumbBl(uint sourceAddress, uint targetAddress)
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

        /// <summary>Decodes a Thumb BL's target address, or false if <paramref name="branchBytes"/> isn't one.</summary>
        public static bool TryGetThumbBlTarget(uint sourceAddress, byte[] branchBytes, out uint targetAddress)
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

        private static byte[] BuildBuildingRotationPayload(BuildingRotationPatchData data, uint payloadAddress)
        {
            byte[] payload = (byte[])data.payload.Clone();
            byte[] branchBytes = BuildThumbBl(
                payloadAddress + BuildingRotationPatchData.payloadInternalBranchOffset,
                data.rotationMatrixFunctionAddress);
            Array.Copy(branchBytes, 0, payload, (int)BuildingRotationPatchData.payloadInternalBranchOffset, branchBytes.Length);
            return payload;
        }

        /// <summary>Human-readable status of a synthetic-overlay byte range, for confirmation prompts.</summary>
        public static string GetSyntheticOverlayRangeStatus(uint offset, byte[] expectedBytes)
        {
            string expandedPath = Filesystem.expArmPath;
            if (!File.Exists(expandedPath))
            {
                return "Synthetic overlay range status: synthetic overlay file was not found.";
            }

            long fileLength = new FileInfo(expandedPath).Length;
            if (offset >= fileLength || (long)offset + expectedBytes.Length > fileLength)
            {
                return "Synthetic overlay range status: selected range is outside the synthetic overlay file.";
            }

            byte[] currentBytes = DSUtils.ReadFromFile(expandedPath, offset, expectedBytes.Length);
            if (currentBytes.Length != expectedBytes.Length)
            {
                return "Synthetic overlay range status: selected range could not be read.";
            }

            if (currentBytes.All(b => b == 0))
            {
                return "Synthetic overlay range status: empty.";
            }

            return "Synthetic overlay range status: already contains data; continuing will overwrite it.";
        }

        // ── File-state checks (do the ROM bytes say the patch is applied?) ─────────────────────────

        /// <summary>Why the ARM9 expansion can't be applied to this ROM, or null when it can (or already is).</summary>
        public static string Arm9ExpansionWhyNot() =>
            RomInfo.isHGE ? HgEngine.HgEngineSyntheticOverlay.ToolboxReason
            : !ARM9PatchData.arm9ExpansionCodeDB.ContainsKey("branchString" + "_" + RomInfo.gameFamily + "_" + RomInfo.gameLanguage)
                ? "The ARM9 expansion isn't available for this game's language."
            : null;

        public static bool CheckFilesArm9ExpansionApplied()
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

            // HGSS ties this patch to overlay 1, whose compression state a legacy ndstool project
            // can't reliably track (see RomInfo.IsDsRomProject), require ds-rom format there.
            if (RomInfo.gameFamily == GameFamilies.HGSS && !RomInfo.IsDsRomProject)
            {
                return false;
            }

            BDHCAMPatchData data = new BDHCAMPatchData();

            byte[] branchCode = DSUtils.HexStringToByteArray(data.branchString);
            byte[] branchCodeRead = ARM9.ReadBytes(data.branchOffset, branchCode.Length);

            if (branchCode.Length != branchCodeRead.Length || !branchCode.SequenceEqual(branchCodeRead))
            {
                return false;
            }

            string overlayFilePath = OverlayUtils.GetPath(data.overlayNumber);

            byte[] overlayCode1 = DSUtils.HexStringToByteArray(data.overlayString1);
            byte[] overlayCode1Read = DSUtils.ReadFromFile(overlayFilePath, data.overlayOffset1, overlayCode1.Length);
            if (overlayCode1.Length != overlayCode1Read.Length || !overlayCode1.SequenceEqual(overlayCode1Read))
                return false;

            byte[] overlayCode2 = DSUtils.HexStringToByteArray(data.overlayString2);
            byte[] overlayCode2Read = DSUtils.ReadFromFile(overlayFilePath, data.overlayOffset2, overlayCode2.Length); //Write new overlayCode1
            if (overlayCode2.Length != overlayCode2Read.Length || !overlayCode2.SequenceEqual(overlayCode2Read))
                return false; //0 means BDHCAM patch has not been applied

            String fullFilePath = Filesystem.expArmPath;
            byte[] subroutineRead = DSUtils.ReadFromFile(fullFilePath, BDHCAMPatchData.BDHCamSubroutineOffset, data.subroutine.Length); //Write new overlayCode1
            if (data.subroutine.Length != subroutineRead.Length || !data.subroutine.SequenceEqual(subroutineRead))
                return false; //0 means BDHCAM patch has not been applied

            return true;
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

        public static bool CheckFilesDynamicHeadersPatchApplied()
        {
            DynamicHeadersPatchData data = new DynamicHeadersPatchData();
            ushort initValue = BitConverter.ToUInt16(ARM9.ReadBytes(data.initOffset, 0x2), 0);
            return initValue == 0xB500;
        }

        // ── Patch apply-methods ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Recases each all-capitals word, leaving control codes in braces and words already in lower case
        /// (the "a" and "an" before a name) as they are.
        /// </summary>
        internal static string SentenceCaseName(string text)
        {
            TextInfo textInfo = System.Globalization.CultureInfo.CurrentCulture.TextInfo;
            StringBuilder sb = new System.Text.StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == '{')
                {
                    int close = text.IndexOf('}', i);
                    if (close < 0) close = text.Length - 1;
                    sb.Append(text, i, close - i + 1);
                    i = close + 1;
                    continue;
                }
                int end = i;
                while (end < text.Length && text[end] != '{' && text[end] != ' ') end++;
                string word = text.Substring(i, end - i);
                bool upper = word.Any(char.IsLetter) && !word.Any(char.IsLower);
                sb.Append(upper ? textInfo.ToTitleCase(word.ToLower()) : word);
                while (end < text.Length && text[end] == ' ') sb.Append(text[end++]);
                i = end;
            }
            return sb.ToString();
        }

        /// <summary>Convert every Pokémon name to Sentence Case, including names the user renamed themselves. Always supported.</summary>
        public static bool ApplySentenceCasePatch()
        {
            if (!ConfirmYesNo("Confirming this process will apply the following changes:\n\n" +
                "- Every Pokémon name will be converted to Sentence Case, including names you've renamed yourself.\n" +
                "- Any other text (trainer dialogue, item descriptions, etc) mentioning a renamed Pokémon will be updated to match." + "\n\n" +
                "Do you wish to continue?" + CreditNote("sentenceCase"), "Confirm to proceed"))
            {
                ShowInfo("No changes have been made.", "Operation canceled");
                return false;
            }

            List<(string searchString, string replaceString, bool caseSensitive)> renamePairs = new List<(string searchString, string replaceString, bool caseSensitive)>();

            foreach (int ID in RomInfo.pokemonNamesTextNumbers)
            {
                TextArchive pokeName = new TextArchive(ID);
                for (int i = 1; i < pokeName.messages.Count; i++)
                {
                    string current = pokeName.messages[i];
                    if (string.IsNullOrEmpty(current))
                    {
                        continue;
                    }

                    string sentenceCased = SentenceCaseName(current);
                    if (sentenceCased != current)
                    {
                        pokeName.messages[i] = sentenceCased;
                        renamePairs.Add((current, sentenceCased, true));
                    }
                }
                pokeName.SaveToExpandedDir(ID, showSuccessMessage: false);
            }

            int archivesUpdated = renamePairs.Count > 0 ? DSUtils.ReplaceTextEverywhere(renamePairs, wholeWord: true) : 0;
            ShowInfo($"Pokémon names have been converted to Sentence Case.\nOther text banks updated: {archivesUpdated}", "Operation successful");
            return true;
        }

        /// <summary>Convert every Item name to Sentence Case, including names the user renamed themselves. Always supported.</summary>
        public static bool ApplyItemSentenceCasePatch()
        {
            if (!ConfirmYesNo("Confirming this process will apply the following changes:\n\n" +
                "- Every Item name will be converted to Sentence Case, including names you've renamed yourself.\n" +
                "- Any other text (trainer dialogue, script text, etc) mentioning a renamed Item will be updated to match." + "\n\n" +
                "Do you wish to continue?" + CreditNote("itemSentenceCase"), "Confirm to proceed"))
            {
                ShowInfo("No changes have been made.", "Operation canceled");
                return false;
            }

            List<(string searchString, string replaceString, bool caseSensitive)> renamePairs = new List<(string searchString, string replaceString, bool caseSensitive)>();

            TextArchive itemNames = new TextArchive(RomInfo.itemNamesTextNumber);
            for (int i = 1; i < itemNames.messages.Count; i++)
            {
                string current = itemNames.messages[i];
                if (string.IsNullOrEmpty(current))
                {
                    continue;
                }

                string sentenceCased = SentenceCaseName(current);
                if (sentenceCased != current)
                {
                    itemNames.messages[i] = sentenceCased;
                    renamePairs.Add((current, sentenceCased, true));
                }
            }
            itemNames.SaveToExpandedDir(RomInfo.itemNamesTextNumber, showSuccessMessage: false);

            int archivesUpdated = renamePairs.Count > 0 ? DSUtils.ReplaceTextEverywhere(renamePairs, wholeWord: true) : 0;
            ShowInfo($"Item names have been converted to Sentence Case.\nOther text banks updated: {archivesUpdated}", "Operation successful");
            return true;
        }

        /// <summary>Apply the BDHCam / Dynamic Cameras routine (Plat/HGSS EN/ES). Requires a ds-rom-format project on HGSS.</summary>
        public static bool ApplyBDHCamPatch()
        {
            if (RomInfo.gameFamily == GameFamilies.HGSS && !RomInfo.IsDsRomProject)
            {
                ShowError("Convert this project to ds-rom format before applying the Dynamic Cameras patch.", "ds-rom project required");
                return false;
            }

            BDHCAMPatchData data = new BDHCAMPatchData();

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.synthOverlay });
            if (AlreadyApplied(RomPatchState.flag_BDHCamPatchApplied || Probe(CheckFilesBDHCamPatchApplied))) return false;
            string expandedCheckPath = Filesystem.expArmPath;
            if (!File.Exists(expandedCheckPath) || new FileInfo(expandedCheckPath).Length < 0x16000)
            {
                ShowError("Apply the ARM9 expansion patch first, the synthetic overlay file is missing or not fully expanded.", "ARM9 Expansion Required");
                return false;
            }

            if (!ConfirmYesNo("This process will apply the following changes:\n\n" +
            "- Backup ARM9 file (arm9.bin" + BackupSuffix + " will be created)." + "\n\n" +
            "- Backup Overlay" + data.overlayNumber + " file (overlay" + data.overlayNumber + ".bin" + BackupSuffix + " will be created)." + "\n\n" +
            "- Replace " + (data.branchString.Length / 3 + 1) + " bytes of data at arm9 offset 0x" + data.branchOffset.ToString("X") + " with " + '\n' + data.branchString + "\n\n" +
            "- Replace " + (data.overlayString1.Length / 3 + 1) + " bytes of data at overlay" + data.overlayNumber + " offset 0x" + data.overlayOffset1.ToString("X") + " with " + '\n' + data.overlayString1 + "\n\n" +
            "- Replace " + (data.overlayString2.Length / 3 + 1) + " bytes of data at overlay" + data.overlayNumber + " offset 0x" + data.overlayOffset2.ToString("X") + " with " + '\n' + data.overlayString2 + "\n\n" +
            "- Modify file #" + RomPatchState.expandedARMfileID + " inside " + '\n' + RomInfo.gameDirs[DirNames.synthOverlay].unpackedDir + '\n' + "to insert the BDHCAM routine (any data between 0x" + BDHCAMPatchData.BDHCamSubroutineOffset.ToString("X") + " and 0x" + (BDHCAMPatchData.BDHCamSubroutineOffset + data.subroutine.Length).ToString("X") + " will be overwritten)." + "\n\n" +
            "Do you wish to continue?" + CreditNote("bdhcam"), "Confirm to proceed"))
            {
                ShowInfo("No changes have been made.", "Operation canceled");
                return false;
            }

            File.Copy(RomInfo.arm9Path, RomInfo.arm9Path + BackupSuffix, overwrite: true);
            string overlayBackupPath = OverlayUtils.GetPath(data.overlayNumber);
            File.Copy(overlayBackupPath, overlayBackupPath + BackupSuffix, overwrite: true);

            try
            {
                /* Write to overlayfile */
                string overlayFilePath = OverlayUtils.GetPath(data.overlayNumber);
                if (OverlayUtils.IsCompressed(data.overlayNumber))
                {
                    int decompressResult = OverlayUtils.Decompress(data.overlayNumber, makeBackup: false);
                    if (decompressResult != 0)
                    {
                        AppLogger.Error($"Could not decompress overlay {data.overlayNumber}; BDHCAM patch was not applied.");
                        File.Copy(overlayBackupPath, overlayFilePath, overwrite: true);
                        if (decompressResult != DSUtils.ERR_TOOL_UNAVAILABLE)
                        {
                            ShowError("The target overlay could not be decompressed, so no changes were made.",
                                "Decompression failed");
                        }
                        return false;
                    }
                }

                ARM9.WriteBytes(DSUtils.HexStringToByteArray(data.branchString), data.branchOffset); //Write new branchOffset
                DSUtils.WriteToFile(overlayFilePath, DSUtils.HexStringToByteArray(data.overlayString1), data.overlayOffset1); //Write new overlayCode1
                DSUtils.WriteToFile(overlayFilePath, DSUtils.HexStringToByteArray(data.overlayString2), data.overlayOffset2); //Write new overlayCode2

                /*Write Expanded ARM9 File*/
                DSUtils.WriteToFile(Filesystem.expArmPath, data.subroutine, BDHCAMPatchData.BDHCamSubroutineOffset);
            }
            catch
            {
                ShowError("Operation failed. It is strongly advised that you restore the arm9 and overlay from their respective backups.", "Something went wrong");
                return false;
            }

            RomPatchState.flag_BDHCamPatchApplied = true;

            ShowInfo("The BDHCAM patch has been applied.", "Operation successful.");
            return true;
        }

        /// <summary>Checks whether the Building Rotation routine hook + payload are already present on the ROM.</summary>
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
            string expandedPath = Filesystem.expArmPath;
            if (!File.Exists(expandedPath))
            {
                return false;
            }

            long fileLength = new FileInfo(expandedPath).Length;
            if ((long)payloadOffset + data.payload.Length > fileLength)
            {
                return false;
            }

            byte[] payloadRead = DSUtils.ReadFromFile(expandedPath, payloadOffset, data.payload.Length);
            return payloadRead.SequenceEqual(BuildBuildingRotationPayload(data, targetAddress));
        }

        /// <summary>
        /// Apply the Building Rotation routine (Diamond/Pearl/Platinum/HeartGold/SoulSilver EN, Plat FR,
        /// HG IT). Requires the ARM9 expansion patch and a ds-rom-format project (the hook writes into
        /// an overlay whose compression state ds-rom tracks automatically; a legacy ndstool project can't
        /// reliably guarantee the overlay is uncompressed here). Lets the user choose where in the
        /// synthetic overlay the payload lands via <see cref="PickSyntheticOverlayOffset"/>.
        /// </summary>
        public static bool ApplyBuildingRotationPatch()
        {
            if (!RomInfo.IsDsRomProject)
            {
                ShowError("Convert this project to ds-rom format before applying the Building Rotation patch.", "ds-rom project required");
                return false;
            }

            if (!RomPatchState.flag_arm9Expanded && !CheckFilesArm9ExpansionApplied())
            {
                ShowError("Apply the ARM9 Expansion patch before applying the Building Rotation patch.", "ARM9 Expansion Required");
                return false;
            }

            BuildingRotationPatchData data;
            try
            {
                data = new BuildingRotationPatchData();
            }
            catch
            {
                ShowError("This ROM version is not supported by the Building Rotation patch.", "Unsupported");
                return false;
            }

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.synthOverlay });
            if (AlreadyApplied(RomPatchState.flag_BuildingRotationPatchApplied || Probe(CheckFilesBuildingRotationPatchApplied))) return false;
            string expandedPath = Filesystem.expArmPath;
            if (!File.Exists(expandedPath) || new FileInfo(expandedPath).Length < 0x16000)
            {
                ShowError("Apply the ARM9 expansion patch first, the synthetic overlay file is missing or not fully expanded.", "ARM9 Expansion Required");
                return false;
            }

            uint? pickedOffset = PickSyntheticOverlayOffset("Building rotation routine", expandedPath, data.defaultPayloadOffset, data.payload, synthOverlayLoadAddress);
            if (pickedOffset == null)
            {
                ShowInfo("No changes have been made.", "Operation canceled");
                return false;
            }

            uint payloadOffset = pickedOffset.Value;
            uint payloadAddress = synthOverlayLoadAddress + payloadOffset;
            byte[] branchBytes = BuildThumbBl(data.hookRuntimeAddress, payloadAddress);
            byte[] payloadBytes = BuildBuildingRotationPayload(data, payloadAddress);
            string rangeStatus = GetSyntheticOverlayRangeStatus(payloadOffset, data.payload);

            if (!ConfirmYesNo("This process will apply the following changes:\n\n" +
                "- Backup Overlay " + data.overlayNumber + " file (overlay" + data.overlayNumber + ".bin" + BackupSuffix + " will be created).\n\n" +
                "- Replace 4 bytes at Overlay " + data.overlayNumber + " offset 0x" + data.hookOverlayOffset.ToString("X") + " with a branch to the building rotation routine.\n\n" +
                "- Modify file #" + RomPatchState.expandedARMfileID + " inside " + '\n' + RomInfo.gameDirs[DirNames.synthOverlay].unpackedDir + '\n' +
                "to insert the building rotation routine at offset 0x" + payloadOffset.ToString("X") + " (runtime address 0x" + payloadAddress.ToString("X8") + ").\n" +
                rangeStatus + "\n\n" +
                "This enables the existing building rotation values to be used when placing buildings.\n\n" +
                "Do you wish to continue?" + CreditNote("buildingRotation"), "Confirm to proceed"))
            {
                ShowInfo("No changes have been made.", "Operation canceled");
                return false;
            }

            string overlayFilePath = OverlayUtils.GetPath(data.overlayNumber);
            File.Copy(overlayFilePath, overlayFilePath + BackupSuffix, overwrite: true);

            try
            {
                DSUtils.WriteToFile(overlayFilePath, branchBytes, data.hookOverlayOffset);
                DSUtils.WriteToFile(expandedPath, payloadBytes, payloadOffset);
            }
            catch
            {
                ShowError("Operation failed. It is strongly advised that you restore the Overlay " + data.overlayNumber + " backup.", "Something went wrong");
                return false;
            }

            RomPatchState.flag_BuildingRotationPatchApplied = true;

            ShowInfo("The Building Rotation patch has been applied.\n\n" +
                "Synthetic overlay offset: 0x" + payloadOffset.ToString("X"), "Operation successful.");
            return true;
        }

        /// <summary>The external trainer shiny patch from PR #271 (US HeartGold/SoulSilver, Italian HeartGold).</summary>
        public static bool ApplyTrainerShinyPatch()
        {
            if (!RomInfo.IsDsRomProject)
            {
                ShowError("Convert this project to ds-rom format before applying the trainer shiny patch.", "ds-rom project required");
                return false;
            }
            if (!TrainerShinyPatch.SupportsCurrentRom)
            {
                ShowError("The trainer shiny patch supports US HeartGold and SoulSilver and Italian HeartGold.", "Unsupported");
                return false;
            }
            if (!RomPatchState.flag_arm9Expanded && !CheckFilesArm9ExpansionApplied())
            {
                ShowError("Apply the ARM9 Expansion patch before applying the trainer shiny patch.", "ARM9 Expansion Required");
                return false;
            }
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.synthOverlay });
            if (AlreadyApplied(Probe(TrainerShinyPatch.DetectCurrentProject))) return false;
            if (TrainerShinyPatch.WhyNotApplicable(File.ReadAllBytes(RomInfo.arm9Path)) is string why)
            {
                ShowError(why, "Trainer shiny patch");
                return false;
            }

            string expandedPath = Filesystem.expArmPath;
            if (!File.Exists(expandedPath) || new FileInfo(expandedPath).Length < 0x16000)
            {
                ShowError("Apply the ARM9 expansion patch first, the synthetic overlay file is missing or not fully expanded.", "ARM9 Expansion Required");
                return false;
            }

            byte[] template = TrainerShinyPatch.Template;
            uint? picked = PickSyntheticOverlayOffset("Trainer shiny routine", expandedPath, TrainerShinyPatch.DefaultPayloadOffset, template, synthOverlayLoadAddress);
            if (picked == null)
            {
                ShowInfo("No changes have been made.", "Operation canceled");
                return false;
            }
            uint offset = picked.Value;
            if ((offset & 3) != 0)
            {
                ShowError("The routine has to start on a four-byte boundary.", "Trainer shiny patch");
                return false;
            }

            if (!ConfirmYesNo("This process will apply the following changes:\n\n" +
                "- Insert the trainer shiny routine (" + template.Length + " bytes) at synthetic overlay offset 0x" + offset.ToString("X") +
                " (runtime address 0x" + (synthOverlayLoadAddress + offset).ToString("X8") + ").\n" +
                GetSyntheticOverlayRangeStatus(offset, template) + "\n\n" +
                "- Point the ARM9's trainer party setup at it and leave flag 0x40 out of the ability.\n\n" +
                "Party members ticked Shiny in the Trainer editor then battle shiny." + BackupNote("the ARM9") + "\n\nDo you wish to continue?" + CreditNote("trainerShiny"), "Confirm to proceed"))
            {
                ShowInfo("No changes have been made.", "Operation canceled");
                return false;
            }

            ARM9.DecompressIfMarked();
            File.Copy(RomInfo.arm9Path, RomInfo.arm9Path + BackupSuffix, overwrite: true);
            try { TrainerShinyPatch.Apply(offset); }
            catch
            {
                ShowError("Operation failed. It is strongly advised that you restore the ARM9 backup.", "Something went wrong");
                return false;
            }
            ShowInfo("The trainer shiny patch has been applied.\n\nSynthetic overlay offset: 0x" + offset.ToString("X"), "Operation successful.");
            return true;
        }

        /// <summary>The external Trainer Class Metadata patch from PR #272 (US HeartGold/SoulSilver).</summary>
        public static bool ApplyTrainerClassMetadataPatch()
        {
            if (!RomInfo.IsDsRomProject)
            {
                ShowError("Convert this project to ds-rom format before applying the trainer class metadata patch.", "ds-rom project required");
                return false;
            }
            if (!TrainerClassMetadataPatch.SupportsCurrentRom)
            {
                ShowError("The trainer class metadata patch supports US HeartGold and SoulSilver.", "Unsupported");
                return false;
            }
            if (!RomPatchState.flag_arm9Expanded && !CheckFilesArm9ExpansionApplied())
            {
                ShowError("Apply the ARM9 Expansion patch before applying the trainer class metadata patch.", "ARM9 Expansion Required");
                return false;
            }
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.synthOverlay });
            if (AlreadyApplied(Probe(() => TrainerClassMetadataStore.DetectCurrentRom(out _) == TrainerClassMetadataDetectionState.SchemaV1))) return false;
            if (TrainerClassMetadataPatch.WhyNotApplicable() is string why)
            {
                ShowError(why, "Trainer class metadata patch");
                return false;
            }

            string expandedPath = Filesystem.expArmPath;
            if (!File.Exists(expandedPath) || new FileInfo(expandedPath).Length < 0x16000)
            {
                ShowError("Apply the ARM9 expansion patch first, the synthetic overlay file is missing or not fully expanded.", "ARM9 Expansion Required");
                return false;
            }

            byte[] footprint = TrainerClassMetadataPatch.Footprint;
            uint? picked = PickSyntheticOverlayOffset("Trainer class metadata routines", expandedPath, TrainerClassMetadataPatch.DefaultPayloadOffset, footprint, synthOverlayLoadAddress);
            if (picked == null)
            {
                ShowInfo("No changes have been made.", "Operation canceled");
                return false;
            }
            uint offset = picked.Value;
            if ((offset & 3) != 0 || offset + footprint.Length > new FileInfo(expandedPath).Length)
            {
                ShowError("The routines need 0x" + footprint.Length.ToString("X") + " bytes starting on a four-byte boundary inside the synthetic overlay.", "Trainer class metadata patch");
                return false;
            }
            string rangeStatus = GetSyntheticOverlayRangeStatus(offset, footprint);

            if (!ConfirmYesNo("This process will apply the following changes:\n\n" +
                "- Build one record per trainer class in a/1/5/5 from the game's own gender, prize, music and intro tables.\n\n" +
                "- Insert the patch routines (0x" + footprint.Length.ToString("X") + " bytes) at synthetic overlay offset 0x" + offset.ToString("X") +
                " (runtime address 0x" + (synthOverlayLoadAddress + offset).ToString("X8") + ").\n" + rangeStatus + "\n\n" +
                "- Hook the ARM9 and overlays 1, 12, 80, 115, 117, 118, 119 and 120 to them.\n\n" +
                "Backups (" + BackupSuffix + ") are made of the ARM9, those overlays and the synthetic overlay, and of a/1/5/5 as " +
                "unpacked/a155.narc" + BackupSuffix + ".\n\n" +
                "Each trainer class then owns its gender, prize, music and VS intro, edited in the Trainer Classes window. " +
                "Not compatible with hg-engine.\n\nDo you wish to continue?" + CreditNote("trainerClassMetadata"), "Confirm to proceed"))
            {
                ShowInfo("No changes have been made.", "Operation canceled");
                return false;
            }

            ARM9.DecompressIfMarked();
            File.Copy(RomInfo.arm9Path, RomInfo.arm9Path + BackupSuffix, overwrite: true);
            foreach (int overlay in new[] { 1, 12, 80, 115, 117, 118, 119, 120 })
                File.Copy(OverlayUtils.GetPath(overlay), OverlayUtils.GetPath(overlay) + BackupSuffix, overwrite: true);
            if (File.Exists(Filesystem.expArmPath)) File.Copy(Filesystem.expArmPath, Filesystem.expArmPath + BackupSuffix, overwrite: true);
            // Not beside the archive: ds-rom packs every file under files/, so a copy there would join the ROM.
            (string metadataArchive, string metadataUnpacked) = RomInfo.gameDirs[DirNames.trainerClassMetadata];
            string metadataBackup = Path.Combine(Path.GetDirectoryName(metadataUnpacked), "a155.narc" + BackupSuffix);
            if (File.Exists(metadataArchive))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(metadataBackup));
                File.Copy(metadataArchive, metadataBackup, overwrite: true);
            }
            try { TrainerClassMetadataPatch.Apply(offset); }
            catch (Exception ex)
            {
                ShowError("Operation failed: " + ex.Message + "\nIt is strongly advised that you restore the ARM9, overlay, synthetic overlay and a/1/5/5 backups.", "Something went wrong");
                return false;
            }
            ShowInfo("The trainer class metadata patch has been applied.\n\nSynthetic overlay offset: 0x" + offset.ToString("X"), "Operation successful.");
            return true;
        }

        /// <summary>Rearrange item scripts to ascending index order and fix ground-item references. Not supported on hg-engine ROMs.</summary>
        public static bool ApplyItemStandardizePatch()
        {
            if (RomInfo.isHGE)
            {
                ShowError("This patch isn't supported on hg-engine ROMs.", "Unsupported");
                return false;
            }

            if (!ConfirmYesNo("This process will apply the following changes:\n\n" +
                "- Item scripts will be rearranged to follow the natural, ascending index order.\n\n" +
                "- Any unsaved change to the current Event File will be discarded." + CreditNote("itemStandardize"), "Confirm to proceed"))
            {
                ShowInfo("No changes have been made.", "Operation canceled");
                return false;
            }

            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.scripts });
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.eventFiles });

            if (AlreadyApplied(RomPatchState.flag_standardizedItems || Probe(CheckScriptsStandardizedItemNumbers))) return false;

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
            }
            ;

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
            ShowInfo("Operation successful.", "Process completed.");

            RomPatchState.flag_standardizedItems = true;
            return true;
        }

        /// <summary>Expand the ARM9's usable memory (synthetic overlay). Enables BDHCam on Plat/HGSS.</summary>
        public static bool ApplyARM9ExpansionPatch()
        {
            if (AlreadyApplied(RomPatchState.flag_arm9Expanded || Probe(CheckFilesArm9ExpansionApplied))) return false;

            ARM9PatchData data = new ARM9PatchData();

            if (!ConfirmYesNo("Confirming this process will apply the following changes:\n\n" +
                    "- Backup ARM9 file (arm9.bin" + BackupSuffix + " will be created)." + "\n\n" +
                    "- Replace " + (data.branchString.Length / 3 + 1) + " bytes of data at arm9 offset 0x" + data.branchOffset.ToString("X") + " with " + '\n' + data.branchString + "\n\n" +
                    "- Replace " + (data.initString.Length / 3 + 1) + " bytes of data at arm9 offset 0x" + data.initOffset.ToString("X") + " with " + '\n' + data.initString + "\n\n" +
                    "- Modify file #" + RomPatchState.expandedARMfileID + " inside " + '\n' + RomInfo.gameDirs[DirNames.synthOverlay].unpackedDir + '\n' + " to accommodate for 88KB of data (no backup)." + "\n\n" +
                    "If you do not understand the implications of these changes and how they can affect your game do NOT continue. You can and will break the game if you do not know what you are doing here.\n\n" +
                    "Do you wish to continue?" + CreditNote("arm9"), "Confirm to proceed"))
            {
                ShowInfo("No changes have been made.", "Operation canceled");
                return false;
            }

            File.Copy(RomInfo.arm9Path, RomInfo.arm9Path + BackupSuffix, overwrite: true);

            try
            {
                ARM9.WriteBytes(DSUtils.HexStringToByteArray(data.branchString), data.branchOffset); //Write new branchOffset
                ARM9.WriteBytes(DSUtils.HexStringToByteArray(data.initString), data.initOffset); //Write new initOffset

                // The synthetic overlay's backing NARC has to actually be unpacked on disk before its
                // file #0 can be checked/created, on a fresh project (Header Editor never opened) this
                // directory doesn't exist yet, which used to make the block below a silent no-op while
                // still reporting success.
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.synthOverlay });

                string fullFilePath = Filesystem.expArmPath;

                // Do a file size check first just in case the file is already expanded so we don't nuke existing data
                if (File.Exists(fullFilePath))
                {
                    FileInfo fi = new FileInfo(fullFilePath);
                    if (fi.Length >= 0x16000)
                    {
                        ShowInfo("The synthetic Overlay already exists. " +
                            "This may be due to a previous application of the ARM9 expansion patch. " +
                            "No changes have been made to the file to avoid data loss.\n\n" +
                            "Double check to make sure this is correct!", "Synthetic Overlay Exists");
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

                RomPatchState.flag_arm9Expanded = true;

                ShowInfo("The ARM9's usable memory has been expanded.", "Operation successful.");
                return true;
            }
            catch
            {
                ShowError("Operation failed. It is strongly advised that you restore the arm9 backup (arm9.bin" + BackupSuffix + ").", "Something went wrong");
                return false;
            }
        }

        /// <summary>Expand Matrix 0 up to twice its size (HGSS EN/ES).</summary>
        public static bool ApplyMatrixExpansionPatch()
        {
            if (AlreadyApplied(RomPatchState.flag_MatrixExpansionApplied || Probe(CheckFilesMatrixExpansionApplied))) return false;

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

            if (!ConfirmYesNo("Confirming this process will apply the following changes:\n\n" +
                "- Backup ARM9 file (arm9.bin" + BackupSuffix + " will be created).\n\n" +
                listOfChanges +
                "Do you wish to continue?" + CreditNote("matrix"), "Confirm to proceed"))
            {
                ShowInfo("No changes have been made.", "Operation canceled");
                return false;
            }

            ARM9.DecompressIfMarked();
            BackUp(RomInfo.arm9Path);
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
                ShowError("Operation failed. It is strongly advised that you restore the arm9 backup (arm9.bin" + BackupSuffix + ").", "Something went wrong");
                return false;
            }
            RomPatchState.flag_MatrixExpansionApplied = true;
            ShowInfo("Matrix 0 can now be freely expanded up to twice its size.", "Operation successful.");
            return true;
        }

        /// <summary>Dynamically allocate map headers in memory (Plat/HGSS).</summary>
        public static bool ApplyDynamicHeadersPatch()
        {
            // A second run would split the already-patched table over the headers it moved out.
            if (AlreadyApplied(RomPatchState.flag_DynamicHeadersPatchApplied || Probe(CheckFilesDynamicHeadersPatchApplied))) return false;

            DynamicHeadersPatchData data = new DynamicHeadersPatchData();
            (string packedDir, string unpackedDir) headersDir = RomInfo.gameDirs[DirNames.dynamicHeaders];

            bool specialCase = RomInfo.gameFamily == GameFamilies.HGSS && RomInfo.gameLanguage != GameLanguages.Japanese && RomInfo.gameLanguage != GameLanguages.Spanish;
            string specialCaseChanges = "";

            if (specialCase)
            {
                specialCaseChanges = "- Replace " + (data.specialCaseData1.Length / 3 + 1) + " bytes of data at arm9 offset 0x" + (data.specialCaseOffset1 + data.pointerDiff).ToString("X") + " with " + '\n' + data.specialCaseData1 + "\n\n" +
                    "- Replace " + (data.specialCaseData2.Length / 3 + 1) + " bytes of data at arm9 offset 0x" + (data.specialCaseOffset2 + data.pointerDiff).ToString("X") + " with " + '\n' + data.specialCaseData2 + "\n\n" +
                    "- Replace " + (data.specialCaseData3.Length / 3 + 1) + " bytes of data at arm9 offset 0x" + (data.specialCaseOffset3 + data.pointerDiff).ToString("X") + " with " + '\n' + data.specialCaseData3 + "\n\n";
            }

            if (!ConfirmYesNo("Confirming this process will apply the following changes:\n\n" +
                "- Backup ARM9 file (arm9.bin" + BackupSuffix + " will be created)." + "\n\n" +
                "- NARC file at " + headersDir.packedDir + " will become the new header container." + "\n\n" +
                "- The default ARM9 header table will be split into multiple files (one per header), each one saved into NARC " + headersDir.packedDir + " upon saving the ROM." + "\n\n" +
                "- Replace " + (data.initString.Length / 3 + 1) + " bytes of data at arm9 offset 0x" + data.initOffset.ToString("X") + " with " + '\n' + data.initString + "\n\n" +
                "- Neutralize instances of (HeaderID * 0x18) so the base offset which the data is read from is always 0x0." + "\n\n" +
                "- Change pointers to header fields, from(ARM9_HEADER_TABLE_OFFSET + n) to simply(0 + n)" + "\n\n" +
                specialCaseChanges +
                "Do you wish to continue?" + CreditNote("dynamicHeaders"), "Confirm to proceed"))
            {
                ShowInfo("No changes have been made.", "Operation canceled");
                return false;
            }

            File.Copy(RomInfo.arm9Path, RomInfo.arm9Path + BackupSuffix, overwrite: true);

            try
            {
                ARM9.WriteBytes(DSUtils.HexStringToByteArray(data.initString), data.initOffset);

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
                    DSUtils.WriteToFile(Path.Combine(headersDir.unpackedDir, i.ToString("D4")), headerData);
                }

                RomPatchState.flag_DynamicHeadersPatchApplied = true;

                ShowInfo("The headers are now dynamically allocated in memory.", "Operation successful.");
                return true;
            }
            catch
            {
                ShowError("Operation failed. It is strongly advised that you restore the arm9 backup (arm9.bin" + BackupSuffix + ").", "Something went wrong");
                return false;
            }
        }

        // WildMonSetRandomHeldItem gives a species listing one item twice that item every time; turning its
        // branch-if-different into an unconditional branch sends every species through the odds.
        private static readonly byte[] SameItemBranchVanilla = { 0x09, 0xD1 }, SameItemBranchPatched = { 0x09, 0xE0 };

        public static PatchState SameHeldItemOddsState()
        {
            if (ROMFiles.GameTableFile.WhyNot(GameTable.HeldItemSameItemBranch, 2) is string why) return Unsupported(why);
            byte[] now = ROMFiles.GameTableFile.Read(GameTable.HeldItemSameItemBranch, 2);
            if (now.AsSpan().SequenceEqual(SameItemBranchPatched)) return PatchState.Applied;
            if (now.AsSpan().SequenceEqual(SameItemBranchVanilla)) return PatchState.Available;
            return Unsupported("The code there has already been changed by something else.");
        }

        public static bool ApplySameHeldItemOddsPatch()
        {
            if (SameHeldItemOddsState() != PatchState.Available) return false;
            if (!ConfirmYesNo("Wild Pokémon whose two held items are the same will hold it only as often as the " +
                "held item odds say, instead of always." + BackupNote("the ARM9") + "\n\nApply this patch?" + CreditNote("sameHeldItemOdds"), "Confirm to proceed"))
                return false;
            BackUp(ROMFiles.GameTableFile.PathOf(RomInfo.SpotOf(GameTable.HeldItemSameItemBranch).Value));
            ROMFiles.GameTableFile.Write(GameTable.HeldItemSameItemBranch, SameItemBranchPatched);
            ShowInfo("Same held items now use the held item odds.", "Operation successful.");
            return true;
        }

        /// <summary>Moves the trainer class gender and prize tables into blocks with room for every class.</summary>
        public static bool ApplyMoveTrainerClassTables()
        {
            if (!TrainerClassTableExpansion.IsSupportedForCurrentRom) { ShowError("Only Platinum (English) is supported.", "Patch not applied"); return false; }
            if (AlreadyApplied(TrainerClassTableExpansion.ClassTablesHaveRoom)) return false;
            if (PlacementNote(TrainerClassTableExpansion.ClassTableBlockLengths(), out int[] offsets) is not string placement) return false;
            if (!ConfirmYesNo("This copies the trainer class gender table and prize money table into their own blocks in the synthetic overlay with room for " +
                TrainerClassTableExpansion.MaxClasses + " classes, and points the ARM9 and overlay 16 at them. Adding a class then writes into that room." + placement + "\n\n" +
                "Backups (" + BackupSuffix + ") are made of the ARM9, overlay 16 and the synthetic overlay first. It can't be removed by DSPRE; restore the backups to undo it." +
                "\n\nApply this patch?" + CreditNote("trainerClassTablesExpanded"), "Confirm to proceed"))
                return false;
            File.Copy(RomInfo.arm9Path, RomInfo.arm9Path + BackupSuffix, overwrite: true);
            File.Copy(OverlayUtils.GetPath(16), OverlayUtils.GetPath(16) + BackupSuffix, overwrite: true);
            File.Copy(Filesystem.expArmPath, Filesystem.expArmPath + BackupSuffix, overwrite: true);
            string error = null;
            bool moved = false;
            SyntheticOverlaySpace.PlaceAt(offsets, () => moved = TrainerClassTableExpansion.MoveClassTables(out error));
            if (!moved)
            {
                ShowError(error, "Tables not moved");
                return false;
            }
            ShowInfo("The trainer class tables have been moved.", "Operation successful.");
            return true;
        }

        public static bool ApplyMoveEncounterMusicTable()
        {
            if (AlreadyApplied(TrainerClassTableExpansion.MusicTableHasRoom)) return false;
            if (!TrainerClassTableExpansion.IsSupportedForCurrentRom) { ShowError("Only Platinum (English) is supported.", "Patch not applied"); return false; }
            if (PlacementNote(new[] { TrainerClassTableExpansion.MusicBlockLength }, out int[] offsets) is not string placement) return false;
            if (!ConfirmYesNo("This copies the eye-contact music table into its own block in the synthetic overlay with room for an entry per class (" +
                TrainerClassTableExpansion.MaxClasses + "), and points the ARM9 at it. Giving a class music then writes into that room and raises the entry count." + placement + "\n\n" +
                "Backups (" + BackupSuffix + ") are made of the ARM9 and the synthetic overlay first. It can't be removed by DSPRE; restore the backups to undo it." +
                "\n\nApply this patch?" + CreditNote("trainerEncounterBgmRepointed"), "Confirm to proceed"))
                return false;
            File.Copy(RomInfo.arm9Path, RomInfo.arm9Path + BackupSuffix, overwrite: true);
            File.Copy(Filesystem.expArmPath, Filesystem.expArmPath + BackupSuffix, overwrite: true);
            string error = null;
            bool moved = false;
            SyntheticOverlaySpace.PlaceAt(offsets, () => moved = TrainerClassTableExpansion.MoveEncounterMusicTable(out error));
            if (!moved)
            {
                ShowError(error, "Table not moved");
                return false;
            }
            ShowInfo("The eye-contact music table has been moved.", "Operation successful.");
            return true;
        }

        public static bool ApplyDisableDynamicTexturesPatch()
        {
            if (!ConfirmYesNo("Applying this patch will set the Dynamic Textures field of all AreaData files to 0xFFFF.\n\n" +
                "Are you sure you want to proceed?" + CreditNote("disableTextures"), "Confirm to proceed"))
            {
                ShowInfo("No changes have been made.", "Operation canceled");
                return false;
            }

            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { DirNames.areaData });

            string[] adFiles = Directory.GetFiles(gameDirs[DirNames.areaData].unpackedDir);
            foreach (string s in adFiles)
            {
                AreaData a = new AreaData(new FileStream(s, FileMode.Open))
                {
                    groundAnimation = 0xFFFF
                };
                a.SaveToFile(s, showSuccessMessage: false);
            }

            ShowInfo("Texture Animations have been disabled in every AreaData.", "Operation successful.");
            return true;
        }

        /// <summary>Extend the Trainer Name max length.</summary>
        public static bool ApplyExpandTrainerNamesPatch()
        {
            if (AlreadyApplied(RomPatchState.flag_TrainerNamesExpanded || RomInfo.trainerNameMaxLen > TrainerFile.defaultNameLen)) return false;

            if (!ConfirmYesNo($"Applying this patch will set the Trainer Name max length to {RomPatchState.expandedTrainerNameLength - 1} usable characters." +
                BackupNote("the ARM9") + "\n\nAre you sure you want to proceed?" + CreditNote("trainerNames"), "Confirm to proceed"))
            {
                ShowInfo("No changes have been made.", "Operation canceled");
                return false;
            }

            try
            {
                ARM9.DecompressIfMarked();
                BackUp(RomInfo.arm9Path);
                using (ARM9.Writer wr = new ARM9.Writer(RomInfo.trainerNameLenOffset))
                {
                    wr.Write((byte)RomPatchState.expandedTrainerNameLength);
                }

                RomPatchState.flag_TrainerNamesExpanded = true;
                ShowInfo("Trainer Names have been extended.", "Operation successful.");
                return true;
            }
            catch (IOException)
            {
                ShowError("ARM9 could not be written.", "Operation canceled");
                return false;
            }
        }

        // ── Script-command table (moves the in-game ScrCommands table + count into the synthetic
        // overlay; does not add commands or edit the JSON script-command metadata) ─────────────────

        private const uint ScrcmdOriginalCommandCount = 0x355;
        private const int ScrcmdOriginalTableLength = (int)(4 * ScrcmdOriginalCommandCount);
        private const uint ScrcmdCountOffsetInBlock = 0x04;
        private const uint ScrcmdTableMarkerOffsetInBlock = 0x08;
        private const uint ScrcmdTableOffsetInBlock = 0x0C;
        private const uint ScrcmdCountMarker = 0x4E554F43; // "COUN"
        private const uint ScrcmdTableMarker = 0x4C424154; // "TABL"
        private const uint ScrcmdBlockDefaultOffset = 0x200;

        /// <summary>Move the ScrCommands table + count into the expanded ARM9 file (HGSS EN/ES).</summary>
        public static bool ApplyScrcmdRepointPatch()
        {
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.synthOverlay });
            string expandedPath = Filesystem.expArmPath;
            if (!File.Exists(expandedPath))
            {
                ShowError("Apply the ARM9 expansion patch first, the synthetic overlay file is missing.", "ARM9 not expanded");
                return false;
            }

            if (GetCommandTableOffset() >= 0)
            {
                ShowInfo("The script command table is already repointed to the expanded ARM9 file.", "Already applied");
                return true;
            }

            byte[] commandTablePayload;
            try
            {
                commandTablePayload = BuildCommandTablePayload();
            }
            catch
            {
                ShowError("This ROM version is not supported by the ScrCommands table patch.", "Unsupported");
                return false;
            }

            uint? pickedOffset = PickSyntheticOverlayOffset("Script command table block", expandedPath, ScrcmdBlockDefaultOffset, commandTablePayload, synthOverlayLoadAddress);
            if (pickedOffset == null)
            {
                ShowInfo("No changes have been made.", "Operation canceled");
                return false;
            }

            uint blockOffset = pickedOffset.Value;
            string rangeStatus = GetSyntheticOverlayRangeStatus(blockOffset, commandTablePayload);

            if (!ConfirmYesNo("This process will apply the following changes:\n\n" +
                "- Backup ARM9 file (arm9.bin" + BackupSuffix + " will be created).\n\n" +
                "- Write the moved ScrCommands block to synthetic overlay offset 0x" + blockOffset.ToString("X") + ".\n\n" +
                "- Update the ARM9 ScrCommands table pointer.\n\n" +
                "- Update the ARM9 ScrCommands count pointer.\n" +
                rangeStatus + "\n\n" +
                "Do you wish to continue?" + CreditNote("scrcmdRepoint"), "Confirm to proceed"))
            {
                ShowInfo("No changes have been made.", "Operation canceled");
                return false;
            }

            try
            {
                File.Copy(RomInfo.arm9Path, RomInfo.arm9Path + BackupSuffix, overwrite: true);
                RepointCommandTable(blockOffset, commandTablePayload);
            }
            catch
            {
                ShowError("Repointing the script command table failed. It is strongly advised that you restore the arm9 backup (arm9.bin" + BackupSuffix + ").", "Something went wrong");
                return false;
            }

            ShowInfo("The ScrCommands table patch has been applied.\n\n" +
                "This does not add new commands or update DSPRE's JSON script-command metadata.\n\n" +
                "Synthetic overlay offset: 0x" + blockOffset.ToString("X") +
                " (count: 0x" + (blockOffset + ScrcmdCountOffsetInBlock).ToString("X") +
                ", table: 0x" + (blockOffset + ScrcmdTableOffsetInBlock).ToString("X") + ")",
                "ScrCommands Table Moved");
            return true;
        }

        /// <summary>Checks if the command table is repointed IN THE EXPANDED ARM9 FILE, returns its pointer inside that file (or -1).</summary>
        /// <summary>Whether the ScrCommands table has been moved into the synthetic overlay (table + count pointer both valid).</summary>
        public static bool IsScrcmdRepointApplied() => GetCommandTableOffset() >= 0 && CheckScrcmdCommandCountPointerValid();

        private static int GetCommandTableOffset()
        {
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
                    string expandedPath = Filesystem.expArmPath;
                    if (File.Exists(expandedPath))
                    {
                        long fileLength = new FileInfo(expandedPath).Length;
                        if (offset >= ScrcmdTableOffsetInBlock &&
                            (long)offset + ScrcmdOriginalTableLength <= fileLength &&
                            CheckScrcmdBlockMarkers((int)(offset - ScrcmdTableOffsetInBlock)))
                        {
                            return (int)offset; // Table position inside the expanded arm9 file
                        }
                    }
                }
            }
            catch
            {
                return -1;
            }
            return -1; // No table in expanded arm9 file
        }

        /// <summary>Whether the ARM9 command-count pointer already points at the moved block's count field.</summary>
        private static bool CheckScrcmdCommandCountPointerValid()
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
            }
            catch
            {
                return false;
            }
        }

        private static bool CheckScrcmdBlockMarkers(int blockOffset)
        {
            string expandedPath = Filesystem.expArmPath;
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

        /// <summary>Builds the moved block: COUN marker + command count + TABL marker + the vanilla command table.</summary>
        private static byte[] BuildCommandTablePayload()
        {
            byte[] originalTable = DSUtils.ReadFromFile(RomInfo.arm9Path, (uint)GetCustomScrcmdDBInt("originalTableOffset"), ScrcmdOriginalTableLength);
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

        private static uint ReadScrcmdCommandCount()
        {
            using (ARM9.Reader reader = new ARM9.Reader(GetCustomScrcmdDBInt("commandCountOffset")))
            {
                return reader.ReadUInt32();
            }
        }

        private static void RepointCommandTable(uint blockOffset, byte[] commandTablePayload)
        {
            string expandedPath = Filesystem.expArmPath;
            DSUtils.WriteToFile(expandedPath, commandTablePayload, blockOffset);

            using (ARM9.Writer wr = new ARM9.Writer())
            {
                wr.BaseStream.Position = GetCustomScrcmdDBInt("pointerOffset");
                wr.Write(synthOverlayLoadAddress + blockOffset + ScrcmdTableOffsetInBlock);

                wr.BaseStream.Position = GetCustomScrcmdDBInt("commandCountPointerOffset");
                wr.Write(synthOverlayLoadAddress + blockOffset + ScrcmdCountOffsetInBlock);
            }
        }

        // ── Patch catalogue / status (read-only, UI-agnostic) ────────────────────────────────────
        // Lets a non-WinForms shell (the Avalonia Patch Toolbox) list the patches, show each one's
        // applied/supported state, and apply it, mirroring the gating the WinForms constructor does.

        public enum PatchState { Available, Applied, Unsupported }

        public sealed class PatchInfo
        {
            public string Key;
            public string Title;
            public string Description;
            public PatchState State;
            public string Reason;       // shown for Unsupported (why) or Applied (optional note)
            public string ActionLabel;  // button caption when Available (defaults to "Apply")
            public string Author;       // credited beside the title
            public string Link;         // where to get a patch DSPRE can't apply itself
            public string Guide;        // further reading on what the patch changes
            public List<PatchPart> Parts;
        }

        /// <summary>One patch inside a group that another tool applies, such as hzla's PlatPatches.</summary>
        public sealed class PatchPart
        {
            public string Key;
            public string Title;
            public bool Applied;
            public string Note;
        }

        // Credited to whoever wrote the research or code, not to whoever added it to the toolbox.
        private static readonly Dictionary<string, (string Who, string What)> Credits = new Dictionary<string, (string, string)>
        {
            ["arm9"] = ("Mikelan98 and Nømura", "ARM9 expansion"),
            ["bdhcam"] = ("Mikelan98 and Trifindo", "dynamic cameras"),
            ["dynamicHeaders"] = ("Nømura", "dynamic map headers"),
            ["itemStandardize"] = ("Nømura", "item number standardization"),
            ["matrix"] = ("AdAstra", "Matrix 0 expansion"),
            ["scrcmdRepoint"] = ("AdAstra and MrHam88", "script command table repoint"),
            ["disableTextures"] = ("AdAstra", "disable texture animations"),
            ["sentenceCase"] = ("AdAstra", "sentence-case Pokémon names"),
            ["itemSentenceCase"] = ("Mixone", "sentence-case item names"),
            ["trainerNames"] = ("Mixone and AdAstra", "trainer name expansion"),
            ["sameHeldItemOdds"] = ("Mixone", "held item odds"),
            ["buildingRotation"] = ("MrHam88", "building rotation"),
            ["trainerShiny"] = ("MrHam88", "shiny trainer Pokémon"),
            ["trainerClassMetadata"] = ("MrHam88", "trainer class metadata"),
            ["owSpriteExpansion"] = ("hzla", "custom overworld sprites"),
            ["platItemExpansion"] = ("hzla", "item expansion"),
            ["platExtraTms"] = ("hzla", "extra TMs"),
            ["trainerClassTablesExpanded"] = ("Mixone", "trainer class table expansion"),
            ["trainerEncounterBgmRepointed"] = ("Mixone", "trainer encounter music repoint"),
            ["punchingMovesExpanded"] = ("DSPRE", "punching move list expansion"),
            ["soundMovesExpanded"] = ("DSPRE", "sound move list expansion"),
            ["vsIntroTimings"] = ("DSPRE", "VS intro timing"),
            ["typeChartExpanded"] = ("DSPRE", "type chart expansion"),
            ["swarmTableExpanded"] = ("DSPRE", "swarm table expansion"),
            ["bpShopExpanded"] = ("DSPRE", "Battle Point list expansion"),
            ["martsExpanded"] = ("DSPRE", "mart expansion"),
        };

        private const string MoveListResearch = "built on MrHam88's research and the DS Pokémon Hacking wiki's move editing guide (Yako and Lhea)";

        // What a patch was built on and how, as its author asked to be credited.
        private static readonly Dictionary<string, string> CreditSources = new Dictionary<string, string>
        {
            ["trainerClassMetadata"] = "built on the pokeheartgold decompilation and Mixone's Ghidra symbol map, and developed with AI assistance",
            ["punchingMovesExpanded"] = MoveListResearch,
            ["soundMovesExpanded"] = MoveListResearch,
        };

        /// <summary>The credit line for a patch, as the credits list writes it, or null.</summary>
        public static string CreditLine(string key)
        {
            if (!Credits.TryGetValue(key, out (string Who, string What) c)) return null;
            return CreditSources.TryGetValue(key, out string sources)
                ? $"{c.Who} for the {c.What} patch, {sources}"
                : $"{c.Who} for the {c.What} patch";
        }

        private static string CreditNote(string key) =>
            Credits.TryGetValue(key, out (string Who, string What) c)
                ? $"\n\nPlease credit {c.Who} if you use this patch." +
                  (CreditSources.TryGetValue(key, out string sources) ? $" It was {sources}." : "")
                : "";

        /// <summary>Credit keys of the applied patches and the found parts of patch groups.</summary>
        public static List<string> AppliedCreditKeys(IEnumerable<PatchInfo> statuses) =>
            statuses.Where(p => p.Parts == null && p.State == PatchState.Applied && p.Reason != AppliedByHgEngine).Select(p => p.Key)
                .Concat(statuses.Where(p => p.Parts != null).SelectMany(p => p.Parts).Where(part => part.Applied).Select(part => part.Key))
                .Where(Credits.ContainsKey)
                .ToList();

        /// <summary>Whether this project should still be offered credits after a save.</summary>
        public static bool CreditsOfferDue() =>
            !string.IsNullOrEmpty(RomInfo.workDir) && !SettingsManager.Settings.patchCreditsHandled.Contains(RomInfo.workDir);

        /// <summary>Stops the after-save credits offer for this project.</summary>
        public static void MarkCreditsHandled()
        {
            if (!CreditsOfferDue()) return;
            SettingsManager.Settings.patchCreditsHandled.Add(RomInfo.workDir);
            SettingsManager.Save();
        }

        /// <summary>Credits ready to paste into a hack's readme, one line per applied patch, then DSPRE.</summary>
        public static string CreditsText(IEnumerable<string> appliedKeys)
        {
            List<string> applied = new List<string>();
            List<string> detected = new List<string>();
            foreach (string key in appliedKeys)
                if (CreditLine(key) is string line)
                    (key is "owSpriteExpansion" or "platItemExpansion" or "platExtraTms" ? detected : applied).Add(line);

            List<string> lines = new List<string>
            {
                detected.Count == 0
                    ? "These credits were generated by DSPRE from the patches applied through it."
                    : "These credits were generated by DSPRE from the patches applied through it and the ones it detected."
            };
            lines.AddRange(applied);
            lines.AddRange(detected);
            lines.Add("The DSPRE developers and its many contributors and research gurus");
            return string.Join("\n", lines);
        }

        /// <summary>
        /// Computes the current status of every toolbox patch for the loaded ROM, without touching
        /// any WinForms control. Mirrors the enable/disable + Check* logic in the dialog constructor.
        /// Some checks (BDHCam) decompress an overlay as a side effect, same as the WinForms dialog.
        /// </summary>
        public static List<PatchInfo> GetPatchStatuses()
        {
            List<PatchInfo> list = new List<PatchInfo>();

            list.Add(Status("sameHeldItemOdds", "Same held items use the odds",
                "Wild Pokémon whose two held items are the same normally always hold it. With this patch they hold it only as often as the held item odds say.",
                SameHeldItemOddsState));

            list.Add(Status("sentenceCase", "Sentence-case Pokémon names",
                "Convert every Pokémon name from ALL-CAPS to Sentence Case, including names you've renamed yourself.",
                () => PatchState.Available));   // no reliable applied-detection

            list.Add(Status("itemSentenceCase", "Sentence-case item names",
                "Convert every Item name from ALL-CAPS to Sentence Case, including names you've renamed yourself.",
                () => PatchState.Available));   // no reliable applied-detection

            list.Add(Status("itemStandardize", "Standardize item numbers",
                "Rearrange item scripts into ascending index order and fix ground-item references.",
                () =>
                {
                    if (RomInfo.isHGE) return Unsupported("Unsupported on hg-engine ROMs");
                    DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.scripts });
                    bool applied = RomPatchState.flag_standardizedItems || CheckScriptsStandardizedItemNumbers();
                    return applied ? PatchState.Applied : PatchState.Available;
                }));

            PatchInfo punching = Status("punchingMovesExpanded", "Expand the punching move list",
                $"Moves the list of punching moves Iron Fist boosts into the expanded ARM9 area with room for {MoveCategoryTable.ExpandedCapacity}, so the Move Data editor can change which moves are punching. Requires the ARM9 expansion patch.",
                () => MoveListProbe(MoveCategoryTable.Kind.Punching));
            punching.Guide = MoveCategoryTable.GuideUrl(MoveCategoryTable.Kind.Punching);
            list.Add(punching);

            list.Add(Status("martsExpanded", "Expand the marts",
                $"Moves every mart into the expanded ARM9 area with room for {MartData.RoomyShops} marts of {MartData.RoomyItems} items, so the Mart Editor can add items and custom marts. Requires the ARM9 expansion patch.",
                MartExpansionProbe));

            list.Add(Status("bpShopExpanded", "Expand the Battle Point lists",
                $"Moves the Battle Point exchange's item and TM counters and their prices into the expanded ARM9 area with room for {BpShopData.MaxListItems} items each, so the Battle Point Shop editor can add more (Platinum). Requires the ARM9 expansion patch.",
                BpShopExpansionProbe));

            list.Add(Status("swarmTableExpanded", "Expand the swarm table",
                $"Moves the table of places a swarm can happen into the expanded ARM9 area with room for {SwarmTable.MaxRows} rows, so the Swarms editor can add more. Requires the ARM9 expansion patch.",
                SwarmTableExpansionProbe));

            list.Add(Status("typeChartExpanded", "Expand the type chart",
                $"Moves the type chart into the expanded ARM9 area with room for {TypeChart.ExpandedCapacity - 2} matchups, so the Type Chart editor can add more. Requires the ARM9 expansion patch.",
                TypeChartExpansionProbe));

            PatchInfo sound = Status("soundMovesExpanded", "Expand the sound move list",
                $"Moves the list of sound moves Soundproof blocks into the expanded ARM9 area with room for {MoveCategoryTable.ExpandedCapacity}, so the Move Data editor can change which moves are sound. The trainer AI's own sound list is not changed. Requires the ARM9 expansion patch.",
                () => MoveListProbe(MoveCategoryTable.Kind.Sound));
            sound.Guide = MoveCategoryTable.GuideUrl(MoveCategoryTable.Kind.Sound);
            list.Add(sound);

            list.Add(Status("arm9", "Expand ARM9 (synthetic overlay)",
                "Add ~88 KB of usable ARM9 memory. Required by the BDHCam / script-command patches. Advanced, can break the game if misused.",
                () =>
                {
                    if (RomInfo.isHGE) return Unsupported(HgEngine.HgEngineSyntheticOverlay.ToolboxReason);
                    if (!ARM9PatchData.arm9ExpansionCodeDB.ContainsKey("branchString" + "_" + RomInfo.gameFamily + "_" + RomInfo.gameLanguage))
                        return Unsupported("Unsupported language");
                    bool applied = RomPatchState.flag_arm9Expanded || CheckFilesArm9ExpansionApplied();
                    return applied ? PatchState.Applied : PatchState.Available;
                }));

            list.Add(Status("bdhcam", "Dynamic cameras (BDHCam)",
                "Install the BDHCam camera subroutine (Platinum / HGSS, EN or ES). Requires the ARM9 expansion patch first.",
                () =>
                {
                    if (RomInfo.isHGE) return Unsupported(HgEngine.HgEngineSyntheticOverlay.ToolboxReason);
                    if (!ScrcmdLikeLangOk()) return Unsupported("Unsupported version/language");
                    if (RomInfo.gameFamily == GameFamilies.HGSS && !RomInfo.IsDsRomProject) return Unsupported("Convert to ds-rom");
                    if (!Arm9Expanded()) return Unsupported("Requires ARM9 expansion");
                    bool applied = RomPatchState.flag_BDHCamPatchApplied || CheckFilesBDHCamPatchApplied();
                    return applied ? PatchState.Applied : PatchState.Available;
                }));

            list.Add(Status("buildingRotation", "Building rotation",
                "Enables the game to recognise the rotation of buildings placed in the Map Editor. Requires the ARM9 expansion patch and a ds-rom-format project.",
                () =>
                {
                    if (RomInfo.isHGE) return Unsupported(HgEngine.HgEngineSyntheticOverlay.ToolboxReason);
                    if (!RomInfo.IsDsRomProject) return Unsupported("Convert to ds-rom");
                    if (!BuildingRotationPatchData.SupportsCurrentRom()) return Unsupported("Unsupported version");
                    if (!Arm9Expanded()) return Unsupported("Requires ARM9 expansion");
                    bool applied = RomPatchState.flag_BuildingRotationPatchApplied || CheckFilesBuildingRotationPatchApplied();
                    return applied ? PatchState.Applied : PatchState.Available;
                }));

            list.Add(Status("trainerShiny", "Shiny trainer Pokémon",
                "Lets party members ticked Shiny in the Trainer editor battle shiny (US HeartGold/SoulSilver, Italian HeartGold). Requires the ARM9 expansion patch and a ds-rom-format project.",
                () =>
                {
                    if (RomInfo.isHGE) return Unsupported(HgEngine.HgEngineSyntheticOverlay.ToolboxReason);
                    if (!TrainerShinyPatch.SupportsCurrentRom) return Unsupported("Unsupported version");
                    if (!RomInfo.IsDsRomProject) return Unsupported("Convert to ds-rom");
                    if (!Arm9Expanded()) return Unsupported("Requires ARM9 expansion");
                    return TrainerShinyPatch.DetectCurrentProject() ? PatchState.Applied : PatchState.Available;
                }));

            list.Add(Status("trainerClassMetadata", "Trainer class metadata",
                "Gives every trainer class its own record for gender, prize, eye-contact and battle music and VS intro, edited in the Trainer Classes window and the VS intro editor (US HeartGold/SoulSilver). Requires the ARM9 expansion patch and a ds-rom-format project.",
                () =>
                {
                    if (RomInfo.isHGE) return Unsupported(HgEngine.HgEngineSyntheticOverlay.ToolboxReason);
                    if (!TrainerClassMetadataPatch.SupportsCurrentRom) return Unsupported("Unsupported version");
                    if (!RomInfo.IsDsRomProject) return Unsupported("Convert to ds-rom");
                    if (!Arm9Expanded()) return Unsupported("Requires ARM9 expansion");
                    return TrainerClassMetadataStore.DetectCurrentRom(out _) switch
                    {
                        TrainerClassMetadataDetectionState.SchemaV1 => PatchState.Applied,
                        TrainerClassMetadataDetectionState.Inconsistent => Unsupported("Partly applied"),
                        _ => PatchState.Available,
                    };
                }));

            list.Add(Status("vsIntroTimings", "VS intro timings per class",
                $"Lets each trainer class set its own VS intro timings (flashes, slides, holds) in the VS intro editor, for up to {VsIntroTimingAddon.Classes} classes. US HeartGold/SoulSilver with the trainer class metadata patch.",
                VsIntroTimingsProbe));

            list.Add(Status("dynamicHeaders", "Dynamic map headers",
                "Move the ARM9 header table into a NARC so headers are dynamically allocated (Platinum / HGSS).",
                () =>
                {
                    if (RomInfo.gameFamily == GameFamilies.DP) return Unsupported("Unsupported");
                    bool applied = RomPatchState.flag_DynamicHeadersPatchApplied || CheckFilesDynamicHeadersPatchApplied();
                    return applied ? PatchState.Applied : PatchState.Available;
                }));

            list.Add(Status("matrix", "Expand Matrix 0",
                "Allow Matrix 0 to be freely expanded up to twice its size (HGSS, EN or ES).",
                () =>
                {
                    if (RomInfo.gameFamily != GameFamilies.HGSS) return Unsupported("HGSS only");
                    if (RomInfo.gameLanguage != GameLanguages.English && RomInfo.gameLanguage != GameLanguages.Spanish)
                        return Unsupported("Unsupported language");
                    bool applied = RomPatchState.flag_MatrixExpansionApplied || CheckFilesMatrixExpansionApplied();
                    return applied ? PatchState.Applied : PatchState.Available;
                }));

            list.Add(Status("scrcmdRepoint", "Repoint script command table",
                "Move the script command table into the expanded ARM9 file so custom commands can be installed (HGSS, EN or ES). Requires the ARM9 expansion patch.",
                () =>
                {
                    if (RomInfo.isHGE) return Unsupported(HgEngine.HgEngineSyntheticOverlay.ToolboxReason);
                    if (!ScrcmdLikeLangOk() || RomInfo.gameFamily != GameFamilies.HGSS) return Unsupported("Unsupported version/language");
                    if (!Arm9Expanded()) return Unsupported("Requires ARM9 expansion");
                    return IsScrcmdRepointApplied() ? PatchState.Applied : PatchState.Available;
                }));

            list.Add(Status("disableTextures", "Disable dynamic textures",
                "Set the Dynamic Textures field of every AreaData to 0xFFFF, disabling texture animations (HGSS).",
                () => RomInfo.gameFamily == GameFamilies.HGSS ? PatchState.Available : Unsupported("Unsupported")));

            list.Add(Status("trainerNames", "Expand trainer-name length",
                $"Raise the trainer-name max length to {RomPatchState.expandedTrainerNameLength - 1} usable characters.",
                () =>
                {
                    if (RomPatchState.flag_TrainerNamesExpanded) return AppliedHere();
                    if (RomInfo.trainerNameLenOffset < 0) return Unsupported("Unsupported");
                    if (RomInfo.trainerNameMaxLen > TrainerFile.defaultNameLen)
                    {
                        RomPatchState.flag_TrainerNamesExpanded = true;
                        return AppliedHere();
                    }
                    return PatchState.Available;
                }));

            list.Add(PlatPatchesStatus());

            list.Add(Status("trainerClassTablesExpanded", "Trainer class tables",
                $"Moves the gender and prize money tables into the synthetic overlay with room for {TrainerClassTableExpansion.MaxClasses} classes, so the Trainer Classes editor can add classes. Requires the ARM9 expansion patch.",
                () =>
                {
                    if (!TrainerClassTableExpansion.IsSupportedForCurrentRom) return Unsupported("Platinum (English) only");
                    if (TrainerClassTableExpansion.ClassTablesHaveRoom) return PatchState.Applied;
                    return Arm9Expanded() ? PatchState.Available : Unsupported("Requires ARM9 expansion");
                }));

            list.Add(Status("trainerEncounterBgmRepointed", "Trainer encounter music table",
                $"Moves the eye-contact music table into the synthetic overlay with room for an entry per class, so the Trainer Classes editor can give more classes music. Requires the ARM9 expansion patch.",
                () =>
                {
                    if (TrainerClassTableExpansion.MusicTableHasRoom) return PatchState.Applied;
                    if (!TrainerClassTableExpansion.IsSupportedForCurrentRom) return Unsupported("Platinum (English) only");
                    return Arm9Expanded() ? PatchState.Available : Unsupported("Requires ARM9 expansion");
                }));

            return list;
        }

        // Applied by hzla's own tool; DSPRE only detects them.
        private static PatchInfo PlatPatchesStatus()
        {
            PatchInfo info = new PatchInfo
            {
                Key = "platPatches",
                Title = "PlatPatches",
                Author = "hzla",
                Link = "https://github.com/hzla/PlatPatches",
                Description = "Applied with hzla's own tool, not DSPRE. DSPRE finds them in the ROM and its editors use what they add.",
            };
            if (RomInfo.gameFamily != GameFamilies.Plat || RomInfo.isHGE)
            {
                info.State = PatchState.Unsupported;
                info.Reason = "Platinum only";
                return info;
            }
            info.Parts = new List<PatchPart>
            {
                Part("owSpriteExpansion", "Custom overworld sprites", () => OverworldSpriteTableExpansion.Detect()
                    ? $"{OverworldSpriteTableExpansion.UsedCount}/{OverworldSpriteTableExpansion.Capacity} custom slots used" : null),
                Part("platItemExpansion", "Item expansion", () => PlatPatches.Items() != null ? "" : null),
                Part("platExtraTms", "Extra TMs", () => PlatPatches.Tms() is { } t ? $"{t.Count} extra TMs" : null),
            };
            info.State = info.Parts.Any(p => p.Applied) ? PatchState.Applied : PatchState.Unsupported;
            return info;
        }

        // A null note means absent, and so does a detector that throws.
        private static PatchPart Part(string key, string title, Func<string> detect)
        {
            string note;
            try { note = detect(); }
            catch { note = null; }
            return new PatchPart { Key = key, Title = title, Applied = note != null, Note = note };
        }

        private static bool Arm9Expanded() => RomPatchState.flag_arm9Expanded || CheckFilesArm9ExpansionApplied();

        private static PatchState MartExpansionProbe()
        {
            if (HgEngine.HgEngineMarts.Enabled) return Unsupported("hg-engine keeps the marts in its own source");
            if (!RomInfo.IsMartEditorAvailable()) return Unsupported("Unsupported version");
            if (!Arm9Expanded()) return Unsupported("Requires ARM9 expansion");
            try { return MartData.LoadCurrent().HasRoom ? PatchState.Applied : PatchState.Available; }
            catch (Exception e) when (e is IOException || e is InvalidOperationException) { return Unsupported("The marts could not be read"); }
        }

        /// <summary>Moves the saved marts into the roomy layout, after backing up what it changes.</summary>
        public static bool ApplyMartExpansion()
        {
            MartData marts;
            try { marts = MartData.LoadCurrent(); }
            catch (Exception e) when (e is IOException || e is InvalidOperationException) { ShowError(e.Message, "Patch not applied"); return false; }
            if (AlreadyApplied(marts.HasRoom)) return false;
            if (PlacementNote(new[] { MartData.RoomyLength }, out int[] offsets) is not string placement) return false;
            if (!ConfirmYesNo($"This copies the common mart and every specialty mart into one block in the synthetic overlay with room for {MartData.RoomyShops} marts of " +
                $"{MartData.RoomyItems} items each, and points the ARM9's two mart pointers at it." + placement + "\n\n" +
                "Backups (" + BackupSuffix + ") are made of the ARM9 and the synthetic overlay first. It can't be removed by DSPRE; restore the backups to undo it." +
                "\n\nApply this patch?" + CreditNote("martsExpanded"), "Confirm to proceed"))
                return false;
            File.Copy(RomInfo.arm9Path, RomInfo.arm9Path + BackupSuffix, overwrite: true);
            File.Copy(Filesystem.expArmPath, Filesystem.expArmPath + BackupSuffix, overwrite: true);
            try { SyntheticOverlaySpace.PlaceAt(offsets, marts.MoveToRoomyLayout); }
            catch (Exception e) when (e is IOException || e is InvalidOperationException || e is UnauthorizedAccessException)
            {
                ShowError("The marts were not moved:\n" + e.Message, "Patch not applied");
                return false;
            }
            ShowInfo($"The marts now have room for {MartData.RoomyShops} marts of {MartData.RoomyItems} items each.", "Operation successful.");
            return true;
        }

        private static PatchState BpShopExpansionProbe()
        {
            if (RomInfo.isHGE || RomInfo.gameFamily != RomInfo.GameFamilies.Plat) return Unsupported("Platinum only");
            if (BpShopData.WhyNot() != null) return Unsupported("Unsupported version");
            if (!Arm9Expanded()) return Unsupported("Requires ARM9 expansion");
            try
            {
                BpShopData shop = BpShopData.Load();
                if (shop.MovedByPatch) return Unsupported("Moved by another patch");
                return shop.Expanded ? PatchState.Applied : PatchState.Available;
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is InvalidOperationException) { return Unsupported("The lists could not be read"); }
        }

        /// <summary>Moves the saved Battle Point lists into the expanded ARM9 area, after backing up what it changes.</summary>
        public static bool ApplyBpShopExpansion()
        {
            if (RomInfo.gameFamily != RomInfo.GameFamilies.Plat) { ShowError("The Battle Point lists can be expanded in Platinum only.", "Patch not applied"); return false; }
            BpShopData shop;
            try { shop = BpShopData.Load(); }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is InvalidOperationException) { ShowError(e.Message, "Patch not applied"); return false; }
            if (AlreadyApplied(shop.Expanded)) return false;
            if (PlacementNote(new[] { shop.ExpansionBlockLength }, out int[] offsets) is not string placement) return false;
            if (!ConfirmYesNo($"This copies the saved Battle Point counters and prices into their own block in the synthetic overlay with room for {BpShopData.MaxListItems} items on each counter " +
                $"and {BpShopData.MaxPriceRows} prices, and points the exchange code in the ARM9 and the price lookup in overlay 7 at it." + placement + "\n\n" +
                "Backups (" + BackupSuffix + ") are made of the ARM9, overlay 7 and the synthetic overlay first. It can't be removed by DSPRE; restore the backups to undo it." +
                "\n\nApply this patch?" + CreditNote("bpShopExpanded"), "Confirm to proceed"))
                return false;
            File.Copy(RomInfo.arm9Path, RomInfo.arm9Path + BackupSuffix, overwrite: true);
            File.Copy(OverlayUtils.GetPath(7), OverlayUtils.GetPath(7) + BackupSuffix, overwrite: true);
            File.Copy(Filesystem.expArmPath, Filesystem.expArmPath + BackupSuffix, overwrite: true);
            try { SyntheticOverlaySpace.PlaceAt(offsets, shop.MoveToExpansion); }
            catch (Exception e) when (e is IOException || e is InvalidOperationException || e is UnauthorizedAccessException)
            {
                ShowError("The Battle Point lists were not moved:\n" + e.Message, "Patch not applied");
                return false;
            }
            ShowInfo($"Each Battle Point counter can now hold {BpShopData.MaxListItems} items.", "Operation successful.");
            return true;
        }

        private static PatchState SwarmTableExpansionProbe()
        {
            if (RomInfo.isHGE) return Unsupported("hg-engine keeps the swarms in its own source");
            if (SwarmTable.WhyNot() != null) return Unsupported("Unsupported version");
            if (!Arm9Expanded()) return Unsupported("Requires ARM9 expansion");
            try { return SwarmTable.Load().Expanded ? PatchState.Applied : PatchState.Available; }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is InvalidOperationException) { return Unsupported("The table could not be read"); }
        }

        /// <summary>Moves the saved swarm table into the expanded ARM9 area, after backing up what it changes.</summary>
        public static bool ApplySwarmTableExpansion()
        {
            SwarmTable table;
            try { table = SwarmTable.Load(); }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is InvalidOperationException) { ShowError(e.Message, "Patch not applied"); return false; }
            if (AlreadyApplied(table.Expanded)) return false;
            string code = Path.GetFileName(table.CodePath);
            if (PlacementNote(new[] { table.ExpansionBlockLength }, out int[] offsets) is not string placement) return false;
            if (!ConfirmYesNo($"This copies the saved swarm table ({table.Rows.Count} rows) into its own block in the synthetic overlay with room for {SwarmTable.MaxRows}, " +
                $"and points the swarm code in {code} at it." + placement + "\n\nBackups (" + BackupSuffix + $") are made of {code} and the synthetic overlay first. " +
                "It can't be removed by DSPRE; restore the backups to undo it.\n\nApply this patch?" + CreditNote("swarmTableExpanded"), "Confirm to proceed"))
                return false;
            File.Copy(table.CodePath, table.CodePath + BackupSuffix, overwrite: true);
            File.Copy(Filesystem.expArmPath, Filesystem.expArmPath + BackupSuffix, overwrite: true);
            try { SyntheticOverlaySpace.PlaceAt(offsets, table.MoveToExpansion); }
            catch (Exception e) when (e is IOException || e is InvalidOperationException || e is UnauthorizedAccessException)
            {
                ShowError("The swarm table was not moved:\n" + e.Message, "Patch not applied");
                return false;
            }
            ShowInfo($"The swarm table can now hold {SwarmTable.MaxRows} rows.", "Operation successful.");
            return true;
        }

        private static PatchState TypeChartExpansionProbe()
        {
            if (RomInfo.isHGE) return Unsupported("hg-engine keeps the chart in its own source");
            if (TypeChart.WhyNot() != null) return Unsupported("Unsupported version");
            if (!Arm9Expanded()) return Unsupported("Requires ARM9 expansion");
            try { return TypeChart.Load().InExpansion ? PatchState.Applied : PatchState.Available; }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is InvalidOperationException) { return Unsupported("The chart could not be read"); }
        }

        /// <summary>Moves the saved type chart into the expanded ARM9 area, after backing up what it changes.</summary>
        public static bool ApplyTypeChartExpansion()
        {
            TypeChart chart;
            try { chart = TypeChart.Load(); }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is InvalidOperationException) { ShowError(e.Message, "Patch not applied"); return false; }
            if (AlreadyApplied(chart.InExpansion)) return false;
            if (PlacementNote(new[] { TypeChart.ExpansionBlockLength }, out int[] offsets) is not string placement) return false;
            if (!ConfirmYesNo($"This copies the saved type chart into its own block in the synthetic overlay with room for {TypeChart.ExpandedCapacity - 2} matchups, " +
                "points the battle code at it and raises Conversion 2's count to match"
                + (RomInfo.gameFamily == RomInfo.GameFamilies.HGSS ? "." : ", and writes the chart into the Pokétch's copy.") + placement + "\n\n" +
                "Backups (" + BackupSuffix + ") are made of the battle overlay and the synthetic overlay first. It can't be removed by DSPRE; " +
                "restore the backups to undo it." + "\n\nApply this patch?" + CreditNote("typeChartExpanded"), "Confirm to proceed"))
                return false;
            File.Copy(chart.CodePath, chart.CodePath + BackupSuffix, overwrite: true);
            File.Copy(Filesystem.expArmPath, Filesystem.expArmPath + BackupSuffix, overwrite: true);
            try { SyntheticOverlaySpace.PlaceAt(offsets, chart.MoveToExpansion); }
            catch (Exception e) when (e is IOException || e is InvalidOperationException || e is UnauthorizedAccessException)
            {
                ShowError((chart.InExpansion ? "The type chart moved, but the Pokétch copy wasn't updated:" : "The type chart was not moved:") + "\n" + e.Message, "Patch not applied");
                if (!chart.InExpansion) return false;
            }
            ShowInfo("The type chart can now hold more matchups.", "Operation successful.");
            return true;
        }

        private static PatchState VsIntroTimingsProbe()
        {
            if (RomInfo.isHGE) return Unsupported(HgEngine.HgEngineSyntheticOverlay.ToolboxReason);
            if (RomInfo.gameLanguage != RomInfo.GameLanguages.English || (RomInfo.romID != "IPKE" && RomInfo.romID != "IPGE"))
                return Unsupported("Unsupported version");
            if (!Arm9Expanded()) return Unsupported("Requires ARM9 expansion");
            if (TrainerClassMetadataPatch.InstalledOffset() == null) return Unsupported("Requires trainer class metadata");
            if (VsIntroTimingAddon.WhyNot() is string why) return Unsupported(why.TrimEnd('.'));
            return VsIntroTimingAddon.Load() != null ? PatchState.Applied : PatchState.Available;
        }

        /// <summary>Places the per-class timing table and hooks the intro code, after backing up what it changes.</summary>
        public static bool ApplyVsIntroTimings()
        {
            if (VsIntroTimingAddon.WhyNot() is string why) { ShowError(why, "Patch not applied"); return false; }
            if (AlreadyApplied(VsIntroTimingAddon.Load() != null)) return false;
            if (PlacementNote(new[] { VsIntroTimingAddon.BlockLength }, out int[] offsets) is not string placement) return false;
            if (!ConfirmYesNo("This places a small helper and a table of eight timings for each of " + VsIntroTimingAddon.Classes +
                " trainer classes in the synthetic overlay, and points " + VsIntroTimingAddon.HookCount + " places in overlays 115, 117, 118 and 119 " +
                "at it. A timing of 0 keeps the game's own value." + placement + "\n\nBackups (" + BackupSuffix + ") are made of those overlays and the synthetic " +
                "overlay first. It can't be removed by DSPRE; restore the backups to undo it.\n\nApply this patch?" + CreditNote("vsIntroTimings"),
                "Confirm to proceed"))
                return false;
            foreach (int overlay in VsIntroTimingAddon.HookedOverlays)
            {
                string path = VsIntroTimingAddon.OverlayFilePath(overlay);
                File.Copy(path, path + BackupSuffix, overwrite: true);
            }
            File.Copy(Filesystem.expArmPath, Filesystem.expArmPath + BackupSuffix, overwrite: true);
            try { SyntheticOverlaySpace.PlaceAt(offsets, () => VsIntroTimingAddon.Install()); }
            catch (Exception e) when (e is InvalidOperationException || e is IOException || e is UnauthorizedAccessException)
            {
                ShowError("The VS intro timings were not added:\n" + e.Message, "Patch not applied");
                return false;
            }
            ShowInfo("Each trainer class can now set its own VS intro timings in the VS intro editor.", "Operation successful.");
            return true;
        }

        private static PatchState MoveListProbe(MoveCategoryTable.Kind kind)
        {
            if (RomInfo.isHGE) return Unsupported(HgEngine.HgEngineSyntheticOverlay.ToolboxReason);
            if (MoveCategoryTable.WhyNot(kind) != null) return Unsupported("Unsupported version");
            if (!Arm9Expanded()) return Unsupported("Requires ARM9 expansion");
            return MoveCategoryTable.IsExpanded(kind) ? PatchState.Applied : PatchState.Available;
        }

        /// <summary>Moves a punching or sound move list into the expanded ARM9 area, keeping its moves.</summary>
        public static bool ApplyMoveListExpansion(MoveCategoryTable.Kind kind)
        {
            string key = kind == MoveCategoryTable.Kind.Punching ? "punchingMovesExpanded" : "soundMovesExpanded";
            string name = MoveCategoryTable.NameOf(kind);
            if (MoveCategoryTable.WhyNot(kind) is string why) { ShowError(why, "Patch not applied"); return false; }
            if (AlreadyApplied(MoveCategoryTable.IsExpanded(kind))) return false;
            if (PlacementNote(new[] { MoveCategoryTable.ExpansionBlockLength }, out int[] offsets) is not string placement) return false;
            if (!ConfirmYesNo($"The list of {name} moves will move into the expanded ARM9 area with room for {MoveCategoryTable.ExpandedCapacity} moves, " +
                    "and the battle code will be pointed at it. Its moves stay as they are." + placement + "\n\n" +
                    "Backups (" + BackupSuffix + ") are made of the battle overlay and the synthetic overlay first. It can't be removed by DSPRE; restore the backups to undo it.\n\n" +
                    "Do you wish to continue?" + CreditNote(key), "Confirm to proceed"))
            {
                ShowInfo("No changes have been made.", "Operation canceled");
                return false;
            }
            try
            {
                MoveCategoryTable table = MoveCategoryTable.Load(kind);
                BackUp(table.CodePath, Filesystem.expArmPath);
                SyntheticOverlaySpace.PlaceAt(offsets, table.MoveToExpansion);
                ShowInfo($"The {name} move list now has room for {MoveCategoryTable.ExpandedCapacity} moves. Mark moves in the Move Data editor.", "Success");
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is InvalidOperationException || ex is UnauthorizedAccessException)
            {
                ShowError($"The {name} move list was not moved:\n" + ex.Message, "Patch not applied");
                return false;
            }
        }

        /// <summary>Keeps a .backup copy of each file a patch is about to change.</summary>
        private static void BackUp(params string[] paths)
        {
            foreach (string path in paths)
                if (File.Exists(path)) File.Copy(path, path + BackupSuffix, overwrite: true);
        }

        /// <summary>
        /// Works out where a relocation's blocks go and words it for the confirm, so the range is shown before anything is
        /// placed. Null, after an error, when there is no room.
        /// </summary>
        private static string PlacementNote(int[] lengths, out int[] offsets)
        {
            offsets = SyntheticOverlaySpace.Available() ? SyntheticOverlaySpace.Plan(lengths) : null;
            if (offsets == null)
            {
                ShowError(SyntheticOverlaySpace.Available() ? "There isn't enough free space left in the synthetic overlay for this patch."
                          : "Apply the ARM9 expansion in the ROM Patch Toolbox first.", "Patch not applied");
                return null;
            }
            int[] at = offsets;
            IEnumerable<string> ranges = at.Select((start, i) => $"0x{start:X} to 0x{start + lengths[i] - 1:X}");
            return "\n\nIt takes " + string.Join(" and ", ranges) + " in the synthetic overlay, the first free space no other patch or DSPRE table uses.";
        }

        // Ends a code patch's confirm with what it backs up and how to undo it.
        private static string BackupNote(string files) =>
            "\n\nBackups (" + BackupSuffix + ") are made of " + files + " first. It can't be removed by DSPRE; restore the backups to undo it.";

        private static bool AlreadyApplied(bool applied)
        {
            if (applied) ShowInfo("This patch has already been applied.", "Can't reapply patch");
            return applied;
        }

        // A check that can't read its files counts as not applied, like the status probes.
        private static bool Probe(Func<bool> check)
        {
            try { return check(); }
            catch { return false; }
        }

        // Language/version gate shared by BDHCam and the script-command patches (Plat/HGSS, EN or ES).
        private static bool ScrcmdLikeLangOk() =>
            (RomInfo.gameFamily == GameFamilies.HGSS || RomInfo.gameFamily == GameFamilies.Plat)
            && (RomInfo.gameLanguage == GameLanguages.English || RomInfo.gameLanguage == GameLanguages.Spanish);

        // Small helpers so GetPatchStatuses stays declarative and a single check throwing can't
        // take down the whole catalogue (a status probe should never be fatal).
        [ThreadStatic] private static string _reason_text;
        private static PatchState Unsupported(string reason) { _reason_text = reason; return PatchState.Unsupported; }

        public const string AppliedByHgEngine = "Applied by hg-engine";

        // hg-engine raises the trainer-name limit and moves the eye-contact music table itself, so on its ROMs
        // these patches are its doing and earn no credit here.
        private static PatchState AppliedHere()
        {
            if (RomInfo.isHGE) _reason_text = AppliedByHgEngine;
            return PatchState.Applied;
        }

        private static PatchInfo Status(string key, string title, string desc, Func<PatchState> probe, string actionLabel = null)
        {
            PatchInfo info = new PatchInfo { Key = key, Title = title, Description = desc, ActionLabel = actionLabel,
                                     Author = Credits.TryGetValue(key, out (string Who, string What) credit) ? credit.Who : null };
            try
            {
                _reason_text = null;
                info.State = probe();
                // A probe can set _reason_text itself (e.g. via Unsupported(), or directly for an
                // optional Applied-state note); Unsupported falls back to a generic label if it didn't.
                info.Reason = _reason_text ?? (info.State == PatchState.Unsupported ? "Unsupported" : null);
            }
            catch (Exception ex)
            {
                info.State = PatchState.Unsupported;
                info.Reason = "Unavailable (" + ex.GetType().Name + ")";
            }
            return info;
        }

        /// <summary>Applies the patch identified by <paramref name="key"/>. Returns whether it was applied.</summary>
        public static bool ApplyByKey(string key)
        {
            string hgEngineRefusal = HgEngine.HgEngineSyntheticOverlay.ExpansionRefusal();
            if (hgEngineRefusal != null && key is "arm9" or "bdhcam" or "buildingRotation" or "scrcmdRepoint"
                or "trainerClassTablesExpanded" or "trainerEncounterBgmRepointed" or "punchingMovesExpanded" or "soundMovesExpanded")
            {
                ShowError(hgEngineRefusal, "Not available on hg-engine");
                return false;
            }

            switch (key)
            {
                case "sentenceCase": return ApplySentenceCasePatch();
                case "itemSentenceCase": return ApplyItemSentenceCasePatch();
                case "itemStandardize": return ApplyItemStandardizePatch();
                case "arm9": return ApplyARM9ExpansionPatch();
                case "bdhcam": return ApplyBDHCamPatch();   // caller re-queries statuses afterwards
                case "buildingRotation": return ApplyBuildingRotationPatch();
                case "trainerShiny": return ApplyTrainerShinyPatch();
                case "trainerClassMetadata": return ApplyTrainerClassMetadataPatch();
                case "dynamicHeaders": return ApplyDynamicHeadersPatch();
                case "matrix": return ApplyMatrixExpansionPatch();
                case "scrcmdRepoint": return ApplyScrcmdRepointPatch();
                case "disableTextures": return ApplyDisableDynamicTexturesPatch();
                case "trainerNames": return ApplyExpandTrainerNamesPatch();
                case "sameHeldItemOdds": return ApplySameHeldItemOddsPatch();
                case "trainerClassTablesExpanded": return ApplyMoveTrainerClassTables();
                case "trainerEncounterBgmRepointed": return ApplyMoveEncounterMusicTable();
                case "punchingMovesExpanded": return ApplyMoveListExpansion(MoveCategoryTable.Kind.Punching);
                case "soundMovesExpanded": return ApplyMoveListExpansion(MoveCategoryTable.Kind.Sound);
                case "vsIntroTimings": return ApplyVsIntroTimings();
                case "typeChartExpanded": return ApplyTypeChartExpansion();
                case "swarmTableExpanded": return ApplySwarmTableExpansion();
                case "bpShopExpanded": return ApplyBpShopExpansion();
                case "martsExpanded": return ApplyMartExpansion();
                default: return false;
            }
        }
    }
}

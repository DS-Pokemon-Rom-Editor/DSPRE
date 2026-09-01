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
            string expandedPath = Path.Combine(RomInfo.gameDirs[DirNames.synthOverlay].unpackedDir, "0000");
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

        /// <summary>Convert every Pokémon name to Sentence Case, including names the user renamed themselves. Always supported.</summary>
        public static bool ApplySentenceCasePatch()
        {
            if (!ConfirmYesNo("Confirming this process will apply the following changes:\n\n" +
                "- Every Pokémon name will be converted to Sentence Case, including names you've renamed yourself.\n" +
                "- Any other text (trainer dialogue, item descriptions, etc) mentioning a renamed Pokémon will be updated to match." + "\n\n" +
                "Do you wish to continue?", "Confirm to proceed"))
            {
                ShowInfo("No changes have been made.", "Operation canceled");
                return false;
            }

            var renamePairs = new List<(string searchString, string replaceString, bool caseSensitive)>();

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

                    string sentenceCased = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(current.ToLower());
                    if (sentenceCased != current)
                    {
                        pokeName.messages[i] = sentenceCased;
                        renamePairs.Add((current, sentenceCased, false));
                    }
                }
                pokeName.SaveToExpandedDir(ID, showSuccessMessage: false);
            }

            int archivesUpdated = renamePairs.Count > 0 ? DSUtils.ReplaceTextEverywhere(renamePairs) : 0;
            ShowInfo($"Pokémon names have been converted to Sentence Case.\nOther text banks updated: {archivesUpdated}", "Operation successful");
            return true;
        }

        /// <summary>Convert every Item name to Sentence Case, including names the user renamed themselves. Always supported.</summary>
        public static bool ApplyItemSentenceCasePatch()
        {
            if (!ConfirmYesNo("Confirming this process will apply the following changes:\n\n" +
                "- Every Item name will be converted to Sentence Case, including names you've renamed yourself.\n" +
                "- Any other text (trainer dialogue, script text, etc) mentioning a renamed Item will be updated to match." + "\n\n" +
                "Do you wish to continue?", "Confirm to proceed"))
            {
                ShowInfo("No changes have been made.", "Operation canceled");
                return false;
            }

            var renamePairs = new List<(string searchString, string replaceString, bool caseSensitive)>();

            TextArchive itemNames = new TextArchive(RomInfo.itemNamesTextNumber);
            for (int i = 1; i < itemNames.messages.Count; i++)
            {
                string current = itemNames.messages[i];
                if (string.IsNullOrEmpty(current))
                {
                    continue;
                }

                string sentenceCased = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(current.ToLower());
                if (sentenceCased != current)
                {
                    itemNames.messages[i] = sentenceCased;
                    renamePairs.Add((current, sentenceCased, false));
                }
            }
            itemNames.SaveToExpandedDir(RomInfo.itemNamesTextNumber, showSuccessMessage: false);

            int archivesUpdated = renamePairs.Count > 0 ? DSUtils.ReplaceTextEverywhere(renamePairs) : 0;
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
            string expandedCheckPath = Path.Combine(RomInfo.gameDirs[DirNames.synthOverlay].unpackedDir, "0000");
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
            "Do you wish to continue?", "Confirm to proceed"))
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
            string expandedPath = Path.Combine(RomInfo.gameDirs[DirNames.synthOverlay].unpackedDir, "0000");
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
            string expandedPath = Path.Combine(RomInfo.gameDirs[DirNames.synthOverlay].unpackedDir, "0000");
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
                "Do you wish to continue?", "Confirm to proceed"))
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
                "- Any unsaved change to the current Event File will be discarded.\n\n", "Confirm to proceed"))
            {
                ShowInfo("No changes have been made.", "Operation canceled");
                return false;
            }

            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.scripts });
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.eventFiles });

            if (RomPatchState.flag_standardizedItems)
            {
                ShowInfo("This patch has already been applied.", "Can't reapply patch");
                return false;
            }

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
            ARM9PatchData data = new ARM9PatchData();

            if (!ConfirmYesNo("Confirming this process will apply the following changes:\n\n" +
                    "- Backup ARM9 file (arm9.bin" + BackupSuffix + " will be created)." + "\n\n" +
                    "- Replace " + (data.branchString.Length / 3 + 1) + " bytes of data at arm9 offset 0x" + data.branchOffset.ToString("X") + " with " + '\n' + data.branchString + "\n\n" +
                    "- Replace " + (data.initString.Length / 3 + 1) + " bytes of data at arm9 offset 0x" + data.initOffset.ToString("X") + " with " + '\n' + data.initString + "\n\n" +
                    "- Modify file #" + RomPatchState.expandedARMfileID + " inside " + '\n' + RomInfo.gameDirs[DirNames.synthOverlay].unpackedDir + '\n' + " to accommodate for 88KB of data (no backup)." + "\n\n" +
                    "If you do not understand the implications of these changes and how they can affect your game do NOT continue. You can and will break the game if you do not know what you are doing here.\n\n" +
                    "Do you wish to continue?", "Confirm to proceed"))
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
                listOfChanges +
                "Do you wish to continue?", "Confirm to proceed"))
            {
                ShowInfo("No changes have been made.", "Operation canceled");
                return false;
            }

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
            }
            // NOTE: preserving original behaviour, the patch is marked applied even if the write threw.
            RomPatchState.flag_MatrixExpansionApplied = true;
            ShowInfo("Matrix 0 can now be freely expanded up to twice its size.", "Operation successful.");
            return true;
        }

        /// <summary>Dynamically allocate map headers in memory (Plat/HGSS).</summary>
        public static bool ApplyDynamicHeadersPatch()
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

            if (!ConfirmYesNo("Confirming this process will apply the following changes:\n\n" +
                "- Backup ARM9 file (arm9.bin" + BackupSuffix + " will be created)." + "\n\n" +
                "- NARC file at " + headersDir.packedDir + " will become the new header container." + "\n\n" +
                "- The default ARM9 header table will be split into multiple files (one per header), each one saved into NARC" + headersDir.packedDir + " upon saving the ROM." + "\n\n" +
                "- Replace " + (data.initString.Length / 3 + 1) + " bytes of data at arm9 offset 0x" + data.initOffset.ToString("X") + " with " + '\n' + data.initString + "\n\n" +
                "- Neutralize instances of (HeaderID * 0x18) so the base offset which the data is read from is always 0x0." + "\n\n" +
                "- Change pointers to header fields, from(ARM9_HEADER_TABLE_OFFSET + n) to simply(0 + n)" + "\n\n" +
                specialCaseChanges +
                "Do you wish to continue?", "Confirm to proceed"))
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

        /// <summary>Set the Dynamic Textures field of every AreaData to 0xFFFF (HGSS).</summary>
        public static bool ApplyDisableDynamicTexturesPatch()
        {
            if (!ConfirmYesNo("Applying this patch will set the Dynamic Textures field of all AreaData files to 0xFFFF.\n\n" +
                "Are you sure you want to proceed?", "Confirm to proceed"))
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
            if (!ConfirmYesNo($"Applying this patch will set the Trainer Name max length to {RomPatchState.expandedTrainerNameLength - 1} usable characters.\n" +
                "Are you sure you want to proceed?", "Confirm to proceed"))
            {
                ShowInfo("No changes have been made.", "Operation canceled");
                return false;
            }

            try
            {
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
            string expandedPath = Path.Combine(RomInfo.gameDirs[DirNames.synthOverlay].unpackedDir, "0000");
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
                "Do you wish to continue?", "Confirm to proceed"))
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
                    string expandedPath = Path.Combine(RomInfo.gameDirs[DirNames.synthOverlay].unpackedDir, "0000");
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
            string expandedPath = Path.Combine(RomInfo.gameDirs[DirNames.synthOverlay].unpackedDir, "0000");
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
            string expandedPath = Path.Combine(RomInfo.gameDirs[DirNames.synthOverlay].unpackedDir, "0000");
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
        }

        /// <summary>
        /// Computes the current status of every toolbox patch for the loaded ROM, without touching
        /// any WinForms control. Mirrors the enable/disable + Check* logic in the dialog constructor.
        /// Some checks (BDHCam) decompress an overlay as a side effect, same as the WinForms dialog.
        /// </summary>
        public static List<PatchInfo> GetPatchStatuses()
        {
            var list = new List<PatchInfo>();

            list.Add(Status("sentenceCase", "Sentence-case Pokémon names",
                "Convert every Pokémon name from ALL-CAPS to Sentence Case, including names you've renamed yourself.",
                () => PatchState.Available));   // no reliable applied-detection

            list.Add(Status("itemSentenceCase", "Sentence-case Item names",
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

            list.Add(Status("arm9", "Expand ARM9 (synthetic overlay)",
                "Add ~88 KB of usable ARM9 memory. Required by the BDHCam / script-command patches. Advanced, can break the game if misused.",
                () =>
                {
                    if (!ARM9PatchData.arm9ExpansionCodeDB.ContainsKey("branchString" + "_" + RomInfo.gameFamily + "_" + RomInfo.gameLanguage))
                        return Unsupported("Unsupported language");
                    bool applied = RomPatchState.flag_arm9Expanded || CheckFilesArm9ExpansionApplied();
                    return applied ? PatchState.Applied : PatchState.Available;
                }));

            list.Add(Status("bdhcam", "Dynamic Cameras (BDHCam)",
                "Install the BDHCam camera subroutine (Platinum / HGSS, EN or ES). Requires the ARM9 expansion patch first.",
                () =>
                {
                    if (!ScrcmdLikeLangOk()) return Unsupported("Unsupported version/language");
                    if (RomInfo.gameFamily == GameFamilies.HGSS && !RomInfo.IsDsRomProject) return Unsupported("Convert to ds-rom");
                    if (!Arm9Expanded()) return Unsupported("Requires ARM9 expansion");
                    bool applied = RomPatchState.flag_BDHCamPatchApplied || CheckFilesBDHCamPatchApplied();
                    return applied ? PatchState.Applied : PatchState.Available;
                }));

            list.Add(Status("buildingRotation", "Building Rotation",
                "Enables the game to recognise the rotation of buildings placed in the Map Editor. Requires the ARM9 expansion patch and a ds-rom-format project.",
                () =>
                {
                    if (!RomInfo.IsDsRomProject) return Unsupported("Convert to ds-rom");
                    if (!BuildingRotationPatchData.SupportsCurrentRom()) return Unsupported("Unsupported version");
                    if (!Arm9Expanded()) return Unsupported("Requires ARM9 expansion");
                    bool applied = RomPatchState.flag_BuildingRotationPatchApplied || CheckFilesBuildingRotationPatchApplied();
                    return applied ? PatchState.Applied : PatchState.Available;
                }));

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
                    if (RomPatchState.flag_TrainerNamesExpanded) return PatchState.Applied;
                    if (RomInfo.trainerNameLenOffset < 0) return Unsupported("Unsupported");
                    if (RomInfo.trainerNameMaxLen > TrainerFile.defaultNameLen)
                    {
                        RomPatchState.flag_TrainerNamesExpanded = true;
                        return PatchState.Applied;
                    }
                    return PatchState.Available;
                }));

            list.Add(Status("owSpriteExpansion", "Custom Overworld Sprites (hzla PlatPatches)",
                "Detects hzla's PlatPatches \"overworld sprites\" expansion (github.com/hzla/PlatPatches), which relocates and expands the field-object tables to allow custom overworld appearance IDs. DSPRE only detects this patch, it is applied externally by that tool, not by DSPRE.",
                () =>
                {
                    if (RomInfo.gameFamily != GameFamilies.Plat) return Unsupported("Platinum only");
                    if (!OverworldSpriteTableExpansion.Detect())
                        return Unsupported("Not detected: apply via hzla's PlatPatches tool (github.com/hzla/PlatPatches); DSPRE does not apply this patch itself.");
                    _reason_text = $"{OverworldSpriteTableExpansion.UsedCount}/{OverworldSpriteTableExpansion.Capacity} custom slots used";
                    return PatchState.Applied;
                }));

            list.Add(Status("trainerClassTablesExpanded", "Trainer Class Tables Expanded (gender / prize money)",
                "Whether the trainer-class gender and prize-money-multiplier tables have been repointed into the synthetic overlay, either by DSPRE's own \"Add Trainer Class\" or by hand (per the community write-up on adding a new trainer class). Platinum (English) only, since these tables have no bounds checking, so DSPRE won't touch them anywhere else.",
                () =>
                {
                    if (!TrainerClassTableExpansion.IsSupportedForCurrentRom) return Unsupported("Platinum (English) only");
                    TrainerClassTableExpansion.Detect();
                    bool applied = TrainerClassTableExpansion.IsGenderTableRepointed && TrainerClassTableExpansion.IsPrizeMulTableRepointed;
                    if (!applied)
                    {
                        return Unsupported(TrainerClassTableExpansion.IsGenderTableRepointed || TrainerClassTableExpansion.IsPrizeMulTableRepointed
                            ? "Only one of the two tables has been expanded so far. Add a trainer class in the Trainer Editor to finish the other."
                            : "Not detected. Use \"Add Trainer Class\" in the Trainer Editor, or repoint by hand.");
                    }
                    return PatchState.Applied;
                }));

            list.Add(Status("trainerEncounterBgmRepointed", "Trainer Encounter Music Table Repointed",
                "Whether the trainer-class \"eye contact\" encounter-music table has been repointed into the synthetic overlay (by hand, or by DSPRE's own \"Add Trainer Class\"). DSPRE's Trainer Editor already reads/writes this table correctly either way, this row is just visibility into which location is in use.",
                () => TrainerClassTableExpansion.DetectMusicTableRepointed() ? PatchState.Applied : Unsupported("Not repointed. This ROM's trainer-class music table is still at its original location, which is completely normal.")));

            return list;
        }

        private static bool Arm9Expanded() => RomPatchState.flag_arm9Expanded || CheckFilesArm9ExpansionApplied();

        // Language/version gate shared by BDHCam and the script-command patches (Plat/HGSS, EN or ES).
        private static bool ScrcmdLikeLangOk() =>
            (RomInfo.gameFamily == GameFamilies.HGSS || RomInfo.gameFamily == GameFamilies.Plat)
            && (RomInfo.gameLanguage == GameLanguages.English || RomInfo.gameLanguage == GameLanguages.Spanish);

        // Small helpers so GetPatchStatuses stays declarative and a single check throwing can't
        // take down the whole catalogue (a status probe should never be fatal).
        [ThreadStatic] private static string _reason_text;
        private static PatchState Unsupported(string reason) { _reason_text = reason; return PatchState.Unsupported; }

        private static PatchInfo Status(string key, string title, string desc, Func<PatchState> probe, string actionLabel = null)
        {
            var info = new PatchInfo { Key = key, Title = title, Description = desc, ActionLabel = actionLabel };
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
            switch (key)
            {
                case "sentenceCase": return ApplySentenceCasePatch();
                case "itemSentenceCase": return ApplyItemSentenceCasePatch();
                case "itemStandardize": return ApplyItemStandardizePatch();
                case "arm9": return ApplyARM9ExpansionPatch();
                case "bdhcam": return ApplyBDHCamPatch();   // caller re-queries statuses afterwards
                case "buildingRotation": return ApplyBuildingRotationPatch();
                case "dynamicHeaders": return ApplyDynamicHeadersPatch();
                case "matrix": return ApplyMatrixExpansionPatch();
                case "scrcmdRepoint": return ApplyScrcmdRepointPatch();
                case "disableTextures": return ApplyDisableDynamicTexturesPatch();
                case "trainerNames": return ApplyExpandTrainerNamesPatch();
                default: return false;
            }
        }
    }
}

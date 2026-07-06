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
    /// identical patch code (no ROM-writing divergence). Core — no UI-toolkit dependency.
    ///
    /// All user prompts go through the pluggable <see cref="ConfirmYesNo"/> / <see cref="ShowInfo"/> /
    /// <see cref="ShowError"/> / <see cref="PickCustomCommandFile"/> hooks (defaults route through
    /// <see cref="AppMessages"/>; each shell installs its own dialogs — WinForms via
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
        /// <summary>Pick a <c>.scrcmd</c> custom-command file. Returns null if cancelled (or headless).</summary>
        public static Func<string> PickCustomCommandFile = () => null;

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
            BDHCAMPatchData data = new BDHCAMPatchData();

            byte[] branchCode = DSUtils.HexStringToByteArray(data.branchString);
            byte[] branchCodeRead = ARM9.ReadBytes(data.branchOffset, branchCode.Length);

            if (branchCode.Length != branchCodeRead.Length || !branchCode.SequenceEqual(branchCodeRead))
            {
                return false;
            }

            string overlayFilePath = OverlayUtils.GetPath(data.overlayNumber);
            OverlayUtils.Decompress(data.overlayNumber);

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

        public static bool ConfigureOverlay1Uncompressed()
        {
            bool isCompressed = false;
            string stringDecompressOverlay = "";

            if (OverlayUtils.IsCompressed(1))
            {
                isCompressed = true;
                stringDecompressOverlay = "- Overlay 1 will be decompressed.\n\n";
            }

            if (ConfirmYesNo("This process will apply the following changes:\n\n" +
                stringDecompressOverlay +
                "- Overlay 1 will be configured as \"uncompressed\" in the overlay table.\n\n" +
                "Do you wish to continue?", "Confirm to proceed"))
            {
                OverlayUtils.OverlayTable.SetDefaultCompressed(1, false);
                if (isCompressed)
                {
                    OverlayUtils.Decompress(1);
                }

                ShowInfo("Overlay1 is now configured as uncompressed.", "Operation successful");
                return true;
            }
            else
            {
                ShowInfo("No changes have been made.", "Operation canceled");
                return false;
            }
        }

        // ── Patch apply-methods ──────────────────────────────────────────────────────────────────

        /// <summary>Convert every Pokémon name to Sentence Case. Always supported.</summary>
        public static bool ApplySentenceCasePatch()
        {
            if (!ConfirmYesNo("Confirming this process will apply the following changes:\n\n" +
                "- Every Pokémon name will be converted to Sentence Case." + "\n\n" +
                "Do you wish to continue?", "Confirm to proceed"))
            {
                ShowInfo("No changes have been made.", "Operation canceled");
                return false;
            }

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
            ShowInfo("Pokémon names have been converted to Sentence Case.", "Operation successful");
            return true;
        }

        /// <summary>
        /// Apply the BDHCam routine (Plat/HGSS EN/ES). <paramref name="onOverlay1Configured"/> is
        /// invoked if the user accepts the "configure Overlay1 uncompressed first" recommendation and
        /// it succeeds, so the caller can refresh the Overlay1 patch UI (mirrors the old WinForms flow).
        /// </summary>
        public static bool ApplyBDHCamPatch(Action onOverlay1Configured)
        {
            BDHCAMPatchData data = new BDHCAMPatchData();

            if (RomInfo.gameFamily == GameFamilies.HGSS)
            {
                if (OverlayUtils.OverlayTable.IsDefaultCompressed(data.overlayNumber))
                {
                    if (ConfirmYesNo("It is STRONGLY recommended to configure Overlay1 as uncompressed before proceeding.\n\n" +
                        "More details in the following dialog.\n\n" + "Do you want to know more?", "Confirm to proceed"))
                    {
                        if (ConfigureOverlay1Uncompressed())
                        {
                            onOverlay1Configured?.Invoke();
                        }
                    }
                }
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
            string ov5path = OverlayUtils.GetPath(5);
            File.Copy(ov5path, ov5path + BackupSuffix, overwrite: true);

            try
            {
                ARM9.WriteBytes(DSUtils.HexStringToByteArray(data.branchString), data.branchOffset); //Write new branchOffset

                /* Write to overlayfile */
                string overlayFilePath = OverlayUtils.GetPath(data.overlayNumber);
                if (OverlayUtils.IsCompressed(data.overlayNumber))
                {
                    OverlayUtils.Decompress(data.overlayNumber);
                }

                DSUtils.WriteToFile(overlayFilePath, DSUtils.HexStringToByteArray(data.overlayString1), data.overlayOffset1); //Write new overlayCode1
                DSUtils.WriteToFile(overlayFilePath, DSUtils.HexStringToByteArray(data.overlayString2), data.overlayOffset2); //Write new overlayCode2
                RomPatchState.overlay1MustBeRestoredFromBackup = false;

                /*Write Expanded ARM9 File*/
                DSUtils.WriteToFile(Filesystem.expArmPath, data.subroutine, BDHCAMPatchData.BDHCamSubroutineOffset);
            }
            catch
            {
                ShowError("Operation failed. It is strongly advised that you restore the arm9 and overlay from their respective backups.", "Something went wrong");
                return false;
            }

            RomPatchState.overlay1MustBeRestoredFromBackup = false;
            RomPatchState.flag_BDHCamPatchApplied = true;

            ShowInfo("The BDHCAM patch has been applied.", "Operation successful.");
            return true;
        }

        /// <summary>Rearrange item scripts to ascending index order and fix ground-item references.</summary>
        public static bool ApplyItemStandardizePatch()
        {
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
            // NOTE: preserving original behaviour — the patch is marked applied even if the write threw.
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
                    dynamicTextureType = 0xFFFF
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

        // ── Script-command table (Mikelan's custom commands) ─────────────────────────────────────

        /// <summary>Repoint the script command table into the expanded ARM9 file (HGSS EN/ES).</summary>
        public static bool ApplyScrcmdRepointPatch()
        {
            string expandedPath = Path.Combine(RomInfo.gameDirs[DirNames.synthOverlay].unpackedDir, "0000");
            if (!File.Exists(expandedPath))
            {
                ShowError("Apply the ARM9 expansion patch first — the synthetic overlay file is missing.", "ARM9 not expanded");
                return false;
            }

            if (GetCommandTableOffset() >= 0)
            {
                ShowInfo("The script command table is already repointed to the expanded ARM9 file.", "Already applied");
                return true;
            }

            if (!ConfirmYesNo("Script command table has not been repointed.\n\n" +
                "Do you wish to repoint it to the expanded ARM9 file?\n\n" +
                "By default it will be written from 0x200 to 0x1700.\n" +
                "If you already have something there, you must cancel this window and move these things to a new location, or you can manually repoint the script command table to a different free location in the expanded ARM9 file",
                "Confirm to proceed"))
            {
                ShowInfo("No changes have been made.", "Operation canceled");
                return false;
            }

            try
            {
                RepointCommandTable();
            }
            catch
            {
                ShowError("Repointing the script command table failed.", "Something went wrong");
                return false;
            }

            ShowInfo("The script command table has been repointed to the expanded ARM9 file.", "Operation successful.");
            return true;
        }

        /// <summary>Install a custom script command from a <c>.scrcmd</c> file (repointing the table first if needed).</summary>
        public static bool InstallCustomScriptCommand()
        {
            string expandedPath = Path.Combine(RomInfo.gameDirs[DirNames.synthOverlay].unpackedDir, "0000");
            if (!File.Exists(expandedPath))
            {
                ShowError("Apply the ARM9 expansion patch first — the synthetic overlay file is missing.", "ARM9 not expanded");
                return false;
            }

            int expTableOffset = GetCommandTableOffset();

            if (expTableOffset < 0)
            {
                if (ConfirmYesNo("Script command table has not been repointed.\n\n" +
                    "Do you wish to repoint it to the expanded ARM9 file?\n\n" +
                    "By default it will be written from 0x200 to 0x1700.\n" +
                    "If you already have something there, you must cancel this window and move these things to a new location, or you can manually repoint the script command table to a different free location in the expanded ARM9 file",
                    "Confirm to proceed"))
                {
                    RepointCommandTable();
                }
                else
                {
                    return false;
                }
            }

            if (ImportCustomCommand())
            {
                ShowInfo("Script commands succesfully installed in the ROM", "Done");
                return true;
            }

            return false;
        }

        private static int GetCommandTableOffset()
        { // Checks if command table is repointed IN THE EXPANDED ARM9 FILE, returns pointer inside this file
            ResourceManager customcmdDB = new ResourceManager("DSPRE.Resources.ROMToolboxDB.CustomScrCmdDB", Assembly.GetExecutingAssembly());
            int pointerOffset = int.Parse(customcmdDB.GetString("pointerOffset" + "_" + RomInfo.gameVersion + "_" + RomInfo.gameLanguage));
            using (ARM9.Reader r = new ARM9.Reader(pointerOffset))
            {
                uint cmdTable = r.ReadUInt32();
                uint offset = cmdTable - synthOverlayLoadAddress;

                if ((offset >= 0) && (offset <= 0x12B00))
                {
                    return (int)offset; // Table position inside the expanded arm9 file
                }
            }
            return -1; // No table in expanded arm9 file
        }

        private static void RepointCommandTable()
        {
            string expandedPath = Path.Combine(RomInfo.gameDirs[DirNames.synthOverlay].unpackedDir, "0000");
            ResourceManager customcmdDB = new ResourceManager("DSPRE.Resources.ROMToolboxDB.CustomScrCmdDB", Assembly.GetExecutingAssembly());

            FileStream arm9FileStream = new FileStream(RomInfo.arm9Path, FileMode.Open); // I make a copy of the stream so the file is free for writing
            MemoryStream arm9Stream = new MemoryStream();
            arm9FileStream.CopyTo(arm9Stream);
            byte[] cmdTbl = arm9Stream.ToArray();

            using (BinaryWriter expArmWriter = new BinaryWriter(new FileStream(expandedPath, FileMode.Open)))
            {
                expArmWriter.BaseStream.Position = 0x200; // Command table default offset
                expArmWriter.Write(cmdTbl, int.Parse(customcmdDB.GetString("originalTableOffset" + "_" + RomInfo.gameVersion + "_" + RomInfo.gameLanguage)), 4 * 0x355);
            }

            arm9FileStream.Close();

            using (ARM9.Writer wr = new ARM9.Writer())
            { // Change both the pointer and the limit
                wr.BaseStream.Position = int.Parse(customcmdDB.GetString("pointerOffset" + "_" + RomInfo.gameVersion + "_" + RomInfo.gameLanguage));
                wr.Write((uint)0x023C8200);

                wr.BaseStream.Position = int.Parse(customcmdDB.GetString("limitOffset" + "_" + RomInfo.gameVersion + "_" + RomInfo.gameLanguage));
                wr.Write((uint)0x053C);
            }
        }

        private static bool ImportCustomCommand()
        {
            string expandedPath = Path.Combine(RomInfo.gameDirs[DirNames.synthOverlay].unpackedDir, "0000");
            int appliedPatches = 0;

            string chosenFile = PickCustomCommandFile();
            if (string.IsNullOrEmpty(chosenFile))
            {
                return false;
            }

            FileStream expandedFileStream = new FileStream(expandedPath, FileMode.Open);
            MemoryStream expandedStream = new MemoryStream();
            expandedFileStream.CopyTo(expandedStream);
            expandedFileStream.Close();

            using (DSUtils.EasyWriter expandedWriter = new DSUtils.EasyWriter(expandedPath, fmode: FileMode.Open))
            {
                using (BinaryReader expandedReader = new BinaryReader(expandedStream))
                {
                    try
                    {
                        System.Xml.Linq.XDocument xmldoc = System.Xml.Linq.XDocument.Load(new FileStream(chosenFile, FileMode.Open));

                        foreach (var node in xmldoc.Root.Elements("scriptcommand"))
                        {
                            ushort commandID = ushort.Parse(node.Attribute("ID").Value, System.Globalization.NumberStyles.HexNumber);
                            string targetROM = node.Element("ROM").Value;
                            string targetLang = node.Element("lang").Value;
                            string commandName = node.Element("name").Value;
                            string paramCount = node.Element("paramcount").Value;
                            string paramCode = node.Element("paramcode").Value;
                            int asmOffset = Int32.Parse(node.Element("asmoffset").Value, System.Globalization.NumberStyles.HexNumber);
                            string asmCode = node.Element("asmcode").Value.Replace("\n", "").Replace("\t", "").Replace(" ", "");

                            if (RomInfo.gameVersion.ToString().Equals(targetROM) && RomInfo.gameLanguage.Equals(targetLang))
                            {
                                expandedReader.BaseStream.Position = 0x200 + commandID * 4;
                                if (expandedReader.ReadUInt32() != 0)
                                {
                                    if (!ConfirmYesNo("Script command " + commandID.ToString("X4") + " is already used.\n\n" +
                                        "Do you really want to overwrite it?", "Confirm to proceed"))
                                    {
                                        continue;
                                    }
                                }

                                expandedWriter.BaseStream.Position = 0x200 + commandID * 4;
                                expandedWriter.Write((int)(synthOverlayLoadAddress + asmOffset + 1));

                                byte[] asmCodeBytes = DSUtils.StringToByteArray(asmCode);
                                expandedWriter.BaseStream.Position = asmOffset;
                                expandedWriter.Write(asmCodeBytes);

                                appliedPatches++;
                            }
                        }
                    }
                    catch
                    {
                        ShowError("Selected command installation file is corrupted.\n\n" +
                        "Please, download it again or contact its creator.", "Error");

                        return false;
                    }
                }
            }

            if (appliedPatches == 0)
            {
                ShowInfo("No command could be installed from this file.\n\n" +
                "Make sure the command installation file supports your current ROM.", "Warning");
                return false;
            }

            return true;
        }

        // ── Patch catalogue / status (read-only, UI-agnostic) ────────────────────────────────────
        // Lets a non-WinForms shell (the Avalonia Patch Toolbox) list the patches, show each one's
        // applied/supported state, and apply it — mirroring the gating the WinForms constructor does.

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
        /// Some checks (BDHCam) decompress an overlay as a side effect — same as the WinForms dialog.
        /// </summary>
        public static List<PatchInfo> GetPatchStatuses()
        {
            var list = new List<PatchInfo>();

            list.Add(Status("sentenceCase", "Sentence-case Pokémon names",
                "Convert every Pokémon name from ALL-CAPS to Sentence Case.",
                () => PatchState.Available));   // no reliable applied-detection

            list.Add(Status("itemStandardize", "Standardize item numbers",
                "Rearrange item scripts into ascending index order and fix ground-item references.",
                () =>
                {
                    DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.scripts });
                    bool applied = RomPatchState.flag_standardizedItems || CheckScriptsStandardizedItemNumbers();
                    return applied ? PatchState.Applied : PatchState.Available;
                }));

            list.Add(Status("arm9", "Expand ARM9 (synthetic overlay)",
                "Add ~88 KB of usable ARM9 memory. Required by the BDHCam / script-command patches. Advanced — can break the game if misused.",
                () =>
                {
                    if (!ARM9PatchData.arm9ExpansionCodeDB.ContainsKey("branchString" + "_" + RomInfo.gameFamily + "_" + RomInfo.gameLanguage))
                        return Unsupported("Unsupported language");
                    bool applied = RomPatchState.flag_arm9Expanded || CheckFilesArm9ExpansionApplied();
                    return applied ? PatchState.Applied : PatchState.Available;
                }));

            list.Add(Status("overlay1", "Configure Overlay 1 uncompressed",
                "Decompress Overlay 1 and mark it uncompressed in the overlay table (HGSS). Recommended before BDHCam.",
                () =>
                {
                    if (RomInfo.gameFamily != GameFamilies.HGSS) return Unsupported("HGSS only");
                    return OverlayUtils.OverlayTable.IsDefaultCompressed(1) ? PatchState.Available : PatchState.Applied;
                }));

            list.Add(Status("bdhcam", "BDHCam camera routine",
                "Install the BDHCam camera subroutine (Platinum / HGSS, EN or ES). Requires the ARM9 expansion patch first.",
                () =>
                {
                    if (!ScrcmdLikeLangOk()) return Unsupported("Unsupported version/language");
                    if (!Arm9Expanded()) return Unsupported("Requires ARM9 expansion");
                    bool applied = RomPatchState.flag_BDHCamPatchApplied || CheckFilesBDHCamPatchApplied();
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
                    return GetCommandTableOffset() >= 0 ? PatchState.Applied : PatchState.Available;
                }));

            list.Add(Status("installCustomCommand", "Install custom script command (.scrcmd)",
                "Install one or more custom script commands from a .scrcmd file (repointing the table first if needed). Repeatable.",
                () =>
                {
                    if (!ScrcmdLikeLangOk() || RomInfo.gameFamily != GameFamilies.HGSS) return Unsupported("Unsupported version/language");
                    if (!Arm9Expanded()) return Unsupported("Requires ARM9 expansion");
                    return PatchState.Available;   // repeatable action — never latches to "Applied"
                }, actionLabel: "Install…"));

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
                info.Reason = info.State == PatchState.Unsupported ? (_reason_text ?? "Unsupported") : null;
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
                case "itemStandardize": return ApplyItemStandardizePatch();
                case "arm9": return ApplyARM9ExpansionPatch();
                case "overlay1": return ConfigureOverlay1Uncompressed();
                case "bdhcam": return ApplyBDHCamPatch(null);   // caller re-queries statuses afterwards
                case "dynamicHeaders": return ApplyDynamicHeadersPatch();
                case "matrix": return ApplyMatrixExpansionPatch();
                case "scrcmdRepoint": return ApplyScrcmdRepointPatch();
                case "installCustomCommand": return InstallCustomScriptCommand();
                case "disableTextures": return ApplyDisableDynamicTexturesPatch();
                case "trainerNames": return ApplyExpandTrainerNamesPatch();
                default: return false;
            }
        }
    }
}

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
    /// Applies the ROM patch toolbox's patches. Prompts go through <see cref="ConfirmYesNo"/>, <see cref="ShowInfo"/>
    /// and <see cref="ShowError"/>, which the Avalonia shell points at its dialogs; each apply returns whether it applied.
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
            byte[] payload = (byte[])data.Payload.Clone();
            byte[] branchBytes = BuildThumbBl(
                payloadAddress + BuildingRotationPatchData.PayloadInternalBranchOffset,
                data.RotationMatrixFunctionAddress);
            Array.Copy(branchBytes, 0, payload, (int)BuildingRotationPatchData.PayloadInternalBranchOffset, branchBytes.Length);
            return payload;
        }

        // ── File-state checks (do the ROM bytes say the patch is applied?) ─────────────────────────

        /// <summary>Why the ARM9 expansion can't be applied to this ROM, or null when it can (or already is).</summary>
        public static string Arm9ExpansionWhyNot() =>
            RomInfo.IsHGE ? HgEngine.HgEngineSyntheticOverlay.ToolboxReason
            : !ARM9PatchData.Supported
                ? "The ARM9 expansion isn't available for this game's language."
            : null;

        public static bool CheckFilesArm9ExpansionApplied()
        {
            if (!ARM9PatchData.Supported) return false;
            if (Arm9ExpansionIn(new ARM9PatchData())) return true;
            // A ROM expanded before its dump was measured carries the per-language entry instead.
            return RomLocator.Arm9ExpansionSites != null && ARM9PatchData.HasLanguageEntry && Arm9ExpansionIn(new ARM9PatchData(languageEntry: true));
        }

        private static bool Arm9ExpansionIn(ARM9PatchData data)
        {

            byte[] branchCode = DSUtils.HexStringToByteArray(data.BranchString);
            byte[] branchCodeRead = ARM9.ReadBytes(data.BranchOffset, data.BranchString.Length / 3 + 1);
            if (branchCodeRead.Length != branchCode.Length || !branchCodeRead.SequenceEqual(branchCode))
                return false;

            byte[] initCode = DSUtils.HexStringToByteArray(data.InitString);
            byte[] initCodeRead = ARM9.ReadBytes(data.InitOffset, data.InitString.Length / 3 + 1);
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

            // HGSS writes into overlay 1, whose compression an ndstool project can't track, so ds-rom is required there.
            if (RomInfo.GameFamily == GameFamilies.HGSS && !RomInfo.IsDsRomProject)
            {
                return false;
            }

            BDHCAMPatchData data = new BDHCAMPatchData();

            byte[] branchCode = DSUtils.HexStringToByteArray(data.BranchString);
            byte[] branchCodeRead = ARM9.ReadBytes(data.BranchOffset, branchCode.Length);

            if (branchCode.Length != branchCodeRead.Length || !branchCode.SequenceEqual(branchCodeRead))
            {
                return false;
            }

            string overlayFilePath = OverlayUtils.GetPath(data.OverlayNumber);

            byte[] overlayCode1 = DSUtils.HexStringToByteArray(data.OverlayString1);
            byte[] overlayCode1Read = DSUtils.ReadFromFile(overlayFilePath, data.OverlayOffset1, overlayCode1.Length);
            if (overlayCode1.Length != overlayCode1Read.Length || !overlayCode1.SequenceEqual(overlayCode1Read))
                return false;

            byte[] overlayCode2 = DSUtils.HexStringToByteArray(data.OverlayString2);
            byte[] overlayCode2Read = DSUtils.ReadFromFile(overlayFilePath, data.OverlayOffset2, overlayCode2.Length);
            if (overlayCode2.Length != overlayCode2Read.Length || !overlayCode2.SequenceEqual(overlayCode2Read))
                return false;

            String fullFilePath = Filesystem.ExpArmPath;
            byte[] subroutineRead = DSUtils.ReadFromFile(fullFilePath, BDHCAMPatchData.BDHCamSubroutineOffset, data.Subroutine.Length);
            if (data.Subroutine.Length != subroutineRead.Length || !data.Subroutine.SequenceEqual(subroutineRead))
                return false;

            return true;
        }

        public static bool CheckFilesMatrixExpansionApplied()
        {
            foreach (KeyValuePair<uint[], string> kv in ToolboxDB.MatrixExpansionDB)
            {
                foreach (uint offset in kv.Key)
                {
                    if (!(MatrixExpansionCode is int code0)) return false;
                    byte[] read = ARM9.ReadBytes((uint)(code0 + offset), kv.Value.Length / 3 + 1);
                    byte[] code = DSUtils.HexStringToByteArray(kv.Value);
                    if (read.Length != code.Length || !read.SequenceEqual(code))
                        return false;
                }
            }
            return true;
        }

        public static bool CheckScriptsStandardizedItemNumbers()
        {
            ScriptFile itemScript = new ScriptFile(RomInfo.ItemScriptFileNumber);
            if (itemScript.AllScripts.Count - 1 < new TextArchive(RomInfo.ItemNamesTextNumber).Messages.Count)
            {
                return false;
            }

            for (ushort i = 0; i < itemScript.AllScripts.Count - 1; i++)
            {
                if (BitConverter.ToUInt16(itemScript.AllScripts[i].Commands[0].CmdParams[1], 0) != i || BitConverter.ToUInt16(itemScript.AllScripts[i].Commands[1].CmdParams[1], 0) != 1)
                {
                    return false;
                }
            }
            return true;
        }

        public static bool CheckFilesDynamicHeadersPatchApplied()
        {
            if (!DynamicHeadersPatchData.SupportsCurrentRom) return false;
            DynamicHeadersPatchData data = new DynamicHeadersPatchData();
            ushort initValue = BitConverter.ToUInt16(ARM9.ReadBytes(data.InitOffset, 0x2), 0);
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

        /// <summary>Writes every Pokémon name in sentence case, including renamed ones. Always supported.</summary>
        public static bool ApplySentenceCasePatch()
        {
            if (!ConfirmYesNo("Every Pokémon name is written in sentence case instead of capitals, including Pokémon you renamed, " +
                "and any other text that mentions a renamed Pokémon is updated to match.\n\nApply this patch?" +
                CreditNote("sentenceCase"), "Confirm to proceed"))
                return false;

            List<(string searchString, string replaceString, bool caseSensitive)> renamePairs = new List<(string searchString, string replaceString, bool caseSensitive)>();

            foreach (int ID in RomInfo.PokemonNamesTextNumbers)
            {
                TextArchive pokeName = new TextArchive(ID);
                for (int i = 1; i < pokeName.Messages.Count; i++)
                {
                    string current = pokeName.Messages[i];
                    if (string.IsNullOrEmpty(current))
                    {
                        continue;
                    }

                    string sentenceCased = SentenceCaseName(current);
                    if (sentenceCased != current)
                    {
                        pokeName.Messages[i] = sentenceCased;
                        renamePairs.Add((current, sentenceCased, true));
                    }
                }
                pokeName.SaveToExpandedDir(ID, showSuccessMessage: false);
            }

            int archivesUpdated = renamePairs.Count > 0 ? DSUtils.ReplaceTextEverywhere(renamePairs, wholeWord: true) : 0;
            ShowInfo($"Pokémon names are now in sentence case, and {Plural.Of(archivesUpdated, "other text archive")} {(archivesUpdated == 1 ? "was" : "were")} updated to match.", "Names converted");
            return true;
        }

        /// <summary>Writes every item name in sentence case, including renamed ones. Always supported.</summary>
        public static bool ApplyItemSentenceCasePatch()
        {
            if (!ConfirmYesNo("Every item name is written in sentence case instead of capitals, including items you renamed, " +
                "and any other text that mentions a renamed item is updated to match.\n\nApply this patch?" +
                CreditNote("itemSentenceCase"), "Confirm to proceed"))
                return false;

            List<(string searchString, string replaceString, bool caseSensitive)> renamePairs = new List<(string searchString, string replaceString, bool caseSensitive)>();

            TextArchive itemNames = new TextArchive(RomInfo.ItemNamesTextNumber);
            for (int i = 1; i < itemNames.Messages.Count; i++)
            {
                string current = itemNames.Messages[i];
                if (string.IsNullOrEmpty(current))
                {
                    continue;
                }

                string sentenceCased = SentenceCaseName(current);
                if (sentenceCased != current)
                {
                    itemNames.Messages[i] = sentenceCased;
                    renamePairs.Add((current, sentenceCased, true));
                }
            }
            itemNames.SaveToExpandedDir(RomInfo.ItemNamesTextNumber, showSuccessMessage: false);

            int archivesUpdated = renamePairs.Count > 0 ? DSUtils.ReplaceTextEverywhere(renamePairs, wholeWord: true) : 0;
            ShowInfo($"Item names are now in sentence case, and {Plural.Of(archivesUpdated, "other text archive")} {(archivesUpdated == 1 ? "was" : "were")} updated to match.", "Names converted");
            return true;
        }

        /// <summary>The BDHCam routine (Platinum and HGSS, English or Spanish); HGSS needs a ds-rom project.</summary>
        public static bool ApplyBDHCamPatch()
        {
            if (RomInfo.GameFamily == GameFamilies.HGSS && !RomInfo.IsDsRomProject)
            {
                ShowError("Convert this project to ds-rom format before applying the dynamic cameras patch.", "ds-rom project required");
                return false;
            }

            BDHCAMPatchData data = new BDHCAMPatchData();

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.SynthOverlay });
            if (AlreadyApplied(RomPatchState.FlagBDHCamPatchApplied || Probe(CheckFilesBDHCamPatchApplied))) return false;
            string expandedCheckPath = Filesystem.ExpArmPath;
            if (!File.Exists(expandedCheckPath) || new FileInfo(expandedCheckPath).Length < 0x16000)
            {
                ShowError("Apply the ARM9 expansion first. The synthetic overlay is missing or too small.", "ARM9 expansion needed");
                return false;
            }

            // The routine only works at its own offset, so it can't move out of another block's way.
            long camStart = BDHCAMPatchData.BDHCamSubroutineOffset, camEnd = camStart + data.Subroutine.Length;
            if (SyntheticOverlaySpace.Reserved(File.ReadAllBytes(expandedCheckPath)).Any(r => r.Start < camEnd && camStart < r.End))
            {
                ShowError("Another patch or a table DSPRE moved already uses 0x" + camStart.ToString("X") + " to 0x" + (camEnd - 1).ToString("X") +
                          " in the synthetic overlay, where this routine has to go.", "Patch not applied");
                return false;
            }

            if (!ConfirmYesNo("Maps can change the camera as the player walks, using BDHCam files. The routine goes at 0x" +
                BDHCAMPatchData.BDHCamSubroutineOffset.ToString("X") + " in the synthetic overlay." +
                BackupNote($"the ARM9 and overlay {data.OverlayNumber}") + "\n\nApply this patch?" + CreditNote("bdhcam"), "Confirm to proceed"))
                return false;

            PlainFiles.Copy(RomInfo.Arm9Path, RomInfo.Arm9Path + BackupSuffix, overwrite: true);
            string overlayBackupPath = OverlayUtils.GetPath(data.OverlayNumber);
            PlainFiles.Copy(overlayBackupPath, overlayBackupPath + BackupSuffix, overwrite: true);

            try
            {
                string overlayFilePath = OverlayUtils.GetPath(data.OverlayNumber);
                if (OverlayUtils.IsCompressed(data.OverlayNumber))
                {
                    int decompressResult = OverlayUtils.Decompress(data.OverlayNumber, makeBackup: false);
                    if (decompressResult != 0)
                    {
                        AppLogger.Error($"Couldn't decompress overlay {data.OverlayNumber}; BDHCAM patch wasn't applied.");
                        DSUtils.WriteWholeFile(overlayFilePath, File.ReadAllBytes(overlayBackupPath + BackupSuffix));
                        if (decompressResult != DSUtils.ErrToolUnavailable)
                        {
                            ShowError("The target overlay couldn't be decompressed, so no changes were made.",
                                "Decompression failed");
                        }
                        return false;
                    }
                }

                ARM9.WriteBytes(DSUtils.HexStringToByteArray(data.BranchString), data.BranchOffset);
                DSUtils.WriteToFile(overlayFilePath, DSUtils.HexStringToByteArray(data.OverlayString1), data.OverlayOffset1);
                DSUtils.WriteToFile(overlayFilePath, DSUtils.HexStringToByteArray(data.OverlayString2), data.OverlayOffset2);

                DSUtils.WriteToFile(Filesystem.ExpArmPath, data.Subroutine, BDHCAMPatchData.BDHCamSubroutineOffset);
            }
            catch
            {
                ShowError($"The patch failed. Restore the ARM9 and overlay {data.OverlayNumber} from their backups.", "Patch failed");
                return false;
            }

            RomPatchState.FlagBDHCamPatchApplied = true;

            ShowInfo("Maps can now change the camera with BDHCam files.", "Patch applied");
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
            string overlayFilePath = OverlayUtils.GetPath(data.OverlayNumber);

            byte[] hookBytes = DSUtils.ReadFromFile(overlayFilePath, data.HookOverlayOffset, 4);
            if (!TryGetThumbBlTarget(data.HookRuntimeAddress, hookBytes, out uint targetAddress))
            {
                return false;
            }

            if (targetAddress < SynthOverlayLoadAddress)
            {
                return false;
            }

            uint payloadOffset = targetAddress - SynthOverlayLoadAddress;
            string expandedPath = Filesystem.ExpArmPath;
            if (!File.Exists(expandedPath))
            {
                return false;
            }

            long fileLength = new FileInfo(expandedPath).Length;
            if ((long)payloadOffset + data.Payload.Length > fileLength)
            {
                return false;
            }

            byte[] payloadRead = DSUtils.ReadFromFile(expandedPath, payloadOffset, data.Payload.Length);
            return payloadRead.SequenceEqual(BuildBuildingRotationPayload(data, targetAddress));
        }

        /// <summary>
        /// Apply the Building Rotation routine (Diamond/Pearl/Platinum/HeartGold/SoulSilver EN, Plat FR,
        /// HG IT). Requires the ARM9 expansion patch and a ds-rom-format project (the hook writes into
        /// an overlay whose compression state ds-rom tracks automatically; a legacy ndstool project can't
        /// reliably guarantee the overlay is uncompressed here).
        /// </summary>
        public static bool ApplyBuildingRotationPatch()
        {
            if (!RomInfo.IsDsRomProject)
            {
                ShowError("Convert this project to ds-rom format before applying the building rotation patch.", "ds-rom project required");
                return false;
            }

            if (!RomPatchState.FlagArm9Expanded && !CheckFilesArm9ExpansionApplied())
            {
                ShowError("Apply the ARM9 expansion before the building rotation patch.", "ARM9 expansion needed");
                return false;
            }

            BuildingRotationPatchData data;
            try
            {
                data = new BuildingRotationPatchData();
            }
            catch
            {
                ShowError("The building rotation patch doesn't support this ROM version.", "Unsupported");
                return false;
            }

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.SynthOverlay });
            if (AlreadyApplied(RomPatchState.FlagBuildingRotationPatchApplied || Probe(CheckFilesBuildingRotationPatchApplied))) return false;
            string expandedPath = Filesystem.ExpArmPath;
            if (!File.Exists(expandedPath) || new FileInfo(expandedPath).Length < 0x16000)
            {
                ShowError("Apply the ARM9 expansion first. The synthetic overlay is missing or too small.", "ARM9 expansion needed");
                return false;
            }

            int blockLength = SyntheticOverlaySpace.HeaderSize + data.Payload.Length;
            if (PlacementNote(new[] { blockLength }, out int[] offsets) is not string placement) return false;

            uint payloadOffset = (uint)offsets[0] + SyntheticOverlaySpace.HeaderSize;
            uint payloadAddress = SynthOverlayLoadAddress + payloadOffset;
            byte[] branchBytes = BuildThumbBl(data.HookRuntimeAddress, payloadAddress);
            byte[] payloadBytes = BuildBuildingRotationPayload(data, payloadAddress);

            if (!ConfirmYesNo("Buildings use the rotation values already stored for them when they are placed." +
                placement + BackupNote("overlay " + data.OverlayNumber + " and the synthetic overlay") +
                "\n\nApply this patch?" + CreditNote("buildingRotation"), "Confirm to proceed"))
                return false;

            string overlayFilePath = OverlayUtils.GetPath(data.OverlayNumber);
            PlainFiles.Copy(overlayFilePath, overlayFilePath + BackupSuffix, overwrite: true);
            PlainFiles.Copy(expandedPath, expandedPath + BackupSuffix, overwrite: true);

            try
            {
                DSUtils.WriteToFile(expandedPath, SyntheticOverlaySpace.Header(SyntheticOverlaySpace.BuildingRotationMarker, blockLength), (uint)offsets[0]);
                DSUtils.WriteToFile(expandedPath, payloadBytes, payloadOffset);
                DSUtils.WriteToFile(overlayFilePath, branchBytes, data.HookOverlayOffset);
            }
            catch
            {
                ShowError("The patch failed. Restore overlay " + data.OverlayNumber + " from its backup.", "Patch failed");
                return false;
            }

            RomPatchState.FlagBuildingRotationPatchApplied = true;

            ShowInfo("Buildings placed in the Map editor now keep their rotation in game.", "Patch applied");
            return true;
        }

        public static bool ApplyTrainerCustomPokeballsPatch()
        {
            if (!TrainerCustomPokeballsPatch.SupportsCurrentRom)
            {
                ShowError("The trainer custom Poké Balls patch is for HeartGold and SoulSilver, without hg-engine.", "Unsupported");
                return false;
            }
            if (AlreadyApplied(Probe(TrainerCustomPokeballsPatch.DetectCurrentProject))) return false;
            if (TrainerCustomPokeballsPatch.WhyNotApplicable(RomInfo.RomID, File.ReadAllBytes(RomInfo.Arm9Path)) is string why)
            {
                ShowError(why, "Trainer custom Poké Balls");
                return false;
            }
            if (!ConfirmYesNo("Each party member's ball capsule becomes a Poké Ball, picked in the Trainer editor; zero keeps the " +
                "default Poké Ball. Enemy trainers can no longer use ball capsule seals, and capsule values already set are read " +
                "as items, so check them in the Trainer editor." + BackupNote("the ARM9") +
                "\n\nApply this patch?" + CreditNote("trainerCustomPokeballs"), "Confirm to proceed"))
                return false;
            ARM9.DecompressIfMarked();
            PlainFiles.Copy(RomInfo.Arm9Path, RomInfo.Arm9Path + BackupSuffix, overwrite: true);
            try { TrainerCustomPokeballsPatch.Apply(); }
            catch (Exception ex)
            {
                ShowError("The patch failed. Restore the ARM9 from its backup if needed.\n\n" + ex.Message, "Patch failed");
                return false;
            }
            ShowInfo("Pick each party member's Poké Ball in the Trainer editor.", "Patch applied");
            return true;
        }

        /// <summary>The trainer shiny patch from PR #271 (US HeartGold and SoulSilver, Italian HeartGold).</summary>
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
            if (!RomPatchState.FlagArm9Expanded && !CheckFilesArm9ExpansionApplied())
            {
                ShowError("Apply the ARM9 expansion before the trainer shiny patch.", "ARM9 expansion needed");
                return false;
            }
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.SynthOverlay });
            if (AlreadyApplied(Probe(TrainerShinyPatch.DetectCurrentProject))) return false;
            if (TrainerShinyPatch.WhyNotApplicable(File.ReadAllBytes(RomInfo.Arm9Path)) is string why)
            {
                ShowError(why, "Trainer shiny patch");
                return false;
            }

            string expandedPath = Filesystem.ExpArmPath;
            if (!File.Exists(expandedPath) || new FileInfo(expandedPath).Length < 0x16000)
            {
                ShowError("Apply the ARM9 expansion first. The synthetic overlay is missing or too small.", "ARM9 expansion needed");
                return false;
            }

            int blockLength = SyntheticOverlaySpace.HeaderSize + TrainerShinyPatch.Template.Length;
            if (PlacementNote(new[] { blockLength }, out int[] offsets) is not string placement) return false;
            uint offset = (uint)offsets[0] + SyntheticOverlaySpace.HeaderSize;

            if (!ConfirmYesNo("Party members ticked Shiny in the Trainer editor are shiny in battle." + placement +
                BackupNote("the ARM9 and the synthetic overlay") + "\n\nApply this patch?" + CreditNote("trainerShiny"), "Confirm to proceed"))
                return false;

            ARM9.DecompressIfMarked();
            PlainFiles.Copy(RomInfo.Arm9Path, RomInfo.Arm9Path + BackupSuffix, overwrite: true);
            PlainFiles.Copy(expandedPath, expandedPath + BackupSuffix, overwrite: true);
            try
            {
                DSUtils.WriteToFile(expandedPath, SyntheticOverlaySpace.Header(SyntheticOverlaySpace.TrainerShinyMarker, blockLength), (uint)offsets[0]);
                TrainerShinyPatch.Apply(offset);
            }
            catch
            {
                ShowError("The patch failed. Restore the ARM9 from its backup.", "Patch failed");
                return false;
            }
            ShowInfo("Tick Shiny on a party member in the Trainer editor to make it shiny in battle.", "Patch applied");
            return true;
        }

        /// <summary>The trainer class metadata patch from PR #272 (US HeartGold and SoulSilver).</summary>
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
            if (!RomPatchState.FlagArm9Expanded && !CheckFilesArm9ExpansionApplied())
            {
                ShowError("Apply the ARM9 expansion before the trainer class metadata patch.", "ARM9 expansion needed");
                return false;
            }
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.SynthOverlay });
            if (AlreadyApplied(Probe(() => TrainerClassMetadataStore.DetectCurrentRom(out _) == TrainerClassMetadataDetectionState.SchemaV1))) return false;
            if (TrainerClassMetadataPatch.WhyNotApplicable() is string why)
            {
                ShowError(why, "Trainer class metadata patch");
                return false;
            }

            string expandedPath = Filesystem.ExpArmPath;
            if (!File.Exists(expandedPath) || new FileInfo(expandedPath).Length < 0x16000)
            {
                ShowError("Apply the ARM9 expansion first. The synthetic overlay is missing or too small.", "ARM9 expansion needed");
                return false;
            }

            int blockLength = SyntheticOverlaySpace.HeaderSize + TrainerClassMetadataPatch.PayloadSize;
            if (PlacementNote(new[] { blockLength }, out int[] offsets) is not string placement) return false;
            uint offset = (uint)offsets[0] + SyntheticOverlaySpace.HeaderSize;

            if (!ConfirmYesNo("Each trainer class gets its own gender, prize money, eye-contact music and VS intro, edited in the " +
                "Trainer editor's Classes tab. Every class starts with what the game's tables give it now. Not compatible with hg-engine." +
                placement + BackupNote("the ARM9, overlays 1, 12, 80, 115, 117, 118, 119 and 120, the synthetic overlay and a/1/5/5 " +
                "(as unpacked/a155.narc" + BackupSuffix + ")") + "\n\nApply this patch?" + CreditNote("trainerClassMetadata"), "Confirm to proceed"))
                return false;

            ARM9.DecompressIfMarked();
            PlainFiles.Copy(RomInfo.Arm9Path, RomInfo.Arm9Path + BackupSuffix, overwrite: true);
            foreach (int overlay in new[] { 1, 12, 80, 115, 117, 118, 119, 120 })
                PlainFiles.Copy(OverlayUtils.GetPath(overlay), OverlayUtils.GetPath(overlay) + BackupSuffix, overwrite: true);
            if (File.Exists(Filesystem.ExpArmPath)) PlainFiles.Copy(Filesystem.ExpArmPath, Filesystem.ExpArmPath + BackupSuffix, overwrite: true);
            // Not beside the archive: ds-rom packs every file under files/, so a copy there would join the ROM.
            (string metadataArchive, string metadataUnpacked) = RomInfo.GameDirs[DirNames.TrainerClassMetadata];
            string metadataBackup = Path.Combine(Path.GetDirectoryName(metadataUnpacked), "a155.narc" + BackupSuffix);
            if (File.Exists(metadataArchive))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(metadataBackup));
                PlainFiles.Copy(metadataArchive, metadataBackup, overwrite: true);
            }
            try
            {
                DSUtils.WriteToFile(expandedPath, SyntheticOverlaySpace.Header(SyntheticOverlaySpace.ClassMetadataMarker, blockLength), (uint)offsets[0]);
                TrainerClassMetadataPatch.Apply(offset);
            }
            catch (Exception ex)
            {
                ShowError("The patch failed: " + ex.Message + "\nRestore the ARM9, overlays, synthetic overlay and a/1/5/5 from their backups.", "Patch failed");
                return false;
            }
            ShowInfo("Every trainer class now has its own gender, prize money, music and VS intro. Edit them in the Trainer editor's Classes tab.", "Patch applied");
            return true;
        }

        /// <summary>Puts item scripts in item number order and updates every item event to match. Not on hg-engine ROMs.</summary>
        public static bool ApplyItemStandardizePatch()
        {
            if (RomInfo.IsHGE)
            {
                ShowError("This patch isn't supported on hg-engine ROMs.", "Unsupported");
                return false;
            }

            if (!ConfirmYesNo("Item scripts are put in item number order and every item event is updated to match. Unsaved changes " +
                "in the Event editor are discarded.\n\nApply this patch?" + CreditNote("itemStandardize"), "Confirm to proceed"))
                return false;

            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.Scripts });
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.EventFiles });

            if (AlreadyApplied(RomPatchState.FlagStandardizedItems || Probe(CheckScriptsStandardizedItemNumbers))) return false;

            ScriptFile itemScriptFile = new ScriptFile(RomInfo.ItemScriptFileNumber);

            int[] vanillaItemsArray = new int[itemScriptFile.AllScripts.Count - 1];

            for (int i = 0; i < itemScriptFile.AllScripts.Count - 1; i++)
            {
                vanillaItemsArray[i] = BitConverter.ToInt16(itemScriptFile.AllScripts[i].Commands[0].CmdParams[1], 0);
            }
            ;

            int cnt = Filesystem.GetEventFileCount();
            (int itemScrMin, int itemScrMax) = (7000, 8000);

            for (int i = 0; i < cnt; i++)
            {
                bool dirty = false;

                EventFile eventFile = new EventFile(i);

                for (int j = 0; j < eventFile.Overworlds.Count; j++)
                {
                    // If ow is marked as an item, or in the rare case it is not but script no. falls within item script range:
                    bool isItem = eventFile.Overworlds[j].Type == (ushort)Overworld.OwType.ITEM
                                  || (eventFile.Overworlds[j].ScriptNumber >= itemScrMin
                                  && eventFile.Overworlds[j].ScriptNumber <= itemScrMax);

                    if (isItem)
                    {
                        int itemScriptID = eventFile.Overworlds[j].ScriptNumber - (itemScrMin - 1);
                        eventFile.Overworlds[j].ScriptNumber = (ushort)(itemScrMin + vanillaItemsArray[itemScriptID - 1]);
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

            // Turnback Cave's Griseous Orb script number, which the Distortion World code holds itself.
            if (RomLocator.Where(RomLocator.Thing.DistortionWorldItemScript) is RomLocator.Spot orb)
            {
                string orbPath = RomLocator.PathOf(orb);

                int itemScriptID;

                using (DSUtils.EasyReader ewr = new DSUtils.EasyReader(orbPath, orb.Offset))
                {
                    itemScriptID = ewr.ReadUInt16() - (itemScrMin - 1);
                }

                DSUtils.WriteToFile(orbPath, BitConverter.GetBytes((ushort)(itemScrMin + vanillaItemsArray[itemScriptID - 1])), (uint)orb.Offset);
            }

            int itemCount = new TextArchive(RomInfo.ItemNamesTextNumber).Messages.Count;
            ScriptCommandContainer executeGive = new ScriptCommandContainer((uint)itemCount + 1, itemScriptFile.AllScripts[itemScriptFile.AllScripts.Count - 1]);

            itemScriptFile.AllScripts.Clear();

            for (ushort i = 0; i < itemCount; i++)
            {
                List<ScriptCommand> cmdList = new List<ScriptCommand> {
                    new ScriptCommand("SetVar 0x8008 " + i),
                    new ScriptCommand("SetVar 0x8009 0x1"),
                    new ScriptCommand("Jump Function_#1")
                };

                itemScriptFile.AllScripts.Add(new ScriptCommandContainer((ushort)(i + 1), ScriptFile.ContainerTypes.Script, commandList: cmdList));
            }

            itemScriptFile.AllScripts.Add(executeGive);
            itemScriptFile.AllFunctions[0].UsedScriptID = itemCount + 1;

            itemScriptFile.SaveToFileDefaultDir(RomInfo.ItemScriptFileNumber, showSuccessMessage: false);
            ShowInfo("The item scripts now follow item number order.", "Patch applied");

            RomPatchState.FlagStandardizedItems = true;
            return true;
        }

        /// <summary>Gives the game an 88 KB synthetic overlay that other patches put their code in.</summary>
        public static bool ApplyARM9ExpansionPatch()
        {
            if (AlreadyApplied(RomPatchState.FlagArm9Expanded || Probe(CheckFilesArm9ExpansionApplied))) return false;

            ARM9PatchData data = new ARM9PatchData();

            if (!ConfirmYesNo("The game loads an 88 KB synthetic overlay, file " + RomPatchState.ExpandedARMfileID + " of " +
                    RomInfo.GameDirs[DirNames.SynthOverlay].packedDir + ", where other patches put their code. That file isn't backed up." +
                    BackupNote("the ARM9") + "\n\nApply this patch?" + CreditNote("arm9"), "Confirm to proceed"))
                return false;

            PlainFiles.Copy(RomInfo.Arm9Path, RomInfo.Arm9Path + BackupSuffix, overwrite: true);

            try
            {
                ARM9.WriteBytes(DSUtils.HexStringToByteArray(data.BranchString), data.BranchOffset);
                ARM9.WriteBytes(DSUtils.HexStringToByteArray(data.InitString), data.InitOffset);

                // A fresh project hasn't unpacked the synthetic overlay's NARC yet.
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.SynthOverlay });

                string fullFilePath = Filesystem.ExpArmPath;

                // An already expanded file is left alone so its data survives.
                if (File.Exists(fullFilePath))
                {
                    FileInfo fi = new FileInfo(fullFilePath);
                    if (fi.Length >= 0x16000)
                    {
                        ShowInfo("The synthetic overlay is already there, probably from an earlier ARM9 expansion, so it was " +
                            "left as it is.", "Synthetic overlay found");
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

                RomPatchState.FlagArm9Expanded = true;

                ShowInfo("The synthetic overlay is ready for other patches.", "Patch applied");
                return true;
            }
            catch
            {
                ShowError("The patch failed. Restore the ARM9 from its backup (arm9.bin" + BackupSuffix + ").", "Patch failed");
                return false;
            }
        }

        /// <summary>Where the matrix expansion's first site is in this dump; null where the patch isn't offered.</summary>
        private static int? MatrixExpansionCode => RomLocator.Where(RomLocator.Thing.MatrixExpansionCode)?.Offset;

        /// <summary>Lets matrix 0 grow to twice its size (HGSS, English or Spanish).</summary>
        public static bool ApplyMatrixExpansionPatch()
        {
            if (AlreadyApplied(RomPatchState.FlagMatrixExpansionApplied || Probe(CheckFilesMatrixExpansionApplied))) return false;

            if (!(MatrixExpansionCode is int code0))
            {
                ShowError("This patch supports English and Spanish HeartGold and SoulSilver.", "Unsupported");
                return false;
            }

            if (!ConfirmYesNo("Matrix 0 can grow to twice its size." + BackupNote("the ARM9") + "\n\nApply this patch?" +
                CreditNote("matrix"), "Confirm to proceed"))
                return false;

            ARM9.DecompressIfMarked();
            BackUp(RomInfo.Arm9Path);
            try
            {
                foreach (KeyValuePair<uint[], string> kv in ToolboxDB.MatrixExpansionDB)
                {
                    foreach (uint offset in kv.Key)
                    {
                        ARM9.WriteBytes(DSUtils.HexStringToByteArray(kv.Value), (uint)(code0 + offset));
                    }
                }
            }
            catch
            {
                ShowError("The patch failed. Restore the ARM9 from its backup (arm9.bin" + BackupSuffix + ").", "Patch failed");
                return false;
            }
            RomPatchState.FlagMatrixExpansionApplied = true;
            ShowInfo("Matrix 0 can now grow to twice its size.", "Patch applied");
            return true;
        }

        /// <summary>Loads map headers from their own files instead of the ARM9 table (Platinum and HGSS).</summary>
        public static bool ApplyDynamicHeadersPatch()
        {
            // A second run would split the already-patched table over the headers it moved out.
            if (!DynamicHeadersPatchData.SupportsCurrentRom) return false;
            if (AlreadyApplied(RomPatchState.FlagDynamicHeadersPatchApplied || Probe(CheckFilesDynamicHeadersPatchApplied))) return false;

            DynamicHeadersPatchData data = new DynamicHeadersPatchData();
            (string packedDir, string unpackedDir) headersDir = RomInfo.GameDirs[DirNames.DynamicHeaders];

            // Every HGSS dump has this read, Japanese and Spanish included.
            bool specialCase = RomInfo.GameFamily == GameFamilies.HGSS;

            if (!ConfirmYesNo("Map headers move out of the ARM9 table into their own files, so you can add headers. Saving the ROM packs them into " +
                headersDir.packedDir + "." + BackupNote("the ARM9") + "\n\nApply this patch?" +
                CreditNote("dynamicHeaders"), "Confirm to proceed"))
                return false;

            PlainFiles.Copy(RomInfo.Arm9Path, RomInfo.Arm9Path + BackupSuffix, overwrite: true);

            try
            {
                ARM9.WriteBytes(DSUtils.HexStringToByteArray(data.InitString), data.InitOffset);

                foreach (Tuple<uint, uint> reference in DynamicHeadersPatchData.DynamicHeadersPointersDB[RomInfo.GameFamily])
                {
                    ARM9.WriteBytes(DSUtils.HexStringToByteArray(data.REFERENCE_STRING), (uint)(reference.Item1 + data.InitOffset));
                    uint pointerValue = BitConverter.ToUInt32(ARM9.ReadBytes((uint)(reference.Item2 + data.InitOffset), 4), 0) - RomInfo.HeaderTableOffset - ARM9.Address;
                    ARM9.WriteBytes(BitConverter.GetBytes(pointerValue), (uint)(reference.Item2 + data.InitOffset));
                }

                if (specialCase)
                {

                    ARM9.WriteBytes(DSUtils.HexStringToByteArray(data.SpecialCaseData1), (uint)(data.SpecialCaseOffset1 + data.InitOffset));
                    ARM9.WriteBytes(DSUtils.HexStringToByteArray(data.SpecialCaseData2), (uint)(data.SpecialCaseOffset2 + data.InitOffset));
                    ARM9.WriteBytes(DSUtils.HexStringToByteArray(data.SpecialCaseData3), (uint)(data.SpecialCaseOffset3 + data.InitOffset));
                }

                Directory.Delete(headersDir.unpackedDir, true);
                Directory.CreateDirectory(headersDir.unpackedDir);

                // Saving the ROM packs these into a/0/5/0 in HGSS or debug/cb_edit/d_test.narc in Platinum.

                int headerCount = RomInfo.GetHeaderCount();
                for (int i = 0; i < headerCount; i++)
                {
                    byte[] headerData = MapHeader.LoadFromARM9((ushort)i).ToByteArray();
                    DSUtils.WriteToFile(Path.Combine(headersDir.unpackedDir, i.ToString("D4")), headerData);
                }

                RomPatchState.FlagDynamicHeadersPatchApplied = true;

                ShowInfo("Map headers are now loaded from their own files, so you can add more.", "Patch applied");
                return true;
            }
            catch
            {
                ShowError("The patch failed. Restore the ARM9 from its backup (arm9.bin" + BackupSuffix + ").", "Patch failed");
                return false;
            }
        }

        // WildMonSetRandomHeldItem gives a species listing one item twice that item every time; turning its
        // branch-if-different into an unconditional branch sends every species through the odds.
        // The branch distance is the ROM's own (9 halfwords in US, 8 in Japanese DP); only the condition changes.
        private const byte BranchIfDifferent = 0xD1, BranchAlways = 0xE0;

        public static PatchState SameHeldItemOddsState()
        {
            if (RomLocator.WhyNot(RomLocator.Thing.HeldItemSameItemBranch, 2) is string why) return Unsupported(why);
            byte[] now = RomLocator.Read(RomLocator.Thing.HeldItemSameItemBranch, 2);
            if (now[0] is < 0x08 or > 0x09) return Unsupported("Something else has already changed this code, so DSPRE leaves it alone.");
            if (now[1] == BranchAlways) return PatchState.Applied;
            if (now[1] == BranchIfDifferent) return PatchState.Available;
            return Unsupported("Something else has already changed this code, so DSPRE leaves it alone.");
        }

        public static PatchState PartyPanelSizeState()
        {
            if (RomInfo.IsHGE) return Unsupported(HgEngine.HgEngineSyntheticOverlay.ToolboxReason);
            if (!PartyPanelSizePatch.SupportsCurrentRom) return Unsupported("HeartGold and SoulSilver only");
            if (PartyPanelSizePatch.IsApplied) return PatchState.Applied;
            return PartyPanelSizePatch.WhyNotApplicable() is string why ? Unsupported(why) : PatchState.Available;
        }

        public static PatchState BannerPositionState()
        {
            if (BannerPositionPatch.IsApplied) return PatchState.Applied;
            return BannerPositionPatch.WhyNotApplicable() is string why ? Unsupported(why) : PatchState.Available;
        }

        public static bool ApplyBannerPositionPatch()
        {
            if (BannerPositionState() != PatchState.Available) return false;
            int overlay = RomLocator.PlaceNameBannerSites.Overlay;
            if (!ConfirmYesNo("The place-name banner can sit anywhere on the screen. Move it in the Screens editor's Place-name banner " +
                "layout. Nothing changes in game until you do." + BackupNote($"overlay {overlay}") + "\n\nApply this patch?" +
                CreditNote("bannerPosition"), "Confirm to proceed"))
                return false;
            BackUp(OverlayUtils.GetPath(overlay));
            BannerPositionPatch.Apply();
            ShowInfo("Move the banner in the Screens editor's Place-name banner layout.", "Patch applied");
            return true;
        }

        public static bool ApplyBannerMotionPatch()
        {
            if (AlreadyApplied(Probe(() => BannerMotionPatch.IsApplied))) return false;
            if (BannerMotionPatch.WhyNotApplicable() is string why) { ShowError(why, "Patch not applied"); return false; }
            if (PlacementNote(new[] { BannerMotionPatch.BlockLength }, out int[] offsets) is not string placement) return false;
            int overlay = RomLocator.PlaceNameBannerSites.Overlay;
            if (!ConfirmYesNo("The place-name banner can slide in from the bottom or appear without sliding. It can ease, overshoot or bounce, and fade " +
                "or pixelate as it comes and goes. Pick a style in the Screens editor's Place-name banner layout." + placement +
                BackupNote($"overlay {overlay} and the synthetic overlay") + "\n\nApply this patch?" + CreditNote("bannerMotion"), "Confirm to proceed"))
                return false;
            BackUp(OverlayUtils.GetPath(overlay), Filesystem.ExpArmPath);
            BannerMotionPatch.Apply(offsets[0]);
            ShowInfo("Pick the banner's motion in the Screens editor's Place-name banner layout.", "Patch applied");
            return true;
        }

        public static bool ApplyPartyPanelSizePatch()
        {
            if (PartyPanelSizeState() != PatchState.Available) return false;
            if (!ConfirmYesNo("The Pokémon panels in the party menu can be any size up to 96 tiles, set in the Screens editor's Party " +
                "layout. Nothing changes in game until you set a size." + BackupNote("the ARM9") + "\n\nApply this patch?" +
                CreditNote("partyPanelSize"), "Confirm to proceed"))
                return false;
            BackUp(RomInfo.Arm9Path);
            PartyPanelSizePatch.Apply();
            ShowInfo("Set the panel size in the Screens editor's Party layout.", "Patch applied");
            return true;
        }

        public static bool ApplySameHeldItemOddsPatch()
        {
            if (SameHeldItemOddsState() != PatchState.Available) return false;
            if (!ConfirmYesNo("Wild Pokémon whose two held items are the same will hold it only as often as the " +
                "held item odds say, instead of always." + BackupNote("the ARM9") + "\n\nApply this patch?" + CreditNote("sameHeldItemOdds"), "Confirm to proceed"))
                return false;
            BackUp(RomLocator.PathOf(RomLocator.Where(RomLocator.Thing.HeldItemSameItemBranch)));
            byte[] branch = RomLocator.Read(RomLocator.Thing.HeldItemSameItemBranch, 2);
            RomLocator.Write(RomLocator.Thing.HeldItemSameItemBranch, new[] { branch[0], BranchAlways });
            ShowInfo("Same held items now use the held item odds.", "Patch applied");
            return true;
        }

        /// <summary>Moves the trainer class gender and prize tables into blocks with room for every class.</summary>
        public static bool ApplyMoveTrainerClassTables()
        {
            if (!TrainerClassTableExpansion.IsSupportedForCurrentRom) { ShowError("Only Platinum is supported.", "Patch not applied"); return false; }
            if (AlreadyApplied(TrainerClassTableExpansion.ClassTablesHaveRoom)) return false;
            if (PlacementNote(TrainerClassTableExpansion.ClassTableBlockLengths(), out int[] offsets) is not string placement) return false;
            if (!ConfirmYesNo("New trainer classes can be added: the gender and prize money tables get room for " +
                TrainerClassTableExpansion.MaxClasses + " classes." + placement + BackupNote("the ARM9, overlay 16 and the synthetic overlay") +
                "\n\nApply this patch?" + CreditNote("trainerClassTablesExpanded"), "Confirm to proceed"))
                return false;
            PlainFiles.Copy(RomInfo.Arm9Path, RomInfo.Arm9Path + BackupSuffix, overwrite: true);
            string prizeOverlay = OverlayUtils.GetPath(RomLocator.TrainerPrizeTable.Overlay);
            PlainFiles.Copy(prizeOverlay, prizeOverlay + BackupSuffix, overwrite: true);
            PlainFiles.Copy(Filesystem.ExpArmPath, Filesystem.ExpArmPath + BackupSuffix, overwrite: true);
            string error = null;
            bool moved = false;
            SyntheticOverlaySpace.PlaceAt(offsets, () => moved = TrainerClassTableExpansion.MoveClassTables(out error));
            if (!moved)
            {
                ShowError(error, "Tables not moved");
                return false;
            }
            ShowInfo($"The trainer class tables now have room for {TrainerClassTableExpansion.MaxClasses} classes.", "Patch applied");
            return true;
        }

        public static bool ApplyMoveEncounterMusicTable()
        {
            if (AlreadyApplied(TrainerClassTableExpansion.MusicTableHasRoom)) return false;
            if (!TrainerClassTableExpansion.IsSupportedForCurrentRom) { ShowError("Only Platinum is supported.", "Patch not applied"); return false; }
            if (PlacementNote(new[] { TrainerClassTableExpansion.MusicBlockLength }, out int[] offsets) is not string placement) return false;
            if (!ConfirmYesNo("Every trainer class can have eye-contact music: the music table gets room for all " +
                TrainerClassTableExpansion.MaxClasses + " classes." + placement + BackupNote("the ARM9 and the synthetic overlay") +
                "\n\nApply this patch?" + CreditNote("trainerEncounterBgmRepointed"), "Confirm to proceed"))
                return false;
            PlainFiles.Copy(RomInfo.Arm9Path, RomInfo.Arm9Path + BackupSuffix, overwrite: true);
            PlainFiles.Copy(Filesystem.ExpArmPath, Filesystem.ExpArmPath + BackupSuffix, overwrite: true);
            string error = null;
            bool moved = false;
            SyntheticOverlaySpace.PlaceAt(offsets, () => moved = TrainerClassTableExpansion.MoveEncounterMusicTable(out error));
            if (!moved)
            {
                ShowError(error, "Table not moved");
                return false;
            }
            ShowInfo("Every trainer class can now have eye-contact music.", "Patch applied");
            return true;
        }

        public static bool ApplyPickupSlotsPatch()
        {
            if (AlreadyApplied(Probe(() => PickupSlotsPatch.IsApplied))) return false;
            if (PickupSlotsPatch.WhyNotApplicable() is string why) { ShowError(why, "Patch not applied"); return false; }
            if (PlacementNote(new[] { PickupSlotsPatch.BlockLength }, out int[] offsets) is not string placement) return false;
            int overlay = RomInfo.PickupTableOverlayNumber;
            if (!ConfirmYesNo("Each Pickup level range gets its own nine items and two rare items. Pickup gives the same items as before " +
                "until you change them in Item tables." + placement + BackupNote($"overlay {overlay} and the synthetic overlay") +
                "\n\nApply this patch?" + CreditNote("pickupSlots"), "Confirm to proceed"))
                return false;
            string overlayPath = OverlayUtils.GetPath(overlay);
            PlainFiles.Copy(overlayPath, overlayPath + BackupSuffix, overwrite: true);
            PlainFiles.Copy(Filesystem.ExpArmPath, Filesystem.ExpArmPath + BackupSuffix, overwrite: true);
            PickupSlotsPatch.Apply(offsets[0]);
            ShowInfo("Every Pickup level range now has its own items.", "Patch applied");
            return true;
        }

        public static bool ApplyClassIntrosPatch()
        {
            if (AlreadyApplied(Probe(() => ClassIntroPatch.IsApplied))) return false;
            if (ClassIntroPatch.WhyNotApplicable() is string why) { ShowError(why, "Patch not applied"); return false; }
            if (PlacementNote(new[] { ClassIntroPatch.BlockLength }, out int[] offsets) is not string placement) return false;
            int overlay = RomLocator.VsIntroSites.RecordOverlay;
            string look = RomInfo.GameFamily == RomInfo.GameFamilies.DP ? "its own face, cut from any class's battle sprite" : "its own portrait, name and colours";
            if (!ConfirmYesNo("Each trainer class gets its own VS intro: a style, the battle music, and for gym leader and Elite Four " +
                $"styles {look}, edited in the VS intro editor. Every class keeps the intro it has now until you " +
                "change it." + placement + BackupNote($"the ARM9, overlay {overlay} and the synthetic overlay") +
                "\n\nApply this patch?" + CreditNote("classIntros"), "Confirm to proceed"))
                return false;
            string overlayPath = OverlayUtils.GetPath(overlay);
            PlainFiles.Copy(overlayPath, overlayPath + BackupSuffix, overwrite: true);
            PlainFiles.Copy(RomInfo.Arm9Path, RomInfo.Arm9Path + BackupSuffix, overwrite: true);
            PlainFiles.Copy(Filesystem.ExpArmPath, Filesystem.ExpArmPath + BackupSuffix, overwrite: true);
            ClassIntroPatch.Apply(offsets[0]);
            ShowInfo("Every trainer class now has its own VS intro. Edit them in the VS intro editor.", "Patch applied");
            return true;
        }

        public static bool ApplyHpBarTextPatch()
        {
            if (AlreadyApplied(Probe(() => BattleGaugeTextPatch.IsApplied))) return false;
            if (BattleGaugeTextPatch.WhyNotApplicable() is string why) { ShowError(why, "Patch not applied"); return false; }
            if (BattleGaugeTextPatch.NeedsTileUpdate)
            {
                int patched = RomLocator.HpBarTextSites.Overlay;
                if (!ConfirmYesNo("This ROM has the patch for the name, level and HP. The update also removes the box behind the gender symbol, \"Lv\" and the caught ball." +
                    BackupNote($"overlay {patched}") + "\n\nUpdate the patch?" + CreditNote("hpBarText"), "Confirm to proceed"))
                    return false;
                string patchedPath = OverlayUtils.GetPath(patched);
                PlainFiles.Copy(patchedPath, patchedPath + BackupSuffix, overwrite: true);
                BattleGaugeTextPatch.AddTiles();
                ShowInfo("The box behind the gender symbol, \"Lv\" and the caught ball is gone too.", "Patch updated");
                return true;
            }
            if (PlacementNote(new[] { BattleGaugeTextPatch.BlockLength }, out int[] offsets) is not string placement) return false;
            int overlay = RomLocator.HpBarTextSites.Overlay;
            if (!ConfirmYesNo("Removes the white box behind the name, level, HP, gender symbol and \"Lv\" in battle, so your HP bar art shows behind them." +
                placement + BackupNote($"overlay {overlay} and the synthetic overlay") +
                "\n\nApply this patch?" + CreditNote("hpBarText"), "Confirm to proceed"))
                return false;
            string overlayPath = OverlayUtils.GetPath(overlay);
            PlainFiles.Copy(overlayPath, overlayPath + BackupSuffix, overwrite: true);
            PlainFiles.Copy(Filesystem.ExpArmPath, Filesystem.ExpArmPath + BackupSuffix, overwrite: true);
            BattleGaugeTextPatch.Apply(offsets[0]);
            ShowInfo("Your HP bar art now shows behind the name, level and HP.", "Patch applied");
            return true;
        }

        public static bool ApplySummaryFontsPatch()
        {
            if (AlreadyApplied(Probe(() => SummaryFontPatch.IsApplied))) return false;
            if (SummaryFontPatch.WhyNotApplicable() is string why) { ShowError(why, "Patch not applied"); return false; }
            if (PlacementNote(new[] { SummaryFontPatch.BlockLength }, out int[] offsets) is not string placement) return false;
            if (!ConfirmYesNo("Each text box on the summary screen can use any of the game's fonts. The narrow Pokéwalker font " +
                "fits long names. Nothing changes until you pick a font in the Screens editor's Summary layout." + placement +
                BackupNote("the ARM9 and the synthetic overlay") + "\n\nApply this patch?" + CreditNote("summaryFonts"), "Confirm to proceed"))
                return false;
            PlainFiles.Copy(RomInfo.Arm9Path, RomInfo.Arm9Path + BackupSuffix, overwrite: true);
            PlainFiles.Copy(Filesystem.ExpArmPath, Filesystem.ExpArmPath + BackupSuffix, overwrite: true);
            SummaryFontPatch.Apply(offsets[0]);
            ShowInfo("Pick each text box's font in the Screens editor's Summary layout.", "Patch applied");
            return true;
        }

        public static bool ApplyHpBarFontsPatch()
        {
            if (AlreadyApplied(Probe(() => HpBarFontPatch.IsApplied))) return false;
            if (HpBarFontPatch.WhyNotApplicable() is string why) { ShowError(why, "Patch not applied"); return false; }
            if (PlacementNote(new[] { HpBarFontPatch.BlockLength }, out int[] offsets) is not string placement) return false;
            if (!ConfirmYesNo("The Pokémon names on the HP bars in battle, and the Safari and Park ball counts, can use any of the game's fonts. " +
                "The narrow Pokéwalker font fits long names. Nothing changes until you pick a font in the Screens editor's HP bars." + placement +
                BackupNote("overlay 12 and the synthetic overlay") + "\n\nApply this patch?" + CreditNote("hpBarFonts"), "Confirm to proceed"))
                return false;
            PlainFiles.Copy(OverlayUtils.GetPath(12), OverlayUtils.GetPath(12) + BackupSuffix, overwrite: true);
            PlainFiles.Copy(Filesystem.ExpArmPath, Filesystem.ExpArmPath + BackupSuffix, overwrite: true);
            HpBarFontPatch.Apply(offsets[0]);
            ShowInfo("Pick the name font in the Screens editor's HP bars.", "Patch applied");
            return true;
        }

        public static bool ApplyExtraFontsPatch()
        {
            if (AlreadyApplied(Probe(() => ExtraFontsPatch.IsApplied))) return false;
            if (ExtraFontsPatch.WhyNotApplicable() is string why) { ShowError(why, "Patch not applied"); return false; }
            if (PlacementNote(new[] { ExtraFontsPatch.BlockLength }, out int[] offsets) is not string placement) return false;
            if (!ConfirmYesNo($"The game gets room for {ExtraFontsPatch.FontCount} fonts instead of {ExtraFontsPatch.GameFontCount}. " +
                "Nothing changes in game until you add a font in the Font editor." + placement +
                BackupNote("the ARM9 and the synthetic overlay") + "\n\nApply this patch?" + CreditNote("extraFonts"), "Confirm to proceed"))
                return false;
            PlainFiles.Copy(RomInfo.Arm9Path, RomInfo.Arm9Path + BackupSuffix, overwrite: true);
            PlainFiles.Copy(Filesystem.ExpArmPath, Filesystem.ExpArmPath + BackupSuffix, overwrite: true);
            ExtraFontsPatch.Apply(offsets[0]);
            ShowInfo("Add fonts in the Font editor.", "Patch applied");
            return true;
        }

        public static bool ApplyPartyFontsPatch()
        {
            if (AlreadyApplied(Probe(() => PartyFontPatch.IsApplied))) return false;
            if (PartyFontPatch.WhyNotApplicable() is string why) { ShowError(why, "Patch not applied"); return false; }
            if (PlacementNote(new[] { PartyFontPatch.BlockLength }, out int[] offsets) is not string placement) return false;
            if (!ConfirmYesNo("The Pokémon names and the line under them in the party menu can use any of the game's fonts. The narrow " +
                "Pokéwalker font fits long names. Nothing changes until you pick a font in the Screens editor's Party layout." + placement +
                BackupNote("the ARM9 and the synthetic overlay") + "\n\nApply this patch?" + CreditNote("partyFonts"), "Confirm to proceed"))
                return false;
            PlainFiles.Copy(RomInfo.Arm9Path, RomInfo.Arm9Path + BackupSuffix, overwrite: true);
            PlainFiles.Copy(Filesystem.ExpArmPath, Filesystem.ExpArmPath + BackupSuffix, overwrite: true);
            PartyFontPatch.Apply(offsets[0]);
            ShowInfo("Pick the name font in the Screens editor's Party layout.", "Patch applied");
            return true;
        }

        private static PatchState VsClassTableProbe()
        {
            if (RomInfo.IsHGE) return Unsupported(HgEngine.HgEngineSyntheticOverlay.ToolboxReason);
            if (RomInfo.GameFamily != GameFamilies.HGSS) return Unsupported("HeartGold and SoulSilver only");
            if (VsIntroTables.WhyNot() != null) return UnsupportedVersion(RomLocator.VsIntroClassTableDumps);
            try
            {
                VsIntroTables tables = VsIntroTables.Load();
                if (tables.ClassTableMoved) return PatchState.Applied;
                return Arm9Expanded() ? PatchState.Available : Unsupported("Requires ARM9 expansion");
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is InvalidOperationException) { return Unsupported("The VS intro tables couldn't be read"); }
        }

        public static bool ApplyVsClassTableExpansion()
        {
            VsIntroTables tables;
            try { tables = VsIntroTables.Load(); }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is InvalidOperationException) { ShowError(e.Message, "Patch not applied"); return false; }
            if (AlreadyApplied(tables.ClassTableMoved)) return false;
            if (tables.WhyNoClassRoom() is string why) { ShowError(why, "Patch not applied"); return false; }
            if (PlacementNote(new[] { VsIntroTables.ClassRoomBlockLength }, out int[] offsets) is not string placement) return false;
            if (!ConfirmYesNo($"More trainer classes can have their own VS intro and battle music: the table gets room for " +
                $"{VsIntroTables.MostClassRows} classes instead of {tables.ClassRowCount}." + placement +
                BackupNote("the ARM9 and the synthetic overlay") +
                "\n\nApply this patch?" + CreditNote("vsClassTableExpanded"), "Confirm to proceed"))
                return false;
            PlainFiles.Copy(RomInfo.Arm9Path, RomInfo.Arm9Path + BackupSuffix, overwrite: true);
            PlainFiles.Copy(Filesystem.ExpArmPath, Filesystem.ExpArmPath + BackupSuffix, overwrite: true);
            try { SyntheticOverlaySpace.PlaceAt(offsets, tables.MakeClassRoom); }
            catch (Exception e) when (e is IOException || e is InvalidOperationException || e is UnauthorizedAccessException)
            {
                ShowError("The class table wasn't moved:\n" + e.Message, "Patch not applied");
                return false;
            }
            ShowInfo($"The class table now has room for {VsIntroTables.MostClassRows} classes. Give classes their intro in the VS intro editor.", "Patch applied");
            return true;
        }

        public static bool ApplyDisableDynamicTexturesPatch()
        {
            if (!ConfirmYesNo("Texture animations stop on every map." + "\n\nApply this patch?" + CreditNote("disableTextures"), "Confirm to proceed"))
                return false;

            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { DirNames.AreaData });

            string[] adFiles = Directory.GetFiles(GameDirs[DirNames.AreaData].unpackedDir);
            foreach (string s in adFiles)
            {
                AreaData a = new AreaData(new FileStream(s, FileMode.Open, FileAccess.Read))
                {
                    GroundAnimation = 0xFFFF
                };
                a.SaveToFile(s, showSuccessMessage: false);
            }

            ShowInfo("Texture animations are now off on every map.", "Patch applied");
            return true;
        }

        /// <summary>Raises the trainer name length.</summary>
        public static bool ApplyExpandTrainerNamesPatch()
        {
            if (AlreadyApplied(RomPatchState.FlagTrainerNamesExpanded || RomInfo.TrainerNameMaxLen > TrainerFile.DefaultNameLen)) return false;

            if (!ConfirmYesNo($"Trainer names can be up to {RomPatchState.ExpandedTrainerNameLength - 1} letters long." +
                BackupNote("the ARM9") + "\n\nApply this patch?" + CreditNote("trainerNames"), "Confirm to proceed"))
                return false;

            try
            {
                ARM9.DecompressIfMarked();
                BackUp(RomInfo.Arm9Path);
                using (ARM9.Writer wr = new ARM9.Writer(RomInfo.TrainerNameLenOffset))
                {
                    wr.Write((byte)RomPatchState.ExpandedTrainerNameLength);
                }

                RomPatchState.FlagTrainerNamesExpanded = true;
                ShowInfo($"Trainer names can now be {RomPatchState.ExpandedTrainerNameLength - 1} letters long.", "Patch applied");
                return true;
            }
            catch (IOException)
            {
                ShowError("The ARM9 couldn't be written.", "Patch failed");
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

        /// <summary>Moves the script command table and count into the synthetic overlay (HGSS, English or Spanish).</summary>
        public static bool ApplyScrcmdRepointPatch()
        {
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.SynthOverlay });
            string expandedPath = Filesystem.ExpArmPath;
            if (!File.Exists(expandedPath))
            {
                ShowError("Apply the ARM9 expansion first. The synthetic overlay is missing.", "ARM9 expansion needed");
                return false;
            }

            if (GetCommandTableOffset() >= 0)
            {
                ShowInfo("The script command table is already in the synthetic overlay.", "Already applied");
                return true;
            }

            byte[] commandTablePayload;
            try
            {
                commandTablePayload = BuildCommandTablePayload();
            }
            catch
            {
                ShowError("The script command table patch doesn't support this ROM version.", "Unsupported");
                return false;
            }

            int blockLength = SyntheticOverlaySpace.HeaderSize + commandTablePayload.Length;
            if (PlacementNote(new[] { blockLength }, out int[] offsets) is not string placement) return false;
            uint blockOffset = (uint)offsets[0] + SyntheticOverlaySpace.HeaderSize;

            if (!ConfirmYesNo("The script command table and its count move into the synthetic overlay, and the ARM9's two " +
                "pointers to them are updated. This doesn't add commands." + placement + BackupNote("the ARM9 and the synthetic overlay") +
                "\n\nApply this patch?" + CreditNote("scrcmdRepoint"), "Confirm to proceed"))
                return false;

            try
            {
                PlainFiles.Copy(RomInfo.Arm9Path, RomInfo.Arm9Path + BackupSuffix, overwrite: true);
                PlainFiles.Copy(expandedPath, expandedPath + BackupSuffix, overwrite: true);
                DSUtils.WriteToFile(expandedPath, SyntheticOverlaySpace.Header(SyntheticOverlaySpace.CommandTableMarker, blockLength), (uint)offsets[0]);
                RepointCommandTable(blockOffset, commandTablePayload);
            }
            catch
            {
                ShowError("The script command table couldn't be moved. Restore the ARM9 from its backup (arm9.bin" + BackupSuffix + ").", "Patch failed");
                return false;
            }

            ShowInfo("The script command table has been moved. It doesn't add commands or update DSPRE's script command database.", "Patch applied");
            return true;
        }

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
                    if (cmdTable < SynthOverlayLoadAddress)
                    {
                        return -1;
                    }

                    uint offset = cmdTable - SynthOverlayLoadAddress;
                    string expandedPath = Filesystem.ExpArmPath;
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
            catch (Exception ex)
            {
                AppLogger.WarnOnce("PatchToolboxLogic.GetCommandTableOffset", ex);
                return -1;
            }
            return -1; // No table in expanded arm9 file
        }

        /// <summary>
        /// Synthetic overlay ranges taken by patches placed without a block marker: at an offset the user picked (building
        /// rotation, trainer shiny, the script command table) or at BDHCam's fixed offset. Each is found from what points
        /// at it, so a payload that was partly overwritten since is still kept. Never throws.
        /// </summary>
        public static List<(long Start, long End)> HandPlacedRanges()
        {
            List<(long Start, long End)> ranges = new List<(long Start, long End)>();
            byte[] arm9 = null;
            try { arm9 = File.ReadAllBytes(RomInfo.Arm9Path); } catch (Exception ex) { AppLogger.Warn("Hand-placed ranges: ARM9 unreadable: " + ex.Message); }

            try
            {
                if (BuildingRotationPatchData.SupportsCurrentRom())
                {
                    BuildingRotationPatchData data = new BuildingRotationPatchData();
                    byte[] hook = DSUtils.ReadFromFile(OverlayUtils.GetPath(data.OverlayNumber), data.HookOverlayOffset, 4);
                    if (TryGetThumbBlTarget(data.HookRuntimeAddress, hook, out uint target) && target >= SynthOverlayLoadAddress)
                    {
                        long offset = target - SynthOverlayLoadAddress;
                        if (offset + data.Payload.Length <= RomInfo.SynthOverlaySize) ranges.Add((offset, offset + data.Payload.Length));
                    }
                }
            }
            catch (Exception ex) { AppLogger.Warn("Hand-placed ranges: building rotation: " + ex.Message); }

            try
            {
                if (arm9 != null && TrainerShinyPatch.InstalledRange(arm9) is (long Start, long End) shiny) ranges.Add(shiny);
            }
            catch (Exception ex) { AppLogger.Warn("Hand-placed ranges: trainer shiny patch: " + ex.Message); }

            try
            {
                int table = GetCommandTableOffset();
                if (table >= 0)
                {
                    long block = table - ScrcmdTableOffsetInBlock;
                    // Commands added after the move lengthen the table, and the block's count says by how much.
                    uint count = ScrcmdOriginalCommandCount;
                    byte[] stored = DSUtils.ReadFromFile(Filesystem.ExpArmPath, (uint)(block + ScrcmdCountOffsetInBlock), 4);
                    if (stored.Length == 4) count = Math.Max(count, BitConverter.ToUInt32(stored, 0));
                    long end = Math.Min(table + 4L * count, RomInfo.SynthOverlaySize);
                    ranges.Add((block, end));
                }
            }
            catch (Exception ex) { AppLogger.Warn("Hand-placed ranges: synthetic overlay table: " + ex.Message); }

            try
            {
                if (arm9 != null && BDHCAMPatchData.SupportsCurrentRom())
                {
                    BDHCAMPatchData data = new BDHCAMPatchData();
                    byte[] branch = DSUtils.HexStringToByteArray(data.BranchString);
                    if (data.BranchOffset + branch.Length <= arm9.Length && arm9.AsSpan((int)data.BranchOffset, branch.Length).SequenceEqual(branch))
                        ranges.Add((BDHCAMPatchData.BDHCamSubroutineOffset, BDHCAMPatchData.BDHCamSubroutineOffset + data.Subroutine.Length));
                }
            }
            catch (Exception ex) { AppLogger.Warn("Hand-placed ranges: BDHC camera patch: " + ex.Message); }

            return ranges;
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

                uint expectedCountPointer = SynthOverlayLoadAddress + (uint)tableOffset - ScrcmdTableOffsetInBlock + ScrcmdCountOffsetInBlock;
                using (ARM9.Reader r = new ARM9.Reader(GetCustomScrcmdDBInt("commandCountPointerOffset")))
                {
                    return r.ReadUInt32() == expectedCountPointer;
                }
            }
            catch (Exception ex)
            {
                AppLogger.WarnOnce("PatchToolboxLogic.CheckScrcmdCommandCountPointerValid", ex);
                return false;
            }
        }

        private static bool CheckScrcmdBlockMarkers(int blockOffset)
        {
            string expandedPath = Filesystem.ExpArmPath;
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
            string value = customcmdDB.GetString(keyPrefix + "_" + RomInfo.GameVersion + "_" + RomInfo.GameLanguage);
            if (value == null)
            {
                throw new NotSupportedException();
            }

            return int.Parse(value);
        }

        /// <summary>Builds the moved block: COUN marker + command count + TABL marker + the vanilla command table.</summary>
        private static byte[] BuildCommandTablePayload()
        {
            byte[] originalTable = DSUtils.ReadFromFile(RomInfo.Arm9Path, (uint)GetCustomScrcmdDBInt("originalTableOffset"), ScrcmdOriginalTableLength);
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
            string expandedPath = Filesystem.ExpArmPath;
            DSUtils.WriteToFile(expandedPath, commandTablePayload, blockOffset);

            using (ARM9.Writer wr = new ARM9.Writer())
            {
                wr.BaseStream.Position = GetCustomScrcmdDBInt("pointerOffset");
                wr.Write(SynthOverlayLoadAddress + blockOffset + ScrcmdTableOffsetInBlock);

                wr.BaseStream.Position = GetCustomScrcmdDBInt("commandCountPointerOffset");
                wr.Write(SynthOverlayLoadAddress + blockOffset + ScrcmdCountOffsetInBlock);
            }
        }

        // The catalogue the ROM patch toolbox lists, with each patch's state and how to apply it.

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
            ["partyPanelSize"] = ("Mixone", "party panel size"),
            ["bannerPosition"] = ("Mixone", "place-name banner position"),
            ["bannerMotion"] = ("Mixone", "place-name banner motion"),
            ["buildingRotation"] = ("MrHam88", "building rotation"),
            ["trainerShiny"] = ("MrHam88", "shiny trainer Pokémon"),
            ["trainerCustomPokeballs"] = ("MrHam88", "trainer custom Poké Balls"),
            ["trainerClassMetadata"] = ("MrHam88", "trainer class metadata"),
            ["owSpriteExpansion"] = ("hzla", "custom overworld sprites"),
            ["platItemExpansion"] = ("hzla", "item expansion"),
            ["platExtraTms"] = ("hzla", "extra TMs"),
            ["trainerClassTablesExpanded"] = ("Mixone", "trainer class table expansion"),
            ["trainerEncounterBgmRepointed"] = ("Mixone", "trainer encounter music repoint"),
            ["pickupSlots"] = ("Mixone", "independent pickup slots"),
            ["classIntros"] = ("Mixone", "trainer class intros"),
            ["hpBarText"] = ("Mixone", "clear battle text background"),
            ["summaryFonts"] = ("Mixone", "summary screen fonts"),
            ["partyFonts"] = ("Mixone", "party menu fonts"),
            ["hpBarFonts"] = ("Mixone", "HP bar fonts"),
            ["extraFonts"] = ("Mixone", "extra fonts"),
            ["punchingMovesExpanded"] = ("Mixone", "punching move list expansion"),
            ["soundMovesExpanded"] = ("Mixone", "sound move list expansion"),
            ["regionalDexCount"] = ("Mixone", "regional Pokédex size"),
            ["evolutionSlots"] = ("Mixone", "evolution slots"),
            ["vsIntroTimings"] = ("Mixone", "VS intro timing"),
            ["vsClassTableExpanded"] = ("Mixone", "VS intro class table expansion"),
            ["typeChartExpanded"] = ("Mixone", "type chart expansion"),
            ["swarmTableExpanded"] = ("Mixone", "swarm table expansion"),
            ["bpShopExpanded"] = ("Mixone", "Battle Point list expansion"),
            ["martsExpanded"] = ("Mixone", "mart expansion"),
        };

        private const string MoveListResearch = "built on MrHam88's research and the DS Pokémon Hacking wiki's move editing guide (Yako and Lhea)";

        // What a patch was built on and how, as its author asked to be credited.
        private static readonly Dictionary<string, string> CreditSources = new Dictionary<string, string>
        {
            ["trainerCustomPokeballs"] = "built on the pokeheartgold decompilation",
            ["trainerClassMetadata"] = "built on the pokeheartgold decompilation and Mixone's Ghidra symbol map, and developed with AI assistance",
            ["punchingMovesExpanded"] = MoveListResearch,
            ["soundMovesExpanded"] = MoveListResearch,
            ["martsExpanded"] = "built on SpagoAsparago's guide to adding Poké Marts on the DS Pokémon Hacking wiki",
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
            !string.IsNullOrEmpty(RomInfo.WorkDir) && !SettingsManager.Settings.PatchCreditsHandled.Contains(RomInfo.WorkDir);

        /// <summary>Stops the after-save credits offer for this project.</summary>
        public static void MarkCreditsHandled()
        {
            if (!CreditsOfferDue()) return;
            SettingsManager.Settings.PatchCreditsHandled.Add(RomInfo.WorkDir);
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
            if (RomInfo.IsHGE) lines.Add("BluRosie and the hg-engine contributors");
            lines.Add("The DSPRE developers and its many contributors and research gurus");
            return string.Join("\n", lines);
        }

        /// <summary>Each toolbox patch's name, as the toolbox lists it and as editors that hand over to it say it.</summary>
        public static readonly IReadOnlyDictionary<string, string> PatchTitles = new Dictionary<string, string>
        {
            ["sameHeldItemOdds"] = "Same held item odds",
            ["partyPanelSize"] = "Party panel size",
            ["bannerPosition"] = "Place-name banner position",
            ["bannerMotion"] = "Place-name banner motion",
            ["sentenceCase"] = "Sentence-case Pokémon names",
            ["itemSentenceCase"] = "Sentence-case item names",
            ["itemStandardize"] = "Item script order",
            ["punchingMovesExpanded"] = "Punching move list expansion",
            ["regionalDexCount"] = "Regional Pokédex size",
            ["evolutionSlots"] = "Evolution slots",
            ["martsExpanded"] = "Mart expansion",
            ["bpShopExpanded"] = "Battle Point list expansion",
            ["swarmTableExpanded"] = "Swarm table expansion",
            ["typeChartExpanded"] = "Type chart expansion",
            ["soundMovesExpanded"] = "Sound move list expansion",
            ["arm9"] = "ARM9 expansion",
            ["bdhcam"] = "Dynamic cameras (BDHCam)",
            ["buildingRotation"] = "Building rotation",
            ["trainerShiny"] = "Shiny trainer Pokémon",
            ["trainerClassMetadata"] = "Trainer class metadata",
            ["vsIntroTimings"] = "VS intro timings per class",
            ["vsClassTableExpanded"] = "VS intro class table expansion",
            ["dynamicHeaders"] = "Dynamic map headers",
            ["matrix"] = "Matrix 0 expansion",
            ["scrcmdRepoint"] = "Script command table expansion",
            ["disableTextures"] = "No texture animations",
            ["trainerNames"] = "Trainer name length",
            ["trainerClassTablesExpanded"] = "Trainer class tables",
            ["trainerEncounterBgmRepointed"] = "Trainer encounter music table",
            ["pickupSlots"] = "Independent pickup slots",
            ["classIntros"] = "Trainer class intros",
            ["hpBarText"] = "Clear battle text background",
            ["summaryFonts"] = "Summary screen fonts",
            ["partyFonts"] = "Party menu fonts",
            ["hpBarFonts"] = "HP bar fonts",
            ["extraFonts"] = "Extra fonts",
            ["trainerCustomPokeballs"] = "Trainer custom Poké Balls",
        };

        // Some checks (BDHCam) decompress an overlay as they look.
        public static List<PatchInfo> GetPatchStatuses()
        {
            List<PatchInfo> list = new List<PatchInfo>();

            list.Add(Status("partyPanelSize",
                "Lets the Pokémon panels in the party menu be any size up to 96 tiles, set in the Screens editor (HeartGold and SoulSilver). Needs no ARM9 expansion.",
                PartyPanelSizeState));

            list.Add(Status("bannerPosition",
                "Lets the place-name banner sit anywhere on the screen. Move it in the Screens editor. Needs no ARM9 expansion.",
                BannerPositionState));

            list.Add(Status("bannerMotion",
                "Lets the place-name banner slide in from the bottom or appear without sliding, bounce or ease, and fade or pixelate. Pick it in the Screens editor. Requires the ARM9 expansion patch.",
                () =>
                {
                    if (RomInfo.IsHGE) return Unsupported(HgEngine.HgEngineSyntheticOverlay.ToolboxReason);
                    if (!BannerMotionPatch.SupportsCurrentRom) return Unsupported("Platinum, HeartGold and SoulSilver only");
                    if (BannerMotionPatch.IsApplied) return PatchState.Applied;
                    return Arm9Expanded() ? PatchState.Available : Unsupported("Requires ARM9 expansion");
                }));

            list.Add(Status("sameHeldItemOdds",
                "Wild Pokémon whose two held items are the same normally always hold it. With this patch they hold it only as often as the held item odds say.",
                SameHeldItemOddsState));

            list.Add(Status("sentenceCase",
                "Writes every Pokémon name in sentence case instead of capitals, including Pokémon you renamed.",
                () => PatchState.Available));   // no reliable applied-detection

            list.Add(Status("itemSentenceCase",
                "Writes every item name in sentence case instead of capitals, including items you renamed.",
                () => PatchState.Available));   // no reliable applied-detection

            list.Add(Status("itemStandardize",
                "Puts item scripts in item number order and updates every item event to match.",
                () =>
                {
                    if (RomInfo.IsHGE) return Unsupported("Not compatible with hg-engine");
                    DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.Scripts });
                    bool applied = RomPatchState.FlagStandardizedItems || CheckScriptsStandardizedItemNumbers();
                    return applied ? PatchState.Applied : PatchState.Available;
                }));

            PatchInfo punching = Status("punchingMovesExpanded",
                $"Moves the list of punching moves Iron Fist boosts into the expanded ARM9 area with room for {MoveCategoryTable.ExpandedCapacity}, so the Move data editor can change which moves are punching. Requires the ARM9 expansion patch.",
                () => MoveListProbe(MoveCategoryTable.Kind.Punching));
            punching.Guide = MoveCategoryTable.GuideUrl(MoveCategoryTable.Kind.Punching);
            list.Add(punching);

            list.Add(Status("regionalDexCount",
                "Sets the counts the game checks the regional Pokédex against (when it is complete, the professor's rating and the regional number lookup) to the size of the regional order saved in Pokédex lists.",
                RegionalDexCountProbe));

            list.Add(Status("evolutionSlots",
                $"Gives every Pokémon room for {EvolutionSlots.Max} evolutions instead of {EvolutionSlots.Vanilla}, so the Pokémon editor's Evolutions tab can list more.",
                EvolutionSlotsProbe));

            list.Add(Status("martsExpanded",
                $"Moves every mart into the expanded ARM9 area with room for {MartData.RoomyShops} marts of {MartData.RoomyItems} items, so the Mart editor can add items and custom marts. Requires the ARM9 expansion patch.",
                MartExpansionProbe));

            list.Add(Status("bpShopExpanded",
                $"Moves the Battle Point exchange's item and TM counters and their prices into the expanded ARM9 area with room for {BpShopData.MaxListItems} items each, so more can be added in the Battle Point shop editor (Platinum). Requires the ARM9 expansion patch.",
                BpShopExpansionProbe));

            list.Add(Status("swarmTableExpanded",
                $"Moves the table of places a swarm can happen into the expanded ARM9 area with room for {SwarmTable.MaxRows} rows, so the Special encounters editor's Swarms tab can add more. Requires the ARM9 expansion patch.",
                SwarmTableExpansionProbe));

            list.Add(Status("typeChartExpanded",
                $"Moves the type chart into the expanded ARM9 area with room for {TypeChart.ExpandedCapacity - 2} matchups, so the Type chart editor can add more. Requires the ARM9 expansion patch.",
                TypeChartExpansionProbe));

            PatchInfo sound = Status("soundMovesExpanded",
                $"Moves the list of sound moves Soundproof blocks into the expanded ARM9 area with room for {MoveCategoryTable.ExpandedCapacity}, so the Move data editor can change which moves are sound. The trainer AI's own sound list isn't changed. Requires the ARM9 expansion patch.",
                () => MoveListProbe(MoveCategoryTable.Kind.Sound));
            sound.Guide = MoveCategoryTable.GuideUrl(MoveCategoryTable.Kind.Sound);
            list.Add(sound);

            list.Add(Status("arm9",
                "Adds about 88 KB of usable ARM9 memory, the synthetic overlay. Every patch here that says it requires the ARM9 expansion puts its code or tables there. For advanced users: it can break the game if misused.",
                () =>
                {
                    if (RomInfo.IsHGE) return Unsupported(HgEngine.HgEngineSyntheticOverlay.ToolboxReason);
                    if (!ARM9PatchData.Supported) return UnsupportedVersion(ARM9PatchData.SupportedDumps);
                    bool applied = RomPatchState.FlagArm9Expanded || CheckFilesArm9ExpansionApplied();
                    return applied ? PatchState.Applied : PatchState.Available;
                }));

            list.Add(Status("bdhcam",
                "Lets maps change the camera as the player walks, from BDHCam files (Platinum, HeartGold and SoulSilver in English or Spanish). Requires the ARM9 expansion patch.",
                () =>
                {
                    if (RomInfo.IsHGE) return Unsupported(HgEngine.HgEngineSyntheticOverlay.ToolboxReason);
                    if (!ScrcmdLikeLangOk()) return UnsupportedVersion(ScrcmdLikeDumps());
                    if (RomInfo.GameFamily == GameFamilies.HGSS && !RomInfo.IsDsRomProject) return Unsupported("Convert to ds-rom");
                    if (!Arm9Expanded()) return Unsupported("Requires ARM9 expansion");
                    bool applied = RomPatchState.FlagBDHCamPatchApplied || CheckFilesBDHCamPatchApplied();
                    return applied ? PatchState.Applied : PatchState.Available;
                }));

            list.Add(Status("buildingRotation",
                "Buildings placed in the Map editor keep their rotation in game. Requires the ARM9 expansion patch and a ds-rom project.",
                () =>
                {
                    if (RomInfo.IsHGE) return Unsupported(HgEngine.HgEngineSyntheticOverlay.ToolboxReason);
                    if (!RomInfo.IsDsRomProject) return Unsupported("Convert to ds-rom");
                    if (!BuildingRotationPatchData.SupportsCurrentRom()) return UnsupportedVersion(RomLocator.WorksOn(RomLocator.BuildingRotationSitesIn));
                    if (!Arm9Expanded()) return Unsupported("Requires ARM9 expansion");
                    bool applied = RomPatchState.FlagBuildingRotationPatchApplied || CheckFilesBuildingRotationPatchApplied();
                    return applied ? PatchState.Applied : PatchState.Available;
                }));

            list.Add(Status("trainerShiny",
                "Party members ticked Shiny in the Trainer editor are shiny in battle (HeartGold and SoulSilver). Requires the ARM9 expansion patch and a ds-rom project.",
                () =>
                {
                    if (RomInfo.IsHGE) return Unsupported(HgEngine.HgEngineSyntheticOverlay.ToolboxReason);
                    if (!TrainerShinyPatch.SupportsCurrentRom) return UnsupportedVersion(RomLocator.WorksOn(RomLocator.TrainerShinySitesIn));
                    if (!RomInfo.IsDsRomProject) return Unsupported("Convert to ds-rom");
                    if (!Arm9Expanded()) return Unsupported("Requires ARM9 expansion");
                    return TrainerShinyPatch.DetectCurrentProject() ? PatchState.Applied : PatchState.Available;
                }));

            list.Add(Status("trainerClassMetadata",
                "Gives every trainer class its own record for gender, prize, eye-contact and battle music and VS intro, edited in the Trainer editor's Classes tab and the VS intro editor (US HeartGold and SoulSilver). Requires the ARM9 expansion patch and a ds-rom project.",
                () =>
                {
                    if (RomInfo.IsHGE) return Unsupported(HgEngine.HgEngineSyntheticOverlay.ToolboxReason);
                    if (!TrainerClassMetadataPatch.SupportsCurrentRom) return UnsupportedVersion(RomInfo.WithTwins(new[] { ("IPKE", 0), ("IPGE", 0) }));
                    if (!RomInfo.IsDsRomProject) return Unsupported("Convert to ds-rom");
                    if (!Arm9Expanded()) return Unsupported("Requires ARM9 expansion");
                    return TrainerClassMetadataStore.DetectCurrentRom(out _) switch
                    {
                        TrainerClassMetadataDetectionState.SchemaV1 => PatchState.Applied,
                        TrainerClassMetadataDetectionState.Inconsistent => Unsupported("Partly applied"),
                        _ => PatchState.Available,
                    };
                }));

            list.Add(Status("vsIntroTimings",
                $"Lets each trainer class set its own VS intro timings (flashes, slides, holds) in the VS intro editor, for up to {VsIntroTimingAddon.Classes} classes. US HeartGold and SoulSilver with the trainer class metadata patch.",
                VsIntroTimingsProbe));

            list.Add(Status("vsClassTableExpanded",
                $"Gives the table that picks each trainer class's VS intro and battle music room for {VsIntroTables.MostClassRows} classes (HeartGold and SoulSilver). Requires the ARM9 expansion patch.",
                VsClassTableProbe));

            list.Add(Status("dynamicHeaders",
                "Loads map headers from their own files instead of a fixed table, so headers can be added (Platinum, HeartGold and SoulSilver).",
                () =>
                {
                    // Its offsets are known for the US, European and Japanese ARM9s only.
                    if (RomInfo.GameFamily == GameFamilies.DP || RomInfo.GameLanguage == GameLanguages.Korean)
                        return UnsupportedVersion(RomInfo.KnownDumps.Where(d => (d.id.StartsWith("CPU") || d.id.StartsWith("IP")) && d.id[3] != 'K'));
                    bool applied = RomPatchState.FlagDynamicHeadersPatchApplied || CheckFilesDynamicHeadersPatchApplied();
                    return applied ? PatchState.Applied : PatchState.Available;
                }));

            list.Add(Status("matrix",
                "Lets matrix 0 grow to twice its size (HeartGold and SoulSilver in English or Spanish).",
                () =>
                {
                    if (RomInfo.GameFamily != GameFamilies.HGSS) return Unsupported("HeartGold and SoulSilver only");
                    if (RomInfo.GameLanguage != GameLanguages.English && RomInfo.GameLanguage != GameLanguages.Spanish)
                        return UnsupportedVersion(RomInfo.KnownDumps.Where(d => d.id.StartsWith("IP") && (d.id[3] == 'E' || d.id[3] == 'S')));
                    bool applied = RomPatchState.FlagMatrixExpansionApplied || CheckFilesMatrixExpansionApplied();
                    return applied ? PatchState.Applied : PatchState.Available;
                }));

            list.Add(Status("scrcmdRepoint",
                "Moves the script command table into the synthetic overlay so custom commands can be added (HeartGold and SoulSilver in English or Spanish). Requires the ARM9 expansion patch.",
                () =>
                {
                    if (RomInfo.IsHGE) return Unsupported(HgEngine.HgEngineSyntheticOverlay.ToolboxReason);
                    if (!ScrcmdLikeLangOk() || RomInfo.GameFamily != GameFamilies.HGSS)
                        return UnsupportedVersion(ScrcmdLikeDumps().Where(d => d.id.StartsWith("IP")));
                    if (!Arm9Expanded()) return Unsupported("Requires ARM9 expansion");
                    return IsScrcmdRepointApplied() ? PatchState.Applied : PatchState.Available;
                }));

            list.Add(Status("disableTextures",
                "Turns off texture animations on every map (HeartGold and SoulSilver).",
                () => RomInfo.GameFamily == GameFamilies.HGSS ? PatchState.Available : UnsupportedVersion(RomInfo.KnownDumps.Where(d => d.id.StartsWith("IP")))));

            list.Add(Status("trainerNames",
                $"Trainer names can be up to {RomPatchState.ExpandedTrainerNameLength - 1} letters long.",
                () =>
                {
                    if (RomPatchState.FlagTrainerNamesExpanded) return AppliedHere();
                    if (RomInfo.TrainerNameLenOffset < 0) return UnsupportedVersion(RomLocator.WorksOn(RomLocator.Thing.TrainerNameLength));
                    if (RomInfo.TrainerNameMaxLen > TrainerFile.DefaultNameLen)
                    {
                        RomPatchState.FlagTrainerNamesExpanded = true;
                        return AppliedHere();
                    }
                    return PatchState.Available;
                }));

            list.Add(PlatPatchesStatus());
            list.Add(new PatchInfo
            {
                Key = "g4Patcher",
                Title = "G4Patcher",
                Author = "Kalaay",
                Link = "https://github.com/KalaayPT/G4Patcher",
                Description = "Applies .asm code patches, such as EV and IV checking or a portable PC, to an unpacked project folder.",
                State = PatchState.Unsupported,
                Reason = "Separate tool",
            });

            list.Add(Status("trainerClassTablesExpanded",
                $"Moves the gender and prize money tables into the synthetic overlay with room for {TrainerClassTableExpansion.MaxClasses} classes, so the Trainer editor's Classes tab can add classes. Requires the ARM9 expansion patch.",
                () =>
                {
                    if (!TrainerClassTableExpansion.IsSupportedForCurrentRom) return Unsupported("Platinum only");
                    if (TrainerClassTableExpansion.ClassTablesHaveRoom) return PatchState.Applied;
                    return Arm9Expanded() ? PatchState.Available : Unsupported("Requires ARM9 expansion");
                }));

            list.Add(Status("trainerEncounterBgmRepointed",
                $"Moves the eye-contact music table into the synthetic overlay with room for an entry per class, so the Trainer editor's Classes tab can give more classes music. Requires the ARM9 expansion patch.",
                () =>
                {
                    if (TrainerClassTableExpansion.MusicTableHasRoom) return PatchState.Applied;
                    if (!TrainerClassTableExpansion.IsSupportedForCurrentRom) return Unsupported("Platinum only");
                    return Arm9Expanded() ? PatchState.Available : Unsupported("Requires ARM9 expansion");
                }));

            list.Add(Status("pickupSlots",
                "Each Pickup level range gets its own nine items and two rare items. Requires the ARM9 expansion patch.",
                () =>
                {
                    if (RomInfo.IsHGE) return Unsupported(HgEngine.HgEngineSyntheticOverlay.ToolboxReason);
                    if (!PickupSlotsPatch.SupportsCurrentRom) return UnsupportedVersion(RomLocator.WorksOn(RomLocator.PickupSlotSitesIn));
                    if (PickupSlotsPatch.IsApplied) return PatchState.Applied;
                    return Arm9Expanded() ? PatchState.Available : Unsupported("Requires ARM9 expansion");
                }));

            list.Add(Status("classIntros",
                "Each trainer class gets its own VS intro: style, battle music, and for gym leader and Elite Four styles its own look, edited in the VS intro editor (Diamond, Pearl and Platinum). Requires the ARM9 expansion patch.",
                () =>
                {
                    if (RomInfo.IsHGE) return Unsupported(HgEngine.HgEngineSyntheticOverlay.ToolboxReason);
                    if (RomInfo.GameFamily == RomInfo.GameFamilies.HGSS) return Unsupported("Diamond, Pearl and Platinum only");
                    if (!ClassIntroPatch.SupportsCurrentRom) return UnsupportedVersion(ClassIntroPatch.SupportedDumps);
                    if (ClassIntroPatch.IsApplied) return PatchState.Applied;
                    return Arm9Expanded() ? PatchState.Available : Unsupported("Requires ARM9 expansion");
                }));

            list.Add(Status("summaryFonts",
                "Lets each text box on the summary screen use any of the game's fonts, set in the Screens editor (HeartGold and SoulSilver). Requires the ARM9 expansion patch.",
                () =>
                {
                    if (RomInfo.IsHGE) return Unsupported(HgEngine.HgEngineSyntheticOverlay.ToolboxReason);
                    if (RomInfo.GameFamily != RomInfo.GameFamilies.HGSS) return Unsupported("HeartGold and SoulSilver only");
                    if (!SummaryFontPatch.SupportsCurrentRom) return UnsupportedVersion(RomLocator.SummaryFontDumps);
                    if (SummaryFontPatch.IsApplied) return PatchState.Applied;
                    return Arm9Expanded() ? PatchState.Available : Unsupported("Requires ARM9 expansion");
                }));

            list.Add(Status("partyFonts",
                "Lets the Pokémon names and the line under them in the party menu use any of the game's fonts, set in the Screens editor (HeartGold and SoulSilver). Requires the ARM9 expansion patch.",
                () =>
                {
                    if (RomInfo.IsHGE) return Unsupported(HgEngine.HgEngineSyntheticOverlay.ToolboxReason);
                    if (RomInfo.GameFamily != RomInfo.GameFamilies.HGSS) return Unsupported("HeartGold and SoulSilver only");
                    if (!PartyFontPatch.SupportsCurrentRom) return UnsupportedVersion(RomLocator.PartyFontDumps);
                    if (PartyFontPatch.IsApplied) return PatchState.Applied;
                    return Arm9Expanded() ? PatchState.Available : Unsupported("Requires ARM9 expansion");
                }));

            list.Add(Status("hpBarFonts",
                "Lets the Pokémon names on the HP bars in battle, and the Safari and Park ball counts, use any of the game's fonts, set in the Screens editor (HeartGold and SoulSilver). Requires the ARM9 expansion patch.",
                () =>
                {
                    if (RomInfo.IsHGE) return Unsupported(HgEngine.HgEngineSyntheticOverlay.ToolboxReason);
                    if (RomInfo.GameFamily != RomInfo.GameFamilies.HGSS) return Unsupported("HeartGold and SoulSilver only");
                    if (!HpBarFontPatch.SupportsCurrentRom) return UnsupportedVersion(RomLocator.HpBarFontDumps);
                    if (HpBarFontPatch.IsApplied) return PatchState.Applied;
                    return Arm9Expanded() ? PatchState.Available : Unsupported("Requires ARM9 expansion");
                }));

            list.Add(Status("extraFonts",
                $"Lets you add up to {ExtraFontsPatch.Capacity} fonts in the Font editor (HeartGold and SoulSilver). Requires the ARM9 expansion patch.",
                () =>
                {
                    if (RomInfo.IsHGE) return Unsupported(HgEngine.HgEngineSyntheticOverlay.ToolboxReason);
                    if (RomInfo.GameFamily != RomInfo.GameFamilies.HGSS) return Unsupported("HeartGold and SoulSilver only");
                    if (!ExtraFontsPatch.SupportsCurrentRom) return UnsupportedVersion(RomLocator.ExtraFontsDumps);
                    if (ExtraFontsPatch.IsApplied) return PatchState.Applied;
                    return Arm9Expanded() ? PatchState.Available : Unsupported("Requires ARM9 expansion");
                }));

            list.Add(Status("hpBarText",
                "Removes the white box behind the name, level, HP, gender symbol and \"Lv\" in battle. Requires the ARM9 expansion patch.",
                () =>
                {
                    if (RomInfo.IsHGE) return Unsupported(HgEngine.HgEngineSyntheticOverlay.ToolboxReason);
                    if (!BattleGaugeTextPatch.SupportsCurrentRom) return UnsupportedVersion(RomLocator.HpBarTextDumps);
                    if (BattleGaugeTextPatch.IsApplied) return PatchState.Applied;
                    return Arm9Expanded() ? PatchState.Available : Unsupported("Requires ARM9 expansion");
                }));

            list.Add(Status("trainerCustomPokeballs",
                "Lets each trainer party member have its own Poké Ball instead of ball capsule seals. Needs no ARM9 expansion.",
                () =>
                {
                    if (RomInfo.IsHGE) return Unsupported("Not compatible with hg-engine");
                    if (!TrainerCustomPokeballsPatch.SupportsCurrentRom)
                        return UnsupportedVersion(RomInfo.KnownDumps.Where(d => TrainerCustomPokeballsPatch.Supports(d.id)));
                    if (TrainerCustomPokeballsPatch.DetectCurrentProject()) return PatchState.Applied;
                    return TrainerCustomPokeballsPatch.WhyNotApplicable(RomInfo.RomID, File.ReadAllBytes(RomInfo.Arm9Path)) == null
                        ? PatchState.Available : Unsupported("Capsule routine modified");
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
            if (RomInfo.GameFamily != GameFamilies.Plat || RomInfo.IsHGE)
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
            catch (Exception ex) { AppLogger.WarnOnce("PatchToolboxLogic.Part", ex); note = null; }
            return new PatchPart { Key = key, Title = title, Applied = note != null, Note = note };
        }

        private static bool Arm9Expanded() => RomPatchState.FlagArm9Expanded || CheckFilesArm9ExpansionApplied();

        private static PatchState MartExpansionProbe()
        {
            if (RomInfo.IsHGE) return Unsupported("hg-engine keeps the marts in its own source");
            if (!RomInfo.IsMartEditorAvailable())
                return UnsupportedVersion(RomLocator.WorksOnAll(RomLocator.Thing.MartCommonCount, RomLocator.Thing.MartCommonPointer, RomLocator.Thing.MartSpecialtyPointer));
            if (!Arm9Expanded()) return Unsupported("Requires ARM9 expansion");
            try { return MartData.LoadCurrent().Expanded ? PatchState.Applied : PatchState.Available; }
            catch (Exception e) when (e is IOException || e is InvalidOperationException) { return Unsupported("The marts couldn't be read"); }
        }

        /// <summary>Moves the saved marts into the roomy layout, after backing up what it changes.</summary>
        public static bool ApplyMartExpansion()
        {
            MartData marts;
            try { marts = MartData.LoadCurrent(); }
            catch (Exception e) when (e is IOException || e is InvalidOperationException) { ShowError(e.Message, "Patch not applied"); return false; }
            if (AlreadyApplied(marts.Expanded)) return false;
            if (PlacementNote(new[] { MartData.RoomyLength }, out int[] offsets) is not string placement) return false;
            if (!ConfirmYesNo($"Marts can grow and custom marts can be added in the Mart editor: there is room for {MartData.RoomyShops} marts of " +
                $"{MartData.RoomyItems} items each." + placement + BackupNote("the ARM9 and the synthetic overlay") +
                "\n\nApply this patch?" + CreditNote("martsExpanded"), "Confirm to proceed"))
                return false;
            PlainFiles.Copy(RomInfo.Arm9Path, RomInfo.Arm9Path + BackupSuffix, overwrite: true);
            PlainFiles.Copy(Filesystem.ExpArmPath, Filesystem.ExpArmPath + BackupSuffix, overwrite: true);
            try { SyntheticOverlaySpace.PlaceAt(offsets, marts.MoveToRoomyLayout); }
            catch (Exception e) when (e is IOException || e is InvalidOperationException || e is UnauthorizedAccessException)
            {
                ShowError("The marts weren't moved:\n" + e.Message, "Patch not applied");
                return false;
            }
            ShowInfo($"The marts now have room for {MartData.RoomyShops} marts of {MartData.RoomyItems} items each.", "Patch applied");
            return true;
        }

        private static PatchState BpShopExpansionProbe()
        {
            if (RomInfo.IsHGE || RomInfo.GameFamily != RomInfo.GameFamilies.Plat) return Unsupported("Platinum only");
            if (BpShopData.WhyNot() is string bpWhy) return NotHere(RomLocator.BpShopDumps.Where(d => d.id.StartsWith("CPU")), bpWhy);
            if (!Arm9Expanded()) return Unsupported("Requires ARM9 expansion");
            try
            {
                BpShopData shop = BpShopData.Load();
                if (shop.MovedByPatch) return Unsupported("Moved by another patch");
                return shop.Expanded ? PatchState.Applied : PatchState.Available;
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is InvalidOperationException) { return Unsupported("The lists couldn't be read"); }
        }

        /// <summary>Moves the saved Battle Point lists into the expanded ARM9 area, after backing up what it changes.</summary>
        public static bool ApplyBpShopExpansion()
        {
            if (RomInfo.GameFamily != RomInfo.GameFamilies.Plat) { ShowError("The Battle Point lists can be expanded in Platinum only.", "Patch not applied"); return false; }
            BpShopData shop;
            try { shop = BpShopData.Load(); }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is InvalidOperationException) { ShowError(e.Message, "Patch not applied"); return false; }
            if (AlreadyApplied(shop.Expanded)) return false;
            if (PlacementNote(new[] { shop.ExpansionBlockLength }, out int[] offsets) is not string placement) return false;
            if (!ConfirmYesNo($"The Battle Point exchange can sell more: each counter holds up to {BpShopData.MaxListItems} items, and there's room for " +
                $"{BpShopData.MaxPriceRows} prices in all." + placement + BackupNote("the ARM9, overlay 7 and the synthetic overlay") +
                "\n\nApply this patch?" + CreditNote("bpShopExpanded"), "Confirm to proceed"))
                return false;
            PlainFiles.Copy(RomInfo.Arm9Path, RomInfo.Arm9Path + BackupSuffix, overwrite: true);
            PlainFiles.Copy(OverlayUtils.GetPath(7), OverlayUtils.GetPath(7) + BackupSuffix, overwrite: true);
            PlainFiles.Copy(Filesystem.ExpArmPath, Filesystem.ExpArmPath + BackupSuffix, overwrite: true);
            try { SyntheticOverlaySpace.PlaceAt(offsets, shop.MoveToExpansion); }
            catch (Exception e) when (e is IOException || e is InvalidOperationException || e is UnauthorizedAccessException)
            {
                ShowError("The Battle Point lists weren't moved:\n" + e.Message, "Patch not applied");
                return false;
            }
            ShowInfo($"Each Battle Point counter can now hold {BpShopData.MaxListItems} items.", "Patch applied");
            return true;
        }

        private static PatchState SwarmTableExpansionProbe()
        {
            if (RomInfo.IsHGE) return Unsupported("hg-engine keeps the swarms in its own source");
            if (SwarmTable.WhyNot() is string swarmWhy) return NotHere(RomLocator.SwarmDumps, swarmWhy);
            if (!Arm9Expanded()) return Unsupported("Requires ARM9 expansion");
            try { return SwarmTable.Load().Expanded ? PatchState.Applied : PatchState.Available; }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is InvalidOperationException) { return Unsupported("The table couldn't be read"); }
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
            if (!ConfirmYesNo($"More places can have swarms: the swarm table gets room for {SwarmTable.MaxRows} rows instead of {table.Rows.Count}." +
                placement + BackupNote($"{code} and the synthetic overlay") +
                "\n\nApply this patch?" + CreditNote("swarmTableExpanded"), "Confirm to proceed"))
                return false;
            PlainFiles.Copy(table.CodePath, table.CodePath + BackupSuffix, overwrite: true);
            PlainFiles.Copy(Filesystem.ExpArmPath, Filesystem.ExpArmPath + BackupSuffix, overwrite: true);
            try { SyntheticOverlaySpace.PlaceAt(offsets, table.MoveToExpansion); }
            catch (Exception e) when (e is IOException || e is InvalidOperationException || e is UnauthorizedAccessException)
            {
                ShowError("The swarm table wasn't moved:\n" + e.Message, "Patch not applied");
                return false;
            }
            ShowInfo($"The swarm table can now hold {SwarmTable.MaxRows} rows.", "Patch applied");
            return true;
        }

        private static PatchState TypeChartExpansionProbe()
        {
            if (RomInfo.IsHGE) return Unsupported("hg-engine keeps the chart in its own source");
            if (TypeChart.WhyNot() is string chartWhy) return NotHere(RomLocator.WorksOn(RomLocator.Thing.TypeChartPointers), chartWhy);
            if (!Arm9Expanded()) return Unsupported("Requires ARM9 expansion");
            try { return TypeChart.Load().InExpansion ? PatchState.Applied : PatchState.Available; }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is InvalidOperationException) { return Unsupported("The chart couldn't be read"); }
        }

        /// <summary>Moves the saved type chart into the expanded ARM9 area, after backing up what it changes.</summary>
        public static bool ApplyTypeChartExpansion()
        {
            TypeChart chart;
            try { chart = TypeChart.Load(); }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is InvalidOperationException) { ShowError(e.Message, "Patch not applied"); return false; }
            if (AlreadyApplied(chart.InExpansion)) return false;
            if (PlacementNote(new[] { TypeChart.ExpansionBlockLength }, out int[] offsets) is not string placement) return false;
            if (!ConfirmYesNo($"The type chart gets room for {TypeChart.ExpandedCapacity - 2} matchups, and Conversion 2 follows it"
                + (RomInfo.GameFamily == RomInfo.GameFamilies.HGSS ? "." : ", as does the Pokétch's copy.") + placement + BackupNote("the battle overlay and the synthetic overlay") +
                "\n\nApply this patch?" + CreditNote("typeChartExpanded"), "Confirm to proceed"))
                return false;
            PlainFiles.Copy(chart.CodePath, chart.CodePath + BackupSuffix, overwrite: true);
            PlainFiles.Copy(Filesystem.ExpArmPath, Filesystem.ExpArmPath + BackupSuffix, overwrite: true);
            try { SyntheticOverlaySpace.PlaceAt(offsets, chart.MoveToExpansion); }
            catch (Exception e) when (e is IOException || e is InvalidOperationException || e is UnauthorizedAccessException)
            {
                ShowError((chart.InExpansion ? "The type chart moved, but the Pokétch copy wasn't updated:" : "The type chart wasn't moved:") + "\n" + e.Message, "Patch not applied");
                if (!chart.InExpansion) return false;
            }
            ShowInfo("The type chart can now hold more matchups.", "Patch applied");
            return true;
        }

        private static PatchState VsIntroTimingsProbe()
        {
            if (RomInfo.IsHGE) return Unsupported(HgEngine.HgEngineSyntheticOverlay.ToolboxReason);
            if (RomInfo.GameLanguage != RomInfo.GameLanguages.English || (RomInfo.RomID != "IPKE" && RomInfo.RomID != "IPGE"))
                return UnsupportedVersion(RomInfo.WithTwins(new[] { ("IPKE", 0), ("IPGE", 0) }));
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
            if (!ConfirmYesNo("Each of " + VsIntroTimingAddon.Classes + " trainer classes can set its own VS intro timings in the VS intro " +
                "editor. A timing of 0 keeps the game's value." + placement + BackupNote("overlays 115, 117, 118 and 119 and the synthetic overlay") +
                "\n\nApply this patch?" + CreditNote("vsIntroTimings"),
                "Confirm to proceed"))
                return false;
            foreach (int overlay in VsIntroTimingAddon.HookedOverlays)
            {
                string path = VsIntroTimingAddon.OverlayFilePath(overlay);
                PlainFiles.Copy(path, path + BackupSuffix, overwrite: true);
            }
            PlainFiles.Copy(Filesystem.ExpArmPath, Filesystem.ExpArmPath + BackupSuffix, overwrite: true);
            try { SyntheticOverlaySpace.PlaceAt(offsets, () => VsIntroTimingAddon.Install()); }
            catch (Exception e) when (e is InvalidOperationException || e is IOException || e is UnauthorizedAccessException)
            {
                ShowError("The VS intro timings weren't added:\n" + e.Message, "Patch not applied");
                return false;
            }
            ShowInfo("Each trainer class can now set its own VS intro timings in the VS intro editor.", "Patch applied");
            return true;
        }

        private static PatchState RegionalDexCountProbe()
        {
            if (RomInfo.IsHGE) return Unsupported("hg-engine sets these counts in its own source");
            if (RegionalDexCount.WhyNot() is string dexWhy)
                return NotHere(RomLocator.WorksOnAll(RomLocator.Thing.RegionalDexCompletion, RomLocator.Thing.RegionalDexRatingLast), dexWhy);
            List<ushort> order = RegionalDexCount.SavedOrder();
            if (order == null) return Unsupported("The regional order couldn't be read");
            if (RegionalDexCount.Problem(order) != null) return Unsupported($"More than {RegionalDexCount.Max} species");
            if (!RegionalDexCount.Matches(order)) return PatchState.Available;
            // An untouched game already matches its own order; that is not this patch and earns no credit.
            return RegionalDexCount.Current() == RegionalDexCount.Retail ? Unsupported("Already matches") : PatchState.Applied;
        }

        private static PatchState EvolutionSlotsProbe()
        {
            if (RomInfo.IsHGE) return Unsupported("hg-engine sets its evolution slots in its own source");
            if (EvolutionSlots.WhyNot() is string evoWhy)
                return NotHere(RomLocator.WorksOnAll(RomLocator.Thing.EvolutionSlotBuffer, RomLocator.Thing.EvolutionSlotLoopLevel,
                    RomLocator.Thing.EvolutionSlotLoopTrade, RomLocator.Thing.EvolutionSlotLoopItem), evoWhy);
            return EvolutionSlots.Expanded ? PatchState.Applied : PatchState.Available;
        }

        /// <summary>Raises the evolution slots to the most the game's code can hold and pads every evolution file to match.</summary>
        public static bool ApplyEvolutionSlots()
        {
            if (EvolutionSlots.WhyNot() is string why) { ShowError(why, "Patch not applied"); return false; }
            if (AlreadyApplied(EvolutionSlots.Expanded)) return false;
            string backupDir = Path.Combine(RomInfo.DspreDir, "backups", "evolutions-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            if (!ConfirmYesNo($"Every Pokémon gets room for {EvolutionSlots.Max} evolutions instead of {EvolutionSlots.Current()}, edited in the " +
                "Pokémon editor's Evolutions tab." +
                BackupNote("the ARM9") + " The evolution files are copied to " + backupDir + "." +
                "\n\nApply this patch?" + CreditNote("evolutionSlots"), "Confirm to proceed"))
                return false;
            BackUp(RomInfo.Arm9Path);
            try { EvolutionSlots.Expand(backupDir); }
            catch (Exception e) when (e is IOException || e is InvalidOperationException || e is UnauthorizedAccessException)
            {
                ShowError("The evolution slots weren't changed:\n" + e.Message, "Patch not applied");
                return false;
            }
            ShowInfo($"Every Pokémon now has room for {EvolutionSlots.Max} evolutions in the Evolutions editor.", "Patch applied");
            return true;
        }

        /// <summary>Sets the game's regional Pokédex counts to the saved regional order's size.</summary>
        public static bool ApplyRegionalDexCount()
        {
            if (RegionalDexCount.WhyNot() is string why) { ShowError(why, "Patch not applied"); return false; }
            List<ushort> order = RegionalDexCount.SavedOrder();
            if (order == null) { ShowError("The regional order couldn't be read.", "Patch not applied"); return false; }
            if (RegionalDexCount.Problem(order) is string problem) { ShowError(problem, "Patch not applied"); return false; }
            if (AlreadyApplied(RegionalDexCount.Matches(order))) return false;
            RegionalDexCount.Counts now = RegionalDexCount.Current(), next = RegionalDexCount.For(order);
            if (!ConfirmYesNo($"The regional Pokédex holds {order.Count} species. The game will count it complete at {next.Completion} " +
                $"(now {now.Completion}) and the professor's rating will follow." + BackupNote("the ARM9") + "\n\nApply this patch?" + CreditNote("regionalDexCount"), "Confirm to proceed"))
                return false;
            BackUp(RomInfo.Arm9Path);
            RegionalDexCount.Apply(order);
            ShowInfo("The game's regional Pokédex size now matches the regional order.", "Patch applied");
            return true;
        }

        private static PatchState MoveListProbe(MoveCategoryTable.Kind kind)
        {
            if (RomInfo.IsHGE) return Unsupported(HgEngine.HgEngineSyntheticOverlay.ToolboxReason);
            if (MoveCategoryTable.WhyNot(kind) is string listWhy) return NotHere(RomLocator.MoveListDumps(MoveCategoryTable.TableOf(kind)), listWhy);
            if (!Arm9Expanded()) return Unsupported("Requires ARM9 expansion");
            return MoveCategoryTable.Expanded(kind) ? PatchState.Applied : PatchState.Available;
        }

        /// <summary>Moves a punching or sound move list into the expanded ARM9 area, keeping its moves.</summary>
        public static bool ApplyMoveListExpansion(MoveCategoryTable.Kind kind)
        {
            string key = kind == MoveCategoryTable.Kind.Punching ? "punchingMovesExpanded" : "soundMovesExpanded";
            string name = MoveCategoryTable.NameOf(kind);
            if (MoveCategoryTable.WhyNot(kind) is string why) { ShowError(why, "Patch not applied"); return false; }
            if (AlreadyApplied(MoveCategoryTable.Expanded(kind))) return false;
            if (PlacementNote(new[] { MoveCategoryTable.ExpansionBlockLength }, out int[] offsets) is not string placement) return false;
            if (!ConfirmYesNo($"The {name} move list gets room for {MoveCategoryTable.ExpandedCapacity} moves, picked in the Move data editor. " +
                    "Its moves stay as they are." + placement + BackupNote("the battle overlay and the synthetic overlay") +
                    "\n\nApply this patch?" + CreditNote(key), "Confirm to proceed"))
                return false;
            try
            {
                MoveCategoryTable table = MoveCategoryTable.Load(kind);
                BackUp(table.CodePath, Filesystem.ExpArmPath);
                SyntheticOverlaySpace.PlaceAt(offsets, table.MoveToExpansion);
                ShowInfo($"The {name} move list now has room for {MoveCategoryTable.ExpandedCapacity} moves. Mark moves in the Move data editor.", "Patch applied");
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is InvalidOperationException || ex is UnauthorizedAccessException)
            {
                ShowError($"The {name} move list wasn't moved:\n" + ex.Message, "Patch not applied");
                return false;
            }
        }

        /// <summary>Keeps a .backup copy of each file a patch is about to change.</summary>
        private static void BackUp(params string[] paths)
        {
            foreach (string path in paths)
                if (File.Exists(path)) PlainFiles.Copy(path, path + BackupSuffix, overwrite: true);
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
                          : "Apply the ARM9 expansion in the ROM patch toolbox first.", "Patch not applied");
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
            catch (Exception ex) { AppLogger.WarnOnce("PatchToolboxLogic.Probe", ex); return false; }
        }

        // Language/version gate shared by BDHCam and the script-command patches (Plat/HGSS, EN or ES).
        private static bool ScrcmdLikeLangOk() =>
            (RomInfo.GameFamily == GameFamilies.HGSS || RomInfo.GameFamily == GameFamilies.Plat)
            && (RomInfo.GameLanguage == GameLanguages.English || RomInfo.GameLanguage == GameLanguages.Spanish);

        // Small helpers so GetPatchStatuses stays declarative and a single check throwing can't
        // take down the whole catalogue (a status probe should never be fatal).
        [ThreadStatic] private static string _reason_text;
        private static PatchState Unsupported(string reason) { _reason_text = reason; return PatchState.Unsupported; }

        // Names the dumps a patch works on, from the same tables its check reads.
        private static PatchState UnsupportedVersion(IEnumerable<(string id, int rev)> works) =>
            Unsupported("Unsupported version\nWorks on " + RomInfo.DescribeDumps(works));

        // A known version can still fail for another reason, such as a missing file.
        private static PatchState NotHere(IEnumerable<(string id, int rev)> works, string why)
        {
            List<(string id, int rev)> list = works.ToList();
            return list.Contains((RomInfo.RomID, RomInfo.RomRevision)) ? Unsupported(why) : UnsupportedVersion(list);
        }

        // Platinum, HeartGold and SoulSilver in English or Spanish, as ScrcmdLikeLangOk checks.
        private static IEnumerable<(string id, int rev)> ScrcmdLikeDumps() =>
            RomInfo.KnownDumps.Where(d => (d.id.StartsWith("CPU") || d.id.StartsWith("IP")) && (d.id[3] == 'E' || d.id[3] == 'S'));

        public const string AppliedByHgEngine = "Applied by hg-engine";

        // hg-engine raises the trainer-name limit and moves the eye-contact music table itself, so on its ROMs
        // these patches are its doing and earn no credit here.
        private static PatchState AppliedHere()
        {
            if (RomInfo.IsHGE) _reason_text = AppliedByHgEngine;
            return PatchState.Applied;
        }

        private static PatchInfo Status(string key, string desc, Func<PatchState> probe, string actionLabel = null)
        {
            PatchInfo info = new PatchInfo { Key = key, Title = PatchTitles[key], Description = desc, ActionLabel = actionLabel,
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
                AppLogger.WarnOnce("PatchToolboxLogic.Status", ex);
                info.State = PatchState.Unsupported;
                info.Reason = "Unavailable (" + ex.GetType().Name + ")";
            }
            return info;
        }

        /// <summary>Applies the patch identified by <paramref name="key"/>. Returns whether it was applied.</summary>
        public static bool ApplyByKey(string key)
        {
            string hgEngineRefusal = HgEngine.HgEngineSyntheticOverlay.WhyNot();
            if (hgEngineRefusal != null && key is "arm9" or "bdhcam" or "buildingRotation" or "scrcmdRepoint"
                or "trainerClassTablesExpanded" or "trainerEncounterBgmRepointed" or "punchingMovesExpanded" or "soundMovesExpanded"
                or "pickupSlots" or "classIntros" or "hpBarText" or "vsClassTableExpanded" or "trainerShiny" or "trainerClassMetadata"
                or "martsExpanded" or "bpShopExpanded" or "swarmTableExpanded" or "typeChartExpanded" or "vsIntroTimings" or "partyPanelSize"
                or "summaryFonts" or "partyFonts" or "hpBarFonts" or "extraFonts" or "bannerMotion")
            {
                ShowError(hgEngineRefusal, "Not available on hg-engine");
                return false;
            }

            using IDisposable by = ProjectWrites.By("Patch toolbox: " + (PatchTitles.TryGetValue(key, out string title) ? title : key));
            switch (key)
            {
                case "sentenceCase": return ApplySentenceCasePatch();
                case "itemSentenceCase": return ApplyItemSentenceCasePatch();
                case "itemStandardize": return ApplyItemStandardizePatch();
                case "arm9": return ApplyARM9ExpansionPatch();
                case "bdhcam": return ApplyBDHCamPatch();   // caller re-queries statuses afterwards
                case "buildingRotation": return ApplyBuildingRotationPatch();
                case "trainerShiny": return ApplyTrainerShinyPatch();
                case "trainerCustomPokeballs": return ApplyTrainerCustomPokeballsPatch();
                case "trainerClassMetadata": return ApplyTrainerClassMetadataPatch();
                case "dynamicHeaders": return ApplyDynamicHeadersPatch();
                case "matrix": return ApplyMatrixExpansionPatch();
                case "scrcmdRepoint": return ApplyScrcmdRepointPatch();
                case "disableTextures": return ApplyDisableDynamicTexturesPatch();
                case "trainerNames": return ApplyExpandTrainerNamesPatch();
                case "sameHeldItemOdds": return ApplySameHeldItemOddsPatch();
                case "partyPanelSize": return ApplyPartyPanelSizePatch();
                case "bannerPosition": return ApplyBannerPositionPatch();
                case "bannerMotion": return ApplyBannerMotionPatch();
                case "trainerClassTablesExpanded": return ApplyMoveTrainerClassTables();
                case "trainerEncounterBgmRepointed": return ApplyMoveEncounterMusicTable();
                case "pickupSlots": return ApplyPickupSlotsPatch();
                case "classIntros": return ApplyClassIntrosPatch();
                case "hpBarText": return ApplyHpBarTextPatch();
                case "summaryFonts": return ApplySummaryFontsPatch();
                case "partyFonts": return ApplyPartyFontsPatch();
                case "hpBarFonts": return ApplyHpBarFontsPatch();
                case "extraFonts": return ApplyExtraFontsPatch();
                case "vsClassTableExpanded": return ApplyVsClassTableExpansion();
                case "punchingMovesExpanded": return ApplyMoveListExpansion(MoveCategoryTable.Kind.Punching);
                case "soundMovesExpanded": return ApplyMoveListExpansion(MoveCategoryTable.Kind.Sound);
                case "regionalDexCount": return ApplyRegionalDexCount();
                case "evolutionSlots": return ApplyEvolutionSlots();
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

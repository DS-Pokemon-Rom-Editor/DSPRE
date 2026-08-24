using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE
{
    // Repoints sTrainerClassGender/sTrainerClassPrizeMul/sTrainerEncounterBGMs into the synthetic
    // overlay to add a new trainer class, per a community write-up. Platinum/English only, since
    // that's the only version with confirmed offsets for the gender/prize-mul tables.
    public static class TrainerClassTableExpansion
    {
        public static bool IsSupportedForCurrentRom =>
            RomInfo.gameFamily == GameFamilies.Plat && RomInfo.gameLanguage == GameLanguages.English;

        public static bool IsPrizeMulSupportedForCurrentRom =>
            RomInfo.gameLanguage == GameLanguages.English &&
            (RomInfo.gameFamily == GameFamilies.Plat || RomInfo.gameFamily == GameFamilies.DP || RomInfo.gameFamily == GameFamilies.HGSS);

        public static bool IsGenderTableRepointed { get; private set; }
        public static bool IsPrizeMulTableRepointed { get; private set; }

        // sTrainerEncounterBGMs' offsets are tracked for every language/family already
        // (RomInfo.SetEncounterMusicTableOffsetToRAMAddress), so this can run without IsSupportedForCurrentRom.
        public static bool DetectMusicTableRepointed()
        {
            try
            {
                RomInfo.SetEncounterMusicTableOffsetToRAMAddress();
                uint ptr = BitConverter.ToUInt32(ARM9.ReadBytes(RomInfo.encounterMusicTableOffsetToRAMAddress, 4), 0);
                return ptr >= RomInfo.synthOverlayLoadAddress;
            }
            catch
            {
                return false;
            }
        }

        public static void Detect()
        {
            IsGenderTableRepointed = false;
            IsPrizeMulTableRepointed = false;
            if (!IsSupportedForCurrentRom) return;

            try
            {
                uint ptr = BitConverter.ToUInt32(DSUtils.ReadFromFile(RomInfo.arm9Path, RomInfo.trainerClassGenderTablePointerOffset, 4), 0);
                IsGenderTableRepointed = ptr >= RomInfo.synthOverlayLoadAddress;
            }
            catch { }

            try
            {
                string ov16Path = OverlayUtils.GetPath(RomInfo.trainerClassPrizeMulOverlayNumber);
                uint ptr = BitConverter.ToUInt32(DSUtils.ReadFromFile(ov16Path, RomInfo.trainerClassPrizeMulTablePointerOffset, 4), 0);
                IsPrizeMulTableRepointed = ptr >= RomInfo.synthOverlayLoadAddress;
            }
            catch { }
        }

        // Neither table stores its own length in the ROM, so once repointed, the current length
        // is derived from the trainer-class name archive's entry count (kept in lockstep by AddTrainerClass).
        private static bool TryResolveByteTable(string pointerFilePath, uint pointerFileOffset,
            string vanillaFilePath, uint vanillaFileOffset, int vanillaCount,
            out byte[] bytes, out string error)
        {
            bytes = null; error = null;
            try
            {
                uint ptr = BitConverter.ToUInt32(DSUtils.ReadFromFile(pointerFilePath, pointerFileOffset, 4), 0);
                bool repointed = ptr >= RomInfo.synthOverlayLoadAddress;

                if (repointed)
                {
                    int classCount = new TextArchive(RomInfo.trainerClassMessageNumber).messages.Count;
                    long start = ptr - RomInfo.synthOverlayLoadAddress;
                    bytes = DSUtils.ReadFromFile(Filesystem.expArmPath, start, classCount);
                }
                else
                {
                    bytes = DSUtils.ReadFromFile(vanillaFilePath, vanillaFileOffset, vanillaCount);
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryReadGender(int classId, out byte gender, out string error)
        {
            gender = 0;
            error = null;
            if (!IsSupportedForCurrentRom) { error = "Only implemented for Platinum (English)."; return false; }
            if (!TryResolveByteTable(RomInfo.arm9Path, RomInfo.trainerClassGenderTablePointerOffset, RomInfo.arm9Path, RomInfo.trainerClassGenderTableVanillaOffset, RomInfo.trainerClassGenderTableVanillaCount, out byte[] table, out error))
                return false;
            if (classId < 0 || classId >= table.Length) { error = "Class index out of range."; return false; }
            gender = table[classId];
            return true;
        }

        public static bool TryWriteGender(int classId, byte gender, out string error)
        {
            error = null;
            if (!IsSupportedForCurrentRom) { error = "Only implemented for Platinum (English)."; return false; }
            if (!TryResolveByteTable(RomInfo.arm9Path, RomInfo.trainerClassGenderTablePointerOffset, RomInfo.arm9Path, RomInfo.trainerClassGenderTableVanillaOffset, RomInfo.trainerClassGenderTableVanillaCount, out byte[] table, out error))
                return false;
            if (classId < 0 || classId >= table.Length) { error = "Class index out of range."; return false; }

            uint ptr = BitConverter.ToUInt32(DSUtils.ReadFromFile(RomInfo.arm9Path, RomInfo.trainerClassGenderTablePointerOffset, 4), 0);
            if (ptr < RomInfo.synthOverlayLoadAddress)
            {
                error = "The gender table hasn't been expanded yet. Add a trainer class first (or repoint it by hand).";
                return false;
            }

            long start = ptr - RomInfo.synthOverlayLoadAddress;
            DSUtils.WriteToFile(Filesystem.expArmPath, new[] { gender }, (uint)(start + classId));
            return true;
        }

        public static bool TryReadPrizeMul(int classId, out byte multiplier, out string error)
        {
            multiplier = 0;
            error = null;
            if (!IsPrizeMulSupportedForCurrentRom) { error = "Prize multiplier isn't known for this game/language."; return false; }

            EnsureOverlayDecompressed(RomInfo.trainerClassPrizeMulOverlayNumber);
            string ovPath = OverlayUtils.GetPath(RomInfo.trainerClassPrizeMulOverlayNumber);

            if (RomInfo.trainerClassPrizeMulTableIsPaired)
                return TryReadPairedPrizeMul(ovPath, classId, out multiplier, out error);

            byte[] table;
            if (RomInfo.trainerClassPrizeMulTablePointerOffset == 0)
            {
                // No known repoint pointer for this family: the vanilla table is always the real one.
                try { table = DSUtils.ReadFromFile(ovPath, RomInfo.trainerClassPrizeMulTableVanillaOffset, RomInfo.trainerClassPrizeMulTableVanillaCount); }
                catch (Exception ex) { error = ex.Message; return false; }
            }
            else if (!TryResolveByteTable(ovPath, RomInfo.trainerClassPrizeMulTablePointerOffset, ovPath, RomInfo.trainerClassPrizeMulTableVanillaOffset, RomInfo.trainerClassPrizeMulTableVanillaCount, out table, out error))
            {
                return false;
            }

            if (classId < 0 || classId >= table.Length) { error = "Class index out of range."; return false; }
            multiplier = table[classId];
            return true;
        }

        public static bool TryWritePrizeMul(int classId, byte multiplier, out string error)
        {
            error = null;
            if (!IsPrizeMulSupportedForCurrentRom) { error = "Prize multiplier isn't known for this game/language."; return false; }

            EnsureOverlayDecompressed(RomInfo.trainerClassPrizeMulOverlayNumber);
            string ovPath = OverlayUtils.GetPath(RomInfo.trainerClassPrizeMulOverlayNumber);

            if (RomInfo.trainerClassPrizeMulTableIsPaired)
                return TryWritePairedPrizeMul(ovPath, classId, multiplier, out error);

            if (!TryResolveByteTable(ovPath, RomInfo.trainerClassPrizeMulTablePointerOffset, ovPath, RomInfo.trainerClassPrizeMulTableVanillaOffset, RomInfo.trainerClassPrizeMulTableVanillaCount, out byte[] table, out error))
                return false;
            if (classId < 0 || classId >= table.Length) { error = "Class index out of range."; return false; }

            bool repointed = false;
            if (RomInfo.trainerClassPrizeMulTablePointerOffset != 0)
            {
                uint ptr = BitConverter.ToUInt32(DSUtils.ReadFromFile(ovPath, RomInfo.trainerClassPrizeMulTablePointerOffset, 4), 0);
                repointed = ptr >= RomInfo.synthOverlayLoadAddress;
                if (repointed)
                {
                    long start = ptr - RomInfo.synthOverlayLoadAddress;
                    DSUtils.WriteToFile(Filesystem.expArmPath, new[] { multiplier }, (uint)(start + classId));
                    return true;
                }
            }

            // Not repointed (or this family has no known repoint pointer): the vanilla table is the real one.
            DSUtils.WriteToFile(ovPath, new[] { multiplier }, (uint)(RomInfo.trainerClassPrizeMulTableVanillaOffset + classId));
            return true;
        }

        // HGSS stores {u16 classId, u16 multiplier} pairs rather than a plain array indexed by class,
        // so entries are matched by their classId field instead of by position.
        private static bool TryReadPairedPrizeMul(string ovPath, int classId, out byte multiplier, out string error)
        {
            multiplier = 0;
            error = null;
            try
            {
                byte[] data = DSUtils.ReadFromFile(ovPath, RomInfo.trainerClassPrizeMulTableVanillaOffset, RomInfo.trainerClassPrizeMulTableVanillaCount * 4);
                for (int i = 0; i < RomInfo.trainerClassPrizeMulTableVanillaCount; i++)
                {
                    if (BitConverter.ToUInt16(data, i * 4) == classId)
                    {
                        multiplier = (byte)BitConverter.ToUInt16(data, i * 4 + 2);
                        return true;
                    }
                }
                error = "No prize-multiplier entry found for this class.";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryWritePairedPrizeMul(string ovPath, int classId, byte multiplier, out string error)
        {
            error = null;
            try
            {
                byte[] data = DSUtils.ReadFromFile(ovPath, RomInfo.trainerClassPrizeMulTableVanillaOffset, RomInfo.trainerClassPrizeMulTableVanillaCount * 4);
                for (int i = 0; i < RomInfo.trainerClassPrizeMulTableVanillaCount; i++)
                {
                    if (BitConverter.ToUInt16(data, i * 4) != classId) continue;

                    uint offset = (uint)(RomInfo.trainerClassPrizeMulTableVanillaOffset + i * 4 + 2);
                    DSUtils.WriteToFile(ovPath, BitConverter.GetBytes((ushort)multiplier), offset);
                    return true;
                }
                error = "No prize-multiplier entry found for this class.";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool AddTrainerClass(string name, string description, byte gender, byte prizeMultiplier,
            bool addEncounterMusic, ushort musicMain, ushort musicNight, out string error)
        {
            error = null;
            if (!IsSupportedForCurrentRom)
            {
                error = "Adding trainer classes is only supported for Platinum (English) right now.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(name)) { error = "Enter a class name."; return false; }

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.synthOverlay, DirNames.textArchives });
            EnsureOverlayDecompressed(RomInfo.trainerClassPrizeMulOverlayNumber);

            // Validate every table resolves before writing anything (all-or-nothing).
            if (!TryResolveByteTable(RomInfo.arm9Path, RomInfo.trainerClassGenderTablePointerOffset, RomInfo.arm9Path, RomInfo.trainerClassGenderTableVanillaOffset, RomInfo.trainerClassGenderTableVanillaCount, out byte[] genderTable, out error))
                return false;
            string ov16Path = OverlayUtils.GetPath(RomInfo.trainerClassPrizeMulOverlayNumber);
            if (!TryResolveByteTable(ov16Path, RomInfo.trainerClassPrizeMulTablePointerOffset, ov16Path, RomInfo.trainerClassPrizeMulTableVanillaOffset, RomInfo.trainerClassPrizeMulTableVanillaCount, out byte[] prizeMulTable, out error))
                return false;

            try
            {
                int newClassId = genderTable.Length;

                byte[] newGenderTable = genderTable.Concat(new[] { gender }).ToArray();
                if (RepointByteArrayTable(RomInfo.arm9Path, RomInfo.trainerClassGenderTablePointerOffset, newGenderTable, out error) < 0) return false;

                byte[] newPrizeMulTable = prizeMulTable.Concat(new[] { prizeMultiplier }).ToArray();
                if (RepointByteArrayTable(ov16Path, RomInfo.trainerClassPrizeMulTablePointerOffset, newPrizeMulTable, out error) < 0) return false;

                if (addEncounterMusic && !AddEncounterMusicEntry((byte)newClassId, musicMain, musicNight, out error))
                    return false;

                var nameArchive = new TextArchive(RomInfo.trainerClassMessageNumber);
                nameArchive.messages.Add(name);
                nameArchive.SaveToExpandedDir(RomInfo.trainerClassMessageNumber, showSuccessMessage: false);

                var descArchive = new TextArchive(RomInfo.trainerClassDescriptionMessageNumber);
                descArchive.messages.Add(description ?? "");
                descArchive.SaveToExpandedDir(RomInfo.trainerClassDescriptionMessageNumber, showSuccessMessage: false);

                Detect();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // Used both by AddTrainerClass for a brand-new class and by an "enable eye-contact music"
        // UI action on an existing class that doesn't have an entry yet.
        public static bool AddEncounterMusicEntry(byte classId, ushort musicMain, ushort musicNight, out string error)
        {
            error = null;
            try
            {
                RomInfo.SetEncounterMusicTableOffsetToRAMAddress();
                uint tableSizeOffset = 10;
                if (RomInfo.gameFamily == GameFamilies.HGSS) tableSizeOffset += 2;
                uint lengthFieldOffset = RomInfo.encounterMusicTableOffsetToRAMAddress - tableSizeOffset;

                uint ptr = BitConverter.ToUInt32(ARM9.ReadBytes(RomInfo.encounterMusicTableOffsetToRAMAddress, 4), 0);
                bool repointed = ptr >= RomInfo.synthOverlayLoadAddress;
                string dataPath = repointed ? Filesystem.expArmPath : RomInfo.arm9Path;
                long dataStart = ptr - (repointed ? RomInfo.synthOverlayLoadAddress : ARM9.address);

                byte entryCount = ARM9.ReadByte(lengthFieldOffset);
                int entrySize = RomInfo.gameFamily == GameFamilies.HGSS ? 6 : 4;
                byte[] existing = DSUtils.ReadFromFile(dataPath, dataStart, entryCount * entrySize);

                for (int i = 0; i < entryCount; i++)
                {
                    if (BitConverter.ToUInt16(existing, i * entrySize) == classId)
                    {
                        error = "This class already has an eye-contact music entry.";
                        return false;
                    }
                }

                byte[] newEntry = new byte[entrySize];
                BitConverter.GetBytes((ushort)classId).CopyTo(newEntry, 0);
                BitConverter.GetBytes(musicMain).CopyTo(newEntry, 2);
                if (RomInfo.gameFamily == GameFamilies.HGSS)
                    BitConverter.GetBytes(musicNight).CopyTo(newEntry, 4);

                byte[] combined = existing.Concat(newEntry).ToArray();
                long newStart = RepointByteArrayTable(RomInfo.arm9Path, RomInfo.encounterMusicTableOffsetToRAMAddress, combined, out error);
                if (newStart < 0) return false;

                // Referenced by two pointers: base, and base+2 (reads the first entry's seqId half directly).
                uint newBase = RomInfo.synthOverlayLoadAddress + (uint)newStart;
                DSUtils.WriteToFile(RomInfo.arm9Path, BitConverter.GetBytes(newBase + 2), RomInfo.encounterMusicTableOffsetToRAMAddress + 4);

                DSUtils.WriteToFile(RomInfo.arm9Path, new[] { (byte)(entryCount + 1) }, lengthFieldOffset);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static void EnsureOverlayDecompressed(int overlayNumber)
        {
            if (OverlayUtils.OverlayTable.IsDefaultCompressed(overlayNumber) && OverlayUtils.IsCompressed(overlayNumber))
            {
                OverlayUtils.Decompress(overlayNumber);
            }
        }

        // Writes a full replacement copy of a table into free space in the synthetic overlay and
        // repoints the pointer at it. Returns the file offset written to, or -1 + error on failure.
        private static long RepointByteArrayTable(string pointerFilePath, uint pointerFileOffset, byte[] newFullTableBytes, out string error)
        {
            error = null;
            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.synthOverlay });
                string expPath = Filesystem.expArmPath;
                byte[] expData = File.ReadAllBytes(expPath);
                long freeOffset = FindFreeRegion(expData, newFullTableBytes.Length, 4);
                if (freeOffset < 0)
                {
                    error = "No free space found in the synthetic overlay for this table.";
                    return -1;
                }

                DSUtils.WriteToFile(expPath, newFullTableBytes, (uint)freeOffset);
                uint newRamAddress = RomInfo.synthOverlayLoadAddress + (uint)freeOffset;
                DSUtils.WriteToFile(pointerFilePath, BitConverter.GetBytes(newRamAddress), pointerFileOffset);
                return freeOffset;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return -1;
            }
        }

        // Scans for a run of all-zero bytes, skipping OverworldSpriteTableExpansion's reserved
        // headroom (that range reads as zero long before it's actually claimed).
        private static long FindFreeRegion(byte[] data, int length, int alignment)
        {
            OverworldSpriteTableExpansion.Detect();
            var owReserved = OverworldSpriteTableExpansion.GetReservedByteRange();

            for (long offset = 0; offset + length <= data.Length; offset += alignment)
            {
                if (owReserved.HasValue && offset + length > owReserved.Value.Start && offset < owReserved.Value.End)
                {
                    offset = owReserved.Value.End - alignment;
                    continue;
                }

                bool allZero = true;
                for (int i = 0; i < length; i++)
                {
                    if (data[offset + i] != 0) { allZero = false; break; }
                }
                if (allZero) return offset;
            }
            return -1;
        }
    }
}

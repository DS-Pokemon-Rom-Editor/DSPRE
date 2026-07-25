using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE
{
    /// <summary>
    /// Lets DSPRE perform the "add a new trainer class" repoint documented in a community write-up
    /// (repoint+extend sTrainerClassGender, sTrainerClassPrizeMul, sTrainerEncounterBGMs into the
    /// synthetic overlay, then append name/description text-archive entries) instead of requiring
    /// manual hex editing. Platinum-English only: sTrainerClassPrizeMul and the gender-table pointer
    /// slot only have confirmed offsets for that version. Every other language/family is refused
    /// outright rather than guessed at, since neither array has any bounds checking in the game.
    ///
    /// Every write goes through <see cref="RepointByteArrayTable"/>: a fresh, full copy of the table
    /// (existing bytes + the new entry) is written into free space in the synthetic overlay and the
    /// relevant pointer(s) updated. No attempt is made to reserve/reuse headroom across multiple
    /// additions (unlike <c>OverworldSpriteTableExpansion</c>'s 256-slot scheme). Trainer classes
    /// are expected to be added far less often, so simple-and-correct wins over clever-and-fast.
    /// </summary>
    public static class TrainerClassTableExpansion
    {
        public static bool IsSupportedForCurrentRom =>
            RomInfo.gameFamily == GameFamilies.Plat && RomInfo.gameLanguage == GameLanguages.English;

        // ── sTrainerClassGender (byte[N], 0=Male 1=Female), lives in arm9 ────────────────────────
        private const uint GenderTablePointerOffset = 0x793B4;
        private const uint VanillaGenderTableFileOffset = 0xF0714;
        private const int VanillaGenderTableCount = 0x69;

        // ── sTrainerClassPrizeMul (byte[N]), lives in overlay 16 ─────────────────────────────────
        private const int PrizeMulOverlayNumber = 16;
        private const uint PrizeMulTablePointerOverlayOffset = 0x816C;
        private const uint VanillaPrizeMulTableOverlayOffset = 0x359E0;
        private const int VanillaPrizeMulTableCount = 0x69;

        // sTrainerEncounterBGMs' offsets are already tracked (all languages) by
        // RomInfo.SetEncounterMusicTableOffsetToRAMAddress()/encounterMusicTableOffsetToRAMAddress,
        // and its repoint-aware read/write already exists; only "append a new entry" is new here.

        public static bool IsGenderTableRepointed { get; private set; }
        public static bool IsPrizeMulTableRepointed { get; private set; }

        /// <summary>Unlike the gender/prize-mul tables, sTrainerEncounterBGMs' offsets are already
        /// tracked for every language/family DSPRE supports (RomInfo.SetEncounterMusicTableOffsetToRAMAddress),
        /// so its repoint status can be checked regardless of IsSupportedForCurrentRom. This is used
        /// by the Patch Toolbox status row so it doesn't require the Trainer Editor to have been
        /// opened first (which is what normally triggers the check as a side effect of loading).</summary>
        public static bool DetectMusicTableRepointed()
        {
            try
            {
                RomInfo.SetEncounterMusicTableOffsetToRAMAddress();
                uint ptr = BitConverter.ToUInt32(ARM9.ReadBytes(RomInfo.encounterMusicTableOffsetToRAMAddress, 4), 0);
                bool repointed = ptr >= RomInfo.synthOverlayLoadAddress;
                RomPatchState.flag_TrainerEncounterBGMTableRepointed = repointed;
                return repointed;
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
                uint ptr = BitConverter.ToUInt32(DSUtils.ReadFromFile(RomInfo.arm9Path, GenderTablePointerOffset, 4), 0);
                IsGenderTableRepointed = ptr >= RomInfo.synthOverlayLoadAddress;
            }
            catch { /* leave false */ }

            try
            {
                string ov16Path = OverlayUtils.GetPath(PrizeMulOverlayNumber);
                uint ptr = BitConverter.ToUInt32(DSUtils.ReadFromFile(ov16Path, PrizeMulTablePointerOverlayOffset, 4), 0);
                IsPrizeMulTableRepointed = ptr >= RomInfo.synthOverlayLoadAddress;
            }
            catch { /* leave false */ }
        }

        // ── Generic byte-array table resolve/read ────────────────────────────────────────────────
        // Once repointed, the table's real length is derived from the trainer-class NAME text
        // archive's current entry count (kept in lockstep by AddTrainerClass, which is the only
        // thing that ever grows any of these tables) rather than a separate length field, since neither
        // the gender nor the prize-mul table has one in the ROM itself.
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
            if (!TryResolveByteTable(RomInfo.arm9Path, GenderTablePointerOffset, RomInfo.arm9Path, VanillaGenderTableFileOffset, VanillaGenderTableCount, out byte[] table, out error))
                return false;
            if (classId < 0 || classId >= table.Length) { error = "Class index out of range."; return false; }
            gender = table[classId];
            return true;
        }

        public static bool TryWriteGender(int classId, byte gender, out string error)
        {
            error = null;
            if (!IsSupportedForCurrentRom) { error = "Only implemented for Platinum (English)."; return false; }
            if (!TryResolveByteTable(RomInfo.arm9Path, GenderTablePointerOffset, RomInfo.arm9Path, VanillaGenderTableFileOffset, VanillaGenderTableCount, out byte[] table, out error))
                return false;
            if (classId < 0 || classId >= table.Length) { error = "Class index out of range."; return false; }

            uint ptr = BitConverter.ToUInt32(DSUtils.ReadFromFile(RomInfo.arm9Path, GenderTablePointerOffset, 4), 0);
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
            if (!IsSupportedForCurrentRom) { error = "Only implemented for Platinum (English)."; return false; }
            string ov16Path = OverlayUtils.GetPath(PrizeMulOverlayNumber);
            if (!TryResolveByteTable(ov16Path, PrizeMulTablePointerOverlayOffset, ov16Path, VanillaPrizeMulTableOverlayOffset, VanillaPrizeMulTableCount, out byte[] table, out error))
                return false;
            if (classId < 0 || classId >= table.Length) { error = "Class index out of range."; return false; }
            multiplier = table[classId];
            return true;
        }

        public static bool TryWritePrizeMul(int classId, byte multiplier, out string error)
        {
            error = null;
            if (!IsSupportedForCurrentRom) { error = "Only implemented for Platinum (English)."; return false; }
            string ov16Path = OverlayUtils.GetPath(PrizeMulOverlayNumber);
            if (!TryResolveByteTable(ov16Path, PrizeMulTablePointerOverlayOffset, ov16Path, VanillaPrizeMulTableOverlayOffset, VanillaPrizeMulTableCount, out byte[] table, out error))
                return false;
            if (classId < 0 || classId >= table.Length) { error = "Class index out of range."; return false; }

            uint ptr = BitConverter.ToUInt32(DSUtils.ReadFromFile(ov16Path, PrizeMulTablePointerOverlayOffset, 4), 0);
            if (ptr < RomInfo.synthOverlayLoadAddress)
            {
                error = "The prize-multiplier table hasn't been expanded yet. Add a trainer class first (or repoint it by hand).";
                return false;
            }

            long start = ptr - RomInfo.synthOverlayLoadAddress;
            DSUtils.WriteToFile(Filesystem.expArmPath, new[] { multiplier }, (uint)(start + classId));
            return true;
        }

        // ── Add a whole new trainer class ─────────────────────────────────────────────────────────
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
            EnsureOverlayDecompressed(PrizeMulOverlayNumber);

            // Validate every table resolves before writing anything (all-or-nothing).
            if (!TryResolveByteTable(RomInfo.arm9Path, GenderTablePointerOffset, RomInfo.arm9Path, VanillaGenderTableFileOffset, VanillaGenderTableCount, out byte[] genderTable, out error))
                return false;
            string ov16Path = OverlayUtils.GetPath(PrizeMulOverlayNumber);
            if (!TryResolveByteTable(ov16Path, PrizeMulTablePointerOverlayOffset, ov16Path, VanillaPrizeMulTableOverlayOffset, VanillaPrizeMulTableCount, out byte[] prizeMulTable, out error))
                return false;

            try
            {
                int newClassId = genderTable.Length; // 0-based: new entry lands right after the last one

                byte[] newGenderTable = genderTable.Concat(new[] { gender }).ToArray();
                if (RepointByteArrayTable(RomInfo.arm9Path, GenderTablePointerOffset, newGenderTable, out error) < 0) return false;

                byte[] newPrizeMulTable = prizeMulTable.Concat(new[] { prizeMultiplier }).ToArray();
                if (RepointByteArrayTable(ov16Path, PrizeMulTablePointerOverlayOffset, newPrizeMulTable, out error) < 0) return false;

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

        /// <summary>Appends a new eye-contact-music entry for a trainer class that doesn't already
        /// have one (both a brand-new class from <see cref="AddTrainerClass"/>, and an existing
        /// class the "Enable eye-contact music" UI action targets). Fails if the class already has
        /// an entry, use the normal Trainer Classes editing flow to change an existing one instead.</summary>
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

                // This table is referenced by *two* pointers: base, and base+2 (used to read the
                // first entry's seqId half directly). Both need to point at the new location.
                uint newBase = RomInfo.synthOverlayLoadAddress + (uint)newStart;
                DSUtils.WriteToFile(RomInfo.arm9Path, BitConverter.GetBytes(newBase + 2), RomInfo.encounterMusicTableOffsetToRAMAddress + 4);

                DSUtils.WriteToFile(RomInfo.arm9Path, new[] { (byte)(entryCount + 1) }, lengthFieldOffset);
                RomPatchState.flag_TrainerEncounterBGMTableRepointed = true;
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

        /// <summary>Writes a full replacement copy of a simple byte-array table into free space in
        /// the synthetic overlay and repoints the single pointer at pointerFilePath/pointerFileOffset
        /// at it. Returns the file offset the table was written at, or -1 + error on failure.</summary>
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

        /// <summary>Scans for a run of all-zero bytes to write a new table into. Zero bytes alone
        /// aren't proof a region is actually unclaimed: <see cref="OverworldSpriteTableExpansion"/>
        /// pre-reserves headroom for future custom overworld entries that reads as zero for a long
        /// time (until each slot is actually used), so its reserved range is explicitly excluded here
        /// even though a naive zero-scan would otherwise happily write straight into it and corrupt
        /// whichever patch claims that space second. Other synthetic-overlay patches (Building
        /// Rotation, the ScrCmd table repoint) don't have this problem: they write their real payload
        /// bytes immediately when applied, so a region only reads as zero while genuinely unclaimed.</summary>
        private static long FindFreeRegion(byte[] data, int length, int alignment)
        {
            // Detect() (not just IsApplied) so this is accurate even if nothing has touched
            // OverworldSpriteTableExpansion yet this session (e.g. Trainer Editor opened first).
            OverworldSpriteTableExpansion.Detect();
            var owReserved = OverworldSpriteTableExpansion.GetReservedByteRange();

            for (long offset = 0; offset + length <= data.Length; offset += alignment)
            {
                if (owReserved.HasValue && offset + length > owReserved.Value.Start && offset < owReserved.Value.End)
                {
                    // Skip straight past the reserved range instead of re-checking every aligned
                    // offset inside it one at a time.
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

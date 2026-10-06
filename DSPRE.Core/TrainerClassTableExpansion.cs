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
    /// synthetic overlay, then append name and name-with-article text-archive entries) instead of requiring
    /// manual hex editing. The new class also gets a front sprite, copied from a class the user picks.
    /// Platinum-English only: sTrainerClassPrizeMul and the gender-table pointer
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

        // Prize multiplier is also known for DP/HGSS English, but neither has a known repoint
        // pointer (pointerOffset 0 below means "always read/write the vanilla table directly").
        // HGSS stores u16 {classId, multiplier} pairs instead of a plain byte array, so vanillaCount
        // means "pairs" there, not bytes.
        public static bool IsPrizeMulSupportedForCurrentRom => TryGetPrizeMulTableInfo(out _, out _, out _, out _, out _);

        private static bool TryGetPrizeMulTableInfo(out int overlayNumber, out uint pointerOffset, out uint vanillaOffset, out int vanillaCount, out bool isPaired)
        {
            overlayNumber = 0; pointerOffset = 0; vanillaOffset = 0; vanillaCount = 0; isPaired = false;
            if (RomInfo.gameLanguage != GameLanguages.English) return false;

            switch (RomInfo.gameFamily)
            {
                case GameFamilies.Plat:
                    overlayNumber = PrizeMulOverlayNumber;
                    pointerOffset = PrizeMulTablePointerOverlayOffset;
                    vanillaOffset = VanillaPrizeMulTableOverlayOffset;
                    vanillaCount = VanillaPrizeMulTableCount;
                    return true;
                case GameFamilies.DP:
                    overlayNumber = 11;
                    vanillaOffset = 0x32960;
                    vanillaCount = 0x62;
                    return true;
                case GameFamilies.HGSS:
                    overlayNumber = 12;
                    vanillaOffset = 0x34C04;
                    vanillaCount = 129;
                    isPaired = true;
                    return true;
                default:
                    return false;
            }
        }

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
                // Not repointed: the retail table is the one the game reads, and classId is inside it.
                if (ptr != ARM9.address + VanillaGenderTableFileOffset)
                {
                    error = "The gender table was moved inside arm9, so it can't be written here.";
                    return false;
                }
                DSUtils.WriteToFile(RomInfo.arm9Path, new[] { gender }, (uint)(VanillaGenderTableFileOffset + classId));
                return true;
            }

            long start = ptr - RomInfo.synthOverlayLoadAddress;
            DSUtils.WriteToFile(Filesystem.expArmPath, new[] { gender }, (uint)(start + classId));
            return true;
        }

        public static bool TryReadPrizeMul(int classId, out byte multiplier, out string error)
        {
            multiplier = 0;
            error = null;
            if (!TryGetPrizeMulTableInfo(out int overlayNumber, out uint pointerOffset, out uint vanillaOffset, out int vanillaCount, out bool isPaired))
            {
                error = "Prize multiplier isn't known for this game/language.";
                return false;
            }

            EnsureOverlayDecompressed(overlayNumber);
            string ovPath = OverlayUtils.GetPath(overlayNumber);

            if (isPaired)
                return TryReadPairedPrizeMul(ovPath, vanillaOffset, vanillaCount, classId, out multiplier, out error);

            byte[] table;
            if (pointerOffset == 0)
            {
                // No known repoint pointer for this family: the vanilla table is always the real one.
                try { table = DSUtils.ReadFromFile(ovPath, vanillaOffset, vanillaCount); }
                catch (Exception ex) { error = ex.Message; return false; }
            }
            else if (!TryResolveByteTable(ovPath, pointerOffset, ovPath, vanillaOffset, vanillaCount, out table, out error))
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
            if (!TryGetPrizeMulTableInfo(out int overlayNumber, out uint pointerOffset, out uint vanillaOffset, out int vanillaCount, out bool isPaired))
            {
                error = "Prize multiplier isn't known for this game/language.";
                return false;
            }

            EnsureOverlayDecompressed(overlayNumber);
            string ovPath = OverlayUtils.GetPath(overlayNumber);

            if (isPaired)
                return TryWritePairedPrizeMul(ovPath, vanillaOffset, vanillaCount, classId, multiplier, out error);

            byte[] table;
            if (pointerOffset == 0)
            {
                // No known repoint pointer: resolving one would read the overlay's first word as a pointer.
                try { table = DSUtils.ReadFromFile(ovPath, vanillaOffset, vanillaCount); }
                catch (Exception ex) { error = ex.Message; return false; }
            }
            else if (!TryResolveByteTable(ovPath, pointerOffset, ovPath, vanillaOffset, vanillaCount, out table, out error))
            {
                return false;
            }
            if (classId < 0 || classId >= table.Length) { error = "Class index out of range."; return false; }

            bool repointed = false;
            if (pointerOffset != 0)
            {
                uint ptr = BitConverter.ToUInt32(DSUtils.ReadFromFile(ovPath, pointerOffset, 4), 0);
                repointed = ptr >= RomInfo.synthOverlayLoadAddress;
                if (repointed)
                {
                    long start = ptr - RomInfo.synthOverlayLoadAddress;
                    DSUtils.WriteToFile(Filesystem.expArmPath, new[] { multiplier }, (uint)(start + classId));
                    return true;
                }
            }

            // Not repointed (or this family has no known repoint pointer): the vanilla table is the real one.
            DSUtils.WriteToFile(ovPath, new[] { multiplier }, (uint)(vanillaOffset + classId));
            return true;
        }

        // HGSS stores {u16 classId, u16 multiplier} pairs rather than a plain array indexed by class,
        // so entries are matched by their classId field instead of by position.
        private static bool TryReadPairedPrizeMul(string ovPath, uint vanillaOffset, int pairCount, int classId, out byte multiplier, out string error)
        {
            multiplier = 0;
            error = null;
            try
            {
                byte[] data = DSUtils.ReadFromFile(ovPath, vanillaOffset, pairCount * 4);
                for (int i = 0; i < pairCount; i++)
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

        private static bool TryWritePairedPrizeMul(string ovPath, uint vanillaOffset, int pairCount, int classId, byte multiplier, out string error)
        {
            error = null;
            try
            {
                byte[] data = DSUtils.ReadFromFile(ovPath, vanillaOffset, pairCount * 4);
                for (int i = 0; i < pairCount; i++)
                {
                    if (BitConverter.ToUInt16(data, i * 4) != classId) continue;

                    uint offset = (uint)(vanillaOffset + i * 4 + 2);
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

        // ── Add a whole new trainer class ─────────────────────────────────────────────────────────
        /// <summary>Why a class with this name can't be added, or null. Checked when the user adds it, before Save.</summary>
        // Class adding is English only, so a blank entry gets the English article.
        private static string WithArticle(string name) =>
            ("AEIOU".IndexOf(char.ToUpperInvariant(name.TrimStart().FirstOrDefault())) >= 0 ? "an " : "a ") + name.Trim();

        public static string AddRefusal(string name)
        {
            if (!IsSupportedForCurrentRom) return "Adding trainer classes is only supported for Platinum (English) right now.";
            if (!SyntheticOverlaySpace.Available()) return NeedsExpansion;
            return string.IsNullOrWhiteSpace(name) ? "Enter a class name." : null;
        }

        // Without the expansion the synthetic overlay is never loaded, so a moved table would be read from empty memory.
        private const string NeedsExpansion = "This needs the ARM9 expansion from the ROM Patch Toolbox, which isn't applied to this ROM.";

        // The game reads class N's front sprite from trfgra members 5N to 5N+4: tiles, palette, cells, animation, scan.
        private const int SpriteFilesPerClass = 5;

        private static string SpriteFile(int classId, int part) => Filesystem.GetTrainerGraphicsPath(classId * SpriteFilesPerClass + part);

        /// <summary>Whether the trainer sprite archive already holds all five files for this class.</summary>
        public static bool HasSprite(int classId)
        {
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.trainerGraphics });
            return SpriteComplete(classId);
        }

        private static bool SpriteComplete(int classId) =>
            Enumerable.Range(0, SpriteFilesPerClass).All(part => File.Exists(SpriteFile(classId, part)));

        /// <param name="spriteFromClass">The class whose front sprite the new class starts with, or -1 to keep
        /// the sprite already in the new class's slot.</param>
        public static bool AddTrainerClass(string name, string nameWithArticle, byte gender, byte prizeMultiplier,
            bool addEncounterMusic, ushort musicMain, ushort musicNight, int spriteFromClass, out string error)
        {
            _writtenThisOperation = new List<(long, long)>();
            try { return AddTrainerClassTables(name, nameWithArticle, gender, prizeMultiplier, addEncounterMusic, musicMain, musicNight, spriteFromClass, out error); }
            finally { _writtenThisOperation = null; }
        }

        private static bool AddTrainerClassTables(string name, string nameWithArticle, byte gender, byte prizeMultiplier,
            bool addEncounterMusic, ushort musicMain, ushort musicNight, int spriteFromClass, out string error)
        {
            error = AddRefusal(name);
            if (error != null) return false;

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.synthOverlay, DirNames.textArchives, DirNames.trainerGraphics });
            EnsureOverlayDecompressed(PrizeMulOverlayNumber);

            // Validate every table resolves before writing anything (all-or-nothing).
            if (!TryResolveByteTable(RomInfo.arm9Path, GenderTablePointerOffset, RomInfo.arm9Path, VanillaGenderTableFileOffset, VanillaGenderTableCount, out byte[] genderTable, out error))
                return false;
            string ov16Path = OverlayUtils.GetPath(PrizeMulOverlayNumber);
            if (!TryResolveByteTable(ov16Path, PrizeMulTablePointerOverlayOffset, ov16Path, VanillaPrizeMulTableOverlayOffset, VanillaPrizeMulTableCount, out byte[] prizeMulTable, out error))
                return false;
            int newClassId = genderTable.Length;
            if (!SpriteComplete(spriteFromClass >= 0 ? spriteFromClass : newClassId))
            {
                error = spriteFromClass >= 0
                    ? $"Trainer class {spriteFromClass} has no complete sprite to copy."
                    : $"The trainer sprite archive has no sprite for class {newClassId}.";
                return false;
            }
            // Packing takes the folder's files in order, so a gap would shift every later sprite.
            for (int id = 0; id < newClassId; id++)
            {
                if (SpriteComplete(id)) continue;
                error = $"The trainer sprite archive is missing files for class {id}.";
                return false;
            }

            try
            {
                byte[] newGenderTable = genderTable.Concat(new[] { gender }).ToArray();
                if (RepointByteArrayTable(RomInfo.arm9Path, GenderTablePointerOffset, newGenderTable, out error) < 0) return false;

                byte[] newPrizeMulTable = prizeMulTable.Concat(new[] { prizeMultiplier }).ToArray();
                if (RepointByteArrayTable(ov16Path, PrizeMulTablePointerOverlayOffset, newPrizeMulTable, out error) < 0) return false;

                if (addEncounterMusic && !AddEncounterMusicEntry((byte)newClassId, musicMain, musicNight, out error))
                    return false;

                if (spriteFromClass >= 0)
                    for (int part = 0; part < SpriteFilesPerClass; part++)
                        File.Copy(SpriteFile(spriteFromClass, part), SpriteFile(newClassId, part), overwrite: true);

                TextArchive nameArchive = new TextArchive(RomInfo.trainerClassMessageNumber);
                nameArchive.messages.Add(name);
                nameArchive.SaveToExpandedDir(RomInfo.trainerClassMessageNumber, showSuccessMessage: false);

                TextArchive articleArchive = new TextArchive(RomInfo.trainerClassWithArticleMessageNumber);
                articleArchive.messages.Add(string.IsNullOrWhiteSpace(nameWithArticle) ? WithArticle(name) : nameWithArticle);
                articleArchive.SaveToExpandedDir(RomInfo.trainerClassWithArticleMessageNumber, showSuccessMessage: false);

                Detect();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>Moves the gender and prize money tables into the synthetic overlay unchanged, without adding a class.</summary>
        public static bool MoveClassTables(out string error)
        {
            _writtenThisOperation = new List<(long, long)>();
            try { return MoveClassTablesUnchanged(out error); }
            finally { _writtenThisOperation = null; }
        }

        private static bool MoveClassTablesUnchanged(out string error)
        {
            error = !IsSupportedForCurrentRom ? "Only Platinum (English) is supported."
                : !SyntheticOverlaySpace.Available() ? NeedsExpansion : null;
            if (error != null) return false;

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.synthOverlay, DirNames.textArchives });
            EnsureOverlayDecompressed(PrizeMulOverlayNumber);
            Detect();

            string ov16Path = OverlayUtils.GetPath(PrizeMulOverlayNumber);
            if (!TryResolveByteTable(RomInfo.arm9Path, GenderTablePointerOffset, RomInfo.arm9Path, VanillaGenderTableFileOffset, VanillaGenderTableCount, out byte[] genderTable, out error))
                return false;
            if (!TryResolveByteTable(ov16Path, PrizeMulTablePointerOverlayOffset, ov16Path, VanillaPrizeMulTableOverlayOffset, VanillaPrizeMulTableCount, out byte[] prizeMulTable, out error))
                return false;

            // A moved table's length is read from the class name count, so both must already match it.
            int classCount = new TextArchive(RomInfo.trainerClassMessageNumber).messages.Count;
            if (genderTable.Length != classCount || prizeMulTable.Length != classCount)
            {
                error = $"The ROM has {classCount} trainer class names but the tables have {genderTable.Length} and {prizeMulTable.Length} entries.";
                return false;
            }

            if (!IsGenderTableRepointed && RepointByteArrayTable(RomInfo.arm9Path, GenderTablePointerOffset, genderTable, out error) < 0) return false;
            if (!IsPrizeMulTableRepointed && RepointByteArrayTable(ov16Path, PrizeMulTablePointerOverlayOffset, prizeMulTable, out error) < 0) return false;
            Detect();
            return true;
        }

        /// <summary>Moves the eye-contact music table into the synthetic overlay unchanged.</summary>
        public static bool MoveEncounterMusicTable(out string error)
        {
            error = !IsSupportedForCurrentRom ? "Only Platinum (English) is supported."
                : !SyntheticOverlaySpace.Available() ? NeedsExpansion : null;
            if (error != null) return false;
            return DetectMusicTableRepointed() || RepointEncounterMusic(null, null, out error);
        }

        /// <summary>Appends a new eye-contact-music entry for a trainer class that doesn't already
        /// have one (both a brand-new class from <see cref="AddTrainerClass"/>, and an existing
        /// class the "Enable eye-contact music" UI action targets). Fails if the class already has
        /// an entry, use the normal Trainer Classes editing flow to change an existing one instead.</summary>
        public static bool AddEncounterMusicEntry(byte classId, ushort musicMain, ushort musicNight, out string error)
        {
            byte[] newEntry = new byte[RomInfo.gameFamily == GameFamilies.HGSS ? 6 : 4];
            BitConverter.GetBytes((ushort)classId).CopyTo(newEntry, 0);
            BitConverter.GetBytes(musicMain).CopyTo(newEntry, 2);
            if (RomInfo.gameFamily == GameFamilies.HGSS)
                BitConverter.GetBytes(musicNight).CopyTo(newEntry, 4);
            return RepointEncounterMusic(classId, newEntry, out error);
        }

        private static bool RepointEncounterMusic(byte? classId, byte[] newEntry, out string error)
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
                    if (classId.HasValue && BitConverter.ToUInt16(existing, i * entrySize) == classId)
                    {
                        error = "This class already has an eye-contact music entry.";
                        return false;
                    }
                }

                byte[] combined = newEntry == null ? existing : existing.Concat(newEntry).ToArray();
                long newStart = RepointByteArrayTable(RomInfo.arm9Path, RomInfo.encounterMusicTableOffsetToRAMAddress, combined, out error);
                if (newStart < 0) return false;

                // This table is referenced by *two* pointers: base, and base+2 (used to read the
                // first entry's seqId half directly). Both need to point at the new location.
                uint newBase = RomInfo.synthOverlayLoadAddress + (uint)newStart;
                DSUtils.WriteToFile(RomInfo.arm9Path, BitConverter.GetBytes(newBase + 2), RomInfo.encounterMusicTableOffsetToRAMAddress + 4);

                if (newEntry != null)
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
            if (!SyntheticOverlaySpace.Available())
            {
                error = NeedsExpansion;
                return -1;
            }
            try
            {
                string expPath = Filesystem.expArmPath;
                byte[] expData = File.ReadAllBytes(expPath);
                List<(long Start, long End)> reserved = DSPRE.ROMFiles.SyntheticOverlaySpace.Reserved(expData);
                if (_writtenThisOperation != null) reserved.AddRange(_writtenThisOperation);
                long freeOffset = DSPRE.ROMFiles.SyntheticOverlaySpace.FindFree(expData, newFullTableBytes.Length, 4, reserved);
                if (freeOffset < 0)
                {
                    error = "No free space found in the synthetic overlay for this table.";
                    return -1;
                }

                DSUtils.WriteToFile(expPath, newFullTableBytes, (uint)freeOffset);
                _writtenThisOperation?.Add((freeOffset, freeOffset + newFullTableBytes.Length));
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

        // Tables written earlier in this add or move: the class count that sizes them grows only at the end.
        private static List<(long Start, long End)> _writtenThisOperation;

        /// <summary>The synthetic overlay bytes the moved trainer class and eye-contact music tables use.
        /// Their entries can be zero, so a free-space scan would otherwise take them for empty space.</summary>
        public static List<(long Start, long End)> MovedTableRanges()
        {
            List<(long, long)> ranges = new List<(long, long)>();
            if (RomInfo.isHGE) return ranges;
            uint load = RomInfo.synthOverlayLoadAddress;
            if (IsSupportedForCurrentRom)
            {
                try
                {
                    int classCount = new TextArchive(RomInfo.trainerClassMessageNumber).messages.Count;
                    uint gender = BitConverter.ToUInt32(DSUtils.ReadFromFile(RomInfo.arm9Path, GenderTablePointerOffset, 4), 0);
                    if (gender >= load) ranges.Add((gender - load, gender - load + classCount));
                    uint prize = BitConverter.ToUInt32(DSUtils.ReadFromFile(OverlayUtils.GetPath(PrizeMulOverlayNumber), PrizeMulTablePointerOverlayOffset, 4), 0);
                    if (prize >= load) ranges.Add((prize - load, prize - load + classCount));
                }
                catch { /* an unreadable table reserves nothing */ }
            }
            try
            {
                RomInfo.SetEncounterMusicTableOffsetToRAMAddress();
                uint music = BitConverter.ToUInt32(ARM9.ReadBytes(RomInfo.encounterMusicTableOffsetToRAMAddress, 4), 0);
                if (music >= load)
                {
                    uint lengthField = RomInfo.encounterMusicTableOffsetToRAMAddress - (RomInfo.gameFamily == GameFamilies.HGSS ? 12u : 10u);
                    int entrySize = RomInfo.gameFamily == GameFamilies.HGSS ? 6 : 4;
                    ranges.Add((music - load, music - load + ARM9.ReadByte(lengthField) * entrySize));
                }
            }
            catch { /* an unreadable table reserves nothing */ }
            return ranges;
        }
    }
}

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
    /// Platinum only, wherever sTrainerClassPrizeMul and the gender-table pointer are measured. Every other
    /// family is refused outright rather than guessed at, since neither array has any bounds checking in the game.
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
            RomInfo.gameFamily == GameFamilies.Plat && RomInfo.trainerClassGenderPointerOffset != 0 && RomInfo.trainerPrizeTable?.Pointer > 0;

        // A pointer of 0 means "always read/write the vanilla table directly" (DP and HGSS).
        public static bool IsPrizeMulSupportedForCurrentRom => TryGetPrizeMulTableInfo(out _, out _, out _, out _, out _);

        private static bool TryGetPrizeMulTableInfo(out int overlayNumber, out uint pointerOffset, out uint vanillaOffset, out int vanillaCount, out bool isPaired)
        {
            RomInfo.TrainerPrizeTable t = RomInfo.trainerPrizeTable;
            overlayNumber = t?.Overlay ?? 0; pointerOffset = t?.Pointer ?? 0; vanillaOffset = t?.Table ?? 0;
            vanillaCount = t?.Count ?? 0; isPaired = t?.Paired ?? false;
            return t != null;
        }

        // sTrainerEncounterBGMs' offsets are already tracked (all languages) by
        // RomInfo.SetEncounterMusicTableOffsetToRAMAddress()/encounterMusicTableOffsetToRAMAddress,
        // and its repoint-aware read/write already exists; only "append a new entry" is new here.

        // The toolbox patches place each table in a marked block with room for every class a byte can name, so adding
        // a class or its music writes in place and never moves a table or rewrites a pointer.
        public const string GenderMarker = "TRCLSGENDER1", PrizeMarker = "TRCLSPRIZE01", MusicMarker = "TRCLSMUSIC01";
        public const int MaxClasses = 256;
        private static int MusicEntrySize => RomInfo.gameFamily == GameFamilies.HGSS ? 6 : 4;

        /// <summary>The data offset of a table in the synthetic overlay when its pointer leads into a marked block with
        /// room for <paramref name="capacity"/> bytes, else -1.</summary>
        private static long RoomyTable(uint pointer, string marker, int capacity)
        {
            if (pointer < RomInfo.synthOverlayLoadAddress || !File.Exists(Filesystem.expArmPath)) return -1;
            long at = pointer - RomInfo.synthOverlayLoadAddress;
            byte[] synth = File.ReadAllBytes(Filesystem.expArmPath);
            foreach ((long Start, long End) b in SyntheticOverlaySpace.Blocks(synth, marker))
                if (at == b.Start + SyntheticOverlaySpace.HeaderSize && b.End - at >= capacity) return at;
            return -1;
        }

        private static uint GenderPointer => BitConverter.ToUInt32(DSUtils.ReadFromFile(RomInfo.arm9Path, RomInfo.trainerClassGenderPointerOffset, 4), 0);
        private static uint PrizePointer => BitConverter.ToUInt32(DSUtils.ReadFromFile(OverlayUtils.GetPath(RomInfo.trainerPrizeTable.Overlay), RomInfo.trainerPrizeTable.Pointer, 4), 0);

        /// <summary>Whether the gender and prize tables have room for every class, as the toolbox patch leaves them.</summary>
        public static bool ClassTablesHaveRoom
        {
            get
            {
                try
                {
                    EnsureOverlayDecompressed(RomInfo.trainerPrizeTable.Overlay);
                    return RoomyTable(GenderPointer, GenderMarker, MaxClasses) >= 0 && RoomyTable(PrizePointer, PrizeMarker, MaxClasses) >= 0;
                }
                catch (Exception e) when (e is IOException || e is ArgumentException) { return false; }
            }
        }

        /// <summary>Whether the eye-contact music table has room for an entry per class, as the toolbox patch leaves it.</summary>
        public static bool MusicTableHasRoom
        {
            get
            {
                try
                {
                    RomInfo.SetEncounterMusicTableOffsetToRAMAddress();
                    uint ptr = BitConverter.ToUInt32(ARM9.ReadBytes(RomInfo.encounterMusicTableOffsetToRAMAddress, 4), 0);
                    return RoomyTable(ptr, MusicMarker, MaxClasses * MusicEntrySize) >= 0;
                }
                catch (Exception e) when (e is IOException || e is ArgumentException) { return false; }
            }
        }

        private static int RoomyBlockLength(int capacity) => (SyntheticOverlaySpace.HeaderSize + capacity + 3) & ~3;

        /// <summary>The sizes of the blocks the class tables patch places, in the order it places them.</summary>
        public static int[] ClassTableBlockLengths()
        {
            EnsureOverlayDecompressed(RomInfo.trainerPrizeTable.Overlay);
            List<int> lengths = new List<int>();
            if (RoomyTable(GenderPointer, GenderMarker, MaxClasses) < 0) lengths.Add(RoomyBlockLength(MaxClasses));
            if (RoomyTable(PrizePointer, PrizeMarker, MaxClasses) < 0) lengths.Add(RoomyBlockLength(MaxClasses));
            return lengths.ToArray();
        }

        /// <summary>The size of the block the eye-contact music patch places.</summary>
        public static int MusicBlockLength => RoomyBlockLength(MaxClasses * MusicEntrySize);

        /// <summary>Writes <paramref name="table"/> into a new marked block with room for <paramref name="capacity"/>
        /// bytes, clears the table's old copy if it sat in the synthetic overlay, and returns the data's offset.</summary>
        private static long PlaceRoomyTable(string marker, byte[] table, int capacity, uint oldPointer, int oldLength, out string error)
        {
            error = null;
            byte[] synth = File.ReadAllBytes(Filesystem.expArmPath);
            byte[] block = new byte[RoomyBlockLength(capacity)];
            System.Text.Encoding.ASCII.GetBytes(marker).CopyTo(block, 0);
            BitConverter.GetBytes(1u).CopyTo(block, 0x0C);
            BitConverter.GetBytes((uint)block.Length).CopyTo(block, 0x10);
            table.CopyTo(block, SyntheticOverlaySpace.HeaderSize);
            List<(long Start, long End)> reserved = SyntheticOverlaySpace.Reserved(synth);
            if (_writtenThisOperation != null) reserved.AddRange(_writtenThisOperation);
            // The old copy is about to be left behind, so its bytes may be reused.
            if (oldPointer >= RomInfo.synthOverlayLoadAddress)
            {
                long old = oldPointer - RomInfo.synthOverlayLoadAddress;
                reserved.RemoveAll(r => r.Start == old);
                if (old + oldLength <= synth.Length) Array.Clear(synth, (int)old, oldLength);
            }
            int at;
            try { at = SyntheticOverlaySpace.Place(synth, block.Length, reserved); }
            catch (InvalidOperationException e) { error = e.Message; return -1; }
            if (at < 0) { error = "No free space was found in the expanded ARM9 area for this table."; return -1; }
            block.CopyTo(synth, at);
            File.WriteAllBytes(Filesystem.expArmPath, synth);
            _writtenThisOperation?.Add((at, at + block.Length));
            return at + SyntheticOverlaySpace.HeaderSize;
        }

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
                uint ptr = BitConverter.ToUInt32(DSUtils.ReadFromFile(RomInfo.arm9Path, RomInfo.trainerClassGenderPointerOffset, 4), 0);
                IsGenderTableRepointed = ptr >= RomInfo.synthOverlayLoadAddress;
            }
            catch { /* leave false */ }

            try
            {
                string prizePath = OverlayUtils.GetPath(RomInfo.trainerPrizeTable.Overlay);
                uint ptr = BitConverter.ToUInt32(DSUtils.ReadFromFile(prizePath, RomInfo.trainerPrizeTable.Pointer, 4), 0);
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
            if (!IsSupportedForCurrentRom) { error = "Only implemented for Platinum."; return false; }
            if (!TryResolveByteTable(RomInfo.arm9Path, RomInfo.trainerClassGenderPointerOffset, RomInfo.arm9Path, RomInfo.trainerClassGenderTableOffset, RomInfo.vanillaTrainerClassCount, out byte[] table, out error))
                return false;
            if (classId < 0 || classId >= table.Length) { error = "Class index out of range."; return false; }
            gender = table[classId];
            return true;
        }

        public static bool TryWriteGender(int classId, byte gender, out string error)
        {
            error = null;
            if (!IsSupportedForCurrentRom) { error = "Only implemented for Platinum."; return false; }
            if (!TryResolveByteTable(RomInfo.arm9Path, RomInfo.trainerClassGenderPointerOffset, RomInfo.arm9Path, RomInfo.trainerClassGenderTableOffset, RomInfo.vanillaTrainerClassCount, out byte[] table, out error))
                return false;
            if (classId < 0 || classId >= table.Length) { error = "Class index out of range."; return false; }

            uint ptr = BitConverter.ToUInt32(DSUtils.ReadFromFile(RomInfo.arm9Path, RomInfo.trainerClassGenderPointerOffset, 4), 0);
            if (ptr < RomInfo.synthOverlayLoadAddress)
            {
                // Not repointed: the retail table is the one the game reads, and classId is inside it.
                if (ptr != ARM9.address + RomInfo.trainerClassGenderTableOffset)
                {
                    error = "The gender table was moved inside arm9, so it can't be written here.";
                    return false;
                }
                DSUtils.WriteToFile(RomInfo.arm9Path, new[] { gender }, (uint)(RomInfo.trainerClassGenderTableOffset + classId));
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
        // A blank entry gets the English article in English; other languages keep the name as typed.
        private static string WithArticle(string name) => RomInfo.gameLanguage != GameLanguages.English ? name.Trim()
            : ("AEIOU".IndexOf(char.ToUpperInvariant(name.TrimStart().FirstOrDefault())) >= 0 ? "an " : "a ") + name.Trim();

        public static string AddRefusal(string name)
        {
            if (!IsSupportedForCurrentRom) return "Adding trainer classes is only supported for Platinum right now.";
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
            EnsureOverlayDecompressed(RomInfo.trainerPrizeTable.Overlay);

            // Validate every table resolves before writing anything (all-or-nothing).
            if (!TryResolveByteTable(RomInfo.arm9Path, RomInfo.trainerClassGenderPointerOffset, RomInfo.arm9Path, RomInfo.trainerClassGenderTableOffset, RomInfo.vanillaTrainerClassCount, out byte[] genderTable, out error))
                return false;
            string prizePath = OverlayUtils.GetPath(RomInfo.trainerPrizeTable.Overlay);
            if (!TryResolveByteTable(prizePath, RomInfo.trainerPrizeTable.Pointer, prizePath, RomInfo.trainerPrizeTable.Table, RomInfo.trainerPrizeTable.Count, out byte[] prizeMulTable, out error))
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

            if (!ClassTablesHaveRoom)
            {
                error = "Adding a class needs the \"Trainer class tables\" patch from the ROM Patch Toolbox.";
                return false;
            }
            if (newClassId >= MaxClasses)
            {
                error = $"The game names a trainer class with one byte, so there can be at most {MaxClasses}.";
                return false;
            }
            if (addEncounterMusic && !MusicTableHasRoom)
            {
                error = "Eye-contact music for a new class needs the \"Trainer encounter music table\" patch from the ROM Patch Toolbox.";
                return false;
            }

            try
            {
                // The tables already have room, so the new entry goes straight in.
                DSUtils.WriteToFile(Filesystem.expArmPath, new[] { gender }, (uint)(GenderPointer - RomInfo.synthOverlayLoadAddress + newClassId));
                DSUtils.WriteToFile(Filesystem.expArmPath, new[] { prizeMultiplier }, (uint)(PrizePointer - RomInfo.synthOverlayLoadAddress + newClassId));

                if (addEncounterMusic && !AddEncounterMusicEntry((byte)newClassId, musicMain, musicNight, out error))
                    return false;

                if (spriteFromClass >= 0)
                    for (int part = 0; part < SpriteFilesPerClass; part++)
                        File.Copy(SpriteFile(spriteFromClass, part), SpriteFile(newClassId, part), overwrite: true);

                TextArchive nameArchive = new TextArchive(RomInfo.trainerClassMessageNumber);
                nameArchive.messages.Add(name);
                nameArchive.SaveToExpandedDir(RomInfo.trainerClassMessageNumber, showSuccessMessage: false);

                // Japanese and Korean have no article bank.
                if (RomInfo.trainerClassWithArticleMessageNumber >= 0)
                {
                    TextArchive articleArchive = new TextArchive(RomInfo.trainerClassWithArticleMessageNumber);
                    articleArchive.messages.Add(string.IsNullOrWhiteSpace(nameWithArticle) ? WithArticle(name) : nameWithArticle);
                    articleArchive.SaveToExpandedDir(RomInfo.trainerClassWithArticleMessageNumber, showSuccessMessage: false);
                }

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
            error = !IsSupportedForCurrentRom ? "Only Platinum is supported."
                : !SyntheticOverlaySpace.Available() ? NeedsExpansion : null;
            if (error != null) return false;

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.synthOverlay, DirNames.textArchives });
            EnsureOverlayDecompressed(RomInfo.trainerPrizeTable.Overlay);
            Detect();

            string prizePath = OverlayUtils.GetPath(RomInfo.trainerPrizeTable.Overlay);
            if (!TryResolveByteTable(RomInfo.arm9Path, RomInfo.trainerClassGenderPointerOffset, RomInfo.arm9Path, RomInfo.trainerClassGenderTableOffset, RomInfo.vanillaTrainerClassCount, out byte[] genderTable, out error))
                return false;
            if (!TryResolveByteTable(prizePath, RomInfo.trainerPrizeTable.Pointer, prizePath, RomInfo.trainerPrizeTable.Table, RomInfo.trainerPrizeTable.Count, out byte[] prizeMulTable, out error))
                return false;

            // A moved table's length is read from the class name count, so both must already match it.
            int classCount = new TextArchive(RomInfo.trainerClassMessageNumber).messages.Count;
            if (genderTable.Length != classCount || prizeMulTable.Length != classCount)
            {
                error = $"The ROM has {classCount} trainer class names but the tables have {genderTable.Length} and {prizeMulTable.Length} entries.";
                return false;
            }

            if (RoomyTable(GenderPointer, GenderMarker, MaxClasses) < 0)
            {
                uint old = GenderPointer;
                long at = PlaceRoomyTable(GenderMarker, genderTable, MaxClasses, old, genderTable.Length, out error);
                if (at < 0) return false;
                DSUtils.WriteToFile(RomInfo.arm9Path, BitConverter.GetBytes(RomInfo.synthOverlayLoadAddress + (uint)at), RomInfo.trainerClassGenderPointerOffset);
            }
            if (RoomyTable(PrizePointer, PrizeMarker, MaxClasses) < 0)
            {
                uint old = PrizePointer;
                long at = PlaceRoomyTable(PrizeMarker, prizeMulTable, MaxClasses, old, prizeMulTable.Length, out error);
                if (at < 0) return false;
                DSUtils.WriteToFile(prizePath, BitConverter.GetBytes(RomInfo.synthOverlayLoadAddress + (uint)at), RomInfo.trainerPrizeTable.Pointer);
            }
            Detect();
            return true;
        }

        /// <summary>Moves the eye-contact music table into the synthetic overlay unchanged.</summary>
        public static bool MoveEncounterMusicTable(out string error)
        {
            error = !IsSupportedForCurrentRom ? "Only Platinum is supported."
                : !SyntheticOverlaySpace.Available() ? NeedsExpansion : null;
            if (error != null) return false;
            if (MusicTableHasRoom) return true;
            _writtenThisOperation = new List<(long, long)>();
            try { return RepointEncounterMusic(null, null, out error); }
            finally { _writtenThisOperation = null; }
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
                uint lengthFieldOffset = RomInfo.encounterMusicCountSite;

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

                long roomy = RoomyTable(ptr, MusicMarker, MaxClasses * entrySize);
                if (newEntry != null)
                {
                    // Adding writes into the room the toolbox patch made; only the entry count changes in the code.
                    if (roomy < 0)
                    {
                        error = "More eye-contact music needs the \"Trainer encounter music table\" patch from the ROM Patch Toolbox.";
                        return false;
                    }
                    if (entryCount >= MaxClasses) { error = "Every trainer class already has eye-contact music."; return false; }
                    DSUtils.WriteToFile(Filesystem.expArmPath, newEntry, (uint)(roomy + entryCount * entrySize));
                    DSUtils.WriteToFile(RomInfo.arm9Path, new[] { (byte)(entryCount + 1) }, lengthFieldOffset);
                    RomPatchState.flag_TrainerEncounterBGMTableRepointed = true;
                    return true;
                }

                long newStart = PlaceRoomyTable(MusicMarker, existing, MaxClasses * entrySize, ptr, existing.Length, out error);
                if (newStart < 0) return false;
                uint newBase = RomInfo.synthOverlayLoadAddress + (uint)newStart;
                // This table is referenced by *two* pointers: base, and base+2 (used to read the
                // first entry's seqId half directly). Both need to point at the new location.
                DSUtils.WriteToFile(RomInfo.arm9Path, BitConverter.GetBytes(newBase), RomInfo.encounterMusicTableOffsetToRAMAddress);
                DSUtils.WriteToFile(RomInfo.arm9Path, BitConverter.GetBytes(newBase + 2), RomInfo.encounterMusicTableOffsetToRAMAddress + 4);
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
                    uint gender = BitConverter.ToUInt32(DSUtils.ReadFromFile(RomInfo.arm9Path, RomInfo.trainerClassGenderPointerOffset, 4), 0);
                    if (gender >= load) ranges.Add((gender - load, gender - load + classCount));
                    uint prize = BitConverter.ToUInt32(DSUtils.ReadFromFile(OverlayUtils.GetPath(RomInfo.trainerPrizeTable.Overlay), RomInfo.trainerPrizeTable.Pointer, 4), 0);
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
                    uint lengthField = RomInfo.encounterMusicCountSite;
                    int entrySize = RomInfo.gameFamily == GameFamilies.HGSS ? 6 : 4;
                    ranges.Add((music - load, music - load + ARM9.ReadByte(lengthField) * entrySize));
                }
            }
            catch { /* an unreadable table reserves nothing */ }
            return ranges;
        }
    }
}

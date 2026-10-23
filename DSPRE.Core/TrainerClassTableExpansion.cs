using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE
{
    /// <summary>
    /// Adds Platinum trainer classes: the gender, prize and eye-contact music tables move into the synthetic overlay
    /// with room for every class, the names are added and a front sprite is copied. Other games are refused, since
    /// the game doesn't bounds-check these tables.
    ///
    /// Each table moves once, through <see cref="PlaceRoomyTable"/>, into a marked block of the synthetic overlay with
    /// room for every class; later additions write into that room and only change the count.
    /// </summary>
    /// <remarks>The tables are sTrainerClassGender, sTrainerClassPrizeMul and sTrainerEncounterBGMs.</remarks>
    public static class TrainerClassTableExpansion
    {
        public static bool IsSupportedForCurrentRom =>
            RomInfo.GameFamily == GameFamilies.Plat && RomLocator.TrainerClassGenderPointer != 0 && RomLocator.TrainerPrizeTable?.Pointer > 0;

        // A pointer of 0 means "always read/write the vanilla table directly" (DP and HGSS).
        public static bool IsPrizeMulSupportedForCurrentRom => TryGetPrizeMulTableInfo(out _, out _, out _, out _, out _);

        private static bool TryGetPrizeMulTableInfo(out int overlayNumber, out uint pointerOffset, out uint vanillaOffset, out int vanillaCount, out bool isPaired)
        {
            RomInfo.TrainerPrizeTable t = RomLocator.TrainerPrizeTable;
            overlayNumber = t?.Overlay ?? 0; pointerOffset = t?.Pointer ?? 0; vanillaOffset = t?.Table ?? 0;
            vanillaCount = t?.Count ?? 0; isPaired = t?.Paired ?? false;
            return t != null;
        }

        // sTrainerEncounterBGMs' offsets are already tracked (all languages) by
        // RomInfo.encounterMusicTableOffsetToRAMAddress,
        // and its repoint-aware read/write already exists; only "append a new entry" is new here.

        // The toolbox patches place each table in a marked block with room for every class a byte can name, so adding
        // a class or its music writes in place and never moves a table or rewrites a pointer.
        public const string GenderMarker = "TRCLSGENDER1", PrizeMarker = "TRCLSPRIZE01", MusicMarker = "TRCLSMUSIC01";
        public const int MaxClasses = 256;
        private static int MusicEntrySize => RomInfo.GameFamily == GameFamilies.HGSS ? 6 : 4;

        /// <summary>The data offset of a table in the synthetic overlay when its pointer leads into a marked block with
        /// room for <paramref name="capacity"/> bytes, else -1.</summary>
        private static long RoomyTable(uint pointer, string marker, int capacity)
        {
            if (pointer < RomInfo.SynthOverlayLoadAddress || !File.Exists(Filesystem.ExpArmPath)) return -1;
            long at = pointer - RomInfo.SynthOverlayLoadAddress;
            byte[] synth = File.ReadAllBytes(Filesystem.ExpArmPath);
            foreach ((long Start, long End) b in SyntheticOverlaySpace.Blocks(synth, marker))
                if (at == b.Start + SyntheticOverlaySpace.HeaderSize && b.End - at >= capacity) return at;
            return -1;
        }

        private static uint GenderPointer => BitConverter.ToUInt32(DSUtils.ReadFromFile(RomInfo.Arm9Path, RomLocator.TrainerClassGenderPointer, 4), 0);
        private static uint PrizePointer => BitConverter.ToUInt32(DSUtils.ReadFromFile(OverlayUtils.GetPath(RomLocator.TrainerPrizeTable.Overlay), RomLocator.TrainerPrizeTable.Pointer, 4), 0);

        /// <summary>Whether the gender and prize tables have room for every class, as the toolbox patch leaves them.</summary>
        public static bool ClassTablesHaveRoom
        {
            get
            {
                try
                {
                    EnsureOverlayDecompressed(RomLocator.TrainerPrizeTable.Overlay);
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
                    uint ptr = DSPRE.ROMFiles.EncounterMusicTable.Address();
                    return RoomyTable(ptr, MusicMarker, MaxClasses * MusicEntrySize) >= 0;
                }
                catch (Exception e) when (e is IOException || e is ArgumentException) { return false; }
            }
        }

        private static int RoomyBlockLength(int capacity) => (SyntheticOverlaySpace.HeaderSize + capacity + 3) & ~3;

        /// <summary>The sizes of the blocks the class tables patch places, in the order it places them.</summary>
        public static int[] ClassTableBlockLengths()
        {
            EnsureOverlayDecompressed(RomLocator.TrainerPrizeTable.Overlay);
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
            byte[] synth = File.ReadAllBytes(Filesystem.ExpArmPath);
            byte[] block = new byte[RoomyBlockLength(capacity)];
            System.Text.Encoding.ASCII.GetBytes(marker).CopyTo(block, 0);
            BitConverter.GetBytes(1u).CopyTo(block, 0x0C);
            BitConverter.GetBytes((uint)block.Length).CopyTo(block, 0x10);
            table.CopyTo(block, SyntheticOverlaySpace.HeaderSize);
            List<(long Start, long End)> reserved = SyntheticOverlaySpace.Reserved(synth);
            if (_writtenThisOperation != null) reserved.AddRange(_writtenThisOperation);
            // The old copy is about to be left behind, so its bytes may be reused.
            if (oldPointer >= RomInfo.SynthOverlayLoadAddress)
            {
                long old = oldPointer - RomInfo.SynthOverlayLoadAddress;
                reserved.RemoveAll(r => r.Start == old);
                if (old + oldLength <= synth.Length) Array.Clear(synth, (int)old, oldLength);
            }
            int at;
            try { at = SyntheticOverlaySpace.Place(synth, block.Length, reserved); }
            catch (InvalidOperationException e) { error = e.Message; return -1; }
            if (at < 0) { error = "The synthetic overlay has no room left for this table."; return -1; }
            block.CopyTo(synth, at);
            DSUtils.WriteWholeFile(Filesystem.ExpArmPath, synth);
            _writtenThisOperation?.Add((at, at + block.Length));
            return at + SyntheticOverlaySpace.HeaderSize;
        }

        public static bool IsGenderTableRepointed { get; private set; }
        public static bool IsPrizeMulTableRepointed { get; private set; }

        /// <summary>Unlike the gender/prize-mul tables, sTrainerEncounterBGMs' offsets are already
        /// tracked for every language/family DSPRE supports (RomLocator.Thing.EncounterMusicPointer),
        /// so its repoint status can be checked regardless of IsSupportedForCurrentRom. This is used
        /// by the ROM patch toolbox status row so it doesn't require the Trainer editor to have been
        /// opened first, since loading it normally runs the check.</summary>
        public static bool DetectMusicTableRepointed()
        {
            try
            {
                uint ptr = DSPRE.ROMFiles.EncounterMusicTable.Address();
                bool repointed = ptr >= RomInfo.SynthOverlayLoadAddress;
                return repointed;
            }
            catch (Exception ex)
            {
                AppLogger.WarnOnce("TrainerClassTableExpansion.DetectMusicTableRepointed", ex);
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
                uint ptr = BitConverter.ToUInt32(DSUtils.ReadFromFile(RomInfo.Arm9Path, RomLocator.TrainerClassGenderPointer, 4), 0);
                IsGenderTableRepointed = ptr >= RomInfo.SynthOverlayLoadAddress;
            }
            catch { /* leave false */ }

            try
            {
                string prizePath = OverlayUtils.GetPath(RomLocator.TrainerPrizeTable.Overlay);
                uint ptr = BitConverter.ToUInt32(DSUtils.ReadFromFile(prizePath, RomLocator.TrainerPrizeTable.Pointer, 4), 0);
                IsPrizeMulTableRepointed = ptr >= RomInfo.SynthOverlayLoadAddress;
            }
            catch { /* leave false */ }
        }

        // ── Generic byte-array table resolve/read ────────────────────────────────────────────────
        // A moved table's length is the class name count, which AddTrainerClass keeps in step; the ROM stores none.
        private static bool TryResolveByteTable(string pointerFilePath, uint pointerFileOffset,
            string vanillaFilePath, uint vanillaFileOffset, int vanillaCount,
            out byte[] bytes, out string error)
        {
            bytes = null; error = null;
            try
            {
                uint ptr = BitConverter.ToUInt32(DSUtils.ReadFromFile(pointerFilePath, pointerFileOffset, 4), 0);
                bool repointed = ptr >= RomInfo.SynthOverlayLoadAddress;

                if (repointed)
                {
                    int classCount = new TextArchive(RomInfo.TrainerClassMessageNumber).Messages.Count;
                    long start = ptr - RomInfo.SynthOverlayLoadAddress;
                    bytes = DSUtils.ReadFromFile(Filesystem.ExpArmPath, start, classCount);
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
            if (!IsSupportedForCurrentRom) { error = "Only Platinum is supported."; return false; }
            if (!TryResolveByteTable(RomInfo.Arm9Path, RomLocator.TrainerClassGenderPointer, RomInfo.Arm9Path, RomInfo.TrainerClassGenderTableOffset, RomInfo.VanillaTrainerClassCount, out byte[] table, out error))
                return false;
            if (classId < 0 || classId >= table.Length) { error = $"There is no trainer class {classId}."; return false; }
            gender = table[classId];
            return true;
        }

        public static bool TryWriteGender(int classId, byte gender, out string error)
        {
            error = null;
            if (!IsSupportedForCurrentRom) { error = "Only Platinum is supported."; return false; }
            if (!TryResolveByteTable(RomInfo.Arm9Path, RomLocator.TrainerClassGenderPointer, RomInfo.Arm9Path, RomInfo.TrainerClassGenderTableOffset, RomInfo.VanillaTrainerClassCount, out byte[] table, out error))
                return false;
            if (classId < 0 || classId >= table.Length) { error = $"There is no trainer class {classId}."; return false; }

            uint ptr = BitConverter.ToUInt32(DSUtils.ReadFromFile(RomInfo.Arm9Path, RomLocator.TrainerClassGenderPointer, 4), 0);
            if (ptr < RomInfo.SynthOverlayLoadAddress)
            {
                // Not repointed: the retail table is the one the game reads, and classId is inside it.
                if (ptr != ARM9.Address + RomInfo.TrainerClassGenderTableOffset)
                {
                    error = "The gender table was moved inside arm9, so it can't be written here.";
                    return false;
                }
                DSUtils.WriteToFile(RomInfo.Arm9Path, new[] { gender }, (uint)(RomInfo.TrainerClassGenderTableOffset + classId));
                return true;
            }

            long start = ptr - RomInfo.SynthOverlayLoadAddress;
            DSUtils.WriteToFile(Filesystem.ExpArmPath, new[] { gender }, (uint)(start + classId));
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

            if (classId < 0 || classId >= table.Length) { error = $"There is no trainer class {classId}."; return false; }
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
            if (classId < 0 || classId >= table.Length) { error = $"There is no trainer class {classId}."; return false; }

            bool repointed = false;
            if (pointerOffset != 0)
            {
                uint ptr = BitConverter.ToUInt32(DSUtils.ReadFromFile(ovPath, pointerOffset, 4), 0);
                repointed = ptr >= RomInfo.SynthOverlayLoadAddress;
                if (repointed)
                {
                    long start = ptr - RomInfo.SynthOverlayLoadAddress;
                    DSUtils.WriteToFile(Filesystem.ExpArmPath, new[] { multiplier }, (uint)(start + classId));
                    return true;
                }
            }

            // Not repointed (or this family has no known repoint pointer): the vanilla table is the real one.
            DSUtils.WriteToFile(ovPath, new[] { multiplier }, (uint)(vanillaOffset + classId));
            return true;
        }

        // HGSS stores {u16 classId, u16 multiplier} pairs, not an array indexed by class,
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
                error = "This class has no prize money entry.";
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
                error = "This class has no prize money entry.";
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
        private static string WithArticle(string name) => RomInfo.GameLanguage != GameLanguages.English ? name.Trim()
            : ("AEIOU".IndexOf(char.ToUpperInvariant(name.TrimStart().FirstOrDefault())) >= 0 ? "an " : "a ") + name.Trim();

        public static string AddRefusal(string name)
        {
            if (!IsSupportedForCurrentRom) return "Adding trainer classes is only supported for Platinum right now.";
            if (!SyntheticOverlaySpace.Available()) return NeedsExpansion;
            return string.IsNullOrWhiteSpace(name) ? "Enter a class name." : null;
        }

        // Without the expansion the synthetic overlay is never loaded, so a moved table would be read from empty memory.
        private const string NeedsExpansion = "This needs the ARM9 expansion from the ROM patch toolbox, which isn't applied to this ROM.";

        // The game reads class N's front sprite from trfgra members 5N to 5N+4: tiles, palette, cells, animation, scan.
        private const int SpriteFilesPerClass = 5;

        private static string SpriteFile(int classId, int part) => Filesystem.GetTrainerGraphicsPath(classId * SpriteFilesPerClass + part);

        /// <summary>Whether the trainer sprite archive already holds all five files for this class.</summary>
        public static bool HasSprite(int classId)
        {
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.TrainerGraphics });
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

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.SynthOverlay, DirNames.TextArchives, DirNames.TrainerGraphics });
            EnsureOverlayDecompressed(RomLocator.TrainerPrizeTable.Overlay);

            // Validate every table resolves before writing anything (all-or-nothing).
            if (!TryResolveByteTable(RomInfo.Arm9Path, RomLocator.TrainerClassGenderPointer, RomInfo.Arm9Path, RomInfo.TrainerClassGenderTableOffset, RomInfo.VanillaTrainerClassCount, out byte[] genderTable, out error))
                return false;
            string prizePath = OverlayUtils.GetPath(RomLocator.TrainerPrizeTable.Overlay);
            if (!TryResolveByteTable(prizePath, RomLocator.TrainerPrizeTable.Pointer, prizePath, RomLocator.TrainerPrizeTable.Table, RomLocator.TrainerPrizeTable.Count, out byte[] prizeMulTable, out error))
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
                error = "Adding a class needs the \"Trainer class tables\" patch from the ROM patch toolbox.";
                return false;
            }
            if (newClassId >= MaxClasses)
            {
                error = $"The game names a trainer class with one byte, so there can be at most {MaxClasses}.";
                return false;
            }
            if (addEncounterMusic && !MusicTableHasRoom)
            {
                error = "Eye-contact music for a new class needs the \"Trainer encounter music table\" patch from the ROM patch toolbox.";
                return false;
            }

            try
            {
                // The tables already have room, so the new entry goes straight in.
                DSUtils.WriteToFile(Filesystem.ExpArmPath, new[] { gender }, (uint)(GenderPointer - RomInfo.SynthOverlayLoadAddress + newClassId));
                DSUtils.WriteToFile(Filesystem.ExpArmPath, new[] { prizeMultiplier }, (uint)(PrizePointer - RomInfo.SynthOverlayLoadAddress + newClassId));

                if (addEncounterMusic && !AddEncounterMusicEntry((byte)newClassId, musicMain, musicNight, out error))
                    return false;

                if (spriteFromClass >= 0)
                    for (int part = 0; part < SpriteFilesPerClass; part++)
                        ArchiveMembers.Write(SpriteFile(newClassId, part), File.ReadAllBytes(SpriteFile(spriteFromClass, part)));

                TextArchive nameArchive = new TextArchive(RomInfo.TrainerClassMessageNumber);
                nameArchive.Messages.Add(name);
                nameArchive.SaveToExpandedDir(RomInfo.TrainerClassMessageNumber, showSuccessMessage: false);

                // Japanese and Korean have no article bank.
                if (RomInfo.TrainerClassWithArticleMessageNumber >= 0)
                {
                    TextArchive articleArchive = new TextArchive(RomInfo.TrainerClassWithArticleMessageNumber);
                    articleArchive.Messages.Add(string.IsNullOrWhiteSpace(nameWithArticle) ? WithArticle(name) : nameWithArticle);
                    articleArchive.SaveToExpandedDir(RomInfo.TrainerClassWithArticleMessageNumber, showSuccessMessage: false);
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

            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.SynthOverlay, DirNames.TextArchives });
            EnsureOverlayDecompressed(RomLocator.TrainerPrizeTable.Overlay);
            Detect();

            string prizePath = OverlayUtils.GetPath(RomLocator.TrainerPrizeTable.Overlay);
            if (!TryResolveByteTable(RomInfo.Arm9Path, RomLocator.TrainerClassGenderPointer, RomInfo.Arm9Path, RomInfo.TrainerClassGenderTableOffset, RomInfo.VanillaTrainerClassCount, out byte[] genderTable, out error))
                return false;
            if (!TryResolveByteTable(prizePath, RomLocator.TrainerPrizeTable.Pointer, prizePath, RomLocator.TrainerPrizeTable.Table, RomLocator.TrainerPrizeTable.Count, out byte[] prizeMulTable, out error))
                return false;

            // A moved table's length is read from the class name count, so both must already match it.
            int classCount = new TextArchive(RomInfo.TrainerClassMessageNumber).Messages.Count;
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
                DSUtils.WriteToFile(RomInfo.Arm9Path, BitConverter.GetBytes(RomInfo.SynthOverlayLoadAddress + (uint)at), RomLocator.TrainerClassGenderPointer);
            }
            if (RoomyTable(PrizePointer, PrizeMarker, MaxClasses) < 0)
            {
                uint old = PrizePointer;
                long at = PlaceRoomyTable(PrizeMarker, prizeMulTable, MaxClasses, old, prizeMulTable.Length, out error);
                if (at < 0) return false;
                DSUtils.WriteToFile(prizePath, BitConverter.GetBytes(RomInfo.SynthOverlayLoadAddress + (uint)at), RomLocator.TrainerPrizeTable.Pointer);
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

        /// <summary>Adds eye-contact music for a class that has none, new or existing; fails when it already
        /// has an entry.</summary>
        public static bool AddEncounterMusicEntry(byte classId, ushort musicMain, ushort musicNight, out string error)
        {
            byte[] newEntry = new byte[RomInfo.GameFamily == GameFamilies.HGSS ? 6 : 4];
            BitConverter.GetBytes((ushort)classId).CopyTo(newEntry, 0);
            BitConverter.GetBytes(musicMain).CopyTo(newEntry, 2);
            if (RomInfo.GameFamily == GameFamilies.HGSS)
                BitConverter.GetBytes(musicNight).CopyTo(newEntry, 4);
            return RepointEncounterMusic(classId, newEntry, out error);
        }

        private static bool RepointEncounterMusic(byte? classId, byte[] newEntry, out string error)
        {
            error = null;
            try
            {
                uint lengthFieldOffset = RomInfo.EncounterMusicCountSite;

                uint ptr = DSPRE.ROMFiles.EncounterMusicTable.Address();
                bool repointed = ptr >= RomInfo.SynthOverlayLoadAddress;
                string dataPath = repointed ? Filesystem.ExpArmPath : RomInfo.Arm9Path;
                long dataStart = ptr - (repointed ? RomInfo.SynthOverlayLoadAddress : ARM9.Address);

                byte entryCount = ARM9.ReadByte(lengthFieldOffset);
                int entrySize = RomInfo.GameFamily == GameFamilies.HGSS ? 6 : 4;
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
                        error = "More eye-contact music needs the \"Trainer encounter music table\" patch from the ROM patch toolbox.";
                        return false;
                    }
                    if (entryCount >= MaxClasses) { error = "Every trainer class already has eye-contact music."; return false; }
                    DSUtils.WriteToFile(Filesystem.ExpArmPath, newEntry, (uint)(roomy + entryCount * entrySize));
                    DSUtils.WriteToFile(RomInfo.Arm9Path, new[] { (byte)(entryCount + 1) }, lengthFieldOffset);
                    return true;
                }

                long newStart = PlaceRoomyTable(MusicMarker, existing, MaxClasses * entrySize, ptr, existing.Length, out error);
                if (newStart < 0) return false;
                uint newBase = RomInfo.SynthOverlayLoadAddress + (uint)newStart;
                // This table is referenced by *two* pointers: base, and base+2 (used to read the
                // first entry's seqId half directly). Both need to point at the new location.
                DSUtils.WriteToFile(RomInfo.Arm9Path, BitConverter.GetBytes(newBase), RomInfo.EncounterMusicTableOffsetToRAMAddress);
                DSUtils.WriteToFile(RomInfo.Arm9Path, BitConverter.GetBytes(newBase + 2), RomInfo.EncounterMusicTableOffsetToRAMAddress + 4);
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

        // Tables written earlier in this add or move: the class count that sizes them grows only at the end.
        private static List<(long Start, long End)> _writtenThisOperation;

        /// <summary>The synthetic overlay bytes the moved trainer class and eye-contact music tables use.
        /// Their entries can be zero, so a free-space scan would otherwise take them for empty space.</summary>
        public static List<(long Start, long End)> MovedTableRanges()
        {
            List<(long, long)> ranges = new List<(long, long)>();
            if (RomInfo.IsHGE) return ranges;
            uint load = RomInfo.SynthOverlayLoadAddress;
            if (IsSupportedForCurrentRom)
            {
                try
                {
                    int classCount = new TextArchive(RomInfo.TrainerClassMessageNumber).Messages.Count;
                    uint gender = BitConverter.ToUInt32(DSUtils.ReadFromFile(RomInfo.Arm9Path, RomLocator.TrainerClassGenderPointer, 4), 0);
                    if (gender >= load) ranges.Add((gender - load, gender - load + classCount));
                    uint prize = BitConverter.ToUInt32(DSUtils.ReadFromFile(OverlayUtils.GetPath(RomLocator.TrainerPrizeTable.Overlay), RomLocator.TrainerPrizeTable.Pointer, 4), 0);
                    if (prize >= load) ranges.Add((prize - load, prize - load + classCount));
                }
                catch { /* an unreadable table reserves nothing */ }
            }
            try
            {
                uint music = DSPRE.ROMFiles.EncounterMusicTable.Address();
                if (music >= load)
                {
                    uint lengthField = RomInfo.EncounterMusicCountSite;
                    int entrySize = RomInfo.GameFamily == GameFamilies.HGSS ? 6 : 4;
                    ranges.Add((music - load, music - load + ARM9.ReadByte(lengthField) * entrySize));
                }
            }
            catch { /* an unreadable table reserves nothing */ }
            return ranges;
        }
    }
}

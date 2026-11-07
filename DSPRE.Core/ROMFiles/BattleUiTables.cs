using System;
using System.Collections.Generic;
using System.IO;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Read-only tables the battle menus and summary icons read from code, not archives. Each reader returns null when
    /// the table isn't found or doesn't look right.
    /// </summary>
    public static class BattleUiTables
    {
        /// <summary>18 types, then Cool, Beauty, Cute, Smart and Tough.</summary>
        public const int IconCount = 23;
        public const int TypeCount = 18, CategoryCount = 3, PaletteBanks = 3;

        public sealed class IconTables
        {
            public byte[] TypeBanks;      // IconCount entries
            public byte[] CategoryBanks;  // physical, special, status
            public uint[] TypeMembers;    // battle object archive member of each type or contest icon
        }

        public static IconTables ReadIconTables()
        {
            byte[] banks = TryRead(RomLocator.Thing.TypeIconBanks, IconCount);
            byte[] kinds = TryRead(RomLocator.Thing.MoveCategoryIconBanks, CategoryCount);
            byte[] members = TryRead(RomLocator.Thing.TypeIconMembers, IconCount * 4);
            if (banks == null || kinds == null || members == null) return null;
            if (Array.Exists(banks, b => b >= PaletteBanks) || Array.Exists(kinds, b => b >= PaletteBanks)) return null;
            uint[] ids = new uint[IconCount];
            for (int i = 0; i < IconCount; i++) ids[i] = BitConverter.ToUInt32(members, i * 4);
            return new IconTables { TypeBanks = banks, CategoryBanks = kinds, TypeMembers = ids };
        }

        /// <summary>Each type's icon, then the five contest icons, as the game's icon getters return them.</summary>
        public sealed class TypeIconTable
        {
            public int TypeCount;
            public uint[] Members;   // TypeCount + 5 entries
            public byte[] Banks;     // TypeCount + 5 entries
            /// <summary>Where a type expansion moved the tables in the synthetic overlay, or empty for the retail tables.</summary>
            public List<(long Start, long End)> InExpansion = new();
        }

        public const int ContestCount = 5;
        private const int MaxTypes = 255;

        /// <summary>The type icon tables the game reads, following a type expansion's branch if there is one. Null when unreadable.</summary>
        public static TypeIconTable ReadTypeIcons()
        {
            IconTables fixedTables = ReadIconTables();
            if (fixedTables == null) return null;
            TypeIconTable retail = new TypeIconTable { TypeCount = TypeCount, Members = fixedTables.TypeMembers, Banks = fixedTables.TypeBanks };
            try { return Expanded(fixedTables) ?? retail; }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            {
                AppLogger.WarnOnce("BattleUiTables.ReadTypeIcons", e);
                return retail;
            }
        }

        // Each getter is `ldr r0, =table` with its literal 0x14 bytes after its start, the same in DP, Pt and HGSS.
        // The type expansion guide replaces its first instructions with `push {lr}; bl new; pop {pc}`.
        private const int GetterLength = 0x14;

        private static TypeIconTable Expanded(IconTables fixedTables)
        {
            RomLocator.Spot members = RomLocator.Where(RomLocator.Thing.TypeIconMembers), banks = RomLocator.Where(RomLocator.Thing.TypeIconBanks);
            if (members == null || banks == null || members.Overlay >= 0 || banks.Overlay >= 0) return null;
            byte[] arm9 = File.ReadAllBytes(Arm9Path);
            byte[] synth = SyntheticOverlaySpace.BytesIfAny() ?? Array.Empty<byte>();

            (uint Ram, int Size)? memberTable = PatchedTable(arm9, synth, ARM9.Address + (uint)members.Offset);
            (uint Ram, int Size)? bankTable = PatchedTable(arm9, synth, ARM9.Address + (uint)banks.Offset);
            if (memberTable == null || bankTable == null || bankTable.Value.Size != 1) return null;

            byte[] memberFile = FileHolding(arm9, synth, memberTable.Value.Ram, out int memberAt);
            byte[] bankFile = FileHolding(arm9, synth, bankTable.Value.Ram, out int bankAt);
            if (memberFile == null || bankFile == null) return null;
            int width = memberTable.Value.Size;
            uint Member(int i) => width == 2 ? BitConverter.ToUInt16(memberFile, memberAt + i * 2) : BitConverter.ToUInt32(memberFile, memberAt + i * 4);
            int fits = Math.Min((memberFile.Length - memberAt) / width, bankFile.Length - bankAt);

            // The contest icons follow the types, so the first retail contest member marks the type count.
            for (int types = TypeCount; types <= MaxTypes && types + ContestCount <= fits; types++)
            {
                bool contests = true;
                for (int c = 0; c < ContestCount && contests; c++) contests = Member(types + c) == fixedTables.TypeMembers[TypeCount + c];
                if (!contests) continue;
                int count = types + ContestCount;
                TypeIconTable t = new TypeIconTable { TypeCount = types, Members = new uint[count], Banks = new byte[count] };
                for (int i = 0; i < count; i++) { t.Members[i] = Member(i); t.Banks[i] = bankFile[bankAt + i]; }
                if (ReferenceEquals(memberFile, synth)) t.InExpansion.Add((memberAt, memberAt + (long)count * width));
                if (ReferenceEquals(bankFile, synth)) t.InExpansion.Add((bankAt, bankAt + (long)count));
                return Array.Exists(t.Banks, b => b >= PaletteBanks) ? null : t;
            }
            return null;
        }

        /// <summary>The table a patched getter indexes and its entry size, or null when the getter isn't patched.</summary>
        private static (uint Ram, int Size)? PatchedTable(byte[] arm9, byte[] synth, uint retailTable)
        {
            byte[] literal = BitConverter.GetBytes(retailTable);
            int at = arm9.AsSpan().IndexOf(literal);
            if (at < GetterLength || at % 4 != 0 || arm9.AsSpan(at + 4).IndexOf(literal) >= 0) return null;
            int getter = at - GetterLength;
            if (BitConverter.ToUInt16(arm9, getter) != 0xB500 || BitConverter.ToUInt16(arm9, getter + 6) != 0xBD00) return null;
            if (!PatchToolboxLogic.TryGetThumbBlTarget(ARM9.Address + (uint)getter + 2, arm9[(getter + 2)..(getter + 6)], out uint target)) return null;

            uint start = target & ~1u;
            byte[] code = FileHolding(arm9, synth, start, out int pc);
            if (code == null) return null;
            uint? table = null;
            for (int i = 0; i < 8 && pc + i * 2 + 2 <= code.Length; i++)
            {
                ushort op = BitConverter.ToUInt16(code, pc + i * 2);
                if ((op & 0xF800) == 0x4800)   // ldr rN, [pc, #imm]
                {
                    uint pool = ((start + (uint)(i * 2) + 4) & ~3u) + (uint)((op & 0xFF) * 4);
                    byte[] poolFile = FileHolding(arm9, synth, pool, out int poolAt);
                    if (poolFile == null || poolAt + 4 > poolFile.Length) return null;
                    table = BitConverter.ToUInt32(poolFile, poolAt);
                }
                else if (table != null && (op & 0xFE00) == 0x5A00) return (table.Value, 2);   // ldrh rD, [rN, rM]
                else if (table != null && (op & 0xFE00) == 0x5800) return (table.Value, 4);   // ldr rD, [rN, rM]
                else if (table != null && (op & 0xFE00) == 0x5C00) return (table.Value, 1);   // ldrb rD, [rN, rM]
                else if (op == 0xBD00 || (op & 0xFF80) == 0x4700) return null;               // pop {pc} or bx
            }
            return null;
        }

        /// <summary>The synthetic overlay or arm9, whichever holds <paramref name="ram"/>, and the offset there.</summary>
        private static byte[] FileHolding(byte[] arm9, byte[] synth, uint ram, out int offset)
        {
            (int region, int at) = RamAddress.Find(ram, 1, RamAddress.Synthetic(synth.Length), RamAddress.Arm9(arm9.Length));
            offset = at;
            return region switch { 0 => synth, 1 => arm9, _ => null };
        }

        /// <summary>The 16 BGR555 colours the game paints a move button with for a move of this type.</summary>
        public static ushort[] MoveButtonPalette(int type)
        {
            if (type < 0 || type >= TypeCount) return null;
            try
            {
                RomLocator.Spot spot = RomLocator.Where(RomLocator.Thing.MoveTypeButtonPalettes);
                if (spot == null || RomLocator.WhyNot(RomLocator.Thing.MoveTypeButtonPalettes, TypeCount * 4) != null) return null;
                // A preview must not rewrite the project, so a still-compressed overlay is not unpacked here.
                if (spot.Overlay >= 0 && OverlayUtils.IsStillCompressed(spot.Overlay)) return null;

                byte[] file = File.ReadAllBytes(RomLocator.PathOf(spot));
                uint ramBase = RomLocator.RamBase(spot);
                uint pointer = BitConverter.ToUInt32(file, spot.Offset + type * 4);
                long at = (long)pointer - ramBase;
                if ((pointer & 1) != 0 || at < 0 || at + 32 > file.Length) return null;

                ushort[] colours = new ushort[16];
                for (int i = 0; i < 16; i++) colours[i] = BitConverter.ToUInt16(file, (int)at + i * 2);
                return colours;
            }
            catch (Exception e) when (e is IOException || e is InvalidOperationException || e is UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static byte[] TryRead(RomLocator.Thing table, int length)
        {
            try { return RomLocator.WhyNot(table, length) == null ? RomLocator.Read(table, length) : null; }
            catch (Exception e) when (e is IOException || e is InvalidOperationException || e is UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}

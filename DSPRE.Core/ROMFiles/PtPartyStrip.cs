using System;
using System.IO;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Where Platinum's party panels keep the HP bar strip: the egg blank, the health bar getter and the copy after a switch all
    /// hold its column and row in arm9.
    /// </summary>
    public static class PtPartyStrip
    {
        public const int PanelWidth = 16, PanelHeight = 6, Length = 9;
        /// <summary>Tiles from the strip's start to the bar, past the "HP" label.</summary>
        public const int BarOffset = 2;
        // Offset of the back panel buffer in the party menu's work area. The getter adds the strip's offset to it.
        private const int BackPanel = 0x324;

        /// <summary>Column and row in the panel, or null where the code isn't the game's.</summary>
        public static (int Column, int Row)? Read()
        {
            RomInfo.PtPartyStripSites sites = RomLocator.PtPartyStripSites;
            if (sites == null || !File.Exists(RomInfo.Arm9Path)) return null;
            byte[] arm9 = File.ReadAllBytes(RomInfo.Arm9Path);
            if (sites.Swap.Max() + 2 > arm9.Length) return null;
            ushort U(uint at) => BitConverter.ToUInt16(arm9, (int)at);
            int Imm3(ushort op) => ThumbImm3.Is(op) ? ThumbImm3.Value(op) : -1;
            int row = Imm3(U(sites.EggRow)), column = Imm3(U(sites.EggRow + 0x12));
            if (row < 0 || column < 0) return null;
            int offset = 2 * (row * PanelWidth + column);
            ushort movs = U(sites.Getter), lsls = U(sites.Getter + 2);
            // lsls r1, r1, #n
            if (!ThumbImm8.Is(movs, ThumbImm8.Op.Movs) || !ThumbLsls.Is(lsls) || (lsls & 0x3F) != 0x09) return null;
            if (ThumbImm8.Value(movs) << ThumbLsls.Amount(lsls) != BackPanel + offset) return null;
            if (sites.Swap.Any(at => !ThumbImm8.Is(U(at), ThumbImm8.Op.Adds) || ThumbImm8.Value(U(at)) != offset)) return null;
            return (column, row);
        }

        /// <summary>Null when the code can hold it.</summary>
        public static string WhyNot(int column, int row)
        {
            if (column < 0 || row < 0 || column + Length > PanelWidth || row >= PanelHeight) return "The HP bar frame has to stay inside the panel.";
            if (column % 2 != 0) return "The HP bar frame's column has to be even.";
            return null;
        }

        /// <summary>Writes the column and row to every place the code keeps them, all in one write.</summary>
        public static void Write(int column, int row)
        {
            if (WhyNot(column, row) is string why) throw new InvalidDataException(why);
            RomInfo.PtPartyStripSites sites = RomLocator.PtPartyStripSites ?? throw new InvalidDataException("DSPRE can't find the HP bar strip's place in this ROM.");
            if (Read() == null) throw new InvalidDataException("The HP bar strip's code isn't the game's, so DSPRE leaves it alone.");
            string path = RomInfo.Arm9Path;
            byte[] arm9 = File.ReadAllBytes(path);
            int offset = 2 * (row * PanelWidth + column);
            ushort U(uint at) => BitConverter.ToUInt16(arm9, (int)at);
            void Put(uint at, ushort op) => BitConverter.GetBytes(op).CopyTo(arm9, (int)at);
            Put(sites.EggRow, ThumbImm3.With(U(sites.EggRow), row));
            Put(sites.EggRow + 0x12, ThumbImm3.With(U(sites.EggRow + 0x12), column));
            // A movs shifted left by 2 only holds multiples of 4, so the column has to be even.
            Put(sites.Getter, ThumbImm8.Make(ThumbImm8.Op.Movs, ThumbImm8.Register(U(sites.Getter)), (BackPanel + offset) >> 2));
            Put(sites.Getter + 2, ThumbLsls.With(U(sites.Getter + 2), 2));
            foreach (uint at in sites.Swap) Put(at, ThumbImm8.Make(ThumbImm8.Op.Adds, ThumbImm8.Register(U(at)), offset));
            uint first = new[] { sites.EggRow, sites.Getter }.Concat(sites.Swap).Min(), last = sites.Swap.Concat(new[] { sites.EggRow + 0x12, sites.Getter + 2 }).Max() + 2;
            DSUtils.WriteToFile(path, arm9.AsSpan((int)first, (int)(last - first)).ToArray(), first);
        }
    }
}

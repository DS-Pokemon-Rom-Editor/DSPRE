using System;
using System.IO;
using System.Text.RegularExpressions;
using NarcAPI;

namespace DSPRE.HgEngine
{
    /// <summary>hg-engine's learnsets narc (a/0/3/3) is one combined table, not the vanilla one-file-
    /// per-species stream <see cref="LearnsetData"/> expects. Splits it back into that format on sync.</summary>
    internal static class HgEngineLearnsets
    {
        private const int VanillaMoveBits = 9;   // matches LearnsetData.bitsMove
        private const int VanillaLevelBits = 7;  // matches LearnsetData.bitsLevel

        /// <summary>Rebuilds every species' vanilla-format learnset file from hg-engine's combined table.
        /// Species past the table's own row count get an empty file instead of a missing one.</summary>
        public static bool Sync(string learnsetNarcPath, string repoUnc, string unpackedDir, int totalSpeciesCount, out string error)
        {
            error = null;
            int maxLevelupMoves = ReadMaxLevelupMoves(repoUnc);
            if (maxLevelupMoves <= 0) { error = "Could not read MAX_LEVELUP_MOVES from the checkout's generated header."; return false; }

            Narc narc = Narc.Open(learnsetNarcPath);
            if (narc == null || narc.ElementCount == 0) { error = $"Failed to parse built narc: {learnsetNarcPath}"; return false; }
            byte[] table = narc.GetElementBytes(0);

            int rowBytes = maxLevelupMoves * 4;
            int rowCount = table.Length / rowBytes;

            if (Directory.Exists(unpackedDir)) Directory.Delete(unpackedDir, true);
            Directory.CreateDirectory(unpackedDir);

            for (int species = 0; species < totalSpeciesCount; species++)
            {
                byte[] outBytes = species < rowCount
                    ? ConvertRow(table, species * rowBytes, maxLevelupMoves)
                    : BitConverter.GetBytes((ushort)0xFFFF);
                File.WriteAllBytes(Path.Combine(unpackedDir, species.ToString("D4")), outBytes);
            }
            return true;
        }

        private static byte[] ConvertRow(byte[] table, int rowStart, int maxLevelupMoves)
        {
            using var mem = new MemoryStream();
            using var writer = new BinaryWriter(mem);

            for (int i = 0; i < maxLevelupMoves; i++)
            {
                int off = rowStart + i * 4;
                uint raw = BitConverter.ToUInt32(table, off);
                int moveId = (int)(raw & 0xFFFF);
                if (moveId == 0xFFFF) break;   // hg-engine's terminator: no more real entries in this row

                int level = (int)((raw >> 16) & 0xFFFF);
                // Clamp instead of throw: a move id past the 9-bit vanilla format shows wrong, not a crash.
                ushort entry = (ushort)((moveId & ((1 << VanillaMoveBits) - 1)) | ((level & ((1 << VanillaLevelBits) - 1)) << VanillaMoveBits));
                writer.Write(entry);
            }
            writer.Write((ushort)0xFFFF);
            return mem.ToArray();
        }

        private static int ReadMaxLevelupMoves(string repoUnc)
        {
            string path = Path.Combine(repoUnc, "include", "constants", "generated", "learnsets.h");
            if (!File.Exists(path)) return -1;
            var m = Regex.Match(File.ReadAllText(path), @"#define\s+MAX_LEVELUP_MOVES\s+(\d+)");
            return m.Success ? int.Parse(m.Groups[1].Value) : -1;
        }
    }
}

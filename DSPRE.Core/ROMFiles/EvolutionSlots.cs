using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// How many evolutions each Pokémon can have. The only reader of the evolution archive is GetMonEvolution
    /// (pokeplatinum, pokeheartgold and pokediamond pokemon.c): it allocates slots x 6 bytes (a `movs r1, #imm`),
    /// reads the species' file whole into it, and walks three slot loops (level, trade, item; each a `cmp rN, #imm`).
    /// Every file must be padded to the new size: a shorter file leaves heap garbage in the loops, a longer one
    /// overruns the buffer.
    /// </summary>
    public static class EvolutionSlots
    {
        public const int Vanilla = 7;
        public const int RecordSize = 6;

        /// <summary>The buffer size is an 8-bit immediate: 42 x 6 = 252.</summary>
        public const int Max = byte.MaxValue / RecordSize;

        private static readonly GameTable[] Loops = { GameTable.EvolutionSlotLoopLevel, GameTable.EvolutionSlotLoopTrade, GameTable.EvolutionSlotLoopItem };

        /// <summary>Why the slot count can't be read or set in this ROM, or null.</summary>
        public static string WhyNot()
        {
            if (isHGE) return "hg-engine sets its evolution slots in its own source.";
            if (SpotOf(GameTable.EvolutionSlotBuffer) == null) return "DSPRE knows where the evolution slot count is only in US Diamond, Platinum and HeartGold.";
            foreach (GameTable t in Loops.Prepend(GameTable.EvolutionSlotBuffer))
                if (GameTableFile.WhyNot(t, 2) is string why) return why;
            byte[] buffer = GameTableFile.Read(GameTable.EvolutionSlotBuffer, 2);
            // movs r1, #imm, then cmp rN, #imm in each loop; anything else means the code isn't the one DSPRE knows.
            bool known = buffer[1] == 0x21 && buffer[0] % RecordSize == 0
                && Loops.Select(t => GameTableFile.Read(t, 2)).All(c => c[1] >= 0x28 && c[1] <= 0x2F && c[0] * RecordSize == buffer[0]);
            return known ? null : "The code that reads evolutions has been changed, so DSPRE leaves it alone.";
        }

        /// <summary>This ROM's slots per Pokémon; the game's 7 where DSPRE can't read the code.</summary>
        public static int Current() => WhyNot() == null ? GameTableFile.Read(GameTable.EvolutionSlotBuffer, 1)[0] / RecordSize : Vanilla;

        /// <summary>The size every evolution file needs for <paramref name="slots"/>: the game's 44 bytes for 7.</summary>
        public static int FileSize(int slots) => (slots * RecordSize + 3) & ~3;

        public static bool Expanded => WhyNot() == null && Current() >= Max;

        /// <summary>
        /// Pads every evolution file to the new size, then sets the buffer and the three loops to
        /// <see cref="Max"/> slots. The unpacked files are copied to <paramref name="backupDir"/> first.
        /// </summary>
        public static void Expand(string backupDir)
        {
            if (WhyNot() is string why) throw new InvalidOperationException(why);
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.evolutions });
            string dir = gameDirs[DirNames.evolutions].unpackedDir;
            string[] files = Directory.GetFiles(dir);
            int size = FileSize(Max);
            string tooLong = files.FirstOrDefault(f => new FileInfo(f).Length > size);
            if (tooLong != null) throw new InvalidOperationException($"Evolution file {Path.GetFileName(tooLong)} is already longer than {size} bytes.");

            Directory.CreateDirectory(backupDir);
            foreach (string f in files) File.Copy(f, Path.Combine(backupDir, Path.GetFileName(f)), overwrite: true);
            foreach (string f in files)
            {
                byte[] data = File.ReadAllBytes(f);
                if (data.Length == size) continue;
                Array.Resize(ref data, size);
                File.WriteAllBytes(f, data);
            }
            GameTableFile.Write(GameTable.EvolutionSlotBuffer, new[] { (byte)(Max * RecordSize) });
            foreach (GameTable t in Loops) GameTableFile.Write(t, new[] { (byte)Max });
        }
    }
}

using System;
using System.Buffers.Binary;
using System.IO;

namespace DSPRE
{
    /// <summary>
    /// The HeartGold/SoulSilver phone book: a u32 entry count then fixed-size entries. A caller's trainer
    /// ID is what <see cref="PokegearRematchTable"/> is keyed by, so a row nobody can phone is unreachable.
    /// </summary>
    public static class PokegearPhoneBook
    {
        public const int HeaderSize = 4;
        public const int EntrySize = 20;
        public const int TrainerIdOffset = 4;

        public static bool IsSupported => RomInfo.gameFamily == RomInfo.GameFamilies.HGSS;

        public static string FilePath => Path.Combine(RomInfo.dataPath, "tel", "pmtel_book.dat");

        /// <summary>Trainer ID per entry, in entry order. Zero means an unused slot.</summary>
        public static bool TryReadTrainerIds(out ushort[] trainerIds, out string error)
        {
            trainerIds = Array.Empty<ushort>();
            error = null;

            if (!IsSupported)
            {
                error = "Only HeartGold and SoulSilver have a Pokégear phone book.";
                return false;
            }

            string path = FilePath;
            if (!File.Exists(path))
            {
                error = "The Pokégear phone book is missing from this project.";
                return false;
            }

            byte[] data;
            try { data = File.ReadAllBytes(path); }
            catch (Exception ex)
            {
                error = $"The Pokégear phone book couldn't be read: {ex.Message}";
                return false;
            }

            if (data.Length < HeaderSize)
            {
                error = "The Pokégear phone book header is truncated.";
                return false;
            }

            uint count = BinaryPrimitives.ReadUInt32LittleEndian(data);
            if (HeaderSize + (long)count * EntrySize > data.Length)
            {
                error = "The Pokégear phone book entries are truncated.";
                return false;
            }

            trainerIds = new ushort[count];
            for (int i = 0; i < count; i++)
            {
                trainerIds[i] = BinaryPrimitives.ReadUInt16LittleEndian(
                    data.AsSpan(HeaderSize + i * EntrySize + TrainerIdOffset, 2));
            }
            return true;
        }
    }
}

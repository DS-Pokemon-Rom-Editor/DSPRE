using System.Collections.Generic;

namespace DSPRE
{
    /// <summary>
    /// The HeartGold/SoulSilver rematch table, one row per trainer who can phone the player. The address
    /// comes from the overlay's own code and the row count from where its data ends.
    /// </summary>
    public static class PokegearRematchTable
    {
        public const int RowSize = RematchTable.RowSize;
        public const int RematchLevelCount = RematchTable.RematchLevelCount;
        public const ushort NoRematch = RematchTable.NoRematch;
        public const ushort ChainEnd = RematchTable.ChainEnd;

        public static bool IsSupported =>
            RomInfo.gameFamily == RomInfo.GameFamilies.HGSS &&
            RomInfo.pokegearRematchOverlayNumber >= 0;

        private static RematchTable.Descriptor Descriptor => new RematchTable.Descriptor
        {
            Name = "Pokégear rematch table",
            OverlayNumber = RomInfo.pokegearRematchOverlayNumber,
            FallbackOffset = RomInfo.pokegearRematchFallbackTableOffset,
            FixedRowCount = 0,
            DeriveOffset = true,
        };

        public static RematchTable.Location Resolve(out string error)
        {
            error = null;
            if (!IsSupported)
            {
                error = "Only HeartGold and SoulSilver have a Pokégear rematch table.";
                return null;
            }
            return RematchTable.Resolve(Descriptor, out error);
        }

        public static List<RematchTable.Row> ReadAll(out RematchTable.Location location, out string error)
        {
            location = Resolve(out error);
            return location == null ? new List<RematchTable.Row>() : RematchTable.ReadAll(location);
        }

        public static List<RematchTable.Row> ReadAll()
        {
            return ReadAll(out _, out _);
        }

        public static bool WriteRow(RematchTable.Location location, int rowIndex, RematchTable.Row row,
            out string error)
        {
            error = null;
            if (location == null)
            {
                location = Resolve(out error);
                if (location == null)
                {
                    error ??= "The Pokégear rematch table couldn't be located.";
                    return false;
                }
            }
            return RematchTable.WriteRow(location, rowIndex, row, out error);
        }
    }
}

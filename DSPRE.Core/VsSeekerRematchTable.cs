using System.Collections.Generic;

namespace DSPRE
{
    /// <summary>
    /// The Vs. Seeker rematch table (Diamond/Pearl/Platinum, English only): 240 fixed rows in overlay 5,
    /// sparse and keyed by the encounter trainer in slot 0 rather than by row index.
    /// </summary>
    public static class VsSeekerRematchTable
    {
        public const int RowCount = 240;
        public const int RowSize = RematchTable.RowSize;
        public const int RematchLevelCount = RematchTable.RematchLevelCount;
        public const ushort NoRematch = RematchTable.NoRematch;
        public const ushort ChainEnd = RematchTable.ChainEnd;

        public static bool IsSupported =>
            !RomInfo.isHGE &&
            RomInfo.gameLanguage == RomInfo.GameLanguages.English &&
            RomInfo.vsSeekerRematchOverlayNumber >= 0 &&
            (RomInfo.gameFamily == RomInfo.GameFamilies.Plat || RomInfo.gameFamily == RomInfo.GameFamilies.DP);

        private static RematchTable.Descriptor Descriptor => new RematchTable.Descriptor
        {
            Name = "Vs. Seeker rematch table",
            OverlayNumber = RomInfo.vsSeekerRematchOverlayNumber,
            FallbackOffset = RomInfo.vsSeekerRematchTableOffset,
            FixedRowCount = RowCount,
            DeriveOffset = false,
        };

        public static RematchTable.Location Resolve(out string error)
        {
            error = null;
            if (!IsSupported)
            {
                error = "The Vs. Seeker rematch table isn't supported for this game and language.";
                return null;
            }
            return RematchTable.Resolve(Descriptor, out error);
        }

        public static List<RematchTable.Row> ReadAll()
        {
            RematchTable.Location location = Resolve(out _);
            return location == null ? new List<RematchTable.Row>() : RematchTable.ReadAll(location);
        }

        public static bool WriteRow(int rowIndex, RematchTable.Row row, out string error)
        {
            RematchTable.Location location = Resolve(out error);
            if (location == null)
            {
                error ??= "The Vs. Seeker rematch table couldn't be located.";
                return false;
            }
            return RematchTable.WriteRow(location, rowIndex, row, out error);
        }
    }
}

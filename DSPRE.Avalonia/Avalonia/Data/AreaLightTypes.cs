using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// What an area data record's light type does, per game. Each light file is a table of time slots, each with four
    /// lights and the material colours; the material colours only reach the terrain where the game counts the area as
    /// outdoor. Platinum uses the value as a file of data/arealight.narc (0 and 3 count as outdoor; area_data.c). DP
    /// loads area01light for 0 and area00light for anything else (overlay 5). HeartGold and SoulSilver load area01light
    /// for 0, area00light for 1, the Olivine Lighthouse lamp room files for 2 and area00light for anything else
    /// (overlay 1); any nonzero value counts as outdoor there. Their data/arealight.narc is never read.
    /// </summary>
    public static class AreaLightTypes
    {
        public static readonly string[] PlatinumLabels = { "Outdoor day/night", "Indoor", "Cave", "Constant daylight" };
        public static readonly string[] DiamondPearlLabels = { "Indoor", "Outdoor day/night" };
        public static readonly string[] HeartGoldLabels = { "Indoor", "Outdoor day/night", "Lighthouse lamp room" };

        private static readonly string[] PlatinumNotes =
        {
            "Follows the clock", "Constant, terrain keeps its colours", "Dim cave light", "Constant midday light",
        };
        private static readonly string[] DiamondPearlNotes = { "Constant, terrain keeps its colours", "Follows the clock" };
        private static readonly string[] HeartGoldNotes =
        {
            "Constant, terrain keeps its colours", "Follows the clock", "Lamp off, lit after flag 0x96A",
        };

        /// <summary>The label category the open game's light names live in, so a project can rename them.</summary>
        public static string LabelKey => gameFamily switch
        {
            GameFamilies.Plat => "area_light_pt",
            GameFamilies.HGSS => "area_light_hgss",
            _ => "area_light_dp",
        };

        /// <summary>What a value does in the open game.</summary>
        public static string NoteFor(int value)
        {
            switch (gameFamily)
            {
                case GameFamilies.Plat:
                    return value >= 0 && value < PlatinumNotes.Length ? PlatinumNotes[value] : "Out of range, the game stops";
                case GameFamilies.HGSS:
                    return value >= 0 && value < HeartGoldNotes.Length ? HeartGoldNotes[value] : "Same as 1";
                default:
                    return value == 0 ? DiamondPearlNotes[0] : value == 1 ? DiamondPearlNotes[1] : "Same as 1";
            }
        }
    }
}

namespace DSPRE.ROMFiles
{
    /// <summary>The header the games park every matrix square nobody ever walks on.</summary>
    public static class FieldCatchAllHeader
    {
        /// <summary>More squares than any real header owns. </summary>
        public const int MostSquaresARealHeaderOwns = 32;

        /// <summary>Whether a header owning this many matrix squares is the catch-all rather than a place.</summary>
        public static bool IsCatchAll(int squaresOwned) => squaresOwned > MostSquaresARealHeaderOwns;

        /// <summary>What to tell somebody who opens it.</summary>
        public const string Explanation =
            "This header covers every part of the map nobody walks on, so there is nothing here to edit. "
            + "Pick a header for a real place to see its map and events.";
    }
}

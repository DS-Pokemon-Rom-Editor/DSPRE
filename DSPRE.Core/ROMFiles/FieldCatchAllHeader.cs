namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The header the games park every matrix square nobody ever walks on.
    ///
    /// HeartGold calls header 0 "EVERYWHERE". It owns 291 of its matrix's squares, and it is the only
    /// header in the game that owns more than a handful: every header somebody can actually stand in
    /// owns six squares or fewer. Stitching all 291 together is a lot of work for a place that has
    /// nothing in it to edit, so the editors say so instead.
    /// </summary>
    public static class FieldCatchAllHeader
    {
        /// <summary>
        /// More squares than any real header owns. The largest ordinary HeartGold header owns six, so
        /// this leaves a wide gap rather than sitting right on the edge of what is normal.
        /// </summary>
        public const int MostSquaresARealHeaderOwns = 32;

        /// <summary>Whether a header owning this many matrix squares is the catch-all rather than a place.</summary>
        public static bool IsCatchAll(int squaresOwned) => squaresOwned > MostSquaresARealHeaderOwns;

        /// <summary>What to tell somebody who opens it.</summary>
        public const string Explanation =
            "This header covers every part of the map nobody walks on, so there is nothing here to edit. "
            + "Pick a header for a real place to see its map and events.";
    }
}

namespace DSPRE.ROMFiles
{
    /// <summary>The header the games give every matrix square nobody walks on.</summary>
    public static class FieldCatchAllHeader
    {
        /// <summary>More squares than any real header owns. </summary>
        public const int MostSquaresARealHeaderOwns = 32;

        /// <summary>Whether a header owning this many matrix squares is the catch-all, not a real place.</summary>
        public static bool IsCatchAll(int squaresOwned) => squaresOwned > MostSquaresARealHeaderOwns;

        /// <summary>What to tell somebody who opens it.</summary>
        public const string Explanation =
            "Unused map squares, nothing to edit";
    }
}

using static DSPRE.RomInfo;

namespace DSPRE
{
    /// <summary>File indices of one trainer class: two per class on Diamond and Pearl, five on later games.</summary>
    public static class TrainerGraphicsLayout
    {
        public static int Stride => gameFamily == GameFamilies.DP ? 2 : 5;

        public static bool HasCells => gameFamily != GameFamilies.DP;

        public static bool PixelsAreScrambled => gameFamily == GameFamilies.DP;

        public static int DrawingEntry(int trClassID) => trClassID * Stride;

        public static int ColoursEntry(int trClassID) => trClassID * Stride + 1;

        /// <summary>-1 where the class has no cells.</summary>
        public static int CellsEntry(int trClassID) => HasCells ? trClassID * Stride + 2 : -1;

        /// <summary>-1 where the class has no animation.</summary>
        public static int AnimationEntry(int trClassID) => HasCells ? trClassID * Stride + 3 : -1;

        /// <summary>The plain two-frame copy some screens draw instead of the cells; -1 where there is none.</summary>
        public static int ScanEntry(int trClassID) => HasCells ? trClassID * Stride + 4 : -1;

        /// <summary>HGSS keys its trainer scans from the last word like DP; Platinum from the first.</summary>
        public static bool ScanScrambledFromEnd => gameFamily != GameFamilies.Plat;

        public static int ClassOf(int fileIndex) => fileIndex / Stride;
    }
}

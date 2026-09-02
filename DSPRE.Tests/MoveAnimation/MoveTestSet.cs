namespace DSPRE.Tests
{
    /// <summary>The moves to record, in the order to record them.</summary>
    internal static class MoveTestSet
    {
        /// <summary>Covers every opcode and every drawing path, in both games.</summary>
        public static readonly int[] OpcodeCover =
        {
            143, 232, 352, 151, 272, 45, 55, 16, 57, 59, 69, 224, 225, 311, 475, 192, 226,
        };

        /// <summary>The rest: operator settings, then the routines only one or two moves call.</summary>
        public static readonly int[] Remainder =
        {
            145, 464, 50, 61, 245, 18, 56, 60, 70, 95, 101, 131, 161, 194, 217, 246, 252, 304, 308, 320,
            406, 0, 19, 27, 35, 64, 65, 74, 89, 91, 93, 96, 102, 104, 107, 109, 144, 148, 150, 165, 171,
            180, 185, 204, 207, 216, 222, 230, 233, 255, 262, 276, 289, 293, 307, 322, 325, 326, 330, 339,
        };

        /// <summary>Every move to record, in order.</summary>
        public static int[] InOrder()
        {
            var all = new int[OpcodeCover.Length + Remainder.Length];
            OpcodeCover.CopyTo(all, 0);
            Remainder.CopyTo(all, OpcodeCover.Length);
            return all;
        }
    }
}

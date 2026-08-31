namespace DSPRE.Tests
{
    /// <summary>
    /// The moves to record, in the order to record them.
    ///
    /// Picked by covering set rather than by taste. Every opcode, every operator setting, every support
    /// routine and every drawing path counts as a mechanism, and a mechanism is counted per game, because
    /// 426 of the 501 scripts differ between HeartGold and Platinum so covering one says nothing about the
    /// other. That gives 389 things to cover, and these 77 moves cover all of them; 76 mechanisms are used
    /// by exactly one move, which is why the number is not smaller.
    ///
    /// The order matters. The first 17 cover every opcode and every drawing path between them, so the
    /// front of the list is the quickest route to an error that affects many moves at once. The rest fill
    /// in the operator settings and the long tail of routines that only one or two moves ever call.
    ///
    /// <see cref="MoveCoverageTests"/> re-derives the cover from the ROMs and fails if this list stops
    /// covering everything, so it cannot silently rot.
    /// </summary>
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

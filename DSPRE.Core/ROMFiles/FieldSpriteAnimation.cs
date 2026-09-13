namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Which picture of an overworld's sprite bank to show. A person's bank holds four pictures for each way of
    /// facing, up, down, left then right, in the order standing, one foot, standing, the other foot; the hero's
    /// adds sixteen running pictures after the walk; a pair Pokémon's holds two for each facing.
    /// </summary>
    public static class FieldSpriteAnimation
    {
        /// <summary>The first sixteen pictures of the hero's bank are the walk; the rest is the run.</summary>
        public const int WalkingPictures = 16;

        /// <summary>How long a pair Pokémon holds each of its pictures, in frames.</summary>
        public const int FramesPerPairPicture = 10;

        /// <summary>How many pictures a bank keeps for each way of facing.</summary>
        public static int PerFacing(int frameCount)
        {
            if (frameCount <= 0) return 0;
            if (frameCount < 8) return 1;         // a bank too small to hold a walk; the one picture is all there is
            return frameCount == 8 ? 2 : 4;
        }

        /// <summary>Which picture of the bank to show for this facing, with <paramref name="cycle"/> null for standing still.</summary>
        public static int PictureFor(int frameCount, int facing, FieldWalkCycle cycle)
        {
            int per = PerFacing(frameCount);
            if (per <= 0) return 0;
            if (facing < 0 || facing > 3) facing = 0;

            int start = facing * per;
            if (start + per > frameCount) return 0;      // an odd bank; stay on something that exists
            if (per == 1) return start;

            if (per == 2)
            {
                // Down starts half way through its loop.
                int t = ((cycle?.PairFrame ?? 0) + (facing == 1 ? FramesPerPairPicture / 2 : 0)) % (FramesPerPairPicture * 2);
                return start + t / FramesPerPairPicture;
            }

            if (cycle != null && cycle.Running && frameCount >= WalkingPictures * 2)
                return WalkingPictures + start + cycle.RunFrame / 4;
            return start + (cycle?.Frame ?? 0) / 4;
        }
    }
}

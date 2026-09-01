using System;

namespace DSPRE.ROMFiles
{
    /// <summary>Which picture out of an overworld's sprite bank to show while it walks.</summary>
    public static class FieldSpriteAnimation
    {
        /// <summary>How long a person holds each picture, in frames.</summary>
        public const int FramesPerPicture = 4;

        /// <summary>How long a following Pokemon holds each picture, in frames.</summary>
        public const int FramesPerPictureFollowing = 10;

        /// <summary>The first sixteen pictures of the hero's bank are the walk; the rest is the run.</summary>
        public const int WalkingPictures = 16;

        /// <summary>How many pictures a bank keeps for each way of facing.</summary>
        public static int PerFacing(int frameCount)
        {
            if (frameCount <= 0) return 0;
            if (frameCount < 8) return 1;         // a bank too small to hold a walk; the one picture is all there is
            return frameCount == 8 ? 2 : 4;
        }

        /// <summary>Which picture of the bank to show. </summary>
        public static int PictureFor(int frameCount, int facing, int cell, bool moving)
        {
            int per = PerFacing(frameCount);
            if (per <= 0) return 0;
            if (facing < 0 || facing > 3) facing = 0;

            int start = facing * per;
            if (start + per > frameCount) return 0;      // an odd bank; stay on something that exists
            if (!moving || per == 1) return start;

            int hold = per == 2 ? FramesPerPictureFollowing : FramesPerPicture;
            if (cell < 0) cell = 0;
            return start + (cell / hold) % per;
        }
    }
}

using System;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Which picture out of an overworld's sprite bank to show while it walks.
    ///
    /// A bank holds one group of pictures for each way of facing, in the order up, down, left, right.
    /// People get four to a group: the first and third are the same standing picture, the second and
    /// fourth are the left and right steps. Following Pokemon get two, a standing one and a step.
    /// The hero's bank has sixteen more pictures after those, which are the running set.
    ///
    /// The pace comes from fieldobj_drawdata.c. DATA_FieldOBJ_BlActAnmTbl_Normal0 (line 1473) gives a
    /// person four looping animations of sixteen steps each, one per facing, so with four pictures in
    /// a group each picture is held for four steps. DATA_FieldOBJ_BlActAnmTbl_PairPoke (line 1584)
    /// gives a following Pokemon four loops of twenty over two pictures, so ten steps each.
    ///
    /// A walk of one tile lasts eight frames, so a person gets through two pictures per tile: standing,
    /// then a step, and the step alternates feet on the tile after. That is what makes a walk read as
    /// walking.
    /// </summary>
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

        /// <summary>
        /// Which picture of the bank to show. <paramref name="facing"/> is 0 up, 1 down, 2 left, 3 right,
        /// and <paramref name="cell"/> counts frames spent moving. Standing still shows the first picture
        /// of the group.
        /// </summary>
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

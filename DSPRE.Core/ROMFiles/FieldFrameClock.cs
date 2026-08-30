using System;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Turns real elapsed time into whole field frames.
    ///
    /// Windows only wakes a timer every 15.6ms, so a timer asked for 33.3ms actually fires at 31.25 or
    /// 46.875. Rounding each of those to a whole number of frames on its own throws away the part left
    /// over, which ran the preview at roughly seven tenths of proper speed. Keeping the remainder and
    /// paying it out on a later tick makes the preview keep time with the real thing no matter how
    /// raggedly the timer fires.
    /// </summary>
    public sealed class FieldFrameClock
    {
        /// <summary>The field runs at thirty frames a second.</summary>
        public const double FramesPerSecond = 30.0;

        /// <summary>The most frames one tick will ever hand out, so coming back from a pause does not fast-forward.</summary>
        public const int MostFramesAtOnce = 30;

        private double _owed;

        /// <summary>How much of a frame is waiting to be paid out. Only useful for checking the clock itself.</summary>
        public double Owed => _owed;

        /// <summary>Forgets any part-frame, for a pause or a restart.</summary>
        public void Reset() => _owed = 0;

        /// <summary>How many whole frames have come due after this much real time at this speed.</summary>
        public int Tick(double seconds, double speed = 1.0)
        {
            if (seconds <= 0 || speed <= 0) return 0;

            _owed += seconds * FramesPerSecond * speed;
            if (_owed > MostFramesAtOnce) _owed = MostFramesAtOnce;

            int frames = (int)_owed;
            _owed -= frames;
            return frames;
        }
    }
}

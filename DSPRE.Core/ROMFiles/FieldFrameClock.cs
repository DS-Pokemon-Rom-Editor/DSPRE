using System;

namespace DSPRE.ROMFiles
{
    /// <summary>Turns real elapsed time into whole field frames.</summary>
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

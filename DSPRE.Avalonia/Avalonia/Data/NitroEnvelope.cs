using System;

namespace DSPRE.Avalonia.Data
{
    /// <summary>Turns an SBNK region's envelope bytes into the fixed shapes a SoundFont can hold. Export only;
    /// playback steps the envelope in <see cref="NitroSoundDriver"/>.</summary>
    public static class NitroEnvelope
    {
        /// <summary>Seconds between two runs of the sound driver.</summary>
        public const double TickSeconds = NitroSoundTables.UpdateSeconds;

        public readonly struct Shape
        {
            public readonly double AttackRate, DecaySeconds, ReleaseSeconds, SustainLevel;
            public Shape(double attackRate, double d, double r, double s) { AttackRate = attackRate; DecaySeconds = d; ReleaseSeconds = r; SustainLevel = s; }
        }

        /// <summary>A 0-127 level as linear gain, through the driver's squared attenuation table.</summary>
        public static double LevelToGain(int level) =>
            level >= 0x7F ? 1.0 : level <= 0 ? 0.0 : Math.Pow(10.0, NitroSoundTables.CalcDecibelSquare(level) / 10.0 / 20.0);

        public static Shape Compute(int attack, int decay, int sustain, int release)
        {
            int realAttack = NitroSoundTables.AttackRate(attack);
            double sustainLevel = LevelToGain(sustain);

            // Decay and release remove a fixed attenuation per update from 72.3 dB down.
            const long fullRange = -(long)NitroSoundTables.VolumeDbMin << 7;
            double decaySeconds = decay >= 0x7F ? 0.001 : (fullRange / NitroSoundTables.FallRate(decay)) * TickSeconds;
            double releaseSeconds = (fullRange / NitroSoundTables.FallRate(release)) * TickSeconds;

            return new Shape(realAttack, decaySeconds, releaseSeconds, sustainLevel);
        }

        /// <summary>Attack gain after <paramref name="elapsedTicks"/> updates; each scales the remaining attenuation by rate/256.</summary>
        public static double AttackGain(double attackRate, double elapsedTicks)
        {
            if (elapsedTicks <= 0) return 0.0;
            double x0 = -NitroSoundTables.VolumeDbMin << 7;
            double ratio = attackRate / 256.0;
            if (ratio <= 0) return 1.0;
            double x = x0 * Math.Pow(ratio, elapsedTicks);
            return Math.Pow(10.0, -(x / 128.0) / 10.0 / 20.0);
        }
    }
}

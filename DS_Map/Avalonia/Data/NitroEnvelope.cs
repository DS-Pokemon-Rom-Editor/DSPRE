using System;

namespace DSPRE.Avalonia.Data
{
    /// <summary>Converts an SBNK region's raw attack/decay/sustain/release bytes (the Nitro SDK's own
    /// <c>SNDInstParam</c> rate/level encoding: attack/decay/release are 0-127 "how fast", not milliseconds,
    /// and sustain is a 0-127 index into a squared-dB attenuation table, not a linear level) into a linear
    /// 0-1 gain a mixer can use directly.
    ///
    /// Every formula and table here matches Nintendo's own ARM7 mixer source (<c>snd_exchannel.c</c>'s
    /// <c>SND_UpdateExChannelEnvelope</c>/<c>SND_SetExChannelAttack</c>/<c>CalcRelease</c>,
    /// <c>snd_util.c</c>'s <c>SNDi_DecibelSquareTable</c>), not a third-party reimplementation.</summary>
    public static class NitroEnvelope
    {
        // The NDS sound engine ticks its envelope (and, at the same rate, its modulation LFO/sweep-pitch state)
        // every 64 sound-hardware timer intervals; each interval is 2728 ARM7 clock cycles at the ARM7's
        // ~33.514MHz clock: (2728*64)/33513982 seconds between ticks. Public: SseqPlayer's vibrato/sweep math
        // shares this same tick rate, since `SND_SeqMain`/`SND_ExChannelMain` are both driven by the same
        // `SND_WaitForIntervalTimer` message.
        public const double TickSeconds = (2728.0 * 64.0) / 33513982.0;

        // SNDi_DecibelSquareTable (snd_util.c), byte-exact, used directly (no /10 scaling in the real source;
        // the "tenths of a dB" framing is this project's own bookkeeping choice from when the table was only
        // known via VGMTrans, kept for readability, not a hardware fact).
        private static readonly short[] SustainDeciBel =
        {
            -32768, -722, -721, -651, -601, -562, -530, -503, -480, -460, -442, -425, -410, -396, -383, -371,
            -360, -349, -339, -330, -321, -313, -305, -297, -289, -282, -276, -269, -263, -257, -251, -245,
            -239, -234, -229, -224, -219, -214, -210, -205, -201, -196, -192, -188, -184, -180, -176, -173,
            -169, -165, -162, -158, -155, -152, -149, -145, -142, -139, -136, -133, -130, -127, -125, -122,
            -119, -116, -114, -111, -109, -106, -103, -101, -99,  -96,  -94,  -91,  -89,  -87,  -85,  -82,
            -80,  -78,  -76,  -74,  -72,  -70,  -68,  -66,  -64,  -62,  -60,  -58,  -56,  -54,  -52,  -50,
            -49,  -47,  -45,  -43,  -42,  -40,  -38,  -36,  -35,  -33,  -31,  -30,  -28,  -27,  -25,  -23,
            -22,  -20,  -19,  -17,  -16,  -14,  -13,  -11,  -10,  -8,   -7,   -6,   -4,   -3,   -1,   0,
        };

        // SND_SetExChannelAttack's attack_table (snd_exchannel.c), byte-exact.
        private static readonly byte[] AttackTimeTable =
        {
            0x00, 0x01, 0x05, 0x0E, 0x1A, 0x26, 0x33, 0x3F, 0x49, 0x54, 0x5C, 0x64, 0x6D, 0x74, 0x7B, 0x7F, 0x84, 0x89, 0x8F,
        };

        // CalcRelease (snd_exchannel.c): the falling-rate curve shared by decay and release, a per-tick
        // multiplicative rate derived from the raw 0-127 byte, with two special-cased extremes (127 = as fast
        // as representable, 126 = one fixed fast step) and a reciprocal-shaped ramp in between.
        private static int FallingRate(int raw)
        {
            if (raw == 0x7F) return 0xFFFF;
            if (raw == 0x7E) return 0x3C00;
            if (raw < 0x32) return ((raw * 2) + 1) & 0xFFFF;
            int denom = 0x7E - raw;
            return denom == 0 ? 0xFFFF : (0x1E00 / denom) & 0xFFFF;
        }

        public readonly struct Shape
        {
            public readonly double AttackRate, DecaySeconds, ReleaseSeconds, SustainLevel;
            public Shape(double attackRate, double d, double r, double s) { AttackRate = attackRate; DecaySeconds = d; ReleaseSeconds = r; SustainLevel = s; }
        }

        /// <summary>Converts a 0-127 "level" value (note velocity, track/expression/master volume, every
        /// 0-127 register in this format) to a linear gain, via the same squared-dB table SBNK sustain
        /// level uses: the same mixer reads all of these as one kind of attenuation register, so they
        /// share one curve rather than each getting its own linear approximation.</summary>
        public static double LevelToGain(int level) => level >= 0x7F ? 1.0 : level <= 0 ? 0.0 : Math.Pow(10.0, SustainDeciBel[Math.Clamp(level, 0, 127)] / 10.0 / 20.0);

        public static Shape Compute(int attack, int decay, int sustain, int release)
        {
            // ch_p->attack, exactly as SND_SetExChannelAttack computes it. Exposed as AttackRate (not a
            // duration) so the mixer can run the REAL per-tick recurrence directly (see AttackGain below)
            // rather than approximate it as a fixed-duration linear ramp.
            int realAttack = attack >= 0x6D ? AttackTimeTable[0x7F - attack] : 0xFF - attack;

            double sustainLevel = LevelToGain(sustain);

            const long attackRef = 0x16980;   // -SND_EX_CHANNEL_ENVDECAY_INIT: |SND_VOLUME_DB_MIN(-72.3dB) << 7|
            double decaySeconds = decay >= 0x7F ? 0.001 : (attackRef / FallingRate(decay)) * TickSeconds;
            double releaseSeconds = (attackRef / FallingRate(release)) * TickSeconds;

            return new Shape(realAttack, decaySeconds, releaseSeconds, sustainLevel);
        }

        /// <summary>The real attack curve: the continuous-time equivalent of the hardware's own per-tick
        /// recurrence (`SND_UpdateExChannelEnvelope`'s <c>ATTACK</c> case: <c>x(tick+1) = x(tick)*attackRate/256</c>,
        /// starting at <c>x(0) = 92544</c> = |SND_EX_CHANNEL_ENVDECAY_INIT|, in dB-tenths-times-128 units), not
        /// a linear ramp approximation. Returns linear gain (0-1) given elapsed TICKS (not samples/seconds)
        /// since note-on.</summary>
        public static double AttackGain(double attackRate, double elapsedTicks)
        {
            if (elapsedTicks <= 0) return 0.0;
            const double x0 = 92544.0;
            double ratio = attackRate / 256.0;
            if (ratio <= 0) return 1.0;   // an attack rate of 0 collapses to full volume in a single tick
            double x = x0 * Math.Pow(ratio, elapsedTicks);
            // x(tick)/128 = dB-tenths below full; convert straight to linear gain.
            return Math.Pow(10.0, -(x / 128.0) / 10.0 / 20.0);
        }
    }
}

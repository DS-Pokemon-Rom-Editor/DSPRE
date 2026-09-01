using System;
using System.Collections.Concurrent;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// The sound of the DS's own tone generators, written out as a short looping clip so the ordinary note
    /// machinery can play it like any other sample.
    ///
    /// A square wave is one cycle of 256 points, high for as much of it as the duty setting asks for. The
    /// clip is given a rate of 256 times the frequency of its own base note, so playing it at that note
    /// comes out at that frequency and every other note lands where it should. Noise is the console's
    /// fifteen-bit shift register written out in full, one cycle of it, clocked eight times per wave cycle
    /// the way the square's eight-step counter is.
    ///
    /// The duty steps and the shift register are how the hardware is documented to behave; they are not
    /// from any leaked source. What is from source is that this is used at all: snd_system.c:1292 keeps a
    /// PSG-only bank loaded so the Game Boy music can play, and without this those tracks are silent.
    /// </summary>
    public static class PsgWaveform
    {
        private const int PointsPerCycle = 256;

        /// <summary>How much of a cycle each duty setting spends high, in eighths. Setting 7 is silent and
        /// HeartGold's own Game Boy bank never uses it (checked across all 128 of its programs).</summary>
        private static readonly int[] HighEighths = { 1, 2, 3, 4, 5, 6, 7, 0 };

        private static readonly ConcurrentDictionary<int, SwavSample> _cache = new ConcurrentDictionary<int, SwavSample>();

        /// <summary>The clip for a region that has no recording of its own, or null if it has one.</summary>
        public static SwavSample For(SbnkRegion region)
        {
            if (region == null || region.Psg == PsgKind.None) return null;
            int key = ((int)region.Psg << 16) | ((region.PsgDuty & 7) << 8) | (region.BaseNote & 0xFF);
            return _cache.GetOrAdd(key, _ => Build(region.Psg, region.PsgDuty & 7, region.BaseNote));
        }

        private static SwavSample Build(PsgKind kind, int duty, int baseNote)
        {
            // The frequency the base note itself sounds at, on the usual A above middle C at 440 Hz.
            double baseHz = 440.0 * Math.Pow(2.0, (baseNote - 69) / 12.0);

            if (kind == PsgKind.Square)
            {
                int high = HighEighths[duty] * (PointsPerCycle / 8);
                var pcm = new short[PointsPerCycle];
                for (int i = 0; i < PointsPerCycle; i++) pcm[i] = i < high ? (short)10000 : (short)-10000;
                return new SwavSample
                {
                    Pcm = pcm,
                    SampleRate = (int)Math.Round(baseHz * PointsPerCycle),
                    Loop = true,
                    LoopStartSample = 0,
                };
            }

            // One full turn of the shift register: fifteen bits, so 32767 steps before it repeats.
            const int Steps = 32767;
            var noise = new short[Steps];
            int lfsr = 0x7FFF;
            for (int i = 0; i < Steps; i++)
            {
                bool carry = (lfsr & 1) != 0;
                lfsr >>= 1;
                if (carry) lfsr ^= 0x6000;
                noise[i] = carry ? (short)-10000 : (short)10000;
            }
            return new SwavSample
            {
                Pcm = noise,
                SampleRate = (int)Math.Round(baseHz * 8.0),
                Loop = true,
                LoopStartSample = 0,
            };
        }
    }
}

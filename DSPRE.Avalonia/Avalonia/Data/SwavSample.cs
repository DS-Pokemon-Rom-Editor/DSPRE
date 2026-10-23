using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>One decoded wave sample from a wave archive (SWAR): 16-bit PCM at <see cref="SampleRate"/>,
    /// plus an optional loop point (in samples).</summary>
    public sealed class SwavSample
    {
        public int SampleRate;

        /// <summary>The channel timer for the wave's own note, or 0 when it didn't come from a ROM.</summary>
        public int Timer;
        public bool Loop;
        public int LoopStartSample;   // sample index the clip loops back to (only meaningful if Loop)
        public short[] Pcm;           // mono 16-bit samples

        /// <summary>How the game stores it: 0 PCM8, 1 PCM16, 2 IMA-ADPCM. A replacement goes back in the same form; below 0 it
        /// isn't from a ROM and the writer decides.</summary>
        public int Encoding = -1;

        /// <summary>The wave as it was in the file, so an untouched sample is written back as it was.</summary>
        public byte[] Raw;

        /// <summary>
        /// Parses a SWAR into its waves, decoding PCM8, PCM16 and IMA-ADPCM to 16-bit PCM. A DATA block holds a count, then
        /// offsets to SWAV records: wave type, loop flag, sample rate, loop point, length, then the samples.
        /// </summary>
        public static System.Collections.Generic.List<SwavSample> ParseArchive(byte[] d)
        {
            List<SwavSample> list = new System.Collections.Generic.List<SwavSample>();
            if (d == null || d.Length < 16 + 8 + 4) return list;

            uint U32(int o) => (uint)(d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24));
            string Sig4(int o) => System.Text.Encoding.ASCII.GetString(d, o, 4);

            if (Sig4(0) != "SWAR") return list;

            const int blockStart = 16;   // right after the 16-byte file header
            if (Sig4(blockStart) != "DATA") return list;
            // A 32-byte reserved area, zero on disk, sits between the block header and the wave count, as in SNDWaveArc.
            const int reservedSize = 32;
            int count = (int)U32(blockStart + 8 + reservedSize);

            for (int i = 0; i < count; i++)
            {
                int entryAt = blockStart + 8 + reservedSize + 4 + i * 4;
                if (entryAt + 4 > d.Length) break;
                int relOff = (int)U32(entryAt);
                if (relOff == 0) { list.Add(null); continue; }
                // Relative to the SWAR's byte 0, not the DATA block, as in an SBNK's instrument table.
                int at = relOff;
                SwavSample wav = ParseOne(d, at);
                if (wav != null)
                {
                    // Up to where the next wave starts, or the end of the archive for the last one.
                    int ends = d.Length;
                    for (int j = 0; j < count; j++)
                    {
                        int other = blockStart + 8 + reservedSize + 4 + j * 4;
                        if (other + 4 > d.Length) break;
                        int off = (int)U32(other);
                        if (off > at && off < ends) ends = off;
                    }
                    if (ends > at && ends <= d.Length)
                    {
                        wav.Raw = new byte[ends - at];
                        System.Buffer.BlockCopy(d, at, wav.Raw, 0, ends - at);
                    }
                }
                list.Add(wav);
            }
            return list;
        }

        private static SwavSample ParseOne(byte[] d, int at)
        {
            if (at + 16 > d.Length) return null;
            int U16(int o) => d[o] | (d[o + 1] << 8);
            uint U32(int o) => (uint)(d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24));

            int waveType = d[at];
            bool loop = d[at + 1] != 0;
            int sampleRate = U16(at + 2);
            // The hardware plays the wave by this clock divisor, not by the rounded rate beside it.
            int timer = U16(at + 4);
            int loopOffsetWords = U16(at + 6);
            long nonLoopLenWords = U32(at + 8);   // long: a broken record can hold a huge value
            int dataAt = at + 12;
            if (dataAt > d.Length) return null;

            long totalWordsWide = loopOffsetWords + nonLoopLenWords;
            // Clamp to the data that's there: a bad offset can give a huge length that overflows the byte count below.
            long maxWordsFromFileSize = (d.Length - dataAt) / 4 + 1;
            int totalWords = (int)System.Math.Clamp(totalWordsWide, 0, maxWordsFromFileSize);

            switch (waveType)
            {
                case 0:   // PCM8: a sample a byte, 4 samples a word.
                {
                    int sampleCount = totalWords * 4;
                    if (dataAt + sampleCount > d.Length) sampleCount = System.Math.Max(0, d.Length - dataAt);
                        short[] pcm = new short[sampleCount];
                    for (int i = 0; i < sampleCount; i++) pcm[i] = (short)((sbyte)d[dataAt + i] * 256);
                    return new SwavSample { Encoding = waveType, SampleRate = sampleRate, Timer = timer, Loop = loop, LoopStartSample = loopOffsetWords * 4, Pcm = pcm };
                }
                case 1:   // PCM16: a sample every 2 bytes, 2 samples a word.
                {
                    int sampleCount = totalWords * 2;
                    if (dataAt + sampleCount * 2 > d.Length) sampleCount = System.Math.Max(0, (d.Length - dataAt) / 2);
                        short[] pcm = new short[sampleCount];
                    for (int i = 0; i < sampleCount; i++) pcm[i] = (short)(d[dataAt + i * 2] | (d[dataAt + i * 2 + 1] << 8));
                    return new SwavSample { Encoding = waveType, SampleRate = sampleRate, Timer = timer, Loop = loop, LoopStartSample = loopOffsetWords * 2, Pcm = pcm };
                }
                case 2:   // IMA-ADPCM: 8 samples a word, but the first word is the decoder's header, so it doesn't count
                          // toward the loop start.
                {
                    int byteLen = totalWords * 4;
                    if (dataAt + byteLen > d.Length) byteLen = System.Math.Max(0, d.Length - dataAt);
                        short[] pcm = DecodeImaAdpcm(d, dataAt, byteLen);
                    int adpcmLoopStart = System.Math.Max(0, loopOffsetWords - 1) * 8;
                    return new SwavSample { Encoding = waveType, SampleRate = sampleRate, Timer = timer, Loop = loop, LoopStartSample = adpcmLoopStart, Pcm = pcm };
                }
                default:
                    return null;
            }
        }

        // Standard IMA-ADPCM step/index tables (the same public algorithm used by NDS SWAV/STRM ADPCM).
        private static readonly int[] StepTable =
        {
            7,8,9,10,11,12,13,14,16,17,19,21,23,25,28,31,34,37,41,45,50,55,60,66,73,80,88,97,107,118,130,143,
            157,173,190,209,230,253,279,307,337,371,408,449,494,544,598,658,724,796,876,963,1060,1166,1282,
            1411,1552,1707,1878,2066,2272,2499,2749,3024,3327,3660,4026,4428,4871,5358,5894,6484,7132,7845,
            8630,9493,10442,11487,12635,13899,15289,16818,18500,20350,22385,24623,27086,29794,32767
        };
        private static readonly int[] IndexTable = { -1, -1, -1, -1, 2, 4, 6, 8 };

        // DS ADPCM has one 4-byte header for the whole clip (s16 predictor, u8 step index, u8 reserved), then a nibble per
        // sample, low first; WAV IMA-ADPCM has a header per block.
        private static short[] DecodeImaAdpcm(byte[] d, int at, int byteLen)
        {
            if (byteLen < 4) return System.Array.Empty<short>();
            int predictor = (short)(d[at] | (d[at + 1] << 8));
            int stepIndex = System.Math.Clamp((int)d[at + 2], 0, StepTable.Length - 1);
            int nibbleCount = (byteLen - 4) * 2;
            short[] pcm = new short[nibbleCount];

            for (int i = 0; i < nibbleCount; i++)
            {
                byte b = d[at + 4 + i / 2];
                int nibble = (i % 2 == 0) ? (b & 0xF) : (b >> 4);

                int step = StepTable[stepIndex];
                int diff = step >> 3;
                if ((nibble & 1) != 0) diff += step >> 2;
                if ((nibble & 2) != 0) diff += step >> 1;
                if ((nibble & 4) != 0) diff += step;
                if ((nibble & 8) != 0) predictor -= diff; else predictor += diff;
                predictor = System.Math.Clamp(predictor, short.MinValue, short.MaxValue);

                stepIndex = System.Math.Clamp(stepIndex + IndexTable[nibble & 7], 0, StepTable.Length - 1);
                pcm[i] = (short)predictor;
            }
            return pcm;
        }
    }
}

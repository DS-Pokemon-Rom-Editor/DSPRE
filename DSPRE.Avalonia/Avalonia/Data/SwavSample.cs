namespace DSPRE.Avalonia.Data
{
    /// <summary>One decoded wave sample from a wave archive (SWAR): 16-bit PCM at <see cref="SampleRate"/>,
    /// plus an optional loop point (in samples).</summary>
    public sealed class SwavSample
    {
        public int SampleRate;
        public bool Loop;
        public int LoopStartSample;   // sample index the clip loops back to (only meaningful if Loop)
        public short[] Pcm;           // mono 16-bit samples

        /// <summary>
        /// Parses a SWAR wave archive (public Nitro sound format: a file header, one DATA block holding a count
        /// then an offset table, each entry a small SWAV record: wave type / loop flag / sample rate / loop point
        /// / length, followed by the raw sample data) into its individual waves, decoding PCM8/PCM16/IMA-ADPCM to
        /// 16-bit PCM. This is the standard wave-archive format shared by DS sound engines generally, not
        /// something specific to this game, so this decode follows the well-documented public format rather
        /// than the game's own source (unlike the SDAT container itself).
        /// </summary>
        public static System.Collections.Generic.List<SwavSample> ParseArchive(byte[] d)
        {
            var list = new System.Collections.Generic.List<SwavSample>();
            if (d == null || d.Length < 16 + 8 + 4) return list;

            int U16(int o) => d[o] | (d[o + 1] << 8);
            uint U32(int o) => (uint)(d[o] | (d[o + 1] << 8) | (d[o + 2] << 16) | (d[o + 3] << 24));
            string Sig4(int o) => System.Text.Encoding.ASCII.GetString(d, o, 4);

            if (Sig4(0) != "SWAR") return list;

            const int blockStart = 16;   // right after the 16-byte file header
            if (Sig4(blockStart) != "DATA") return list;
            // The on-disk block mirrors SNDWaveArc's in-memory layout: a 32-byte reserved area (topLink pointer +
            // reserved[7], zero-filled on disk since pointers don't exist there) sits between the block header
            // and the real wave count.
            const int reservedSize = 32;
            int count = (int)U32(blockStart + 8 + reservedSize);

            for (int i = 0; i < count; i++)
            {
                int entryAt = blockStart + 8 + reservedSize + 4 + i * 4;
                if (entryAt + 4 > d.Length) break;
                int relOff = (int)U32(entryAt);
                if (relOff == 0) { list.Add(null); continue; }
                // Relative to this SWAR sub-file's own byte 0, not the DATA block's start (same convention as
                // SBNK's instrument offset table).
                int at = relOff;
                var wav = ParseOne(d, at);
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
            // at+4: timer value (hardware clock divisor), not needed since we already have sampleRate directly.
            int loopOffsetWords = U16(at + 6);
            long nonLoopLenWords = U32(at + 8);   // widen: a malformed/misaligned record can hold a huge raw value
            int dataAt = at + 12;
            if (dataAt > d.Length) return null;

            long totalWordsWide = loopOffsetWords + nonLoopLenWords;
            // Clamp against the data actually available rather than trust the header: a wave type/loop/length
            // read from a bad offset can produce an arbitrarily large value that would otherwise overflow the
            // byte-count math below.
            long maxWordsFromFileSize = (d.Length - dataAt) / 4 + 1;
            int totalWords = (int)System.Math.Clamp(totalWordsWide, 0, maxWordsFromFileSize);

            switch (waveType)
            {
                case 0:   // PCM8: 1 sample/byte, 1 word (4 bytes) = 4 samples.
                {
                    int sampleCount = totalWords * 4;
                    if (dataAt + sampleCount > d.Length) sampleCount = System.Math.Max(0, d.Length - dataAt);
                    var pcm = new short[sampleCount];
                    for (int i = 0; i < sampleCount; i++) pcm[i] = (short)((sbyte)d[dataAt + i] * 256);
                    return new SwavSample { SampleRate = sampleRate, Loop = loop, LoopStartSample = loopOffsetWords * 4, Pcm = pcm };
                }
                case 1:   // PCM16: 1 sample/2 bytes, 1 word (4 bytes) = 2 samples.
                {
                    int sampleCount = totalWords * 2;
                    if (dataAt + sampleCount * 2 > d.Length) sampleCount = System.Math.Max(0, (d.Length - dataAt) / 2);
                    var pcm = new short[sampleCount];
                    for (int i = 0; i < sampleCount; i++) pcm[i] = (short)(d[dataAt + i * 2] | (d[dataAt + i * 2 + 1] << 8));
                    return new SwavSample { SampleRate = sampleRate, Loop = loop, LoopStartSample = loopOffsetWords * 2, Pcm = pcm };
                }
                case 2:   // IMA-ADPCM: 2 samples/byte (nibbles), 1 word (4 bytes) = 8 samples, but the first word
                          // is the one-time predictor/step header, consumed by the decoder and absent from the
                          // decoded PCM array, so it doesn't count towards the loop-start sample index.
                {
                    int byteLen = totalWords * 4;
                    if (dataAt + byteLen > d.Length) byteLen = System.Math.Max(0, d.Length - dataAt);
                    var pcm = DecodeImaAdpcm(d, dataAt, byteLen);
                    int adpcmLoopStart = System.Math.Max(0, loopOffsetWords - 1) * 8;
                    return new SwavSample { SampleRate = sampleRate, Loop = loop, LoopStartSample = adpcmLoopStart, Pcm = pcm };
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

        // NDS SWAV/STRM ADPCM: a single 4-byte header (s16 initial predictor, u8 step index, u8 reserved) for the
        // WHOLE clip, then one 4-bit nibble per sample (low nibble first), unlike classic WAV IMA-ADPCM's
        // per-block headers.
        private static short[] DecodeImaAdpcm(byte[] d, int at, int byteLen)
        {
            if (byteLen < 4) return System.Array.Empty<short>();
            int predictor = (short)(d[at] | (d[at + 1] << 8));
            int stepIndex = System.Math.Clamp((int)d[at + 2], 0, StepTable.Length - 1);
            int nibbleCount = (byteLen - 4) * 2;
            var pcm = new short[nibbleCount];

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

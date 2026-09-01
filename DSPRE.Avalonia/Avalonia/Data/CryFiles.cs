using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>Getting a Pokemon's cry out of the ROM and putting a new one back.</summary>
    public static class CryFiles
    {
        /// <summary>What a WAV has to be for this to read it.</summary>
        public const string AcceptedFormat =
            "WAV, uncompressed, 8 or 16 bits a sample, mono or stereo. Stereo is mixed down to one "
            + "channel and the sample rate is kept as it is.";

        // ── reading a WAV ────────────────────────────────────────────────────────────

        /// <summary>Reads a WAV into plain samples. </summary>
        public static short[] ReadWav(byte[] file, out int sampleRate, out string problem)
        {
            sampleRate = 0;
            problem = null;
            if (file == null || file.Length < 44) { problem = "That file is too small to be a WAV."; return null; }

            string Sig(int o) => System.Text.Encoding.ASCII.GetString(file, o, 4);
            int U16(int o) => file[o] | (file[o + 1] << 8);
            int U32(int o) => file[o] | (file[o + 1] << 8) | (file[o + 2] << 16) | (file[o + 3] << 24);

            if (Sig(0) != "RIFF" || Sig(8) != "WAVE") { problem = "That is not a WAV file."; return null; }

            int format = 0, channels = 0, bits = 0;
            int dataAt = -1, dataLen = 0;

            int at = 12;
            while (at + 8 <= file.Length)
            {
                string id = Sig(at);
                int size = U32(at + 4);
                if (size < 0 || at + 8 + size > file.Length) size = file.Length - at - 8;
                int body = at + 8;

                if (id == "fmt " && size >= 16)
                {
                    format = U16(body);
                    channels = U16(body + 2);
                    sampleRate = U32(body + 4);
                    bits = U16(body + 14);
                }
                else if (id == "data") { dataAt = body; dataLen = size; }

                at = body + size + (size & 1);          // a chunk is padded to an even length
            }

            if (dataAt < 0) { problem = "That WAV has no sound in it."; return null; }
            if (format != 1) { problem = "That WAV is compressed. Save it again as plain uncompressed WAV."; return null; }
            if (channels < 1 || channels > 2) { problem = $"That WAV has {channels} channels. Use mono or stereo."; return null; }
            if (bits != 8 && bits != 16) { problem = $"That WAV is {bits} bits a sample. Use 8 or 16."; return null; }
            if (sampleRate <= 0 || sampleRate > 65535) { problem = "That WAV's sample rate is outside what the sound hardware can hold."; return null; }

            int bytesPerSample = bits / 8;
            int frames = dataLen / (bytesPerSample * channels);
            if (frames <= 0) { problem = "That WAV has no sound in it."; return null; }

            var pcm = new short[frames];
            for (int i = 0; i < frames; i++)
            {
                int sum = 0;
                for (int c = 0; c < channels; c++)
                {
                    int o = dataAt + (i * channels + c) * bytesPerSample;
                    sum += bits == 8 ? (file[o] - 128) * 256 : (short)(file[o] | (file[o + 1] << 8));
                }
                pcm[i] = (short)Math.Clamp(sum / channels, short.MinValue, short.MaxValue);
            }
            return pcm;
        }

        // ── writing a WAV ────────────────────────────────────────────────────────────

        /// <summary>One channel of samples as an ordinary WAV.</summary>
        public static byte[] WriteWav(short[] pcm, int sampleRate)
        {
            pcm = pcm ?? Array.Empty<short>();
            if (sampleRate <= 0) sampleRate = 8000;
            int dataLen = pcm.Length * 2;
            var o = new byte[44 + dataLen];

            void Ascii(int at, string s) { for (int i = 0; i < 4; i++) o[at + i] = (byte)s[i]; }
            void U32(int at, int v) { o[at] = (byte)v; o[at + 1] = (byte)(v >> 8); o[at + 2] = (byte)(v >> 16); o[at + 3] = (byte)(v >> 24); }
            void U16(int at, int v) { o[at] = (byte)v; o[at + 1] = (byte)(v >> 8); }

            Ascii(0, "RIFF"); U32(4, 36 + dataLen); Ascii(8, "WAVE");
            Ascii(12, "fmt "); U32(16, 16); U16(20, 1); U16(22, 1);
            U32(24, sampleRate); U32(28, sampleRate * 2); U16(32, 2); U16(34, 16);
            Ascii(36, "data"); U32(40, dataLen);
            for (int i = 0; i < pcm.Length; i++) { o[44 + i * 2] = (byte)pcm[i]; o[44 + i * 2 + 1] = (byte)(pcm[i] >> 8); }
            return o;
        }

        // ── building a wave archive ──────────────────────────────────────────────────

        /// <summary>Wraps samples back up as a wave archive, uncompressed at sixteen bits. </summary>
        public static byte[] BuildArchive(IReadOnlyList<SwavSample> samples, bool squeeze = true)
        {
            const int header = 16, blockHeader = 8, reserved = 32;
            int count = samples?.Count ?? 0;

            int tableAt = header + blockHeader + reserved + 4;
            int firstWaveAt = tableAt + count * 4;

            var waves = new List<byte[]>(count);
            for (int i = 0; i < count; i++) waves.Add(BuildWave(samples[i], squeeze));

            int total = firstWaveAt;
            foreach (var w in waves) total += w.Length;

            var o = new byte[total];
            void Ascii(int at, string s) { for (int i = 0; i < s.Length; i++) o[at + i] = (byte)s[i]; }
            void U32(int at, int v) { o[at] = (byte)v; o[at + 1] = (byte)(v >> 8); o[at + 2] = (byte)(v >> 16); o[at + 3] = (byte)(v >> 24); }
            void U16(int at, int v) { o[at] = (byte)v; o[at + 1] = (byte)(v >> 8); }

            Ascii(0, "SWAR");
            U16(4, 0xFEFF); U16(6, 0x0100);       // byte order and version, as every Nitro file carries
            U32(8, total);
            U16(12, header); U16(14, 1);          // how long the header is, and that there is one block

            Ascii(header, "DATA");
            U32(header + 4, total - header);
            U32(header + blockHeader + reserved, count);

            int waveAt = firstWaveAt;
            for (int i = 0; i < waves.Count; i++)
            {
                U32(tableAt + i * 4, waveAt);
                Buffer.BlockCopy(waves[i], 0, o, waveAt, waves[i].Length);
                waveAt += waves[i].Length;
            }
            return o;
        }

        private static byte[] BuildWave(SwavSample s, bool squeeze)
        {
            // A sample that came out of the ROM and was not replaced goes back exactly as it was.
            if (s?.Raw != null && s.Raw.Length >= 12) return s.Raw;

            var pcm = s?.Pcm ?? Array.Empty<short>();
            int rate = s == null || s.SampleRate <= 0 ? 8000 : Math.Min(s.SampleRate, 65535);
            bool loop = s != null && s.Loop;

            byte[] body;
            int waveType, loopWords, totalWords;

            if (squeeze)
            {
                body = EncodeAdpcm(pcm);
                waveType = 2;
                totalWords = body.Length / 4;
                // The four-byte header counts as a word here, and the loop point is measured in words
                // past it, which is why the reader adds one back when it works the sample index out.
                loopWords = loop ? Math.Clamp(s.LoopStartSample / 8 + 1, 0, totalWords) : 0;
            }
            else
            {
                int words = (pcm.Length + 1) / 2;       // two samples to a word at sixteen bits
                body = new byte[words * 4];
                for (int i = 0; i < pcm.Length; i++) { body[i * 2] = (byte)pcm[i]; body[i * 2 + 1] = (byte)(pcm[i] >> 8); }
                waveType = 1;
                totalWords = words;
                loopWords = loop ? Math.Clamp(s.LoopStartSample / 2, 0, words) : 0;
            }

            var o = new byte[12 + body.Length];
            void U32(int at, int v) { o[at] = (byte)v; o[at + 1] = (byte)(v >> 8); o[at + 2] = (byte)(v >> 16); o[at + 3] = (byte)(v >> 24); }
            void U16(int at, int v) { o[at] = (byte)v; o[at + 1] = (byte)(v >> 8); }

            o[0] = (byte)waveType;
            o[1] = (byte)(loop ? 1 : 0);
            U16(2, rate);
            U16(4, (int)Math.Clamp(16756991.0 / rate, 0, 65535));   // the hardware's own clock divisor
            U16(6, loopWords);
            U32(8, totalWords - loopWords);

            Buffer.BlockCopy(body, 0, o, 12, body.Length);
            return o;
        }

        // The same step and index tables the reader uses, because an encoder has to walk the decoder
        // backwards to stay in step with it.
        private static readonly int[] StepTable =
        {
            7,8,9,10,11,12,13,14,16,17,19,21,23,25,28,31,34,37,41,45,50,55,60,66,73,80,88,97,107,118,130,143,
            157,173,190,209,230,253,279,307,337,371,408,449,494,544,598,658,724,796,876,963,1060,1166,1282,
            1411,1552,1707,1878,2066,2272,2499,2749,3024,3327,3660,4026,4428,4871,5358,5894,6484,7132,7845,
            8630,9493,10442,11487,12635,13899,15289,16818,18500,20350,22385,24623,27086,29794,32767
        };
        private static readonly int[] IndexTable = { -1, -1, -1, -1, 2, 4, 6, 8 };

        /// <summary>
        /// Squeezes samples down to four bits each, the way the games keep their cries: one small header
        /// holding where the sound starts and how big its steps are, then a nibble a sample.
        /// </summary>
        public static byte[] EncodeAdpcm(short[] pcm)
        {
            pcm = pcm ?? Array.Empty<short>();

            int predictor = 0;
            int stepIndex = 0;
            // Start with a step that suits how loud the sound opens, so the first samples are not chasing
            // the signal from a standing start.
            if (pcm.Length > 0)
            {
                int first = Math.Abs((int)pcm[0]);
                while (stepIndex < StepTable.Length - 1 && StepTable[stepIndex] < first / 8) stepIndex++;
            }

            int nibbles = pcm.Length;
            int bodyBytes = (nibbles + 1) / 2;
            // Everything is counted in four-byte words, so round the whole thing up to one.
            int total = 4 + bodyBytes;
            total = (total + 3) & ~3;
            var o = new byte[total];

            o[0] = (byte)predictor; o[1] = (byte)(predictor >> 8);
            o[2] = (byte)stepIndex; o[3] = 0;

            for (int i = 0; i < nibbles; i++)
            {
                int step = StepTable[stepIndex];
                int delta = pcm[i] - predictor;

                int nibble = 0;
                if (delta < 0) { nibble = 8; delta = -delta; }

                // Work out the four bits the same way the reader will take them apart.
                int diff = step >> 3;
                if (delta >= step) { nibble |= 4; delta -= step; diff += step; }
                if (delta >= (step >> 1)) { nibble |= 2; delta -= step >> 1; diff += step >> 1; }
                if (delta >= (step >> 2)) { nibble |= 1; diff += step >> 2; }

                if ((nibble & 8) != 0) predictor -= diff; else predictor += diff;
                predictor = Math.Clamp(predictor, short.MinValue, short.MaxValue);

                stepIndex = Math.Clamp(stepIndex + IndexTable[nibble & 7], 0, StepTable.Length - 1);

                if ((i & 1) == 0) o[4 + i / 2] = (byte)nibble;
                else o[4 + i / 2] |= (byte)(nibble << 4);
            }
            return o;
        }
    }
}

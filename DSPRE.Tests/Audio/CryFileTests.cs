using System;
using System.Linq;
using DSPRE.Avalonia.Data;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>Taking a cry out of a ROM and putting one back.</summary>
    public class CryFileTests
    {
        private static short[] Ramp(int n)
        {
            var p = new short[n];
            for (int i = 0; i < n; i++) p[i] = (short)(i * 37 - 12000);
            return p;
        }

        [Fact]
        public void AWavWrittenHereReadsBackTheSame()
        {
            var pcm = Ramp(500);
            var file = CryFiles.WriteWav(pcm, 10512);

            var back = CryFiles.ReadWav(file, out int rate, out string problem);

            Assert.Null(problem);
            Assert.Equal(10512, rate);
            Assert.Equal(pcm, back);
        }

        [Fact]
        public void AnEightBitWavIsReadAndScaledUp()
        {
            // Build a small 8-bit WAV by hand: 8-bit samples are unsigned with 128 as silence.
            var file = CryFiles.WriteWav(new short[] { 0 }, 8000).Take(44).ToArray();
            var eight = new byte[44 + 4];
            Array.Copy(file, eight, 44);
            eight[20] = 1; eight[21] = 0;                  // still plain PCM
            eight[34] = 8; eight[35] = 0;                  // eight bits a sample
            eight[32] = 1; eight[33] = 0;                  // one byte a frame
            eight[40] = 4; eight[41] = eight[42] = eight[43] = 0;
            eight[4] = (byte)(36 + 4);
            eight[44] = 128; eight[45] = 255; eight[46] = 0; eight[47] = 128;

            var back = CryFiles.ReadWav(eight, out int rate, out string problem);
            Assert.Null(problem);
            Assert.Equal(8000, rate);
            Assert.Equal(4, back.Length);
            Assert.Equal(0, back[0]);            // 128 is the middle, so silence
            Assert.True(back[1] > 30000);        // 255 is near the top
            Assert.True(back[2] < -30000);       // 0 is near the bottom
        }

        [Theory]
        [InlineData(new byte[] { 1, 2, 3 }, "too small")]
        public void SomethingTooSmallIsRefusedWithAReason(byte[] file, string expect)
        {
            Assert.Null(CryFiles.ReadWav(file, out _, out string problem));
            Assert.Contains(expect, problem, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SomethingThatIsNotAWavIsRefusedWithAReason()
        {
            var junk = new byte[64];
            for (int i = 0; i < junk.Length; i++) junk[i] = (byte)i;

            Assert.Null(CryFiles.ReadWav(junk, out _, out string problem));
            Assert.Contains("not a WAV", problem);
        }

        [Fact]
        public void NothingAtAllIsRefusedRatherThanThrowing()
        {
            Assert.Null(CryFiles.ReadWav(null, out _, out string problem));
            Assert.False(string.IsNullOrEmpty(problem));
        }

        [Fact]
        public void ACompressedWavIsRefusedAndSaysWhatToDo()
        {
            var file = CryFiles.WriteWav(Ramp(100), 8000);
            file[20] = 17;                                  // some codec that is not plain PCM

            Assert.Null(CryFiles.ReadWav(file, out _, out string problem));
            Assert.Contains("compressed", problem);
            Assert.Contains("uncompressed", problem);
        }

        [Fact]
        public void AnArchiveKeptUncompressedReadsBackExactly()
        {
            var sample = new SwavSample { SampleRate = 10512, Loop = false, LoopStartSample = 0, Pcm = Ramp(1000) };

            var archive = CryFiles.BuildArchive(new[] { sample }, squeeze: false);
            var back = SwavSample.ParseArchive(archive);

            var one = Assert.Single(back);
            Assert.Equal(10512, one.SampleRate);
            Assert.False(one.Loop);
            // The last sample can be padded to fill a whole word, so compare what was put in.
            Assert.True(one.Pcm.Length >= sample.Pcm.Length);
            for (int i = 0; i < sample.Pcm.Length; i++) Assert.Equal(sample.Pcm[i], one.Pcm[i]);
        }

        [Fact]
        public void AnOddNumberOfSamplesStillSurvivesUncompressed()
        {
            // Two samples share a word, so an odd count is the case that could lose the last one.
            var sample = new SwavSample { SampleRate = 8000, Pcm = Ramp(777) };
            var back = SwavSample.ParseArchive(CryFiles.BuildArchive(new[] { sample }, squeeze: false));

            var one = Assert.Single(back);
            for (int i = 0; i < 777; i++) Assert.Equal(sample.Pcm[i], one.Pcm[i]);
        }

        [Fact]
        public void SqueezingKeepsTheSoundRecognisableAndMuchSmaller()
        {
            // Something that moves about like a real cry rather than a straight ramp, so the encoder has
            // to keep up with it the way it would with real sound.
            var pcm = new short[4000];
            for (int i = 0; i < pcm.Length; i++)
                pcm[i] = (short)(Math.Sin(i * 0.05) * 12000 * Math.Exp(-i / 2500.0));
            var sample = new SwavSample { SampleRate = 10512, Pcm = pcm };

            var squeezed = CryFiles.BuildArchive(new[] { sample });
            var plain = CryFiles.BuildArchive(new[] { sample }, squeeze: false);
            var back = Assert.Single(SwavSample.ParseArchive(squeezed));

            // Four bits a sample against sixteen, so about a quarter the size.
            Assert.True(squeezed.Length < plain.Length / 3,
                $"squeezed {squeezed.Length} should be far smaller than plain {plain.Length}");

            // And what comes back should follow the original closely, not merely be non-silent.
            int n = Math.Min(pcm.Length, back.Pcm.Length);
            Assert.True(n >= pcm.Length - 8, "almost every sample should survive");
            double sum = 0;
            for (int i = 0; i < n; i++) { double d = pcm[i] - back.Pcm[i]; sum += d * d; }
            double rms = Math.Sqrt(sum / n);
            int peak = pcm.Max(v => Math.Abs((int)v));
            Assert.True(rms < peak * 0.12, $"error {rms:F0} is too large against a peak of {peak}");
        }

        [Fact]
        public void SqueezingSilenceStaysSilent()
        {
            var sample = new SwavSample { SampleRate = 8000, Pcm = new short[512] };
            var back = Assert.Single(SwavSample.ParseArchive(CryFiles.BuildArchive(new[] { sample })));
            foreach (short v in back.Pcm) Assert.True(Math.Abs((int)v) < 64, "silence should stay silent");
        }

        [Fact]
        public void SqueezingNothingAtAllDoesNotThrow()
        {
            var encoded = CryFiles.EncodeAdpcm(Array.Empty<short>());
            Assert.True(encoded.Length >= 4);          // the little header is still there
            Assert.Equal(0, encoded.Length % 4);       // and it fills whole words
            Assert.NotNull(CryFiles.EncodeAdpcm(null));
        }

        [Fact]
        public void AnArchiveWithNothingInItIsStillAValidArchive()
        {
            var archive = CryFiles.BuildArchive(Array.Empty<SwavSample>());
            Assert.Equal("SWAR", System.Text.Encoding.ASCII.GetString(archive, 0, 4));
            Assert.Empty(SwavSample.ParseArchive(archive));

            var fromNull = CryFiles.BuildArchive(null);
            Assert.Equal("SWAR", System.Text.Encoding.ASCII.GetString(fromNull, 0, 4));
        }

        [Fact]
        public void TheArchiveSaysHowLongItIs()
        {
            var archive = CryFiles.BuildArchive(new[] { new SwavSample { SampleRate = 8000, Pcm = Ramp(64) } });
            int stated = archive[8] | (archive[9] << 8) | (archive[10] << 16) | (archive[11] << 24);
            Assert.Equal(archive.Length, stated);
        }

        [Fact]
        public void SomebodyIsToldWhatSortOfFileToBring()
        {
            Assert.Contains("WAV", CryFiles.AcceptedFormat);
            Assert.Contains("uncompressed", CryFiles.AcceptedFormat);
            Assert.Contains("mono or stereo", CryFiles.AcceptedFormat);
            Assert.Contains("sample", SoundArchive.HowItWorks);
        }
    }
}

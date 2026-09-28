using System.Linq;
using DSPRE.Avalonia.Data;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>The preview uses the particle library's generator: x = x * 0x5eedf715 + 0x1b0cb173.</summary>
    public class SplRandomTests
    {
        [Fact]
        public void TheGeneratorProducesTheLibrarySequence()
        {
            var r = new SplRandom(0);
            Assert.Equal(0x1b0cb173u, r.Next());
            Assert.Equal(0x13c434e2u, r.Next());
            Assert.Equal(0xfa6515fdu, r.Next());

            r.Reset(0x5EED);
            Assert.Equal(new uint[] { 0x5e1425e4, 0x0f56c927, 0xa45ad2a6, 0x2f6c2311 },
                         Enumerable.Range(0, 4).Select(_ => r.Next()).ToArray());
        }

        [Fact]
        public void TheRangeHelpersTakeTheTopBits()
        {
            // The first draw from seed 0 is 0x1b0cb173: its top 8 bits are 0x1b, top 9 are 0x36.
            Assert.Equal(0x1bu, new SplRandom(0).U32(8));
            Assert.Equal(20.0 * (0x36 - 256) / 256.0, new SplRandom(0).Range(20.0), 9);
            // ScaledRange(num, range) = num * (255 - ((range * U8) >> 8)) >> 8.
            Assert.Equal((30 * (255 - ((200 * 0x1b) >> 8))) >> 8, new SplRandom(0).ScaledRange(30, 200));
            // DoubleScaledRange multiplier = (255 + range - ((range * U8) >> 7)) / 256.
            Assert.Equal((255 + 64 - ((64 * 0x1b) >> 7)) / 256.0, new SplRandom(0).DoubleScaledRange(64), 9);
        }

        [Fact]
        public void AZeroLifeRangeStillKeepsTheWholeLife()
        {
            // Life is ScaledRange(life, 0) + 1: 255/256 of the life, truncated, plus one frame.
            var r = new SplRandom(1234);
            for (int life = 1; life <= 256; life++) Assert.Equal(life, r.ScaledRange(life, 0) + 1);
        }

        [Fact]
        public void EveryHelperConsumesOneDrawEvenWithAZeroRange()
        {
            var a = new SplRandom(7);
            a.DoubleScaledRange(0); a.ScaledRange(0); a.Range(0);
            var b = new SplRandom(7);
            b.Next(); b.Next(); b.Next();
            Assert.Equal(b.State, a.State);
        }
    }
}

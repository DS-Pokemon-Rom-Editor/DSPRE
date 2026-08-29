using DSPRE.Avalonia.Gl;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Which faces of a polygon get drawn, out of the material's polygon attribute.
    ///
    /// The cull mode lives in bits 6 and 7 (REG_G3_POLYGON_ATTR_BK_SHIFT is 6 in the SDK's ioreg_G3.h)
    /// and the values are GXCull from g3.h: 0 draws neither face, 1 drops the front, 2 drops the back,
    /// 3 draws both. A material also carries a mask saying which bits it actually sets, and across all
    /// 676 HeartGold map models exactly one material per map leaves these two alone; reading them
    /// regardless would say "draw nothing" and take that material off every map in the game.
    /// </summary>
    public class PolygonCullTests
    {
        private const uint SetsCull = 0xC0;      // a mask that covers both cull bits

        [Theory]
        [InlineData(0u, NsbmdCull.Nothing)]
        [InlineData(1u << 6, NsbmdCull.Front)]
        [InlineData(2u << 6, NsbmdCull.Back)]
        [InlineData(3u << 6, NsbmdCull.None)]
        public void TheModeComesOutOfBitsSixAndSeven(uint attr, int expected)
            => Assert.Equal(expected, NsbmdCull.FromPolyAttrib(attr, SetsCull));

        [Fact]
        public void OtherBitsInTheAttributeAreIgnored()
        {
            // Light bits, polygon mode, id and alpha all share the word and must not leak in.
            uint attr = (2u << 6) | 0x0F | (1u << 4) | (31u << 16) | (63u << 24);
            Assert.Equal(NsbmdCull.Back, NsbmdCull.FromPolyAttrib(attr, SetsCull));
        }

        [Fact]
        public void AMaterialThatNeverSetsTheBitsIsLeftDoubleSided()
        {
            // The bits happen to read as "draw nothing", but the mask says they were never set, so the
            // material must still be drawn.
            Assert.Equal(NsbmdCull.None, NsbmdCull.FromPolyAttrib(0u, 0u));
            Assert.Equal(NsbmdCull.None, NsbmdCull.FromPolyAttrib(3u << 6, 0x3F));   // mask covers other bits only
        }

        [Fact]
        public void APartialMaskStillCounts()
        {
            // Either cull bit being masked in means the material is speaking about culling.
            Assert.Equal(NsbmdCull.Back, NsbmdCull.FromPolyAttrib(2u << 6, 0x40));
            Assert.Equal(NsbmdCull.Back, NsbmdCull.FromPolyAttrib(2u << 6, 0x80));
        }
    }
}

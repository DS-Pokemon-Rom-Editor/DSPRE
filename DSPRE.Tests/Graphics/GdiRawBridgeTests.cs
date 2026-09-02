using System.Drawing;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Pins the pixel format of the GDI+ → <see cref="DSPRE.RawImage"/> migration bridge: BGRA byte
    /// order, straight (NOT premultiplied) alpha, top-down, tightly packed. Getting this wrong would
    /// tint/garble every image rendered through <c>ImageConverter.ToAvaloniaBitmap</c>.
    /// </summary>
    public class GdiRawBridgeTests
    {
        [Fact]
        public void FromGdi_ProducesBgraStraightAlpha()
        {
            using var bmp = new Bitmap(2, 1);                       // default = Format32bppArgb (straight alpha)
            bmp.SetPixel(0, 0, Color.FromArgb(255, 10, 20, 30));    // opaque   A255 R10  G20  B30
            bmp.SetPixel(1, 0, Color.FromArgb(128, 200, 100, 50));  // semi     A128 R200 G100 B50

            var raw = DSPRE.GdiRawBridge.FromGdi(bmp);

            Assert.Equal(2, raw.Width);
            Assert.Equal(1, raw.Height);
            Assert.Equal(2 * 1 * 4, raw.Bgra.Length);

            // Pixel (0,0): stored as B, G, R, A
            Assert.Equal(30, raw.Bgra[0]);
            Assert.Equal(20, raw.Bgra[1]);
            Assert.Equal(10, raw.Bgra[2]);
            Assert.Equal(255, raw.Bgra[3]);

            // Pixel (1,0): channels must be the ORIGINAL values (straight alpha).
            // If they were premultiplied, R would be ~100 (200*128/255), not 200.
            Assert.Equal(50, raw.Bgra[4]);
            Assert.Equal(100, raw.Bgra[5]);
            Assert.Equal(200, raw.Bgra[6]);
            Assert.Equal(128, raw.Bgra[7]);
        }

        [Fact]
        public void RawImage_SetPixel_WritesBgra()
        {
            var img = new DSPRE.RawImage(1, 1);
            img.SetPixel(0, 0, r: 1, g: 2, b: 3, a: 4);
            Assert.Equal(new byte[] { 3, 2, 1, 4 }, img.Bgra);   // B,G,R,A
        }

        [Fact]
        public void FromGdi_Null_ReturnsNull()
        {
            Assert.Null(DSPRE.GdiRawBridge.FromGdi(null));
        }
    }
}

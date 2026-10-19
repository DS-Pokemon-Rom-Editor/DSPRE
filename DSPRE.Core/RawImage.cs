namespace DSPRE
{
    /// <summary>
    /// A UI-toolkit-agnostic bitmap: raw 32-bit pixels in <b>BGRA</b> byte order (the order both
    /// Avalonia's <c>Bgra8888</c> and GDI+'s <c>Format32bppArgb</c> use in memory on little-endian),
    /// straight (un-premultiplied) alpha, top-down rows, tightly packed (stride = Width*4).
    ///
    /// This is the core-side image currency for the cross-platform migration: decoders will eventually
    /// produce <see cref="RawImage"/> directly (no <c>System.Drawing</c>), and each UI layer builds its
    /// own native bitmap from it (Avalonia <c>WriteableBitmap</c>, WinForms <c>Bitmap</c>). See
    /// <c>Avalonia/ImageConverter.ToAvaloniaBitmap(RawImage)</c> and the transitional
    /// <c>GdiRawBridge.FromGdi</c>.
    /// </summary>
    public sealed class RawImage
    {
        public int Width { get; }
        public int Height { get; }

        /// <summary>BGRA8888, straight alpha, top-down, length = Width*Height*4.</summary>
        public byte[] Bgra { get; }

        public RawImage(int width, int height, byte[] bgra)
        {
            Width = width;
            Height = height;
            Bgra = bgra;
        }

        public RawImage(int width, int height)
            : this(width, height, new byte[checked(width * height * 4)])
        {
        }

        public bool IsEmpty => Width <= 0 || Height <= 0 || Bgra == null || Bgra.Length == 0;

        public int Stride => Width * 4;

        /// <summary>Sets one pixel (bounds-unchecked). Channels are 0-255, straight alpha.</summary>
        public void SetPixel(int x, int y, byte r, byte g, byte b, byte a)
        {
            int i = (y * Width + x) * 4;
            Bgra[i] = b;
            Bgra[i + 1] = g;
            Bgra[i + 2] = r;
            Bgra[i + 3] = a;
        }
    }
}

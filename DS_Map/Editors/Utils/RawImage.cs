using System.Drawing;
using System.Drawing.Imaging;

namespace DSPRE {
    /// <summary>
    /// A plain 32-bit image: BGRA byte order, straight alpha, top-down rows, stride = Width*4.
    /// Same shape GDI+'s Format32bppArgb uses in memory, so converting either way is a straight copy.
    /// </summary>
    public sealed class RawImage {
        public int Width { get; private set; }
        public int Height { get; private set; }

        /// <summary>BGRA8888, straight alpha, top-down, length = Width*Height*4.</summary>
        public byte[] Bgra { get; private set; }

        public RawImage(int width, int height, byte[] bgra) {
            Width = width;
            Height = height;
            Bgra = bgra;
        }

        public RawImage(int width, int height)
            : this(width, height, new byte[checked(width * height * 4)]) {
        }

        public bool IsEmpty {
            get { return Width <= 0 || Height <= 0 || Bgra == null || Bgra.Length == 0; }
        }

        public int Stride {
            get { return Width * 4; }
        }

        public void SetPixel(int x, int y, byte r, byte g, byte b, byte a) {
            int i = (y * Width + x) * 4;
            Bgra[i] = b;
            Bgra[i + 1] = g;
            Bgra[i + 2] = r;
            Bgra[i + 3] = a;
        }

        public static RawImage FromBitmap(Bitmap source) {
            if (source == null) {
                return null;
            }

            RawImage result = new RawImage(source.Width, source.Height);
            using (Bitmap copy = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb)) {
                using (Graphics g = Graphics.FromImage(copy)) {
                    g.DrawImageUnscaled(source, 0, 0);
                }

                BitmapData data = copy.LockBits(new Rectangle(0, 0, copy.Width, copy.Height),
                    ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try {
                    for (int y = 0; y < copy.Height; y++) {
                        System.Runtime.InteropServices.Marshal.Copy(
                            System.IntPtr.Add(data.Scan0, y * data.Stride),
                            result.Bgra, y * result.Stride, result.Stride);
                    }
                } finally {
                    copy.UnlockBits(data);
                }
            }
            return result;
        }

        public Bitmap ToBitmap() {
            Bitmap bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, Width, Height),
                ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try {
                for (int y = 0; y < Height; y++) {
                    System.Runtime.InteropServices.Marshal.Copy(
                        Bgra, y * Stride,
                        System.IntPtr.Add(data.Scan0, y * data.Stride), Stride);
                }
            } finally {
                bmp.UnlockBits(data);
            }
            return bmp;
        }
    }
}

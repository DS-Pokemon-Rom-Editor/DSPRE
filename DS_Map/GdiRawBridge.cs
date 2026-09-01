using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace DSPRE
{
    /// <summary>
    /// TRANSITIONAL bridge: converts a GDI+ <see cref="System.Drawing.Image"/> into a toolkit-agnostic
    /// <see cref="RawImage"/> via <c>LockBits</c> (no PNG round-trip). This is the single, clearly-marked
    /// place the cross-platform migration still touches <c>System.Drawing</c> on the render path; as each
    /// decoder is changed to emit <see cref="RawImage"/> directly, its callers stop going through here,
    /// and this file is deleted once nothing produces GDI bitmaps anymore.
    /// </summary>
    public static class GdiRawBridge
    {
        public static RawImage FromGdi(Image image)
        {
            if (image == null) return null;

            Bitmap bmp = image as Bitmap;
            bool ownsBitmap = false;
            if (bmp == null)
            {
                bmp = new Bitmap(image);
                ownsBitmap = true;
            }

            try
            {
                int w = bmp.Width, h = bmp.Height;
                var rect = new Rectangle(0, 0, w, h);
                // Format32bppArgb is B,G,R,A byte order in memory (little-endian) with straight alpha, 
                // exactly what RawImage/Avalonia Bgra8888 expect.
                BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int dstStride = w * 4;
                    byte[] buf = new byte[dstStride * h];
                    for (int y = 0; y < h; y++)
                    {
                        Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), buf, y * dstStride, dstStride);
                    }
                    return new RawImage(w, h, buf);
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
            }
            finally
            {
                if (ownsBitmap) bmp.Dispose();
            }
        }
    }
}

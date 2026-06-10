using System.Drawing.Imaging;
using System.IO;
using GdiImage = System.Drawing.Image;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace DSPRE.Avalonia
{
    internal static class ImageConverter
    {
        /// <summary>
        /// Converts a System.Drawing.Image (WinForms/GDI+) to an Avalonia Bitmap
        /// suitable for binding to an Image control's Source property.
        /// Returns null if the source image is null.
        /// </summary>
        public static AvaloniaBitmap ToAvaloniaBitmap(GdiImage drawing)
        {
            if (drawing == null) return null;
            using var ms = new MemoryStream();
            drawing.Save(ms, ImageFormat.Png);
            ms.Seek(0, SeekOrigin.Begin);
            return new AvaloniaBitmap(ms);
        }
    }
}

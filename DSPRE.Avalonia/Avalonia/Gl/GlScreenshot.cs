using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;

namespace DSPRE.Avalonia.Gl
{
    /// <summary>Saves what a 3D view shows as a PNG, for the editors' Screenshot buttons.</summary>
    public static class GlScreenshot
    {
        /// <summary>Asks where to save, then captures the view's next frame there. <paramref name="saved"/> gets the path.</summary>
        public static async Task SaveAsync(NsbmdGlControl view, Window owner, string namePrefix, Action<string> saved)
        {
            string path = await DialogHelper.SaveFileAsync(owner, "Save map view screenshot",
                new[] { new FilePickerFileType("PNG image") { Patterns = new[] { "*.png" } } },
                $"{namePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            if (path == null) return;
            view.CaptureFrame((rgba, w, h) =>
            {
                try
                {
                    if (rgba == null || w <= 0 || h <= 0) throw new InvalidOperationException("the view returned no pixels");
                    SaveRgbaToPng(rgba, w, h, path);
                    saved?.Invoke(path);
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is System.IO.IOException || ex is UnauthorizedAccessException)
                {
                    _ = DialogHelper.ShowErrorAsync($"Couldn't save the screenshot:\n{ex.Message}", "Screenshot");
                }
            });
        }

        /// <summary>Writes bottom-row-first RGBA pixels, as the GL view reads them back, to a PNG.</summary>
        public static void SaveRgbaToPng(byte[] rgba, int w, int h, string path)
        {
            WriteableBitmap bmp = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormats.Rgba8888, AlphaFormat.Unpremul);
            using (ILockedFramebuffer fb = bmp.Lock())
            {
                int rowBytes = w * 4;
                for (int y = 0; y < h; y++)
                    System.Runtime.InteropServices.Marshal.Copy(rgba, (h - 1 - y) * rowBytes, fb.Address + y * fb.RowBytes, rowBytes);
            }
            bmp.Save(path, PngBitmapEncoderOptions.Default);
        }
    }
}

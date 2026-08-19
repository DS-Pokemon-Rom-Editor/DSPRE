using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using static DSPRE.RomInfo;

namespace DSPRE.Editors.Utils
{
    // Decodes a real battle-scene backdrop from the battle-background NARC (DirNames.battleBg).
    public sealed class BattleBgRenderer
    {
        public const int BackdropCount = 23;
        private const int BackdropChr0 = 3, BackdropScr = 2;
        private static int BackdropPal0 => RomInfo.gameFamily == RomInfo.GameFamilies.HGSS ? 176 : 172;

        private readonly EntryNarc _narc = new EntryNarc(DirNames.battleBg);
        public bool Available => _narc.Available;

        // Crop to 256x192, don't stretch; the source tilemap is often taller (e.g. 256x256).
        public Bitmap BuildBackdrop(int bgId, int timeZone = 0)
        {
            if (bgId < 0 || bgId >= BackdropCount || !_narc.Available) return null;
            int tz = Math.Max(0, Math.Min(2, timeZone));
            byte[] chr = NitroBgCodec.Inflate(_narc.Get(BackdropChr0 + bgId));
            byte[] pal = NitroBgCodec.Inflate(_narc.Get(BackdropPal0 + bgId * 3 + tz));
            byte[] scr = NitroBgCodec.Inflate(_narc.Get(BackdropScr));
            if (chr == null || pal == null || scr == null || NitroBgCodec.Find(pal, "TTLP", 0) < 0) return null;
            try
            {
                var c = NitroBgCodec.Composite(chr, pal, scr);
                return CropToBitmap(c.Rgba, c.Width, c.Height, 256, 192);
            }
            catch { return null; }
        }

        private static Bitmap CropToBitmap(byte[] rgba, int srcW, int srcH, int w, int h)
        {
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] bgra = new byte[w * h * 4];
                int rows = Math.Min(h, srcH), cols = Math.Min(w, srcW);
                for (int y = 0; y < rows; y++)
                    for (int x = 0; x < cols; x++)
                    {
                        int si = (y * srcW + x) * 4, di = (y * w + x) * 4;
                        bgra[di + 0] = rgba[si + 2]; bgra[di + 1] = rgba[si + 1]; bgra[di + 2] = rgba[si + 0]; bgra[di + 3] = rgba[si + 3];
                    }
                Marshal.Copy(bgra, 0, data.Scan0, bgra.Length);
            }
            finally { bmp.UnlockBits(data); }
            return bmp;
        }
    }
}

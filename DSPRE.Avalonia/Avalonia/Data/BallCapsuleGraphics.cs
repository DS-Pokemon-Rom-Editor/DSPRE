using System;
using System.Collections.Generic;
using DSPRE.ROMFiles;
using AvaBitmap = global::Avalonia.Media.Imaging.Bitmap;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.Data
{
    /// <summary>Seal stickers and the Ball Capsule board picture.</summary>
    public static class BallCapsuleGraphics
    {
        private const int BoardDrawing = 267, BoardColours = 287, BoardOverview = 283;

        /// <summary>Offset from a stored seal position, in case-screen coordinates, to the overview picture's ball.</summary>
        public const int PlacedToBoardX = -56, PlacedToBoardY = 16;

        private static bool Johto => gameFamily == GameFamilies.HGSS;
        private static int StickerLayout => Johto ? 38 : 93;
        private static int StickerAnimation => Johto ? 36 : 1;
        private static int StickerColours => Johto ? 6 : 293;

        public static bool HasStickers => gameDirs != null && gameDirs.ContainsKey(DirNames.sealGraphics);

        /// <summary>The sticker at 32 by 32, centred, or null.</summary>
        public static AvaBitmap Sticker(BallSeal seal) => StickerCells(seal)?.RenderCell(0, 32, 32);

        /// <summary>The sticker's cell renderer, or null when its files do not load.</summary>
        public static WeCellAnimRenderer StickerCells(BallSeal seal)
        {
            if (seal == null || !HasStickers) return null;
            var renderer = new WeCellAnimRenderer();
            return renderer.Load(DirNames.sealGraphics, seal.Sprite, DirNames.sealGraphics, StickerColours,
                                 DirNames.sealGraphics, StickerLayout, DirNames.sealGraphics, StickerAnimation)
                ? renderer : null;
        }

        /// <summary>The capsule board at 256 by 192, or null. Its files are only known for Diamond, Pearl and Platinum.</summary>
        public static AvaBitmap Board()
        {
            if (!HasStickers || Johto) return null;
            try
            {
                var narc = new ScriptNarc(DirNames.sealGraphics);
                var ball = NitroBgCodec.Composite(NitroBgCodec.Inflate(narc.Get(BoardDrawing)), NitroBgCodec.Inflate(narc.Get(BoardColours)),
                                                  NitroBgCodec.Inflate(narc.Get(BoardOverview)), transparentZero: false);
                if (ball?.Rgba == null) return null;
                var rgba = new byte[256 * 192 * 4];
                for (int y = 0; y < Math.Min(192, ball.Height); y++)
                    Array.Copy(ball.Rgba, y * ball.Width * 4, rgba, y * 256 * 4, Math.Min(256, ball.Width) * 4);
                return ImageConverter.FromRgba(rgba, 256, 192);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Ball capsule board could not be drawn: " + ex.Message);
                return null;
            }
        }
    }
}

using System;
using System.IO;
using System.Linq;
using DSPRE.HgEngine;
using Ekona.Images;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// A trainer sprite's hg-engine source PNG, which the build turns into its drawing and first 16 colours.
    /// </summary>
    internal static class TrainerSpriteSourcePng
    {
        /// <summary>Replaces the loaded drawing and first palette with the PNG's. False when it is missing or a different size.</summary>
        public static bool TryApply(string pngPath, ImageBase tile, PaletteBase pal)
        {
            try
            {
                if (tile == null || pal == null || !File.Exists(pngPath)) return false;
                if (!IndexedPng.TryRead(File.ReadAllBytes(pngPath), out var raster, out var colours, out int w, out int h)) return false;
                if (w % 8 != 0 || h % 8 != 0 || w * h != tile.Tiles.Length * 2) return false;

                tile.Set_Tiles(HgEngineTrainerGraphicsSource.Pack4(HgEngineTrainerGraphicsSource.RasterToTiles(raster, w, h)));
                var bank = pal.Palette[0];
                for (int i = 0; i < bank.Length && i < colours.Length; i++)
                    bank[i] = System.Drawing.Color.FromArgb((int)(colours[i] >> 16) & 0xF8, (int)(colours[i] >> 8) & 0xF8, (int)colours[i] & 0xF8);
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error("Trainer sprite source PNG could not be read: " + ex.Message);
                return false;
            }
        }

        /// <summary>Writes the drawing and first palette back into the PNG, keeping its size and colour count. Null on success.</summary>
        public static string Write(string pngPath, ImageBase tile, PaletteBase pal)
        {
            try
            {
                if (!IndexedPng.TryRead(File.ReadAllBytes(pngPath), out _, out var colours, out int w, out int h))
                    return $"{Path.GetFileName(pngPath)} is not an indexed PNG.";
                if (w * h != tile.Tiles.Length * 2)
                    return $"{Path.GetFileName(pngPath)} is {w}×{h}, which doesn't match the sprite's drawing.";

                var raster = HgEngineTrainerGraphicsSource.TilesToRaster(HgEngineTrainerGraphicsSource.Unpack4(tile.Tiles), w, h);
                var bank = pal.Palette[0];
                // A colour the DS would store the same keeps the PNG's own 8-bit value, so an untouched save changes nothing.
                var palette = colours.Select((c, i) =>
                {
                    if (i >= bank.Length) return c;
                    uint edited = 0xFF000000u | ((uint)bank[i].R << 16) | ((uint)bank[i].G << 8) | bank[i].B;
                    return (edited & 0xF8F8F8) == (c & 0xF8F8F8) ? c : edited;
                }).ToArray();
                File.WriteAllBytes(pngPath, IndexedPng.Write(raster, palette, w, h, palette.Length <= 16 ? 4 : 8));
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}

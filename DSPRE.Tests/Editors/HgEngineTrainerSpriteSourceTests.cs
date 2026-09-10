using System;
using System.IO;
using System.Linq;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Data;
using DSPRE.HgEngine;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// hg-engine trainer sprite sources: the PNG becomes the drawing tile by tile, and saving writes it back unchanged.
    /// </summary>
    public class HgEngineTrainerSpriteSourceTests
    {
        [Fact]
        public void TheDrawingIsThePngReadTileByTile()
        {
            // 16×8: two tiles side by side, pixel value = its column within the image.
            var raster = Enumerable.Range(0, 16 * 8).Select(i => (byte)(i % 16)).ToArray();
            var tiles = HgEngineTrainerGraphicsSource.RasterToTiles(raster, 16, 8);

            Assert.Equal(Enumerable.Range(0, 64).Select(i => (byte)(i % 8)), tiles.Take(64));
            Assert.Equal(Enumerable.Range(0, 64).Select(i => (byte)(8 + i % 8)), tiles.Skip(64));
            Assert.Equal(raster, HgEngineTrainerGraphicsSource.TilesToRaster(tiles, 16, 8));

            var packed = HgEngineTrainerGraphicsSource.Pack4(tiles);
            Assert.Equal(0x10, packed[0]);   // low nibble first
            Assert.Equal(tiles, HgEngineTrainerGraphicsSource.Unpack4(packed));
        }

        [Fact]
        public void AFourBitPngReadsBackExactly()
        {
            var palette = Enumerable.Range(0, 16).Select(i => 0xFF000000u | (uint)(i * 0x0F0B07)).ToArray();
            var indices = Enumerable.Range(0, 7 * 3).Select(i => (byte)((i * 5) % 16)).ToArray();

            var png = IndexedPng.Write(indices, palette, 7, 3, bitDepth: 4);

            Assert.Equal(4, png[24]);   // IHDR bit depth
            Assert.True(IndexedPng.TryRead(png, out var readIndices, out var readPalette, out int w, out int h));
            Assert.Equal((7, 3), (w, h));
            Assert.Equal(indices, readIndices);
            Assert.Equal(palette, readPalette);
        }

        [SkippableTheory]
        [InlineData("data/graphics/trainer_gfx/019.png", "build/trainer_gfx/8_019")]
        [InlineData("data/graphics/trainer_back_gfx/00.png", "build/trainer_back_gfx/6_00")]
        public void TheCheckoutsSourcesMatchTheirBuildAndSaveBackUnchanged(string png, string built)
        {
            string root = Environment.GetEnvironmentVariable("DSPRE_TEST_HGENGINE");
            Skip.If(string.IsNullOrWhiteSpace(root) || !File.Exists(Path.Combine(root, png)) || !File.Exists(Path.Combine(root, built + "-00.NCGR")),
                "Set DSPRE_TEST_HGENGINE to a built hg-engine checkout");

            string pngPath = Path.Combine(root, png);
            var tile = new Images.NCGR(Path.Combine(root, built + "-00.NCGR"), 0, "tiles");
            var pal = new Images.NCLR(Path.Combine(root, built + "-01.NCLR"), 1, "colours");
            byte[] builtTiles = (byte[])tile.Tiles.Clone();
            var builtColours = pal.Palette[0].Select(c => c.ToArgb()).ToArray();

            // Scramble what was loaded, so the checks below can only pass if the PNG really replaced it.
            tile.Set_Tiles(new byte[builtTiles.Length]);
            Assert.True(TrainerSpriteSourcePng.TryApply(pngPath, tile, pal));
            Assert.Equal(builtTiles, tile.Tiles);
            Assert.Equal(builtColours, pal.Palette[0].Select(c => c.ToArgb()));

            string copy = Path.Combine(Path.GetTempPath(), $"dspre-trainer-png-{Guid.NewGuid():N}.png");
            try
            {
                File.Copy(pngPath, copy);
                Assert.Null(TrainerSpriteSourcePng.Write(copy, tile, pal));
                Assert.True(IndexedPng.TryRead(File.ReadAllBytes(pngPath), out var wantIndices, out var wantPalette, out _, out _));
                Assert.True(IndexedPng.TryRead(File.ReadAllBytes(copy), out var gotIndices, out var gotPalette, out _, out _));
                Assert.Equal(wantIndices, gotIndices);
                Assert.Equal(wantPalette, gotPalette);
                Assert.Equal(4, File.ReadAllBytes(copy)[24]);

                // An edit lands where it was made: the second tile's first pixel is (8, 0) in the PNG.
                var indices = HgEngineTrainerGraphicsSource.Unpack4(tile.Tiles);
                indices[64] = (byte)((indices[64] + 1) % 16);
                tile.Set_Tiles(HgEngineTrainerGraphicsSource.Pack4(indices));
                pal.Palette[0][3] = System.Drawing.Color.FromArgb(0xF8, 0x08, 0x00);
                Assert.Null(TrainerSpriteSourcePng.Write(copy, tile, pal));
                Assert.True(IndexedPng.TryRead(File.ReadAllBytes(copy), out var editedIndices, out var editedPalette, out int w, out _));
                Assert.Equal(indices[64], editedIndices[8]);
                Assert.Equal(wantIndices.Where((_, i) => i != 8), editedIndices.Where((_, i) => i != 8));
                Assert.Equal(0xFFF80800u, editedPalette[3]);
                Assert.Equal(wantPalette.Where((_, i) => i != 3), editedPalette.Where((_, i) => i != 3));
            }
            finally { File.Delete(copy); }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.Avalonia.Data;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// Making the three files a background is drawn from out of a plain picture, and reading them back
    /// to see the same picture come out.
    /// </summary>
    public class TilesetBuilderTests
    {
        // ── pictures to build from ────────────────────────────────────────────────────────────────

        private static byte[] Blank(int w, int h)
        {
            var p = new byte[w * h * 4];
            for (int i = 0; i < w * h; i++) p[i * 4 + 3] = 255;
            return p;
        }

        private static void Put(byte[] rgba, int w, int x, int y, int r, int g, int b, int a = 255)
        {
            int i = (y * w + x) * 4;
            rgba[i] = (byte)r; rgba[i + 1] = (byte)g; rgba[i + 2] = (byte)b; rgba[i + 3] = (byte)a;
        }

        /// <summary>A picture with plenty of repeating in it, the way a real background looks.</summary>
        private static byte[] Patterned(int w, int h, int coloursPerSquare = 6)
        {
            var p = Blank(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int square = (y / 8) * (w / 8) + (x / 8);
                    int n = (x % 8 + y % 8 * 3 + square) % coloursPerSquare;
                    Put(p, w, x, y, 8 + n * 40, 16 + n * 24, 200 - n * 30);
                }
            return p;
        }

        /// <summary>The picture the screen can actually keep: five bits of each channel, nothing else lost.</summary>
        private static byte[] AsTheScreenKeepsIt(byte[] rgba)
        {
            var o = (byte[])rgba.Clone();
            for (int i = 0; i < o.Length; i += 4)
            {
                o[i] &= 0xF8; o[i + 1] &= 0xF8; o[i + 2] &= 0xF8;
                o[i + 3] = o[i + 3] < 128 ? (byte)0 : (byte)255;
            }
            return o;
        }

        /// <summary>Reads the three files back into a picture the way the browser draws one.</summary>
        private static byte[] ReadBack(TilesetBuilder.Result r, out int w, out int h)
        {
            var img = NitroBgCodec.Composite(r.Tiles, r.Colours, r.Arrangement, r.ClearSlotKept);
            w = img.Width; h = img.Height;
            return img.Rgba;
        }

        private static (int differing, string first) Compare(byte[] want, byte[] got, int w)
        {
            int bad = 0; string first = null;
            for (int i = 0; i < want.Length; i += 4)
            {
                bool wantClear = want[i + 3] == 0, gotClear = got[i + 3] == 0;
                bool same = wantClear && gotClear
                    || (!wantClear && !gotClear && want[i] == got[i] && want[i + 1] == got[i + 1]
                        && want[i + 2] == got[i + 2]);
                if (same) continue;
                bad++;
                first ??= $"at {(i / 4) % w},{(i / 4) / w} wanted "
                        + $"{want[i]},{want[i + 1]},{want[i + 2]},{want[i + 3]} but got "
                        + $"{got[i]},{got[i + 1]},{got[i + 2]},{got[i + 3]}";
            }
            return (bad, first);
        }

        // ── the round trip ────────────────────────────────────────────────────────────────────────

        [Theory]
        [InlineData(64, 48, false)]
        [InlineData(256, 192, false)]
        [InlineData(64, 48, true)]
        [InlineData(512, 256, false)]   // wider than one 32-square block, so the block layout matters
        [InlineData(8, 8, false)]
        public void APictureComesBackOutTheSameAsItWentIn(int w, int h, bool eightBit)
        {
            var picture = Patterned(w, h);
            var built = TilesetBuilder.Build(picture, w, h, eightBit, keepClearSlot: false);
            Assert.Null(built.Whynot);

            var got = ReadBack(built, out int gw, out int gh);
            Assert.Equal(w, gw);
            Assert.Equal(h, gh);

            var (differing, first) = Compare(AsTheScreenKeepsIt(picture), got, w);
            Assert.True(differing == 0, $"{differing} of {w * h} pixels came back different, {first}");
        }

        [Fact]
        public void SeeThroughPixelsComeBackSeeThrough()
        {
            const int w = 32, h = 24;
            var picture = Patterned(w, h);
            for (int y = 4; y < 12; y++)
                for (int x = 6; x < 20; x++) Put(picture, w, x, y, 0, 0, 0, 0);

            var built = TilesetBuilder.Build(picture, w, h, false, keepClearSlot: true);
            Assert.Null(built.Whynot);
            Assert.True(built.ClearSlotKept);

            var got = ReadBack(built, out _, out _);
            var (differing, first) = Compare(AsTheScreenKeepsIt(picture), got, w);
            Assert.True(differing == 0, $"{differing} pixels came back different, {first}");
        }

        // ── sharing ───────────────────────────────────────────────────────────────────────────────

        [Fact]
        public void ASquareThatOnlyRepeatsTurnedOverStillSharesItsTile()
        {
            // Two squares side by side, the right one the left one mirrored. The single mark sits off
            // centre both ways, so the left square matches none of its own turnings and sharing can
            // only happen by turning it over.
            const int w = 16, h = 8;
            var picture = Blank(w, h);
            Put(picture, w, 1, 2, 248, 0, 0);
            Put(picture, w, 14, 2, 248, 0, 0);

            var built = TilesetBuilder.Build(picture, w, h, false, false);
            Assert.Null(built.Whynot);
            Assert.Equal(1, built.TilesKept);
            Assert.Equal(1, built.RepeatedTurnedOver);
            Assert.Equal(0, built.RepeatedAsIs);

            var (differing, first) = Compare(AsTheScreenKeepsIt(picture), ReadBack(built, out _, out _), w);
            Assert.True(differing == 0, $"{differing} pixels came back different, {first}");
        }

        [Fact]
        public void ASquareThatRepeatsExactlySharesItsTileWithoutTurning()
        {
            const int w = 16, h = 8;
            var picture = Blank(w, h);
            Put(picture, w, 1, 2, 248, 0, 0);
            Put(picture, w, 9, 2, 248, 0, 0);

            var built = TilesetBuilder.Build(picture, w, h, false, false);
            Assert.Null(built.Whynot);
            Assert.Equal(1, built.TilesKept);
            Assert.Equal(1, built.RepeatedAsIs);
            Assert.Equal(0, built.RepeatedTurnedOver);
        }

        [Fact]
        public void EverySquareOfAPlainPictureSharesOneTile()
        {
            var picture = Blank(64, 64);
            var built = TilesetBuilder.Build(picture, 64, 64, false, false);
            Assert.Null(built.Whynot);
            Assert.Equal(1, built.TilesKept);
            Assert.Equal(64, built.Squares);
            Assert.Equal(63, built.RepeatedAsIs);
            Assert.Equal(1, built.Banks);
        }

        // ── what it refuses, and what it says ─────────────────────────────────────────────────────

        [Fact]
        public void ASizeThatDoesNotDivideByEightIsRefusedWithTheSizeThatWould()
        {
            var built = TilesetBuilder.Build(Blank(60, 45), 60, 45, false, false);
            Assert.NotNull(built.Whynot);
            Assert.Contains("56 by 40", built.Whynot);
            Assert.Null(built.Tiles);
            Assert.Null(built.Colours);
            Assert.Null(built.Arrangement);
        }

        [Fact]
        public void ASquareWithMoreColoursThanABankHoldsIsRefusedAndNamed()
        {
            var picture = Blank(16, 8);
            // The right-hand square asks for seventeen colours and nothing else; sixteen is the most a
            // bank holds. Every one of its 64 pixels is painted, so the ground adds no eighteenth.
            for (int i = 0; i < 64; i++)
            {
                int n = i % 17;
                Put(picture, 16, 8 + i % 8, i / 8, 8 + n * 13, 40 + n * 7, 90 + n * 5);
            }

            var built = TilesetBuilder.Build(picture, 16, 8, false, keepClearSlot: false);
            Assert.NotNull(built.Whynot);
            Assert.Contains("16 colours", built.Whynot);
            Assert.Contains("the square at 8,0 wants 17", built.Whynot);
            Assert.Contains("256 colours", built.Whynot);   // says what would fit
            Assert.Null(built.Tiles);
        }

        [Fact]
        public void KeepingASeeThroughSlotLeavesRoomForOneFewerColour()
        {
            // Sixteen greys, the first of them black, so a see-through pixel turned black asks for no
            // seventeenth colour and only the kept slot can push it over.
            var picture = Blank(8, 8);
            for (int i = 0; i < 64; i++)
            {
                int n = i % 16;
                Put(picture, 8, i % 8, i / 8, n * 16, n * 16, n * 16);
            }
            Put(picture, 8, 7, 7, 0, 0, 0, 0);   // one see-through pixel, so the clear slot is kept

            Assert.Null(TilesetBuilder.Build(picture, 8, 8, false, keepClearSlot: false).Whynot);

            var tight = TilesetBuilder.Build(picture, 8, 8, false, keepClearSlot: true);
            Assert.NotNull(tight.Whynot);
            Assert.Contains("15 colours", tight.Whynot);
        }

        [Fact]
        public void WithOneListOf256APictureUsingMoreIsRefusedByTheColourCount()
        {
            // 300 colours spread thinly, so no single square is over any limit of its own and only the
            // whole picture is. In sixteen banks this would be refused for banks instead.
            const int w = 160, h = 160;
            var picture = Blank(w, h);
            // Stepping by eight keeps every one of them a colour the screen can actually tell apart.
            for (int i = 0; i < 300; i++)
                Put(picture, w, i % w, i / w, (i % 32) * 8, (i / 32) * 8, 8);

            var built = TilesetBuilder.Build(picture, w, h, eightBit: true, keepClearSlot: false);
            Assert.NotNull(built.Whynot);
            Assert.Contains("One list holds 256 colours", built.Whynot);
            Assert.DoesNotContain("bank", built.Whynot);   // banks are not a thing in this mode
            Assert.Null(built.Tiles);
        }

        [Fact]
        public void WithOneListOf256EverySquareDrawsFromIt()
        {
            const int w = 160, h = 160;
            var picture = Blank(w, h);
            for (int i = 0; i < 200; i++)
                Put(picture, w, i % w, i / w, (i % 32) * 8, (i / 32) * 8, 8);

            var built = TilesetBuilder.Build(picture, w, h, eightBit: true, keepClearSlot: false);
            Assert.Null(built.Whynot);
            Assert.Equal(1, built.Banks);
            var (differing, first) = Compare(AsTheScreenKeepsIt(picture), ReadBack(built, out _, out _), w);
            Assert.True(differing == 0, $"{differing} pixels came back different, {first}");
        }

        [Fact]
        public void APictureNeedingMoreThanSixteenBanksIsRefused()
        {
            // Seventeen squares in a row, each wanting sixteen colours of its own that no other square
            // shares, so no two can ever sit in one bank.
            const int w = 17 * 8, h = 8;
            var picture = Blank(w, h);
            for (int s = 0; s < 17; s++)
                for (int i = 0; i < 64; i++)
                {
                    int n = s * 16 + i % 16;      // no colour number is ever reached by two squares
                    Put(picture, w, s * 8 + i % 8, i / 8, 8 + n, 8 + (n * 3) % 240, 8 + (n * 7) % 240);
                }

            var built = TilesetBuilder.Build(picture, w, h, false, false);
            Assert.NotNull(built.Whynot);
            Assert.Contains("more than 16 colour banks", built.Whynot);
            Assert.Null(built.Arrangement);
        }

        [Fact]
        public void APictureNeedingMoreThanAThousandTilesIsRefusedWithTheCount()
        {
            // 1025 squares, every one different from every other however it is turned.
            const int cols = 41, rows = 25, w = cols * 8, h = rows * 8;   // 1025 squares
            var picture = Blank(w, h);
            for (int s = 0; s < cols * rows; s++)
            {
                int sx = (s % cols) * 8, sy = (s / cols) * 8;
                // A square's own number written across its top two rows, eleven bits of it, so no two
                // squares carry the same picture. Keeping the writing at the top and adding one mark in
                // the bottom left keeps every square unlike its own turnings as well.
                for (int bit = 0; bit < 11; bit++)
                    Put(picture, w, sx + bit % 8, sy + bit / 8, ((s >> bit) & 1) * 248, 0, 0);
                Put(picture, w, sx, sy + 7, 0, 248, 0);
            }

            var built = TilesetBuilder.Build(picture, w, h, false, false);
            Assert.NotNull(built.Whynot);
            Assert.Contains("1024 tiles", built.Whynot);
            Assert.Matches(@"needs 10\d\d", built.Whynot);
        }

        [Fact]
        public void ThePictureIsTooBigToFitOnAScreenAtAll()
        {
            var built = TilesetBuilder.Build(Blank(2048, 8), 2048, 8, false, false);
            Assert.NotNull(built.Whynot);
            Assert.Contains("1024", built.Whynot);
        }

        // ── what it says about colours the screen cannot tell apart ───────────────────────────────

        [Fact]
        public void ColoursTheScreenCannotTellApartAreCountedAndSaidPlainly()
        {
            var picture = Blank(8, 8);
            // Four reds that differ only below the five bits the screen keeps, so all four come out as one.
            Put(picture, 8, 0, 0, 128, 0, 0);
            Put(picture, 8, 1, 0, 129, 0, 0);
            Put(picture, 8, 2, 0, 130, 0, 0);
            Put(picture, 8, 3, 0, 131, 0, 0);

            var built = TilesetBuilder.Build(picture, 8, 8, false, false);
            Assert.Null(built.Whynot);
            Assert.Equal(2, built.ColoursKept);              // the one red and the black behind it
            Assert.Equal(3, built.ColoursMergedByScreen);
            Assert.Contains(built.Notes, n => n.Contains("came out as 2"));
        }

        [Fact]
        public void TheSummarySaysWhatWasMade()
        {
            var built = TilesetBuilder.Build(Patterned(64, 48), 64, 48, false, false);
            Assert.Null(built.Whynot);
            Assert.Contains("64 by 48", built.Summary);
            Assert.Contains($"{built.TilesKept} tiles", built.Summary);
            Assert.Equal(48, built.Squares);
        }

        // ── the files themselves ──────────────────────────────────────────────────────────────────

        [Fact]
        public void TheThreeFilesLookLikeTheOnesTheGamesCarry()
        {
            var built = TilesetBuilder.Build(Patterned(64, 48), 64, 48, false, false);
            Assert.Null(built.Whynot);

            Assert.Equal("RLCN", Tag(built.Colours, 0));
            Assert.Equal("TTLP", Tag(built.Colours, 0x10));
            Assert.Equal(built.Colours.Length, NitroBgCodec.U32(built.Colours, 8));
            Assert.Equal(3, NitroBgCodec.U32(built.Colours, 0x18));          // sixteen colours a bank
            Assert.Equal(16, NitroBgCodec.U32(built.Colours, 0x24));         // as every real file says

            Assert.Equal("RGCN", Tag(built.Tiles, 0));
            Assert.Equal("RAHC", Tag(built.Tiles, 0x10));
            Assert.Equal(built.Tiles.Length, NitroBgCodec.U32(built.Tiles, 8));
            Assert.Equal(1, NitroBgCodec.U16(built.Tiles, 0x18));                       // one tile down
            Assert.Equal(built.TilesKept, NitroBgCodec.U16(built.Tiles, 0x1A));         // that many across
            Assert.Equal(built.TilesKept * 32, NitroBgCodec.U32(built.Tiles, 0x28));
            Assert.Equal("SOPC", Tag(built.Tiles, built.Tiles.Length - 0x10));

            Assert.Equal("RCSN", Tag(built.Arrangement, 0));
            Assert.Equal("NRCS", Tag(built.Arrangement, 0x10));
            Assert.Equal(built.Arrangement.Length, NitroBgCodec.U32(built.Arrangement, 8));
            Assert.Equal(64, NitroBgCodec.U16(built.Arrangement, 0x18));
            Assert.Equal(48, NitroBgCodec.U16(built.Arrangement, 0x1A));
        }

        private static string Tag(byte[] d, int at) =>
            new string(new[] { (char)d[at], (char)d[at + 1], (char)d[at + 2], (char)d[at + 3] });
    }
}

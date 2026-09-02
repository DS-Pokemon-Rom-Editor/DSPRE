using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.ROMFiles;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The font the games write with, read out of the ROM rather than kept in DSPRE, so it shows whatever
    /// the person using it has put there.
    /// </summary>
    [Collection("rom")]
    public class FieldFontTests
    {
        private const string Project = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";

        private static FieldFont Talk()
        {
            Assert.True(Directory.Exists(Project), $"these tests need the project at {Project}");
            new RomInfo("IPKE", Project);
            var font = FieldFont.LoadTalkFont();
            Assert.NotNull(font);
            return font;
        }

        [Fact]
        public void TheTalkingFontComesOutOfTheRomTheWayTheHeaderSaysItShould()
        {
            var font = Talk();

            // Two bits a pixel in a sixteen by sixteen box, which is what the header declares.
            Assert.Equal(2, font.BitsPerPixel);
            Assert.Equal(16, font.Height);
            Assert.Equal(16, font.MaxWidth);

            // Enough letters for a Latin alphabet, the kana and the punctuation.
            Assert.InRange(font.GlyphCount, 256, 2048);
        }

        [Fact]
        public void EveryLetterKeepsItsInkInsideItsOwnWidth()
        {
            // This is the check that says the pixels are being unpacked correctly.
            var font = Talk();

            int checkedGlyphs = 0;
            for (int g = 0; g < font.GlyphCount; g++)
            {
                int w = font.WidthOf(g);
                if (w <= 0) continue;
                checkedGlyphs++;

                for (int y = 0; y < FieldFont.CellSize; y++)
                    for (int x = w; x < FieldFont.CellSize; x++)
                        Assert.True(font.PixelAt(g, x, y) == FieldFont.Nothing,
                            $"letter {g} has ink at column {x} but says it is only {w} wide");
            }

            Assert.True(checkedGlyphs > 400, $"only {checkedGlyphs} letters had a width to check");
        }

        [Fact]
        public void TheLettersAreWhereTheCharacterTableSaysTheyAre()
        {
            var font = Talk();
            Assert.True(FieldFontCharacters.Ready, "the character table did not load");

            // Every letter and digit has to have a picture, and it has to have some ink in it.
            foreach (char c in "ABCXYZabcxyz0189?!.,")
            {
                int g = FieldFontCharacters.GlyphFor(c);
                Assert.True(g >= 0 && g < font.GlyphCount, $"no picture for {c}");
                Assert.True(font.WidthOf(g) > 0, $"the picture for {c} has no width");

                bool anyInk = false;
                for (int y = 0; y < FieldFont.CellSize && !anyInk; y++)
                    for (int x = 0; x < FieldFont.CellSize && !anyInk; x++)
                    {
                        byte v = font.PixelAt(g, x, y);
                        if (v != FieldFont.Nothing && v != FieldFont.Paper) anyInk = true;
                    }
                Assert.True(anyInk, $"the picture for {c} is blank");
            }
        }

        [Fact]
        public void ASpaceIsBlankButStillTakesUpRoom()
        {
            var font = Talk();
            int g = FieldFontCharacters.GlyphFor(' ');
            Assert.True(g >= 0);
            Assert.True(font.WidthOf(g) > 0, "a space with no width would run every word together");

            for (int y = 0; y < FieldFont.CellSize; y++)
                for (int x = 0; x < FieldFont.CellSize; x++)
                {
                    byte v = font.PixelAt(g, x, y);
                    Assert.True(v == FieldFont.Nothing || v == FieldFont.Paper,
                        "a space should have no ink in it");
                }
        }

        [Fact]
        public void LettersAreDifferentWidthsBecauseTheFontIsProportional()
        {
            var font = Talk();
            int i = FieldFontCharacters.GlyphFor('i');
            int w = FieldFontCharacters.GlyphFor('W');

            Assert.True(font.WidthOf(i) < font.WidthOf(w),
                "an i should be narrower than a W, or the widths are not being read");
        }

        [Fact]
        public void MeasuringASentenceAddsUpTheLettersItIsMadeOf()
        {
            var font = Talk();
            int Glyph(char c) => FieldFontCharacters.GlyphFor(c);

            const string line = "HELLO";
            int expected = line.Sum(c => font.WidthOf(Glyph(c)));
            Assert.Equal(expected, font.Measure(line, Glyph));

            // And a real line of dialogue has to fit across the box.
            Assert.True(font.Measure("I like SHORTS! They're comfy", Glyph) <= FieldMessageWindow.TextWidth * 2);
        }

        [Fact]
        public void RubbishIsTurnedAwayRatherThanRead()
        {
            Assert.Null(FieldFont.Read(null));
            Assert.Null(FieldFont.Read(new byte[4]));
            Assert.Null(FieldFont.Read(Enumerable.Repeat((byte)0xAA, 4096).ToArray()));
        }
    }
}

using System;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.ROMFiles;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>
    /// Saving a font: that pressing Save really puts the edit in the ROM's own file, that reopening
    /// finds it, and that nothing else in the font moved.
    /// </summary>
    [Collection("rom")]
    public class FontEditorSaveTests
    {
        private readonly ITestOutputHelper _out;
        public FontEditorSaveTests(ITestOutputHelper o) { _out = o; }

        private static readonly string Project = TestRoms.HeartGold;

        [Fact]
        public void AnEditedLetterIsStillThereAfterTheFontIsSavedAndOpenedAgain()
        {
            if (!Directory.Exists(Project))
            { Assert.Fail($"{Project} is not there, so this proved nothing."); return; }
            try { new RomInfo("IPKE", Project); }
            catch (Exception ex) { Assert.Fail("the project would not open: " + ex.Message); return; }

            DSUtils.TryUnpackNarcs(new System.Collections.Generic.List<DirNames> { DirNames.fonts });
            string dir = gameDirs[DirNames.fonts].unpackedDir;
            var files = Directory.GetFiles(dir).OrderBy(x => x).ToArray();

            int which = -1;
            FieldFont font = null;
            for (int i = 0; i < files.Length; i++)
            {
                try { font = FieldFont.Read(File.ReadAllBytes(files[i])); } catch { continue; }
                if (font != null) { which = i; break; }
            }
            Assert.True(font != null, "no readable font was found, so this proved nothing");

            byte[] before = File.ReadAllBytes(files[which]);
            string backup = Path.Combine(Path.GetTempPath(), "dspre_font_backup.bin");
            File.WriteAllBytes(backup, before);
            try
            {
                // Turn one spot of one letter to something it is not, and change that letter's width.
                const int glyph = 40, x = 3, y = 4;
                byte was = font.PixelAt(glyph, x, y);
                byte now = (byte)(was == 1 ? 2 : 1);
                int wasWidth = font.WidthOf(glyph);
                int nowWidth = wasWidth == 9 ? 8 : 9;

                font.SetPixel(glyph, x, y, now);
                font.SetWidth(glyph, nowWidth);
                File.WriteAllBytes(files[which], font.Write());

                // Open it again the way the editor does, from the file rather than from what is in hand.
                var reopened = FieldFont.Read(File.ReadAllBytes(files[which]));
                Assert.True(reopened != null, "the saved font could not be read back");
                Assert.Equal(now, reopened.PixelAt(glyph, x, y));
                Assert.Equal(nowWidth, reopened.WidthOf(glyph));

                // And nothing else moved. Every other spot of every other letter has to be as it was.
                var original = FieldFont.Read(before);
                int moved = 0;
                for (int g = 0; g < original.GlyphCount; g++)
                {
                    if (original.WidthOf(g) != reopened.WidthOf(g) && g != glyph) moved++;
                    for (int py = 0; py < FieldFont.CellSize; py++)
                        for (int px = 0; px < FieldFont.CellSize; px++)
                        {
                            if (g == glyph && px == x && py == y) continue;
                            if (original.PixelAt(g, px, py) != reopened.PixelAt(g, px, py)) moved++;
                        }
                }
                _out.WriteLine($"font {which}: letter {glyph} spot {x},{y} went from {was} to {now}, "
                             + $"width from {wasWidth} to {nowWidth}. {original.GlyphCount} letters "
                             + $"checked, {moved} other things moved.");
                Assert.Equal(0, moved);
            }
            finally
            {
                File.WriteAllBytes(files[which], File.ReadAllBytes(backup));
                File.Delete(backup);
            }

            // The font on disk is back exactly as it was, so this leaves the ROM alone.
            Assert.Equal(before, File.ReadAllBytes(files[which]));
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.ViewModels.Graphics;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// Taking a step back in the font editor, and moving a letter in and out as a picture. Nothing
    /// here writes to the ROM: only Save puts the font back, and these never call it.
    /// </summary>
    [Collection("rom")]
    public class FontEditorUndoAndPngTests
    {
        private readonly ITestOutputHelper _out;
        public FontEditorUndoAndPngTests(ITestOutputHelper o) => _out = o;

        private static FontEditorViewModel Open()
        {
            if (!Directory.Exists(TestRoms.Platinum)) return null;
            SettingsManager.Load();
            new RomInfo("CPUE", TestRoms.Platinum);
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.fonts });
            return new FontEditorViewModel();
        }

        /// <summary>A letter with ink in it, so a change to it is a change to something.</summary>
        private static int ADrawnLetter(FontEditorViewModel vm) =>
            vm.Glyphs.First(g => g.HasPicture).Index;

        [Fact]
        public void PaintingCanBeTakenBackAndPutAgain()
        {
            var vm = Open();
            if (vm == null) { _out.WriteLine("Platinum not unpacked here, skipped"); return; }
            if (vm.Glyphs.Count == 0) { Assert.Fail("no font was loaded, so this proved nothing"); }

            vm.SelectedGlyphIndex = ADrawnLetter(vm);
            Assert.False(vm.CanUndo, "there is nothing to take back before anything is painted");

            byte was = vm.PixelAt(3, 3);
            byte now = (byte)(was == 1 ? 2 : 1);
            _out.WriteLine($"letter {vm.SelectedGlyphIndex} pixel 3,3 was {was}, painting {now}");

            vm.SetPixel(3, 3, now);
            Assert.Equal(now, vm.PixelAt(3, 3));
            Assert.True(vm.CanUndo, "painting left nothing to take back");

            vm.Undo();
            Assert.Equal(was, vm.PixelAt(3, 3));
            Assert.True(vm.CanRedo, "taking it back left nothing to put again");

            vm.Redo();
            Assert.Equal(now, vm.PixelAt(3, 3));
            _out.WriteLine("took it back to " + was + " and put it again as " + now);
        }

        /// <summary>Changing how wide a letter is can be taken back too, not only the painting.</summary>
        [Fact]
        public void ChangingHowWideALetterIsCanBeTakenBack()
        {
            var vm = Open();
            if (vm == null) { _out.WriteLine("Platinum not unpacked here, skipped"); return; }

            vm.SelectedGlyphIndex = ADrawnLetter(vm);
            int was = vm.GlyphWidth;
            vm.GlyphWidth = was == 5 ? 6 : 5;
            Assert.NotEqual(was, vm.GlyphWidth);

            vm.Undo();
            _out.WriteLine($"width went {was} to {(was == 5 ? 6 : 5)} and back to {vm.GlyphWidth}");
            Assert.Equal(was, vm.GlyphWidth);
        }

        /// <summary>
        /// A letter written out and read straight back is the same letter. This is the check that catches
        /// a shade being written as one grey and read back as another, which would quietly change every
        /// pixel of a font somebody edited in a paint program.
        /// </summary>
        [Fact]
        public void ALetterGoesOutToAPictureAndComesBackTheSame()
        {
            var vm = Open();
            if (vm == null) { _out.WriteLine("Platinum not unpacked here, skipped"); return; }

            vm.SelectedGlyphIndex = ADrawnLetter(vm);
            int cell = DSPRE.ROMFiles.FieldFont.CellSize;

            var before = new byte[cell * cell];
            for (int y = 0; y < cell; y++)
                for (int x = 0; x < cell; x++)
                    before[y * cell + x] = vm.PixelAt(x, y);
            Assert.Contains(before, p => p == 1 || p == 2);

            string path = Path.Combine(Path.GetTempPath(), "dspre_letter_" + Guid.NewGuid().ToString("N") + ".png");
            try
            {
                Assert.Null(vm.ExportPng(path, wholeFont: false));
                Assert.True(File.Exists(path), "nothing was written");

                // scribble over it so reading back has to do the work
                for (int y = 0; y < cell; y++)
                    for (int x = 0; x < cell; x++)
                        vm.SetPixel(x, y, 0);

                Assert.Null(vm.ImportPng(path, wholeFont: false));

                int differ = 0;
                for (int y = 0; y < cell; y++)
                    for (int x = 0; x < cell; x++)
                        if (vm.PixelAt(x, y) != before[y * cell + x]) differ++;

                _out.WriteLine($"{cell * cell} pixels went out and came back, {differ} of them differently");
                Assert.Equal(0, differ);
            }
            finally { try { File.Delete(path); } catch { } }
        }

        /// <summary>The whole font goes out and comes back, every letter of it, not just the one on show.</summary>
        [Fact]
        public void TheWholeFontGoesOutAndComesBackTheSame()
        {
            var vm = Open();
            if (vm == null) { _out.WriteLine("Platinum not unpacked here, skipped"); return; }

            int cell = DSPRE.ROMFiles.FieldFont.CellSize;
            int letters = vm.Font.GlyphCount;
            Assert.True(letters > 100, $"only {letters} letters, so this proved little");

            var before = new byte[letters][];
            for (int g = 0; g < letters; g++)
            {
                before[g] = new byte[cell * cell];
                for (int y = 0; y < cell; y++)
                    for (int x = 0; x < cell; x++)
                        before[g][y * cell + x] = vm.Font.PixelAt(g, x, y);
            }

            string path = Path.Combine(Path.GetTempPath(), "dspre_font_" + Guid.NewGuid().ToString("N") + ".png");
            try
            {
                Assert.Null(vm.ExportPng(path, wholeFont: true));

                // a sheet must not be readable onto a single letter, or a slip wipes one out
                string refused = vm.ImportPng(path, wholeFont: false);
                Assert.False(string.IsNullOrEmpty(refused), "a whole sheet was accepted onto one letter");
                _out.WriteLine("reading a sheet onto one letter says: " + refused);

                Assert.Null(vm.ImportPng(path, wholeFont: true));

                int differ = 0;
                for (int g = 0; g < letters; g++)
                    for (int y = 0; y < cell; y++)
                        for (int x = 0; x < cell; x++)
                            if (vm.Font.PixelAt(g, x, y) != before[g][y * cell + x]) differ++;

                _out.WriteLine($"{letters} letters went out and came back, {differ} pixels differently");
                Assert.Equal(0, differ);
            }
            finally { try { File.Delete(path); } catch { } }
        }
    }
}

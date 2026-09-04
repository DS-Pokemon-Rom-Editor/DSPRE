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
    /// Finding one letter among five hundred. A font holds far more pictures than an English character
    /// map ever asks for, so the list needs filtering to be usable at all.
    ///
    /// It also has to stop saying "not in the character map" about pictures that are drawn. That reads as
    /// "nothing here" when the truth is the opposite: the picture exists, nothing writes it.
    /// </summary>
    [Collection("rom")]
    public class FontEditorFilterTests
    {
        private readonly ITestOutputHelper _out;
        public FontEditorFilterTests(ITestOutputHelper o) => _out = o;

        private static FontEditorViewModel Open(string project)
        {
            if (!Directory.Exists(project)) return null;
            SettingsManager.Load();
            new RomInfo("CPUE", project);
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.fonts });
            return new FontEditorViewModel();
        }

        [SkippableFact]
        public void AFontHoldsBothDrawnAndEmptyPicturesAndTheListSaysWhich()
        {
            var vm = Open(TestRoms.Platinum);
            Skip.If(vm == null, "Platinum not unpacked here");
            if (vm.Glyphs.Count == 0) { Assert.Fail("no font was loaded, so this proved nothing"); }

            int all = vm.Glyphs.Count;
            int drawn = vm.Glyphs.Count(g => g.HasPicture);
            int mapped = vm.Glyphs.Count(g => g.IsMapped);
            _out.WriteLine($"{all} letters: {drawn} with something drawn, {mapped} a character writes");

            // Both kinds have to exist or the filters have nothing to separate.
            Assert.True(drawn > 0 && drawn < all, $"{drawn} of {all} are drawn, so filtering proves nothing");
            Assert.True(mapped > 0 && mapped < all, $"{mapped} of {all} are mapped, so filtering proves nothing");

            // The three things a row can say, each about the right kind of letter.
            var drawnUnmapped = vm.Glyphs.First(g => g.HasPicture && !g.IsMapped);
            var empty = vm.Glyphs.First(g => !g.HasPicture && !g.IsMapped);
            _out.WriteLine($"letter {drawnUnmapped.Index} (drawn) says \"{drawnUnmapped.Describe}\", "
                         + $"letter {empty.Index} says \"{empty.Describe}\"");

            // Nearly every letter is drawn, so the list stays quiet about it and speaks up only for
            // the handful of empty slots. Announcing "drawn" on four hundred odd rows buried the
            // couple of dozen that are the interesting ones.
            Assert.Equal("", drawnUnmapped.Describe);
            Assert.Equal("empty", empty.Describe);
            Assert.DoesNotContain(vm.Glyphs.Where(g => g.HasPicture), g => g.Describe == "empty");
            Assert.All(vm.Glyphs.Where(g => g.IsMapped), g => Assert.Equal(g.Letter, g.Describe));
        }

        [SkippableFact]
        public void EachFilterShowsOnlyWhatItSays()
        {
            var vm = Open(TestRoms.Platinum);
            Skip.If(vm == null, "Platinum not unpacked here");
            if (vm.Glyphs.Count == 0) { Assert.Fail("no font was loaded, so this proved nothing"); }

            int all = vm.Glyphs.Count;

            vm.ShowWhat = 1;                       // only ones with something drawn
            Assert.All(vm.Glyphs, g => Assert.True(g.HasPicture));
            int drawn = vm.Glyphs.Count;

            vm.ShowWhat = 2;                       // only empty ones
            Assert.All(vm.Glyphs, g => Assert.False(g.HasPicture));
            int empty = vm.Glyphs.Count;

            vm.ShowWhat = 3;                       // only ones a character writes
            Assert.All(vm.Glyphs, g => Assert.True(g.IsMapped));
            int mapped = vm.Glyphs.Count;

            vm.ShowWhat = 4;                       // only ones no character writes
            Assert.All(vm.Glyphs, g => Assert.False(g.IsMapped));
            int unmapped = vm.Glyphs.Count;

            vm.ShowWhat = 0;
            Assert.Equal(all, vm.Glyphs.Count);

            _out.WriteLine($"of {all}: drawn {drawn}, empty {empty}, mapped {mapped}, unmapped {unmapped}");
            Assert.Equal(all, drawn + empty);
            Assert.Equal(all, mapped + unmapped);
            Assert.True(drawn > 0 && empty > 0, "one of the two kinds is missing, so this proved nothing");
        }

        [SkippableFact]
        public void TypingFindsALetterAndANumberJumpsStraightToIt()
        {
            var vm = Open(TestRoms.Platinum);
            Skip.If(vm == null, "Platinum not unpacked here");

            var target = vm.Glyphs.FirstOrDefault(g => g.IsMapped && g.Letter == "A");
            if (target == null) { Assert.Fail("this font has no letter A in the map, so this proved nothing"); }

            vm.Search = "A";
            _out.WriteLine($"looking for A leaves {vm.Glyphs.Count} of them");
            Assert.True(vm.Glyphs.Count > 0, "looking for A found nothing");
            Assert.All(vm.Glyphs, g => Assert.Contains("A", g.Letter, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(vm.Glyphs, g => g.Index == target.Index);

            vm.Search = target.Index.ToString();
            _out.WriteLine($"typing {target.Index} leaves {vm.Glyphs.Count}");
            Assert.Single(vm.Glyphs);
            Assert.Equal(target.Index, vm.Glyphs[0].Index);
            Assert.Equal(target.Index, vm.SelectedGlyphIndex);

            vm.Search = "";
            Assert.True(vm.Glyphs.Count > 1, "clearing the box did not bring the rest back");
        }

        [SkippableFact]
        public void PictureCommandsFollowSelectionAndWholeFontMode()
        {
            var vm = Open(TestRoms.Platinum);
            Skip.If(vm == null, "Platinum not unpacked here");
            Assert.True(vm.Glyphs.Count > 0, "no font was loaded, so this proved nothing");
            Assert.True(vm.CanUsePictureCommand);

            vm.Search = "a search no glyph can contain";
            Assert.Empty(vm.Glyphs);
            Assert.False(vm.HasGlyph);
            Assert.False(vm.CanUsePictureCommand);

            vm.WholeFontForPictures = true;
            Assert.True(vm.CanUsePictureCommand);
            vm.WholeFontForPictures = false;
            Assert.False(vm.CanUsePictureCommand);

            vm.Search = "";
            Assert.True(vm.Glyphs.Count > 0);
            Assert.True(vm.HasGlyph);
            Assert.True(vm.CanUsePictureCommand);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.ROMFiles;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>Writing a font back out again, which has to be safe before anything may save one.</summary>
    [Collection("rom")]
    public class FieldFontWriteTests
    {
        private readonly ITestOutputHelper _out;
        public FieldFontWriteTests(ITestOutputHelper o) => _out = o;

        private static readonly (string code, string path, string name)[] Games =
        {
            ("ADAE", @"C:\Romhacking\ROMs\NDS\DP\Pokemon Diamond (v05) (U)(Legacy)\1015 - Pokemon Diamond (v05) (U)(Legacy)_DSPRE_contents", "Diamond"),
            ("CPUE", @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents", "Platinum"),
            ("IPKE", @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents", "HeartGold"),
        };

        /// <summary>Every entry of the font archive that is a font, in every game unpacked here.</summary>
        private static IEnumerable<(int entry, byte[] raw, FieldFont font)> FontsOf()
        {
            string dir = RomInfo.gameDirs[RomInfo.DirNames.fonts].unpackedDir;
            if (!Directory.Exists(dir)) yield break;
            var files = RomFiles.Settled(dir);
            for (int i = 0; i < files.Length; i++)
            {
                byte[] raw = File.ReadAllBytes(files[i]);
                var font = FieldFont.Read(raw);
                if (font != null) yield return (i, raw, font);
            }
        }

        /// <summary>
        /// A font read and written straight back must be the same bytes. Reading does not look at every
        /// byte of the header and some entries carry something after the width table, so rebuilding from
        /// the parsed parts alone would drop them without the file changing size in any obvious way.
        /// </summary>
        [Fact]
        public void EveryFontInEveryGameGoesOutExactlyAsItCameIn()
        {
            int games = 0, fonts = 0;
            foreach (var (code, path, name) in Games)
            {
                if (!Directory.Exists(path)) { _out.WriteLine($"{name}: not unpacked here, skipped"); continue; }
                try { new RomInfo(code, path); }
                catch (Exception ex) { _out.WriteLine($"{name}: would not load ({ex.Message}), skipped"); continue; }
                DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.fonts });
                games++;

                int here = 0;
                foreach (var (entry, raw, font) in FontsOf())
                {
                    byte[] again = font.Write();
                    Assert.True(raw.Length == again.Length,
                        $"{name} font {entry}: {raw.Length} bytes in, {again.Length} out");
                    int at = -1;
                    for (int i = 0; i < raw.Length; i++)
                        if (raw[i] != again[i]) { at = i; break; }
                    Assert.True(at < 0, $"{name} font {entry}: byte {at} changed, {raw[at < 0 ? 0 : at]:X2} became {again[at < 0 ? 0 : at]:X2}");
                    here++; fonts++;
                }
                Assert.True(here > 0, name + ": no font was found in the font archive");
                _out.WriteLine($"{name}: {here} fonts, all byte for byte");
            }
            Assert.True(games > 0, "no game was unpacked here, so nothing was checked");
            _out.WriteLine($"{games} games, {fonts} fonts checked");
        }

        /// <summary>A changed pixel and a changed width both survive the trip, and nothing else moves.</summary>
        [Fact]
        public void AnEditedLetterComesBackChangedAndTheRestDoesNot()
        {
            string folder = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";
            if (!Directory.Exists(folder)) { _out.WriteLine("HeartGold not unpacked here"); return; }
            new RomInfo("IPKE", folder);
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.fonts });

            var first = FontsOf().FirstOrDefault();
            Assert.True(first.font != null, "no font was found to edit");
            var font = first.font;

            byte was = font.PixelAt(5, 3, 4);
            byte now = (byte)(was == 2 ? 1 : 2);
            font.SetPixel(5, 3, 4, now);
            font.SetWidth(5, 9);

            var back = FieldFont.Read(font.Write());
            Assert.NotNull(back);
            Assert.Equal(now, back.PixelAt(5, 3, 4));
            Assert.Equal(9, back.WidthOf(5));

            // Its neighbours are untouched.
            Assert.Equal(font.PixelAt(4, 3, 4), back.PixelAt(4, 3, 4));
            Assert.Equal(font.PixelAt(6, 3, 4), back.PixelAt(6, 3, 4));
            Assert.Equal(font.WidthOf(4), back.WidthOf(4));
            _out.WriteLine($"letter 5 pixel 3,4 went from {was} to {now} and its width to 9, neighbours unchanged");
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels.Graphics;
using DSPRE.ROMFiles;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>The Cell Animations picker finds animations in every archive and offers each party icon in its own colours.</summary>
    [Collection("rom")]
    public class CellAnimationEverywhereTests
    {
        private readonly ITestOutputHelper _out;
        public CellAnimationEverywhereTests(ITestOutputHelper o) => _out = o;

        [SkippableFact]
        public void DiamondOffersEveryAnimationFileInTheRom()
        {
            Skip.IfNot(Directory.Exists(TestRoms.Diamond), "Diamond is not unpacked here");
            new RomInfo("ADAE", TestRoms.Diamond);

            var found = CellAnimationPickerViewModel.Everywhere();
            var files = found.Where(f => f.Source.Dir != DirNames.monIcons)
                             .Select(f => (f.ArchiveName, f.Animation)).Distinct().Count();
            int loose = found.Count(f => f.Source.LoosePath != null);
            var icons = found.Where(f => f.Source.Dir == DirNames.monIcons).ToList();
            _out.WriteLine($"Diamond: {files} animation files outside the icons, {loose} rows from unmapped archives, {icons.Count} icon rows");

            // Diamond has 422 animation files; three are the shared party icon animations.
            Assert.InRange(files, 400, 419);
            Assert.True(loose > 250, $"only {loose} rows came from archives DSPRE does not map");
            Assert.DoesNotContain(found, f => f.Source.Dir == DirNames.dynamicHeaders);

            Assert.Equal(540 - (PokemonIconFiles.SharedFiles + 1), icons.Count);
            Assert.All(icons, r => Assert.Equal(DSUtils.GetMonIconPaletteId(r.Sprites - PokemonIconFiles.SharedFiles), r.PaletteRow));
            Assert.Contains(icons, r => r.PaletteRow > 0);
            // Species names are in capitals in the ROM.
            Assert.Contains(icons, r => string.Equals(r.Label, "Unown, B", StringComparison.OrdinalIgnoreCase));
        }

        [SkippableFact]
        public void AnUnmappedArchiveSavesInPlaceKeepingEveryOtherFile()
        {
            Skip.IfNot(Directory.Exists(TestRoms.Diamond), "Diamond is not unpacked here");
            new RomInfo("ADAE", TestRoms.Diamond);

            string original = Path.Combine(dataPath, "application", "custom_ball", "data", "cb_data.narc");
            Skip.IfNot(File.Exists(original), "Diamond's seal data archive is not where it is expected");
            string copy = Path.Combine(Path.GetTempPath(), "dspre_loose_" + Guid.NewGuid().ToString("N") + ".narc");
            File.Copy(original, copy);
            try
            {
                var before = ArchiveFiles.Loose(copy, "copy");
                int count = before.Count;
                var old = Enumerable.Range(0, count).Select(before.Get).ToArray();
                byte[] changed = old[1].Reverse().ToArray();

                before.Put(new Dictionary<int, byte[]> { [1] = changed });

                var after = ArchiveFiles.Loose(copy, "copy");
                Assert.Equal(count, after.Count);
                Assert.Equal(changed, after.Get(1));
                for (int i = 0; i < count; i++)
                    if (i != 1) Assert.Equal(old[i], after.Get(i));
            }
            finally { File.Delete(copy); }
        }
    }
}

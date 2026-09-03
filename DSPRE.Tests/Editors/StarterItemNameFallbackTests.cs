using System;
using System.Collections.Generic;
using System.IO;
using DSPRE;
using DSPRE.ROMFiles;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// The editor writes the held item by its readable name, which rotom knows as a built-in rather than
    /// from anything in the project, so there is no way to check the spelling in advance. It tries the
    /// name and falls back to the plain number. Both halves have to work, on a COPY of the project.
    /// </summary>
    [Collection("rom")]
    public class StarterItemNameFallbackTests
    {
        private readonly ITestOutputHelper _out;
        public StarterItemNameFallbackTests(ITestOutputHelper o) => _out = o;

        private static readonly string Platinum = TestRoms.Platinum;

        [Theory]
        [InlineData("Oran Berry", "ITEM_ORAN_BERRY")]
        [InlineData("King's Rock", "ITEM_KING_S_ROCK")]
        [InlineData("  Potion  ", "ITEM_POTION")]
        [InlineData("", null)]
        public void ANameIsTurnedIntoTheSpellingRotomUses(string shown, string expected) =>
            Assert.Equal(expected, StarterRotomSource.ItemToken(shown));

        /// <summary>
        /// A name rotom accepts is written as a name; one it does not is written as the number instead,
        /// and the save still lands. Without the fallback a mis-spelled item would fail the whole save.
        /// </summary>
        [Fact]
        public void AGoodNameIsWrittenAndABadOneFallsBackToTheNumber()
        {
            if (!Directory.Exists(Platinum)) { _out.WriteLine("Platinum not unpacked here, skipped"); return; }

            string copy = Path.Combine(Path.GetDirectoryName(Platinum), "dspre_item_" + Guid.NewGuid().ToString("N"));
            try
            {
                CopyTree(Platinum, copy);
                SettingsManager.Load();
                new RomInfo("CPUE", copy);
                DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.scripts });
                if (!StarterRotomSource.IsAvailable)
                { _out.WriteLine("this project has no decompiled sources, skipped"); return; }

                var starter = StarterRotomSource.FindStarter();
                Assert.NotNull(starter);

                string failure = StarterRotomSource.SaveAsync(starter, 7, "ITEM_ORAN_BERRY", 155)
                                                   .GetAwaiter().GetResult();
                Assert.True(failure == null, "saving a good name reported: " + failure);
                var after = StarterRotomSource.FindStarter();
                _out.WriteLine("with a name rotom knows: " + after.Line.Trim());
                Assert.Equal("ITEM_ORAN_BERRY", after.HeldItemArgument);
                Assert.Equal(7, after.Level);

                // A spelling rotom has never heard of, with the real number alongside it.
                failure = StarterRotomSource.SaveAsync(after, 9, "ITEM_NOT_A_REAL_ITEM_AT_ALL", 155)
                                            .GetAwaiter().GetResult();
                Assert.True(failure == null, "the fallback reported: " + failure);

                var last = StarterRotomSource.FindStarter();
                _out.WriteLine("with a name it does not know: " + last.Line.Trim());
                Assert.Equal("155", last.HeldItemArgument);
                Assert.Equal(9, last.Level);
            }
            finally
            {
                // Put the shared RomInfo back on the real project: the tests after this one in the
                // collection would otherwise be pointed at a folder that is about to be deleted.
                try { new RomInfo("CPUE", Platinum); } catch { }
                try { Directory.Delete(copy, true); } catch { }
            }
        }

        private static void CopyTree(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (string d in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(d.Replace(from, to));
            foreach (string f in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
                File.Copy(f, f.Replace(from, to), true);
        }
    }
}

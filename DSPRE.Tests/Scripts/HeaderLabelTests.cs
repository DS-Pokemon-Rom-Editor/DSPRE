using System;
using System.IO;
using System.Linq;
using DSPRE;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>What a header is called wherever one is picked.</summary>
    [Collection("rom")]
    public class HeaderLabelTests
    {
        private readonly ITestOutputHelper _out;
        public HeaderLabelTests(ITestOutputHelper o) => _out = o;

        private static readonly (string code, string path, string name)[] Games =
        {
            ("ADAE", @"C:\Romhacking\ROMs\NDS\DP\Pokemon Diamond (v05) (U)(Legacy)\1015 - Pokemon Diamond (v05) (U)(Legacy)_DSPRE_contents", "Diamond"),
            ("CPUE", @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents", "Platinum"),
            ("IPKE", @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents", "HeartGold"),
        };

        /// <summary>
        /// Every header should carry the place it is, in every game. Reading the place through the
        /// dynamic-headers folder when the patch is not applied answers for almost none of them, and the
        /// list quietly falls back to internal codes.
        /// </summary>
        [Fact]
        public void EveryHeaderIsNamedForThePlaceItIs()
        {
            int checkedGames = 0;
            foreach (var (code, path, name) in Games)
            {
                if (!Directory.Exists(path)) { _out.WriteLine($"{name}: not unpacked here, skipped"); continue; }
                try { new RomInfo(code, path); }
                catch (Exception ex) { _out.WriteLine($"{name}: would not load ({ex.Message}), skipped"); continue; }
                HeaderLabels.Forget();
                checkedGames++;

                var labels = HeaderLabels.Friendly();
                var plain = HeaderLists.GetHeaderListBoxNames();
                Assert.NotEmpty(labels);
                Assert.Equal(plain.Count, labels.Count);

                // A label carries a place only if something was added to the bare number and code.
                // The bare form already has spaces in it, so length is what separates the two.
                int named = 0;
                for (int i = 0; i < labels.Count; i++)
                    if (labels[i].Length > plain[i].TrimEnd((char)0).TrimEnd().Length) named++;
                _out.WriteLine($"{name}: {named} of {labels.Count} headers carry a place name");
                Assert.Equal(labels.Count, named);
            }
            Assert.True(checkedGames > 0, "no game was unpacked here, so nothing was checked");
            _out.WriteLine($"{checkedGames} games checked");
        }

        /// <summary>A folder for dynamic headers is not the same as the patch being applied.</summary>
        [Fact]
        public void HavingTheFolderIsNotTheSameAsHavingThePatch()
        {
            string folder = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";
            if (!Directory.Exists(folder)) { _out.WriteLine("HeartGold not unpacked here"); return; }
            new RomInfo("IPKE", folder);

            Assert.True(RomInfo.gameDirs.ContainsKey(RomInfo.DirNames.dynamicHeaders),
                        "this ROM is expected to have the folder");
            Assert.False(HeaderLabels.DynamicHeaders, "but not the patch");
        }
    }
}

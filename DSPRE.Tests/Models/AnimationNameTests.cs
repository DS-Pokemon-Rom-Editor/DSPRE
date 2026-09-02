using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using DSPRE.ROMFiles;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// Movement files carry the name of every animation in them, and DSPRE was listing them by number.
    /// </summary>
    [Collection("rom")]
    public class AnimationNameTests
    {
        private readonly ITestOutputHelper _out;
        public AnimationNameTests(ITestOutputHelper o) { _out = o; }

        public static IEnumerable<object[]> Games => new List<object[]>
        {
            new object[] { "IPKE", @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents", "HeartGold" },
            new object[] { "CPUE", @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents", "Platinum" },
        };

        [Theory]
        [MemberData(nameof(Games))]
        public void EveryMovementFileSaysWhatItsAnimationsAreCalled(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);

            int files = 0, animations = 0, named = 0;
            var examples = new List<string>();
            var blank = new List<string>();

            foreach (var a in ModelAssets.All)
            {
                int n;
                try { n = ModelAssets.Count(a); } catch { continue; }
                if (n == 0) continue;
                var narc = new ScriptNarc(a.Dir);

                for (int i = 0; i < n; i++)
                {
                    var b = narc.Get(i);
                    if (b == null || ModelAssets.Identify(b) != ModelAssets.Kind.JointAnimation) continue;
                    files++;

                    var names = JointAnimation.NamesIn(b);
                    foreach (string name in names)
                    {
                        animations++;
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            named++;
                            if (examples.Count < 8) examples.Add($"{a.Title}[{i}] \"{name}\"");
                        }
                        else if (blank.Count < 4) blank.Add($"{a.Title}[{i}]");
                    }
                }
            }

            _out.WriteLine($"{game}: {files} movement files holding {animations} animations, "
                         + $"{named} of them named");
            foreach (var e in examples) _out.WriteLine("   " + e);
            foreach (var b in blank) _out.WriteLine("   no name: " + b);

            // Platinum keeps 32 of these and HeartGold 89, so the bar sits below both rather than at a
            // number one of them happens to clear.
            Assert.True(files > 25, $"{game}: only {files} movement files found, this proved little");
            Assert.True(animations > 0, $"{game}: no animations read at all");

            // If most of them had no name there would be nothing to show, and the claim that the names
            // are in the files would be wrong.
            Assert.True(named * 2 > animations,
                $"{game}: only {named} of {animations} animations carry a name");
        }

        /// <summary>The check proves able to fail: a file that is not a movement file yields no names.</summary>
        [Fact]
        public void SomethingThatIsNotAMovementFileHasNoNames()
        {
            Assert.Empty(JointAnimation.NamesIn(null));
            Assert.Empty(JointAnimation.NamesIn(new byte[] { 0x42, 0x4D, 0x44, 0x30, 1, 2, 3, 4 }));
            _out.WriteLine("a model file and a null both give no animation names");
        }
    }
}

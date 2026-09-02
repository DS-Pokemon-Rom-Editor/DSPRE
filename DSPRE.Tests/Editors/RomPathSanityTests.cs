using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// Where the ROM's archives live. A path here that is not a path stops the ROM being built at all,
    /// and the only sign of it is a message about a null character, so it is worth checking directly.
    /// </summary>
    [Collection("rom")]
    public class RomPathSanityTests
    {
        private readonly ITestOutputHelper _out;
        public RomPathSanityTests(ITestOutputHelper o) { _out = o; }

        private static readonly (string Code, string Project)[] Games =
        {
            ("IPKE", @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents"),
            ("CPUE", @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents"),
            ("ADAE", @"C:\Romhacking\ROMs\NDS\DP\Pokemon Diamond (v05) (U)(Legacy)\1015 - Pokemon Diamond (v05) (U)(Legacy)_DSPRE_contents"),
        };

        [Fact]
        public void EveryArchiveInEveryGamePointsAtSomethingThatCouldBeAPath()
        {
            int games = 0, checked_ = 0;
            var wrong = new List<string>();

            foreach (var (code, project) in Games)
            {
                if (!Directory.Exists(project)) continue;
                try { new RomInfo(code, project); } catch { continue; }
                games++;

                foreach (var kv in RomInfo.gameDirs)
                {
                    checked_++;
                    foreach (var (what, path) in new[]
                             { ("unpacked", kv.Value.unpackedDir), ("packed", kv.Value.packedDir) })
                    {
                        if (string.IsNullOrEmpty(path)) continue;
                        // A control character in a path is always a mistake in a written-out path, and
                        // it is what a mistyped escape leaves behind.
                        int at = path.ToCharArray().ToList().FindIndex(c => c < 32);
                        if (at >= 0)
                            wrong.Add($"{code} {kv.Key} {what} holds character {(int)path[at]} at {at}: "
                                    + Readable(path));
                        else if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                            wrong.Add($"{code} {kv.Key} {what} is not a usable path: " + Readable(path));
                    }
                }
            }

            _out.WriteLine($"{games} games opened, {checked_} archives checked.");
            // A run that opened no game would pass while checking nothing.
            Assert.True(games > 0, "no game project was there to open, so this proved nothing");
            Assert.True(checked_ > 50, $"only {checked_} archives were checked");
            Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong.Take(8)));
        }

        private static string Readable(string s) =>
            new string(s.Select(c => c < 32 ? '?' : c).ToArray());
    }
}

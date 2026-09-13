using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Images;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>A character graphic read and written straight back comes out byte for byte the same.</summary>
    [Collection("rom")]
    public class NcgrWriteTests
    {
        private readonly ITestOutputHelper _out;
        public NcgrWriteTests(ITestOutputHelper o) => _out = o;

        public static IEnumerable<object[]> Games => new[]
        {
            new object[] { "Diamond", TestRoms.Diamond },
            new object[] { "Platinum", TestRoms.Platinum },
            new object[] { "HeartGold", TestRoms.HeartGold },
        };

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void EveryCharacterGraphicWritesBackUnchanged(string game, string project)
        {
            string unpacked = Path.Combine(project, "unpacked");
            Skip.If(!Directory.Exists(unpacked), $"{game} test project not configured");

            int written = 0, twoSections = 0;
            var changed = new List<string>();
            var unreadable = new List<string>();
            string copy = Path.Combine(Path.GetTempPath(), $"dspre-ncgr-{Guid.NewGuid():N}");
            try
            {
                foreach (string path in Directory.EnumerateFiles(unpacked, "*", SearchOption.AllDirectories))
                {
                    if (!StartsWithCharacterMagic(path)) continue;
                    byte[] original = File.ReadAllBytes(path);

                    NCGR tile;
                    try { tile = new NCGR(path, 0, Path.GetFileName(path)); }
                    catch (Exception ex) { unreadable.Add($"{Relative(unpacked, path)}: {ex.Message}"); continue; }

                    if (File.Exists(copy)) File.Delete(copy);
                    tile.Write(copy, null);
                    written++;
                    if (BitConverter.ToUInt16(original, 0x0E) == 2) twoSections++;

                    byte[] after = File.ReadAllBytes(copy);
                    if (!after.SequenceEqual(original))
                    {
                        int at = Enumerable.Range(0, Math.Min(after.Length, original.Length)).FirstOrDefault(k => after[k] != original[k]);
                        changed.Add($"{Relative(unpacked, path)}: {original.Length} to {after.Length} bytes, first difference at 0x{at:X}");
                    }
                }
            }
            finally
            {
                if (File.Exists(copy)) File.Delete(copy);
            }

            _out.WriteLine($"{game}: {written} written back ({twoSections} with a second section), {changed.Count} changed, {unreadable.Count} unreadable");
            foreach (var c in changed.Take(5)) _out.WriteLine("  changed: " + c);
            foreach (var u in unreadable.Take(5)) _out.WriteLine("  unreadable: " + u);

            Assert.True(written > 0, $"{game}: no character graphic was found, so this proves nothing");
            Assert.True(twoSections > 0, $"{game}: no two-section graphic was found, so the second section's size was never checked");
            Assert.True(changed.Count == 0, $"{game}: {changed.Count} did not write back unchanged: " + string.Join("; ", changed.Take(5)));
        }

        private static bool StartsWithCharacterMagic(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                if (fs.Length < 0x30) return false;
                Span<byte> magic = stackalloc byte[4];
                return fs.Read(magic) == 4 && magic[0] == 'R' && magic[1] == 'G' && magic[2] == 'C' && magic[3] == 'N';
            }
            catch { return false; }
        }

        private static string Relative(string root, string path) => Path.GetRelativePath(root, path);
    }
}

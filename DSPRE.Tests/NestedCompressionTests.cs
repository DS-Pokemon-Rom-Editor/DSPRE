using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// Whether anything in the graphics archives is squeezed down inside something else that is also
    /// squeezed down, and whether an edit keeps it that way.
    ///
    /// DSPRE opens a squeezed file, edits it, and squeezes it again, checking as it goes that what comes
    /// back out is what went in. That is one level. The question here is whether any of these archives
    /// hold an archive of their own, which would need the same care a second time.
    /// </summary>
    [Collection("rom")]
    public class NestedCompressionTests
    {
        private readonly ITestOutputHelper _out;
        public NestedCompressionTests(ITestOutputHelper o) { _out = o; }

        public static IEnumerable<object[]> Games => new List<object[]>
        {
            new object[] { "IPKE", @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents", "HeartGold" },
            new object[] { "CPUE", @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents", "Platinum" },
        };

        private static string Tag(byte[] b)
            => b == null || b.Length < 4 ? "----"
               : new string(new[] { (char)b[0], (char)b[1], (char)b[2], (char)b[3] });

        /// <summary>
        /// Does any graphics archive hold an archive of its own, squeezed or not?
        ///
        /// This settles whether "keep it squeezed through nesting" is a real case in these games or a
        /// thing that only happens elsewhere.
        /// </summary>
        [Theory]
        [MemberData(nameof(Games))]
        public void ReportWhetherAnythingNests(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);
            GraphicAssets.Forget();

            int looked = 0, squeezed = 0, nested = 0, squeezedNested = 0;
            var examples = new List<string>();

            foreach (var a in GraphicAssets.All)
            {
                int n;
                try { n = GraphicAssets.Count(a); } catch { continue; }
                if (n == 0) continue;
                var narc = new ScriptNarc(a.Dir);

                // Every entry of the small archives, a spread through the large ones: enough to find a
                // nested archive if one kind of entry is nested at all.
                int step = n > 400 ? n / 400 : 1;
                for (int i = 0; i < n; i += step)
                {
                    var b = narc.Get(i);
                    if (b == null || b.Length < 8) continue;
                    looked++;

                    bool isSqueezed = GraphicAssets.SqueezeMarker(b) != 0;
                    if (isSqueezed) squeezed++;

                    var inner = isSqueezed ? GraphicAssets.Unsqueeze(b) : b;
                    if (Tag(inner) != "NARC") continue;

                    nested++;
                    if (isSqueezed) squeezedNested++;
                    if (examples.Count < 6)
                        examples.Add($"{a.Title}[{i}] {(isSqueezed ? "squeezed " : "")}NARC "
                                   + $"{inner.Length} bytes");
                }
            }

            // The 3D archives too, so the answer covers everything DSPRE opens rather than the flat
            // graphics alone.
            foreach (var a in ModelAssets.All)
            {
                int n;
                try { n = ModelAssets.Count(a); } catch { continue; }
                if (n == 0) continue;
                var narc = new ScriptNarc(a.Dir);
                int step = n > 200 ? n / 200 : 1;
                for (int i = 0; i < n; i += step)
                {
                    var b = narc.Get(i);
                    if (b == null || b.Length < 8) continue;
                    looked++;
                    bool isSqueezed = GraphicAssets.SqueezeMarker(b) != 0;
                    if (isSqueezed) squeezed++;
                    var inner = isSqueezed ? GraphicAssets.Unsqueeze(b) : b;
                    if (Tag(inner) != "NARC") continue;
                    nested++;
                    if (isSqueezed) squeezedNested++;
                    if (examples.Count < 6) examples.Add($"{a.Title}[{i}] nested NARC");
                }
            }

            _out.WriteLine($"{game}: {looked} entries looked at across the flat and 3D archives, "
                         + $"{squeezed} squeezed, {nested} hold an archive of their own, "
                         + $"{squeezedNested} of those squeezed");
            foreach (var e in examples) _out.WriteLine("   " + e);

            Assert.True(looked > 3000, $"{game}: only {looked} entries were looked at, this proved little");

            // Recorded as a fact about these games, not as a wish: if a later game does nest,
            // this fails and the nesting has to be handled rather than assumed away.
            Assert.Equal(0, nested);
        }

        /// <summary>
        /// A squeezed entry that is edited comes back squeezed, and reads back as what was written.
        ///
        /// This is the one level that does happen, and it is what the move effect archives need: they are
        /// all stored squeezed, so an edit that wrote them back plain would leave the game reading the
        /// wrong thing.
        /// </summary>
        [Theory]
        [MemberData(nameof(Games))]
        public void AnEditedSqueezedEntryStaysSqueezed(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);
            GraphicAssets.Forget();

            // A drawing that is stored squeezed and that DSPRE can take apart.
            foreach (var a in GraphicAssets.All)
            {
                if (a.CannotImportBecause != null) continue;
                int n;
                try { n = GraphicAssets.Count(a); } catch { continue; }
                if (n == 0) continue;
                var narc = new ScriptNarc(a.Dir);

                for (int i = 0; i < Math.Min(n, 200); i++)
                {
                    var raw = narc.Get(i);
                    if (raw == null || GraphicAssets.SqueezeMarker(raw) != 0x10) continue;

                    var ix = GraphicAssets.ReadIndexed(a, i, out _);
                    if (ix?.Indices == null || ix.Indices.Length == 0) continue;

                    byte[] before = raw.ToArray();
                    try
                    {
                        // Write the pixels back exactly as they are: the file must come back squeezed and
                        // hold the same picture.
                        string why = GraphicAssets.WriteIndices(a, i, ix.Indices, ix);
                        if (why != null) continue;

                        var now = narc.Get(i);
                        Assert.NotNull(now);
                        Assert.True(GraphicAssets.SqueezeMarker(now) == 0x10,
                            $"{game}: {a.Title}[{i}] was squeezed and came back plain");

                        var after = GraphicAssets.ReadIndexed(a, i, out _);
                        Assert.NotNull(after);
                        Assert.True(ix.Indices.SequenceEqual(after.Indices),
                            $"{game}: {a.Title}[{i}] read back different pixels");

                        _out.WriteLine($"{game}: {a.Title}[{i}] {before.Length} bytes squeezed in, "
                                     + $"{now.Length} out, same picture");
                        return;
                    }
                    finally { narc.Put(i, before); }
                }
            }

            _out.WriteLine($"{game}: found no squeezed drawing to try, so this proved nothing");
        }
    }
}

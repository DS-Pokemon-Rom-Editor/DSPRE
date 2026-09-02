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
    /// The rows the graphics editor shows for item icons. The games give several items one drawing and
    /// a set of colours each, so a row per drawing loses all but the first of them and leaves their
    /// colours as unnamed files. A row per icon keeps them.
    /// </summary>
    [Collection("rom")]
    public class ItemIconRowTests
    {
        private readonly ITestOutputHelper _out;
        public ItemIconRowTests(ITestOutputHelper o) => _out = o;

        private static readonly (string code, string path, string name)[] Games =
        {
            ("ADAE", @"C:\Romhacking\ROMs\NDS\DP\Pokemon Diamond (v05) (U)(Legacy)\1015 - Pokemon Diamond (v05) (U)(Legacy)_DSPRE_contents", "Diamond"),
            ("CPUE", @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents", "Platinum"),
            ("IPKE", @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents", "HeartGold"),
        };

        private static bool Open(string code, string path)
        {
            if (!Directory.Exists(path)) return false;
            try { new RomInfo(code, path); } catch { return false; }
            return true;
        }

        private static (GraphicAssets.Archive a, int files, List<GraphicAssets.Unit> rows) Rows()
        {
            var a = GraphicAssets.All.First(x => x.Dir == RomInfo.DirNames.itemIcons);
            int files = Directory.GetFiles(RomInfo.gameDirs[RomInfo.DirNames.itemIcons].unpackedDir).Length;
            return (a, files, a.BuildUnits(files));
        }

        /// <summary>
        /// Every file gets a row. A file in no row cannot be found or edited at all, which is what
        /// happened to the colours of every item that shares its drawing with another.
        /// </summary>
        [Fact]
        public void EveryFileIsInSomeRow()
        {
            int played = 0;
            foreach (var (code, path, name) in Games)
            {
                if (!Open(code, path)) { _out.WriteLine($"{name}: not unpacked here, skipped"); continue; }
                var (_, files, rows) = Rows();

                var seen = new Dictionary<int, int>();
                foreach (var u in rows)
                    foreach (var p in u.Parts)
                        seen[p.Index] = seen.TryGetValue(p.Index, out int n) ? n + 1 : 1;

                // A drawing several items share is in one row each, on purpose. Colours are not shared
                // that way, and every file must appear at least once.
                var missing = Enumerable.Range(0, files).Where(i => !seen.ContainsKey(i)).ToList();
                _out.WriteLine($"{name}: {files} files, {rows.Count} rows, {missing.Count} in no row");
                Assert.True(missing.Count == 0,
                    $"{name}: these files are in no row: {string.Join(", ", missing.Take(20))}");
                played++;
            }
            Assert.True(played > 0, "no game was unpacked here, so this proved nothing");
        }

        /// <summary>
        /// The four status healers are the case that used to break: one bottle drawing, four sets of
        /// colours. Every one of them has to have a row of its own, with its own colours.
        /// </summary>
        [Fact]
        public void ItemsSharingADrawingEachKeepTheirOwnRow()
        {
            int played = 0;
            foreach (var (code, path, name) in Games)
            {
                if (!Open(code, path)) { _out.WriteLine($"{name}: not unpacked here, skipped"); continue; }

                var byDrawing = GraphicAssets.ItemIcons.Icons()
                    .GroupBy(i => i.Drawing).Where(g => g.Count() > 1).ToList();
                Assert.True(byDrawing.Count > 0,
                    $"{name}: no drawing is shared at all, so this test cannot see the fault it is for");

                var (_, files, rows) = Rows();
                foreach (var group in byDrawing)
                {
                    // Each icon on this drawing has its own colours, and its own row naming it.
                    var colours = group.Select(i => i.Colours).ToList();
                    Assert.Equal(colours.Count, colours.Distinct().Count());
                    foreach (var icon in group)
                    {
                        var row = rows.FirstOrDefault(u => u.Parts.Any(p => p.Index == icon.Drawing)
                                                        && u.Parts.Any(p => p.Index == icon.Colours));
                        Assert.True(row != null,
                            $"{name}: nothing shows drawing {icon.Drawing} with colours {icon.Colours}");
                    }
                }
                _out.WriteLine($"{name}: {byDrawing.Count} drawings are shared, "
                             + $"{byDrawing.Sum(g => g.Count())} icons between them");
                played++;
            }
            Assert.True(played > 0, "no game was unpacked here, so this proved nothing");
        }

        /// <summary>
        /// The first two files and the last two are not any item's icon, and used to show as the archive's
        /// own title with a file number. The archive's own index names them.
        /// </summary>
        [Fact]
        public void TheFilesThatAreNotAnItemAreNamed()
        {
            int played = 0;
            foreach (var (code, path, name) in Games)
            {
                if (!Open(code, path)) { _out.WriteLine($"{name}: not unpacked here, skipped"); continue; }
                var (a, files, rows) = Rows();

                string RowNaming(int file) =>
                    rows.FirstOrDefault(u => u.Parts.Any(p => p.Index == file))?.Name;

                Assert.Equal("Item icon animation", RowNaming(0));
                Assert.Equal("Item icon layout", RowNaming(1));
                Assert.Equal("Back arrow", RowNaming(files - 1));
                Assert.Equal("Back arrow", RowNaming(files - 2));

                // Nothing is left named after the archive itself, which is what a row nobody accounted
                // for used to look like.
                var vague = rows.Where(u => u.Name == a.Title).ToList();
                _out.WriteLine($"{name}: {vague.Count} rows still named after the archive");
                Assert.Empty(vague);
                played++;
            }
            Assert.True(played > 0, "no game was unpacked here, so this proved nothing");
        }

        /// <summary>Every item icon in the archive is drawn, at the thirty two square the bag draws it at.</summary>
        [Fact]
        public void EveryItemIconIsDrawnAtThirtyTwoSquare()
        {
            int played = 0, drawn = 0;
            foreach (var (code, path, name) in Games)
            {
                if (!Open(code, path)) { _out.WriteLine($"{name}: not unpacked here, skipped"); continue; }
                var (a, files, _) = Rows();

                var wrong = new List<string>();
                foreach (var icon in GraphicAssets.ItemIcons.Icons().DistinctBy(i => i.Drawing))
                {
                    if (icon.Drawing >= files) continue;
                    var p = GraphicAssets.Render(a, icon.Drawing);
                    if (p?.Rgba == null) { wrong.Add($"{icon.Name}: {p?.Whynot}"); continue; }
                    if (p.Width != 32) wrong.Add($"{icon.Name}: {p.Width}x{p.Height}");
                    drawn++;
                }
                _out.WriteLine($"{name}: {drawn} drawings, {wrong.Count} wrong");
                Assert.True(wrong.Count == 0, string.Join("; ", wrong.Take(8)));
                played++;
            }
            Assert.True(played > 0, "no game was unpacked here, so this proved nothing");
        }
    }
}

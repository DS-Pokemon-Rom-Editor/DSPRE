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
    /// Every picture the graphics browser can draw out of a real ROM, rebuilt from its own pixels and
    /// read back, rather than a handful chosen by hand. A picture that goes through the builder and
    /// comes back different is the fault this is looking for.
    /// </summary>
    [Collection("rom")]
    public class TilesetBuilderSweepTests
    {
        private readonly ITestOutputHelper _out;
        public TilesetBuilderSweepTests(ITestOutputHelper o) { _out = o; }

        private const string Project = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";

        private static bool Ready()
        {
            if (!Directory.Exists(Project)) return false;
            try { new RomInfo("IPKE", Project); } catch { return false; }
            return true;
        }

        [Fact]
        public void EveryPictureInTheRomRebuildsIntoTilesAndComesBackTheSame()
        {
            Assert.True(Ready(), "The HeartGold project this sweep reads is not there, so it proved nothing.");

            int drawn = 0, tried = 0, matched = 0, refused = 0;
            var reasons = new Dictionary<string, int>();
            var worst = new List<string>();
            long totalSquares = 0, totalTiles = 0;

            foreach (var archive in GraphicAssets.All)
            {
                var narc = new ScriptNarc(archive.Dir);
                if (!narc.Available) continue;

                for (int i = 0; i < narc.Count; i++)
                {
                    GraphicAssets.Preview shown;
                    try { shown = GraphicAssets.Render(archive, i); }
                    catch { continue; }
                    if (shown?.Rgba == null || shown.Width <= 0 || shown.Height <= 0) continue;
                    drawn++;

                    var built = TilesetBuilder.Build(shown.Rgba, shown.Width, shown.Height,
                                                     eightBit: false, keepClearSlot: true);
                    if (built.Whynot != null)
                    {
                        refused++;
                        string kind = built.Whynot.Split('.')[0];
                        // The numbers in a refusal differ every time; the shape of it is what to count.
                        kind = System.Text.RegularExpressions.Regex.Replace(kind, @"\d+", "N");
                        reasons[kind] = reasons.TryGetValue(kind, out int n) ? n + 1 : 1;
                        continue;
                    }

                    tried++;
                    totalSquares += built.Squares;
                    totalTiles += built.TilesKept;

                    var img = NitroBgCodec.Composite(built.Tiles, built.Colours, built.Arrangement,
                                                     built.ClearSlotKept);
                    int bad = Different(shown.Rgba, img.Rgba, built.ClearSlotKept);
                    if (bad == 0) { matched++; continue; }
                    if (worst.Count < 10)
                        worst.Add($"{archive.Title} entry {i} ({shown.Width}x{shown.Height}): "
                                + $"{bad} of {shown.Width * shown.Height} pixels differ");
                }
            }

            _out.WriteLine($"{drawn} pictures drawn out of the ROM.");
            _out.WriteLine($"{tried} were rebuilt, {matched} came back exactly the same.");
            _out.WriteLine($"{refused} were refused before anything was written:");
            foreach (var kv in reasons.OrderByDescending(k => k.Value))
                _out.WriteLine($"  {kv.Value} x {kv.Key}");
            if (tried > 0)
                _out.WriteLine($"{totalSquares} squares came down to {totalTiles} tiles, "
                             + $"{100 - totalTiles * 100 / Math.Max(1, totalSquares)} percent saved.");

            // A sweep that rebuilt nothing would pass every check below while proving nothing at all.
            Assert.True(tried > 200, $"only {tried} pictures were rebuilt, which is too few to prove anything");
            Assert.True(worst.Count == 0, string.Join("\n", worst));
            Assert.Equal(tried, matched);
        }

        /// <summary>
        /// How many pixels came back different. A picture drawn with a clear slot has its see-through
        /// pixels compared as see-through; the rest are compared at the five bits a screen keeps.
        /// </summary>
        private static int Different(byte[] want, byte[] got, bool clearSlot)
        {
            int bad = 0;
            for (int i = 0; i < want.Length && i < got.Length; i += 4)
            {
                bool wantClear = clearSlot && want[i + 3] < 128;
                bool gotClear = got[i + 3] < 128;
                if (wantClear && gotClear) continue;
                if (wantClear != gotClear) { bad++; continue; }
                if ((want[i] & 0xF8) != (got[i] & 0xF8)
                    || (want[i + 1] & 0xF8) != (got[i + 1] & 0xF8)
                    || (want[i + 2] & 0xF8) != (got[i + 2] & 0xF8)) bad++;
            }
            return bad;
        }
    }
}

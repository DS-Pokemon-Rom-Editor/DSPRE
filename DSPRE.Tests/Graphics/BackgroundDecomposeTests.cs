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
    /// Painting a whole battle background and putting it back into the tiles it is drawn from.
    /// </summary>
    [Collection("rom")]
    public class BackgroundDecomposeTests
    {
        private readonly ITestOutputHelper _out;
        public BackgroundDecomposeTests(ITestOutputHelper o) { _out = o; }

        private const string HeartGold = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";

        private static bool Ready()
        {
            if (!Directory.Exists(HeartGold)) return false;
            try { new RomInfo("IPKE", HeartGold); } catch { return false; }
            GraphicAssets.Forget();
            return true;
        }

        private static GraphicAssets.Archive Backdrops()
            => GraphicAssets.All.First(x => x.Dir == RomInfo.DirNames.battleBg);

        /// <summary>The drawings that DSPRE can compose into a whole background.</summary>
        private static List<int> Drawings(GraphicAssets.Archive a, int max)
        {
            var found = new List<int>();
            var narc = new ScriptNarc(a.Dir);
            int n = GraphicAssets.Count(a);
            for (int i = 0; i < n && found.Count < max; i++)
            {
                var b = narc.Get(i);
                if (b == null) continue;
                if (GraphicAssets.Identify(GraphicAssets.Unsqueeze(b)) != GraphicAssets.Kind.TileGraphic)
                    continue;
                int arranged;
                try { arranged = a.ArrangementEntry?.Invoke(i) ?? -1; } catch { continue; }
                if (arranged < 0 || arranged >= n) continue;
                found.Add(i);
            }
            return found;
        }

        [Fact]
        public void PuttingABackgroundBackUnchangedLeavesItAlone()
        {
            if (!Ready()) { _out.WriteLine("HeartGold not unpacked here"); return; }
            var a = Backdrops();
            var drawings = Drawings(a, 25);
            Assert.True(drawings.Count >= 8, $"only {drawings.Count} backgrounds found, this would prove little");

            var narc = new ScriptNarc(a.Dir);
            int drawn = 0, same = 0, wrote = 0;
            var differed = new List<string>();

            foreach (int at in drawings)
            {
                var before = GraphicAssets.Render(a, at);
                if (before.Rgba == null || before.Width <= 0) continue;
                drawn++;

                byte[] was = narc.Get(at)?.ToArray();
                Assert.NotNull(was);
                try
                {
                    string why = GraphicAssets.PutBackgroundBack(a, at, before.Rgba, before.Width,
                                                                 before.Height, out int changed, out _, out _);
                    if (why != null) { differed.Add($"{at}: {why}"); continue; }
                    wrote += changed;

                    var after = GraphicAssets.Render(a, at);
                    if (after.Rgba != null && after.Width == before.Width && after.Height == before.Height
                        && before.Rgba.SequenceEqual(after.Rgba)) same++;
                    else differed.Add($"{at}: the picture changed when nothing was painted");
                }
                finally { narc.Put(at, was); }
            }

            _out.WriteLine($"{drawn} backgrounds, {same} came back the same picture, "
                         + $"{wrote} squares needed writing in all");
            foreach (var d in differed.Take(6)) _out.WriteLine("   " + d);
            Assert.True(drawn >= 8, $"only {drawn} could be drawn at all");
            Assert.Equal(drawn, same);

            // Putting an unchanged picture back must not need to write a single square, or the reading
            // and the writing disagree about something even when nothing was painted.
            Assert.Equal(0, wrote);
        }

        /// <summary>
        /// The check above proves able to fail: painting really does change the picture.
        /// </summary>
        [Fact]
        public void PaintingABackgroundChangesItAndSaysWhatSharesATile()
        {
            if (!Ready()) { _out.WriteLine("HeartGold not unpacked here"); return; }
            var a = Backdrops();
            var narc = new ScriptNarc(a.Dir);

            foreach (int at in Drawings(a, 25))
            {
                var before = GraphicAssets.Render(a, at);
                if (before.Rgba == null || before.Width < 32 || before.Height < 32) continue;

                // Find one eight by eight square that uses two colours of its own, and paint one of them
                // over the other.
                var painted = (byte[])before.Rgba.Clone();
                int touched = 0;
                for (int ty = 0; ty < before.Height / 8 && touched == 0; ty++)
                {
                    for (int tx = 0; tx < before.Width / 8 && touched == 0; tx++)
                    {
                        var here = new List<(byte r, byte g, byte b)>();
                        for (int y = 0; y < 8; y++)
                            for (int x = 0; x < 8; x++)
                            {
                                int p = ((ty * 8 + y) * before.Width + tx * 8 + x) * 4;
                                if (before.Rgba[p + 3] == 0) continue;
                                var c = (before.Rgba[p], before.Rgba[p + 1], before.Rgba[p + 2]);
                                if (!here.Contains(c)) here.Add(c);
                            }
                        if (here.Count < 2) continue;

                        for (int y = 0; y < 8; y++)
                            for (int x = 0; x < 8; x++)
                            {
                                int p = ((ty * 8 + y) * before.Width + tx * 8 + x) * 4;
                                if (painted[p + 3] == 0) continue;
                                if (painted[p] != here[0].r || painted[p + 1] != here[0].g
                                    || painted[p + 2] != here[0].b) continue;
                                painted[p] = here[1].r; painted[p + 1] = here[1].g; painted[p + 2] = here[1].b;
                                touched++;
                            }
                    }
                }
                if (touched == 0) continue;

                byte[] was = narc.Get(at)?.ToArray();
                try
                {
                    string why = GraphicAssets.PutBackgroundBack(a, at, painted, before.Width,
                                                                 before.Height, out int changed,
                                                                 out int shared, out int fought);
                    if (why != null) continue;

                    var after = GraphicAssets.Render(a, at);
                    _out.WriteLine($"background {at}: painted {touched} pixels, {changed} squares written, "
                                 + $"{shared} of them share their tile with a square elsewhere, "
                                 + $"{fought} pixels wanted two colours at once");
                    Assert.NotNull(after.Rgba);
                    Assert.False(before.Rgba.SequenceEqual(after.Rgba),
                        $"{at}: {touched} pixels were painted, {changed} squares written, nothing changed");
                    Assert.True(changed > 0, "the picture changed but no square was reported written");
                    return;
                }
                finally { narc.Put(at, was); }
            }

            Assert.Fail("no background was suitable to paint on, so this proved nothing");
        }

        /// <summary>A picture of the wrong size is refused, not written badly.</summary>
        [Fact]
        public void ABackgroundPictureOfTheWrongSizeIsRefused()
        {
            if (!Ready()) { _out.WriteLine("HeartGold not unpacked here"); return; }
            var a = Backdrops();
            int at = Drawings(a, 5).FirstOrDefault(-1);
            Assert.True(at >= 0, "no background to try");

            var narc = new ScriptNarc(a.Dir);
            byte[] before = narc.Get(at)?.ToArray();
            Assert.NotNull(before);

            string why = GraphicAssets.PutBackgroundBack(a, at, new byte[8 * 8 * 4], 8, 8,
                                                         out int changed, out _, out _);
            Assert.False(string.IsNullOrWhiteSpace(why));
            Assert.Equal(0, changed);
            _out.WriteLine("refused: " + why);

            Assert.True(before.SequenceEqual(narc.Get(at)), "a refused picture was written anyway");
        }
    }
}

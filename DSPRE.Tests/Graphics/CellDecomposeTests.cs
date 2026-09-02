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
    /// Painting the picture of an assembled sprite and putting it back into the tiles it is drawn from.
    /// </summary>
    [Collection("rom")]
    public class CellDecomposeTests
    {
        private readonly ITestOutputHelper _out;
        public CellDecomposeTests(ITestOutputHelper o) { _out = o; }

        private const string HeartGold = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";

        private static bool Ready()
        {
            if (!Directory.Exists(HeartGold)) return false;
            try { new RomInfo("IPKE", HeartGold); } catch { return false; }
            GraphicAssets.Forget();
            return true;
        }

        private static GraphicAssets.Archive Battle()
            => GraphicAssets.All.First(x => x.Dir == RomInfo.DirNames.battleObj);

        /// <summary>Every layout in the battle archive whose drawing the game names.</summary>
        private static List<int> Layouts(GraphicAssets.Archive a, int max)
        {
            var found = new List<int>();
            var names = BattleObjects.Names();
            var narc = new ScriptNarc(a.Dir);
            for (int i = 0; i < narc.Count && found.Count < max; i++)
            {
                var b = narc.Get(i);
                if (b == null || GraphicAssets.Identify(b) != GraphicAssets.Kind.CellLayout) continue;
                if (BattleObjects.DrawingFor(i) < 0) continue;
                found.Add(i);
            }
            return found;
        }

        [Fact]
        public void PuttingAPictureBackUnchangedLeavesTheDrawingAlone()
        {
            if (!Ready()) { _out.WriteLine("HeartGold not unpacked here"); return; }
            var a = Battle();
            var layouts = Layouts(a, 40);
            Assert.True(layouts.Count >= 10, $"only {layouts.Count} layouts found, the sweep would prove little");

            var narc = new ScriptNarc(a.Dir);
            int same = 0, drawn = 0, couldDo = 0;
            var differed = new List<string>();
            var refused = new List<string>();

            foreach (int layout in layouts)
            {
                var before = GraphicAssets.Render(a, layout);
                if (before.Rgba == null || before.Width <= 0) continue;
                drawn++;

                int drawingAt = BattleObjects.DrawingFor(layout);
                byte[] wasDrawing = narc.Get(drawingAt)?.ToArray();
                Assert.NotNull(wasDrawing);

                try
                {
                    string why = GraphicAssets.PutAssembledBack(a, layout, before.Rgba,
                                                                before.Width, before.Height);
                    // A layout whose first bank holds no pieces has nothing to put back. That is a
                    // refusal, not a bad write, and it must not have written anything either.
                    if (why != null)
                    {
                        refused.Add($"{layout}: {why}");
                        Assert.True(wasDrawing.SequenceEqual(narc.Get(drawingAt)),
                            $"{layout}: refused and wrote anyway");
                        continue;
                    }
                    couldDo++;

                    var after = GraphicAssets.Render(a, layout);
                    if (after.Rgba != null && after.Width == before.Width && after.Height == before.Height
                        && before.Rgba.SequenceEqual(after.Rgba)) same++;
                    else differed.Add($"{layout}: the picture changed when nothing was painted");
                }
                finally { narc.Put(drawingAt, wasDrawing); }
            }

            _out.WriteLine($"{drawn} assembled sprites, {couldDo} could be put back, "
                         + $"{same} came back the same picture, {refused.Count} refused");
            foreach (var d in differed.Take(6)) _out.WriteLine("   changed: " + d);
            foreach (var r in refused.Take(4)) _out.WriteLine("   " + r);

            Assert.True(drawn >= 10, $"only {drawn} could be drawn at all");
            Assert.True(couldDo >= drawn - 4, $"only {couldDo} of {drawn} could be put back at all");
            Assert.Equal(couldDo, same);
        }

        /// <summary>
        /// The check above proves able to fail: painting really does change the picture, and only where it
        /// was painted.
        /// </summary>
        [Fact]
        public void PaintingOnThePictureChangesExactlyWhatWasPainted()
        {
            if (!Ready()) { _out.WriteLine("HeartGold not unpacked here"); return; }
            var a = Battle();
            var narc = new ScriptNarc(a.Dir);

            foreach (int layout in Layouts(a, 40))
            {
                var before = GraphicAssets.Render(a, layout);
                if (before.Rgba == null || before.Width < 16 || before.Height < 16) continue;

                // A run of pixels that are actually drawn, so the paint lands on the sprite and not on the
                // empty space around it.
                var solid = new List<int>();
                for (int p = 0; p < before.Width * before.Height && solid.Count < 40; p++)
                    if (before.Rgba[p * 4 + 3] != 0) solid.Add(p);
                if (solid.Count < 40) continue;

                // Repaint them in another colour the sprite already has, so the edit can be written.
                var used = new HashSet<uint>();
                for (int p = 0; p < before.Width * before.Height; p++)
                    if (before.Rgba[p * 4 + 3] != 0)
                        used.Add((uint)(before.Rgba[p * 4] << 16 | before.Rgba[p * 4 + 1] << 8
                                        | before.Rgba[p * 4 + 2]));
                if (used.Count < 2) continue;

                var painted = (byte[])before.Rgba.Clone();
                uint first = (uint)(before.Rgba[solid[0] * 4] << 16 | before.Rgba[solid[0] * 4 + 1] << 8
                                    | before.Rgba[solid[0] * 4 + 2]);
                uint other = used.First(c => c != first);
                int changedPixels = 0;
                foreach (int p in solid)
                {
                    painted[p * 4] = (byte)(other >> 16);
                    painted[p * 4 + 1] = (byte)(other >> 8);
                    painted[p * 4 + 2] = (byte)other;
                    changedPixels++;
                }

                int drawingAt = BattleObjects.DrawingFor(layout);
                byte[] wasDrawing = narc.Get(drawingAt)?.ToArray();
                try
                {
                    string why = GraphicAssets.PutAssembledBack(a, layout, painted,
                                                                before.Width, before.Height);
                    if (why != null) continue;

                    var after = GraphicAssets.Render(a, layout);
                    Assert.NotNull(after.Rgba);
                    Assert.False(before.Rgba.SequenceEqual(after.Rgba),
                        $"layout {layout}: {changedPixels} pixels were painted and nothing changed");

                    // Only where it was painted. A piece shared with another one would smear the edit
                    // across the sprite, and that has to show up here.
                    var meantTo = new HashSet<int>(solid);
                    int strayed = 0;
                    for (int p = 0; p < before.Width * before.Height; p++)
                    {
                        if (meantTo.Contains(p)) continue;
                        for (int c = 0; c < 4; c++)
                            if (before.Rgba[p * 4 + c] != after.Rgba[p * 4 + c]) { strayed++; break; }
                    }

                    _out.WriteLine($"layout {layout}: painted {changedPixels} pixels, {strayed} others moved");
                    Assert.True(strayed == 0,
                        $"layout {layout}: {strayed} pixels changed that were never painted");
                    return;   // one is enough to prove the check can fail
                }
                finally { narc.Put(drawingAt, wasDrawing); }
            }

            Assert.Fail("no assembled sprite was suitable to paint on, so this proved nothing");
        }

        /// <summary>A picture that is not the size the sprite is drawn at is refused, not written badly.</summary>
        [Fact]
        public void APictureOfTheWrongSizeIsRefused()
        {
            if (!Ready()) { _out.WriteLine("HeartGold not unpacked here"); return; }
            var a = Battle();
            int layout = Layouts(a, 5).FirstOrDefault(-1);
            Assert.True(layout >= 0, "no layout to try");

            var narc = new ScriptNarc(a.Dir);
            int drawingAt = BattleObjects.DrawingFor(layout);
            byte[] before = narc.Get(drawingAt)?.ToArray();
            Assert.NotNull(before);

            string why = GraphicAssets.PutAssembledBack(a, layout, new byte[8 * 8 * 4], 8, 8);
            Assert.False(string.IsNullOrWhiteSpace(why));
            _out.WriteLine("refused: " + why);

            Assert.True(before.SequenceEqual(narc.Get(drawingAt)),
                "a refused picture was written anyway");
        }
    }
}

using System;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using NarcAPI;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>
    /// Builds a Platinum ROM with the enemy HP bar painted through the assembled-picture path, so that
    /// path can be looked at in a running battle rather than only in a test.
    ///
    /// The sprite path was proven in a real battle already. This one is different: the picture is put
    /// together from pieces by a cell layout, and the paint is taken apart again and written back into
    /// whichever piece each pixel belongs to. Nothing about that has been seen by the game.
    ///
    /// The enemy gauge is SINGLE_GAGE1, drawing 188 and layout 187, per gauge.c's GaugeObjParam_bb. A
    /// solid block is painted across the middle of it in a colour the gauge already has. A tool rather
    /// than a check, so it does nothing unless DSPRE_PAINT_ROM is set. The user's own project and ROM are
    /// never touched.
    /// </summary>
    [Collection("rom")]
    public class PaintedGaugeRomBuilder
    {
        private readonly ITestOutputHelper _out;
        public PaintedGaugeRomBuilder(ITestOutputHelper o) { _out = o; }

        private static readonly string Source = TestRoms.Platinum;

        private static readonly string Scratch =
            @"C:\Users\safea\AppData\Local\Temp\claude\C--Romhacking-Tooling-DSPRE\4e7579f1-fc54-4253-a949-eba625115ec9\scratchpad";

        private const int EnemyGaugeLayout = 187;

        private static void CopyTree(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (var d in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(d.Replace(from, to));
            foreach (var f in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
                File.Copy(f, f.Replace(from, to), overwrite: true);
        }

        [SkippableFact]
        public void BuildARomWithAPaintedEnemyGauge()
        {
            Skip.If(Environment.GetEnvironmentVariable("DSPRE_PAINT_ROM") != "1", "DSPRE_PAINT_ROM not set; nothing built");
            Assert.True(Directory.Exists(Source), "the Platinum project is not there, so nothing was built");

            string work = Path.Combine(Scratch, "plat_gauge");
            if (Directory.Exists(work)) Directory.Delete(work, true);
            CopyTree(Source, work);
            new RomInfo("CPUE", work);
            GraphicAssets.Forget();

            var archive = GraphicAssets.All.First(a => a.Dir == DirNames.battleObj);

            var shown = GraphicAssets.Render(archive, EnemyGaugeLayout);
            Assert.True(shown.Rgba != null, shown.Whynot ?? "the gauge could not be drawn");
            _out.WriteLine($"enemy gauge as it appears: {shown.Width} by {shown.Height}");
            GraphicAssets.ExportPng(archive, EnemyGaugeLayout, Path.Combine(Scratch, "gauge_before.png"));

            // The colours the gauge already draws with, so the block can be written at all.
            var used = new System.Collections.Generic.List<(byte r, byte g, byte b)>();
            for (int p = 0; p < shown.Width * shown.Height && used.Count < 6; p++)
            {
                if (shown.Rgba[p * 4 + 3] == 0) continue;
                var c = (shown.Rgba[p * 4], shown.Rgba[p * 4 + 1], shown.Rgba[p * 4 + 2]);
                if (!used.Contains(c)) used.Add(c);
            }
            Assert.True(used.Count >= 2, "the gauge draws in fewer than two colours");
            var ink = used[used.Count - 1];
            _out.WriteLine($"painting with {ink}, one of {used.Count} colours the gauge already uses");

            // A block across the middle, only where the gauge actually draws, so it cannot be mistaken
            // for the battle background showing through.
            var painted = (byte[])shown.Rgba.Clone();
            int touched = 0;
            int top = shown.Height / 3, bottom = Math.Min(shown.Height, top + shown.Height / 3);
            for (int y = top; y < bottom; y++)
                for (int x = 0; x < shown.Width; x++)
                {
                    int at = (y * shown.Width + x) * 4;
                    if (painted[at + 3] == 0) continue;
                    painted[at] = ink.r; painted[at + 1] = ink.g; painted[at + 2] = ink.b;
                    touched++;
                }
            Assert.True(touched > 200, $"only {touched} pixels would be painted, too few to see");
            _out.WriteLine($"{touched} pixels painted across rows {top} to {bottom}");

            string why = GraphicAssets.PutAssembledBack(archive, EnemyGaugeLayout, painted,
                                                       shown.Width, shown.Height);
            Assert.True(why == null, $"the gauge would not take the paint: {why}");

            GraphicAssets.Forget();
            var after = GraphicAssets.Render(archive, EnemyGaugeLayout);
            Assert.True(after.Rgba != null, "the gauge could not be drawn again");
            int differ = after.Rgba.Where((v, i) => i < shown.Rgba.Length && v != shown.Rgba[i]).Count();
            _out.WriteLine($"the picture came back with {differ} bytes different");
            Assert.True(differ > 0, "the paint made no difference to the picture");
            GraphicAssets.ExportPng(archive, EnemyGaugeLayout, Path.Combine(Scratch, "gauge_after.png"));

            foreach (var kvp in gameDirs)
            {
                var di = new DirectoryInfo(kvp.Value.unpackedDir);
                if (di.Exists) Narc.FromFolder(kvp.Value.unpackedDir).Save(kvp.Value.packedDir);
            }

            string outDir = Path.Combine(Scratch, "roms");
            Directory.CreateDirectory(outDir);
            string outRom = Path.Combine(outDir, "plat_painted_gauge.nds");
            if (File.Exists(outRom)) File.Delete(outRom);
            Assert.True(DSUtils.RepackROM(outRom) && File.Exists(outRom), "building the ROM failed");
            _out.WriteLine($"built {outRom}, {new FileInfo(outRom).Length / 1024 / 1024} MB");
        }
    }
}

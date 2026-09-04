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
    /// Builds a Platinum ROM with every text box style marked differently, so the order of the styles
    /// inside the archive can be read off a running game instead of taken on trust.
    /// </summary>
    [Collection("rom")]
    public class PaintedTextBoxRomBuilder
    {
        private readonly ITestOutputHelper _out;
        public PaintedTextBoxRomBuilder(ITestOutputHelper o) { _out = o; }

        private static readonly string Source = TestRoms.Platinum;

        private static readonly string Scratch =
            @"C:\Users\safea\AppData\Local\Temp\claude\C--Romhacking-Tooling-DSPRE\4e7579f1-fc54-4253-a949-eba625115ec9\scratchpad";

        private const int Styles = 20;

        private static void CopyTree(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (var d in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(d.Replace(from, to));
            foreach (var f in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
                File.Copy(f, f.Replace(from, to), overwrite: true);
        }

        /// <summary>Writes out what the marked project holds, so the paint can be looked at away from the
        /// emulator before blaming the game for not showing it.</summary>
        [SkippableFact]
        public void ShowTheMarkedStyles()
        {
            Skip.If(Environment.GetEnvironmentVariable("DSPRE_PAINT_ROM") != "1", "DSPRE_PAINT_ROM not set");

            string work = Path.Combine(Scratch, "plat_textbox");
            Skip.If(!Directory.Exists(work), "nothing built yet");

            foreach (var (root, tag) in new[] { (work, "marked"), (Source, "original") })
            {
                new RomInfo("CPUE", root);
                GraphicAssets.Forget();
                var archive = GraphicAssets.All.First(a => a.Dir == DirNames.windowFrames);
                foreach (int style in new[] { 0, 3 })
                {
                    string png = Path.Combine(Scratch, $"textbox_{tag}_{style}.png");
                    string err = GraphicAssets.ExportPng(archive, 2 + style, png);
                    _out.WriteLine($"{tag} style {style}: {(err ?? "written")}");
                }
            }
        }

        [SkippableFact]
        public void BuildARomWithEveryTextBoxStyleMarked()
        {
            Skip.If(Environment.GetEnvironmentVariable("DSPRE_PAINT_ROM") != "1", "DSPRE_PAINT_ROM not set; nothing built");
            Assert.True(Directory.Exists(Source), "the Platinum project is not there, so nothing was built");

            string work = Path.Combine(Scratch, "plat_textbox");
            if (Directory.Exists(work)) Directory.Delete(work, true);
            CopyTree(Source, work);
            new RomInfo("CPUE", work);
            GraphicAssets.Forget();

            var archive = GraphicAssets.All.First(a => a.Dir == DirNames.windowFrames);
            int firstColour = GraphicAssets.FirstPaletteIndex(archive);
            Assert.True(firstColour >= 22, $"colours start at {firstColour}, not where the list says");

            int marked = 0;
            for (int style = 0; style < Styles; style++)
            {
                int at = 2 + style;     // the styles run from entry 2, per winframe.naix
                var art = GraphicAssets.ReadIndexed(archive, at, out string why);
                Assert.True(art != null, $"style {style} at entry {at} could not be read: {why}");

                // A row of marks along the top edge of the frame's tile sheet, one for each step of the
                // style number, in the colour the frame uses least so it cannot be mistaken for the border
                // itself.
                int ink = Enumerable.Range(1, art.ColourCount - 1)
                    .OrderBy(c => art.Indices.Count(v => v == c))
                    .First();

                var want = (byte[])art.Indices.Clone();
                int drawn = 0;
                for (int mark = 0; mark <= style; mark++)
                {
                    int x0 = 1 + mark * 2;
                    if (x0 + 1 >= art.Width) break;
                    for (int y = 0; y < 2 && y < art.Height; y++)
                    {
                        want[y * art.Width + x0] = (byte)ink;
                        drawn++;
                    }
                }

                string err = GraphicAssets.WriteIndices(archive, at, want, art);
                Assert.True(err == null, $"style {style} would not take the paint: {err}");
                marked++;
                if (style < 3 || style == Styles - 1)
                    _out.WriteLine($"style {style} at entry {at}: {art.Width} by {art.Height}, "
                                 + $"colour {ink}, {drawn} pixels marked");
            }

            Assert.Equal(Styles, marked);

            foreach (var kvp in gameDirs)
            {
                var di = new DirectoryInfo(kvp.Value.unpackedDir);
                if (di.Exists) Narc.FromFolder(kvp.Value.unpackedDir).Save(kvp.Value.packedDir);
            }

            string outDir = Path.Combine(Scratch, "roms");
            Directory.CreateDirectory(outDir);
            string outRom = Path.Combine(outDir, "plat_textbox_styles.nds");
            if (File.Exists(outRom)) File.Delete(outRom);
            Assert.True(DSUtils.RepackROM(outRom) && File.Exists(outRom), "building the ROM failed");
            _out.WriteLine($"built {outRom}, {new FileInfo(outRom).Length / 1024 / 1024} MB");
        }
    }
}

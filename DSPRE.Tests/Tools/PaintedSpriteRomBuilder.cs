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
    /// Builds a Platinum ROM with a battle sprite painted through the Graphics workbench, so the edit can
    /// be looked at in a running game rather than only in a test.
    /// </summary>
    [Collection("rom")]
    public class PaintedSpriteRomBuilder
    {
        private readonly ITestOutputHelper _out;
        public PaintedSpriteRomBuilder(ITestOutputHelper o) { _out = o; }

        private static readonly string Source = TestRoms.Platinum;

        private static readonly string Scratch =
            @"C:\Users\safea\AppData\Local\Temp\claude\C--Romhacking-Tooling-DSPRE\4e7579f1-fc54-4253-a949-eba625115ec9\scratchpad";

        private const int Chimchar = 390;          // its number in the national list
        private const int FilesPerSpecies = 6;     // femaleBack, maleBack, femaleFront, maleFront, colours, shiny

        private static void CopyTree(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (var d in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(d.Replace(from, to));
            foreach (var f in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
                File.Copy(f, f.Replace(from, to), overwrite: true);
        }

        /// <summary>Writes out what the painted project holds, so the edit can be looked at away from the
        /// emulator, and says which species the entry belongs to.</summary>
        [SkippableFact]
        public void ShowWhatWasPainted()
        {
            Skip.If(Environment.GetEnvironmentVariable("DSPRE_PAINT_ROM") != "1", "DSPRE_PAINT_ROM not set");

            string work = Path.Combine(Scratch, "plat_painted");
            Skip.If(!Directory.Exists(work), "nothing built yet");
            new RomInfo("CPUE", work);
            GraphicAssets.Forget();

            var archive = GraphicAssets.All.First(a => a.Dir == DirNames.pokemonBattleSprites);
            _out.WriteLine($"archive holds {GraphicAssets.Count(archive)} files");

            var names = RomInfo.GetPokemonNames();
            _out.WriteLine($"species {Chimchar} is called {(Chimchar < names.Length ? names[Chimchar] : "?")}");

            foreach (int at in new[] { Chimchar * FilesPerSpecies + 2, Chimchar * FilesPerSpecies + 3 })
            {
                string png = Path.Combine(Scratch, $"painted_{at}.png");
                string err = GraphicAssets.ExportPng(archive, at, png);
                _out.WriteLine($"entry {at}: {(err ?? "written to " + png)}");

                // And the same entry from the project that was never touched, to compare.
                new RomInfo("CPUE", Source);
                GraphicAssets.Forget();
                var orig = GraphicAssets.All.First(x => x.Dir == DirNames.pokemonBattleSprites);
                string png2 = Path.Combine(Scratch, $"original_{at}.png");
                _out.WriteLine($"   original: {(GraphicAssets.ExportPng(orig, at, png2) ?? "written to " + png2)}");

                new RomInfo("CPUE", work);
                GraphicAssets.Forget();
            }
        }

        [SkippableFact]
        public void BuildARomWithAPaintedBattleSprite()
        {
            Skip.If(Environment.GetEnvironmentVariable("DSPRE_PAINT_ROM") != "1", "DSPRE_PAINT_ROM not set; nothing built");
            Assert.True(Directory.Exists(Source), "the Platinum project is not there, so nothing was built");

            string work = Path.Combine(Scratch, "plat_painted");
            if (Directory.Exists(work)) Directory.Delete(work, true);
            CopyTree(Source, work);
            new RomInfo("CPUE", work);
            GraphicAssets.Forget();

            var archive = GraphicAssets.All.First(a => a.Dir == DirNames.pokemonBattleSprites);

            // Both front slots, because a species without separate female art still has the file.
            int[] fronts = { Chimchar * FilesPerSpecies + 2, Chimchar * FilesPerSpecies + 3 };
            int painted = 0;

            foreach (int at in fronts)
            {
                var art = GraphicAssets.ReadIndexed(archive, at, out string why);
                Assert.True(art != null, $"entry {at} could not be read: {why}");
                _out.WriteLine($"entry {at}: {art.Width} by {art.Height}, {art.ColourCount} colours");

                // A band across the middle of the first frame, in the colour the sprite uses least, so it
                // reads as an obvious stripe and cannot be mistaken for part of the artwork.
                int ink = Enumerable.Range(1, art.ColourCount - 1)
                    .OrderBy(c => art.Indices.Count(v => v == c))
                    .First();
                _out.WriteLine($"  painting with colour {ink}, used {art.Indices.Count(v => v == ink)} times");

                var want = (byte[])art.Indices.Clone();
                int fw = archive.FrameWidth > 0 ? archive.FrameWidth : art.Width;
                for (int y = 30; y < 42; y++)
                    for (int x = 4; x < fw - 4; x++)
                        want[y * art.Width + x] = (byte)ink;

                string err = GraphicAssets.WriteIndices(archive, at, want, art);
                Assert.True(err == null, $"entry {at} would not take the paint: {err}");

                GraphicAssets.Forget();
                var back = GraphicAssets.ReadIndexed(archive, at, out _);
                Assert.True(back != null, $"entry {at} could not be reopened");
                int differ = back.Indices.Where((v, i) => i < want.Length && v != want[i]).Count();
                _out.WriteLine($"  reopened with {differ} pixels different from what was painted");
                Assert.True(differ <= 4, $"entry {at} came back with {differ} pixels changed; only the four "
                                       + "the key is seeded from may differ");
                painted++;
            }

            Assert.Equal(fronts.Length, painted);

            foreach (var kvp in gameDirs)
            {
                var di = new DirectoryInfo(kvp.Value.unpackedDir);
                if (di.Exists) Narc.FromFolder(kvp.Value.unpackedDir).Save(kvp.Value.packedDir);
            }

            string outDir = Path.Combine(Scratch, "roms");
            Directory.CreateDirectory(outDir);
            string outRom = Path.Combine(outDir, "plat_painted_chimchar.nds");
            if (File.Exists(outRom)) File.Delete(outRom);
            bool ok = DSUtils.RepackROM(outRom);
            Assert.True(ok && File.Exists(outRom), "building the ROM failed");
            _out.WriteLine($"built {outRom}, {new FileInfo(outRom).Length / 1024 / 1024} MB");
        }
    }
}

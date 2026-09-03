using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>Every background a move asks for must be one the preview can actually draw.</summary>
    [Collection("rom")]
    public class BackgroundChangeCoverageTests
    {
        private readonly ITestOutputHelper _out;
        public BackgroundChangeCoverageTests(ITestOutputHelper o) { _out = o; }

        private static readonly string HeartGold = TestRoms.HeartGold;
        private static readonly string Platinum = TestRoms.Platinum;

        [Theory]
        [InlineData("CPUE", WazaSeqVersion.Plat)]
        [InlineData("IPKE", WazaSeqVersion.HGSS)]
        public void EveryBackgroundAMoveAsksForCanBeDrawn(string code, WazaSeqVersion version)
        {
            string project = code == "CPUE" ? Platinum : HeartGold;
            Assert.True(Directory.Exists(project), $"{code}: no unpacked project, so nothing was checked");
            new RomInfo(code, project);

            var narc = new ScriptNarc(DirNames.wazaEffectScripts);
            Assert.True(narc.Available, "the move script archive is missing");

            var renderer = new BattleBgRenderer();
            var asked = new Dictionary<int, List<int>>();   // bg id -> moves asking for it
            int scripts = 0;

            var files = RomFiles.Settled(gameDirs[DirNames.wazaEffectScripts].unpackedDir);
            for (int move = 0; move < files.Length; move++)
            {
                var bytes = File.ReadAllBytes(files[move]);
                if (bytes.Length == 0) continue;
                scripts++;
                foreach (var c in WestScript.Parse(bytes, version))
                {
                    string name = WestOpcodes.Name(version, c.OpId);
                    if (name != "WEST_HAIKEI_CHG" && name != "WEST_HAIKEI_CHG_EX") continue;
                    if (c.Args.Length < 1) continue;
                    int bg = c.Args[0];
                    if (!asked.TryGetValue(bg, out var l)) asked[bg] = l = new List<int>();
                    if (!l.Contains(move)) l.Add(move);
                }
            }

            Assert.True(scripts > 400, $"only {scripts} scripts were read, so this proves nothing");

            // One background written out as a picture, when asked for, so what the preview draws
            // can be looked at rather than guessed at.
            string dump = Environment.GetEnvironmentVariable("DSPRE_DUMP_BG");
            if (!string.IsNullOrWhiteSpace(dump) && int.TryParse(dump, out int bgWant))
            {
                var im = renderer.Build(bgWant);
                if (im?.Rgba != null)
                {
                    string outPath = $"C:/Users/safea/AppData/Local/Temp/claude/C--Romhacking-Tooling-DSPRE/4e7579f1-fc54-4253-a949-eba625115ec9/scratchpad/bg{bgWant}_{code}.png";
                    SaveRgba(outPath, im.Rgba, im.Width, im.Height);
                    double mean = 0;
                    for (int i = 0; i < im.Rgba.Length; i += 4)
                        mean += (im.Rgba[i] + im.Rgba[i + 1] + im.Rgba[i + 2]) / 3.0;
                    mean /= im.Rgba.Length / 4.0;
                    _out.WriteLine($"background {bgWant}: {im.Width}x{im.Height}, mean brightness {mean:F1}");
                }
                else _out.WriteLine($"background {bgWant}: could not be built");
            }

            var missing = asked.Keys.OrderBy(k => k)
                                .Where(bg => renderer.Build(bg) == null)
                                .ToList();

            _out.WriteLine($"{code}: {scripts} scripts read, {asked.Count} different backgrounds asked for, "
                           + $"{missing.Count} of them cannot be drawn");
            foreach (int bg in missing)
                _out.WriteLine($"  background {bg}: asked for by {asked[bg].Count} moves, first is {asked[bg][0]}");

            Assert.True(missing.Count == 0,
                $"{code}: {missing.Count} backgrounds a move asks for cannot be drawn, so those moves silently "
                + $"keep the normal scene: {string.Join(", ", missing)}");
        }

        private static void SaveRgba(string path, byte[] rgba, int w, int h)
        {
            using var bmp = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, w, h),
                System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            var bgra = new byte[rgba.Length];
            for (int i = 0; i < rgba.Length; i += 4)
            { bgra[i] = rgba[i + 2]; bgra[i + 1] = rgba[i + 1]; bgra[i + 2] = rgba[i]; bgra[i + 3] = rgba[i + 3]; }
            System.Runtime.InteropServices.Marshal.Copy(bgra, 0, data.Scan0, bgra.Length);
            bmp.UnlockBits(data);
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
    }
}

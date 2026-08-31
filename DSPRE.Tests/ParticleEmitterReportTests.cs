using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DSPRE;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>
    /// What every particle in the game is made of, so a particle drawn wrong can be traced to its data.
    ///
    /// The preview draws some particles far too large: ExtremeSpeed covers the whole platform with a pale
    /// puff and hides the Pokemon behind it, where the game shows a small flash. This writes out the fields
    /// that decide a particle's size and which texture it uses, for every emitter in the game, so the ones
    /// that come out wrong can be found in the data rather than guessed at.
    /// </summary>
    [Collection("rom")]
    public class ParticleEmitterReportTests
    {
        private readonly ITestOutputHelper _out;
        public ParticleEmitterReportTests(ITestOutputHelper o) { _out = o; }

        private const string HeartGold = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";
        private const string Platinum =
            @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents";

        private static readonly string Doc =
            @"C:\Romhacking\Tooling\DSPRE\Research\Moves\Animation\MoveAnimationParticleSizes.md";

        [Fact]
        public void WriteWhatEveryParticleIsMadeOf()
        {
            Assert.True(Directory.Exists(Platinum), "the Platinum project is not there, so nothing was checked");
            new RomInfo("CPUE", Platinum);

            var files = RomFiles.Settled(gameDirs[DirNames.wazaParticle].unpackedDir);
            Assert.True(files.Length > 100, $"only {files.Length} particle archives, so this proves nothing");

            int archives = 0, emitters = 0, textures = 0, undecoded = 0;
            var bySize = new SortedDictionary<int, int>();     // half-size in px, rounded → how many emitters
            var big = new List<(int file, int em, double scale, double aspect, int tex, int w, int h)>();
            var byFormat = new SortedDictionary<int, (int ok, int bad)>();

            const double ScalePx = 4096.0 / 172.0;   // the preview's world-to-pixel factor for a quad half-size

            for (int i = 0; i < files.Length; i++)
            {
                byte[] bytes;
                try { bytes = File.ReadAllBytes(files[i]); } catch { continue; }
                if (bytes.Length < 32) continue;
                SpaArchive a;
                try { a = SpaArchive.Parse(bytes); } catch { continue; }
                if (a?.Emitters == null) continue;
                archives++;

                foreach (var t in a.Textures)
                {
                    textures++;
                    var cur = byFormat.TryGetValue(t.Format, out var c) ? c : (0, 0);
                    if (t.Rgba == null) { undecoded++; byFormat[t.Format] = (cur.Item1, cur.Item2 + 1); }
                    else byFormat[t.Format] = (cur.Item1 + 1, cur.Item2);
                }

                for (int e = 0; e < a.Emitters.Count; e++)
                {
                    var em = a.Emitters[e];
                    emitters++;
                    double halfY = em.BaseScale * ScalePx;
                    double halfX = halfY * (em.Aspect <= 0 ? 1 : em.Aspect);
                    int bucket = (int)Math.Round(Math.Max(halfX, halfY) / 8.0) * 8;
                    bySize[bucket] = bySize.TryGetValue(bucket, out int n) ? n + 1 : 1;

                    // The animation can shrink as well as grow, so the size that matters is base times the
                    // largest value the animation reaches, not the base on its own.
                    double anim = Math.Max(em.SclS, Math.Max(em.SclN, em.SclE));
                    if (anim > 0) { halfX *= anim; halfY *= anim; }
                    if (Math.Max(halfX, halfY) >= 48)
                    {
                        var tx = (em.TexNo >= 0 && em.TexNo < a.Textures.Count) ? a.Textures[em.TexNo] : null;
                        big.Add((i, e, em.BaseScale, em.Aspect, em.TexNo, tx?.Width ?? 0, tx?.Height ?? 0));
                    }
                }
            }

            var sb = new StringBuilder();
            sb.Append("[Research](../../ResearchNotes.md) / [Move Research](../MoveResearch.md) / Move Animation Particle Sizes\n\n");
            sb.Append("# How big the game's particles are\n\n");
            sb.Append("Generated from the Platinum ROM by `ParticleEmitterReportTests`. Do not edit by hand.\n\n");
            sb.Append("A particle's drawn size does not come from its texture. The quad is sized by the emitter's\n");
            sb.Append("base scale, and the texture is stretched onto it, so a 32 by 32 texture and a 128 by 128 one\n");
            sb.Append("at the same base scale come out the same size on screen. The preview turns a base scale of 1.0\n");
            sb.Append($"into a half-size of {ScalePx:F1} pixels, which is 4096/172: the particle library's own unit\n");
            sb.Append("divided by the battle camera's pixels per unit.\n\n");
            sb.Append($"- {archives} particle archives read, {emitters} emitters, {textures} textures\n");
            sb.Append($"- {undecoded} textures do not decode\n\n");

            sb.Append("## Texture formats\n\n| format | decodes | does not |\n|---:|---:|---:|\n");
            foreach (var kv in byFormat) sb.Append($"| {kv.Key} | {kv.Value.ok} | {kv.Value.bad} |\n");

            sb.Append("\n## How many emitters draw at each size\n\n");
            sb.Append("Half-size in screen pixels, so a quad is twice this across. The screen is 256 by 192.\n\n");
            sb.Append("| half-size px | emitters |\n|---:|---:|\n");
            foreach (var kv in bySize) sb.Append($"| {kv.Key} | {kv.Value} |\n");

            sb.Append($"\n## The {big.Count} emitters that draw at 48 pixels or more\n\n");
            sb.Append("These are the ones worth checking against a real battle first: at this size a particle covers\n");
            sb.Append("a Pokemon. Some are real (a full-screen wave sheet is one enormous quad), some are not.\n\n");
            sb.Append("| archive | emitter | base scale | aspect | texture | texture size |\n|---:|---:|---:|---:|---:|---|\n");
            foreach (var b in big.OrderByDescending(x => x.scale).Take(60))
                sb.Append($"| {b.file} | {b.em} | {b.scale:F3} | {b.aspect:F2} | {b.tex} | {b.w}x{b.h} |\n");

            Directory.CreateDirectory(Path.GetDirectoryName(Doc));
            File.WriteAllText(Doc, sb.ToString());

            // One archive in full, when asked for, so a particle that looks wrong on screen can be read in the data.
            string one = Environment.GetEnvironmentVariable("DSPRE_PARTICLE_ARCHIVE");
            if (!string.IsNullOrWhiteSpace(one) && int.TryParse(one, out int want) && want < files.Length)
            {
                var a2 = SpaArchive.Parse(File.ReadAllBytes(files[want]));
                _out.WriteLine($"archive {want}: {a2.Emitters.Count} emitters, {a2.Textures.Count} textures");
                for (int e = 0; e < a2.Emitters.Count; e++)
                {
                    var em = a2.Emitters[e];
                    var tx = (em.TexNo >= 0 && em.TexNo < a2.Textures.Count) ? a2.Textures[em.TexNo] : null;
                    double half = em.BaseScale * ScalePx;
                    _out.WriteLine($"  emitter {e}: baseScale {em.BaseScale:F3} -> half {half:F1}px, "
                                   + $"aspect {em.Aspect:F2}, drawType {em.DrawType}, tex {em.TexNo} "
                                   + $"({tx?.Width ?? 0}x{tx?.Height ?? 0}), "
                                   + $"pos ({em.PosX:F1},{em.PosY:F1},{em.PosZ:F1}), "
                                   + $"colour ({em.ColorR},{em.ColorG},{em.ColorB}), "
                                   + $"axis ({em.AxisX:F2},{em.AxisY:F2}), shape {em.InitPosType}, "
                                   + $"emitterLife {em.EmitterLife}, particleLife {em.ParticleLife}, every {em.GenInterval}f, "
                                   + $"scaleAnim s{em.SclS:F2} n{em.SclN:F2} e{em.SclE:F2} in{em.SclIn} out{em.SclOut}, "
                                   + $"drawn half {half * Math.Max(em.SclS, Math.Max(em.SclN, em.SclE)):F1}px "
                                   + "at its largest");
                }
            }

            _out.WriteLine($"{archives} archives, {emitters} emitters, {textures} textures, {undecoded} undecoded");
            _out.WriteLine($"{big.Count} emitters draw at 48px half-size or more");
            foreach (var kv in bySize.Reverse().Take(6)) _out.WriteLine($"  half-size {kv.Key}px: {kv.Value} emitters");
            _out.WriteLine("written to " + Doc);
        }
    }
}

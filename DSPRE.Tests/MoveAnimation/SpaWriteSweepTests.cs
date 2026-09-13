using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.Avalonia.Data;
using NarcAPI;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>Every particle archive in every game opens for editing and writes back exactly as it was.</summary>
    [Collection("rom")]
    public class SpaWriteSweepTests
    {
        private readonly ITestOutputHelper _out;
        public SpaWriteSweepTests(ITestOutputHelper o) { _out = o; }

        private static string Project(string code) => code switch
        {
            "CPUE" => TestRoms.Platinum,
            "IPKE" => TestRoms.HeartGold,
            _ => TestRoms.Diamond,
        };

        /// <summary>Each particle archive's files, read without writing anything into the project.</summary>
        private static List<(string Archive, List<(string Name, byte[] Bytes)> Files)> Archives(string code)
        {
            string project = Project(code);
            Skip.IfNot(Directory.Exists(project), $"{code}: no project at {project}");
            new RomInfo(code, project);

            var sources = new List<(string, string packed, string unpacked)>();
            var waza = gameDirs[DirNames.wazaParticle];
            sources.Add(("waza_particle", waza.packedDir, waza.unpackedDir));
            if (gameDirs.TryGetValue(DirNames.ballParticles, out var ball))
                sources.Add(("ball_particle", ball.packedDir, ball.unpackedDir));
            else if (gameFamily == GameFamilies.DP)
                // Diamond and Pearl keep the ball bursts beside the move particles, but RomInfo does not map them.
                sources.Add(("ball_particle", Path.Combine(Path.GetDirectoryName(waza.packedDir), "ball_particle.narc"), null));

            var result = new List<(string, List<(string, byte[])>)>();
            foreach (var (label, packed, unpacked) in sources)
            {
                var files = new List<(string, byte[])>();
                if (unpacked != null && Directory.Exists(unpacked) && Directory.GetFiles(unpacked).Length > 0)
                    files.AddRange(RomFiles.Settled(unpacked).Select(f => (Path.GetFileName(f), File.ReadAllBytes(f))));
                else if (File.Exists(packed))
                {
                    var narc = Narc.Open(packed);
                    Assert.True(narc != null, $"{code}: {packed} is not a NARC");
                    for (int i = 0; i < narc.ElementCount; i++) files.Add((i.ToString("D4"), narc.GetElementBytes(i)));
                    narc.Free();
                }
                result.Add((label, files));
            }
            return result;
        }

        [SkippableTheory]
        [InlineData("CPUE")]
        [InlineData("IPKE")]
        [InlineData("ADAE")]
        public void EveryParticleArchiveWritesBackByteForByte(string code)
        {
            var archives = Archives(code);
            var problems = new List<string>();
            int files = 0, emitters = 0, fieldWrites = 0, textures = 0, texturesSameBytes = 0;
            var refused = new Dictionary<string, int>();

            foreach (var (archive, entries) in archives)
            {
                int archiveFiles = 0;
                foreach (var (name, bytes) in entries)
                {
                    string where = $"{archive}/{name}";
                    if (bytes.Length == 0) continue;
                    if (!SpaDocument.TryLoad(bytes, out var doc, out string why)) { problems.Add($"{where}: {why}"); continue; }
                    files++; archiveFiles++;
                    emitters += doc.EmitterCount;

                    try
                    {
                        for (int e = 0; e < doc.EmitterCount; e++)
                            foreach (var f in SpaFields.All)
                            {
                                if (!doc.Has(e, f) || f.DecidesLayout) continue;
                                doc.SetRaw(e, f, doc.GetRaw(e, f));
                                doc.SetValue(e, f, doc.GetValue(e, f));
                                fieldWrites++;
                            }
                    }
                    catch (Exception ex) { problems.Add($"{where}: rewriting a field threw {ex.Message}"); continue; }

                    if (doc.IsModified || !doc.ToBytes().AsSpan().SequenceEqual(bytes))
                    { problems.Add($"{where}: writing every field back as read changed the file"); continue; }

                    // Importing each texture's own pixels must draw identically.
                    var original = SpaArchive.Parse(bytes).Textures;
                    var retex = SpaDocument.Load(bytes);
                    for (int t = 0; t < retex.TextureCount; t++)
                    {
                        textures++;
                        var info = retex.GetTextureInfo(t);
                        if (info.CannotReplace != null)
                        {
                            refused[info.CannotReplace] = refused.TryGetValue(info.CannotReplace, out int n) ? n + 1 : 1;
                            continue;
                        }
                        var imp = retex.ReplaceTexture(t, original[t].Width, original[t].Height, original[t].Rgba);
                        if (!imp.Succeeded) { problems.Add($"{where} texture {t}: {imp.Error}"); continue; }
                        if (!imp.Rgba.AsSpan().SequenceEqual(original[t].Rgba))
                            problems.Add($"{where} texture {t} (format {info.Format}): re-importing its own pixels changed how it draws");
                        else if (!imp.PaletteKept)
                            problems.Add($"{where} texture {t}: re-importing its own pixels rewrote the palette");
                    }
                    var after = SpaArchive.Parse(retex.ToBytes()).Textures;
                    for (int t = 0; t < after.Count; t++)
                    {
                        if (!after[t].Rgba.AsSpan().SequenceEqual(original[t].Rgba))
                            problems.Add($"{where} texture {t}: the written file draws differently");
                        int pos = original[t].ResourceOffset, len = BitConverter.ToInt32(bytes, pos + 28);
                        if (retex.ToBytes().AsSpan(pos, len).SequenceEqual(bytes.AsSpan(pos, len))) texturesSameBytes++;
                    }
                }
                _out.WriteLine($"{code} {archive}: {archiveFiles} archives");
                Assert.True(archiveFiles > 50, $"{code} {archive}: only {archiveFiles} archives were checked");
            }

            _out.WriteLine($"{code}: {files} archives, {emitters} emitters, {fieldWrites} field writes, "
                           + $"{textures} textures re-imported, {texturesSameBytes} of them byte-identical");
            foreach (var kv in refused) _out.WriteLine($"  refused {kv.Value}: {kv.Key}");

            Assert.True(emitters > 500, $"{code}: only {emitters} emitters were checked");
            Assert.True(textures > 500, $"{code}: only {textures} textures were checked");
            Assert.True(problems.Count == 0, $"{problems.Count} problems:\n" + string.Join("\n", problems.Take(25)));
        }

        [SkippableFact]
        public void EveryFieldEditsInPlaceOnARealRecord()
        {
            var docs = Archives("IPKE").SelectMany(a => a.Files).Where(f => f.Bytes.Length > 0)
                                       .Select(f => (f.Name, f.Bytes, Doc: SpaDocument.Load(f.Bytes))).ToList();
            int edited = 0;
            var absent = new List<string>();
            foreach (var f in SpaFields.All.Where(x => !x.DecidesLayout))
            {
                var hit = docs.Select(d => (d.Bytes, d.Doc, Emitter: Enumerable.Range(0, d.Doc.EmitterCount).FirstOrDefault(e => d.Doc.Has(e, f), -1)))
                              .FirstOrDefault(d => d.Emitter >= 0);
                if (hit.Doc == null) { absent.Add(f.ToString()); continue; }

                var doc = SpaDocument.Load(hit.Bytes);
                long target = doc.GetRaw(hit.Emitter, f) == f.MaxRaw ? f.MinRaw : f.MaxRaw;
                doc.SetRaw(hit.Emitter, f, target);
                var written = doc.ToBytes();
                Assert.Equal(target, SpaDocument.Load(written).GetRaw(hit.Emitter, f));

                int changed = Enumerable.Range(0, written.Length).Count(i => written[i] != hit.Bytes[i]);
                Assert.True(changed >= 1 && changed <= f.Size, $"{f}: {changed} bytes changed");
                var reread = SpaDocument.Load(written);
                foreach (var other in SpaFields.All)
                    if (other != f && reread.Has(hit.Emitter, other))
                        Assert.True(reread.GetRaw(hit.Emitter, other) == hit.Doc.GetRaw(hit.Emitter, other), $"editing {f} changed {other}");
                edited++;
            }
            _out.WriteLine($"{edited} fields edited on real records; no HeartGold record carries: {string.Join(", ", absent)}");
            Assert.True(edited > 120, $"only {edited} fields were edited");
        }

        [SkippableTheory]
        [InlineData(1)]
        [InlineData(6)]
        public void AReplacedGameTextureDrawsAsTheImportReported(int format)
        {
            var hit = Archives("IPKE").SelectMany(a => a.Files).Where(f => f.Bytes.Length > 0)
                .Select(f => (f.Bytes, Doc: SpaDocument.Load(f.Bytes)))
                .SelectMany(d => Enumerable.Range(0, d.Doc.TextureCount).Select(t => (d.Bytes, d.Doc, Info: d.Doc.GetTextureInfo(t))))
                .FirstOrDefault(x => x.Info.Format == format && x.Info.PaletteColors >= 4 && x.Info.Width >= 16);
            Assert.True(hit.Doc != null, $"no format {format} texture with a 4-colour palette was found");

            var info = hit.Info;
            var img = new byte[info.Width * info.Height * 4];
            for (int y = 0; y < info.Height; y++)
                for (int x = 0; x < info.Width; x++)
                {
                    int j = (y * info.Width + x) * 4;
                    img[j] = (byte)(x * 255 / info.Width); img[j + 1] = (byte)(y * 255 / info.Height); img[j + 2] = 200;
                    img[j + 3] = (byte)(x < info.Width / 4 ? 0 : 255);
                }

            var imp = hit.Doc.ReplaceTexture(info.Index, info.Width, info.Height, img);
            Assert.True(imp.Succeeded, imp.Error);
            Assert.True(imp.Quantized);
            var written = hit.Doc.ToBytes();
            var parsed = SpaArchive.Parse(written);
            Assert.True(parsed.Textures[info.Index].Rgba.SequenceEqual(imp.Rgba), "the texture does not draw as the import reported");

            int colours = Enumerable.Range(0, info.Width * info.Height).Where(p => imp.Rgba[p * 4 + 3] > 0)
                                    .Select(p => (imp.Rgba[p * 4], imp.Rgba[p * 4 + 1], imp.Rgba[p * 4 + 2])).Distinct().Count();
            Assert.True(colours <= info.PaletteColors, $"{colours} colours from a {info.PaletteColors}-colour palette");
            Assert.True(Enumerable.Range(0, info.Width * info.Height).All(p => (imp.Rgba[p * 4 + 3] == 0) == (img[p * 4 + 3] == 0)),
                        "transparent pixels moved");

            int pos = parsed.Textures[info.Index].ResourceOffset;
            int end = pos + BitConverter.ToInt32(hit.Bytes, pos + 28);
            for (int k = 0; k < written.Length; k++)
                if (written[k] != hit.Bytes[k]) Assert.True(k >= pos + 32 && k < end, $"byte 0x{k:X} outside the texture changed");
            Assert.Equal(SpaArchive.Parse(hit.Bytes).Emitters.Count, parsed.Emitters.Count);
            _out.WriteLine($"format {format} {info.Width}x{info.Height}, {info.PaletteColors}-colour palette: {colours} colours drawn");
        }
    }
}

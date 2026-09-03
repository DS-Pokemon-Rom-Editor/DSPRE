using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>Every particle a move uses must be drawn as itself.</summary>
    [Collection("rom")]
    public class ParticleTextureDecodeTests
    {
        private readonly ITestOutputHelper _out;
        public ParticleTextureDecodeTests(ITestOutputHelper o) { _out = o; }

        private static readonly string HeartGold = TestRoms.HeartGold;
        private static readonly string Platinum = TestRoms.Platinum;

        [Theory]
        [InlineData("CPUE")]
        [InlineData("IPKE")]
        public void EveryParticleTextureDecodes(string code)
        {
            string project = code == "CPUE" ? Platinum : HeartGold;
            Assert.True(Directory.Exists(project), $"{code}: no unpacked project, so nothing was checked");
            new RomInfo(code, project);

            var files = RomFiles.Settled(gameDirs[DirNames.wazaParticle].unpackedDir);
            Assert.True(files.Length > 100, $"{code}: only {files.Length} particle archives, so this proves nothing");

            int archives = 0, textures = 0, failed = 0;
            var byFormat = new Dictionary<int, int>();
            var failedArchives = new List<int>();

            for (int i = 0; i < files.Length; i++)
            {
                byte[] bytes;
                try { bytes = File.ReadAllBytes(files[i]); } catch { continue; }
                if (bytes.Length < 32) continue;
                SpaArchive a;
                try { a = SpaArchive.Parse(bytes); } catch { continue; }
                if (a?.Textures == null) continue;
                archives++;
                bool bad = false;
                foreach (var t in a.Textures)
                {
                    textures++;
                    if (t.Rgba != null) continue;
                    failed++; bad = true;
                    byFormat[t.Format] = byFormat.TryGetValue(t.Format, out int n) ? n + 1 : 1;
                }
                if (bad) failedArchives.Add(i);
            }

            _out.WriteLine($"{code}: {archives} particle archives, {textures} textures, {failed} do not decode");
            foreach (var kv in byFormat.OrderByDescending(k => k.Value))
                _out.WriteLine($"  format {kv.Key}: {kv.Value} textures");
            if (failedArchives.Count > 0)
                _out.WriteLine($"  archives affected: {failedArchives.Count}, first few: "
                               + string.Join(", ", failedArchives.Take(12)));

            Assert.True(failed == 0,
                $"{code}: {failed} of {textures} particle textures cannot be drawn as themselves and fall back "
                + $"to a plain dot, across {failedArchives.Count} archives");
        }
    }
}

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
    /// <summary>Every particle archive in both games, read with our own parser.</summary>
    [Collection("rom")]
    public class SpaArchiveSweepTests
    {
        private readonly ITestOutputHelper _out;
        public SpaArchiveSweepTests(ITestOutputHelper o) { _out = o; }

        private static readonly string HeartGold = TestRoms.HeartGold;
        private static readonly string Platinum = TestRoms.Platinum;

        private static string ArchiveDir(string project, string gameCode)
        {
            if (!Directory.Exists(project)) return null;
            try { new RomInfo(gameCode, project); } catch { return null; }
            if (!gameDirs.ContainsKey(DirNames.wazaParticle)) return null;
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.wazaParticle });
            var dir = gameDirs[DirNames.wazaParticle].unpackedDir;
            return Directory.Exists(dir) ? dir : null;
        }

        [Fact]
        public void EveryHeartGoldParticleArchiveReadsRightUpToItsTextures()
            => Sweep(HeartGold, "IPKE");

        [Fact]
        public void EveryPlatinumParticleArchiveReadsRightUpToItsTextures()
            => Sweep(Platinum, "CPUE");

        private void Sweep(string project, string gameCode)
        {
            string dir = ArchiveDir(project, gameCode);
            Assert.True(dir != null, gameCode + ": the particle archive could not be unpacked, so nothing was checked");

            var problems = new List<string>();
            int archives = 0, emitters = 0, textures = 0, empty = 0;
            var flagsSeen = new SortedSet<int>();

            foreach (var f in RomFiles.Settled(dir))
            {
                var bytes = File.ReadAllBytes(f);
                if (bytes.Length == 0) { empty++; continue; }
                string name = Path.GetFileName(f);

                var a = SpaArchive.Parse(bytes);
                if (a.EmitterCountInHeader == 0 && a.TextureCount == 0) { empty++; continue; }

                archives++;
                emitters += a.Emitters.Count;
                textures += a.TextureCount;

                if (a.Emitters.Count != a.EmitterCountInHeader)
                    problems.Add($"{name}: header says {a.EmitterCountInHeader} emitters, {a.Emitters.Count} read");
                else if (a.EmittersEndAt != a.TextureOffset)
                    problems.Add($"{name}: emitters ended at {a.EmittersEndAt}, textures start at {a.TextureOffset}"
                                 + $" (out by {a.EmittersEndAt - a.TextureOffset})");

                foreach (var e in a.Emitters) flagsSeen.Add(e.InitPosType);
            }

            _out.WriteLine($"{gameCode}: {archives} archives, {emitters} emitters, {textures} textures, {empty} empty entries");
            _out.WriteLine($"  emission shapes used: {string.Join(", ", flagsSeen)}");

            Assert.True(archives >= 100, $"only {archives} archives had anything in them");
            Assert.True(emitters >= 500, $"only {emitters} emitters were read");
            Assert.True(problems.Count == 0,
                $"{problems.Count} of {archives} archives did not read cleanly:\n" + string.Join("\n", problems.Take(25)));
        }
    }
}

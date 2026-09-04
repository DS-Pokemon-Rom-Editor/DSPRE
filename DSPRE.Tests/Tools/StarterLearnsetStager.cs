using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using NarcAPI;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>
    /// Puts four chosen moves in the player's hands, by rewriting the starter's learnset.
    ///
    /// A starter is received at level 5 knowing the last four moves it can learn at or below level 5, so
    /// writing a learnset whose entries up to level 5 are exactly those four hands them to the player
    /// without touching party data at all. That is what makes a move castable from the BOTTOM of the
    /// screen, which matters because a move looks different from each side and half the corpus is cast by
    /// the enemy in a real battle.
    ///
    /// A tool rather than a check: it does nothing unless DSPRE_STARTER_MOVES names four moves. Works on a
    /// copy of the project; the real one is never touched.
    /// </summary>
    [Collection("rom")]
    public class StarterLearnsetStager
    {
        private readonly ITestOutputHelper _out;
        public StarterLearnsetStager(ITestOutputHelper o) { _out = o; }

        private static readonly string Source = TestRoms.Platinum;

        private static readonly string Scratch =
            @"C:\Users\safea\AppData\Local\Temp\claude\C--Romhacking-Tooling-DSPRE\4e7579f1-fc54-4253-a949-eba625115ec9\scratchpad";

        private const int Turtwig = 387;   // the starter 00_before_starter.State is positioned to choose

        private const int BitsMove = 9;

        private static void CopyTree(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (var d in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(d.Replace(from, to));
            foreach (var f in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
                File.Copy(f, f.Replace(from, to), overwrite: true);
        }

        [SkippableFact]
        public void GiveTheStarterTheMovesNamedInTheEnvironment()
        {
            string list = Environment.GetEnvironmentVariable("DSPRE_STARTER_MOVES");
            Skip.If(string.IsNullOrWhiteSpace(list), "DSPRE_STARTER_MOVES not set; nothing to do");

            var moves = list.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => int.Parse(x.Trim())).ToList();
            Assert.True(moves.Count == 4, $"four moves are needed, {moves.Count} were given");
            Assert.True(Directory.Exists(Source), "the Platinum project is not there");

            string work = Path.Combine(Scratch, "plat_starter");
            if (!Directory.Exists(work)) { _out.WriteLine("copying the project once"); CopyTree(Source, work); }

            new RomInfo("CPUE", work);
            string dir = gameDirs[DirNames.learnsets].unpackedDir;
            string path = Directory.GetFiles(dir).First(p => Path.GetFileName(p).StartsWith(Turtwig.ToString("D4"), StringComparison.Ordinal));

            // Levels 1, 1, 3 and 5, so all four are known by the time it is handed over and the last four
            // learnable at or below 5 are exactly these.
            var levels = new byte[] { 1, 1, 3, 5 };
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                for (int i = 0; i < 4; i++)
                {
                    ushort entry = (ushort)((moves[i] & ((1 << BitsMove) - 1)) | (levels[i] << BitsMove));
                    w.Write(entry);
                }
                w.Write((ushort)0xFFFF);
                w.Write((ushort)0x0000);
                File.WriteAllBytes(path, ms.ToArray());
            }

            foreach (var kvp in gameDirs)
            {
                var di = new DirectoryInfo(kvp.Value.unpackedDir);
                if (di.Exists) Narc.FromFolder(kvp.Value.unpackedDir).Save(kvp.Value.packedDir);
            }

            string outRom = Path.Combine(Scratch, "roms", "plat_starter_" + string.Join("_", moves) + ".nds");
            Directory.CreateDirectory(Path.GetDirectoryName(outRom));
            bool ok = DSUtils.RepackROM(outRom);
            Assert.True(ok && File.Exists(outRom), "building the ROM failed");

            // Read it back so this cannot silently write the wrong thing.
            var check = new List<(int lv, int mv)>();
            foreach (var e in File.ReadAllBytes(path).Chunk(2).Select(b => (ushort)(b[0] | (b[1] << 8))))
            {
                if (e == 0xFFFF) break;
                check.Add((e >> BitsMove, e & ((1 << BitsMove) - 1)));
            }
            _out.WriteLine($"Turtwig's learnset is now: {string.Join(", ", check.Select(c => $"lv{c.lv} move {c.mv}"))}");
            Assert.Equal(moves, check.Select(c => c.mv).ToList());
            _out.WriteLine("built " + outRom);
        }
    }
}

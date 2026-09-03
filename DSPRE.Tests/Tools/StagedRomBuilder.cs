using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.ROMFiles;
using NarcAPI;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>
    /// Builds one Platinum ROM per move to be recorded, on a copy of the project.
    ///
    /// The savestates sit on the rival's prompt and the battle reads its party out of the ROM when it
    /// starts, so giving the rival's Chimchar a single move four times over means the only animation the
    /// recording can show is the one being measured. The user's own project and ROM are never touched.
    ///
    /// This is a tool rather than a check, so it is skipped unless the move list is passed in through the
    /// DSPRE_STAGE_MOVES environment variable, as a comma-separated list of move numbers.
    /// </summary>
    [Collection("rom")]
    public class StagedRomBuilder
    {
        private readonly ITestOutputHelper _out;
        public StagedRomBuilder(ITestOutputHelper o) { _out = o; }

        private static readonly string Source = TestRoms.Platinum;

        private static readonly string Scratch =
            @"C:\Users\safea\AppData\Local\Temp\claude\C--Romhacking-Tooling-DSPRE\4e7579f1-fc54-4253-a949-eba625115ec9\scratchpad";

        // The rival's Chimchar in the TURTWIG_vs_CHIMCHAR battle. The party FILE index is one below the
        // trainer number the savestate's filename uses: 0850 Turtwig, 0851 Chimchar, 0852 Piplup.
        private const int RivalChimchar = 851;

        private static void CopyTree(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (var d in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(d.Replace(from, to));
            foreach (var f in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
                File.Copy(f, f.Replace(from, to), overwrite: true);
        }

        [Fact]
        public void BuildTheRomsNamedInTheEnvironment()
        {
            string list = Environment.GetEnvironmentVariable("DSPRE_STAGE_MOVES");
            if (string.IsNullOrWhiteSpace(list)) { _out.WriteLine("DSPRE_STAGE_MOVES not set; nothing to build"); return; }

            var moves = list.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => int.Parse(x.Trim())).ToList();
            Assert.True(moves.Count > 0, "DSPRE_STAGE_MOVES was set but held no move numbers");
            Assert.True(Directory.Exists(Source), "the Platinum project is not there, so nothing was built");

            string work = Path.Combine(Scratch, "plat_staged");
            if (!Directory.Exists(work)) { _out.WriteLine("copying the project once"); CopyTree(Source, work); }

            string outDir = Path.Combine(Scratch, "roms");
            Directory.CreateDirectory(outDir);

            int built = 0;
            foreach (int move in moves)
            {
                string outRom = Path.Combine(outDir, $"plat_move{move:D3}.nds");
                if (File.Exists(outRom)) { _out.WriteLine($"move {move}: already built"); continue; }

                new RomInfo("CPUE", work);
                string suffix = RivalChimchar.ToString("D4");
                string propPath = Directory.GetFiles(gameDirs[DirNames.trainerProperties].unpackedDir)
                    .First(p => Path.GetFileName(p).StartsWith(suffix, StringComparison.Ordinal));
                string partyPath = Directory.GetFiles(gameDirs[DirNames.trainerParty].unpackedDir)
                    .First(p => Path.GetFileName(p).StartsWith(suffix, StringComparison.Ordinal));

                var trp = new TrainerProperties((ushort)RivalChimchar, new MemoryStream(File.ReadAllBytes(propPath)));
                var tf = new TrainerFile(trp, new MemoryStream(File.ReadAllBytes(partyPath)), "AAAAAAA");
                tf.trp.chooseMoves = true;

                // Some moves do nothing at all against a trainer with one Pokemon and so never animate:
                // Whirlwind has nothing to force out and Baton Pass has nobody to pass to, and both battles
                // ended with the move never playing. Those need a second Pokemon on the other side.
                bool needsPartner = Environment.GetEnvironmentVariable("DSPRE_STAGE_PARTNER") == "1";
                if (needsPartner && tf.trp.partyCount < 2 && tf.party[0] != null)
                {
                    tf.party[1] = tf.party[0];
                    tf.trp.partyCount = 2;
                }

                int touched = 0;
                for (int i = 0; i < tf.trp.partyCount; i++)
                {
                    var poke = tf.party[i];
                    if (poke == null) continue;
                    poke.moves = new ushort[] { (ushort)move, (ushort)move, (ushort)move, (ushort)move };
                    touched++;
                }
                Assert.True(touched > 0, $"move {move}: the rival has no Pokemon, so nothing was staged");

                File.WriteAllBytes(propPath, tf.trp.ToByteArray());
                File.WriteAllBytes(partyPath, tf.party.ToByteArray());

                foreach (var kvp in gameDirs)
                {
                    var di = new DirectoryInfo(kvp.Value.unpackedDir);
                    if (di.Exists) Narc.FromFolder(kvp.Value.unpackedDir).Save(kvp.Value.packedDir);
                }

                bool ok = DSUtils.RepackROM(outRom);
                Assert.True(ok && File.Exists(outRom), $"move {move}: building the ROM failed");
                built++;
                _out.WriteLine($"move {move}: built {Path.GetFileName(outRom)}");
            }
            _out.WriteLine($"{built} ROMs built, {moves.Count - built} already there");
        }
    }
}

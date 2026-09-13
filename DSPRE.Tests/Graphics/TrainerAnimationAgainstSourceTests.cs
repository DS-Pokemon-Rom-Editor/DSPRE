using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DSPRE;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>
    /// Checks the animation reader against hg-engine's own JSON for the same trainer classes. That checkout
    /// stores each class's animation and layout as text it builds the ROM files from, so it is an
    /// independent statement of what the file holds rather than another reading by the same code.
    /// </summary>
    [Collection("rom")]
    public class TrainerAnimationAgainstSourceTests
    {
        private readonly ITestOutputHelper _out;
        public TrainerAnimationAgainstSourceTests(ITestOutputHelper output) => _out = output;

        /// <summary>hg-engine's trainer graphics source, or null when that checkout is not here.</summary>
        private static string SourceFolder()
        {
            foreach (string root in new[]
                     {
                         Environment.GetEnvironmentVariable("DSPRE_HGENGINE"),
                         @"C:\Romhacking\ROMs\NDS\HGSS\hg-engine",
                     })
            {
                if (string.IsNullOrEmpty(root)) continue;
                string at = Path.Combine(root, "data", "graphics", "trainer_gfx");
                if (Directory.Exists(at)) return at;
            }
            return null;
        }

        private static bool Open()
        {
            if (!Directory.Exists(TestRoms.HeartGold)) return false;
            try { new RomInfo("IPKE", TestRoms.HeartGold); GraphicAssets.Forget(); } catch { return false; }
            return true;
        }

        private static byte[] Member(ScriptNarc narc, int i)
        {
            try { return NitroBgCodec.Inflate(narc.Get(i)); } catch { return null; }
        }

        /// <summary>
        /// Five files to a class: drawing, colours, layout, animation, second picture. hg-engine names its
        /// source files by class, so class n's animation is member 5n+3.
        /// </summary>
        [SkippableTheory]
        [InlineData(73)]
        [InlineData(101)]
        [InlineData(97)]
        [InlineData(89)]
        public void TheReaderAgreesWithHgEnginesOwnSourceForAClass(int cls)
        {
            string folder = SourceFolder();
            Skip.If(folder == null, "the hg-engine checkout is not here");
            Skip.If(!Open(), "HeartGold is not unpacked here");

            string animJson = Path.Combine(folder, $"{cls:d3}_anim.json");
            string cellJson = Path.Combine(folder, $"{cls:d3}_cell.json");
            Skip.If(!File.Exists(animJson), $"hg-engine has no source for class {cls}");

            using var anim = JsonDocument.Parse(File.ReadAllText(animJson));
            var root = anim.RootElement;
            int saidSequences = root.GetProperty("sequenceCount").GetInt32();
            int saidFrames = root.GetProperty("frameCount").GetInt32();
            int saidResults = root.GetProperty("resultCount").GetInt32();

            // What each frame points at, sequence by sequence, straight out of the source.
            var saidPerSequence = new List<List<(int Result, int Delay)>>();
            foreach (var seq in root.GetProperty("sequences").EnumerateArray())
            {
                var frames = new List<(int, int)>();
                foreach (var f in seq.GetProperty("frameData").EnumerateArray())
                    frames.Add((f.GetProperty("resultId").GetInt32(), f.GetProperty("frameDelay").GetInt32()));
                saidPerSequence.Add(frames);
            }

            var narc = new ScriptNarc(DirNames.trainerGraphics);
            Skip.If(!narc.Available, "HeartGold has no trainer graphics archive");

            var file = NanrFile.Read(Member(narc, cls * 5 + 3));
            Assert.NotNull(file);

            _out.WriteLine($"class {cls}: source says {saidSequences} sequences, {saidFrames} frames, "
                         + $"{saidResults} results; the reader says {file.Sequences.Count} sequences, "
                         + $"{file.Sequences.Sum(s => s.Frames.Count)} frames");

            Assert.Equal(saidSequences, file.Sequences.Count);
            Assert.Equal(saidFrames, file.Sequences.Sum(s => s.Frames.Count));

            // A frame names a result, and the result names a drawing. They are not the same numbering: one
            // class here has ten results over four drawings, so comparing a result id to a drawing number
            // fails on exactly the files worth checking.
            var resultToCell = new List<int>();
            foreach (var r in root.GetProperty("animationResults").EnumerateArray())
                resultToCell.Add(r.GetProperty("index").GetInt32());
            Assert.Equal(saidResults, resultToCell.Count);

            for (int s = 0; s < saidPerSequence.Count; s++)
            {
                var said = saidPerSequence[s];
                Assert.Equal(said.Count, file.Sequences[s].Frames.Count);
                for (int f = 0; f < said.Count; f++)
                {
                    int wantCell = resultToCell[said[f].Result];
                    Assert.Equal(wantCell, file.CellOf(s, f));
                    Assert.Equal(said[f].Delay, file.Sequences[s].Frames[f].Delay);
                }
                _out.WriteLine($"  sequence {s}: {said.Count} frames agree, results "
                             + string.Join(",", said.Select(x => x.Result))
                             + " -> drawings "
                             + string.Join(",", said.Select(x => resultToCell[x.Result])));
            }

            // And the layout really does hold as many drawings as the animation names.
            if (File.Exists(cellJson))
            {
                using var cell = JsonDocument.Parse(File.ReadAllText(cellJson));
                int saidCells = cell.RootElement.GetProperty("cellCount").GetInt32();
                bool transfer = cell.RootElement.TryGetProperty("vramTransferEnabled", out var v) && v.GetBoolean();
                var banks = DsBgScreen.ReadCells(Member(narc, cls * 5 + 2));
                _out.WriteLine($"  layout: source says {saidCells} drawings, transfer {transfer}; "
                             + $"the reader found {banks.Count}");
                Assert.Equal(saidCells, banks.Count);

                // Every drawing the animation names has to exist in that layout.
                int top = -1;
                for (int s = 0; s < file.Sequences.Count; s++)
                    for (int f = 0; f < file.Sequences[s].Frames.Count; f++)
                        top = Math.Max(top, file.CellOf(s, f));
                Assert.True(top < banks.Count,
                            $"class {cls} names drawing {top} and the layout holds {banks.Count}");

                // A pose's pieces butt up against one another with nothing missed and nothing read twice.
                // Left unscaled by the mapping boundary, the pieces after the body overlap it, which is
                // what drew a trainer's arms as fragments strewn around the body.
                //
                // This holds for layouts the game transfers a slice at a time, which is what these trainer
                // files are. It is not true of every layout: the synthetic overlays and the trainer card
                // deliberately point two pieces at the same tiles, and asserting it there would call
                // correct data broken.
                Assert.True(transfer, $"class {cls} is expected to use a per-cell transfer");
                foreach (var bank in banks)
                {
                    var pieces = bank.Where(p => p != null)
                                     .OrderBy(p => p.Tile)
                                     .ToList();
                    if (pieces.Count < 2) continue;
                    int want = pieces[0].Tile;
                    foreach (var piece in pieces)
                    {
                        Assert.Equal(want, piece.Tile);
                        want += Math.Max(1, piece.Width / 8) * Math.Max(1, piece.Height / 8);
                    }
                }
                _out.WriteLine($"  every pose's pieces run end to end across {banks.Count} drawings");
            }
        }

        /// <summary>
        /// How many poses these animations really have, so a showcase picks a telling one. Most classes hold
        /// a single drawing; the richest hold twelve. Measured from hg-engine's source rather than asserted.
        /// </summary>
        [SkippableFact]
        public void MostTrainerClassesHoldOnePoseAndTheRichestHoldTwelve()
        {
            string folder = SourceFolder();
            Skip.If(folder == null, "the hg-engine checkout is not here");

            var byCount = new SortedDictionary<int, int>();
            int looked = 0, most = 0, mostAt = -1;
            foreach (string path in Directory.EnumerateFiles(folder, "*_cell.json"))
            {
                int cells;
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(path));
                    cells = doc.RootElement.GetProperty("cellCount").GetInt32();
                }
                catch { continue; }
                looked++;
                byCount.TryGetValue(cells, out int n);
                byCount[cells] = n + 1;
                if (cells > most)
                {
                    most = cells;
                    int.TryParse(Path.GetFileName(path).Substring(0, 3), out mostAt);
                }
            }

            Skip.If(looked == 0, "no trainer cell sources could be read");
            _out.WriteLine($"{looked} classes: "
                         + string.Join(", ", byCount.Select(kv => $"{kv.Value} with {kv.Key}")));
            _out.WriteLine($"the richest is class {mostAt} with {most} drawings");

            Assert.True(looked > 100, $"only {looked} classes were read");
            Assert.True(most >= 8, $"the richest class holds only {most} drawings, which reads wrong");
            Assert.True(byCount.ContainsKey(1) && byCount[1] > looked / 2,
                        "most classes are expected to hold a single drawing");
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// The type, contest and move-category icons in the battle furniture archive. These record no size
    /// of their own and share one set of colours in three banks, so DSPRE has to be told both; getting
    /// either wrong shows a scrambled half-width picture or the wrong bank's colours, and nothing in
    /// the file itself would say so.
    /// </summary>
    [Collection("rom")]
    public class BattleIconTests
    {
        private readonly ITestOutputHelper _out;
        public BattleIconTests(ITestOutputHelper o) => _out = o;

        private static readonly (string code, string path, string name)[] Games =
        {
            ("ADAE", TestRoms.Diamond, "Diamond"),
            ("CPUE", TestRoms.Platinum, "Platinum"),
            ("IPKE", TestRoms.HeartGold, "HeartGold"),
        };

        private static IEnumerable<(int index, string thing)> IconsIn()
        {
            var names = BattleObjects.Names();
            for (int i = 0; i < names.Count; i++)
                if (names[i] != null && names[i].StartsWith("P_ST_", StringComparison.Ordinal)
                                     && names[i].EndsWith("_NCGR_BIN", StringComparison.Ordinal))
                    yield return (i, names[i]);
        }

        /// <summary>
        /// Eighteen types, five contest conditions and three move categories, in every game. A count
        /// short of that means the name list and the archive have drifted apart.
        /// </summary>
        [Fact]
        public void EveryGameHasTheSameTwentySixIcons()
        {
            int played = 0;
            foreach (var (code, path, name) in Games)
            {
                if (!Directory.Exists(path)) { _out.WriteLine($"{name}: not unpacked here, skipped"); continue; }
                try { new RomInfo(code, path); }
                catch (Exception ex) { _out.WriteLine($"{name}: would not load ({ex.Message}), skipped"); continue; }

                var icons = IconsIn().ToList();
                _out.WriteLine($"{name}: {icons.Count} icons");
                Assert.Equal(26, icons.Count);
                played++;
            }
            Assert.True(played > 0, "no game was unpacked here, so this proved nothing");
        }

        /// <summary>
        /// Every icon is thirty two by sixteen and comes back in colour. Before this was told to DSPRE
        /// they were guessed at sixteen by thirty two, which cut every word in half and stacked it.
        /// </summary>
        [Fact]
        public void EveryIconIsThirtyTwoBySixteenAndIsPainted()
        {
            int played = 0, drawn = 0;
            foreach (var (code, path, name) in Games)
            {
                if (!Directory.Exists(path)) { _out.WriteLine($"{name}: not unpacked here, skipped"); continue; }
                try { new RomInfo(code, path); }
                catch (Exception ex) { _out.WriteLine($"{name}: would not load ({ex.Message}), skipped"); continue; }

                var archive = GraphicAssets.All.First(a => a.Dir == RomInfo.DirNames.battleObj);
                foreach (var (index, thing) in IconsIn())
                {
                    var p = GraphicAssets.Render(archive, index);
                    Assert.True(p?.Rgba != null, $"{name} {thing}: {p?.Whynot ?? "nothing came back"}");
                    Assert.Equal(32, p.Width);
                    Assert.Equal(16, p.Height);

                    // An icon painted out of an empty bank comes back all one colour, which is what the
                    // wrong bank looked like: solid black with the writing lost in it.
                    var seen = new HashSet<uint>();
                    for (int at = 0; at + 3 < p.Rgba.Length; at += 4)
                        seen.Add(BitConverter.ToUInt32(p.Rgba, at));
                    Assert.True(seen.Count >= 3,
                        $"{name} {thing} came back in only {seen.Count} colours, so the bank is wrong");
                    drawn++;
                }
                played++;
            }
            _out.WriteLine($"{drawn} icons across {played} games");
            Assert.True(played > 0, "no game was unpacked here, so this proved nothing");
        }

        /// <summary>
        /// The icons are painted out of ST_TYPE's colours, in three banks of sixteen. If the set they
        /// were paired with held only one bank, two thirds of them would come out in the wrong colours
        /// and still look like a picture, which is exactly what happened before this was wired up.
        /// </summary>
        [Fact]
        public void TheColoursTheIconsSharedHaveThreeDifferentBanks()
        {
            int played = 0;
            foreach (var (code, path, name) in Games)
            {
                if (!Directory.Exists(path)) { _out.WriteLine($"{name}: not unpacked here, skipped"); continue; }
                try { new RomInfo(code, path); }
                catch (Exception ex) { _out.WriteLine($"{name}: would not load ({ex.Message}), skipped"); continue; }

                var names = BattleObjects.Names();
                var first = IconsIn().First();
                int pal = BattleObjects.ColoursFor(first.index);
                Assert.True(pal >= 0, $"{name}: the icons were paired with no colours at all");
                Assert.Equal("ST_TYPE", BattleObjects.Split(names[pal]).Thing);

                var narc = new ScriptNarc(RomInfo.DirNames.battleObj);
                var colours = NitroBgCodec.ReadPalette(
                    GraphicAssets.Unsqueeze(narc.Get(pal)), out int count);
                _out.WriteLine($"{name}: {names[pal]} holds {count} colours");
                Assert.True(count >= 48, $"{name}: only {count} colours, so there are not three banks");

                var banks = Enumerable.Range(0, 3)
                    .Select(b => string.Join(",", Enumerable.Range(0, 16)
                        .Select(i => colours[b * 16 + i].ToString())))
                    .ToList();
                Assert.Equal(3, banks.Distinct().Count());
                played++;
            }
            Assert.True(played > 0, "no game was unpacked here, so this proved nothing");
        }

        /// <summary>
        /// The table above is only worth anything if the archive actually asks it. Both hooks were
        /// written before anything was wired to them, and the icons went on being drawn from bank zero.
        /// </summary>
        [Fact]
        public void TheArchiveAsksForTheWidthAndTheBank()
        {
            var archive = GraphicAssets.All.First(a => a.Dir == RomInfo.DirNames.battleObj);
            Assert.NotNull(archive.PixelWidthOf);
            Assert.NotNull(archive.ColourBank);
        }
    }
}

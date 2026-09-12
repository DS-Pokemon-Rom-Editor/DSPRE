using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels.Graphics;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// Closes the gaps the round-trip suite leaves. That sweep only reaches the archives DSPRE has names
    /// for; this one walks the game's own file tree, so "every animation file" means every one in the game
    /// rather than every one we happen to describe.
    /// </summary>
    [Collection("rom")]
    public class NanrCoverageTests
    {
        private readonly ITestOutputHelper _out;
        public NanrCoverageTests(ITestOutputHelper output) => _out = output;

        private static bool Open(string project, string id)
        {
            if (!Directory.Exists(project)) return false;
            try { new RomInfo(id, project); } catch { return false; }
            return true;
        }

        /// <summary>Every packed archive in the game's own file tree, whatever DSPRE calls it.</summary>
        private static IEnumerable<string> EveryArchive(string project)
        {
            string files = Path.Combine(project, "files");
            if (!Directory.Exists(files)) yield break;
            foreach (string path in Directory.EnumerateFiles(files, "*", SearchOption.AllDirectories))
            {
                byte[] head;
                try
                {
                    using var s = File.OpenRead(path);
                    head = new byte[4];
                    if (s.Read(head, 0, 4) < 4) continue;
                }
                catch { continue; }
                if (head[0] == 'N' && head[1] == 'A' && head[2] == 'R' && head[3] == 'C') yield return path;
            }
        }

        private void SweepWholeTree(string project, string id, string game)
        {
            Skip.If(!Open(project, id), $"{game} is not unpacked here");

            int archives = 0, animations = 0, extended = 0, compressed = 0;
            var broken = new List<string>();

            foreach (string path in EveryArchive(project))
            {
                NarcAPI.Narc narc;
                try { narc = NarcAPI.Narc.Open(path); } catch { continue; }
                if (narc == null) continue;
                archives++;

                for (int i = 0; i < narc.ElementCount; i++)
                {
                    byte[] stored, raw;
                    try
                    {
                        stored = narc.GetElementBytes(i);
                        raw = NitroBgCodec.Inflate(stored);
                    }
                    catch { continue; }
                    if (raw == null || raw.Length < 0x30) continue;
                    if (raw[0] != 'R' || raw[1] != 'N' || raw[2] != 'A' || raw[3] != 'N') continue;
                    if (!ReferenceEquals(raw, stored)) compressed++;

                    var f = NanrFile.Read(raw);
                    if (f == null) { broken.Add($"{Path.GetFileName(path)}#{i} would not read"); continue; }

                    animations++;
                    if (f.HasExtendedData) extended++;

                    byte[] back = f.Write();
                    if (!back.SequenceEqual(raw))
                    {
                        int at = 0;
                        while (at < Math.Min(back.Length, raw.Length) && back[at] == raw[at]) at++;
                        broken.Add($"{Path.GetFileName(path)}#{i} differs at 0x{at:X}");
                    }
                }
            }

            _out.WriteLine($"{game}: {archives} archives, {animations} animation files, "
                         + $"{extended} with an extended block, {compressed} compressed");

            Assert.True(archives > 0, "no archives were found in the file tree");
            Assert.True(animations > 0, "no animation files were found");
            Assert.True(broken.Count == 0,
                $"{broken.Count} of {animations} did not survive: {string.Join("; ", broken.Take(8))}");
        }

        [SkippableFact]
        public void EveryAnimationInPlatinumsWholeFileTreeComesBackUnchanged()
            => SweepWholeTree(TestRoms.Platinum, "CPUE", "Platinum");

        [SkippableFact]
        public void EveryAnimationInHeartGoldsWholeFileTreeComesBackUnchanged()
            => SweepWholeTree(TestRoms.HeartGold, "IPKE", "HeartGold");

        /// <summary>
        /// The picker works out which layout an animation draws from by looking either side of it. That is a
        /// guess, so it is checked against the Pokétch map, whose pairings were read from the games' own
        /// source rather than inferred.
        /// </summary>
        [SkippableFact]
        public void ThePickersGuessAtAPairingAgreesWithWhatIsKnown()
        {
            Skip.If(!Open(TestRoms.Platinum, "CPUE"), "Platinum is not unpacked here");

            var found = CellAnimationPickerViewModel.InArchive(RomInfo.DirNames.poketch);
            Assert.NotEmpty(found);

            int checkedPairs = 0, agreed = 0;
            var wrong = new List<string>();
            foreach (var app in PoketchApps.All.Where(a => a.Animation >= 0 && a.Cells >= 0))
            {
                var guess = found.FirstOrDefault(f => f.Animation == app.Animation);
                if (guess == null) continue;
                checkedPairs++;
                if (guess.Cells == app.Cells) agreed++;
                else wrong.Add($"{app.Name}: guessed layout {guess.Cells}, the game uses {app.Cells}");
            }

            _out.WriteLine($"{agreed} of {checkedPairs} pairings agreed with the Pokétch map");
            Assert.True(checkedPairs >= 15, $"only {checkedPairs} pairings could be compared");
            Assert.True(wrong.Count == 0, string.Join("; ", wrong.Take(8)));
        }

        /// <summary>
        /// A Pokétch drawing taken out as a PNG and read back carries the same pixels and colours. Nothing is
        /// written to a project here: this is the picture format on its own, which is the part that had never
        /// been exercised on these files.
        /// </summary>
        [SkippableFact]
        public void APoketchDrawingSurvivesBeingTakenOutAsAPngAndReadBack()
        {
            Skip.If(!Open(TestRoms.Platinum, "CPUE"), "Platinum is not unpacked here");

            var archive = GraphicAssets.All.FirstOrDefault(a => a.Dir == RomInfo.DirNames.poketch);
            Assert.NotNull(archive);

            // The casing, the watch face and the picture shown before you have one.
            int[] drawings = { 14, 23, 10 };
            int checkedDrawings = 0;

            foreach (int at in drawings)
            {
                var art = GraphicAssets.ReadIndexed(archive, at, out string whynot);
                Assert.True(art != null, $"member {at} would not read: {whynot}");

                var allowed = art.Palette.Length > art.ColourCount
                    ? art.Palette.Take(art.ColourCount).ToArray()
                    : art.Palette;

                byte[] png = IndexedPng.Write(art.Indices, allowed, art.Width, art.Height);
                Assert.True(png != null && png.Length > 0, $"member {at} produced no PNG");

                Assert.True(IndexedPng.TryRead(png, out byte[] back, out uint[] pal, out int w, out int h),
                            $"the PNG for member {at} could not be read back");

                Assert.Equal(art.Width, w);
                Assert.Equal(art.Height, h);
                Assert.Equal(art.Indices.Length, back.Length);
                Assert.Equal(art.Indices, back);

                // Every colour the drawing is allowed comes back in the same order.
                Assert.True(pal.Length >= allowed.Length,
                            $"member {at} lost colours: {allowed.Length} became {pal.Length}");
                for (int i = 0; i < allowed.Length; i++)
                    Assert.Equal(allowed[i] & 0x00FFFFFFu, pal[i] & 0x00FFFFFFu);

                checkedDrawings++;
                _out.WriteLine($"member {at}: {w}x{h}, {allowed.Length} colours, "
                             + $"{art.Indices.Length} pixels, all carried");
            }

            Assert.Equal(drawings.Length, checkedDrawings);
        }
    }
}

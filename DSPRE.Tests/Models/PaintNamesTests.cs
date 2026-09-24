using DSPRE.Models;
using LibNDSFormats.NSBMD;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests.Models
{
    public class PaintNamesTests
    {
        private readonly ITestOutputHelper _out;
        public PaintNamesTests(ITestOutputHelper o) { _out = o; }

        [SkippableTheory]
        [InlineData("Platinum")]
        [InlineData("HeartGold")]
        public void EveryNameAMapPaintsByIsFoundWhereTheKnownReaderFindsIt(string game)
        {
            string project = game == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            bool hgss = game == "HeartGold";
            Skip.If(!Directory.Exists(project), $"{game} test project not configured");

            int maps = 0, compared = 0;

            foreach (var (name, bytes) in MapModels.Of(project, hgss))
            {
                var file = NsbmdFile.Read(bytes, out _);
                if (file == null) continue;

                using var stream = new MemoryStream(bytes);
                var known = NSBMDLoader.LoadNSBMD(stream);
                var model = known?.models?.FirstOrDefault();
                if (model?.Textures == null) continue;

                var theirs = model.Textures.Select(t => t.texname ?? "").ToList();
                var mine = file.Pictures.Select(p => p.Name).ToList();
                Assert.True(theirs.SequenceEqual(mine),
                    $"{name}: names read as [{string.Join(", ", mine)}] and the known reader says "
                  + $"[{string.Join(", ", theirs)}]");

                compared += mine.Count;
                maps++;
            }

            Assert.True(maps > 0, $"No {game} map was read, so this proved nothing.");
            _out.WriteLine($"{game}: {maps} maps, {compared} picture names agree.");
        }

        [SkippableFact]
        public void RepaintingChangesTheNameAndNothingElse()
        {
            Skip.If(!Directory.Exists(TestRoms.Platinum), "Platinum test project not configured");

            int done = 0;
            foreach (var (name, bytes) in MapModels.Of(TestRoms.Platinum, false).Take(30))
            {
                var file = NsbmdFile.Read(bytes, out _);
                if (file == null || file.Pictures.Count == 0) continue;

                byte[] made = file.WithNames(new Dictionary<int, string> { [0] = "repainted" }, null,
                                             out string whynot);
                Assert.True(made != null, $"{name}: {whynot}");

                Assert.Equal(bytes.Length, made.Length);
                int at = file.Pictures[0].NameAt;
                for (int i = 0; i < bytes.Length; i++)
                    if (i < at || i >= at + 16)
                        Assert.True(bytes[i] == made[i], $"{name}: byte {i} changed, and only a name should have.");

                var again = NsbmdFile.Read(made, out _);
                Assert.Equal("repainted", again.Pictures[0].Name);

                using var stream = new MemoryStream(made);
                var known = NSBMDLoader.LoadNSBMD(stream);
                Assert.Equal("repainted", known.models[0].Textures[0].texname);

                done++;
            }

            Assert.True(done > 0, "No map was repainted, so this proved nothing.");
            _out.WriteLine($"{done} maps repainted with only the name changing.");
        }

        [SkippableFact]
        public void ANameTooLongToHoldIsRefusedWithItsLength()
        {
            Skip.If(!Directory.Exists(TestRoms.Platinum), "Platinum test project not configured");

            var (_, bytes) = MapModels.Of(TestRoms.Platinum, false).First();
            var file = NsbmdFile.Read(bytes, out _);

            byte[] made = file.WithNames(
                new Dictionary<int, string> { [0] = "a_name_far_too_long_to_fit" }, null, out string whynot);

            Assert.Null(made);
            Assert.Contains("sixteen", whynot);
        }

        [SkippableFact]
        public void APictureNameSaysWhichMaterialsItPaints()
        {
            Skip.If(!Directory.Exists(TestRoms.Platinum), "Platinum test project not configured");

            int withMaterials = 0, entries = 0;
            foreach (var (_, bytes) in MapModels.Of(TestRoms.Platinum, false).Take(40))
            {
                var file = NsbmdFile.Read(bytes, out _);
                if (file == null) continue;
                foreach (var picture in file.Pictures)
                {
                    entries++;
                    if (picture.Materials.Count > 0) withMaterials++;
                }
            }

            Assert.True(entries > 0, "No picture name was read, so this proved nothing.");

            Assert.True(withMaterials > entries / 2,
                $"Only {withMaterials} of {entries} picture names say what they paint.");
            _out.WriteLine($"{withMaterials} of {entries} picture names say which materials they paint.");
        }
    }
}

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
    public class NsbmdFileTests
    {
        private readonly ITestOutputHelper _out;
        public NsbmdFileTests(ITestOutputHelper o) { _out = o; }

        [SkippableTheory]
        [InlineData("Platinum")]
        [InlineData("HeartGold")]
        public void EveryMapModelIsTakenApartAndPutBackUnchanged(string game)
        {
            string project = game == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            bool hgss = game == "HeartGold";
            Skip.If(!Directory.Exists(project), $"{game} test project not configured");

            int read = 0, refused = 0, shapes = 0;
            var whyRefused = new List<string>();

            foreach (var (name, model) in MapModels.Of(project, hgss))
            {
                var file = NsbmdFile.Read(model, out string whynot);
                if (file == null)
                {
                    refused++;
                    if (whyRefused.Count < 8) whyRefused.Add($"{name}: {whynot}");
                    continue;
                }

                read++;
                shapes += file.Shapes.Count;

                var same = new Dictionary<int, byte[]>();
                for (int i = 0; i < file.Shapes.Count; i++) same[i] = file.DisplayList(i);

                var back = file.With(same, out string why);
                Assert.True(back != null, $"{name}: {why}");
                Assert.True(model.SequenceEqual(back),
                    $"{name}: putting every shape back unchanged did not give the same {model.Length} bytes.");
            }

            _out.WriteLine($"{game}: {read} models read, {shapes} shapes, {refused} refused.");
            foreach (string s in whyRefused) _out.WriteLine("  " + s);

            Assert.True(read > 0, $"No {game} map model could be read, so this proved nothing.");
            Assert.Equal(0, refused);
        }

        [SkippableTheory]
        [InlineData("Platinum")]
        [InlineData("HeartGold")]
        public void ADrawingProgramThatGrowsMovesEverythingAfterItCorrectly(string game)
        {
            string project = game == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            bool hgss = game == "HeartGold";
            Skip.If(!Directory.Exists(project), $"{game} test project not configured");

            int checkedModels = 0;

            foreach (var (name, model) in MapModels.Of(project, hgss).Take(60))
            {
                var file = NsbmdFile.Read(model, out _);
                if (file == null || file.Shapes.Count == 0) continue;

                var grown = file.DisplayList(0).Concat(new byte[4]).ToArray();
                var made = file.With(new Dictionary<int, byte[]> { [0] = grown }, out string why);
                Assert.True(made != null, $"{name}: {why}");
                Assert.Equal(model.Length + 4, made.Length);

                var again = NsbmdFile.Read(made, out string whynot);
                Assert.True(again != null, $"{name}: grown by four bytes it no longer reads, {whynot}");
                Assert.Equal(file.Shapes.Count, again.Shapes.Count);

                Assert.True(grown.SequenceEqual(again.DisplayList(0)),
                            $"{name}: the shape that grew did not come back as it was written.");
                for (int i = 1; i < file.Shapes.Count; i++)
                    Assert.True(file.DisplayList(i).SequenceEqual(again.DisplayList(i)),
                                $"{name}: shape {i} moved and came back different.");

                using var stream = new MemoryStream(made);
                var reread = NSBMDLoader.LoadNSBMD(stream);
                Assert.NotNull(reread?.models);
                Assert.Equal(file.Shapes.Count, reread.models[0].Polygons.Count(p => p.PolyData != null));

                checkedModels++;
            }

            Assert.True(checkedModels > 0, $"No {game} map model was grown, so this proved nothing.");
            _out.WriteLine($"{game}: {checkedModels} models grown and reread.");
        }

        [SkippableFact]
        public void TheShapesAgreeWithTheReaderThatHasAlwaysReadTheseFiles()
        {
            Skip.If(!Directory.Exists(TestRoms.Platinum), "Platinum test project not configured");

            int compared = 0;
            foreach (var (name, model) in MapModels.Of(TestRoms.Platinum, false).Take(40))
            {
                var file = NsbmdFile.Read(model, out _);
                if (file == null) continue;

                using var stream = new MemoryStream(model);
                var known = NSBMDLoader.LoadNSBMD(stream);
                if (known?.models == null || known.models.Length == 0) continue;

                var withData = known.models[0].Polygons.Where(p => p.PolyData != null).ToList();
                Assert.Equal(withData.Count, file.Shapes.Count);

                for (int i = 0; i < file.Shapes.Count; i++)
                {
                    Assert.True(withData[i].PolyData.SequenceEqual(file.DisplayList(i)),
                        $"{name}: shape {i} was found at different bytes than the known reader finds.");
                    compared++;
                }
            }

            Assert.True(compared > 0, "No shape was compared, so this proved nothing.");
            _out.WriteLine($"{compared} shapes agree with the known reader.");
        }

    }
}

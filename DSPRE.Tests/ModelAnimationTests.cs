using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.Gl;
using DSPRE.ROMFiles;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>
    /// Putting a movement on a model in the Models window actually moves it.
    ///
    /// The window can only be judged by eye, and a button that says Stop proves the timer is running and
    /// nothing else. So the geometry is compared directly: build a model still, build it again on a later
    /// frame of the same movement, and the numbers have to differ. Anything that reports the parts moving
    /// while the vertices stay put would pass a screenshot and fail here.
    /// </summary>
    [Collection("rom")]
    public class ModelAnimationTests
    {
        private readonly ITestOutputHelper _out;
        public ModelAnimationTests(ITestOutputHelper o) { _out = o; }

        private const string Platinum =
            @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents";
        private const string HeartGold =
            @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";

        public static IEnumerable<object[]> Games => new[]
        {
            new object[] { "CPUE", Platinum, "Platinum" },
            new object[] { "IPKE", HeartGold, "HeartGold" },
        };

        /// <summary>How far apart two builds of the same model are, counting only real differences.</summary>
        private static int VerticesThatMoved(NsbmdRenderModel a, NsbmdRenderModel b)
        {
            if (a?.Parts == null || b?.Parts == null) return -1;
            if (a.Parts.Count != b.Parts.Count) return int.MaxValue;

            int moved = 0;
            for (int m = 0; m < a.Parts.Count; m++)
            {
                var va = a.Parts[m].Vertices;
                var vb = b.Parts[m].Vertices;
                if (va == null || vb == null || va.Length != vb.Length) return int.MaxValue;
                for (int i = 0; i < va.Length; i++)
                    if (Math.Abs(va[i] - vb[i]) > 1e-6f) moved++;
            }
            return moved;
        }

        [Theory]
        [MemberData(nameof(Games))]
        public void APlayedMovementActuallyMovesTheModel(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);
            GraphicAssets.Forget();

            var archives = ModelAssets.All.Where(a => a.AnimationArchive != null).ToList();
            Assert.NotEmpty(archives);

            // Every model against every movement is far too much, so this walks models until it has
            // enough pairs to be worth believing, and says how many it actually tried.
            int pairsTried = 0, pairsThatMoved = 0, modelsWalked = 0;
            var stillPairs = new List<string>();

            foreach (var a in archives)
            {
                int models = ModelAssets.Count(a);
                int anims = ModelAssets.AnimationCount(a);
                if (models == 0 || anims == 0) continue;
                _out.WriteLine($"{game} / {a.Title}: {models} models, {anims} movements");

                for (int m = 0; m < models && pairsTried < 60; m++)
                {
                    var nsbmd = ModelAssets.LoadModel(a, m);
                    if (nsbmd?.models == null || nsbmd.models.Length == 0) continue;
                    modelsWalked++;

                    var still = NsbmdGeometry.BuildModel(nsbmd.models[0]);
                    if (still?.Parts == null || still.Parts.Count == 0) continue;

                    for (int k = 0; k < anims && pairsTried < 60; k++)
                    {
                        var anim = ModelAssets.AnimationFor(a, m, k);
                        if (anim == null || anim.FrameCount < 2) continue;

                        // The movement has to touch a part this model actually has, or there is nothing
                        // for it to move and the pair says nothing either way.
                        if (!anim.AnimatedObjects.Any(id => id >= 0 && id < nsbmd.models[0].Objects.Count))
                            continue;

                        float scale = nsbmd.models[0].modelScale;
                        int mid = anim.FrameCount / 2;
                        var moving = NsbmdGeometry.BuildModel(nsbmd.models[0],
                            (objectId, part) => anim.MatrixFor(objectId, mid, part, scale));

                        pairsTried++;
                        int moved = VerticesThatMoved(still, moving);
                        if (moved > 0) pairsThatMoved++;
                        else stillPairs.Add($"{a.Title} model {m} with movement {k}");
                    }
                }
            }

            _out.WriteLine($"{game}: {modelsWalked} models walked, {pairsTried} model-and-movement pairs "
                         + $"tried, {pairsThatMoved} moved");
            foreach (var s in stillPairs.Take(10)) _out.WriteLine("  did not move: " + s);

            Assert.True(pairsTried >= 10,
                $"{game}: only {pairsTried} pairs could be tried, which is too few to prove anything");

            // Not every pair moves, and that is the games rather than a fault: a movement written for one
            // building can name a part another building also has and hold it still the whole way through.
            // Those are the pairs the window now names out loud instead of leaving still and unexplained.
            // What has to hold is that the mechanism works for most of them, so a still model means the
            // movement is still and not that nothing is wired up.
            Assert.True(pairsThatMoved * 2 > pairsTried,
                $"{game}: only {pairsThatMoved} of {pairsTried} pairs moved the model, so putting a "
                + "movement on a model mostly does nothing");
        }





        /// <summary>The check above with the movement taken away, to show it can fail. Two builds of the
        /// same model with no movement have to come out identical, or "it moved" means nothing.</summary>
        [Theory]
        [MemberData(nameof(Games))]
        public void TwoStillBuildsOfTheSameModelAreIdentical(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);

            var a = ModelAssets.All.First(x => x.AnimationArchive != null);
            int looked = 0;
            for (int m = 0; m < ModelAssets.Count(a) && looked < 20; m++)
            {
                var nsbmd = ModelAssets.LoadModel(a, m);
                if (nsbmd?.models == null || nsbmd.models.Length == 0) continue;
                var one = NsbmdGeometry.BuildModel(nsbmd.models[0]);
                var two = NsbmdGeometry.BuildModel(nsbmd.models[0]);
                if (one?.Parts == null || one.Parts.Count == 0) continue;
                looked++;
                Assert.Equal(0, VerticesThatMoved(one, two));
            }
            _out.WriteLine($"{game}: {looked} models built twice, all identical");
            Assert.True(looked >= 10, $"{game}: only {looked} models could be built, too few to prove anything");
        }
    }
}

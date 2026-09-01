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

namespace DSPRE.Tests
{
    /// <summary>
    /// The movement a model is offered first is the one the game actually gives it.
    ///
    /// The Models window used to offer the whole animation archive with nothing to say which entry
    /// belonged to the model on screen, so finding the right one meant trying ninety eight of them. The
    /// games carry a table saying which movements each building uses. This checks that table is being
    /// read, that what it names really is a movement, and that it really moves the model it belongs to,
    /// which is the part that separates "we read a table" from "we read the right table".
    /// </summary>
    [Collection("rom")]
    public class ModelOwnAnimationTests
    {
        private readonly ITestOutputHelper _out;
        public ModelOwnAnimationTests(ITestOutputHelper o) { _out = o; }

        private const string Platinum =
            @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents";
        private const string HeartGold = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";

        public static IEnumerable<object[]> Games => new[]
        {
            new object[] { "CPUE", Platinum, "Platinum" },
            new object[] { "IPKE", HeartGold, "HeartGold" },
        };

        private static int VerticesThatMoved(NsbmdRenderModel a, NsbmdRenderModel b)
        {
            if (a?.Parts == null || b?.Parts == null || a.Parts.Count != b.Parts.Count) return -1;
            int moved = 0;
            for (int m = 0; m < a.Parts.Count; m++)
            {
                var va = a.Parts[m].Vertices; var vb = b.Parts[m].Vertices;
                if (va == null || vb == null || va.Length != vb.Length) return -1;
                for (int i = 0; i < va.Length; i++)
                    if (Math.Abs(va[i] - vb[i]) > 1e-6f) moved++;
            }
            return moved;
        }

        [Theory]
        [MemberData(nameof(Games))]
        public void TheMovementAModelIsGivenIsTheOneTheGameGivesIt(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);
            GraphicAssets.Forget();

            var buildings = ModelAssets.All.Where(a => a.AnimationArchive != null).ToList();
            Assert.NotEmpty(buildings);

            int withOwn = 0, walked = 0, realAnimations = 0, actuallyMoved = 0, otherKinds = 0;
            var didNotMove = new List<string>();

            foreach (var a in buildings)
            {
                int models = ModelAssets.Count(a);
                for (int m = 0; m < models; m++)
                {
                    var own = ModelAssets.OwnAnimations(a, m);
                    if (own.Count == 0) continue;
                    withOwn++;
                    if (walked >= 40) continue;      // reading models is the slow part, so cap the deep check

                    var nsbmd = ModelAssets.LoadModel(a, m);
                    if (nsbmd?.models == null || nsbmd.models.Length == 0) continue;
                    var model = nsbmd.models[0];
                    var still = NsbmdGeometry.BuildModel(model);
                    if (still?.Parts == null || still.Parts.Count == 0) continue;
                    walked++;

                    foreach (int c in own)
                    {
                        // The table names every kind of animation a model uses, not only the joint ones
                        // that move its parts. A texture scroll or a colour fade is named the same way and
                        // is not something this window plays, so those are counted and passed over.
                        var anim = ModelAssets.AnimationFor(a, m, c);
                        if (anim == null) { otherKinds++; continue; }
                        realAnimations++;

                        bool moved = false;
                        for (int f = 1; f < anim.FrameCount && !moved; f++)
                        {
                            var at = NsbmdGeometry.BuildModel(model,
                                (id, part) => anim.MatrixFor(id, f, part, model.modelScale), still);
                            moved = VerticesThatMoved(still, at) > 0;
                        }
                        if (moved) actuallyMoved++;
                        else didNotMove.Add($"{a.Title} model {m} with its own movement {c}");
                    }
                }
            }

            _out.WriteLine($"{game}: {withOwn} models have a movement of their own in the game's table");
            _out.WriteLine($"  {walked} of them opened, {realAnimations} named movements read, "
                         + $"{actuallyMoved} moved the model; {otherKinds} named animations of other kinds");
            foreach (var x in didNotMove.Take(5)) _out.WriteLine("  did not move: " + x);

            Assert.True(withOwn > 20,
                $"{game}: only {withOwn} models have a movement of their own, so the table is probably not being read");
            Assert.True(realAnimations > 0, $"{game}: no named movement could be read at all");

            // A movement the game gives a model has to move it. This is the check that separates reading
            // the right table from reading any table.
            Assert.True(actuallyMoved * 4 >= realAnimations * 3,
                $"{game}: only {actuallyMoved} of {realAnimations} of the game's own movements moved their "
                + "own model, so the table being read is probably the wrong one");
        }

        /// <summary>The same measure applied to a movement picked at random shows it can fail: somebody
        /// else's movement mostly does nothing to a model, which is why naming the right one matters.</summary>
        [Fact]
        public void SomebodyElsesMovementIsMuchLessLikelyToMoveAModel()
        {
            if (!Directory.Exists(Platinum)) { _out.WriteLine("Platinum not unpacked here"); return; }
            new RomInfo("CPUE", Platinum);

            var a = ModelAssets.All.First(x => x.Title == "Buildings, outside");
            int ownTried = 0, ownMoved = 0, otherTried = 0, otherMoved = 0;

            for (int m = 0; m < ModelAssets.Count(a) && ownTried < 15; m++)
            {
                var own = ModelAssets.OwnAnimations(a, m);
                if (own.Count == 0) continue;
                var nsbmd = ModelAssets.LoadModel(a, m);
                if (nsbmd?.models == null || nsbmd.models.Length == 0) continue;
                var model = nsbmd.models[0];
                var still = NsbmdGeometry.BuildModel(model);
                if (still?.Parts == null || still.Parts.Count == 0) continue;

                bool Moves(int c)
                {
                    var anim = ModelAssets.AnimationFor(a, m, c);
                    if (anim == null) return false;
                    for (int f = 1; f < anim.FrameCount; f++)
                    {
                        var at = NsbmdGeometry.BuildModel(model,
                            (id, part) => anim.MatrixFor(id, f, part, model.modelScale), still);
                        if (VerticesThatMoved(still, at) > 0) return true;
                    }
                    return false;
                }

                foreach (int c in own) { ownTried++; if (Moves(c)) ownMoved++; }

                int count = ModelAssets.AnimationCount(a);
                for (int c = 0, taken = 0; c < count && taken < own.Count; c++)
                {
                    if (own.Contains(c)) continue;
                    if (ModelAssets.AnimationFor(a, m, c) == null) continue;
                    taken++; otherTried++;
                    if (Moves(c)) otherMoved++;
                }
            }

            _out.WriteLine($"its own movements: {ownMoved} of {ownTried} moved the model");
            _out.WriteLine($"somebody else's:   {otherMoved} of {otherTried} moved the model");
            Assert.True(ownTried > 5 && otherTried > 5, "too few tried to compare the two");
            Assert.True(ownMoved * otherTried > otherMoved * ownTried,
                "a model's own movement is no more likely to move it than a random one, so naming the "
                + "right one is not buying anything");
        }
    }
}

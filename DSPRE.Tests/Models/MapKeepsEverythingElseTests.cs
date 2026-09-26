using DSPRE.Models;
using DSPRE.ROMFiles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests.Models
{
    [Collection("rom")]
    public class MapKeepsEverythingElseTests
    {
        private readonly ITestOutputHelper _out;
        public MapKeepsEverythingElseTests(ITestOutputHelper o) { _out = o; }

        [SkippableTheory]
        [InlineData("Platinum", "CPUE")]
        [InlineData("HeartGold", "IPKE")]
        public void ChangingAMapsShapeLeavesEverythingElseItCarriesAlone(string game, string code)
        {
            string project = game == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            Skip.If(!Directory.Exists(project), $"{game} test project not configured");

            new RomInfo(code, project);
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.maps });

            int done = 0;

            foreach (int id in new[] { 0, 3, 11, 40, 90 })
            {
                MapFile map;
                try { map = new MapFile(id, RomInfo.gameFamily, discardMoveperms: false, showMessages: false); }
                catch { continue; }
                if (map.mapModelData == null) continue;

                byte[] wasCollisions = map.CollisionsToByteArray();
                byte[] wasBuildings = map.BuildingsToByteArray();
                byte[] wasTerrain = (byte[])map.bdhc.Clone();
                byte[] wasSounds = (byte[])map.bgs.Clone();

                var mesh = MapMesh.Read(map.mapModelData, out string whynot);
                Assert.True(mesh != null, $"map {id}: {whynot}");
                if (mesh.Vertices.Count < 4) continue;

                var v = mesh.Vertices[mesh.Vertices.Count / 2];
                mesh.Move(mesh.Vertices.Count / 2, v.X, v.Y + 0.25f, v.Z);

                byte[] shape = mesh.Save(out string why);
                Assert.True(shape != null, $"map {id}: {why}");

                map.LoadMapModel(shape, showMessages: false);
                map.mapModelData = shape;

                Assert.True(wasCollisions.SequenceEqual(map.CollisionsToByteArray()),
                            $"map {id}: where you may walk changed when only the shape was edited.");
                Assert.True(wasBuildings.SequenceEqual(map.BuildingsToByteArray()),
                            $"map {id}: the buildings changed when only the shape was edited.");
                Assert.True(wasTerrain.SequenceEqual(map.bdhc),
                            $"map {id}: the ground walked on changed although it was not asked for.");
                Assert.True(wasSounds.SequenceEqual(map.bgs),
                            $"map {id}: the sounds underfoot changed when only the shape was edited.");

                byte[] whole = map.ToByteArray();
                Assert.True(whole.Length > shape.Length, $"map {id}: the file came out smaller than its shape.");

                done++;
            }

            Assert.True(done > 0, $"No {game} map was edited, so this proved nothing.");
            _out.WriteLine($"{game}: {done} maps kept their permissions, buildings, terrain and sounds.");
        }

        [SkippableFact]
        public void AskingForTheTerrainChangesTheTerrainAndStillNothingElse()
        {
            Skip.If(!Directory.Exists(TestRoms.Platinum), "Platinum test project not configured");

            new RomInfo("CPUE", TestRoms.Platinum);
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.maps });

            var map = new MapFile(0, RomInfo.gameFamily, discardMoveperms: false, showMessages: false);
            byte[] wasCollisions = map.CollisionsToByteArray();
            byte[] wasBuildings = map.BuildingsToByteArray();
            byte[] wasTerrain = (byte[])map.bdhc.Clone();

            var mesh = MapMesh.Read(map.mapModelData, out _);
            byte[] made = BdhcBuild.From(BdhcBuild.PiecesOf(mesh, mesh.ModelScale), out string whynot);
            Assert.True(made != null, whynot);

            map.ImportTerrain(made);

            Assert.False(wasTerrain.SequenceEqual(map.bdhc), "The terrain was asked for and did not change.");
            Assert.True(wasCollisions.SequenceEqual(map.CollisionsToByteArray()),
                        "Where you may walk changed when only the terrain was asked for.");
            Assert.True(wasBuildings.SequenceEqual(map.BuildingsToByteArray()),
                        "The buildings changed when only the terrain was asked for.");

            _out.WriteLine($"terrain {wasTerrain.Length} bytes became {map.bdhc.Length}, "
                         + "permissions and buildings untouched.");
        }
    }
}

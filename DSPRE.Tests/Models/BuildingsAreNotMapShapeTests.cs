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
    public class BuildingsAreNotMapShapeTests
    {
        private readonly ITestOutputHelper _out;
        public BuildingsAreNotMapShapeTests(ITestOutputHelper o) { _out = o; }

        [SkippableTheory]
        [InlineData("Platinum", "CPUE")]
        [InlineData("HeartGold", "IPKE")]
        public void NoBuildingEverBecomesATileOrIsDisturbedByPainting(string game, string code)
        {
            string project = game == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            Skip.If(!Directory.Exists(project), $"{game} test project not configured");

            new RomInfo(code, project);
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.Maps });

            int withBuildings = 0;
            long buildings = 0;

            foreach (int id in new[] { 0, 3, 11, 40, 90, 150 })
            {
                MapFile map;
                try { map = new MapFile(id, RomInfo.GameFamily, discardMoveperms: false, showMessages: false); }
                catch { continue; }
                if (map.MapModelData == null || map.Buildings == null || map.Buildings.Count == 0) continue;

                byte[] wasBuildings = map.BuildingsToByteArray();
                int wasCount = map.Buildings.Count;

                MapMesh mesh = MapMesh.Read(map.MapModelData, out string whynot);
                Assert.True(mesh != null, $"map {id}: {whynot}");

                Func<int, string> pictureOf = m =>
                {
                    int named = mesh.PictureFor(m);
                    return named >= 0 ? mesh.NameOfPicture(named) : $"material{m}";
                };

                MapTileset set = MapTileset.FromMap(mesh, pictureOf, $"map{id}");
                TileGrid grid = TileGrid.Of(mesh, set, pictureOf, out _);

                byte[] built = TileBake.ToModel(TileBake.Of(grid, set), out string why, mesh.ModelScale);
                Assert.True(built != null, $"map {id}: {why}");

                map.LoadMapModel(built, showMessages: false);
                map.MapModelData = built;

                Assert.Equal(wasCount, map.Buildings.Count);
                Assert.True(wasBuildings.SequenceEqual(map.BuildingsToByteArray()),
                    $"map {id}: the buildings changed when only the shape was rebuilt from tiles.");

                MapMesh after = MapMesh.Read(map.MapModelData, out _);
                Assert.NotNull(after);

                withBuildings++;
                buildings += wasCount;
            }

            Assert.True(withBuildings > 0, $"No {game} map with buildings was tried, so this proved nothing.");
            _out.WriteLine($"{game}: {withBuildings} maps carrying {buildings} buildings, "
                         + "rebuilt from their own tiles with every building untouched.");
        }

        [SkippableFact]
        public void TheGroundWalkedOnIsBuiltFromTheMapAndNotFromWhatStandsOnIt()
        {
            Skip.If(!Directory.Exists(TestRoms.Platinum), "Platinum test project not configured");

            new RomInfo("CPUE", TestRoms.Platinum);
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.Maps });

            MapFile map = new MapFile(0, RomInfo.GameFamily, discardMoveperms: false, showMessages: false);
            Skip.If(map.Buildings == null || map.Buildings.Count == 0, "That map carries no buildings.");

            MapMesh mesh = MapMesh.Read(map.MapModelData, out _);
            List<BdhcBuild.Piece> pieces = BdhcBuild.PiecesOf(mesh, mesh.ModelScale);
            Assert.NotEmpty(pieces);

            _out.WriteLine($"{mesh.Faces.Count} faces of the map's own shape became {pieces.Count} "
                         + $"pieces of ground, with {map.Buildings.Count} buildings taking no part.");

            Assert.True(pieces.Count <= mesh.Faces.Count);
        }
    }
}

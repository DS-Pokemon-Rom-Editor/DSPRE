using DSPRE.Avalonia.Gl;
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
    public class CellToSceneTests
    {
        private readonly ITestOutputHelper _out;
        public CellToSceneTests(ITestOutputHelper o) { _out = o; }

        [SkippableFact]
        public void EveryCornerOfTheMeshLandsWhereTheRendererDrewIt()
        {
            Skip.If(!Directory.Exists(TestRoms.Platinum), "Platinum test project not configured");
            new RomInfo("CPUE", TestRoms.Platinum);
            DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> {
                RomInfo.DirNames.maps, RomInfo.DirNames.mapTextures,
                RomInfo.DirNames.exteriorBuildingModels, RomInfo.DirNames.areaData });

            int checkedMaps = 0;
            long matched = 0, missed = 0;

            foreach (int id in new[] { 0, 5, 20, 60, 120 })
            {
                MapFile map;
                try { map = new MapFile(id, RomInfo.gameFamily, discardMoveperms: false, showMessages: false); }
                catch { continue; }
                if (map.mapModelData == null || map.mapModel == null) continue;

                var scene = MatrixSceneBuilder.BuildFromPlaced(RomInfo.gameFamily,
                    new[] { (0, 0, map, (byte)0, 0f, 0f, 0f) },
                    NsbmdGeometry.MatrixStitchMode.Grid);
                if (scene == null || scene.Parts.Count == 0) continue;

                var mesh = MapMesh.Read(map.mapModelData, out string whynot);
                Assert.True(mesh != null, $"map {id}: {whynot}");

                var drawn = new HashSet<(int x, int y, int z)>();
                foreach (var part in scene.Parts)
                    for (int i = 0; i + 7 < part.Vertices.Length; i += 8)
                        drawn.Add(Near(part.Vertices[i], part.Vertices[i + 1], part.Vertices[i + 2]));

                float[] toScene = scene.CellToScene(0, 0, map.mapModel.models[0].modelScale);

                int here = 0, lost = 0;
                foreach (var v in mesh.Vertices)
                {
                    var (x, y, z) = MeshPick.Place(toScene, v.X, v.Y, v.Z);
                    if (drawn.Contains(Near(x, y, z))) here++; else lost++;
                }

                matched += here; missed += lost;
                checkedMaps++;
                _out.WriteLine($"map {id}: {here} of {here + lost} corners land on a drawn one.");

                Assert.True(lost == 0,
                    $"map {id}: {lost} corners of the map's own model are not where the renderer drew any.");
            }

            Assert.True(checkedMaps > 0, "No map was built into a scene, so this proved nothing.");
            _out.WriteLine($"{checkedMaps} maps, {matched} corners matched, {missed} missed.");
        }

        private static (int x, int y, int z) Near(float x, float y, float z)
            => ((int)MathF.Round(x * 8192f), (int)MathF.Round(y * 8192f), (int)MathF.Round(z * 8192f));
    }
}

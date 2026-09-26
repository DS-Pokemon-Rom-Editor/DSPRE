using DSPRE.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests.Models
{
    public class PdstsFacesTheRightWayTests
    {
        private readonly ITestOutputHelper _out;
        public PdstsFacesTheRightWayTests(ITestOutputHelper o) { _out = o; }

        private static string Shape(IList<(float x, float y, float z)> corners, IEnumerable<int[]> faces)
        {
            float lx = corners.Min(c => c.x), ly = corners.Min(c => c.y), lz = corners.Min(c => c.z);
            (int, int, int) At(int i) => ((int)MathF.Round((corners[i].x - lx) * 64f),
                                          (int)MathF.Round((corners[i].y - ly) * 64f),
                                          (int)MathF.Round((corners[i].z - lz) * 64f));

            var described = new List<string>();
            foreach (int[] face in faces)
            {
                if (face.Length < 3) continue;

                float nx = 0, ny = 0, nz = 0;
                for (int i = 0; i < face.Length; i++)
                {
                    var a = corners[face[i]];
                    var b = corners[face[(i + 1) % face.Length]];
                    nx += (a.y - b.y) * (a.z + b.z);
                    ny += (a.z - b.z) * (a.x + b.x);
                    nz += (a.x - b.x) * (a.y + b.y);
                }
                float len = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
                if (len < 1e-9f) continue;

                string where = string.Join(",", face.Select(At).OrderBy(c => c.Item1)
                                                    .ThenBy(c => c.Item2).ThenBy(c => c.Item3));
                described.Add($"{where}>{MathF.Round(nx / len * 4f)},{MathF.Round(ny / len * 4f)},"
                            + $"{MathF.Round(nz / len * 4f)}");
            }
            described.Sort(StringComparer.Ordinal);
            return string.Join("|", described);
        }

        private static string Shape(MapTileset.Tile tile, bool mirrored)
        {
            var corners = tile.Corners.Select(c => (c.X, c.Y, mirrored ? -c.Z : c.Z)).ToList();
            return Shape(corners, tile.Faces.Select(f => f.Corners));
        }

        [SkippableFact]
        public void TheHeartGoldSetMatchesHeartGoldsOwnTilesTheWayItIsTurned()
        {
            Skip.If(!Directory.Exists(TestRoms.HeartGold), "HeartGold test project not configured");

            string root = Path.Combine(Path.GetTempPath(), "claude");
            var sets = Directory.Exists(root)
                ? Directory.EnumerateFiles(root, "*.pdsts", SearchOption.AllDirectories)
                           .Where(p => p.IndexOf("Heart_Gold", StringComparison.OrdinalIgnoreCase) >= 0)
                           .ToList()
                : new List<string>();
            Skip.If(sets.Count == 0, "No HeartGold Map Studio tileset is available.");

            var real = new HashSet<string>();
            foreach (var (_, bytes) in MapModels.Of(TestRoms.HeartGold, true).Take(120))
            {
                var mesh = MapMesh.Read(bytes, out _);
                if (mesh == null) continue;
                foreach (var tile in MapTileset.FromMap(mesh, _ => "", "real").Tiles)
                    if (tile.Corners.Count >= 3)
                        real.Add(Shape(tile, mirrored: false));
            }
            Assert.True(real.Count > 0, "No HeartGold map gave up a single shape.");

            int asMade = 0, mirrored = 0, lopsided = 0;
            foreach (string path in sets)
            {
                var set = PdstsFile.Read(path, out _);
                if (set == null) continue;

                foreach (var tile in set.Tiles)
                {
                    string turned = Shape(tile, mirrored: false);
                    string other = Shape(tile, mirrored: true);

                    if (turned == other) continue;
                    lopsided++;

                    if (real.Contains(turned)) asMade++;
                    if (real.Contains(other)) mirrored++;
                }
            }

            _out.WriteLine($"{real.Count} shapes in HeartGold's maps, {lopsided} lopsided tiles in its "
                         + $"Map Studio set: {asMade} match as turned, {mirrored} match mirrored.");

            Assert.True(asMade + mirrored > 0,
                "No lopsided tile matched HeartGold's own either way, so this cannot tell.");
            Assert.True(asMade > mirrored * 3,
                $"{asMade} tiles match as turned and {mirrored} match mirrored, so the turn is the wrong way.");
        }
    }
}

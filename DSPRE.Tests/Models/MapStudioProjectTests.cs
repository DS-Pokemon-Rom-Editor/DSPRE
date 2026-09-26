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
    public class MapStudioProjectTests
    {
        private readonly ITestOutputHelper _out;
        public MapStudioProjectTests(ITestOutputHelper o) { _out = o; }

        private static List<string> Projects()
        {
            string root = Environment.GetEnvironmentVariable("DSPRE_TEST_PDSMS");
            return string.IsNullOrEmpty(root) || !Directory.Exists(root)
                ? new List<string>()
                : Directory.EnumerateFiles(root, "*.pdsmap", SearchOption.AllDirectories).ToList();
        }

        [SkippableFact]
        public void EveryMapOfAProjectIsBuiltWithNothingLeftOut()
        {
            var projects = Projects();
            Skip.If(projects.Count == 0, "DSPRE_TEST_PDSMS points at no Map Studio projects.");

            int maps = 0;
            foreach (string path in projects)
            {
                var project = PdsmapFile.Read(path, out string whynot);
                Assert.True(project != null, $"{Path.GetFileName(path)}: {whynot}");
                var set = PdstsFile.Read(project.TilesetPath, out whynot);
                Assert.True(set != null, $"{Path.GetFileName(project.TilesetPath)}: {whynot}");
                TileCollisions.ReadMeta(project.TilesetPath, set);
                set.TurnPlacesIntoDots(_ => (64, 64));

                string folder = Path.GetDirectoryName(path), stem = Path.GetFileNameWithoutExtension(path);
                foreach (var map in project.Maps)
                {
                    var grid = PdsmapFile.ToGrid(map, set, out int dropped);
                    Assert.True(dropped == 0, $"{stem} map {map.X},{map.Y}: {dropped} tiles could not go down.");

                    if (grid.Painted > 0)
                    {
                        byte[] model = TileBake.ToModel(TileBake.Of(grid, set), out whynot);
                        Assert.True(model != null, $"{stem} map {map.X},{map.Y}: {whynot}");
                        Assert.NotNull(MapMesh.Read(model, out _));
                    }

                    string saved = Path.Combine(folder, $"{stem}_{map.X:D2}_{map.Y:D2}");
                    if (File.Exists(saved + ".per")) Assert.Equal(2048, new FileInfo(saved + ".per").Length);
                    if (File.Exists(saved + ".bld")) Assert.Equal(0, new FileInfo(saved + ".bld").Length % 48);
                    if (File.Exists(saved + ".bdhc")) Assert.True(BdhcFile.TryParse(File.ReadAllBytes(saved + ".bdhc"), out _),
                        $"{stem} map {map.X},{map.Y}: its terrain cannot be read.");
                    maps++;
                }

                foreach (var area in project.Maps.GroupBy(m => m.Area))
                {
                    var tiles = new HashSet<int>();
                    foreach (var m in area) foreach (var q in PdsmapFile.ToGrid(m, set, out _).Placed()) tiles.Add(q.square.Tile);
                    int pictures = tiles.SelectMany(t => set.Tiles[t].Pictures).Distinct().Count();
                    Assert.True(pictures <= 255, $"{stem} area {area.Key} uses {pictures} pictures.");
                }
            }
            _out.WriteLine($"{projects.Count} projects, {maps} maps read and built with nothing left out.");
        }
    }
}

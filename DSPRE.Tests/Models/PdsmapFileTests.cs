using DSPRE.Models;
using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace DSPRE.Tests.Models
{
    public class PdsmapFileTests
    {
        private static string Layer(Func<int, int, int> at)
        {
            var sb = new StringBuilder();
            for (int row = 0; row < 32; row++)
            {
                for (int col = 0; col < 32; col++) sb.Append(at(col, row)).Append(' ');
                sb.Append('\n');
            }
            return sb.ToString();
        }

        private static string Project(Func<int, int, int, int> tile, Func<int, int, int, int> height)
        {
            var sb = new StringBuilder();
            sb.Append("gameindex\n2\ntileset\nmine.pdsts\n");
            sb.Append("mapstart\n0 0\nareaindex\n3\nexportgroup\n0\n");
            for (int l = 0; l < 9; l++) sb.Append("tilegrid\n").Append(Layer((c, r) => tile(l, c, r)));
            for (int l = 0; l < 9; l++) sb.Append("heightgrid\n").Append(Layer((c, r) => height(l, c, r)));
            sb.Append("mapend\n");
            return sb.ToString();
        }

        private static MapTileset Set()
        {
            var set = new MapTileset();
            foreach (var (w, d) in new[] { (1, 1), (1, 2) })
            {
                var tile = new MapTileset.Tile { Name = $"{w}x{d}", Wide = w, Deep = d };
                foreach (var (x, z) in new[] { (0f, 0f), (w * 0.25f, 0f), (w * 0.25f, d * 0.25f), (0f, d * 0.25f) })
                    tile.Corners.Add(new MapTileset.Corner { X = x, Z = z });
                tile.Faces.Add(new MapTileset.Face { Corners = new[] { 0, 1, 2, 3 }, Picture = "p" });
                set.Tiles.Add(tile);
            }
            return set;
        }

        [Fact]
        public void AProjectMapLandsOnTheSquaresMapStudioShowsItOn()
        {
            string path = Path.Combine(Path.GetTempPath(), "dspre-" + Guid.NewGuid().ToString("N") + ".pdsmap");
            try
            {
                File.WriteAllText(path, Project(
                    (l, c, r) => l == 0 && c == 0 && r == 0 ? 0 : l == 1 && c == 5 && r == 1 ? 1 : -1,
                    (l, c, r) => l == 0 && c == 0 && r == 0 ? 3 : 0));

                var project = PdsmapFile.Read(path, out string whynot);
                Assert.True(project != null, whynot);
                Assert.Equal(2, project.Game);
                Assert.EndsWith("mine.pdsts", project.TilesetPath);
                var map = Assert.Single(project.Maps);
                Assert.Equal(3, map.Area);

                var grid = PdsmapFile.ToGrid(map, Set(), out int dropped);
                Assert.Equal(0, dropped);

                Assert.Equal(0, grid.PutHere(0, 31, 0));
                Assert.Equal(3 * TileGrid.Step, grid.At(0, 31, 0).Lift, 5);

                Assert.Equal(1, grid.PutHere(5, 29, 1));
                Assert.Equal(1, grid.At(5, 30, 1).Tile);
                Assert.Equal(-1, grid.At(5, 31, 1).Tile);
                Assert.Equal(-1, grid.At(5, 28, 1).Tile);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ATileReachingPastTheNorthEdgeIsKeptHangingOverIt()
        {
            string path = Path.Combine(Path.GetTempPath(), "dspre-" + Guid.NewGuid().ToString("N") + ".pdsmap");
            try
            {
                File.WriteAllText(path, Project((l, c, r) => l == 0 && r == 31 && c == 0 ? 1 : -1, (l, c, r) => 0));
                var project = PdsmapFile.Read(path, out string whynot);
                Assert.True(project != null, whynot);

                var grid = PdsmapFile.ToGrid(project.Maps[0], Set(), out int dropped);
                Assert.Equal(0, dropped);
                var sq = grid.At(0, 0);
                Assert.Equal(1, sq.Tile);
                Assert.Equal(1, sq.PastNorth);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void TileNumbersFollowTheFileWhenAPieceMadeNoTile()
        {
            var set = Set();
            set.TileOfListed = new[] { 0, -1, 1 };
            var map = new PdsmapFile.Map();
            map.Tiles[0] = new int[32, 32];
            for (int c = 0; c < 32; c++) for (int r = 0; r < 32; r++) map.Tiles[0][c, r] = -1;
            map.Tiles[0][2, 10] = 2;
            map.Tiles[0][3, 10] = 1;

            var grid = PdsmapFile.ToGrid(map, set, out int dropped);
            Assert.Equal(1, grid.PutHere(2, 31 - 10 - 1));
            Assert.Equal(1, dropped);
        }

        [Fact]
        public void ABrokenRowIsRefusedWithWhy()
        {
            string path = Path.Combine(Path.GetTempPath(), "dspre-" + Guid.NewGuid().ToString("N") + ".pdsmap");
            try
            {
                File.WriteAllText(path, "gameindex\n0\nmapstart\n0 0\ntilegrid\n1 2 3\n");
                Assert.Null(PdsmapFile.Read(path, out string whynot));
                Assert.Contains("row 0", whynot);
            }
            finally { File.Delete(path); }
        }
    }
}

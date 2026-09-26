using DSPRE.Models;
using System;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests.Models
{
    public class TileMergeTests
    {
        private readonly ITestOutputHelper _out;
        public TileMergeTests(ITestOutputHelper o) { _out = o; }

        private static MapTileset Flat(bool across, bool down, bool repeatU = true, bool repeatV = true)
        {
            var set = new MapTileset();
            var tile = new MapTileset.Tile
            {
                Name = "grass", AcrossTileable = across, DownTileable = down,
                PictureRepeatsAcross = repeatU, PictureRepeatsDown = repeatV,
            };
            foreach (var (x, z, s, t) in new[] { (0f, 0f, 0f, 0f), (0.25f, 0f, 16f, 0f), (0.25f, 0.25f, 16f, 16f), (0f, 0.25f, 0f, 16f) })
                tile.Corners.Add(new MapTileset.Corner { X = x, Z = z, S = s, T = t });
            tile.Faces.Add(new MapTileset.Face { Corners = new[] { 0, 1, 2, 3 }, Picture = "p" });
            set.Tiles.Add(tile);
            return set;
        }

        [Fact]
        public void ARunOfTileableTilesIsBuiltAsOneStretchedTile()
        {
            var set = Flat(true, true);
            var grid = new TileGrid();
            for (int x = 0; x < 3; x++) for (int z = 0; z < 2; z++) grid.PutTile(x, z, 0, 0);

            var baked = TileBake.Of(grid, set);

            Assert.Single(baked.Faces);
            float lowX = baked.Corners.Min(c => c.x), highX = baked.Corners.Max(c => c.x);
            float lowZ = baked.Corners.Min(c => c.z), highZ = baked.Corners.Max(c => c.z);
            Assert.Equal(-4f, lowX, 4); Assert.Equal(-4f + 0.75f, highX, 4);
            Assert.Equal(-4f, lowZ, 4); Assert.Equal(-4f + 0.5f, highZ, 4);

            Assert.Equal(48f, baked.OnPicture[0].Max(p => p.s), 3);
        }

        [Fact]
        public void TilesAtDifferentHeightsOrOfAnotherKindAreNotJoined()
        {
            var set = Flat(true, false);
            var grid = new TileGrid();
            for (int x = 0; x < 4; x++) grid.PutTile(x, 5, 0, 0);
            grid.SetHeight(2, 5, TileGrid.Step);

            var baked = TileBake.Of(grid, set);
            Assert.Equal(3, baked.Faces.Count);
        }

        [Fact]
        public void NoMoreThanEightAreJoined()
        {
            var set = Flat(true, false);
            var grid = new TileGrid();
            for (int x = 0; x < 20; x++) grid.PutTile(x, 0, 0, 0);
            Assert.Equal(3, TileBake.Of(grid, set).Faces.Count);
        }

        [Fact]
        public void APictureThatDoesNotRepeatIsPulledOverTheStretch()
        {
            var set = Flat(true, false, repeatU: false, repeatV: false);
            var grid = new TileGrid();
            for (int x = 0; x < 4; x++) grid.PutTile(x, 0, 0, 0);
            var baked = TileBake.Of(grid, set);
            Assert.Single(baked.Faces);
            Assert.Equal(16f, baked.OnPicture[0].Max(p => p.s), 3);
        }

        [Fact]
        public void ATileInsideALargerOneIsHiddenOnlyWhenTheLargerOneJoins()
        {
            foreach (bool joins in new[] { false, true })
            {
                var set = Flat(joins, false);
                var big = new MapTileset.Tile { Name = "big", Wide = 2, Deep = 2, AcrossTileable = joins };
                big.Corners.AddRange(set.Tiles[0].Corners.Select(c => c.Copy()));
                big.Faces.Add(new MapTileset.Face { Corners = new[] { 0, 1, 2, 3 }, Picture = "q" });
                set.Tiles.Add(big);

                var grid = new TileGrid();
                grid.PutTile(4, 4, 1, 0, 2, 2, layer: 0);
                grid.PutTile(5, 4, 0, 0, layer: 1);
                grid.PutTile(4, 4, 1, 0, 2, 2, layer: 1);
                grid.PutTile(5, 4, 0, 0, layer: 1);

                var hidden = new System.Collections.Generic.List<(int layer, int x, int z)>();
                var laid = TileBake.Laid(grid, set, hidden);
                if (joins) Assert.Equal((1, 5, 4), Assert.Single(hidden));
                else { Assert.Empty(hidden); Assert.Contains(laid, l => l.Square.Tile == 0); }
            }

            var runs = new TileGrid();
            var joining = Flat(true, false);
            for (int x = 0; x < 3; x++) runs.PutTile(x, 0, 0, 0);
            var none = new System.Collections.Generic.List<(int layer, int x, int z)>();
            TileBake.Laid(runs, joining, none);
            Assert.Empty(none);
        }

        [Fact]
        public void WallsFillTheStepBetweenRaisedAndLowSquaresOnlyWhereAsked()
        {
            var set = Flat(false, false);
            var grid = new TileGrid();
            for (int z = 0; z < 3; z++)
                for (int x = 0; x < 3; x++) grid.Put(x, z, 0, x == 1 && z == 1 ? 0.5f : 0f, 0);

            var baked = TileBake.Of(grid, set);
            int before = baked.Faces.Count;
            Assert.Equal(4, TileBake.AddWalls(baked, grid, set));
            Assert.Equal(before + 4, baked.Faces.Count);
            var wall = baked.Faces[^1].Select(i => baked.Corners[i]).ToList();
            Assert.Equal(0f, wall.Min(c => c.y), 4);
            Assert.Equal(0.5f, wall.Max(c => c.y), 4);

            var none = TileBake.Of(grid, set);
            Assert.Equal(0, TileBake.AddWalls(none, grid, set, (x, z) => x == 2 && z == 2));
        }

        [Fact]
        public void AWallWearsTheGroundsPictureNotAShadowListedFirst()
        {
            var set = Flat(false, false);
            var tile = set.Tiles[0];
            int at = tile.Corners.Count;
            foreach (var (x, z) in new[] { (0.05f, 0.05f), (0.1f, 0.05f), (0.1f, 0.1f), (0.05f, 0.1f) })
                tile.Corners.Add(new MapTileset.Corner { X = x, Y = 0.001f, Z = z });
            tile.Faces.Insert(0, new MapTileset.Face { Corners = new[] { at, at + 1, at + 2, at + 3 }, Picture = "h_kage" });

            var grid = new TileGrid();
            for (int z = 0; z < 3; z++)
                for (int x = 0; x < 3; x++) grid.Put(x, z, 0, x == 1 && z == 1 ? 0.5f : 0f, 0);
            var baked = TileBake.Of(grid, set);
            int before = baked.Faces.Count;
            Assert.Equal(4, TileBake.AddWalls(baked, grid, set));
            Assert.All(Enumerable.Range(before, 4), i => Assert.Equal("p", baked.Picture[i]));
        }

        [Fact]
        public void AWallWearsTheGroundNotASnowPatchLyingOverIt()
        {
            var set = Flat(false, false);
            var tile = set.Tiles[0];
            int at = tile.Corners.Count;
            // A patch as big as the square, a sliver above the ground, with more of the surface than the ground shows.
            foreach (var (x, z) in new[] { (0f, 0f), (0.25f, 0f), (0.25f, 0.25f), (0f, 0.25f) })
                tile.Corners.Add(new MapTileset.Corner { X = x, Y = 0.002f, Z = z });
            tile.Faces.Add(new MapTileset.Face { Corners = new[] { at, at + 1, at + 2, at + 3 }, Picture = "s_snowp" });
            tile.Faces.Add(new MapTileset.Face { Corners = new[] { at, at + 1, at + 2, at + 3 }, Picture = "s_snowp" });

            var grid = new TileGrid();
            for (int z = 0; z < 3; z++)
                for (int x = 0; x < 3; x++) grid.Put(x, z, 0, x == 1 && z == 1 ? 0.5f : 0f, 0);
            var baked = TileBake.Of(grid, set);
            int before = baked.Faces.Count;
            Assert.Equal(4, TileBake.AddWalls(baked, grid, set));
            Assert.All(Enumerable.Range(before, 4), i => Assert.Equal("p", baked.Picture[i]));
        }

        [Fact]
        public void AModelAsksForALongPaletteNameTheWayItsPackStoresIt()
        {
            const string picture = "sand_and_grass", palette = "sand_and_grass_pl";
            var set = Flat(false, false);
            set.Tiles[0].Faces[0].Picture = picture;
            set.Tiles[0].Faces[0].Palette = palette;
            set.Tiles[0].Faces[0].Look = MaterialLook.Plain;
            var grid = new TileGrid();
            grid.PutTile(3, 3, 0, 0);
            var model = MapMesh.Read(TileBake.ToModel(TileBake.Of(grid, set), out string whynot), out _);
            Assert.True(model != null, whynot);
            int material = model.Faces[0].Material;
            string asked = model.NameOfColours(model.ColoursFor(material));

            var rgba = new byte[8 * 8 * 4];
            for (int i = 3; i < rgba.Length; i += 4) rgba[i] = 255;
            var texture = DsTexture.From(rgba, 8, 8, picture);
            texture.PaletteNames = new System.Collections.Generic.List<string> { palette };
            var pack = NsbtxWriter.Read(NsbtxWriter.Build(new[] { texture }).Bytes);

            Assert.Contains(asked, pack.Palettes.Keys);
            Assert.Equal("sand_and_grass_p", asked);
        }

        [SkippableFact]
        public void RealTilesetsSayWhichOfTheirTilesJoin()
        {
            string root = Path.Combine(Path.GetTempPath(), "claude");
            var files = Directory.Exists(root) ? Directory.EnumerateFiles(root, "*.pdsts", SearchOption.AllDirectories).ToList() : new();
            Skip.If(files.Count == 0, "No .pdsts tilesets are available to read.");

            int joining = 0, across = 0, tiles = 0;
            foreach (string path in files)
            {
                var set = PdstsFile.Read(path, out _);
                if (set == null) continue;
                tiles += set.Tiles.Count;
                joining += set.Tiles.Count(t => t.Merges);
                across += set.Tiles.Count(t => t.PictureAcrossTheMap);
            }
            _out.WriteLine($"{tiles} tiles, {joining} join their neighbours, {across} lay their picture across the map.");
            Assert.True(joining > 0, "Not one real tile joins its neighbours, so the flags were not read.");
        }
    }
}

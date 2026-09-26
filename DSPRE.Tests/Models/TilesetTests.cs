using DSPRE.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests.Models
{
    public class TilesetTests
    {
        private readonly ITestOutputHelper _out;
        public TilesetTests(ITestOutputHelper o) { _out = o; }

        private static Func<int, string> PictureOf(MapMesh mesh) => material =>
        {
            int named = mesh.PictureFor(material);
            return named >= 0 ? mesh.NameOfPicture(named) : $"material{material}";
        };

        private static Func<int, string> PaletteOf(MapMesh mesh) => material =>
        {
            int named = mesh.ColoursFor(material);
            return named >= 0 ? mesh.NameOfColours(named) : "";
        };

        // Baked the way Apply bakes: ground From map cut into squares goes back whole where nothing changed.
        private static TileBake.Result BakeLikeApply(MapMesh mesh, MapTileset set, TileGrid grid, List<MapMesh.Face> left = null)
        {
            var baked = TileBake.Of(grid, set);
            if (baked.Whynot != null) return baked;
            var cut = new List<MapMesh.Face>();
            var split = new Dictionary<MapMesh.Face, List<MapMesh.Face>>();
            MapTileset.PiecesOf(mesh, cut, split);
            TileBake.PutBackWhole(baked, TileBake.WholeFaces(mesh, cut, PictureOf(mesh), PaletteOf(mesh), split));
            if (left != null) TileBake.PutBackAsTheyWere(baked, TileBake.WholeFaces(mesh, left.Where(mesh.Faces.Contains), PictureOf(mesh), PaletteOf(mesh)));
            return baked;
        }

        [SkippableTheory]
        [InlineData("Platinum")]
        [InlineData("HeartGold")]
        public void AMapTakenApartIntoTilesIsMadeOfFewerTilesThanItHasSquares(string game)
        {
            string project = game == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            bool hgss = game == "HeartGold";
            Skip.If(!Directory.Exists(project), $"{game} test project not configured");

            int maps = 0;
            long tiles = 0, faces = 0;

            foreach (var (name, bytes) in MapModels.Of(project, hgss).Take(40))
            {
                var mesh = MapMesh.Read(bytes, out _);
                if (mesh == null || mesh.Faces.Count == 0) continue;

                var set = MapTileset.FromMap(mesh, PictureOf(mesh), name, PaletteOf(mesh));
                Assert.True(set.Tiles.Count > 0, $"{name}: nothing came out of it as a tile.");

                Assert.True(set.Tiles.Count < TileGrid.Across * TileGrid.Across,
                    $"{name}: {set.Tiles.Count} tiles for {TileGrid.Across * TileGrid.Across} squares.");

                tiles += set.Tiles.Count;
                faces += mesh.Faces.Count;
                maps++;
            }

            Assert.True(maps > 0, $"No {game} map was taken apart, so this proved nothing.");
            _out.WriteLine($"{game}: {maps} maps, {faces} faces became {tiles} tiles, "
                         + $"{(double)faces / Math.Max(1, tiles):0.0} faces to a tile.");
        }

        [SkippableTheory]
        [InlineData("Platinum")]
        [InlineData("HeartGold")]
        public void PaintingAMapsOwnGridAgainGivesTheShapeItAlreadyHad(string game)
        {
            string project = game == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            bool hgss = game == "HeartGold";
            Skip.If(!Directory.Exists(project), $"{game} test project not configured");

            int maps = 0, exact = 0;
            long lost = 0, placed = 0;

            foreach (var (name, bytes) in MapModels.Of(project, hgss).Take(40))
            {
                var mesh = MapMesh.Read(bytes, out _);
                if (mesh == null || mesh.Faces.Count == 0) continue;

                var set = MapTileset.FromMap(mesh, PictureOf(mesh), name, PaletteOf(mesh));
                var left = new List<MapMesh.Face>();
                var grid = TileGrid.Of(mesh, set, PictureOf(mesh), out int unmatched, PaletteOf(mesh), left);

                var baked = BakeLikeApply(mesh, set, grid, left);
                Assert.True(baked.Whynot == null, $"{name}: {baked.Whynot}");

                var had = new HashSet<(int x, int y, int z)>(
                    mesh.Vertices.Select(v => Near(v.X, v.Y, v.Z)));

                int astray = 0;
                foreach (var (x, y, z) in baked.Corners)
                    if (!CloseTo(had, x, y, z)) astray++;

                Assert.True(astray == 0,
                    $"{name}: {astray} of {baked.Corners.Count} corners were painted where the map has none.");

                if (baked.Faces.Count == mesh.Faces.Count) exact++;
                lost += unmatched;
                placed += grid.Painted;
                maps++;
            }

            Assert.True(maps > 0, $"No {game} map was painted again, so this proved nothing.");
            _out.WriteLine($"{game}: {maps} maps, {placed} squares painted, {exact} came back face for face, "
                         + $"{lost} pieces had no free layer and were kept as they were.");
            Assert.Equal(maps, exact);
        }

        [Fact]
        public void StackedFacesOverTwoSquaresAreOneTileOverTheGround()
        {
            var mesh = (MapMesh)Activator.CreateInstance(typeof(MapMesh), nonPublic: true);
            void Quad(float x0, float z0, float x1, float z1, float y)
            {
                int at = mesh.Vertices.Count;
                foreach (var (x, z) in new[] { (x0, z0), (x1, z0), (x1, z1), (x0, z1) })
                    mesh.Vertices.Add(new MapMesh.Vertex { X = x, Y = y, Z = z });
                mesh.Faces.Add(new MapMesh.Face { Corners = new[] { at, at + 1, at + 2, at + 3 }, Material = -1,
                                                  Colour = new[] { -1, -1, -1, -1 }, Normal = new[] { -1, -1, -1, -1 },
                                                  ColourLast = new bool[4] });
            }
            for (int z = 0; z < 4; z++)
                for (int x = 0; x < 4; x++)
                    Quad(-4f + x * 0.25f, -4f + z * 0.25f, -3.75f + x * 0.25f, -3.75f + z * 0.25f, 0f);
            Quad(-4f, -4f, -3.5f, -3.5f, 0.25f);
            Quad(-4.06f, -4.03f, -3.44f, -3.47f, 0.5f);
            Quad(-3.92f, -3.9f, -3.58f, -3.62f, 0.8f);

            var set = MapTileset.FromMap(mesh, _ => "p", "t");
            var grid = TileGrid.Of(mesh, set, _ => "p", out int unmatched);

            Assert.Equal(0, unmatched);
            var tree = set.Tiles.Single(t => t.Faces.Count == 3);
            Assert.Equal((2, 2), (tree.Wide, tree.Deep));
            Assert.Equal(set.Tiles.IndexOf(tree), grid.At(0, 0, 1).Tile);
            Assert.True(grid.At(1, 1, 0).Tile >= 0, "The ground under the tree was lost.");
            Assert.Equal(19, TileBake.Of(grid, set).Faces.Count);
        }

        private static IEnumerable<string> DrawnCorners(MapMesh mesh)
        {
            foreach (var face in mesh.Faces)
            {
                var look = face.Material >= 0 ? mesh.LookOf(face.Material) : null;
                for (int i = 0; i < face.Corners.Length; i++)
                {
                    var v = mesh.Vertices[face.Corners[i]];
                    var (x, y, z) = Near(v.X, v.Y, v.Z);
                    int colour = face.Colour[i] >= 0 ? face.Colour[i] : look?.CornerColour ?? -1;
                    int normal = face.Normal[i];
                    bool last = colour >= 0 && normal >= 0 && face.ColourLast[i];
                    yield return $"{x},{y},{z}|{look?.Key}|{colour}|{normal}|{last}";
                }
            }
        }

        [SkippableTheory]
        [InlineData("Platinum")]
        [InlineData("HeartGold")]
        public void AMapBuiltFromItsOwnTilesDrawsEveryCornerTheWayTheMapDid(string game)
        {
            string project = game == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            bool hgss = game == "HeartGold";
            Skip.If(!Directory.Exists(project), $"{game} test project not configured");

            int maps = 0, lit = 0, coloured = 0, looks = 0;
            long corners = 0, astray = 0, was = 0, now = 0;

            foreach (var (name, bytes) in MapModels.Of(project, hgss).Take(60))
            {
                var mesh = MapMesh.Read(bytes, out _);
                if (mesh == null || mesh.Faces.Count == 0) continue;

                var set = MapTileset.FromMap(mesh, PictureOf(mesh), name, PaletteOf(mesh));
                var grid = TileGrid.Of(mesh, set, PictureOf(mesh), out _, PaletteOf(mesh));
                var baked = BakeLikeApply(mesh, set, grid);
                byte[] model = TileBake.ToModel(baked, out string whynot);
                Assert.True(model != null, $"{name}: {whynot}");

                var back = MapMesh.Read(model, out whynot);
                Assert.True(back != null, $"{name}: the built model cannot be read: {whynot}");

                var had = new HashSet<string>(DrawnCorners(mesh));
                foreach (string corner in DrawnCorners(back))
                {
                    corners++;
                    if (!had.Contains(corner)) astray++;
                }

                was += bytes.Length;
                now += model.Length;

                if (back.Faces.Any(f => f.Normal.Any(n => n >= 0))) lit++;
                if (back.Faces.Any(f => f.Colour.Any(c => c >= 0))) coloured++;
                looks += Enumerable.Range(0, 64).Select(back.LookOf).Where(l => l != null)
                                   .Select(l => l.Key).Distinct().Count();
                maps++;
            }

            _out.WriteLine($"{game}: {maps} maps rebuilt, {corners} corners drawn, {astray} drawn differently; "
                         + $"{lit} maps keep their lighting, {coloured} their corner colours, {looks} material looks.");
            Assert.True(maps > 0, $"No {game} map was rebuilt, so this proved nothing.");
            Assert.True(lit > 0 || coloured > 0, "No rebuilt map carries lighting or colours, so this proved nothing.");
            Assert.Equal(0, astray);

            _out.WriteLine($"{game}: {was:n0} bytes of maps rebuilt as {now:n0}, {(double)now / was:0.00} times.");
            Assert.True(now < was * 1.25, $"Rebuilt maps came to {(double)now / was:0.00} times their size.");
        }

        [SkippableFact]
        public void ATileSetDownTurnedStaysInsideItsOwnSquare()
        {
            var set = new MapTileset();
            var tile = new MapTileset.Tile();
            foreach (var (x, z) in new[] { (0f, 0f), (0.25f, 0f), (0.25f, 0.25f), (0f, 0.25f) })
                tile.Corners.Add(new MapTileset.Corner { X = x, Y = 0f, Z = z });
            tile.Faces.Add(new MapTileset.Face { Corners = new[] { 0, 1, 2, 3 }, Picture = "p" });
            set.Tiles.Add(tile);

            for (byte turn = 0; turn < 4; turn++)
            {
                var grid = new TileGrid();
                grid.Put(5, 7, 0, 0f, turn);
                var baked = TileBake.Of(grid, set);

                float originX = -MapTileset.HalfMap + 5 * MapTileset.TileWidth;
                float originZ = -MapTileset.HalfMap + 7 * MapTileset.TileWidth;

                foreach (var (x, _, z) in baked.Corners)
                {
                    Assert.InRange(x, originX - 1e-4f, originX + MapTileset.TileWidth + 1e-4f);
                    Assert.InRange(z, originZ - 1e-4f, originZ + MapTileset.TileWidth + 1e-4f);
                }
            }
        }

        [SkippableFact]
        public void ATileSetDownHigherStandsHigherByExactlyThatMuch()
        {
            var set = new MapTileset();
            var tile = new MapTileset.Tile();
            foreach (var (x, z) in new[] { (0f, 0f), (0.25f, 0f), (0.25f, 0.25f), (0f, 0.25f) })
                tile.Corners.Add(new MapTileset.Corner { X = x, Y = 0f, Z = z });
            tile.Faces.Add(new MapTileset.Face { Corners = new[] { 0, 1, 2, 3 }, Picture = "p" });
            set.Tiles.Add(tile);

            var grid = new TileGrid();
            grid.Put(0, 0, 0, 0f, 0);
            grid.Put(1, 0, 0, 3 * MapTileset.TileWidth, 0);

            var baked = TileBake.Of(grid, set);
            float low = baked.Corners.Min(c => c.y), high = baked.Corners.Max(c => c.y);
            Assert.Equal(3 * MapTileset.TileWidth, high - low, 4);
        }

        [SkippableFact]
        public void ATilesetBroughtInFromAFileKeepsOneTilePerNamedPart()
        {
            string dir = Path.Combine(Path.GetTempPath(), "dspre_tileset_obj");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "set.obj");

            File.WriteAllText(path, string.Join("\n", new[]
            {
                "mtllib set.mtl",
                "v 0 0 0", "v 1 0 0", "v 1 0 1", "v 0 0 1",
                "v 0 0 0", "v 1 0 0", "v 1 1 0", "v 0 1 0",
                "vt 0 0", "vt 1 0", "vt 1 1", "vt 0 1",
                "o grass", "usemtl green", "f 1/1 2/2 3/3 4/4",
                "o wall",  "usemtl stone", "f 5/1 6/2 7/3 8/4",
            }));
            File.WriteAllText(Path.Combine(dir, "set.mtl"), "newmtl green\nnewmtl stone\n");

            var set = MapTileset.FromObj(path, out string whynot);
            Assert.True(set != null, whynot);
            Assert.Equal(2, set.Tiles.Count);
            Assert.Equal(new[] { "grass", "wall" }, set.Tiles.Select(t => t.Name).ToArray());
            Assert.Equal(new[] { "green", "stone" }, set.Tiles.Select(t => t.Picture).ToArray());

            foreach (var tile in set.Tiles)
            {
                Assert.Equal(0f, tile.Corners.Min(c => c.X), 4);
                Assert.Equal(0f, tile.Corners.Min(c => c.Y), 4);
                Assert.Equal(0f, tile.Corners.Min(c => c.Z), 4);
            }
        }

        [SkippableTheory]
        [InlineData("Platinum")]
        [InlineData("HeartGold")]
        public void AMapBuiltFromTilesNamesThePicturesTheAreaFindsItBy(string game)
        {
            string project = game == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            bool hgss = game == "HeartGold";
            Skip.If(!Directory.Exists(project), $"{game} test project not configured");

            int done = 0;

            foreach (var (name, bytes) in MapModels.Of(project, hgss).Take(25))
            {
                var mesh = MapMesh.Read(bytes, out _);
                if (mesh == null || mesh.Faces.Count == 0) continue;

                var wanted = new HashSet<string>(
                    mesh.Faces.Select(f => PictureOf(mesh)(f.Material)), StringComparer.Ordinal);

                var set = MapTileset.FromMap(mesh, PictureOf(mesh), name, PaletteOf(mesh));
                var grid = TileGrid.Of(mesh, set, PictureOf(mesh), out _, PaletteOf(mesh));
                byte[] built = TileBake.ToModel(BakeLikeApply(mesh, set, grid), out string whynot);
                Assert.True(built != null, $"{name}: {whynot}");

                var file = NsbmdFile.Read(built, out string why);
                Assert.True(file != null, $"{name}: what was built cannot be read, {why}");

                var named = new HashSet<string>(file.Pictures.Select(p => p.Name), StringComparer.Ordinal);
                foreach (string picture in wanted)
                    Assert.True(named.Contains(picture),
                        $"{name}: the built map never names {picture}, so nothing would paint with it.");

                foreach (var picture in file.Pictures)
                    Assert.True(picture.Materials.Count > 0,
                        $"{name}: {picture.Name} is named but paints nothing.");

                done++;
            }

            Assert.True(done > 0, $"No {game} map was built from tiles, so this proved nothing.");
            _out.WriteLine($"{game}: {done} maps built from their own tiles name every picture they need.");
        }

        [SkippableTheory]
        [InlineData("Platinum")]
        [InlineData("HeartGold")]
        public void AMapDrawnAtAnotherScaleIsTakenApartOntoTheGridAndBuiltWhereItStood(string game)
        {
            string project = game == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            bool hgss = game == "HeartGold";
            Skip.If(!Directory.Exists(project), $"{game} test project not configured");

            int done = 0;
            foreach (var (name, bytes) in MapModels.Of(project, hgss))
            {
                var mesh = MapMesh.Read(bytes, out _);
                // Interiors mostly declare 32; the squares are measured at 64.
                if (mesh == null || mesh.Faces.Count == 0 || mesh.ModelScale != 32f) continue;
                float Lowest(MapMesh m, Func<MapMesh.Vertex, float> axis) => m.Vertices.Min(axis) * m.ModelScale;
                float Highest(MapMesh m, Func<MapMesh.Vertex, float> axis) => m.Vertices.Max(axis) * m.ModelScale;
                float x0 = Lowest(mesh, v => v.X), x1 = Highest(mesh, v => v.X), z0 = Lowest(mesh, v => v.Z), z1 = Highest(mesh, v => v.Z);

                mesh.ScaleTo(64f);
                Assert.Null(mesh.Save(out _));
                var set = MapTileset.FromMap(mesh, PictureOf(mesh), name, PaletteOf(mesh));
                var left = new List<MapMesh.Face>();
                var grid = TileGrid.Of(mesh, set, PictureOf(mesh), out _, PaletteOf(mesh), left);
                Assert.True(grid.Painted > 0, $"{name}: nothing landed on the grid.");
                Assert.True(left.Count * 10 < mesh.Faces.Count, $"{name}: {left.Count} of {mesh.Faces.Count} faces found no square.");

                var again = MapMesh.Read(TileBake.ToModel(BakeLikeApply(mesh, set, grid, left), out _), out _);
                Assert.Equal(64f, again.ModelScale, 3);
                // A game unit is a sixteenth of a square; the rebuilt map stands where the original did.
                Assert.InRange(Lowest(again, v => v.X), x0 - 1f, x0 + 1f);
                Assert.InRange(Highest(again, v => v.X), x1 - 1f, x1 + 1f);
                Assert.InRange(Lowest(again, v => v.Z), z0 - 1f, z0 + 1f);
                Assert.InRange(Highest(again, v => v.Z), z1 - 1f, z1 + 1f);
                if (++done == 10) break;
            }

            Assert.True(done > 0, $"No {game} map declares scale 32, so this proved nothing.");
            _out.WriteLine($"{game}: {done} maps at scale 32 taken apart onto the grid and rebuilt in place.");
        }

        [SkippableTheory]
        [InlineData("Platinum")]
        [InlineData("HeartGold")]
        public void AMapBuiltFromTilesNamesTheColoursItsPicturesAreDrawnWith(string game)
        {
            string project = game == "Platinum" ? TestRoms.Platinum : TestRoms.HeartGold;
            bool hgss = game == "HeartGold";
            Skip.If(!Directory.Exists(project), $"{game} test project not configured");

            int done = 0, differ = 0;

            foreach (var (name, bytes) in MapModels.Of(project, hgss).Take(25))
            {
                var mesh = MapMesh.Read(bytes, out _);
                if (mesh == null || mesh.Faces.Count == 0) continue;

                var wanted = new HashSet<string>(
                    mesh.Faces.Select(f => PaletteOf(mesh)(f.Material))
                              .Where(p => !string.IsNullOrEmpty(p)), StringComparer.Ordinal);
                if (wanted.Count == 0) continue;

                if (wanted.Any(p => !mesh.Faces.Select(f => PictureOf(mesh)(f.Material)).Contains(p)))
                    differ++;

                var set = MapTileset.FromMap(mesh, PictureOf(mesh), name, PaletteOf(mesh));
                var grid = TileGrid.Of(mesh, set, PictureOf(mesh), out _, PaletteOf(mesh));
                byte[] built = TileBake.ToModel(BakeLikeApply(mesh, set, grid), out _, mesh.ModelScale);

                var file = NsbmdFile.Read(built, out _);
                var named = new HashSet<string>(file.Colours.Select(c => c.Name), StringComparer.Ordinal);

                foreach (string colours in wanted)
                    Assert.True(named.Contains(colours),
                        $"{name}: the built map never names the colours {colours}.");

                done++;
            }

            Assert.True(done > 0, $"No {game} map was built, so this proved nothing.");
            _out.WriteLine($"{game}: {done} built maps name their colours, "
                         + $"{differ} of them where the colours are not called what the picture is.");
        }

        [Fact]
        public void ATileCoveringSeveralSquaresIsPutDownOnceAndTakenAwayWhole()
        {
            var set = new MapTileset();
            var big = new MapTileset.Tile { Wide = 3, Deep = 2 };
            foreach (var (x, z) in new[] { (0f, 0f), (0.75f, 0f), (0.75f, 0.5f), (0f, 0.5f) })
                big.Corners.Add(new MapTileset.Corner { X = x, Y = 0f, Z = z });
            big.Faces.Add(new MapTileset.Face { Corners = new[] { 0, 1, 2, 3 }, Picture = "p" });
            set.Tiles.Add(big);

            var grid = new TileGrid();
            grid.Put(4, 5, 0, 0f, 0, big.Wide, big.Deep);

            Assert.Equal(6, grid.Painted);
            for (int dz = 0; dz < 2; dz++)
                for (int dx = 0; dx < 3; dx++)
                {
                    var square = grid.At(4 + dx, 5 + dz);
                    Assert.Equal(0, square.Tile);
                    Assert.Equal(4, square.FromX);
                    Assert.Equal(5, square.FromZ);
                }

            Assert.Single(grid.Placed());
            Assert.Single(TileBake.Of(grid, set).Faces);

            grid.Clear(6, 6);
            Assert.Equal(0, grid.Painted);
        }

        [Fact]
        public void PaintingInsideALargeTileLeavesItTheWayMapStudioDoes()
        {
            var grid = new TileGrid();
            grid.Put(2, 2, 0, 0f, 0, 3, 3);
            Assert.Equal(9, grid.Painted);

            grid.PutTile(3, 3, 1, 0);
            Assert.Equal(9, grid.Painted);
            Assert.Equal(1, grid.At(3, 3).Tile);
            Assert.True(grid.At(3, 3).WhereItWasPut);
            Assert.Equal(0, grid.At(2, 2).Tile);
            Assert.Equal(0, grid.At(4, 4).Tile);

            grid.PutTile(2, 2, 1, 0);
            Assert.Equal(1, grid.At(2, 2).Tile);
            Assert.Equal(-1, grid.PutHere(4, 4));
        }

        [Fact]
        public void EveryLayerHoldsItsOwnTilesAndItsOwnHeights()
        {
            var grid = new TileGrid();
            grid.PutTile(5, 5, 0, 0, layer: 0);
            grid.PutTile(5, 5, 1, 0, layer: 1);
            grid.SetHeight(5, 5, 2 * TileGrid.Step, layer: 1);

            Assert.Equal(0, grid.At(5, 5, 0).Tile);
            Assert.Equal(1, grid.At(5, 5, 1).Tile);
            Assert.Equal(0f, grid.At(5, 5, 0).Lift);
            Assert.Equal(2 * TileGrid.Step, grid.At(5, 5, 1).Lift, 5);

            Assert.Equal(2, grid.Placed().Count());
            Assert.Equal(1, grid.Painted);
            Assert.Equal(1, grid.PaintedOn(0));
            Assert.Equal(1, grid.PaintedOn(1));
            Assert.Equal(0, grid.PaintedOn(2));
        }

        [Fact]
        public void PaintingATileLeavesTheHeightThatIsAlreadyThere()
        {
            var grid = new TileGrid();

            grid.SetHeight(7, 7, 3 * TileGrid.Step);
            grid.PutTile(7, 7, 4, 0);
            Assert.Equal(3 * TileGrid.Step, grid.At(7, 7).Lift, 5);

            grid.Clear(7, 7);
            Assert.Equal(-1, grid.At(7, 7).Tile);
            Assert.Equal(3 * TileGrid.Step, grid.HeightAt(7, 7), 5);
        }

        [Fact]
        public void ALayerSlidesAlongWithItsHeightsAndLosesWhatFallsOff()
        {
            var grid = new TileGrid();
            grid.PutTile(0, 0, 3, 0, layer: 2);
            grid.SetHeight(0, 0, TileGrid.Step, layer: 2);
            grid.PutTile(31, 10, 5, 0, layer: 2);

            grid.Shift(2, 1, 0);

            Assert.Equal(3, grid.At(1, 0, 2).Tile);
            Assert.Equal(TileGrid.Step, grid.HeightAt(1, 0, 2), 5);
            Assert.Equal(-1, grid.PutHere(0, 0, 2));

            Assert.Equal(-1, grid.PutHere(0, 10, 2));
            Assert.Single(grid.Placed());

            Assert.False(grid.HasAnything(0));
        }

        [Fact]
        public void ALargeTileSlidOverTheEdgeHangsOverItAsInMapStudio()
        {
            var grid = new TileGrid();
            grid.PutTile(29, 4, 0, 0, 3, 1);
            grid.Shift(0, 1, 0);

            var sq = grid.At(30, 4);
            Assert.Equal(0, sq.Tile);
            Assert.Equal((2, 3), (sq.Wide, sq.FullWide));

            grid.Shift(0, 2, 0);
            Assert.False(grid.HasAnything(0));
        }

        [Fact]
        public void ATileHangingOverTheNorthEdgeIsBuiltWhole()
        {
            var set = new MapTileset();
            var tile = new MapTileset.Tile { Name = "tall", Wide = 1, Deep = 3 };
            foreach (var (x, z) in new[] { (0f, 0f), (0.25f, 0f), (0.25f, 0.75f), (0f, 0.75f) })
                tile.Corners.Add(new MapTileset.Corner { X = x, Z = z });
            tile.Faces.Add(new MapTileset.Face { Corners = new[] { 0, 1, 2, 3 }, Picture = "p" });
            set.Tiles.Add(tile);

            var grid = new TileGrid();
            Assert.True(grid.PutTile(5, -1, 0, 0, 1, 3));
            var sq = grid.At(5, 0);
            Assert.Equal((1, 2, 3), (sq.PastNorth, sq.Deep, sq.FullDeep));

            var baked = TileBake.Of(grid, set);
            Assert.Equal(-4f - 0.25f, baked.Corners.Min(c => c.z), 4);
            Assert.Equal(-4f + 0.5f, baked.Corners.Max(c => c.z), 4);
        }

        [Fact]
        public void RaisingALayerStopsWhereMapStudioStops()
        {
            var grid = new TileGrid();
            grid.SetHeight(1, 1, 14 * TileGrid.Step);
            grid.SetHeight(2, 2, 0f);

            grid.Raise(0, 3);
            Assert.Equal(TileGrid.HighestStep * TileGrid.Step, grid.HeightAt(1, 1), 5);
            Assert.Equal(3 * TileGrid.Step, grid.HeightAt(2, 2), 5);

            grid.Raise(0, -40);
            Assert.Equal(TileGrid.LowestStep * TileGrid.Step, grid.HeightAt(2, 2), 5);
        }

        [Fact]
        public void ABucketFillOfALargeTileMakesANeatPatternAndStopsAtOtherTiles()
        {
            var grid = new TileGrid();

            for (int z = 0; z < TileGrid.Across; z++) grid.PutTile(10, z, 9, 0);

            int laid = grid.FloodFillTile(0, 0, 2, 0, 2, 2);

            Assert.Equal(5 * 16, laid);
            for (int z = 0; z < TileGrid.Across; z += 2)
                for (int x = 0; x < 10; x += 2)
                {
                    Assert.Equal(2, grid.PutHere(x, z));
                    Assert.Equal(-1, grid.PutHere(x + 1, z));
                }

            Assert.Equal(-1, grid.At(20, 5).Tile);
            Assert.Equal(9, grid.At(10, 5).Tile);
        }

        [Fact]
        public void ABucketFillOfHeightFillsOnlyWhatStoodAtTheSameHeight()
        {
            var grid = new TileGrid();
            for (int x = 0; x < TileGrid.Across; x++) grid.SetHeight(x, 16, TileGrid.Step);

            int filled = grid.FloodFillHeight(3, 3, 2 * TileGrid.Step);

            Assert.Equal(16 * TileGrid.Across, filled);
            Assert.Equal(2 * TileGrid.Step, grid.HeightAt(3, 3), 5);
            Assert.Equal(TileGrid.Step, grid.HeightAt(3, 16), 5);
            Assert.Equal(0f, grid.HeightAt(3, 20), 5);
        }

        [Fact]
        public void EveryLayerIsBuiltIntoTheMap()
        {
            var set = new MapTileset();
            foreach (float y in new[] { 0f, 0.5f })
            {
                var tile = new MapTileset.Tile();
                foreach (var (x, z) in new[] { (0f, 0f), (0.25f, 0f), (0.25f, 0.25f), (0f, 0.25f) })
                    tile.Corners.Add(new MapTileset.Corner { X = x, Y = y, Z = z });
                tile.Faces.Add(new MapTileset.Face { Corners = new[] { 0, 1, 2, 3 }, Picture = "p" });
                set.Tiles.Add(tile);
            }

            var grid = new TileGrid();
            grid.PutTile(4, 4, 0, 0, layer: 0);
            grid.PutTile(4, 4, 1, 0, layer: 5);

            var baked = TileBake.Of(grid, set);
            Assert.Equal(2, baked.Faces.Count);
            Assert.Contains(baked.Corners, c => Math.Abs(c.y) < 1e-5f);
            Assert.Contains(baked.Corners, c => Math.Abs(c.y - 0.5f) < 1e-5f);
        }

        [Fact]
        public void ACopiedLayerPastesBackExactlyAndACloneIsItsOwnThing()
        {
            var grid = new TileGrid();
            grid.PutTile(3, 3, 7, 1, 2, 1, layer: 4);
            grid.SetHeight(3, 4, 0.125f, layer: 4);

            var copy = grid.CopyLayer(4);
            grid.PasteLayer(6, copy);
            Assert.Equal(7, grid.At(3, 3, 6).Tile);
            Assert.Equal(1, grid.At(3, 3, 6).Turn);
            Assert.Equal(0.125f, grid.At(3, 3, 6).Lift, 5);

            var clone = grid.Clone();
            grid.ClearLayer(4);
            Assert.Equal(7, clone.At(3, 3, 4).Tile);
            Assert.Equal(-1, grid.At(3, 3, 4).Tile);
        }

        [Fact]
        public void ATileTurnedOnItsSideCoversTheOtherWayAbout()
        {
            var grid = new TileGrid();

            Assert.Equal((3, 1), TileGrid.Footprint(3, 1, 0));
            Assert.Equal((1, 3), TileGrid.Footprint(3, 1, 1));

            Assert.True(grid.Fits(30, 2, 3, 1, 0));
            Assert.False(grid.Fits(4, 30, 1, 3, 0));
            Assert.True(grid.Fits(4, 30, 1, 3, 1));
        }

        [SkippableFact]
        public void ATilesetBroughtInFromAFileSaysHowManySquaresEachTileCovers()
        {
            string dir = Path.Combine(Path.GetTempPath(), "dspre_tileset_big");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "big.obj");

            File.WriteAllText(path, string.Join(Environment.NewLine, new[]
            {
                "v 0 0 0", "v 0.25 0 0", "v 0.25 0 0.25", "v 0 0 0.25",
                "v 0 0 0", "v 0.75 0 0", "v 0.75 0 0.5", "v 0 0 0.5",
                "o one", "f 1 2 3 4",
                "o three", "f 5 6 7 8",
            }));

            var set = MapTileset.FromObj(path, out string whynot);
            Assert.True(set != null, whynot);
            Assert.Equal(2, set.Tiles.Count);

            Assert.Equal(1, set.Tiles[0].Wide);
            Assert.Equal(1, set.Tiles[0].Deep);
            Assert.False(set.Tiles[0].Spreads);

            Assert.Equal(3, set.Tiles[1].Wide);
            Assert.Equal(2, set.Tiles[1].Deep);
            Assert.True(set.Tiles[1].Spreads);
        }

        [SkippableFact]
        public void ATilesetWrittenOutAndBroughtBackIsTheSameSet()
        {
            var files = Directory.Exists(Path.Combine(Path.GetTempPath(), "claude"))
                ? Directory.EnumerateFiles(Path.Combine(Path.GetTempPath(), "claude"), "*.pdsts",
                                           SearchOption.AllDirectories).Take(4).ToList()
                : new List<string>();
            Skip.If(files.Count == 0, "No .pdsts tilesets are available to read.");

            string dir = Path.Combine(Path.GetTempPath(), "dspre_tileset_save");
            Directory.CreateDirectory(dir);

            int done = 0;
            foreach (string source in files)
            {
                var was = PdstsFile.Read(source, out _);
                if (was == null) continue;

                was.TurnPlacesIntoDots(_ => (64, 64));

                string path = Path.Combine(dir, Path.GetFileNameWithoutExtension(source) + ".obj");
                Assert.Null(was.SaveObj(path));

                var back = MapTileset.FromObj(path, out string whynot);
                Assert.True(back != null, whynot);
                back.TurnPlacesIntoDots(_ => (64, 64));

                Assert.Equal(was.Tiles.Count, back.Tiles.Count);

                for (int i = 0; i < was.Tiles.Count; i++)
                {
                    var a = was.Tiles[i];
                    var b = back.Tiles[i];

                    Assert.True(a.Wide == b.Wide && a.Deep == b.Deep,
                        $"{Path.GetFileName(source)}: {a.Name} went out {a.Wide}x{a.Deep} and came back "
                      + $"{b.Wide}x{b.Deep}.");

                    Assert.Equal(a.Faces.Count, b.Faces.Count);

                    for (int f = 0; f < a.Faces.Count; f++)
                        for (int k = 0; k < a.Faces[f].Corners.Length; k++)
                        {
                            var ca = a.Corners[a.Faces[f].Corners[k]];
                            var cb = b.Corners[b.Faces[f].Corners[k]];
                            Assert.True(Math.Abs(ca.X - cb.X) < 1e-4f && Math.Abs(ca.Y - cb.Y) < 1e-4f
                                     && Math.Abs(ca.Z - cb.Z) < 1e-4f,
                                $"{Path.GetFileName(source)}: {a.Name} face {f} moved on the way through.");

                            Assert.Equal(ca.Colour, cb.Colour);
                            Assert.Equal(ca.Faces, cb.Faces);
                            if (ca.Faces)
                                Assert.True(Math.Abs(ca.NX - cb.NX) < 1e-4f && Math.Abs(ca.NY - cb.NY) < 1e-4f
                                         && Math.Abs(ca.NZ - cb.NZ) < 1e-4f,
                                    $"{Path.GetFileName(source)}: {a.Name} face {f} faces another way.");
                            if (ca.Colour >= 0 && ca.Faces) Assert.Equal(ca.ColourLast, cb.ColourLast);
                        }

                    for (int f = 0; f < a.Faces.Count; f++)
                    {
                        Assert.Equal(a.Faces[f].Corners.Length, b.Faces[f].Corners.Length);
                        Assert.Equal(a.Faces[f].Picture, b.Faces[f].Picture);

                        Assert.Equal(a.Faces[f].Palette, b.Faces[f].Palette);
                        Assert.Equal(a.Faces[f].Look?.Key, b.Faces[f].Look?.Key);
                    }
                }

                Assert.Equal(was.SmartDrawings.Count, back.SmartDrawings.Count);
                done++;
            }

            Assert.True(done > 0, "No tileset was written out, so this proved nothing.");
            _out.WriteLine($"{done} tilesets written out and brought back unchanged.");
        }

        [SkippableTheory]
        [InlineData("Platinum")]
        [InlineData("HeartGold")]
        [InlineData("Diamond")]
        public void EveryMapIsTakenApartOntoTheGridAndBuiltBackToTheSameShape(string game)
        {
            string project = game == "Platinum" ? TestRoms.Platinum : game == "Diamond" ? TestRoms.Diamond : TestRoms.HeartGold;
            bool hgss = game == "HeartGold";
            Skip.If(!Directory.Exists(project), $"{game} test project not configured");

            int maps = 0, withLeft = 0;
            long leftFaces = 0;
            var failed = new List<string>();
            var kept = new List<(string name, int left, int faces)>();
            foreach (var (name, bytes) in MapModels.Of(project, hgss))
            {
                var mesh = MapMesh.Read(bytes, out _);
                if (mesh == null || mesh.Faces.Count == 0) continue;
                float scale = mesh.ModelScale;
                // As the Tiles tab does: squares are measured at scale 64.
                mesh.ScaleTo(64f);
                maps++;

                var set = MapTileset.FromMap(mesh, PictureOf(mesh), name, PaletteOf(mesh));
                var left = new List<MapMesh.Face>();
                var grid = TileGrid.Of(mesh, set, PictureOf(mesh), out _, PaletteOf(mesh), left);
                if (left.Count > 0) { withLeft++; leftFaces += left.Count; kept.Add((name, left.Count, mesh.Faces.Count)); }

                byte[] model = TileBake.ToModel(BakeLikeApply(mesh, set, grid, left), out string whynot);
                if (model == null) { failed.Add($"{name} (scale {scale}): cannot be built: {whynot}"); continue; }
                var back = MapMesh.Read(model, out whynot);
                if (back == null) { failed.Add($"{name} (scale {scale}): built model unreadable: {whynot}"); continue; }
                back.ScaleTo(64f);

                HashSet<(int, int, int)> Corners(MapMesh m) => new HashSet<(int, int, int)>(
                    m.Faces.SelectMany(f => f.Corners).Select(i => Near(m.Vertices[i].X, m.Vertices[i].Y, m.Vertices[i].Z)));
                var had = Corners(mesh);
                var now = Corners(back);
                // Ground cut into squares adds corners but still lies on the map's own faces.
                int astray = now.Count(c => !CloseTo(had, c.Item1 / 4096f, c.Item2 / 4096f, c.Item3 / 4096f)
                                            && !OnAFace(mesh, c.Item1 / 4096f, c.Item2 / 4096f, c.Item3 / 4096f));
                int lost = had.Count(c => !CloseTo(now, c.Item1 / 4096f, c.Item2 / 4096f, c.Item3 / 4096f));
                if (astray > 0 || lost > 0)
                    failed.Add($"{name} (scale {scale}, {mesh.Faces.Count} faces, {left.Count} kept as they were): "
                             + $"{astray} corners off the map's faces, {lost} of the map's corners missing");
            }

            _out.WriteLine($"{game}: {maps} maps; {withLeft} keep some faces as they were ({leftFaces} faces); {failed.Count} fail.");
            foreach (string f in failed) _out.WriteLine("  " + f);
            _out.WriteLine("  kept as they were: " + string.Join(", ", kept.OrderByDescending(k => (double)k.left / k.faces)
                                                                     .Select(k => $"{k.name} {k.left}/{k.faces}")));
            Assert.True(maps > 0, $"No {game} map was read, so this proved nothing.");
            Assert.Empty(failed);
        }

        private static bool OnAFace(MapMesh mesh, float x, float y, float z)
        {
            const float Slack = 2f / 4096f;
            foreach (var face in mesh.Faces)
            {
                var c = face.Corners.Select(i => mesh.Vertices[i]).ToArray();
                for (int t = 1; t + 1 < c.Length; t++)
                {
                    var (a, b, d) = (c[0], c[t], c[t + 1]);
                    float ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z, vx = d.X - a.X, vy = d.Y - a.Y, vz = d.Z - a.Z;
                    float nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                    float len = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
                    if (len < 1e-9f) continue;
                    float wx = x - a.X, wy = y - a.Y, wz = z - a.Z;
                    if (MathF.Abs((wx * nx + wy * ny + wz * nz) / len) > Slack) continue;
                    // Barycentric test on the triangle, with a little slack for its edges.
                    float uu = ux * ux + uy * uy + uz * uz, vv = vx * vx + vy * vy + vz * vz, uv = ux * vx + uy * vy + uz * vz;
                    float wu = wx * ux + wy * uy + wz * uz, wv = wx * vx + wy * vy + wz * vz;
                    float den = uv * uv - uu * vv;
                    if (MathF.Abs(den) < 1e-12f) continue;
                    float s = (uv * wv - vv * wu) / den, r = (uv * wu - uu * wv) / den;
                    const float Edge = 1e-3f;
                    if (s >= -Edge && r >= -Edge && s + r <= 1 + Edge) return true;
                }
            }
            return false;
        }

        private static (int x, int y, int z) Near(float x, float y, float z)
            => ((int)MathF.Round(x * 4096f), (int)MathF.Round(y * 4096f), (int)MathF.Round(z * 4096f));

        // Corners of ground cut into squares are mixed in floats and can land a hair (a few 4096ths) off the map's own.
        private static bool CloseTo(HashSet<(int x, int y, int z)> had, float x, float y, float z)
        {
            var (nx, ny, nz) = Near(x, y, z);
            for (int dx = -3; dx <= 3; dx++)
                for (int dy = -3; dy <= 3; dy++)
                    for (int dz = -3; dz <= 3; dz++)
                        if (had.Contains((nx + dx, ny + dy, nz + dz))) return true;
            return false;
        }
    }
}

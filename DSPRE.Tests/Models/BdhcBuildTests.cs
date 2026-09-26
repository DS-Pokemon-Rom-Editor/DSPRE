using DSPRE;
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
    public class BdhcBuildTests
    {
        private readonly ITestOutputHelper _out;
        public BdhcBuildTests(ITestOutputHelper o) { _out = o; }

        private static IEnumerable<(string name, byte[] model, byte[] terrain)> MapsWithTerrain(
            string project, bool hgss)
        {
            string maps = Path.Combine(project, "unpacked", "maps");
            foreach (string path in RomFiles.Settled(maps))
            {
                byte[] model = null, terrain = null;
                try
                {
                    byte[] map = File.ReadAllBytes(path);
                    int permissions = BitConverter.ToInt32(map, 0);
                    int buildings = BitConverter.ToInt32(map, 4);
                    int modelSize = BitConverter.ToInt32(map, 8);
                    int terrainSize = BitConverter.ToInt32(map, 12);

                    int at = 16;
                    if (hgss)
                    {
                        if (BitConverter.ToUInt16(map, at) != 0x1234) continue;
                        at += 4 + BitConverter.ToUInt16(map, at + 2);
                    }
                    at += permissions + buildings;
                    if (at + modelSize + terrainSize > map.Length) continue;

                    model = map.Skip(at).Take(modelSize).ToArray();
                    terrain = map.Skip(at + modelSize).Take(terrainSize).ToArray();
                }
                catch { continue; }

                if (model != null && terrain != null && terrain.Length > 16)
                    yield return (Path.GetFileName(path), model, terrain);
            }
        }

        [SkippableFact]
        public void TerrainBuiltFromAMapsShapeGivesTheHeightsTheMapShipsWith()
        {
            Skip.If(!Directory.Exists(TestRoms.Platinum), "Platinum test project not configured");

            int maps = 0;
            long asked = 0, agreed = 0, onlyMine = 0, onlyTheirs = 0, nearlyAgreed = 0, farOff = 0;

            foreach (var (name, model, terrain) in MapsWithTerrain(TestRoms.Platinum, false).Take(60))
            {
                if (!BdhcFile.TryParse(terrain, out var shipped)) continue;

                var mesh = MapMesh.Read(model, out _);
                if (mesh == null) continue;

                var pieces = BdhcBuild.PiecesOf(mesh, mesh.ModelScale);
                if (pieces.Count == 0) continue;

                byte[] made = BdhcBuild.From(pieces, out string whynot);
                Assert.True(made != null, $"{name}: {whynot}");
                Assert.True(BdhcFile.TryParse(made, out var mine),
                            $"{name}: what was written cannot be read back as terrain.");

                for (int tz = 0; tz < 32; tz++)
                    for (int tx = 0; tx < 32; tx++)
                    {
                        float rawX = (tx + 0.5f) * 0.25f;
                        float rawZ = (tz + 0.5f) * 0.25f;

                        bool theirs = shipped.TryGetHeight(rawX, rawZ, 0f, out float wasY);

                        bool ours = mine.TryGetHeight(rawX, rawZ, theirs ? wasY : 0f, out float nowY);

                        asked++;
                        if (theirs && ours)
                        {
                            float off = Math.Abs(wasY - nowY);
                            if (off < 0.01f) agreed++;
                            else if (off < 0.25f) nearlyAgreed++;
                            else farOff++;
                        }
                        else if (ours) onlyMine++;
                        else if (theirs) onlyTheirs++;
                    }

                maps++;
            }

            Assert.True(maps > 0, "No Platinum map with terrain was read, so this proved nothing.");

            _out.WriteLine($"{maps} maps, {asked} places asked: {agreed} the same height, "
                         + $"{nearlyAgreed} within a quarter tile, {farOff} further off, "
                         + $"{onlyMine} only in the built terrain, {onlyTheirs} only in the shipped one.");

            Assert.True(agreed > asked * 0.7,
                $"Only {agreed} of {asked} places gave the height the map ships with.");
            Assert.True(onlyTheirs < asked / 1000,
                $"{onlyTheirs} places the map can be walked on are not in the built terrain at all.");
        }

        [SkippableFact]
        public void RaisingTheGroundRaisesWhatIsWalkedOn()
        {
            Skip.If(!Directory.Exists(TestRoms.Platinum), "Platinum test project not configured");

            foreach (var (name, model, terrain) in MapsWithTerrain(TestRoms.Platinum, false).Take(40))
            {
                var mesh = MapMesh.Read(model, out _);
                if (mesh == null || mesh.Vertices.Count < 8) continue;

                var before = BdhcBuild.PiecesOf(mesh, mesh.ModelScale);
                if (before.Count == 0) continue;

                byte[] was = BdhcBuild.From(before, out _);
                if (was == null || !BdhcFile.TryParse(was, out var wasTerrain)) continue;

                float atX = 0f, atZ = 0f, wasY = 0f;
                bool found = false;
                for (int tz = 0; tz < 32 && !found; tz++)
                    for (int tx = 0; tx < 32 && !found; tx++)
                    {
                        float rx = (tx + 0.5f) * 0.25f, rz = (tz + 0.5f) * 0.25f;
                        if (!wasTerrain.TryGetHeight(rx, rz, 0f, out wasY)) continue;
                        atX = rx; atZ = rz; found = true;
                    }
                if (!found) continue;

                for (int i = 0; i < mesh.Vertices.Count; i++)
                {
                    var v = mesh.Vertices[i];
                    mesh.Move(i, v.X, v.Y + 0.0625f, v.Z);
                }

                byte[] now = BdhcBuild.From(BdhcBuild.PiecesOf(mesh, mesh.ModelScale), out string whynot);
                Assert.True(now != null, $"{name}: {whynot}");
                Assert.True(BdhcFile.TryParse(now, out var nowTerrain), $"{name}: it cannot be read back.");
                Assert.True(nowTerrain.TryGetHeight(atX, atZ, 0f, out float nowY),
                            $"{name}: the ground is no longer answered for where it was.");

                Assert.True(Math.Abs((nowY - wasY) - 0.0625f) < 0.005f,
                    $"{name}: raising every corner by 0.0625 moved the ground by {nowY - wasY:0.####}.");
                _out.WriteLine($"{name}: ground rose from {wasY:0.####} to {nowY:0.####}.");
                return;
            }

            Assert.Fail("No Platinum map could be raised, so this proved nothing.");
        }

        [Fact]
        public void AWallIsNotSomethingToStandOn()
        {
            var mesh = FlatAndUpright();
            var pieces = BdhcBuild.PiecesOf(mesh, 64f);

            Assert.Single(pieces);
            Assert.True(pieces[0].Ny > 0.9f, "The piece kept is not the flat one.");
        }

        private static BdhcBuild.Piece Flat(float x0, float z0, float x1, float z1, float y)
            => new BdhcBuild.Piece { MinX = x0, MinZ = z0, MaxX = x1, MaxZ = z1, Ny = 1f, D = -y, AtY = y };

        private static List<BdhcBuild.Piece> Ground()
        {
            var pieces = new List<BdhcBuild.Piece>();
            for (int r = 0; r < 32; r++)
                for (int c = 0; c < 32; c++)
                    pieces.Add(Flat(-256 + c * 16, -256 + r * 16, -240 + c * 16, -240 + r * 16, 0f));
            return pieces;
        }

        [Fact]
        public void FlatGroundIsOnePlateAndACanopyAboveItIsDropped()
        {
            var pieces = Ground();
            pieces.Add(Flat(-256 + 5 * 16, -256 + 5 * 16, -240 + 5 * 16, -240 + 5 * 16, 40f));

            var plates = BdhcBuild.Squares(pieces);

            Assert.Single(plates);
            Assert.Equal(-256f, plates[0].MinX); Assert.Equal(256f, plates[0].MaxX);
            Assert.Equal(-256f, plates[0].MinZ); Assert.Equal(256f, plates[0].MaxZ);
        }

        [Fact]
        public void ABridgeOverWalkableSquaresKeepsItsOwnLevel()
        {
            var pieces = Ground();
            pieces.Add(Flat(-256 + 4 * 16, -256 + 10 * 16, -256 + 12 * 16, -256 + 11 * 16, 32f));

            var plates = BdhcBuild.Squares(pieces);
            byte[] made = BdhcBuild.From(plates, out string whynot);
            Assert.True(made != null, whynot);
            Assert.True(BdhcFile.TryParse(made, out var terrain));

            float rawX = 6.5f * 0.25f, rawZ = 10.5f * 0.25f;
            Assert.True(terrain.TryGetHeight(rawX, rawZ, 32f / 64f, out float top));
            Assert.True(terrain.TryGetHeight(rawX, rawZ, 0f, out float under));
            Assert.Equal(0.5f, top, 3);
            Assert.Equal(0f, under, 3);
            Assert.True(plates.Count <= 4, $"{plates.Count} plates for flat ground and one bridge.");
        }

        [Fact]
        public void WrittenPlatesReadBackWithTheirSpanAndHeight()
        {
            byte[] made = BdhcBuild.From(new[] { Flat(-32, -16, 48, 64, 24f) }, out string whynot);
            Assert.True(made != null, whynot);
            Assert.True(BdhcFile.TryParse(made, out var terrain));
            var plate = Assert.Single(terrain.Plates());
            Assert.Equal((-32f, -16f, 48f, 64f), (plate.MinX, plate.MinZ, plate.MaxX, plate.MaxZ));
            Assert.Equal(24f, plate.HeightAt(0f, 0f), 3);
        }

        [Fact]
        public void AProposalKeepsTheUsersPlatesAndBuildsNothingUnderThem()
        {
            var mine = Flat(-256 + 3 * 16, -256 + 3 * 16, -256 + 7 * 16, -256 + 5 * 16, 48f);
            mine.Mine = true;

            var plates = BdhcBuild.Squares(Ground(), mine: new[] { mine });

            var kept = Assert.Single(plates, p => p.Mine);
            Assert.Equal((mine.MinX, mine.MaxX, mine.MinZ, mine.MaxZ), (kept.MinX, kept.MaxX, kept.MinZ, kept.MaxZ));
            Assert.Equal(48f, kept.Height, 3);
            foreach (var p in plates.Where(p => !p.Mine))
                for (float x = p.MinX + 8; x < p.MaxX; x += 16)
                    for (float z = p.MinZ + 8; z < p.MaxZ; z += 16)
                        Assert.False(mine.Covers(x, z), $"A proposed plate covers the user's square at {x},{z}.");

            byte[] made = BdhcBuild.From(plates, out string whynot);
            Assert.True(made != null, whynot);
            Assert.True(BdhcFile.TryParse(made, out var terrain));
            Assert.True(terrain.TryGetHeight(4.5f * 0.25f, 3.5f * 0.25f, 0f, out float y));
            Assert.Equal(48f / 64f, y, 3);
        }

        [Fact]
        public void TiltSetsHeightAtTheCentreAndRisePerSquare()
        {
            var plate = Flat(-32, -32, 32, 32, 0f);
            plate.Tilt(16f, 8f, -4f);

            Assert.Equal(16f, plate.Height, 3);
            Assert.Equal(8f, plate.SlopeX, 3);
            Assert.Equal(-4f, plate.SlopeZ, 3);
            Assert.Equal(16f + 8f, plate.HeightAtOrZero(16f, 0f), 3);
            Assert.Equal(16f + 4f, plate.HeightAtOrZero(0f, -16f), 3);
        }

        [Fact]
        public void UpperLevelsOverBlockedSquaresAreDropped()
        {
            var pieces = Ground();
            pieces.Add(Flat(-256 + 4 * 16, -256 + 10 * 16, -256 + 12 * 16, -256 + 11 * 16, 32f));

            var plates = BdhcBuild.Squares(pieces, (c, r) => r == 10);

            Assert.Single(plates);
        }

        [SkippableTheory]
        [InlineData(false)]
        [InlineData(true)]
        public void SquarePlatesMatchTheShippedGroundWithFarFewerPlates(bool hgss)
        {
            string project = hgss ? TestRoms.HeartGold : TestRoms.Platinum;
            Skip.If(!Directory.Exists(project), "Test project not configured");

            int maps = 0, shippedPlates = 0, oldPlates = 0, newPlates = 0;
            long asked = 0, agreed = 0, missing = 0;
            string maps0 = Path.Combine(project, "unpacked", "maps");
            foreach (var (name, model, terrain) in MapsWithTerrain(project, hgss).Take(60))
            {
                if (!BdhcFile.TryParse(terrain, out var shipped)) continue;
                var mesh = MapMesh.Read(model, out _);
                if (mesh == null) continue;
                var map = new MapFile(new MemoryStream(File.ReadAllBytes(Path.Combine(maps0, name))),
                                      hgss ? RomInfo.GameFamilies.HGSS : RomInfo.GameFamilies.Plat, showMessages: false);

                var pieces = BdhcBuild.PiecesOf(mesh, mesh.ModelScale);
                if (pieces.Count == 0) continue;
                byte[] old = BdhcBuild.From(pieces, out _);
                byte[] made = BdhcBuild.ForMap(mesh, map.collisions, terrain, out string whynot);
                Assert.True(made != null, $"{name}: {whynot}");
                Assert.True(BdhcFile.TryParse(made, out var mine), $"{name}: not readable.");

                shippedPlates += BitConverter.ToUInt16(terrain, 10);
                oldPlates += BitConverter.ToUInt16(old, 10);
                newPlates += BitConverter.ToUInt16(made, 10);

                for (int tz = 0; tz < 32; tz++)
                    for (int tx = 0; tx < 32; tx++)
                    {
                        if ((map.collisions[tz, tx] & 0x80) != 0) continue;
                        float rawX = (tx + 0.5f) * 0.25f, rawZ = (tz + 0.5f) * 0.25f;
                        if (!shipped.TryGetHeight(rawX, rawZ, 0f, out float wasY)) continue;
                        asked++;
                        if (!mine.TryGetHeight(rawX, rawZ, wasY, out float nowY)) { missing++; continue; }
                        if (Math.Abs(wasY - nowY) < 0.01f) agreed++;
                    }
                maps++;
            }

            Assert.True(maps > 0 && asked > 0, "No map was compared, so this proved nothing.");
            _out.WriteLine($"{maps} maps: shipped {shippedPlates} plates, every face {oldPlates}, squares {newPlates}; "
                         + $"{agreed} of {asked} walkable squares at the shipped height, {missing} missing.");
            Assert.True(newPlates * 3 < oldPlates, $"{newPlates} plates is not far fewer than {oldPlates}.");
            Assert.True(agreed > asked * 0.7, $"Only {agreed} of {asked} walkable squares match.");
            Assert.True(missing < asked / 1000 + 1, $"{missing} walkable squares have no ground.");
        }

        private static MapMesh FlatAndUpright()
        {
            var mesh = (MapMesh)Activator.CreateInstance(typeof(MapMesh), nonPublic: true);

            foreach (var (x, y, z) in new[] { (0f, 0f, 0f), (1f, 0f, 0f), (1f, 0f, 1f), (0f, 0f, 1f) })
                mesh.Vertices.Add(new MapMesh.Vertex { X = x, Y = y, Z = z });
            mesh.Faces.Add(new MapMesh.Face { Corners = new[] { 0, 1, 2, 3 }, Material = -1 });

            foreach (var (x, y, z) in new[] { (0f, 0f, 0f), (1f, 0f, 0f), (1f, 1f, 0f), (0f, 1f, 0f) })
                mesh.Vertices.Add(new MapMesh.Vertex { X = x, Y = y, Z = z });
            mesh.Faces.Add(new MapMesh.Face { Corners = new[] { 4, 5, 6, 7 }, Material = -1 });

            return mesh;
        }
    }
}

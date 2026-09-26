using DSPRE.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests.Models
{
    public class PdstsFileTests
    {
        private readonly ITestOutputHelper _out;
        public PdstsFileTests(ITestOutputHelper o) { _out = o; }

        private static IEnumerable<string> Files()
        {
            foreach (string root in new[]
            {
                Environment.GetEnvironmentVariable("DSPRE_TEST_PDSMS"),
                Path.Combine(Path.GetTempPath(), "claude", "C--Romhacking-Tooling-DSPRE"),
            })
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                foreach (string path in Directory.EnumerateFiles(root, "*.pdsts", SearchOption.AllDirectories))
                    yield return path;
            }
        }

        [SkippableFact]
        public void EveryTilesetIsReadRightThroughWithoutGuessingAtALength()
        {
            var files = Files().ToList();
            Skip.If(files.Count == 0, "No .pdsts tilesets are available to read.");

            int read = 0, refused = 0, onOneSquare = 0, sitsOnIt = 0;
            var outside = new Dictionary<string, int>();
            var nearby = new Dictionary<string, int>();
            long tiles = 0, faces = 0, spreading = 0;
            var whyRefused = new List<string>();

            foreach (string path in files)
            {
                var set = PdstsFile.Read(path, out string whynot);
                if (set == null)
                {
                    refused++;
                    if (whyRefused.Count < 6) whyRefused.Add($"{Path.GetFileName(path)}: {whynot}");
                    continue;
                }

                read++;
                tiles += set.Tiles.Count;
                faces += set.Tiles.Sum(t => t.FaceCount);
                spreading += set.Tiles.Count(t => t.Spreads);

                foreach (var tile in set.Tiles)
                {
                    Assert.True(tile.Faces.Count > 0, $"{Path.GetFileName(path)}: a tile draws nothing.");
                    Assert.InRange(tile.Wide, 1, MapTileset.MostSquares);
                    Assert.InRange(tile.Deep, 1, MapTileset.MostSquares);

                    foreach (var face in tile.Faces)
                    {
                        Assert.True(face.Corners.Length == 3 || face.Corners.Length == 4,
                            $"{Path.GetFileName(path)}: a face has {face.Corners.Length} corners.");
                        foreach (int c in face.Corners)
                            Assert.InRange(c, 0, tile.Corners.Count - 1);
                    }

                    bool flat = tile.Corners.Max(c => c.Y) - tile.Corners.Min(c => c.Y) < 1e-3f;
                    if (flat && tile.Wide == 1 && tile.Deep == 1)
                    {
                        onOneSquare++;
                        float lowX = tile.Corners.Min(c => c.X), highX = tile.Corners.Max(c => c.X);
                        float lowZ = tile.Corners.Min(c => c.Z), highZ = tile.Corners.Max(c => c.Z);
                        bool inside = lowX > -0.01f && highX < 0.26f && lowZ > -0.01f && highZ < 0.26f;
                        if (inside) sitsOnIt++;
                        else outside[Path.GetFileName(path)] = outside.GetValueOrDefault(Path.GetFileName(path)) + 1;
                        bool near = lowX > -0.13f && highX < 0.38f && lowZ > -0.13f && highZ < 0.38f;
                        if (!near) nearby[Path.GetFileName(path)] = nearby.GetValueOrDefault(Path.GetFileName(path)) + 1;
                    }
                }
            }

            foreach (string s in whyRefused) _out.WriteLine("  " + s);
            _out.WriteLine($"{read} tilesets read, {refused} refused, {tiles} tiles, {faces} faces, "
                         + $"{spreading} covering more than one square.");

            Assert.True(read > 0, "Not one tileset could be read, so this proved nothing.");
            Assert.Equal(0, refused);

            Assert.True(onOneSquare > 0, "No flat tile of one square was found to check the placing with.");
            foreach (var kv in outside) _out.WriteLine($"  {kv.Key}: {kv.Value} flat one-square tiles nudged off their square");

            Assert.True(sitsOnIt > onOneSquare * 0.95,
                $"Only {sitsOnIt} of {onOneSquare} flat one-square tiles sit on their own square.");
            foreach (var kv in nearby) _out.WriteLine($"  {kv.Key}: {kv.Value} drawn more than half a square away");
            _out.WriteLine($"{sitsOnIt} of {onOneSquare} flat one-square tiles sit on their square, the rest beside it.");
        }

        [SkippableFact]
        public void TilesKnowWhichPicturesTheyNeedAndWhichColoursThoseAreDrawnWith()
        {
            var files = Files().ToList();
            Skip.If(files.Count == 0, "No .pdsts tilesets are available to read.");

            int checkedSets = 0;
            long named = 0, unnamed = 0, differ = 0;

            foreach (string path in files)
            {
                var set = PdstsFile.Read(path, out _);
                if (set == null) continue;

                foreach (var tile in set.Tiles)
                    foreach (var face in tile.Faces)
                    {
                        if (string.IsNullOrEmpty(face.Picture)) unnamed++; else named++;
                        if (!string.IsNullOrEmpty(face.Palette) && face.Palette != face.Picture) differ++;
                    }

                checkedSets++;
            }

            Assert.True(checkedSets > 0, "No tileset was read, so this proved nothing.");

            Assert.True(named > unnamed,
                $"{unnamed} faces name no picture and only {named} do.");
            _out.WriteLine($"{checkedSets} tilesets: {named} faces name a picture, {unnamed} do not, "
                         + $"{differ} name colours called something else.");
        }

        [SkippableFact]
        public void PlacesOnAPictureAreTurnedIntoItsDotsOnceTheSizesAreKnown()
        {
            var files = Files().ToList();
            Skip.If(files.Count == 0, "No .pdsts tilesets are available to read.");

            var set = PdstsFile.Read(files.First(), out string whynot);
            Assert.True(set != null, whynot);
            Assert.True(set.PlacesArePartsOfThePicture,
                        "A set from outside holds places as a fraction until they are turned into dots.");

            var was = set.Tiles[0].Corners.Select(c => (c.S, c.T)).ToList();
            set.TurnPlacesIntoDots(_ => (64, 32));

            Assert.False(set.PlacesArePartsOfThePicture);
            for (int i = 0; i < was.Count; i++)
            {
                Assert.Equal(was[i].S * 64f, set.Tiles[0].Corners[i].S, 3);
                Assert.Equal(was[i].T * 32f, set.Tiles[0].Corners[i].T, 3);
            }

            set.TurnPlacesIntoDots(_ => (64, 32));
            Assert.Equal(was[0].S * 64f, set.Tiles[0].Corners[0].S, 3);
        }

        [SkippableFact]
        public void ATilesetBringsThePicturesItNamesAlongWithIt()
        {
            var files = Files().ToList();
            Skip.If(files.Count == 0, "No .pdsts tilesets are available to read.");

            int sets = 0;
            long named = 0, found = 0;

            foreach (string path in files)
            {
                var set = PdstsFile.Read(path, out _);
                if (set == null) continue;

                var wanted = set.Tiles.SelectMany(t => t.Pictures)
                                      .Where(p => !string.IsNullOrEmpty(p))
                                      .Distinct().ToList();
                named += wanted.Count;
                found += wanted.Count(p => set.PictureFiles.ContainsKey(p));
                sets++;
            }

            Assert.True(sets > 0, "No tileset was read, so this proved nothing.");

            Assert.True(found > named * 0.9,
                $"Only {found} of {named} named pictures were found beside their tilesets.");
            _out.WriteLine($"{sets} tilesets: {found} of {named} named pictures found on disk.");
        }

        [SkippableFact]
        public void TilesComeWithHowTheirMaterialsAreDrawnAndTheirLights()
        {
            var files = Files().ToList();
            Skip.If(files.Count == 0, "No .pdsts tilesets are available to read.");

            long faces = 0, withLook = 0, litCorners = 0, litFacing = 0, colouredCorners = 0;
            var looks = new HashSet<string>();
            foreach (string path in files)
            {
                var set = PdstsFile.Read(path, out _);
                if (set == null) continue;
                foreach (var tile in set.Tiles)
                    foreach (var face in tile.Faces)
                    {
                        faces++;
                        if (face.Look == null) continue;
                        withLook++;
                        looks.Add(face.Look.Key);
                        foreach (int c in face.Corners)
                        {
                            var corner = tile.Corners[c];
                            if (face.Look.Lights != 0) { litCorners++; if (corner.Faces) litFacing++; }
                            if (corner.Colour >= 0) colouredCorners++;
                        }
                    }
            }

            _out.WriteLine($"{faces} faces, {withLook} with a look, {looks.Count} different looks; "
                         + $"{litFacing} of {litCorners} lit corners say which way they face, "
                         + $"{colouredCorners} corners carry their own colour.");
            Assert.True(faces > 0, "No tile was read, so this proved nothing.");
            Assert.Equal(faces, withLook);
            Assert.True(looks.Count > 1, "Every material came out the same, so the settings were not read.");

            Assert.True(litFacing > litCorners * 0.95, $"Only {litFacing} of {litCorners} lit corners face a way.");
        }

        [SkippableFact]
        public void SmartDrawingsComeAlongPointingAtTilesOfTheSet()
        {
            var files = Files().ToList();
            Skip.If(files.Count == 0, "No .pdsts tilesets are available to read.");

            int sets = 0, drawings = 0, slots = 0, oneSquare = 0;
            foreach (string path in files)
            {
                var set = PdstsFile.Read(path, out _);
                if (set == null) continue;
                sets++;

                foreach (var drawing in set.SmartDrawings)
                {
                    drawings++;
                    for (int slot = 0; slot < SmartDrawing.Slots; slot++)
                    {
                        int tile = drawing[slot];
                        if (tile < 0) continue;
                        Assert.InRange(tile, 0, set.Tiles.Count - 1);
                        slots++;
                        if (!set.Tiles[tile].Spreads) oneSquare++;
                    }
                }
            }

            _out.WriteLine($"{sets} tilesets: {drawings} smart drawings, {slots} slots filled, "
                         + $"{oneSquare} of them with a tile of one square.");
            Skip.If(drawings == 0, "None of the tilesets carries a smart drawing.");

            Assert.True(oneSquare > slots * 0.9, $"Only {oneSquare} of {slots} filled slots hold a tile of one square.");
        }

        [SkippableFact]
        public void EveryPictureAndColoursATileAsksForIsFoundInTheBundleMadeFromItsSet()
        {
            var files = Files().ToList();
            Skip.If(files.Count == 0, "No .pdsts tilesets are available to read.");

            string path = files.FirstOrDefault(f => f.Contains("Jiboule")) ?? files.First();
            var set = PdstsFile.Read(path, out string whynot);
            Assert.True(set != null, whynot);

            var pictures = new List<DsTexture>();
            foreach (var kv in set.PictureFiles)
            {
                if (!DSPRE.Avalonia.AnyPng.TryReadRgba(File.ReadAllBytes(kv.Value), out byte[] rgba, out int w, out int h, out _)) continue;
                var t = DsTexture.From(rgba, w, h, kv.Key);
                t.PaletteNames = set.Tiles.SelectMany(x => x.Faces).Where(f => f.Picture == kv.Key && !string.IsNullOrEmpty(f.Palette))
                                          .Select(f => f.Palette).Prepend(kv.Key).Distinct().ToList();
                pictures.Add(t);
            }
            var made = NsbtxWriter.Build(pictures);
            Assert.True(made.Whynot == null, made.Whynot);

            using var stream = new MemoryStream(made.Bytes);
            global::LibNDSFormats.NSBTX.NSBTXLoader.LoadNsbtx(stream, out var texs, out var pals);
            var texNames = new HashSet<string>(texs.Select(t => t.texname));
            var palNames = new HashSet<string>(pals.Select(p => p.palname));

            var asked = set.Tiles.SelectMany(t => t.Faces).Where(f => set.PictureFiles.ContainsKey(f.Picture))
                                 .Select(f => (f.Picture, f.Palette)).Distinct().ToList();
            Assert.NotEmpty(asked);
            Assert.All(asked, a => Assert.Contains(a.Picture, texNames));
            Assert.All(asked.Where(a => !string.IsNullOrEmpty(a.Palette)), a => Assert.Contains(a.Palette, palNames));
            _out.WriteLine($"{Path.GetFileName(path)}: {asked.Count} picture and colour pairs, all found in a bundle of {texNames.Count} pictures and {palNames.Count} colour sets.");
        }

        [SkippableFact]
        public void ATilesetsPicturesBecomeABundleTheGameCanRead()
        {
            var files = Files().ToList();
            Skip.If(files.Count == 0, "No .pdsts tilesets are available to read.");

            foreach (string path in files)
            {
                var set = PdstsFile.Read(path, out _);
                if (set == null || set.PictureFiles.Count == 0) continue;

                var pictures = new List<DsTexture>();
                foreach (var kv in set.PictureFiles.Take(20))
                {
                    if (!DSPRE.Avalonia.AnyPng.TryReadRgba(File.ReadAllBytes(kv.Value),
                            out byte[] rgba, out int w, out int h, out _)) continue;
                    pictures.Add(DsTexture.From(rgba, w, h, kv.Key));
                }
                if (pictures.Count == 0) continue;

                var made = NsbtxWriter.Build(pictures);
                Assert.True(made.Whynot == null, $"{Path.GetFileName(path)}: {made.Whynot}");

                using var stream = new MemoryStream(made.Bytes);
                global::LibNDSFormats.NSBTX.NSBTXLoader.LoadNsbtx(stream, out var back, out _);
                Assert.NotNull(back);

                foreach (var picture in pictures)
                    Assert.Contains(picture.Name, back.Select(t => t.texname));

                _out.WriteLine($"{Path.GetFileName(path)}: {pictures.Count} of its own pictures "
                             + $"became a {made.Bytes.Length:n0} byte bundle the reader accepts.");
                return;
            }

            Assert.Fail("No tileset had pictures on disk, so this proved nothing.");
        }

    }
}

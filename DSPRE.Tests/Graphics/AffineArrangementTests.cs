using System;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>Plain affine background arrangements store one byte per square, a tile number only.</summary>
    [Collection("rom")]
    public class AffineArrangementTests
    {
        private readonly ITestOutputHelper _out;
        public AffineArrangementTests(ITestOutputHelper output) { _out = output; }

        private static void Load(string code, string path, string game)
        {
            Skip.IfNot(Directory.Exists(path), $"The {game} test ROM project is not available.");
            new RomInfo(code, path);
            GraphicAssets.Forget();
        }

        [SkippableFact] public void DiamondAffineScreensHoldOneByteASquare() => OneByteASquare("ADAE", TestRoms.Diamond, "Diamond");
        [SkippableFact] public void PlatinumAffineScreensHoldOneByteASquare() => OneByteASquare("CPUE", TestRoms.Platinum, "Platinum");

        private void OneByteASquare(string code, string path, string game)
        {
            Load(code, path, game);
            int affine = 0, textual = 0;
            foreach (var archive in GraphicAssets.All)
            {
                var narc = new ScriptNarc(archive.Dir);
                if (!narc.Available) continue;
                for (int i = 0; i < narc.Count; i++)
                {
                    byte[] data;
                    try { data = GraphicAssets.Unsqueeze(narc.Get(i)); }
                    catch { continue; }
                    if (GraphicAssets.Identify(data) != GraphicAssets.Kind.TileMap || data.Length < 0x24) continue;
                    int squares = NitroBgCodec.SquareCount(NitroBgCodec.U16(data, 0x18) / 8, NitroBgCodec.U16(data, 0x1a) / 8);
                    int stored = NitroBgCodec.U32(data, 0x20);
                    if (NitroBgCodec.EntryBytes(data) == 1)
                    {
                        affine++;
                        Assert.True(stored == squares, $"{game} {archive.Title}[{i}]: {stored} bytes for {squares} squares");
                    }
                    else textual++;
                }
            }
            _out.WriteLine($"{game}: {affine} affine arrangements, {textual} others");
            Assert.True(affine >= 3, $"{game}: only {affine} affine arrangements were found");
        }

        [SkippableFact] public void DiamondCardArrangementsNameOnlyTilesTheDrawingHas() => CardTiles("ADAE", TestRoms.Diamond, "Diamond");
        [SkippableFact] public void PlatinumCardArrangementsNameOnlyTilesTheDrawingHas() => CardTiles("CPUE", TestRoms.Platinum, "Platinum");

        private void CardTiles(string code, string path, string game)
        {
            Load(code, path, game);
            var narc = new ScriptNarc(RomInfo.DirNames.trainerCardGraphics);
            Assert.True(narc.Available, $"{game}: the trainer card archive is not available");
            var card = RomInfo.TrainerCardMembers;
            var pose = RomInfo.TrainerCardTrainerMembers;

            void Check(int drawing, int arrangement, int wantBytes)
            {
                byte[] chr = NitroBgCodec.Inflate(narc.Get(drawing));
                byte[] scr = NitroBgCodec.Inflate(narc.Get(arrangement));
                int room = NitroBgCodec.TileRoom(chr);
                var (w, h, mapAt) = NitroBgCodec.ReadScreenHeader(scr);
                int bytes = NitroBgCodec.EntryBytes(scr);
                Assert.Equal(wantBytes, bytes);

                int read = 0, highest = -1;
                for (int square = 0; square < NitroBgCodec.SquareCount(w / 8, h / 8); square++)
                {
                    int e = NitroBgCodec.EntryAt(scr, mapAt, square, bytes);
                    if (e < 0) continue;
                    read++;
                    highest = System.Math.Max(highest, e & 0x3FF);
                }
                _out.WriteLine($"{game}: arrangement {arrangement} reads {read} squares, highest tile {highest} of {room}");
                Assert.True(read > 0, $"{game}: arrangement {arrangement} gave no squares");
                Assert.True(highest < room, $"{game}: arrangement {arrangement} names tile {highest} but drawing {drawing} holds {room}");
            }

            Check(card.ncgr, card.facaNscr, 1);
            Check(card.ncgr, card.backNscr, 1);
            Check(pose.ncgr, pose.maleNscr, 2);
            Check(pose.ncgr, pose.femaleNscr, 2);

            var front = new TrainerCardGraphics().ComposeCardFront(0);
            Assert.NotNull(front);

            // The browser must pair the arrangement with the card's own drawing and colours.
            var archive = GraphicAssets.All.First(x => x.Dir == RomInfo.DirNames.trainerCardGraphics);
            var shown = GraphicAssets.Render(archive, card.facaNscr);
            Assert.True(shown.Rgba != null, $"{game}: the browser could not show the card front: {shown.Whynot}");
            Assert.Equal(front.Width, shown.Width);
            int compared = 0, differ = 0;
            for (int y = 0; y < System.Math.Min(front.Height, shown.Height); y++)
                for (int x = 0; x < front.Width; x++)
                {
                    int s = (y * shown.Width + x) * 4, f = (y * front.Width + x) * 4;
                    if (shown.Rgba[s + 3] == 0) continue;
                    compared++;
                    if (shown.Rgba[s] != front.Bgra[f + 2] || shown.Rgba[s + 1] != front.Bgra[f + 1] || shown.Rgba[s + 2] != front.Bgra[f]) differ++;
                }
            _out.WriteLine($"{game}: browser and card editor compared on {compared} pixels, {differ} differ");
            Assert.True(compared > 1000, $"{game}: the browser's card front drew only {compared} pixels");
            Assert.Equal(0, differ);
        }

        [SkippableFact] public void DiamondCardTakesItsOwnFrontBack() => CardRoundTrip("ADAE", TestRoms.Diamond, "Diamond");
        [SkippableFact] public void PlatinumCardTakesItsOwnFrontBack() => CardRoundTrip("CPUE", TestRoms.Platinum, "Platinum");

        /// <summary>Importing the card's own front gives the same card again or refuses with a message.</summary>
        private void CardRoundTrip(string code, string path, string game)
        {
            Load(code, path, game);
            var card = new TrainerCardGraphics();
            Assert.True(card.Available, $"{game}: the trainer card archive is not available");

            var front = card.ComposeCardFront(0);
            Assert.NotNull(front);
            var picture = new RawImage(TrainerCardGraphics.CardWidth, TrainerCardGraphics.CardHeight);
            for (int y = 0; y < picture.Height; y++)
                Array.Copy(front.Bgra, y * front.Width * 4, picture.Bgra, y * picture.Width * 4, picture.Width * 4);

            try
            {
                string refused = card.ImportCardFront(picture);
                _out.WriteLine($"{game}: import says {refused ?? "done"}");
                if (refused != null)
                {
                    Assert.False(string.IsNullOrWhiteSpace(refused));
                    return;
                }

                var again = card.ComposeCardFront(0);
                int differ = 0;
                for (int y = 0; y < picture.Height; y++)
                    for (int x = 0; x < picture.Width; x++)
                    {
                        int a = (y * again.Width + x) * 4, b = (y * picture.Width + x) * 4;
                        if (again.Bgra[a] != picture.Bgra[b] || again.Bgra[a + 1] != picture.Bgra[b + 1] || again.Bgra[a + 2] != picture.Bgra[b + 2]) differ++;
                    }
                _out.WriteLine($"{game}: {differ} pixels differ after the round trip");
                Assert.Equal(0, differ);
            }
            finally { card.RevertAll(); }
        }

        [SkippableFact]
        public void AnAffineArrangementWritesBackUnchangedAndRefusesATileItCannotName()
        {
            Load("ADAE", TestRoms.Diamond, "Diamond");
            var narc = new ScriptNarc(RomInfo.DirNames.trainerCardGraphics);
            Assert.True(narc.Available, "Diamond: the trainer card archive is not available");
            byte[] original = NitroBgCodec.Inflate(narc.Get(RomInfo.TrainerCardMembers.facaNscr));
            byte[] copy = (byte[])original.Clone();
            var (w, h, mapAt) = NitroBgCodec.ReadScreenHeader(copy);
            int squares = NitroBgCodec.SquareCount(w / 8, h / 8);

            for (int square = 0; square < squares; square++)
                Assert.True(NitroBgCodec.PutEntry(copy, mapAt, square, 1, NitroBgCodec.EntryAt(original, mapAt, square, 1)));
            Assert.True(original.SequenceEqual(copy));

            Assert.False(NitroBgCodec.PutEntry(copy, mapAt, 0, 1, 256));
            Assert.False(NitroBgCodec.PutEntry(copy, mapAt, squares, 1, 0));
            Assert.True(original.SequenceEqual(copy));
        }
    }
}

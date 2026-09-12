using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// Cell files are the other half of moving a sprite, so the writer is held to the same standard as the
    /// animation one: every cell file in both games comes back byte for byte, and an edit touches only the
    /// bytes that hold the position.
    /// </summary>
    [Collection("rom")]
    public class NcerRoundTripTests
    {
        private readonly ITestOutputHelper _out;
        public NcerRoundTripTests(ITestOutputHelper output) => _out = output;

        private static bool Open(string project, string id)
        {
            if (!Directory.Exists(project)) return false;
            try { new RomInfo(id, project); } catch { return false; }
            return true;
        }

        private static IEnumerable<string> EveryArchive(string project)
        {
            string files = Path.Combine(project, "files");
            if (!Directory.Exists(files)) yield break;
            foreach (string path in Directory.EnumerateFiles(files, "*", SearchOption.AllDirectories))
            {
                byte[] head;
                try
                {
                    using var s = File.OpenRead(path);
                    head = new byte[4];
                    if (s.Read(head, 0, 4) < 4) continue;
                }
                catch { continue; }
                if (head[0] == 'N' && head[1] == 'A' && head[2] == 'R' && head[3] == 'C') yield return path;
            }
        }

        private void Sweep(string project, string id, string game)
        {
            Skip.If(!Open(project, id), $"{game} is not unpacked here");

            int cellFiles = 0, cells = 0, pieces = 0, extended = 0, named = 0;
            var broken = new List<string>();

            foreach (string path in EveryArchive(project))
            {
                NarcAPI.Narc narc;
                try { narc = NarcAPI.Narc.Open(path); } catch { continue; }
                if (narc == null) continue;

                for (int i = 0; i < narc.ElementCount; i++)
                {
                    byte[] raw;
                    try { raw = NitroBgCodec.Inflate(narc.GetElementBytes(i)); }
                    catch { continue; }
                    if (raw == null || raw.Length < 0x20) continue;
                    if (raw[0] != 'R' || raw[1] != 'E' || raw[2] != 'C' || raw[3] != 'N') continue;

                    var f = NcerFile.Read(raw);
                    if (f == null) { broken.Add($"{Path.GetFileName(path)}#{i} would not read"); continue; }

                    cellFiles++;
                    cells += f.Cells.Count;
                    pieces += f.PieceCount;
                    if (f.Extended) extended++;
                    if (f.Cells.Any(c => !string.IsNullOrEmpty(c.Name))) named++;

                    byte[] back = f.Write();
                    if (!back.SequenceEqual(raw))
                    {
                        int at = 0;
                        while (at < Math.Min(back.Length, raw.Length) && back[at] == raw[at]) at++;
                        broken.Add($"{Path.GetFileName(path)}#{i} differs at 0x{at:X}");
                    }
                }
            }

            _out.WriteLine($"{game}: {cellFiles} cell files, {cells} cells, {pieces} pieces, "
                         + $"{extended} of the extended kind, {named} with names");

            Assert.True(cellFiles > 0, "no cell files were found");
            Assert.True(pieces > 0, "no pieces were read");
            Assert.True(broken.Count == 0,
                $"{broken.Count} of {cellFiles} did not survive: {string.Join("; ", broken.Take(8))}");
        }

        [SkippableFact]
        public void EveryCellFileInPlatinumComesBackUnchanged()
            => Sweep(TestRoms.Platinum, "CPUE", "Platinum");

        [SkippableFact]
        public void EveryCellFileInHeartGoldComesBackUnchanged()
            => Sweep(TestRoms.HeartGold, "IPKE", "HeartGold");

        private static byte[] Member(int i)
            => NitroBgCodec.Inflate(new ScriptNarc(RomInfo.DirNames.poketch).Get(i));

        /// <summary>
        /// Moving a piece changes the four bytes that hold its position and leaves the rest of the file
        /// alone, and putting it back leaves the file as it was found.
        /// </summary>
        [SkippableFact]
        public void MovingAPieceTouchesOnlyItsOwnPosition()
        {
            Skip.If(!Open(TestRoms.Platinum, "CPUE"), "Platinum is not unpacked here");

            // The Matchup Checker's layout, whose animation swims its pieces about the screen.
            byte[] raw = Member(72);
            var f = NcerFile.Read(raw);
            Assert.NotNull(f);

            int cell = f.Cells.FindIndex(c => c.Pieces.Count > 0);
            Assert.True(cell >= 0, "no cell in this file has any pieces");
            var piece = f.Cells[cell].Pieces[0];
            int wasX = piece.X, wasY = piece.Y;

            Assert.Null(f.Move(cell, 0, wasX + 3, wasY - 5));
            byte[] moved = f.Write();

            Assert.Equal(raw.Length, moved.Length);
            var differ = Enumerable.Range(0, raw.Length).Where(i => raw[i] != moved[i]).ToArray();
            Assert.True(differ.All(i => i >= piece.At && i < piece.At + 4),
                        $"bytes outside the piece changed: {string.Join(",", differ.Take(8).Select(i => "0x" + i.ToString("X")))}");

            // Read the moved file back: the new position is what the bytes now say.
            var again = NcerFile.Read(moved);
            Assert.NotNull(again);
            Assert.Equal(wasX + 3, again.Cells[cell].Pieces[0].X);
            Assert.Equal(wasY - 5, again.Cells[cell].Pieces[0].Y);

            // Everything else about the piece survived the patch.
            Assert.Equal(piece.Width, again.Cells[cell].Pieces[0].Width);
            Assert.Equal(piece.Height, again.Cells[cell].Pieces[0].Height);
            Assert.Equal(piece.Tile, again.Cells[cell].Pieces[0].Tile);
            Assert.Equal(piece.Palette, again.Cells[cell].Pieces[0].Palette);
            Assert.Equal(piece.FlipH, again.Cells[cell].Pieces[0].FlipH);
            Assert.Equal(piece.FlipV, again.Cells[cell].Pieces[0].FlipV);

            Assert.Null(again.Move(cell, 0, wasX, wasY));
            Assert.Equal(raw, again.Write());

            _out.WriteLine($"cell {cell} piece 0 was {piece.Width}x{piece.Height} at {wasX},{wasY}, "
                         + $"moved and put back over {differ.Length} bytes");
        }

        /// <summary>
        /// Every Pokétch animation and layout is kept squeezed down in the ROM, so saving one has to put it
        /// back that way. Each is written back through the same squeeze the editor uses and read again, then
        /// restored to exactly the bytes it was found as.
        /// </summary>
        [SkippableFact]
        public void EveryPoketchAnimationAndLayoutGoesBackSqueezed()
        {
            Skip.If(!Open(TestRoms.Platinum, "CPUE"), "Platinum is not unpacked here");

            var narc = new ScriptNarc(RomInfo.DirNames.poketch);
            Skip.If(!narc.Available, "Platinum has no Pokétch archive here");

            var wanted = new SortedSet<int>();
            foreach (var app in PoketchApps.All)
            {
                if (app.Animation >= 0) wanted.Add(app.Animation);
                if (app.Cells >= 0) wanted.Add(app.Cells);
            }
            Assert.True(wanted.Count > 0, "no application names an animation or a layout");

            int squeezed = 0, provedBack = 0;
            var trouble = new List<string>();
            var restore = new Dictionary<int, byte[]>();
            try
            {
                foreach (int at in wanted)
                {
                    byte[] stored = narc.Get(at);
                    if (stored == null) continue;

                    byte marker = GraphicAssets.SqueezeMarker(stored);
                    if (marker == 0) continue;
                    squeezed++;
                    if (marker != 0x10) { trouble.Add($"member {at} is squeezed as 0x{marker:X2}"); continue; }

                    byte[] plain = GraphicAssets.Unsqueeze(stored);
                    byte[] again = GraphicAssets.Squeeze(plain, marker);
                    if (again == null) { trouble.Add($"member {at} would not squeeze back"); continue; }

                    restore[at] = stored.ToArray();
                    narc.Put(at, again);

                    byte[] back = narc.Get(at);
                    Assert.True(GraphicAssets.SqueezeMarker(back) == 0x10,
                                $"member {at} was squeezed and came back plain");
                    Assert.Equal(plain, GraphicAssets.Unsqueeze(back));
                    provedBack++;
                }
            }
            finally { foreach (var kv in restore) narc.Put(kv.Key, kv.Value); }

            _out.WriteLine($"{squeezed} squeezed Pokétch animations and layouts, "
                         + $"{provedBack} written back squeezed and read again as the same bytes");

            Assert.True(squeezed > 0, "none of them were squeezed, so this proved nothing");
            Assert.True(trouble.Count == 0, string.Join("; ", trouble.Take(8)));
            Assert.Equal(squeezed, provedBack);
        }

        /// <summary>A position the fields cannot hold is refused, rather than wrapping to the far side.</summary>
        [SkippableFact]
        public void APositionTheFieldCannotHoldIsRefused()
        {
            Skip.If(!Open(TestRoms.Platinum, "CPUE"), "Platinum is not unpacked here");

            byte[] raw = Member(72);
            var f = NcerFile.Read(raw);
            Assert.NotNull(f);
            int cell = f.Cells.FindIndex(c => c.Pieces.Count > 0);
            Assert.True(cell >= 0);

            Assert.NotNull(f.Move(cell, 0, NcerFile.MaxX + 1, 0));
            Assert.NotNull(f.Move(cell, 0, 0, NcerFile.MinY - 1));
            Assert.NotNull(f.Move(cell, 99999, 0, 0));

            // A refusal writes nothing.
            Assert.Equal(raw, f.Write());
        }
    }
}

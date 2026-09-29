using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using Images;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>Trainer sprites taken apart into frames and put back together.</summary>
    [Collection("rom")]
    public class TrainerSpriteFramesTests
    {
        private readonly ITestOutputHelper _out;
        public TrainerSpriteFramesTests(ITestOutputHelper o) { _out = o; }

        public static IEnumerable<object[]> Games => new[]
        {
            new object[] { "CPUE", TestRoms.Platinum, "Platinum" },
            new object[] { "IPKE", TestRoms.HeartGold, "HeartGold" },
        };

        private static List<(DirNames Dir, int Id, string Folder)> Sprites(string code, string path)
        {
            new RomInfo(code, path);
            var dirs = new List<DirNames> { DirNames.trainerGraphics, DirNames.trainerBackGraphics };
            DSUtils.TryUnpackNarcs(dirs);
            var list = new List<(DirNames, int, string)>();
            foreach (var dir in dirs)
            {
                string folder = gameDirs[dir].unpackedDir;
                int files = RomFiles.Settled(folder).Length;
                for (int id = 0; TrainerGraphicsLayout.ScanEntry(id) < files; id++) list.Add((dir, id, folder));
            }
            return list;
        }

        private static byte[] Entry(string folder, int entry) => File.ReadAllBytes(Path.Combine(folder, entry.ToString("D4")));

        private static TrainerSpriteFrames Open(string folder, int id, out string why) =>
            TrainerSpriteFrames.Read(Entry(folder, TrainerGraphicsLayout.DrawingEntry(id)),
                                     Entry(folder, TrainerGraphicsLayout.CellsEntry(id)),
                                     Entry(folder, TrainerGraphicsLayout.AnimationEntry(id)), out why);

        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void EverySpriteComesBackByteForByte(string code, string path, string game)
        {
            Skip.If(!Directory.Exists(path), $"{game} is not unpacked here");
            int checkedCount = 0, frames = 0;
            foreach (var (dir, id, folder) in Sprites(code, path))
            {
                var s = Open(folder, id, out string why);
                Assert.True(s != null, $"{game} {dir} {id}: {why}");
                var (ncgr, ncer, nanr) = s.Write();
                Assert.Equal(Entry(folder, TrainerGraphicsLayout.DrawingEntry(id)), ncgr);
                Assert.Equal(Entry(folder, TrainerGraphicsLayout.CellsEntry(id)), ncer);
                Assert.Equal(Entry(folder, TrainerGraphicsLayout.AnimationEntry(id)), nanr);
                frames += s.FrameCount;
                checkedCount++;
            }
            _out.WriteLine($"{game}: {checkedCount} sprites, {frames} frames");
            Assert.True(checkedCount > 100);
        }

        // The existing renderer in Images is the reference for what a frame looks like.
        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void FramesDrawAsTheSpriteRendererDrawsThem(string code, string path, string game)
        {
            Skip.If(!Directory.Exists(path), $"{game} is not unpacked here");
            int compared = 0;
            foreach (var (dir, id, folder) in Sprites(code, path))
            {
                var s = Open(folder, id, out _);
                string N(int e) => Path.Combine(folder, e.ToString("D4"));
                int t = TrainerGraphicsLayout.DrawingEntry(id), p = TrainerGraphicsLayout.ColoursEntry(id), c = TrainerGraphicsLayout.CellsEntry(id);
                var pal = new NCLR(N(p), p, "p");
                var tile = new NCGR(N(t), t, "t");
                var cells = new NCER(N(c), c, "c");
                for (int f = 0; f < s.FrameCount; f++)
                {
                    var mine = s.Draw(f);
                    var theirs = cells.Get_RawImage(tile, pal, f, TrainerSpriteFrames.Canvas, TrainerSpriteFrames.Canvas, trans: true, currOAM: -1, draw_index: null);
                    for (int i = 0; i < mine.Length; i++)
                    {
                        bool drawn = theirs.Bgra[i * 4 + 3] != 0;
                        Assert.True(drawn == (mine[i] != 0), $"{game} {dir} {id} frame {f} pixel {i % 128},{i / 128}");
                        if (!drawn) continue;
                        var colour = pal.Palette[mine[i] >> 4][mine[i] & 0xF];
                        Assert.True(colour.R == theirs.Bgra[i * 4 + 2] && colour.G == theirs.Bgra[i * 4 + 1] && colour.B == theirs.Bgra[i * 4],
                            $"{game} {dir} {id} frame {f} pixel {i % 128},{i / 128} colour");
                    }
                    compared++;
                }
            }
            _out.WriteLine($"{game}: {compared} frames compared");
            Assert.True(compared > 100);
        }

        // A frames sheet written out and read straight back in, in the sheet's own colours, changes nothing.
        [SkippableTheory]
        [MemberData(nameof(Games))]
        public void AFramesSheetReadBackChangesNothing(string code, string path, string game)
        {
            Skip.If(!Directory.Exists(path), $"{game} is not unpacked here");
            int sheets = 0;
            foreach (var (dir, id, folder) in Sprites(code, path))
            {
                var s = Open(folder, id, out _);
                int p = TrainerGraphicsLayout.ColoursEntry(id);
                var pal = new NCLR(Path.Combine(folder, p.ToString("D4")), p, "p");
                var palettes = pal.Palette.Select(b => b.Select(c => (uint)c.ToArgb() & 0xFFFFFF).ToArray()).ToList();
                var row = Enumerable.Range(0, s.FrameCount).Select(f => new TrainerSpriteSheet.Cell(s.Draw(f))).ToList();
                var (px, colours, w, h) = TrainerSpriteSheet.Compose(new[] { row }, palettes);

                var sheet = TrainerSpriteSheet.Open(DSPRE.Avalonia.IndexedPng.Write(px, colours, w, h), out string why);
                Assert.True(sheet != null, why);
                Assert.Equal((1, s.FrameCount), (sheet.Rows, sheet.Columns));

                var fresh = Open(folder, id, out _);
                for (int f = 0; f < fresh.FrameCount; f++)
                {
                    var canvas = TrainerSpriteSheet.WithSheetColours(sheet.CellAt(0, f), fresh.PalettesOf(f), palettes.Count, $"frame {f}", out why);
                    Assert.True(canvas != null, $"{game} {dir} {id}: {why}");
                    Assert.Null(fresh.SetDrawing(f, canvas));
                }
                var (ncgr, ncer, nanr) = fresh.Write();
                Assert.Equal(Entry(folder, TrainerGraphicsLayout.DrawingEntry(id)), ncgr);
                Assert.Equal(Entry(folder, TrainerGraphicsLayout.CellsEntry(id)), ncer);
                sheets++;
            }
            _out.WriteLine($"{game}: {sheets} sheets read back");
            Assert.True(sheets > 100);
        }

        [SkippableFact]
        public void AddedAndRemovedFramesKeepTheFilesInStep()
        {
            Skip.If(!Directory.Exists(TestRoms.Platinum), "Platinum is not unpacked here");
            var (_, _, folder) = Sprites("CPUE", TestRoms.Platinum).First(x => x.Dir == DirNames.trainerGraphics);
            // Roark: seven frames and an intro.
            var s = Open(folder, 62, out string why);
            Assert.True(s != null, why);
            int before = s.FrameCount;
            var standing = s.Draw(0);

            // A copy of the standing pose moved right by ten fits by moving the window.
            var moved = new int[standing.Length];
            for (int y = 0; y < 128; y++)
                for (int x = 0; x + 10 < 128; x++) moved[y * 128 + x + 10] = standing[y * 128 + x];
            int added = s.AddFrame();
            Assert.Null(s.SetDrawing(added, moved));
            Assert.Null(s.SetSequence(1, new[] { new TrainerSpriteFrames.Step(0, 10), new TrainerSpriteFrames.Step(added, 20) }));

            var (ncgr, ncer, nanr) = s.Write();
            var back = TrainerSpriteFrames.Read(ncgr, ncer, nanr, out why);
            Assert.True(back != null, why);
            Assert.Equal(before + 1, back.FrameCount);
            Assert.Equal(moved, back.Draw(added));
            Assert.Equal(standing, back.Draw(0));
            Assert.Equal(new[] { 0, added }, back.StepsOf(1).Select(st => st.Frame));
            Assert.Equal(new[] { 10, 20 }, back.StepsOf(1).Select(st => st.Hold));

            // Taking frame 1 out moves every later frame down one and keeps their drawings.
            var third = back.Draw(2);
            back.RemoveFrames(new[] { 1 });
            Assert.Equal(before, back.FrameCount);
            Assert.Equal(third, back.Draw(1));
            Assert.Equal(moved, back.Draw(added - 1));
            Assert.Equal(new[] { 0, added - 1 }, back.StepsOf(1).Select(st => st.Frame));
            var again = back.Write();
            Assert.NotNull(TrainerSpriteFrames.Read(again.Ncgr, again.Ncer, again.Nanr, out why));
        }

        [SkippableFact]
        public void ASpriteWithoutAnIntroGainsOneThatPlaysOnce()
        {
            Skip.If(!Directory.Exists(TestRoms.Platinum), "Platinum is not unpacked here");
            var one = Sprites("CPUE", TestRoms.Platinum)
                .Where(x => x.Dir == DirNames.trainerGraphics)
                .Select(x => Open(x.Folder, x.Id, out _))
                .First(s => s.Animations.Sequences.Count == 1);

            var drawing = one.Draw(0);
            int f = one.AddFrame();
            Assert.Null(one.SetDrawing(f, drawing.Select(v => v == 0 ? 0 : 1).ToArray()));
            Assert.Null(one.SetSequence(1, new[] { new TrainerSpriteFrames.Step(f, 8), new TrainerSpriteFrames.Step(0, 8) }));

            var (ncgr, ncer, nanr) = one.Write();
            var back = TrainerSpriteFrames.Read(ncgr, ncer, nanr, out string why);
            Assert.True(back != null, why);
            Assert.Equal(2, back.Animations.Sequences.Count);
            Assert.Equal(1u, back.Animations.Sequences[1].PlayMode);
            // Off a four-byte boundary the battle hangs.
            Assert.Equal(0u, BitConverter.ToUInt32(nanr, 0x18 + 0x14) % 4);
            Assert.Equal(new[] { f, 0 }, back.StepsOf(1).Select(st => st.Frame));
        }
    }
}

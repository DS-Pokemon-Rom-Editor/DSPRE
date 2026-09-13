using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DSPRE;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels.Graphics;
using Xunit;
using Xunit.Abstractions;

namespace DSPRE.Tests
{
    /// <summary>
    /// Renders real animations out as frames so they can be looked at rather than described. Nothing here
    /// asserts how a picture should look: it draws through the same path the editor's preview uses and
    /// writes what came out, with a manifest saying how long each frame is held.
    ///
    /// It also draws the pairing fix both ways. The old rule took the nearest file of the right kind; the
    /// new one takes the nearest that actually fits. Showing both is the only way to see the difference.
    /// </summary>
    [Collection("rom")]
    public class CellAnimationShowcaseTests
    {
        private readonly ITestOutputHelper _out;
        public CellAnimationShowcaseTests(ITestOutputHelper output) => _out = output;

        private static string Where => Environment.GetEnvironmentVariable("DSPRE_SHOW_OUT");

        private sealed class Pick
        {
            public RomInfo.DirNames Dir;
            public int Animation;
            public string Name;
            /// <summary>Take frame 0 of this many sequences instead of one sequence's frames.</summary>
            public int AcrossSequences;
            /// <summary>Also draw it the way the old nearest-neighbour rule would have.</summary>
            public bool ShowOldPairing;
        }

        private static readonly Pick[] Platinum =
        {
            new Pick { Dir = RomInfo.DirNames.poketch, Animation = 73, Name = "matchup-checker" },
            new Pick { Dir = RomInfo.DirNames.poketch, Animation = 19, Name = "stopwatch" },
            new Pick { Dir = RomInfo.DirNames.poketch, Animation = 41, Name = "dowsing-machine" },
            new Pick { Dir = RomInfo.DirNames.poketch, Animation = 98, Name = "link-searcher" },
            new Pick { Dir = RomInfo.DirNames.poketch, Animation = 28, Name = "analog-watch", AcrossSequences = 60 },
            new Pick { Dir = RomInfo.DirNames.monIcons, Animation = 3, Name = "party-icon" },
            new Pick { Dir = RomInfo.DirNames.battleObj, Animation = 206, Name = "battle-object-206", ShowOldPairing = true },
            new Pick { Dir = RomInfo.DirNames.battleObj, Animation = 249, Name = "battle-object-249", ShowOldPairing = true },
            new Pick { Dir = RomInfo.DirNames.synthOverlay, Animation = 22, Name = "overlay-22", ShowOldPairing = true },
            new Pick { Dir = RomInfo.DirNames.battleObj, Animation = 252, Name = "battle-object-252" },
            // Trainer classes, five files each, the animation at 5n+3 drawing from the class drawing at 5n
            // rather than the second picture beside it. Class 101 holds twelve poses and class 65 ten, which
            // is what these animations really do; most classes hold one.
            new Pick { Dir = RomInfo.DirNames.trainerGraphics, Animation = 508, Name = "trainer-twelve-poses", ShowOldPairing = true },
            new Pick { Dir = RomInfo.DirNames.trainerGraphics, Animation = 328, Name = "trainer-ten-poses" },
        };

        private static readonly Pick[] HeartGold =
        {
            new Pick { Dir = RomInfo.DirNames.fieldTouchMenu, Animation = 5, Name = "touch-menu-icon", ShowOldPairing = true },
            new Pick { Dir = RomInfo.DirNames.battleObj, Animation = 342, Name = "battle-object-342", ShowOldPairing = true },
            new Pick { Dir = RomInfo.DirNames.battleObj, Animation = 183, Name = "battle-object-183" },
            new Pick { Dir = RomInfo.DirNames.monIcons, Animation = 3, Name = "party-icon" },
            new Pick { Dir = RomInfo.DirNames.trainerGraphics, Animation = 508, Name = "trainer-twelve-poses", ShowOldPairing = true },
            new Pick { Dir = RomInfo.DirNames.trainerGraphics, Animation = 488, Name = "trainer-six-poses" },
            new Pick { Dir = RomInfo.DirNames.trainerGraphics, Animation = 368, Name = "trainer-three-poses" },
            new Pick { Dir = RomInfo.DirNames.trainerBackGraphics, Animation = 3, Name = "trainer-back-3", ShowOldPairing = true },
        };

        private static bool Open(string project, string id)
        {
            if (!Directory.Exists(project)) return false;
            try { new RomInfo(id, project); GraphicAssets.Forget(); } catch { return false; }
            return true;
        }

        private static byte[] Member(ScriptNarc narc, int i)
        {
            try { return NitroBgCodec.Inflate(narc.Get(i)); } catch { return null; }
        }

        // The rule as it was: the nearest file of a kind, the one before preferred.
        private static int OldNearest(GraphicAssets.Kind[] kinds, int from, GraphicAssets.Kind want)
        {
            for (int step = 1; step < kinds.Length; step++)
            {
                int before = from - step;
                if (before >= 0 && kinds[before] == want) return before;
                int after = from + step;
                if (after < kinds.Length && kinds[after] == want) return after;
            }
            return -1;
        }

        private static GraphicAssets.Kind[] KindsOf(ScriptNarc narc)
        {
            var kinds = new GraphicAssets.Kind[narc.Count];
            for (int i = 0; i < kinds.Length; i++)
            {
                try { kinds[i] = GraphicAssets.Identify(Member(narc, i)); }
                catch { kinds[i] = GraphicAssets.Kind.Unknown; }
            }
            return kinds;
        }

        // Sprite memory holds a shared sheet first where a screen has one, and cells count across both.
        private static byte[] Sheet(ScriptNarc narc, RomInfo.DirNames dir, int sprites, int animation, int cells)
        {
            byte[] mine = sprites < 0 ? Array.Empty<byte>() : DsBgScreen.ReadCharacters(Member(narc, sprites));
            if (dir != RomInfo.DirNames.poketch) return mine;

            var app = PoketchApps.All.FirstOrDefault(a => a.Animation == animation || a.Cells == cells);
            int shared = app == null ? -1 : PoketchApps.SharedSheetFor(app);
            if (shared < 0) return mine;

            byte[] first = DsBgScreen.ReadCharacters(Member(narc, shared));
            if (first.Length == 0) return mine;
            var both = new byte[first.Length + mine.Length];
            first.CopyTo(both, 0);
            mine.CopyTo(both, first.Length);
            return both;
        }

        private static (int L, int T, int R, int B) Bounds(List<byte[]> frames)
        {
            int l = DsBgScreen.Width, t = DsBgScreen.Height, r = -1, b = -1;
            foreach (var rgba in frames)
                for (int y = 0; y < DsBgScreen.Height; y++)
                    for (int x = 0; x < DsBgScreen.Width; x++)
                        if (rgba[(y * DsBgScreen.Width + x) * 4 + 3] != 0)
                        {
                            if (x < l) l = x;
                            if (x > r) r = x;
                            if (y < t) t = y;
                            if (y > b) b = y;
                        }
            return (l, t, r, b);
        }

        private static byte[] Crop(byte[] rgba, int l, int t, int w, int h)
        {
            var cut = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                Buffer.BlockCopy(rgba, ((t + y) * DsBgScreen.Width + l) * 4, cut, y * w * 4, w * 4);
            return cut;
        }

        /// <summary>Draws one run and writes its frames, returning the manifest line or null.</summary>
        private string Draw(ScriptNarc narc, string folder, string name, RomInfo.DirNames dir,
                            int animation, int cells, int sprites, int palette, int across)
        {
            var file = NanrFile.Read(Member(narc, animation));
            if (file == null || cells < 0 || sprites < 0) return null;

            var banks = DsBgScreen.ReadCells(Member(narc, cells));
            byte[] chars = Sheet(narc, dir, sprites, animation, cells);
            ushort[] all = palette < 0 ? Array.Empty<ushort>() : DsBgScreen.ReadColours(Member(narc, palette));
            if (banks.Count == 0 || chars.Length == 0 || all.Length == 0) return null;
            ushort[] colours = DsBgScreen.Row(all, 0);

            // Either one sequence's frames, or the first frame of many sequences for the runs that store a
            // still per angle.
            var steps = new List<(int Seq, int Frame, int Hold)>();
            if (across > 0)
            {
                for (int s = 0; s < Math.Min(across, file.Sequences.Count); s++)
                    if (file.Sequences[s].Frames.Count > 0) steps.Add((s, 0, 2));
            }
            else
            {
                int pick = -1, longest = 0;
                for (int s = 0; s < file.Sequences.Count; s++)
                    if (file.Sequences[s].Frames.Count > longest)
                    { longest = file.Sequences[s].Frames.Count; pick = s; }
                if (pick < 0) return null;
                for (int f = 0; f < longest; f++)
                    steps.Add((pick, f, Math.Max(1, (int)file.Sequences[pick].Frames[f].Delay)));
            }
            if (steps.Count == 0) return null;

            var frames = new List<byte[]>();
            foreach (var (s, f, _) in steps)
            {
                var rgba = new byte[DsBgScreen.Width * DsBgScreen.Height * 4];
                int cell = file.CellOf(s, f);
                if (cell >= 0 && cell < banks.Count)
                {
                    var (sx, sy) = file.ShiftOf(s, f);
                    var (deg, kx, ky) = file.TurnOf(s, f);
                    DsBgScreen.DrawCellTurned(rgba, banks[cell], chars, _ => colours,
                                              DsBgScreen.Width / 2 + sx, DsBgScreen.Height / 2 + sy,
                                              deg, kx, ky);
                }
                frames.Add(rgba);
            }

            var (bl, bt, br, bb) = Bounds(frames);
            if (br < bl || bb < bt) return null;
            int pad = 6;
            bl = Math.Max(0, bl - pad); bt = Math.Max(0, bt - pad);
            br = Math.Min(DsBgScreen.Width - 1, br + pad); bb = Math.Min(DsBgScreen.Height - 1, bb + pad);
            int cw = br - bl + 1, ch = bb - bt + 1;

            Directory.CreateDirectory(Path.Combine(folder, name));
            for (int i = 0; i < frames.Count; i++)
                File.WriteAllBytes(Path.Combine(folder, name, $"{i:d3}.raw"), Crop(frames[i], bl, bt, cw, ch));

            string holds = string.Join(",", steps.Select(x => x.Hold.ToString(CultureInfo.InvariantCulture)));
            return $"{name}\t{cw}\t{ch}\t{frames.Count}\t{holds}\t{dir}\t{animation}\t{cells}\t{sprites}\t{palette}";
        }

        private void Render(string project, string id, string game, Pick[] picks)
        {
            Skip.If(!Open(project, id), $"{game} is not unpacked here");
            string root = Where;
            Skip.If(root == null, "DSPRE_SHOW_OUT is not set, so there is nowhere to write");

            string folder = Path.Combine(root, game);
            Directory.CreateDirectory(folder);
            var manifest = new List<string>();
            int drawn = 0;

            foreach (var pick in picks)
            {
                ScriptNarc narc;
                try { narc = new ScriptNarc(pick.Dir); } catch { continue; }
                if (!narc.Available) continue;

                var found = CellAnimationPickerViewModel.InArchive(pick.Dir)
                                                        .FirstOrDefault(f => f.Animation == pick.Animation);
                if (found == null) { _out.WriteLine($"{pick.Name}: not found"); continue; }

                // The Pokétch map knows these pairings from the games' own source, so it wins over a guess.
                // Its screens are painted with the theme colours rather than whatever palette sits nearby.
                int cells = found.Cells, sprites = found.Sprites, palette = found.Palette;
                if (pick.Dir == RomInfo.DirNames.poketch)
                {
                    var app = PoketchApps.All.FirstOrDefault(a => a.Animation == pick.Animation);
                    if (app != null)
                    {
                        if (app.Cells >= 0) cells = app.Cells;
                        if (app.Sprites >= 0) sprites = app.Sprites;
                        palette = PoketchApps.ColoursFor(sprites);
                    }
                }

                string line = Draw(narc, folder, pick.Name, pick.Dir, pick.Animation,
                                   cells, sprites, palette, pick.AcrossSequences);
                if (line == null) { _out.WriteLine($"{pick.Name}: nothing drawn"); continue; }
                manifest.Add(line);
                drawn++;

                if (!pick.ShowOldPairing) continue;

                // The same animation as the old rule would have paired it, for the comparison.
                var kinds = KindsOf(narc);
                int oldCells = OldNearest(kinds, pick.Animation, GraphicAssets.Kind.CellLayout);
                int oldSheet = OldNearest(kinds, pick.Animation, GraphicAssets.Kind.TileGraphic);
                if (oldCells == found.Cells && oldSheet == found.Sprites)
                {
                    _out.WriteLine($"{pick.Name}: the old rule picked the same files, no comparison to draw");
                    continue;
                }
                string was = Draw(narc, folder, pick.Name + "-old", pick.Dir, pick.Animation,
                                  oldCells, oldSheet, found.Palette, pick.AcrossSequences);
                _out.WriteLine($"{pick.Name}: old rule took layout {oldCells} sheet {oldSheet}, "
                             + $"now layout {found.Cells} sheet {found.Sprites}"
                             + (was == null ? " (old pairing drew nothing)" : ""));
                if (was != null) manifest.Add(was);
                else manifest.Add($"{pick.Name}-old\t0\t0\t0\t\t{pick.Dir}\t{pick.Animation}\t{oldCells}\t{oldSheet}\t{found.Palette}");
            }

            File.WriteAllText(Path.Combine(folder, "manifest.tsv"), string.Join("\n", manifest));
            foreach (var line in manifest) _out.WriteLine(line);
            Assert.True(drawn >= picks.Length - 2, $"{game}: only {drawn} of {picks.Length} drew anything");
        }

        [SkippableFact]
        public void DrawPlatinumAnimationsOut() => Render(TestRoms.Platinum, "CPUE", "Platinum", Platinum);

        [SkippableFact]
        public void DrawHeartGoldAnimationsOut() => Render(TestRoms.HeartGold, "IPKE", "HeartGold", HeartGold);
    }
}

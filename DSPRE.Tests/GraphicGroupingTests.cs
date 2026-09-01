using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using Xunit;
using Xunit.Abstractions;
using static DSPRE.RomInfo;

namespace DSPRE.Tests
{
    /// <summary>
    /// Grouping an archive's files into things loses none of them and invents none.
    ///
    /// This is the check that matters for grouping: a row that quietly swallows a file it should not own,
    /// or a file that belongs to nothing and so appears nowhere, both look fine on screen. Every archive
    /// is walked in every game and every file has to be reachable from exactly one row, except where a
    /// file is deliberately shared by many rows and is named as such.
    /// </summary>
    [Collection("rom")]
    public class GraphicGroupingTests
    {
        private readonly ITestOutputHelper _out;
        public GraphicGroupingTests(ITestOutputHelper o) { _out = o; }

        private const string Diamond =
            @"C:\Romhacking\ROMs\NDS\DP\Pokemon Diamond (v05) (U)(Legacy)\1015 - Pokemon Diamond (v05) (U)(Legacy)_DSPRE_contents";
        private const string Platinum =
            @"C:\Romhacking\ROMs\NDS\Plat\Pokemon - Platinum Version (USA) (Rev 1)\Pokemon - Platinum Version (USA) (Rev 1)_DSPRE_contents";
        private const string HeartGold = @"C:\Romhacking\ROMs\NDS\HGSS\HeartGold (USA)_DSPRE_contents";

        public static IEnumerable<object[]> Games => new[]
        {
            new object[] { "ADAE", Diamond, "Diamond" },
            new object[] { "CPUE", Platinum, "Platinum" },
            new object[] { "IPKE", HeartGold, "HeartGold" },
        };

        /// <summary>A drawing belongs to one thing and one thing only. Colours, layouts and timings are
        /// routinely shared: every battle backdrop uses one tilemap, and all twenty eight Unown forms are
        /// painted from the same two sets of colours. So sharing is checked only on the drawings, which is
        /// where a grouping mistake would actually show.</summary>
        private static bool MustBeExclusive(string partName)
            => !(partName.StartsWith("Colours", StringComparison.Ordinal)
                 || partName.StartsWith("Layout", StringComparison.Ordinal)
                 || partName.StartsWith("Their layout", StringComparison.Ordinal)
                 || partName.StartsWith("Arrangement", StringComparison.Ordinal)
                 || partName.StartsWith("Timing", StringComparison.Ordinal)
                 || partName.StartsWith("Shiny colours", StringComparison.Ordinal));

        [Theory]
        [MemberData(nameof(Games))]
        public void EveryFileBelongsToExactlyOneThing(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);
            GraphicAssets.Forget();

            int archives = 0, files = 0, rows = 0;
            var missing = new List<string>();
            var doubled = new List<string>();

            foreach (var a in GraphicAssets.All)
            {
                int n;
                try { n = GraphicAssets.Count(a); } catch { n = 0; }
                if (n == 0) continue;
                archives++;
                files += n;

                var units = GraphicAssets.Units(a, n);
                rows += units.Count;

                // Only pieces from this archive count towards covering it: a move effect's colours live in
                // another archive, and that archive is walked on its own turn.
                var owners = new Dictionary<int, List<string>>();
                foreach (var u in units)
                    foreach (var pt in u.Parts)
                    {
                        if (pt.Archive != null && pt.Archive.Dir != a.Dir) continue;
                        if (!owners.TryGetValue(pt.Index, out var who)) owners[pt.Index] = who = new List<string>();
                        if (MustBeExclusive(pt.Name)) who.Add(u.Name);
                        else if (who.Count == 0) who.Add("(shared) " + u.Name);
                    }

                for (int i = 0; i < n; i++)
                {
                    if (!owners.TryGetValue(i, out var who)) { missing.Add($"{a.Title}[{i}]"); continue; }
                    if (who.Count == 0) { missing.Add($"{a.Title}[{i}]"); continue; }
                    if (who.Count > 1)
                        doubled.Add($"{a.Title}[{i}] is claimed by {who.Count}: {string.Join(", ", who.Take(3))}");
                }
            }

            _out.WriteLine($"{game}: {archives} archives, {files} files, {rows} rows");
            foreach (var m in missing.Take(8)) _out.WriteLine("  belongs to nothing: " + m);
            foreach (var d in doubled.Take(8)) _out.WriteLine("  claimed twice: " + d);

            Assert.True(archives > 10, $"{game}: only {archives} archives were read");
            Assert.Empty(missing);
            Assert.Empty(doubled);
        }

        /// <summary>The check above proves able to fail: an archive whose grouping drops a file has to be
        /// caught. Without this, "every file belongs somewhere" could be true because nothing groups.</summary>
        [Fact]
        public void TheCheckCatchesAThingThatDropsAFile()
        {
            if (!Directory.Exists(Platinum)) { _out.WriteLine("Platinum not unpacked here"); return; }
            new RomInfo("CPUE", Platinum);
            GraphicAssets.Forget();

            var real = GraphicAssets.All.First(x => x.Dir == DirNames.battleBg);
            int n = GraphicAssets.Count(real);
            Assert.True(n > 0);

            var honest = GraphicAssets.Units(real, n);
            var covered = honest.SelectMany(u => u.Parts).Select(p => p.Index).ToHashSet();
            _out.WriteLine($"the real grouping reaches {covered.Count} of {n} files");
            Assert.Equal(n, covered.Count);

            // The same archive with one row's pieces thrown away.
            var broken = honest.Select(u => u).ToList();
            broken[0] = new GraphicAssets.Unit { Archive = real, Name = broken[0].Name };
            var afterwards = broken.SelectMany(u => u.Parts).Select(p => p.Index).ToHashSet();
            _out.WriteLine($"with one row emptied it reaches {afterwards.Count}");
            Assert.True(afterwards.Count < covered.Count,
                "emptying a row changed nothing, so this check cannot tell a dropped file from a kept one");
        }


        /// <summary>An editor handing a graphic over lands on the right row and the right piece.
        ///
        /// The point of the hand-off is that somebody does not have to find the thing again among six
        /// thousand rows, so it has to land exactly. Checked for every Pokemon and every trainer class
        /// rather than one of each, because an off-by-one here would look plausible.</summary>
        [Theory]
        [MemberData(nameof(Games))]
        public void HandingAGraphicOverLandsOnIt(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);
            GraphicAssets.Forget();

            var vm = new DSPRE.Avalonia.ViewModels.GraphicsBrowserViewModel();

            int tried = 0;
            var wrong = new List<string>();

            void Check(DirNames dir, int stride, int howMany, string what)
            {
                var a = GraphicAssets.All.FirstOrDefault(x => x.Dir == dir);
                if (a == null) return;
                int n = GraphicAssets.Count(a);
                if (n == 0) return;

                for (int k = 0; k < howMany; k++)
                {
                    int file = k * stride;
                    if (file >= n) break;
                    tried++;
                    if (!vm.JumpTo(a, file)) { wrong.Add($"{what} {k}: not found"); continue; }
                    if (vm.ShowingIndex != file)
                        wrong.Add($"{what} {k}: asked for file {file}, landed on {vm.ShowingIndex}");
                }
            }

            Check(DirNames.pokemonBattleSprites, 6, 60, "Pokemon");
            Check(DirNames.trainerGraphics, 5, 40, "Trainer class");

            // Items do not sit in their icon archive in item order, so the hand-off goes through the
            // game's own table. That is exactly where an off-by-one would hide.
            var icons = GraphicAssets.All.FirstOrDefault(x => x.Dir == DirNames.itemIcons);
            if (icons != null && GraphicAssets.Count(icons) > 0)
            {
                int n = GraphicAssets.Count(icons);
                for (int item = 1; item < 80; item++)
                {
                    int drawing = GraphicAssets.DrawingForItem(item);
                    if (drawing < 0 || drawing >= n) continue;
                    tried++;
                    if (!vm.JumpTo(icons, drawing)) { wrong.Add($"Item {item}: not found"); continue; }
                    if (vm.ShowingIndex != drawing)
                        wrong.Add($"Item {item}: asked for file {drawing}, landed on {vm.ShowingIndex}");
                }
            }

            _out.WriteLine($"{game}: {tried} hand-offs, {wrong.Count} landed wrong");
            foreach (var w in wrong.Take(6)) _out.WriteLine("  " + w);
            Assert.True(tried >= 50, $"{game}: only {tried} hand-offs were tried");
            Assert.Empty(wrong);
        }

        /// <summary>The hand-off check can fail: asking for a file one along has to land somewhere else,
        /// or "it landed on it" would be true however the jump behaved.</summary>
        [Fact]
        public void TheHandOffCheckNoticesTheWrongFile()
        {
            if (!Directory.Exists(Platinum)) { _out.WriteLine("Platinum not unpacked here"); return; }
            new RomInfo("CPUE", Platinum);
            GraphicAssets.Forget();

            var vm = new DSPRE.Avalonia.ViewModels.GraphicsBrowserViewModel();
            var a = GraphicAssets.All.First(x => x.Dir == DirNames.pokemonBattleSprites);

            Assert.True(vm.JumpTo(a, 18));
            int landedOn18 = vm.ShowingIndex;
            Assert.True(vm.JumpTo(a, 19));
            int landedOn19 = vm.ShowingIndex;

            _out.WriteLine($"asked for 18 and landed on {landedOn18}; asked for 19 and landed on {landedOn19}");
            Assert.Equal(18, landedOn18);
            Assert.Equal(19, landedOn19);
            Assert.NotEqual(landedOn18, landedOn19);
        }


        /// <summary>What the map headers actually say about battle backgrounds, before anything is built
        /// on the link. The field is there; the question is whether its numbers line up with the
        /// backdrops the renderer knows about.</summary>
        [Theory]
        [MemberData(nameof(Games))]
        public void ReportWhichBattleBackgroundsTheHeadersUse(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);

            var usedBy = new Dictionary<int, List<int>>();
            int headers = 0;
            try { headers = RomInfo.GetHeaderCount(); } catch { }
            for (ushort i = 0; i < headers; i++)
            {
                try
                {
                    var h = DSPRE.ROMFiles.MapHeader.LoadFromARM9(i);
                    if (h == null) continue;
                    int bg = h.battleBackground;
                    if (!usedBy.TryGetValue(bg, out var list)) usedBy[bg] = list = new List<int>();
                    list.Add(i);
                }
                catch { }
            }

            _out.WriteLine($"{game}: {headers} headers, {usedBy.Count} different battle backgrounds used");
            foreach (var kv in usedBy.OrderBy(k => k.Key))
                _out.WriteLine($"   background {kv.Key}: {kv.Value.Count} places, first is header {kv.Value[0]}");
            _out.WriteLine($"   the renderer knows {DSPRE.Avalonia.Data.BattleBgRenderer.BackdropCount} backdrops");
        }


        /// <summary>What the archives with no grouping yet actually hold, so a decision about them can be
        /// made from the contents rather than from the name.</summary>
        [Fact]
        public void ReportTheUngroupedArchives()
        {
            if (!Directory.Exists(Platinum)) { _out.WriteLine("Platinum not unpacked here"); return; }
            new RomInfo("CPUE", Platinum);
            GraphicAssets.Forget();

            foreach (var a in GraphicAssets.All)
            {
                if (a.BuildUnits != null || a.Stride > 1) continue;
                int n;
                try { n = GraphicAssets.Count(a); } catch { n = 0; }
                if (n == 0) continue;

                var narc = new ScriptNarc(a.Dir);
                var kinds = new List<string>();
                for (int i = 0; i < n; i++) kinds.Add(GraphicAssets.Identify(narc.Get(i)).ToString());
                var tally = kinds.GroupBy(k => k).OrderByDescending(g => g.Count())
                                 .Select(g => $"{g.Count()} {g.Key}");
                _out.WriteLine($"{a.Title}: {n} files [{string.Join(", ", tally)}]");
                _out.WriteLine("   in order: " + string.Join(", ", kinds.Take(16)));
            }
        }


        /// <summary>
        /// The battle screen is grouped and named from the game's own list, in every game that has one.
        ///
        /// The names come from batt_obj_def.h, pl_batt_obj_def.h and batt_obj_gs_def.h in the leaked
        /// source, which name every entry of the battle furniture archive in order. This asserts the
        /// grouping actually ran, because falling back to a flat list would still satisfy the
        /// every-file-belongs-somewhere check while leaving the HP bar unfindable.
        /// </summary>
        [Theory]
        [MemberData(nameof(Games))]
        public void TheBattleScreenIsGroupedAndNamed(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);
            GraphicAssets.Forget();

            var archive = GraphicAssets.All.First(x => x.Dir == DirNames.battleObj);
            int files = GraphicAssets.Count(archive);
            Assert.True(files > 200, $"{game}: only {files} battle furniture files, the test would prove little");

            var names = DSPRE.Avalonia.Data.BattleObjects.Names();
            Assert.True(names.Count >= files,
                $"{game}: {files} files in the ROM but the list names {names.Count}");

            var units = GraphicAssets.Units(archive, files);

            // The things people actually go looking for have to be findable by name.
            foreach (string wanted in new[]
                     { "HP bar, your side", "HP bar, their side",
                       "HP bar, your side, two on two", "HP bar, their side, two on two",
                       "Your six balls", "Their six balls" })
                Assert.True(units.Any(u => u.Name == wanted),
                    $"{game}: no row called \"{wanted}\". Rows: "
                    + string.Join(" | ", units.Take(8).Select(u => u.Name)));

            // Nothing may be left with the archive's own title as its name, which is what a row gets when
            // the list did not name it.
            var unnamed = units.Where(u => u.Name == archive.Title).ToList();
            Assert.True(unnamed.Count == 0,
                $"{game}: {unnamed.Count} battle files the list does not name, first at "
                + string.Join(", ", unnamed.Take(5).Select(u => u.First)));

            // And the rows are spread over the tabs rather than all landing on one.
            var tabs = units.GroupBy(u => u.In).ToDictionary(k => k.Key, v => v.Count());
            _out.WriteLine($"{game}: {files} files, {units.Count} rows, "
                         + string.Join(", ", tabs.Select(k => $"{k.Key}={k.Value}")));
            Assert.True(tabs.Count >= 4, $"{game}: rows landed on only {tabs.Count} tabs");
        }

        /// <summary>The check above proves able to fail: a name the list does not contain must not be
        /// found, or "the HP bar is there" would be true of any list at all.</summary>
        [Fact]
        public void TheBattleNameCheckWouldNotFindSomethingAbsent()
        {
            if (!Directory.Exists(Platinum)) { _out.WriteLine("Platinum not unpacked here"); return; }
            new RomInfo("CPUE", Platinum);
            GraphicAssets.Forget();

            var archive = GraphicAssets.All.First(x => x.Dir == DirNames.battleObj);
            int files = GraphicAssets.Count(archive);
            var units = GraphicAssets.Units(archive, files);
            Assert.NotEmpty(units);
            Assert.DoesNotContain(units, u => u.Name == "Fishing rod");
            _out.WriteLine($"{units.Count} rows, none of them a fishing rod");
        }

        /// <summary>
        /// The thrown-ball drawings are named from the ROM's own item names, in both families.
        ///
        /// ball_effect.c's DP_BallEffectID_Get takes an item number, so the first sixteen rows of the ball
        /// table are items 1 to 16; HeartGold and SoulSilver added the Apricorn balls as items 492 to 499.
        /// Reading the names out of the ROM means a renamed item is renamed here too, so this checks
        /// against what the ROM says rather than against a list in the code.
        ///
        /// Which drawing goes with which ball is not the same in the two families: Diamond and Platinum
        /// shuffle them, so the plain ball uses drawing 00 and Quick and Dusk are the other way round.
        /// </summary>
        [Theory]
        [MemberData(nameof(Games))]
        public void ThrownBallsAreNamedFromTheItemNames(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);
            GraphicAssets.Forget();

            var items = RomInfo.GetItemNames();
            Assert.True(items.Length > 100, $"{game}: only {items.Length} item names read");

            var archive = GraphicAssets.All.First(x => x.Dir == DirNames.battleObj);
            int files = GraphicAssets.Count(archive);
            var units = GraphicAssets.Units(archive, files);

            // Found by what the game calls the file, not by how the row reads, so the check does not
            // quietly shrink when the wording changes.
            var listed = DSPRE.Avalonia.Data.BattleObjects.Names();
            var balls = units.Where(u =>
            {
                if (u.Name == null || u.First >= listed.Count) return false;
                var (thing, _) = DSPRE.Avalonia.Data.BattleObjects.Split(listed[u.First]);
                return thing != null && thing.StartsWith("BATT_BALL_");
            }).ToList();
            Assert.True(balls.Count >= 16,
                $"{game}: only {balls.Count} thrown balls named, expected at least the sixteen shared ones");

            // Every one of the sixteen balls both families have must be named after its own item.
            for (int item = 1; item <= 16; item++)
            {
                string want = items[item]?.Trim();
                if (string.IsNullOrWhiteSpace(want)) continue;
                Assert.True(balls.Any(u => u.Name == want || u.Name.Contains(want + " - ")
                                        || u.Name.EndsWith(" - " + want)),
                    $"{game}: no thrown ball named after item {item}, \"{want}\". Named: "
                    + string.Join(" | ", balls.Take(6).Select(u => u.Name)));
            }

            bool johto = RomInfo.gameFamily == RomInfo.GameFamilies.HGSS;
            if (johto)
                for (int item = 492; item <= 499; item++)
                {
                    string want = items[item]?.Trim();
                    if (string.IsNullOrWhiteSpace(want)) continue;
                    Assert.True(balls.Any(u => u.Name == want),
                        $"{game}: no thrown ball named after the Apricorn ball item {item}, \"{want}\"");
                }

            // Where two of them share one drawing the row names both, joined with a dash, rather than
            // picking one and hiding the other. Sinnoh shares the bait drawing between throwing bait and
            // putting a Pokemon back.
            var shared = balls.Where(u => u.Name.Contains(" - ")).ToList();
            if (!johto)
                Assert.True(shared.Count > 0,
                    $"{game}: no drawing is shared, so the joined naming was never exercised");

            _out.WriteLine($"{game}: {balls.Count} thrown balls named from item names, "
                         + $"{shared.Count} shared by more than one");
            foreach (var u in shared) _out.WriteLine("   shared: " + u.Name);
            foreach (var u in balls.Take(3)) _out.WriteLine("   " + u.Name);
        }

        /// <summary>
        /// The check above proves able to fail: the two families must not agree on which drawing is the
        /// plain ball, because they genuinely do not, and a naming that ignored the table would.
        /// </summary>
        [Fact]
        public void TheTwoFamiliesDisagreeAboutWhichDrawingIsThePlainBall()
        {
            if (!Directory.Exists(Platinum) || !Directory.Exists(HeartGold))
            { _out.WriteLine("need both games unpacked"); return; }

            int DrawingOfPlainBall(string code, string path)
            {
                new RomInfo(code, path);
                GraphicAssets.Forget();
                string plain = RomInfo.GetItemNames()[4]?.Trim();
                var archive = GraphicAssets.All.First(x => x.Dir == DirNames.battleObj);
                var units = GraphicAssets.Units(archive, GraphicAssets.Count(archive));
                var row = units.FirstOrDefault(u => u.Name == plain);
                Assert.NotNull(row);
                var names = DSPRE.Avalonia.Data.BattleObjects.Names();
                var (thing, _) = DSPRE.Avalonia.Data.BattleObjects.Split(names[row.First]);
                return int.Parse(thing.Substring("BATT_BALL_".Length));
            }

            int sinnoh = DrawingOfPlainBall("CPUE", Platinum);
            int johto = DrawingOfPlainBall("IPKE", HeartGold);
            _out.WriteLine($"the plain ball uses drawing {sinnoh} in Platinum and {johto} in HeartGold");
            Assert.Equal(0, sinnoh);
            Assert.Equal(4, johto);
        }

        /// <summary>
        /// Two locations sharing one splash screen must be one row naming both, not two rows.
        ///
        /// HeartGold's table has rows 4 and 5 both naming files 27 to 35 and rows 17 and 18 both naming
        /// 150 to 158, which is real sharing rather than a misread: the shared rows also carry the same
        /// name message id. This pins the merge, and fails if the table stops being read at all.
        /// </summary>
        [Fact]
        public void SplashScreensSharedByTwoPlacesAreOneRow()
        {
            if (!Directory.Exists(HeartGold)) { _out.WriteLine("HeartGold not unpacked here"); return; }
            new RomInfo("IPKE", HeartGold);

            var rows = DSPRE.Avalonia.Data.DungeonCutinTable.Read();
            Assert.Equal(25, rows.Count);

            var sets = rows.Select(r => string.Join(",",
                r.Art.SelectMany(x => new[] { x.Palette, x.Tiles, x.Screen }))).ToList();
            int shared = sets.Count - sets.Distinct().Count();
            Assert.True(shared == 2,
                $"expected two rows to repeat another row's files, found {shared}");

            var archive = GraphicAssets.All.First(x => x.Dir == DirNames.dungeonCutinGraphics);
            int files = GraphicAssets.Count(archive);
            Assert.True(files > 200, $"only {files} splash screen files, the test would prove little");

            var units = DSPRE.Avalonia.Data.DungeonCutinTable.UnitsFor(archive, files);
            int named = units.Count(u => u.Name.StartsWith("Splash screen "));
            Assert.True(named == 23,
                $"expected 23 rows from 25 table entries with two pairs merged, got {named}");

            var both = units.Where(u => u.Name.StartsWith("Splash screen 4")
                                     || u.Name.StartsWith("Splash screen 17")).ToList();
            Assert.Equal(2, both.Count);
            foreach (var u in both)
                Assert.True(u.Name.Contains(","),
                    $"\"{u.Name}\" should name both places that share it");
            _out.WriteLine("merged rows: " + string.Join(" | ", both.Select(u => u.Name)));
        }

        /// <summary>
        /// Text box frames must be paired the way winframe.naix pairs them, not by position.
        ///
        /// The drawings come first and the colours after, so drawing 2 goes with colours 26 in HeartGold
        /// and 25 in Platinum. This asserts the grouping actually ran and produced the twenty styles,
        /// because the builder falls back to a flat list when the archive does not look right, and a flat
        /// list would still satisfy the every-file-belongs-somewhere check.
        /// </summary>
        [Theory]
        [MemberData(nameof(Games))]
        public void TextBoxFramesPairDrawingsWithTheirOwnColours(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);

            var archive = GraphicAssets.All.FirstOrDefault(x => x.Dir == DirNames.windowFrames);
            Assert.NotNull(archive);
            int files = GraphicAssets.Count(archive);
            Assert.True(files > 40, $"{game}: only {files} window frame files, the test would prove little");

            // Read the tags straight out of the ROM rather than trusting the grouping's own idea of where
            // the colours start, so this can catch the grouping being off by one.
            var narc = new ScriptNarc(DirNames.windowFrames);
            var tags = new List<string>();
            for (int i = 0; i < narc.Count; i++)
            {
                var b = narc.Get(i);
                tags.Add(b == null || b.Length < 4 ? "----"
                    : new string(new[] { (char)b[0], (char)b[1], (char)b[2], (char)b[3] }));
            }
            int firstColour = tags.IndexOf("RLCN");
            Assert.True(firstColour >= 24, $"{game}: colours start at {firstColour}, not where the naix says");
            Assert.Equal(firstColour, GraphicAssets.FirstPaletteIndex(archive));

            var units = DSPRE.Avalonia.Data.GraphicUnits.WindowFrames(archive, files);
            var styles = units.Where(u => u.Name.StartsWith("Text box style ")).ToList();
            Assert.True(styles.Count == 20,
                $"{game}: expected 20 text box styles, got {styles.Count}. The builder fell back to flat.");

            for (int i = 0; i < 20; i++)
            {
                var u = styles.First(x => x.Name == $"Text box style {i:00}");
                var drawing = u.Parts.First(x => x.Name == "Drawing");
                var colours = u.Parts.First(x => x.Name == "Colours");
                Assert.Equal(2 + i, drawing.Index);
                Assert.Equal(firstColour + 1 + i, colours.Index);
                Assert.Equal("RGCN", tags[drawing.Index]);
                Assert.Equal("RLCN", tags[colours.Index]);
            }

            Assert.Contains(units, u => u.Name == "System window");
            Assert.Contains(units, u => u.Name == "Field menu window");
            int cursors = units.Count(u => u.Name.StartsWith("Window cursor "));
            Assert.Equal(firstColour - 22, cursors);
            Assert.True(cursors >= 2, $"{game}: {cursors} cursors, the naix says two or three");
            _out.WriteLine($"{game}: {files} files, colours start at {firstColour}, "
                         + $"{units.Count} rows, {units.Count(u => u.Name.StartsWith("Window cursor "))} cursors");
        }

        /// <summary>
        /// Battle scenes must never show a place name the game did not actually give that place.
        ///
        /// The displayed-name lookup answers for every header in Diamond but for almost none in
        /// Platinum and HeartGold, where the handful it does answer are wrong: nine different places
        /// came out as Route 201. So where the lookup cannot answer for nearly every header, every name
        /// shown has to be the internal code instead. This fails if that gate is taken away.
        /// </summary>
        [Theory]
        [MemberData(nameof(Games))]
        public void BattleScenesNeverShowAPlaceNameTheLookupCannotBackUp(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);

            var displayed = RomInfo.GetLocationNames();
            var internalNames = HeaderLists.GetHeaderListBoxNames();
            bool dynamic = RomInfo.gameDirs.ContainsKey(DirNames.dynamicHeaders);
            int headers = RomInfo.GetHeaderCount();
            Assert.True(headers > 0, $"{game}: no headers read, the test would prove nothing");

            int answered = 0;
            for (ushort i = 0; i < headers; i++)
                if (DSPRE.ROMFiles.MapHeader.TryReadLocationNameIndex(i, dynamic, out int at)
                    && at >= 0 && at < displayed.Count) answered++;
            bool trusted = answered * 10 >= headers * 9;

            var scenes = DSPRE.Avalonia.Data.BattleScenes.Read();
            Assert.NotEmpty(scenes);

            var shown = scenes.SelectMany(sc => sc.PlaceNames).Distinct().ToList();
            Assert.NotEmpty(shown);

            var codes = new HashSet<string>(internalNames.Select(n => n?.Trim()));
            if (trusted)
            {
                var real = new HashSet<string>(displayed.Select(n => n?.Trim()));
                int fromDisplayed = shown.Count(n => real.Contains(n));
                Assert.True(fromDisplayed > 0,
                    $"{game}: the lookup answers for {answered}/{headers} headers but no scene uses a "
                    + "displayed name, so the good names are being thrown away");
                _out.WriteLine($"{game}: lookup answered {answered}/{headers}, using real place names "
                             + $"({fromDisplayed} of {shown.Count} shown names are displayed names)");
            }
            else
            {
                var wrong = shown.Where(n => !codes.Contains(n)).ToList();
                Assert.True(wrong.Count == 0,
                    $"{game}: the lookup only answers for {answered}/{headers} headers, so every name "
                    + $"must be the internal code, but {wrong.Count} are not: "
                    + string.Join(", ", wrong.Take(8)));
                _out.WriteLine($"{game}: lookup answered only {answered}/{headers}, showing internal "
                             + $"codes for all {shown.Count} names");
            }
        }

        /// <summary>The groupings actually group: an archive that has one should end up with far fewer
        /// rows than files, or nothing was gained.</summary>
        [Theory]
        [MemberData(nameof(Games))]
        public void TheArchivesWithAGroupingReallyGroup(string code, string path, string game)
        {
            if (!Directory.Exists(path)) { _out.WriteLine($"{game}: not unpacked here"); return; }
            new RomInfo(code, path);
            GraphicAssets.Forget();

            var grouped = new List<string>();
            foreach (var a in GraphicAssets.All)
            {
                if (a.BuildUnits == null && a.Stride <= 1 && a.LeadIn == 0) continue;
                int n;
                try { n = GraphicAssets.Count(a); } catch { n = 0; }
                if (n == 0) continue;

                var units = GraphicAssets.Units(a, n);
                int rows = units.Count;
                bool reachesOtherArchives = units.Any(u => u.Parts.Any(pt => pt.Archive != null && pt.Archive.Dir != a.Dir));

                // An archive whose files are each their own thing gains nothing from folding, and
                // everything from being named: the fonts are one file each and were listed as eleven
                // numbers. So a row carrying a real name counts as work done too.
                int named = units.Count(u => !string.IsNullOrEmpty(u.Name) && u.Name != a.Title);

                grouped.Add($"{a.Title}: {n} files into {rows} rows, {named} of them named"
                          + (reachesOtherArchives ? ", each pulling in its pieces from other archives" : ""));

                // Grouping folds files together, pulls a row's pieces in from other archives, or names
                // them. A move effect does the second: one row per drawing, with its colours, layout and
                // timing fetched from three neighbouring archives.
                Assert.True(rows < n || reachesOtherArchives || named > 0,
                    $"{game} / {a.Title}: {n} files still came out as {rows} rows, none of them named and "
                    + "nothing pulled in from elsewhere, so the grouping did nothing");
            }

            foreach (var g in grouped) _out.WriteLine("  " + g);
            Assert.True(grouped.Count >= 4, $"{game}: only {grouped.Count} archives group at all");
        }
    }
}

using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The Pokétch application map, checked against a real Platinum archive. Every member number in
    /// <see cref="PoketchApps"/> was transcribed by hand, and a wrong one would have the editor paint over
    /// the wrong file, so each is looked up and identified here rather than trusted.
    /// </summary>
    [Collection("rom")]
    public class PoketchAppsTests
    {
        private static bool Ready()
        {
            if (!Directory.Exists(TestRoms.Platinum)) return false;
            try { new RomInfo("CPUE", TestRoms.Platinum); } catch { return false; }
            return true;
        }

        private static ScriptNarc Archive()
        {
            var narc = new ScriptNarc(RomInfo.DirNames.poketch);
            Assert.True(narc.Available, "Platinum has no Pokétch archive");
            return narc;
        }

        private static GraphicAssets.Kind KindOf(ScriptNarc narc, int member)
            => GraphicAssets.Identify(NitroBgCodec.Inflate(narc.Get(member)));

        [SkippableFact]
        public void EveryFileEveryApplicationNamesIsTheKindOfFileItIsClaimedToBe()
        {
            Skip.If(!Ready(), "Platinum is not unpacked here");
            var narc = Archive();
            int looked = 0;

            void Check(PoketchApps.App app, int member, GraphicAssets.Kind wanted, string what)
            {
                if (member < 0) return;
                Assert.True(narc.Get(member) != null,
                    $"{app.Name}: {what} {member} is not in the archive");
                var kind = KindOf(narc, member);
                Assert.True(kind == wanted, $"{app.Name}: {what} {member} is {kind}, not {wanted}");
                looked++;
            }

            foreach (var app in PoketchApps.All)
            {
                Check(app, app.Tiles, GraphicAssets.Kind.TileGraphic, "drawing");
                Check(app, app.Arrangement, GraphicAssets.Kind.TileMap, "arrangement");
                Check(app, app.Sprites, GraphicAssets.Kind.TileGraphic, "sprite sheet");
                Check(app, app.Cells, GraphicAssets.Kind.CellLayout, "cells");
                Check(app, app.Animation, GraphicAssets.Kind.CellAnimation, "animation");
            }

            // Two applications draw nothing of their own, so the count is short of five per application.
            Assert.True(looked > 90, $"only {looked} files were checked");
        }

        /// <summary>
        /// The members said to be authored but never loaded really are in the archive. The claim is that
        /// nothing references them, not that they are missing, and an editor that hides them should say so
        /// for the right reason.
        /// </summary>
        [SkippableFact]
        public void TheFilesNothingLoadsAreStillInTheArchive()
        {
            Skip.If(!Ready(), "Platinum is not unpacked here");
            var narc = Archive();
            foreach (int member in PoketchApps.NeverLoaded)
            {
                Assert.True(narc.Get(member) != null, $"member {member} is not there at all");
                Assert.NotEqual(GraphicAssets.Kind.Unknown, KindOf(narc, member));
            }
            Assert.True(PoketchApps.NeverLoaded.Length > 0);
        }

        /// <summary>The theme palette really does hold the sixteen rows the eight themes are cut from.</summary>
        [SkippableFact]
        public void TheThemePaletteHoldsSixteenRows()
        {
            Skip.If(!Ready(), "Platinum is not unpacked here");
            var narc = Archive();
            ushort[] colours = DsBgScreen.ReadColours(NitroBgCodec.Inflate(narc.Get(0)));
            Assert.Equal(256, colours.Length);

            // The casing keeps two rows of its own, a girl's and a boy's.
            ushort[] casing = DsBgScreen.ReadColours(NitroBgCodec.Inflate(narc.Get(13)));
            Assert.Equal(32, casing.Length);

            foreach (int i in PoketchApps.ToneRamp) Assert.InRange(i, 0, 15);
        }

        /// <summary>Every application is listed once, at the number the Pokétch cycles it in at.</summary>
        [Fact]
        public void TheApplicationsAreListedInOrderWithoutRepeats()
        {
            var all = PoketchApps.All;
            Assert.Equal(25, all.Length);
            for (int i = 0; i < all.Length; i++) Assert.Equal(i, all[i].Id);
            Assert.Equal(all.Length, all.Select(a => a.Name).Distinct().Count());
        }

        /// <summary>
        /// The room each application has, so a future import path has a number to refuse against. The two
        /// squeezed by a text window are the ones worth pinning.
        /// </summary>
        [Fact]
        public void TheApplicationsSqueezedByATextWindowReportTheirRealRoom()
        {
            Assert.Equal(32, PoketchApps.All.Single(a => a.Name == "Link Searcher").TileRoom);
            Assert.Equal(132, PoketchApps.All.Single(a => a.Name == "Roulette").TileRoom);
            Assert.Equal(12, PoketchApps.All.Single(a => a.Name == "Memo Pad").TileRoom);

            // And one that is all art, so the whole screen's worth is available.
            Assert.Equal(PoketchApps.TileCeiling,
                         PoketchApps.All.Single(a => a.Name == "Stopwatch").TileRoom);
        }

        /// <summary>The applications that count tiles per row say how wide their drawing has to stay.</summary>
        [Fact]
        public void TheApplicationsThatCountTilesPerRowPinTheirWidth()
        {
            Assert.Equal(40, PoketchApps.All.Single(a => a.Name == "Calculator").FixedWidthTiles);
            Assert.Equal(12, PoketchApps.All.Single(a => a.Name == "Calendar").FixedWidthTiles);
            Assert.Equal(37, PoketchApps.All.Single(a => a.Name == "Stopwatch").FixedWidthTiles);

            // Most are free to be any shape, so this is not a blanket rule.
            Assert.Equal(0, PoketchApps.All.Single(a => a.Name == "Coin Toss").FixedWidthTiles);
        }

        /// <summary>The two applications with no art of their own carry the reason why.</summary>
        [Fact]
        public void TheApplicationsWithNoArtCarryTheReason()
        {
            foreach (string name in new[] { "Dot Artist", "Pokémon History" })
            {
                var app = PoketchApps.All.Single(a => a.Name == name);
                Assert.True(app.Tiles < 0, $"{name} should name no drawing");
                Assert.False(string.IsNullOrEmpty(app.ReadOnlyBecause));
            }
        }

        /// <summary>
        /// Sprite positions are only claimed where the game's own tables give them, because a sprite drawn
        /// somewhere plausible but wrong is worse than one left out.
        /// </summary>
        [Fact]
        public void SpritePositionsAreOnlyClaimedWhereTheyAreKnown()
        {
            var withSlots = PoketchApps.All.Where(a => a.SpriteSlots != null).ToList();
            Assert.True(withSlots.Count >= 12, $"only {withSlots.Count} applications have positions");

            // Every slot lands on the screen.
            foreach (var app in withSlots)
                foreach (var (x, y) in app.SpriteSlots)
                {
                    Assert.InRange(x, 0, 255);
                    Assert.InRange(y, 0, 191);
                }

            // The counts match what the game's tables hold.
            Assert.Equal(6, PoketchApps.All.Single(a => a.Name == "Party Status").SpriteSlots.Length);
            Assert.Equal(17, PoketchApps.All.Single(a => a.Name == "Kitchen Timer").SpriteSlots.Length);
            Assert.Equal(9, PoketchApps.All.Single(a => a.Name == "Stopwatch").SpriteSlots.Length);
            Assert.Single(PoketchApps.All.Single(a => a.Name == "Analog Watch").SpriteSlots);

            // And an application whose positions are computed as it runs claims none.
            Assert.Null(PoketchApps.All.Single(a => a.Name == "Marking Map").SpriteSlots);
        }

        /// <summary>
        /// The digit sheet and the map art are shared, and picking one has to name everything that moves
        /// with it.
        /// </summary>
        [Fact]
        public void SharedFilesNameEveryApplicationThatDrawsFromThem()
        {
            var digits = PoketchApps.SharedBy(3);
            foreach (string name in new[] { "Counter", "Pedometer", "Stopwatch", "Alarm Clock", "Kitchen Timer" })
                Assert.Contains(name, digits);

            var map = PoketchApps.SharedBy(117);
            Assert.Contains("Marking Map", map);
            Assert.Contains("Berry Searcher", map);

            // The watch drawing is shared by exactly the two watches and nothing else.
            var watch = PoketchApps.SharedBy(23);
            Assert.Equal(new[] { "Digital Watch", "Analog Watch" }, watch.OrderByDescending(n => n).ToArray());

            // And something used once names only its owner, so this cannot pass by naming everything.
            Assert.Equal(new[] { "Calculator" }, PoketchApps.SharedBy(16).ToArray());
        }

        /// <summary>
        /// Only five animations carry a transform. The editor shows rotation and scale boxes for those and
        /// says "frame order only" for the rest, so the list has to be right.
        /// </summary>
        [SkippableFact]
        public void OnlyTheAnimationsWithTransformsAreListedAsHavingThem()
        {
            Skip.If(!Ready(), "Platinum is not unpacked here");
            var narc = Archive();

            // Every one named is really an animation file in the archive.
            foreach (int member in PoketchApps.AnimationsWithTransforms)
            {
                Assert.True(narc.Get(member) != null, $"animation {member} is not in the archive");
                Assert.Equal(GraphicAssets.Kind.CellAnimation, KindOf(narc, member));
            }

            // The Analog Watch's hands are the headline case: its animation is the one with the angles.
            var analog = PoketchApps.All.Single(a => a.Name == "Analog Watch");
            Assert.Contains(analog.Animation, PoketchApps.AnimationsWithTransforms);

            // And most animations are plain, so this is a short list rather than a long one.
            int animations = PoketchApps.All.Count(a => a.Animation >= 0);
            Assert.True(PoketchApps.AnimationsWithTransforms.Length < animations,
                        "the transform list should be shorter than the list of animations");
        }
    }
}

using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels.Graphics;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The pieces the editor offers for each Pokétch application, checked against a real Platinum archive.
    /// <see cref="PoketchAppsTests"/> proves the table is right; this proves the editor turns that table into
    /// pieces that point at the files they claim.
    /// </summary>
    [Collection("rom")]
    public class PoketchAppPiecesTests
    {
        private const BottomScreenEditorViewModel.Tab Poketch = BottomScreenEditorViewModel.Tab.Poketch;

        private static bool Ready()
        {
            if (!Directory.Exists(TestRoms.Platinum)) return false;
            try { new RomInfo("CPUE", TestRoms.Platinum); } catch { return false; }
            return true;
        }

        [SkippableFact]
        public void EveryApplicationsPiecesPointAtFilesThatAreReallyThere()
        {
            Skip.If(!Ready(), "Platinum is not unpacked here");
            var narc = new ScriptNarc(RomInfo.DirNames.poketch);
            Assert.True(narc.Available);
            int looked = 0;

            for (int app = 1; app <= PoketchApps.All.Length; app++)
            {
                var pieces = BottomScreenEditorViewModel.PiecesFor(Poketch, app: app);
                Assert.NotEmpty(pieces);
                string name = PoketchApps.All[app - 1].Name;

                foreach (var piece in pieces)
                {
                    Assert.Equal(RomInfo.DirNames.poketch, piece.Archive);

                    void Check(int member, GraphicAssets.Kind wanted, string what)
                    {
                        if (member < 0) return;
                        Assert.True(narc.Get(member) != null, $"{name}/{piece.Name}: no {what} {member}");
                        var kind = GraphicAssets.Identify(NitroBgCodec.Inflate(narc.Get(member)));
                        Assert.True(kind == wanted, $"{name}/{piece.Name}: {what} {member} is {kind}, not {wanted}");
                        looked++;
                    }

                    Check(piece.Drawing, GraphicAssets.Kind.TileGraphic, "drawing");
                    Check(piece.Arrangement, GraphicAssets.Kind.TileMap, "arrangement");
                    Check(piece.Cells, GraphicAssets.Kind.CellLayout, "layout");
                    Check(piece.Animation, GraphicAssets.Kind.CellAnimation, "animation");
                    Check(piece.PaletteMember, GraphicAssets.Kind.Palette, "colours");
                }
            }

            Assert.True(looked > 100, $"only {looked} files were checked");
        }

        /// <summary>
        /// Picking no application still gives the Pokétch frame, so the older calls that leave the argument
        /// out keep working.
        /// </summary>
        [Fact]
        public void WithoutAnApplicationYouGetTheFrame()
        {
            var pieces = BottomScreenEditorViewModel.PiecesFor(Poketch);
            Assert.Equal(new[] { "Casing", "Watch face", "Watch digits", "Before you have one" },
                         pieces.Select(p => p.Name).ToArray());
        }

        /// <summary>
        /// The two applications the game draws without any file of their own offer one row: their colours,
        /// which is the only thing about them that can be changed, with the reason attached. Showing a black
        /// screen and nothing to do reads as a broken editor.
        /// </summary>
        [Fact]
        public void TheApplicationsWithNoArtOfferTheirColoursAndTheReason()
        {
            foreach (string name in new[] { "Dot Artist", "Pokémon History" })
            {
                int app = PoketchApps.All.ToList().FindIndex(a => a.Name == name) + 1;
                var pieces = BottomScreenEditorViewModel.PiecesFor(Poketch, app: app);

                var only = Assert.Single(pieces);
                Assert.Equal(name + " colours", only.Name);
                Assert.True(only.Drawing < 0, "there is no drawing to offer");

                // The theme is editable, so this row is not read-only; it carries the reason as a caution.
                Assert.Equal(0, only.PaletteMember);
                Assert.Null(only.ReadOnlyBecause);
                Assert.False(string.IsNullOrEmpty(only.Warning));
                Assert.Equal(PoketchApps.All[app - 1].ReadOnlyBecause, only.Warning);
            }
        }

        /// <summary>
        /// An animation row says whether the file carries a turn or a stretch, because the editor shows
        /// different fields for the five that do.
        /// </summary>
        [Fact]
        public void AnAnimationRowSaysWhetherItCarriesATurn()
        {
            int analog = PoketchApps.All.ToList().FindIndex(a => a.Name == "Analog Watch") + 1;
            var turning = BottomScreenEditorViewModel.PiecesFor(Poketch, app: analog)
                                                     .Single(p => p.Name == "Animation");
            Assert.Contains("turn", turning.What);

            int coinToss = PoketchApps.All.ToList().FindIndex(a => a.Name == "Coin Toss") + 1;
            var plain = BottomScreenEditorViewModel.PiecesFor(Poketch, app: coinToss)
                                                   .Single(p => p.Name == "Animation");
            Assert.DoesNotContain("turn", plain.What);

            // Animation files can be written now, so neither row is read-only. Both name the layout their
            // frames draw from, which a frame number means nothing without.
            Assert.Null(turning.ReadOnlyBecause);
            Assert.Null(plain.ReadOnlyBecause);
            Assert.True(turning.Animation >= 0 && turning.Cells >= 0, "the turning row names its files");
            Assert.True(plain.Animation >= 0 && plain.Cells >= 0, "the plain row names its files");
        }

        /// <summary>
        /// An application whose screen shares its drawing with another says so, so a repaint is no surprise.
        /// Berry Searcher and Marking Map are the pair that share everything but the arrangement.
        /// </summary>
        [Fact]
        public void TheTwoMapApplicationsShareOneDrawing()
        {
            int berry = PoketchApps.All.ToList().FindIndex(a => a.Name == "Berry Searcher") + 1;
            int marking = PoketchApps.All.ToList().FindIndex(a => a.Name == "Marking Map") + 1;

            var b = BottomScreenEditorViewModel.PiecesFor(Poketch, app: berry).Single(p => p.Name == "Screen");
            var m = BottomScreenEditorViewModel.PiecesFor(Poketch, app: marking).Single(p => p.Name == "Screen");

            Assert.Equal(b.Drawing, m.Drawing);
            Assert.NotEqual(b.Arrangement, m.Arrangement);
        }

        /// <summary>
        /// Every application draws with the theme palette, and the row follows the theme and the backlight,
        /// since that is how the game picks it.
        /// </summary>
        [Fact]
        public void TheScreenRowFollowsTheThemeAndTheBacklight()
        {
            int calculator = PoketchApps.All.ToList().FindIndex(a => a.Name == "Calculator") + 1;

            var plain = BottomScreenEditorViewModel.PiecesFor(Poketch, theme: 3, app: calculator)
                                                   .Single(p => p.Name == "Screen");
            Assert.Equal(6, plain.PaletteRow);

            var lit = BottomScreenEditorViewModel.PiecesFor(Poketch, theme: 3, backlight: true, app: calculator)
                                                 .Single(p => p.Name == "Screen");
            Assert.Equal(7, lit.PaletteRow);
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE;
using DSPRE.Avalonia.Data;
using DSPRE.Avalonia.ViewModels.Graphics;
using Xunit;

namespace DSPRE.Tests
{
    /// <summary>
    /// The Bottom Screen editor's map of what is made of which file, checked against real ROMs. Nothing
    /// here writes to a project: the one write is patched in memory and thrown away.
    /// </summary>
    [Collection("rom")]
    public class BottomScreenEditorTests
    {
        private static bool Ready(string project, string id)
        {
            if (!Directory.Exists(project)) return false;
            try { new RomInfo(id, project); } catch { return false; }
            return true;
        }

        /// <summary>
        /// What each of a piece's files should turn out to be once it is opened out, and how many of them
        /// were looked at.
        /// </summary>
        private static int CheckFiles(BottomScreenPiece piece)
        {
            var narc = new ScriptNarc(piece.Archive);
            Assert.True(narc.Available, $"{piece.Archive} is not in this game");
            int looked = 0;

            void Check(int member, GraphicAssets.Kind wanted, string what)
            {
                if (member < 0) return;
                byte[] stored = narc.Get(member);
                Assert.True(stored != null, $"{piece.Name}: {what} {member} is not in {piece.Archive}");
                var kind = GraphicAssets.Identify(NitroBgCodec.Inflate(stored));
                Assert.True(kind == wanted,
                    $"{piece.Name}: {what} {member} is {kind}, not {wanted}");
                looked++;
            }

            Check(piece.Drawing, GraphicAssets.Kind.TileGraphic, "drawing");
            Check(piece.Arrangement, GraphicAssets.Kind.TileMap, "arrangement");
            Check(piece.Cells, GraphicAssets.Kind.CellLayout, "layout");
            Check(piece.PaletteMember, GraphicAssets.Kind.Palette, "colours");
            return looked;
        }

        private static void SweepTab(BottomScreenEditorViewModel.Tab tab)
        {
            var pieces = BottomScreenEditorViewModel.PiecesFor(tab);
            Assert.NotEmpty(pieces);

            int checkedFiles = 0;
            foreach (var piece in pieces) checkedFiles += CheckFiles(piece);
            Assert.True(checkedFiles > 0, "no files were checked");
        }

        [SkippableFact]
        public void EveryPieceOfTheTouchMenuIsAFileThatIsReallyThere()
        {
            Skip.If(!Ready(TestRoms.HeartGold, "IPKE"), "HeartGold is not unpacked here");
            SweepTab(BottomScreenEditorViewModel.Tab.Menu);
        }

        [SkippableFact]
        public void EveryPieceOfThePokeBallScreenIsAFileThatIsReallyThere()
        {
            Skip.If(!Ready(TestRoms.HeartGold, "IPKE"), "HeartGold is not unpacked here");
            SweepTab(BottomScreenEditorViewModel.Tab.Choices);
        }

        [SkippableFact]
        public void EveryPieceOfThePoketchIsAFileThatIsReallyThere()
        {
            Skip.If(!Ready(TestRoms.Platinum, "CPUE"), "Platinum is not unpacked here");
            SweepTab(BottomScreenEditorViewModel.Tab.Poketch);
        }

        /// <summary>Diamond has the Pokétch too, with the same applications and its own casing.</summary>
        [SkippableFact]
        public void DiamondsPoketchIsReadAndDrawn()
        {
            Skip.If(!Ready(TestRoms.Diamond, "ADAE"), "Diamond is not unpacked here");
            SweepTab(BottomScreenEditorViewModel.Tab.Poketch);

            var screen = PoketchScreen.Load();
            Assert.NotNull(screen);
            byte[] watch = screen.RenderWatch(false, 0, false, 12, 34, PoketchScreen.Look.Free, PoketchScreen.Look.Free);
            Assert.Equal(DsBgScreen.Width * DsBgScreen.Height * 4, watch.Length);
        }

        /// <summary>
        /// The seven icons are all painted from one row, so the editor has to say so before somebody
        /// recolours the Pokédex button and finds the other six have moved with it.
        /// </summary>
        [SkippableFact]
        public void TheSevenIconsAreToldToShareTheirColours()
        {
            Skip.If(!Ready(TestRoms.HeartGold, "IPKE"), "HeartGold is not unpacked here");

            var pieces = BottomScreenEditorViewModel.PiecesFor(BottomScreenEditorViewModel.Tab.Menu);
            BottomScreenEditorViewModel.NoteSharing(pieces);

            var icons = pieces.Where(p => p.Name.EndsWith(" icon")).ToList();
            Assert.Equal(7, icons.Count);
            Assert.Single(icons.Select(p => (p.PaletteMember, p.PaletteRow)).Distinct());

            foreach (var icon in icons)
            {
                Assert.False(string.IsNullOrEmpty(icon.SharedWith), $"{icon.Name} says nothing about sharing");
                foreach (var other in icons.Where(o => !ReferenceEquals(o, icon)))
                    Assert.Contains(other.Name, icon.SharedWith);
            }
        }

        /// <summary>The answer frame has a file of colours to itself, so it must not claim to share.</summary>
        [SkippableFact]
        public void TheAnswerFrameKeepsItsColoursToItself()
        {
            Skip.If(!Ready(TestRoms.HeartGold, "IPKE"), "HeartGold is not unpacked here");

            var pieces = BottomScreenEditorViewModel.PiecesFor(BottomScreenEditorViewModel.Tab.Choices);
            BottomScreenEditorViewModel.NoteSharing(pieces);

            var frame = pieces.Single(p => p.Name == "Answer frame");
            Assert.Null(frame.SharedWith);

            // And the screen and its boxes really do share, so the check above is not passing by accident.
            var screen = pieces.Single(p => p.Name == "Poké Ball screen");
            Assert.False(string.IsNullOrEmpty(screen.SharedWith));
        }

        /// <summary>
        /// A colour written into a row lands on that colour and leaves the ones beside it alone. These
        /// files hold many rows side by side, so writing to the wrong place would repaint something else.
        /// </summary>
        [SkippableFact]
        public void ChangingOneColourLeavesItsNeighboursAlone()
        {
            Skip.If(!Ready(TestRoms.HeartGold, "IPKE"), "HeartGold is not unpacked here");

            var icons = BottomScreenEditorViewModel.PiecesFor(BottomScreenEditorViewModel.Tab.Menu)
                                                   .First(p => p.Name.EndsWith(" icon"));
            var narc = new ScriptNarc(icons.Archive);
            byte[] file = narc.Get(icons.PaletteMember);
            Assert.NotNull(file);

            ushort[] before = DsBgScreen.ReadColours(file);
            // A colour well past the first row, to prove the row is honoured rather than assumed.
            int at = 16 + 5;
            Skip.If(before.Length <= at, "this palette has only one row");

            const uint wanted = 0xFF3060A0u;
            string trouble = GraphicAssets.PatchPalette(ref file, new[] { wanted }, at);
            Assert.Null(trouble);

            ushort[] after = DsBgScreen.ReadColours(file);
            Assert.Equal(before.Length, after.Length);

            // 8 bits down to 5 is what the hardware stores, so the read-back is the rounded colour.
            int r = 0x30 >> 3, g = 0x60 >> 3, b = 0xA0 >> 3;
            Assert.Equal((ushort)(r | (g << 5) | (b << 10)), after[at]);

            for (int i = 0; i < before.Length; i++)
                if (i != at) Assert.True(before[i] == after[i], $"colour {i} moved as well");

            // The patch happened in memory. The file on disk belongs to somebody's project, so it has to
            // still read back exactly as it did before this test ran.
            Assert.Equal(before, DsBgScreen.ReadColours(narc.Get(icons.PaletteMember)));
        }
    }
}

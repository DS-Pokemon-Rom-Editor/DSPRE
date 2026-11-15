using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using DSPRE.Avalonia.Data;
using DSPRE.Editors;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Avalonia.ViewModels.Graphics
{
    public sealed partial class BottomScreenEditorViewModel
    {
        /// <summary>Which of the bottom screens is being looked at.</summary>
        public enum Tab { Menu, Choices, Poketch }

        private Tab CurrentTab => IsMenuTab ? Tab.Menu : IsChoicesTab ? Tab.Choices : Tab.Poketch;

        private List<BottomScreenPiece> BuildPieces() => PiecesFor(CurrentTab, _female, _theme, _backlight, _app);

        /// <summary>Every piece of one screen and the files it is drawn from.</summary>
        public static List<BottomScreenPiece> PiecesFor(Tab tab, bool female = false, int theme = 0,
                                                        bool backlight = false, int app = 0)
        {
            List<BottomScreenPiece> list = new List<BottomScreenPiece>();
            int row = theme * 2 + (backlight ? 1 : 0);
            if (tab == Tab.Menu) MenuPieces(list);
            else if (tab == Tab.Choices) ChoicePieces(list);
            else if (app >= 1) AppPieces(list, PoketchApps.All[Math.Min(app - 1, PoketchApps.All.Length - 1)], row);
            else FramePieces(list, female, row);
            return list;
        }

        private static void MenuPieces(List<BottomScreenPiece> list)
        {
            list.Add(new BottomScreenPiece
            {
                Name = "Panel",
                Archive = DirNames.FieldTouchMenu, Drawing = MenuTiles, Arrangement = MenuMap,
                PaletteMember = MenuPalettes, PaletteRow = 0, PaletteRows = 16, TileRoom = PanelTileRoom,
                Warning = "The phone bubble draws with some of these tiles and colour row 2. Row 4 colours the text.",
            });
            int rowsPerIcon = FieldMenuColoursPatch.IsApplied ? FieldMenuColoursPatch.RowsPerItem : 0;
            for (int i = 0; i < IconNames.Length; i++)
                list.Add(new BottomScreenPiece
                {
                    Name = IconNames[i],
                    Archive = DirNames.FieldTouchMenu, Drawing = IconDrawings[i], Cells = IconCells,
                    PaletteMember = IconPalettes, PaletteRow = IconKinds[i] * rowsPerIcon,
                    IconKind = IconKinds[i], TileRoom = IconTileRoom,
                });
            list.Add(new BottomScreenPiece
            {
                Name = "Side buttons",
                What = "Registered item frames, running shoes, the A button and the mark beside MENU.",
                Archive = DirNames.FieldTouchMenu, Drawing = ButtonDrawing, Cells = ButtonCells,
                PaletteMember = MenuPalettes, PaletteRow = 0, PaletteRows = 16, TileRoom = SideButtonTileRoom,
                Warning = "The panel also uses colour rows 0 to 3.",
            });
        }

        private static void ChoicePieces(List<BottomScreenPiece> list)
        {
            list.Add(new BottomScreenPiece
            {
                Name = "Poké Ball screen",
                Archive = DirNames.FieldTouchChoices, Drawing = ChoiceTiles, Arrangement = PokeBallMap,
                PaletteMember = ChoicePalettes, PaletteRow = 0, PaletteRows = 5,
            });
            list.Add(new BottomScreenPiece
            {
                Name = "Yes and no boxes",
                Archive = DirNames.FieldTouchChoices, Arrangement = YesNoMap,
                PaletteMember = ChoicePalettes, PaletteRow = 0, PaletteRows = 5,
            });
            for (int n = 2; n <= 8; n++)
                list.Add(new BottomScreenPiece
                {
                    Name = $"List of {n}",
                    Archive = DirNames.FieldTouchChoices, Arrangement = n,
                    PaletteMember = ChoicePalettes, PaletteRow = 0, PaletteRows = 5,
                });
            list.Add(new BottomScreenPiece
            {
                Name = "Answer frame",
                Archive = DirNames.FieldTouchChoices, Drawing = CursorDrawing, Cells = CursorCells,
                PaletteMember = CursorPalette, PaletteRow = 0,
            });
        }

        private static void AppPieces(List<BottomScreenPiece> list, PoketchApps.App a, int row)
        {
            if (a.ReadOnlyBecause != null)
            {
                // Its colours still show, as the theme is the only thing about this screen that can change.
                list.Add(new BottomScreenPiece
                {
                    Name = a.Name + " colours", What = a.ReadOnlyBecause, Archive = DirNames.Poketch,
                    PaletteMember = ThemePalettes, PaletteRow = row,
                    Warning = a.ReadOnlyBecause,
                });
                return;
            }

            if (a.Tiles >= 0)
                list.Add(new BottomScreenPiece
                {
                    Name = "Screen",
                    What = $"{a.Name}'s own screen, {a.TilesUsed} of {PoketchApps.TileCeiling} tiles.",
                    Archive = DirNames.Poketch, Drawing = a.Tiles, Arrangement = a.Arrangement,
                    PaletteMember = ThemePalettes, PaletteRow = row, Warning = a.Warning,
                    TileRoom = a.TileRoom,
                });
            if (a.Sprites >= 0)
                list.Add(new BottomScreenPiece
                {
                    Name = "Sprites",
                    What = $"The moving pieces, {a.SpriteTilesUsed} of {PoketchApps.TileCeiling} sprite tiles"
                         + (a.UsesDigitSheet ? ", counting the shared figures." : "."),
                    Archive = DirNames.Poketch, Drawing = a.Sprites,
                    PaletteMember = ThemePalettes, PaletteRow = row,
                    TileRoom = a.SpriteRoom,
                });
            if (a.Cells >= 0)
                list.Add(new BottomScreenPiece
                {
                    Name = "Sprite positions",
                    Archive = DirNames.Poketch, Cells = a.Cells,
                    Animation = a.Animation,
                    Sprites = a.Sprites,
                    SharedSheet = PoketchApps.SharedSheetFor(a),
                    PaletteMember = ThemePalettes, PaletteRow = row,

                    // Pieces move in the animation editor. Where the whole sprite sits is fixed in game code.
                    ReadOnlyBecause = a.Animation >= 0 ? null
                        : "These positions are read from the file, but this screen has no animation to "
                        + "open them in.",
                });
            if (a.Animation >= 0)
                list.Add(new BottomScreenPiece
                {
                    Name = "Animation",
                    What = PoketchApps.AnimationsWithTransforms.Contains(a.Animation)
                         ? "These frames carry a turn or a stretch."
                         : null,
                    Archive = DirNames.Poketch, Animation = a.Animation,
                    Cells = a.Cells, Drawing = -1,
                    PaletteMember = ThemePalettes, PaletteRow = row,
                    Sprites = a.Sprites,
                    SharedSheet = PoketchApps.SharedSheetFor(a),
                });
        }

        private static void FramePieces(List<BottomScreenPiece> list, bool female, int row)
        {
            list.Add(new BottomScreenPiece
            {
                Name = "Casing",
                What = "Row 0 is the girl's, row 1 the boy's.",
                Archive = DirNames.Poketch, Drawing = CaseTiles, Arrangement = CaseMap,
                PaletteMember = CasePalettes, PaletteRow = female ? 0 : 1,
            });
            list.Add(new BottomScreenPiece
            {
                Name = "Watch face",
                Archive = DirNames.Poketch, Drawing = WatchTiles, Arrangement = WatchMap,
                PaletteMember = ThemePalettes, PaletteRow = row,
            });
            list.Add(new BottomScreenPiece
            {
                Name = "Watch digits",
                Archive = DirNames.Poketch, Arrangement = WatchDigits,
                PaletteMember = ThemePalettes, PaletteRow = row,
            });
            list.Add(new BottomScreenPiece
            {
                Name = "Before the Pokétch",
                What = "Shown until the player is given a Pokétch.",
                Archive = DirNames.Poketch, Drawing = UnavailableTiles, Arrangement = UnavailableMap,
                PaletteMember = UnavailablePalette, PaletteRow = 0,
                Warning = "Also shown by " + Join(PoketchApps.SharedBy(UnavailablePalette).Skip(1).ToList()) + ".",
            });
        }

        /// <summary>Notes which pieces are painted from the same row of the same file.</summary>
        public static void NoteSharing(List<BottomScreenPiece> pieces)
        {
            foreach (BottomScreenPiece p in pieces)
            {
                if (p.PaletteMember < 0) { p.SharedWith = null; continue; }
                List<string> others = pieces.Where(o => !ReferenceEquals(o, p)
                                            && o.Archive == p.Archive
                                            && o.PaletteMember == p.PaletteMember
                                            && Overlaps(o, p))
                                   .Select(o => o.Name).ToList();
                p.SharedWith = others.Count == 0 ? null
                    : "These colours are shared with " + Join(others) + ", so editing them changes those too.";
            }
        }

        private static bool Overlaps(BottomScreenPiece a, BottomScreenPiece b) =>
            a.PaletteRow < b.PaletteRow + b.PaletteRows && b.PaletteRow < a.PaletteRow + a.PaletteRows;

        private static string Join(List<string> names) =>
            names.Count == 1 ? names[0]
                             : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];
    }
}

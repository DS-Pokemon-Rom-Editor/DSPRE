using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.Avalonia.Data
{
    /// <summary>Pictures for the touch tabs whose screens have a separate renderer: the battle touch screen and the Pokétch.</summary>
    public static class TouchTabPictures
    {
        // BattleCommandButtons page order; the first battle and Pal Park show the Command panel.
        private static readonly TouchMenu[] Panels =
            { TouchMenu.Command, TouchMenu.Fight, TouchMenu.Target, TouchMenu.YesNo, TouchMenu.Command, TouchMenu.Command, TouchMenu.Playback };

        private const int PalParkPage = 5;

        // pokeplatinum battle_subscreen.c clears every button under FIGHT in the first battle, and the buttons beside RUN in Pal Park.
        private static readonly (int X, int Y, int W, int H)[][] Blanks =
            { null, null, null, null, new[] { (0, 16, 32, 8) }, new[] { (0, 16, 10, 8), (22, 16, 10, 8) }, null };

        /// <summary>The battle touch screen page with its labels at the spots <paramref name="textOf"/> gives.</summary>
        public static byte[] Battle(int page, Func<int, int, IReadOnlyList<(int X, int Y)>> textOf,
                                    (ROMFiles.BattlePressFrames.Rect Rect, int Page)? pressed = null, bool frontier = false,
                                    ROMFiles.FightIconView icon = null)
        {
            if (page < 0 || page >= Panels.Length) return null;
            BattleScreenRenderer.Options look = new BattleScreenRenderer.Options
            {
                Menu = Panels[page], BlankedCommandTiles = Blanks[page], NoSilhouette = page == PalParkPage,
                Pressed = pressed, Frontier = frontier,
            };
            byte[] rgba = BattleScreenRenderer.Flatten(new BattleScreenRenderer().BuildTouch(look), touch: true);
            if (textOf != null) GameTextScreens.BattleTouchLabels(rgba, page, textOf, icon);
            return rgba;
        }

        /// <summary>
        /// The battle's top screen above the touch menus: backdrop, ground, the HP bars as the ROM has them, and the "What will" prompt in
        /// the message box.
        /// </summary>
        public static byte[] BattleTop()
        {
            BattleScreenRenderer.Options look = new BattleScreenRenderer.Options();
            byte[] top = BattleScreenRenderer.Flatten(new BattleScreenRenderer().Build(look), touch: false);
            if (top == null || RomInfo.BattleStringsTextNumber < 0) return top;
            // The line's one placeholder is the nickname of the Pokémon on the bar.
            List<string> lines = new ROMFiles.TextArchive(RomInfo.BattleStringsTextNumber).Messages;
            if (RomInfo.BattleWhatWillPokemonDoLine < lines.Count)
                BattleMessageText.Draw(top, lines[RomInfo.BattleWhatWillPokemonDoLine], (_, _, _) => look.PokemonName, 0);
            return top;
        }

        /// <summary>Draws Platinum's Pokétch with the named app, or the digital watch when app is null.</summary>
        public static byte[] Poketch(string app)
        {
            PoketchScreen screen = PoketchScreen.Load();
            if (screen == null) return null;
            PoketchApps.App shown = PoketchApps.All.FirstOrDefault(a => string.Equals(a.Name, app ?? "Digital watch", StringComparison.OrdinalIgnoreCase));
            if (shown == null || shown.Tiles < 0) return screen.RenderUnavailable();
            return screen.RenderApp(false, 0, false, shown.Tiles, shown.Arrangement, shown.Sprites, shown.Cells, 0, shown.SpriteSlots, null, null, shown.BgLayer);
        }
    }
}

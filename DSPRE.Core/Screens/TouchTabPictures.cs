using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.Screens
{
    /// <summary>Pictures for the touch tabs whose screens have a separate renderer: the battle touch screen and the Pokétch.</summary>
    public static class TouchTabPictures
    {
        // BattleCommandButtons page order; the first battle and Pal Park show the Command panel.
        private static readonly TouchMenu[] Panels =
            { TouchMenu.Command, TouchMenu.Fight, TouchMenu.Target, TouchMenu.YesNo, TouchMenu.Command, TouchMenu.Command, TouchMenu.Playback };

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
            if (page == 0) PartyBalls(rgba);
            return rgba;
        }

        // pokeplatinum battle_subscreen.c BattleSubscreen_LoadSprites (pokeheartgold battle_input.c alike): the party's balls at 12 + 19i, 13 (batt_obj 208/207/209) and a
        // trainer's at 246 - 12i, 9 (205/204/206), coloured by rows 0 and 1 of file 72. GetBallStatusAnimID: empty 0, alive 1, a status
        // condition 2, fainted 3. The sample trainer has three Pokémon.
        private static void PartyBalls(byte[] rgba)
        {
            if (RomInfo.GameFamily is not (RomInfo.GameFamilies.Plat or RomInfo.GameFamilies.HGSS) || MoveTextScreens.Members(RomInfo.DirNames.BattleObj) is not Func<int, byte[]> m) return;
            ushort[] colours = ROMFiles.NitroBgCodec.ReadColours(m(72));
            if (colours == null) return;
            for (int i = 0; i < 6; i++)
            {
                PartySample.Pokemon who = PartySample.Party[i];
                int status = who.Empty || who.Egg ? 0 : who.Hp == 0 ? 3 : who.Condition > 0 ? 2 : 1;
                GameTextScreens.Sprite(rgba, m, 209, 207, 208, 12 + 19 * i, 13, status, _ => DsBgScreen.Row(colours, 0));
                GameTextScreens.Sprite(rgba, m, 206, 204, 205, 246 - 12 * i, 9, i < 3 ? 1 : 0, _ => DsBgScreen.Row(colours, 1));
            }
        }

        /// <summary>
        /// The battle's top screen above the touch menus: backdrop, ground, the HP bars as the ROM has them, and the "What will" prompt in
        /// the message box.
        /// </summary>
        public static byte[] BattleTop() => BattleTop(0);

        /// <summary>The top screen behind a battle touch page: its prompt, or none while a recorded battle plays.</summary>
        public static byte[] BattleTop(int page)
        {
            BattleScreenRenderer.Options look = new BattleScreenRenderer.Options();
            byte[] top = BattleScreenRenderer.Flatten(new BattleScreenRenderer().Build(look), touch: false);
            if (top == null || RomInfo.BattleStringsTextNumber < 0 || page == RecordedPage) return top;
            int line = page switch
            {
                YesNoPage => RomInfo.BattleUseNextPokemonLine,
                PalParkPage => RomInfo.BattleWhatWillPlayerThrowLine,
                _ => RomInfo.BattleWhatWillPokemonDoLine,
            };
            // The one placeholder is the Pokémon's nickname, or the player's name when throwing.
            List<string> lines = new ROMFiles.TextArchive(RomInfo.BattleStringsTextNumber).Messages;
            if (line < lines.Count)
                BattleMessageText.Draw(top, lines[line], (_, _, _) => page == PalParkPage ? "PLAYER" : look.PokemonName, 0);
            return top;
        }

        // BattleCommandButtons page order.
        private const int YesNoPage = 3, PalParkPage = 5, RecordedPage = 6;

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

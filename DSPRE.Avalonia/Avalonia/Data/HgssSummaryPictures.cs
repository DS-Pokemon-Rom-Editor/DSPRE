using System;
using System.Collections.Generic;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Data
{
    /// <summary>The HGSS summary's bottom screen, for its touch area tab.</summary>
    public static class HgssSummaryPictures
    {
        // pokeheartgold sub_020887C4 draws sub BG2 from colours 0, tiles 1 and the page's screen; DSPRE leaves out the BG1 move panel.
        // Screens per SummaryTouchButtons page: Info, Stats and moves, Stats and moves, Info, Ribbons.
        private static readonly int[] PageScreens = { 12, 17, 17, 12, 9 };
        private const int Colours = 0, PageDrawing = 1;

        /// <summary>The page's pixels with the party icons at <paramref name="icons"/> on the tabs page, or null when the archive can't be read.</summary>
        public static byte[] Draw(int page, IReadOnlyList<(int X, int Y)> icons = null)
        {
            try
            {
                Func<int, byte[]> m = MoveTextScreens.Members(RomInfo.DirNames.SummaryGraphics);
                if (m == null || page < 0 || page >= PageScreens.Length) return null;
                DsBgScreen s = new DsBgScreen();
                ushort[] colours = NitroBgCodec.ReadColours(m(Colours));
                s.SetPalettes(colours);
                s.LoadTiles(0x10000, m(PageDrawing));
                s.InitLayer(2, 2, 0x10000);
                (int w, ushort[] map) = NitroBgCodec.ReadMap(m(PageScreens[page]));
                s.LoadMap(2, map, w);
                byte[] rgba = s.Render();
                if (page == 0 && icons != null) GameTextScreens.PartyIcons(rgba, icons);
                return rgba;
            }
            catch (Exception ex) { AppLogger.WarnOnce("HgssSummaryPictures.Draw", ex); return null; }
        }
    }
}

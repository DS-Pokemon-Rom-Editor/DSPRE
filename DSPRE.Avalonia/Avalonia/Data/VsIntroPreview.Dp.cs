using System;

namespace DSPRE.Avalonia.Data
{
    public sealed partial class VsIntroPreview
    {
        // Diamond and Pearl cut each face as 64 by 64 pixels from the class's battle sprite (pokediamond
        // ov05_021E4080) and draw it at double size.
        public const int DpCutSize = 64;
        private const int DpGymFaceY = 68, DpGymFaceBottom = 98;
        private const int DpPlayerX = 46, DpPlayerY = 114, DpOpponentX = 207, DpOpponentY = 68;
        // Each banner sits this far from its face, the player's to the right and the opponent's to the left.
        private const int DpFrameDx = 16, DpFrameDy = 36;

        private byte[] DpGym(Scene s)
        {
            byte[] rgba = FieldGrid();
            (byte[] Rgba, int Width, int Height)? banner = WideScreen(s.Banner);
            if (banner != null) Layer(rgba, banner.Value, 0);
            DrawCut(rgba, s.Cut, s.EndX, DpGymFaceY, DpGymFaceBottom);
            return rgba;
        }

        private byte[] DpLeague(Scene s)
        {
            byte[] rgba = FieldGrid();
            if (s.Frame != null)
            {
                ushort[] colours = DsBgScreen.ReadColours(_member(s.FramePalette));
                int settled = 1 << 12;
                DrawSprite(Sprite(new[] { -1, s.Frame[0], s.Frame[1] }, colours, rows: true, cell: AnimCell(s.FrameAnim, 0, settled)),
                    rgba, DpPlayerX + DpFrameDx, DpPlayerY + DpFrameDy);
                DrawSprite(Sprite(new[] { -1, s.Frame[0], s.Frame[1] }, colours, rows: true, cell: AnimCell(s.FrameAnim, 1, settled)),
                    rgba, DpOpponentX - DpFrameDx, DpOpponentY + DpFrameDy);
            }
            DrawCut(rgba, s.PlayerCut, DpPlayerX, DpPlayerY, DsBgScreen.Height);
            DrawCut(rgba, s.Cut, DpOpponentX, DpOpponentY, DsBgScreen.Height);
            return rgba;
        }

        private static void DrawCut(byte[] rgba, byte[] cut, int centreX, int centreY, int bottom)
        {
            if (cut == null || cut.Length < DpCutSize * DpCutSize * 4) return;
            int w = DsBgScreen.Width, left = centreX - DpCutSize, top = centreY - DpCutSize;
            for (int y = Math.Max(0, top); y < Math.Min(bottom, top + 2 * DpCutSize); y++)
                for (int x = Math.Max(0, left); x < Math.Min(w, left + 2 * DpCutSize); x++)
                {
                    int from = (((y - top) / 2) * DpCutSize + (x - left) / 2) * 4;
                    if (cut[from + 3] == 0) continue;
                    Array.Copy(cut, from, rgba, (y * w + x) * 4, 4);
                }
        }
    }
}

using System;
using System.Collections.Generic;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Data
{
    /// <summary>Battle text written into the battle's message box, in the talk font.</summary>
    public static class BattleMessageText
    {
        // The talk font's ink and shadow, in 15-bit colour.
        private const ushort Letter = 5 | 5 << 5 | 5 << 10, Shadow = 18 | 19 << 5 | 21 << 10;

        /// <summary>Writes one box's worth of <paramref name="text"/> onto a 256 by 192 top screen and returns how many
        /// boxes the whole text takes.</summary>
        public static int Draw(byte[] screen, string text, Func<int, int, int, string> valueFor, int stop = 0)
        {
            FieldFont font = Views.Controls.FieldMessageBoxView.Font ??= FieldFont.LoadTalkFont();
            string shown = FieldStringVars.Expand(text, valueFor);
            List<FieldMessageFrame> frames = FieldMessageScript.Frames(shown, Views.Controls.FieldMessageBoxView.Measure);
            IReadOnlyList<string> lines = frames.Count > 0 ? frames[Math.Max(0, stop) % frames.Count].Lines : Array.Empty<string>();
            for (int i = 0; i < lines.Count; i++)
                DsBgScreen.DrawText(screen, font, lines[i], FieldMessageWindow.TextLeft,
                                    FieldMessageWindow.TextTop + i * FieldMessageWindow.LineHeight, Letter, Shadow);
            return Math.Max(1, frames.Count);
        }

        /// <summary>The move's "X used Y!" line in its message box, on an otherwise clear 256 by 192 picture; null when
        /// this ROM's line or letters can't be read.</summary>
        /// <param name="foe">The opponent's Pokémon is using the move; otherwise yours.</param>
        public static byte[] MoveUsed(int move, bool foe, string pokemon, int windowStyle)
        {
            if (RomInfo.MovesUsedTextNumber < 0 || move < 0 || !FieldFontCharacters.Ready) return null;
            List<string> lines;
            try { lines = new TextArchive(RomInfo.MovesUsedTextNumber).Messages; }
            catch (Exception ex) { AppLogger.WarnOnce("BattleMessageText.MoveUsed", ex); return null; }
            // Three lines a move: your Pokémon, a wild one, the foe's.
            int line = move * 3 + (foe ? 2 : 0);
            if (lines == null || line >= lines.Count) return null;
            BattleScreenRenderer.Piece box = BattleScreenRenderer.BuildMessageBox(windowStyle);
            if (box.Rgba == null) return null;
            byte[] screen = new byte[256 * 192 * 4];
            for (int y = 0; y < box.Height && box.Top + y < 192; y++)
                for (int x = 0; x < box.Width && box.Left + x < 256; x++)
                    Array.Copy(box.Rgba, (y * box.Width + x) * 4, screen, ((box.Top + y) * 256 + box.Left + x) * 4, 4);
            // The move's line has one word to fill, the Pokémon's name.
            Draw(screen, lines[line], (a, b, c) => pokemon);
            return screen;
        }
    }
}

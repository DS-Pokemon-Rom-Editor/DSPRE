using System;
using System.Collections.Generic;
using DSPRE.ROMFiles;

namespace DSPRE.Screens
{
    /// <summary>Draws the HGSS touch menu and Poké Ball screen, reading the ROM once.</summary>
    internal static class HgssTouchPictures
    {
        private static readonly object Gate = new object();
        private static string _rom;
        private static HgssTouchScreen _screen;
        private static List<string> _lines;
        private static readonly Dictionary<int, FieldFont> Fonts = new();

        /// <summary>Call after the font archive is saved; the next picture reads it again.</summary>
        public static void Forget() { lock (Gate) _rom = null; }

        public static void ForgetText() { lock (Gate) _lines = null; }

        public static HgssTouchScreen Screen()
        {
            lock (Gate)
            {
                ForThisRom();
                return _screen ??= HgssTouchScreen.Load();
            }
        }

        public static FieldFont Font(int entry)
        {
            lock (Gate)
            {
                ForThisRom();
                if (Fonts.TryGetValue(entry, out FieldFont known)) return known;
                FieldFont font = null;
                try { font = FieldFont.LoadFromArchive(entry); }
                catch (Exception ex) { AppLogger.Warn("Touch screen pictures: font: " + ex.Message); }
                Fonts[entry] = font;
                return font;
            }
        }

        public static byte[] Menu(IReadOnlyList<(int X, int Y)> sprites, IReadOnlyList<(int X, int Y)> labels, int mode = 0,
                                  FieldTouchMenuButtons.MenuExtras extras = null)
        {
            lock (Gate)
            {
                HgssTouchScreen screen = Screen();
                if (screen == null) return null;
                FieldFont font = Font(HgssTouchScreen.FontEntry) ?? FieldFont.LoadSystemFont();
                FieldFont iconFont = Font(HgssTouchScreen.IconFontEntry) ?? font;
                string Text(int m) => FieldStringVars.Expand(Line(m), GameTextScreens.SampleWord);
                return screen.RenderMenu(font, iconFont, Text,
                    new HgssTouchScreen.MenuLook { RegisteredItems = mode == 0, Sprites = sprites, Labels = labels, Mode = mode, Extras = extras });
            }
        }

        public static byte[] Choices(IReadOnlyList<string> labels, bool yesNo, IReadOnlyList<(int X, int Y, int Width, int Height)> windows)
        {
            lock (Gate)
                return Screen()?.RenderChoices(Font(HgssTouchScreen.FontEntry) ?? FieldFont.LoadSystemFont(), labels, yesNo, 0, false, windows);
        }

        private static string Line(int line)
        {
            if (_lines == null)
            {
                try { _lines = new TextArchive(RomInfo.FieldTouchMenuTextArchive).Messages; }
                catch (Exception ex) { AppLogger.WarnOnce("HgssTouchPictures.Line", ex); }
                _lines ??= new List<string>();
            }
            return line >= 0 && line < _lines.Count ? _lines[line] : "";
        }

        // Caches last one load; called under Gate.
        private static void ForThisRom()
        {
            if (_rom == ProjectSession.LoadId) return;
            _rom = ProjectSession.LoadId;
            _screen = null;
            _lines = null;
            Fonts.Clear();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Data
{
    /// <summary>The window layouts the screen previews use: the ROM's, or a Layout view's inside <see cref="Use"/>.</summary>
    public static class ScreenWindowLayouts
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ScreenWindowLayout> Read = new();
        private static string _rom;

        // A Layout view's unsaved layout and panels, used instead of the ROM's on this thread.
        [ThreadStatic] private static (string Screen, ScreenWindowLayout Layout, PartyPanelLayout Panels) _using;

        /// <summary>Previews drawn on this thread use these until the result is disposed.</summary>
        public static IDisposable Use(string screen, ScreenWindowLayout layout, PartyPanelLayout panels)
        {
            (string, ScreenWindowLayout, PartyPanelLayout) before = _using;
            _using = (screen, layout, panels);
            return new Restore(before);
        }

        private sealed class Restore : IDisposable
        {
            private readonly (string, ScreenWindowLayout, PartyPanelLayout) _before;
            public Restore((string, ScreenWindowLayout, PartyPanelLayout) before) { _before = before; }
            public void Dispose() => _using = _before;
        }

        public static ScreenWindowLayout For(string screen)
        {
            if (_using.Layout != null && _using.Screen == screen) return _using.Layout;
            if (_rom != RomInfo.WorkDir) { _rom = RomInfo.WorkDir; Read.Clear(); }
            if (!Read.TryGetValue(screen, out ScreenWindowLayout read)) Read[screen] = read = ScreenWindowLayout.Read(screen);
            return read;
        }

        /// <summary>Clears the cache after a save so the next preview reads it again.</summary>
        public static void Forget(string screen)
        {
            Read.TryRemove(screen, out _);
            if (screen == "party") _panelsRom = _fontsRom = null;
        }

        // A Layout view's unsaved party fonts, used instead of the ROM's on this thread.
        [ThreadStatic] private static IReadOnlyDictionary<string, int> _usingFonts;
        private static Dictionary<string, int> _readFonts;
        private static string _fontsRom;

        /// <summary>Previews drawn on this thread use these party fonts until the result is disposed.</summary>
        public static IDisposable UseFonts(IReadOnlyDictionary<string, int> fonts)
        {
            IReadOnlyDictionary<string, int> before = _usingFonts;
            _usingFonts = fonts;
            return new FontsRestore(before);
        }

        private sealed class FontsRestore : IDisposable
        {
            private readonly IReadOnlyDictionary<string, int> _before;
            public FontsRestore(IReadOnlyDictionary<string, int> before) { _before = before; }
            public void Dispose() => _usingFonts = _before;
        }

        /// <summary>The font number a party window part prints in, or 0 (the game's font) when the patch hasn't set one.</summary>
        public static int PartyFont(string part)
        {
            IReadOnlyDictionary<string, int> fonts = _usingFonts;
            if (fonts == null)
            {
                if (_fontsRom != RomInfo.WorkDir)
                {
                    _fontsRom = RomInfo.WorkDir;
                    try { _readFonts = ROMFiles.PartyFontPatch.FontsIn(ROMFiles.PartyFontPatch.ReadTable(), For("party")?.Windows ?? new List<ScreenWindowLayout.Window>()); }
                    catch (Exception ex) { AppLogger.WarnOnce("ScreenWindowLayouts.PartyFont", ex); _readFonts = null; }
                }
                fonts = _readFonts;
            }
            return fonts != null && fonts.TryGetValue(part, out int font) && font >= 0 ? font : 0;
        }

        private static PartyPanelLayout _readPanels;
        private static string _panelsRom;

        /// <summary>The ROM's panels, or a Layout view's inside <see cref="Use"/>; null when unknown.</summary>
        public static PartyPanelLayout Panels()
        {
            if (_using.Panels != null) return _using.Panels;
            if (_panelsRom != RomInfo.WorkDir) { _panelsRom = RomInfo.WorkDir; _readPanels = PartyPanelLayout.Read(); }
            return _readPanels;
        }

        /// <summary>In tiles; <paramref name="fallback"/> when the layout isn't known.</summary>
        public static (int X, int Y, int W, int H) Window(string screen, string name, (int X, int Y, int W, int H) fallback)
        {
            ScreenWindowLayout.Window w = For(screen)?.Windows.FirstOrDefault(x => x.Name == name);
            return w == null ? fallback : (w.X, w.Y, w.Width, w.Height);
        }

        /// <summary>Both screens with their lines, for the Layout preview. Either is null when it isn't drawn.</summary>
        public static (byte[] Top, byte[] Bottom) Draw(string screen, int state = -1)
        {
            GameTextScreens.Screen text = GameTextScreens.Screens.FirstOrDefault(s => s.Key == screen);
            if (text == null) return (null, null);
            int bank = text.Banks().FirstOrDefault(-1);
            if (bank < 0) return (null, null);
            List<string> lines;
            try { lines = new TextArchive(bank).Messages; } catch (Exception ex) { AppLogger.WarnOnce("ScreenWindowLayouts.Draw", ex); return (null, null); }
            string Line(int b, int l)
            {
                if (b != bank)
                {
                    try { List<string> other = new TextArchive(b).Messages; return l >= 0 && l < other.Count ? other[l] : ""; }
                    catch (Exception ex) { AppLogger.WarnOnce("ScreenWindowLayouts.Draw.Line", ex); return ""; }
                }
                return l >= 0 && l < lines.Count ? lines[l] : "";
            }
            int first = Math.Clamp(text.FirstLine, 0, Math.Max(0, lines.Count - 1));
            // A Layout-only state is drawn as the nearest real one.
            int drawn = state < 0 || text.States == null ? -1 : text.DrawnAs?.Invoke(state) ?? Math.Min(state, text.States.Length - 1);
            GameTextScreens.Shot shot = GameTextScreens.Draw(text, bank, first, Line(bank, first), Line, 0, drawn);
            GameTextScreens.Shot other = GameTextScreens.DrawOther(text, Math.Max(0, drawn), Line);
            bool shotOnTop = shot?.Name?.EndsWith("top", StringComparison.Ordinal) == true;
            byte[] main = shot?.Rgba, second = other?.Rgba;
            return shotOnTop ? (main, second) : (second, main);
        }
    }
}

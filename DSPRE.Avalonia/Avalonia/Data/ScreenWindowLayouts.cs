using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// The window layouts the screen previews draw their text in: the ROM's, or inside <see cref="Use"/> a Layout view's.
    /// </summary>
    public static class ScreenWindowLayouts
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ScreenWindowLayout> Read = new();
        private static string _rom;

        // The layout and panels drawn on this thread in place of the ROM's, as a Layout view's unsaved edits.
        [ThreadStatic] private static (string Screen, ScreenWindowLayout Layout, PartyPanelLayout Panels) _using;

        /// <summary>Previews drawn on this thread use <paramref name="layout"/> and <paramref name="panels"/> until the result is disposed.</summary>
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

        /// <summary>Forgets what was read, after a save, so the next preview reads it again.</summary>
        public static void Forget(string screen)
        {
            Read.TryRemove(screen, out _);
            if (screen == "party") _panelsRom = null;
        }

        private static PartyPanelLayout _readPanels;
        private static string _panelsRom;

        /// <summary>The party panels the previews draw: the ROM's, or inside <see cref="Use"/> a Layout view's; null when unknown.</summary>
        public static PartyPanelLayout Panels()
        {
            if (_using.Panels != null) return _using.Panels;
            if (_panelsRom != RomInfo.WorkDir) { _panelsRom = RomInfo.WorkDir; _readPanels = PartyPanelLayout.Read(); }
            return _readPanels;
        }

        /// <summary>A window's place and size in tiles, or <paramref name="fallback"/> when the layout isn't known.</summary>
        public static (int X, int Y, int W, int H) Window(string screen, string name, (int X, int Y, int W, int H) fallback)
        {
            ScreenWindowLayout.Window w = For(screen)?.Windows.FirstOrDefault(x => x.Name == name);
            return w == null ? fallback : (w.X, w.Y, w.Width, w.Height);
        }

        /// <summary>
        /// Both screens in a state with the lines they print, for the Layout view's preview: the one the screen's shots show
        /// and, where the screen draws one, the other. Either is null when it isn't drawn.
        /// </summary>
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
            // A state only the layout has (a multi battle's Pokémon, a menu with field moves) is drawn as the nearest real one.
            int drawn = state < 0 || text.States == null ? -1 : text.DrawnAs?.Invoke(state) ?? Math.Min(state, text.States.Length - 1);
            GameTextScreens.Shot shot = GameTextScreens.Draw(text, bank, first, Line(bank, first), Line, 0, drawn);
            GameTextScreens.Shot other = GameTextScreens.DrawOther(text, Math.Max(0, drawn), Line);
            bool shotOnTop = shot?.Name?.EndsWith("top", StringComparison.Ordinal) == true;
            byte[] main = shot?.Rgba, second = other?.Rgba;
            return shotOnTop ? (main, second) : (second, main);
        }
    }
}

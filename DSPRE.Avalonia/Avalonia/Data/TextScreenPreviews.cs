using System;
using System.Collections.Generic;
using System.Linq;
using global::Avalonia.Media.Imaging;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Data
{
    /// <summary>A screen drawn with a text line on it.</summary>
    public sealed class TextScreenShot
    {
        public string Name { get; init; }
        public Bitmap Picture { get; init; }
        /// <summary>Opens the editor for these graphics at this page.</summary>
        public Action Open { get; init; }
        public string EditorName { get; init; }
        public bool HasEditor => Open != null;
        public string OpenText => EditorName != null ? "Open in " + EditorName : "Open";
        public int Stops { get; init; } = 1;
        /// <summary>True when the game moves to the next box on its own.</summary>
        public bool MovesOnByItself { get; init; }
    }

    /// <summary>
    /// Finds and draws the Pokédex and summary pages that print a text line, with the edit shown. Which archives each page reads is learned
    /// once per ROM.
    /// </summary>
    public static class TextScreenPreviews
    {
        private static readonly Func<int, ScreenBook>[] Books = { species => new PokedexBook(species), species => new SummaryBook(species) };

        private static readonly MoveTextScreens MoveScreens = new MoveTextScreens();

        // The Move data editor's move and item screens.
        private static void AddMoveTextScreens(List<TextScreenShot> shots, int bank, int line, string text)
        {
            bool hg = RomInfo.GameFamily == RomInfo.GameFamilies.HGSS;
            List<(MoveTextScreens.Screen Screen, string Name, bool Top)> screens = new();
            if (bank == RomInfo.MoveDescriptionsTextNumbers)
            {
                screens.Add((MoveTextScreens.Screen.Battle, "Battle party graphics", false));
                screens.Add((MoveTextScreens.Screen.Relearner, "Move Relearner graphics", !hg));
            }
            if (bank == RomInfo.ItemDescriptionsTextNumber) screens.Add((MoveTextScreens.Screen.Bag, "Bag graphics", true));
            if (screens.Count == 0) return;

            FieldFont font = Font(0);
            if (font == null) return;
            string shown = text.Replace("\\n", "\n").Replace("\\r", "\n").Replace("\\f", "\n");
            bool move = bank == RomInfo.MoveDescriptionsTextNumbers;
            foreach ((MoveTextScreens.Screen screen, string name, bool top) in screens)
            {
                byte[] rgba = MoveScreens.Render(screen, shown, font);
                if (rgba == null) continue;
                shots.Add(new TextScreenShot
                {
                    Name = $"{name}: {(top ? "top" : "bottom")}",
                    Picture = ImageConverter.FromRgba(rgba, 256, 192),
                    Open = move ? () => AvaloniaEditorLauncher.OpenMoveDataEditor(line) : () => AvaloniaEditorLauncher.OpenItemEditor(line),
                    EditorName = move ? "Move data editor" : "Item editor",
                });
            }
        }

        // The yes/no box and script menus, drawn over a plain screen since the map behind them varies.
        private static void AddMenuWindows(List<TextScreenShot> shots, int bank, int line, string text)
        {
            if (bank == RomInfo.FieldMenuEntriesArchive && (line == RomInfo.FieldYesEntry || line == RomInfo.FieldNoEntry))
            {
                List<string> yesNo = new List<string>
                {
                    MenuLabel(line == RomInfo.FieldYesEntry ? text : ArchiveLine(bank, RomInfo.FieldYesEntry)),
                    MenuLabel(line == RomInfo.FieldNoEntry ? text : ArchiveLine(bank, RomInfo.FieldNoEntry)),
                };
                byte[] window = Views.Controls.FieldMenuWindowView.PaintRgba(yesNo, 6, 4, 8, 0, 0, 0, out int w, out int h);
                shots.Add(new TextScreenShot { Name = "Yes/no box: top", Picture = OnScreen(window, w, h, 25, 13) });
            }

            HashSet<string> drawn = new HashSet<string>();
            foreach (ScriptMenus.Menu menu in ScriptMenus.Showing(bank, line))
            {
                List<string> labels = menu.Entries.Select(e => MenuLabel(e == line ? text : ArchiveLine(bank, e))).ToList();
                if (!drawn.Add($"{menu.X},{menu.Y},{menu.IsList},{menu.OnTouchScreen}|{string.Join("|", labels)}")) continue;
                int file = menu.ScriptFile;

                if (menu.OnTouchScreen)
                {
                    // HGSS's Poké Ball screen holds 2 to 8 entries.
                    _touch ??= HgssTouchScreen.Load();
                    byte[] choices = _touch?.RenderChoices(Font(HgssTouchScreen.FontEntry) ?? FieldFont.LoadSystemFont(), labels, false,
                                                           Math.Clamp(menu.Cursor, 0, labels.Count - 1), true);
                    if (choices == null) continue;
                    shots.Add(new TextScreenShot
                    {
                        Name = $"Script {file} touch menu: bottom",
                        Picture = ImageConverter.FromRgba(choices, 256, 192),
                        Open = () => AvaloniaEditorLauncher.OpenScriptEditor(file),
                        EditorName = "Script editor",
                    });
                    continue;
                }

                List<string> rows = menu.IsList ? labels.Take(ListRows).ToList() : labels;
                int wide = Views.Controls.FieldMenuWindowView.WidthInTiles(labels);
                int high = rows.Count * 2;
                int cursor = Math.Clamp(menu.Cursor, 0, rows.Count - 1);
                byte[] window = Views.Controls.FieldMenuWindowView.PaintRgba(rows, wide, high, menu.IsList ? 12 : 11, menu.IsList ? 1 : 0,
                    menu.IsList ? 2 : 0, cursor, out int w, out int h);
                shots.Add(new TextScreenShot
                {
                    Name = $"Script {file} {(menu.IsList ? "list" : "menu")}: top",
                    Picture = OnScreen(window, w, h, menu.X, menu.Y),
                    Open = () => AvaloniaEditorLauncher.OpenScriptEditor(file),
                    EditorName = "Script editor",
                });
            }
        }

        private static BattleScreenRenderer _battle;

        // A battle message in the battle's box, and a name on the HP bars.
        private static void AddBattleScreens(List<TextScreenShot> shots, int bank, int line, string text, int stop)
        {
            bool message = bank == RomInfo.BattleStringsTextNumber;
            bool name = RomInfo.PokemonNamesTextNumbers?.Length > 0 && bank == RomInfo.PokemonNamesTextNumbers[0] && line > 0;
            if (!message && !name) return;

            int stops = 1;
            BattleScreenRenderer.Options options = new BattleScreenRenderer.Options { Menu = TouchMenu.Command };
            if (name) options.PokemonName = MenuLabel(text);
            _battle ??= new BattleScreenRenderer();
            byte[] top = BattleScreenRenderer.Flatten(_battle.Build(options), touch: false);
            if (message) stops = BattleMessageText.Draw(top, text, GameTextScreens.SampleWord, stop);
            shots.Add(new TextScreenShot
            {
                Name = "Battle screen graphics: top",
                // A battle message moves on by itself once read.
                Stops = stops,
                MovesOnByItself = stops > 1,
                Picture = ImageConverter.FromRgba(top, 256, 192),
                Open = AvaloniaEditorLauncher.OpenBattleScreenEditor,
                EditorName = "Battle screen",
            });
        }

        // The text entries the HGSS touch menu prints.
        private static readonly int[] TouchMenuMessages = { 0, 1, 2, 3, 4, 5, 8, 14, 32, 34, 35,
            HgssTouchScreen.MenuMessage, HgssTouchScreen.TalkMessage, HgssTouchScreen.CheckMessage,
            HgssTouchScreen.FishingMessage, HgssTouchScreen.NextMessage };

        private static HgssTouchScreen _touch;

        private static void AddTouchMenu(List<TextScreenShot> shots, int bank, int line, string text)
        {
            if (bank != RomInfo.FieldTouchMenuTextArchive || Array.IndexOf(TouchMenuMessages, line) < 0) return;
            _touch ??= HgssTouchScreen.Load();
            if (_touch == null) return;
            FieldFont font = Font(HgssTouchScreen.FontEntry) ?? FieldFont.LoadSystemFont();
            FieldFont iconFont = Font(HgssTouchScreen.IconFontEntry) ?? font;
            string Text(int m) => FieldStringVars.Expand(m == line ? text : ArchiveLine(bank, m),
                                                         GameTextScreens.SampleWord);
            int aLabel = Array.IndexOf(HgssTouchScreen.ALabelMessages, line) >= 0 ? line : HgssTouchScreen.CheckMessage;
            byte[] rgba = _touch.RenderMenu(font, iconFont, Text, new HgssTouchScreen.MenuLook { ALabel = aLabel, RegisteredItems = true });
            if (rgba == null) return;
            shots.Add(new TextScreenShot
            {
                Name = "Touch screen graphics: bottom",
                Picture = ImageConverter.FromRgba(rgba, 256, 192),
                Open = () => AvaloniaEditorLauncher.OpenBottomScreenEditor(),
                EditorName = "Bottom screen",
            });
        }

        private static void AddFieldWindows(List<TextScreenShot> shots, int bank, int line, string text)
        {
            HashSet<string> drawn = new HashSet<string>();
            foreach (FieldTextArchives.Window window in FieldTextArchives.WindowsFor(bank, line))
            {
                List<(string Text, int X, int Y, bool Right)> pieces = window.Text
                    .Select(p => (Shown(p.Line == line ? text : ArchiveLine(bank, p.Line)), p.X, p.Y, p.Right)).ToList();
                if (!drawn.Add(window.Name + "|" + string.Join("|", pieces.Select(p => p.Text)))) continue;
                byte[] rgba = Views.Controls.FieldMenuWindowView.PaintPlaced(window.TilesWide, window.TilesHigh, pieces, window.CursorOn,
                                                                             out int w, out int h);
                shots.Add(new TextScreenShot { Name = window.Name + ": top", Picture = OnScreen(rgba, w, h, window.TileLeft, window.TileTop) });
            }
        }

        // Fills placeholders with sample words and removes markup the window doesn't show.
        private static string Shown(string text)
        {
            string expanded = FieldStringVars.Expand(MenuLabel(text), GameTextScreens.SampleWord);
            return System.Text.RegularExpressions.Regex.Replace(expanded, @"\{[^}]*\}", "");
        }

        /// <summary>The rows a script list shows before it scrolls.</summary>
        private const int ListRows = 8;

        // A menu entry shows its first line only.
        private static string MenuLabel(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            int cut = text.Length;
            foreach (string stop in new[] { "\\n", "\\r", "\\f", "\n", "\r", "\f" })
            {
                int at = text.IndexOf(stop, StringComparison.Ordinal);
                if (at >= 0 && at < cut) cut = at;
            }
            return text.Substring(0, cut).Trim();
        }

        private static readonly Dictionary<int, List<string>> ArchiveLines = new();

        private static string ArchiveLine(int bank, int line)
        {
            if (!ArchiveLines.TryGetValue(bank, out List<string> lines))
            {
                try { lines = new TextArchive(bank).Messages; } catch (Exception ex) { AppLogger.WarnOnce("TextScreenPreviews.ArchiveLine", ex); lines = null; }
                ArchiveLines[bank] = lines;
            }
            return lines != null && line >= 0 && line < lines.Count ? lines[line] : "";
        }

        // A window on a plain top screen; left, top is its text area as the game places it.
        private static Bitmap OnScreen(byte[] window, int width, int height, int tileLeft, int tileTop)
        {
            const int screenW = 256, screenH = 192;
            byte[] screen = new byte[screenW * screenH * 4];
            for (int at = 0; at < screen.Length; at += 4) { screen[at] = 0x30; screen[at + 1] = 0x34; screen[at + 2] = 0x38; screen[at + 3] = 0xFF; }
            int left = (tileLeft - 1) * FieldMessageWindow.TileSize, top = (tileTop - 1) * FieldMessageWindow.TileSize;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int sx = left + x, sy = top + y;
                    if (sx < 0 || sy < 0 || sx >= screenW || sy >= screenH) continue;
                    int from = (y * width + x) * 4;
                    if (window[from + 3] == 0) continue;
                    int to = (sy * screenW + sx) * 4;
                    Array.Copy(window, from, screen, to, 4);
                }
            return ImageConverter.FromRgba(screen, screenW, screenH);
        }

        private static readonly object Gate = new object();
        private static string _rom;
        // The archives each book, page, state and side prints from.
        private static readonly Dictionary<(int Book, string Page, string Variant, ScreenSide Side), HashSet<int>> Reads = new();
        private static readonly Dictionary<int, FieldFont> Fonts = new();

        /// <summary>Reads everything again on the next preview, after the font archive was saved.</summary>
        public static void ForgetFonts() { lock (Gate) _rom = null; }

        public static List<TextScreenShot> For(int bank, int line, string text, int stop = 0)
        {
            List<TextScreenShot> shots = new List<TextScreenShot>();
            HashSet<long> footprints = new HashSet<long>();
            lock (Gate)
            {
                if (_rom != RomInfo.WorkDir) { _rom = RomInfo.WorkDir; Reads.Clear(); Fonts.Clear(); _speciesByAbility = null; _battle = null; _touch = null; }
                // Other lines may have been saved since the last check.
                ArchiveLines.Clear();
                int species = line >= 1 && line < SpeciesCount() ? line : 0;
                for (int b = 0; b < Books.Length; b++)
                {
                    ScreenBook book;
                    ScreenPageComposer composer;
                    int sample = SampleFor(b, bank, line, species);
                    try { book = Books[b](sample); composer = book.MakeComposer(Font); }
                    catch (Exception ex) { AppLogger.WarnOnce("TextScreenPreviews.Book" + b, ex); continue; }
                    if (book.Pages.Count == 0) continue;

                    foreach (ScreenPage page in book.Pages)
                        foreach (string variant in page.Variants.Length > 0 ? page.Variants : new[] { "" })
                            foreach (ScreenSide side in new[] { ScreenSide.Main, ScreenSide.Sub })
                            {
                                (int, string, string, ScreenSide) key = (b, page.Id, variant, side);
                                if (Reads.TryGetValue(key, out HashSet<int> known) && !known.Contains(bank)) continue;

                                HashSet<(int Bank, int Line)> seen = new HashSet<(int Bank, int Line)>();
                                ComposedScreen composed;
                                try { using (ScreenTextReads.Watch(bank, line, text, seen)) composed = composer.Compose(page, variant, side); }
                                catch (Exception ex) { AppLogger.WarnOnce("TextScreenPreviews.Compose", ex); continue; }
                                if (known == null) Reads[key] = seen.Select(s => s.Bank).ToHashSet();
                                int slot = line;
                                if (!seen.Contains((bank, line)))
                                {
                                    // A slot that prints a line picked by the sample (its ability, its move) shows the edit too.
                                    List<int> slots = seen.Where(s => s.Bank == bank).Select(s => s.Line).Distinct().ToList();
                                    if (slots.Count != 1) continue;
                                    slot = slots[0];
                                    try { using (ScreenTextReads.Watch(bank, slot, text, new HashSet<(int Bank, int Line)>())) composed = composer.Compose(page, variant, side); }
                                    catch (Exception ex) { AppLogger.WarnOnce("TextScreenPreviews.Compose", ex); continue; }
                                }
                                if (composed?.Rgba == null) continue;

                                // Pages that print the line the same way add nothing; blanking the line shows which pixels are its own.
                                ComposedScreen blank;
                                try { using (ScreenTextReads.Watch(bank, slot, "", new HashSet<(int Bank, int Line)>())) blank = composer.Compose(page, variant, side); }
                                catch (Exception ex) { AppLogger.WarnOnce("TextScreenPreviews.Compose", ex); continue; }
                                if (!footprints.Add(Footprint(composed.Rgba, blank?.Rgba))) continue;

                                bool top = side == ScreenSide.Main == page.MainOnTop;
                                string graphics = book.Title.EndsWith("graphics", StringComparison.Ordinal) ? book.Title : book.Title + " graphics";
                                string pageId = page.Id;
                                int bookIndex = b;
                                shots.Add(new TextScreenShot
                                {
                                    Name = $"{graphics}: {(top ? "top" : "bottom")}",
                                    Open = bookIndex == 0 ? () => AvaloniaEditorLauncher.OpenPokedexGraphics(sample, pageId)
                                                          : () => _ = AvaloniaEditorLauncher.OpenSummaryScreenAsync(sample, pageId),
                                    EditorName = "Screens",
                                    Picture = ImageConverter.FromRgba(composed.Rgba, ScreenPageComposer.Width, ScreenPageComposer.Height),
                                });
                            }
                }
                AddMoveTextScreens(shots, bank, line, text);
                try { AddMenuWindows(shots, bank, line, text); }
                catch (Exception ex) { AppLogger.WarnOnce("TextScreenPreviews.Menus", ex); }
                try { AddBattleScreens(shots, bank, line, text, Math.Max(0, stop)); }
                catch (Exception ex) { AppLogger.WarnOnce("TextScreenPreviews.Battle", ex); }
                try { AddTouchMenu(shots, bank, line, text); }
                catch (Exception ex) { AppLogger.WarnOnce("TextScreenPreviews.TouchMenu", ex); }
                try { AddFieldWindows(shots, bank, line, text); }
                catch (Exception ex) { AppLogger.WarnOnce("TextScreenPreviews.FieldWindows", ex); }
                foreach (GameTextScreens.Shot shot in GameTextScreens.For(bank, line, text, ArchiveLine, stop))
                    shots.Add(new TextScreenShot { Name = shot.Name, Picture = ImageConverter.FromRgba(shot.Rgba, 256, 192),
                                                     Open = shot.Open ?? (() => AvaloniaEditorLauncher.OpenScreensEditor(shot.Key, bank, line)),
                                                     EditorName = shot.Open == null ? "Screens" : null, Stops = shot.Stops,
                                                     MovesOnByItself = shot.MovesOnByItself });
            }
            return shots;
        }

        private static long Footprint(byte[] with, byte[] without)
        {
            HashCode hash = new HashCode();
            for (int at = 0; at < with.Length; at += 4)
            {
                if (without != null && at + 3 < without.Length && with[at] == without[at] && with[at + 1] == without[at + 1]
                    && with[at + 2] == without[at + 2] && with[at + 3] == without[at + 3]) continue;
                hash.Add(at);
                hash.Add(with[at]); hash.Add(with[at + 1]); hash.Add(with[at + 2]);
            }
            return hash.ToHashCode();
        }

        private static Dictionary<int, int> _speciesByAbility;

        // Draws with the line's own species, or for an ability line one that has the ability, so the name and description match.
        private static int SampleFor(int book, int bank, int line, int species)
        {
            bool ability = bank == RomInfo.AbilityNamesTextNumber
                           || (RomLocator.SummaryScreenSites is RomInfo.SummaryScreenSites sites && bank == sites.AbilityDescriptionBank);
            if (book != 1 || !ability) return species;
            if (_speciesByAbility == null)
            {
                _speciesByAbility = new Dictionary<int, int>();
                for (int s = 1; s < SpeciesCount(); s++)
                {
                    try
                    {
                        int first = new PokemonPersonalData(s).FirstAbility;
                        if (!_speciesByAbility.ContainsKey(first)) _speciesByAbility[first] = s;
                    }
                    catch (Exception ex) { AppLogger.WarnOnce("TextScreenPreviews.SampleFor", ex); break; }
                }
            }
            return _speciesByAbility.TryGetValue(line, out int found) ? found : species;
        }

        private static int SpeciesCount()
        {
            try { return RomInfo.GetPokemonNames()?.Length ?? 0; } catch (Exception) { return 0; }
        }

        private static FieldFont Font(int entry)
        {
            if (Fonts.TryGetValue(entry, out FieldFont known)) return known;
            FieldFont font = null;
            try { font = FieldFont.LoadFromArchive(entry); } catch (Exception ex) { AppLogger.Warn("Text screen previews: font: " + ex.Message); }
            Fonts[entry] = font;
            return font;
        }
    }
}

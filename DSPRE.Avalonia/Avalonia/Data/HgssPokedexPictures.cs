using System;
using System.Collections.Generic;
using System.Linq;
using DSPRE.ROMFiles;

namespace DSPRE.Avalonia.Data
{
    /// <summary>The HGSS Pokédex's bottom screen for each touch and cursor page, drawn with the Pokédex editor's page recipes.</summary>
    public static class HgssPokedexPictures
    {
        // The recipe for each HgssMenuButtons.Pokedex page; the detail tabs use the size page.
        private static readonly string[] Recipes =
        {
            "search_menu", "search_order", "search_name", "search_type", "search_height", "search_weight", "search_area", "search_bodyshape",
            "cover", "list", "size", "size", "area", "forms", "forms_compare", "back",
        };

        /// <summary>A page as edited: its background screen, the spots of each spot table in tiles, and its tile sheet when the screen added tiles.</summary>
        public sealed record Look(byte[] Screen, IReadOnlyList<IReadOnlyList<(int X, int Y)>> Spots, (byte[] Sheet, int Member)? Sheet = null);

        // Where the recipes draw each HgssMenuButtons.Pokedex spot table (ov18_021F9780, ov18_021F990C, ov18_021F9770).
        private static readonly (int X, int Y)[] SearchValues = { (16, 1), (7, 4), (7, 7), (15, 7), (7, 10), (15, 10), (7, 13), (18, 13), (7, 16) };
        private static readonly (int X, int Y)[] Orders = { (3, 6), (18, 6), (3, 10), (18, 10), (3, 14), (18, 14) };
        private static readonly (int X, int Y)[] Types = Enumerable.Range(0, 18).Select(i => (i % 4 * 8, 5 + i / 4 * 3)).ToArray();
        private static readonly (int X, int Y)[] Areas = { (6, 8), (19, 8), (6, 15), (19, 15) };

        private static (int Member, (int X, int Y)[][] Tables)? Game(int page) => page switch
        {
            0 => (57, SearchValues.SelectMany(v => new[] { new[] { v }, new[] { v } }).ToArray()),
            1 => (62, new[] { Orders, Orders }),
            3 => (64, new[] { Types, Types }),
            6 => (67, new[] { Areas, Areas }),
            _ => null,
        };

        private static FieldFont Font(int entry) => GameTextScreens.FontFor(entry);

        /// <summary>The page's pixels, or null when it can't be drawn.</summary>
        /// <param name="look">The page's screen and spots as edited, or null for the game's.</param>
        public static byte[] Draw(int page, Look look = null)
        {
            try
            {
                if (page < 0 || page >= Recipes.Length) return null;
                PokedexBook book = new PokedexBook();
                ScreenPage recipe = book.Pages.FirstOrDefault(p => p.Id == Recipes[page]);
                if (recipe == null) return null;
                ScreenPageComposer composer = book.MakeComposer(Font);
                if (look != null && Game(page) is { } game)
                {
                    composer.Replace = i => i == game.Member ? look.Screen : look.Sheet is { } s && i == s.Member ? s.Sheet : null;
                    Follow(recipe, game.Member, game.Tables, look.Spots);
                }
                // Draw the screen that isn't on top, which is the touch screen.
                return composer.Compose(recipe, "", recipe.MainOnTop ? ScreenSide.Sub : ScreenSide.Main)?.Rgba;
            }
            catch (Exception ex) { AppLogger.WarnOnce("HgssPokedexPictures.Draw", ex); return null; }
        }

        // Texts whose window is at a moved spot, and highlights pasted from or to it, move by the same amount.
        private static void Follow(ScreenPage recipe, int member, (int X, int Y)[][] game, IReadOnlyList<IReadOnlyList<(int X, int Y)>> now)
        {
            Dictionary<(int, int), (int Dx, int Dy)> moved = new Dictionary<(int, int), (int, int)>();
            for (int t = 0; t < game.Length && t < now.Count; t++)
                for (int i = 0; i < game[t].Length && now[t] != null && i < now[t].Count; i++)
                    if (now[t][i] != game[t][i]) moved[game[t][i]] = (now[t][i].X - game[t][i].X, now[t][i].Y - game[t][i].Y);
            if (moved.Count == 0) return;
            foreach (ScreenLayout screen in new[] { recipe.Main, recipe.Sub })
            {
                foreach (ScreenText text in screen.Texts.Where(x => x.WinW > 0).ToList())
                    if (moved.TryGetValue((text.WinX, text.WinY), out (int Dx, int Dy) d))
                    {
                        text.WinX += d.Dx;
                        text.WinY += d.Dy;
                        text.X += d.Dx * 8;
                        text.Y += d.Dy * 8;
                    }
                foreach (ScreenPaste piece in screen.Bgs.SelectMany(b => b.Pieces).Where(p => p.Screen == member && p.Width > 0).ToList())
                    if (moved.TryGetValue((piece.FromX, piece.FromY), out (int Dx, int Dy) d))
                    {
                        piece.FromX += d.Dx;
                        piece.FromY += d.Dy;
                        piece.TileX += d.Dx;
                        piece.TileY += d.Dy;
                    }
            }
        }
    }
}

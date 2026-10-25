using System.Collections.Generic;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>The fonts HGSS prints with, by font number: the game's six, then the ones the extra fonts patch added.</summary>
    public static class GameFonts
    {
        public sealed record Font(int Number, int Entry, string Name);

        private static readonly Font[] Game =
        {
            new(0, 0, "System font"), new(1, 1, "Dialogue font"), new(2, 2, "Touch screen font"), new(3, 3, "Unown font"),
            new(4, 4, "Button font"), new(5, 10, "Pokéwalker font"),
        };

        public static IReadOnlyList<Font> All =>
            Game.Concat(ExtraFontsPatch.AddedEntries().Select((entry, i) => new Font(Game.Length + i, entry, "Extra font " + (i + 1)))).ToList();

        /// <summary>The archive entry font <paramref name="number"/> loads, or -1 when the ROM has no such font.</summary>
        public static int EntryOf(int number)
        {
            IReadOnlyList<Font> all = All;
            return number >= 0 && number < all.Count ? all[number].Entry : -1;
        }

        public static IReadOnlyList<string> Names => All.Select(f => f.Name).ToList();
    }
}

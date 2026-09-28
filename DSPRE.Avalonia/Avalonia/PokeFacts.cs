using System;
using System.IO;
using System.Linq;

namespace DSPRE.Avalonia
{
    /// <summary>A Pokémon fact for the loading card, from the facts file shipped in Tools.</summary>
    public static class PokeFacts
    {
        /// <summary>How long each fact stays on the card.</summary>
        public static readonly TimeSpan Interval = TimeSpan.FromSeconds(6);

        private static string[] _facts;
        private static readonly Random Pick = new();
        private static int _last = -1;

        public static string Next()
        {
            _facts ??= Load();
            if (_facts.Length == 0) return "";
            int i = Pick.Next(_facts.Length);
            if (_facts.Length > 1 && i == _last) i = (i + 1) % _facts.Length;
            _last = i;
            return _facts[i];
        }

        private static string[] Load()
        {
            try
            {
                string path = Path.Combine(AppContext.BaseDirectory, "Tools", "pokefatcs.txt");
                return File.Exists(path)
                    ? File.ReadAllLines(path).Select(l => l.Trim()).Where(l => l.Length > 0).ToArray()
                    : Array.Empty<string>();
            }
            catch (Exception ex)
            {
                AppLogger.Warn("Pokémon facts could not be read: " + ex.Message);
                return Array.Empty<string>();
            }
        }
    }
}

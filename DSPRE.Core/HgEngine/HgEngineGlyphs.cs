using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DSPRE.CharMaps;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// Glyphs the checkout's charmap writes as a single symbol where DSPRE's charmap names them in brackets and keeps
    /// that symbol as an alias (hg-engine's ₧ and ₦ are DSPRE's [PK] and [MN]). Text read from the checkout uses
    /// DSPRE's names, so it reads like vanilla text, and goes back in the checkout's spelling, which its msgenc reads.
    /// </summary>
    public static class HgEngineGlyphs
    {
        private static readonly Regex CharLine = new(@"^([0-9A-Fa-f]{4})=(.+)$");
        private static string _forRoot;
        private static List<(string Checkout, string Dspre)> _pairs = new();

        public static string ToDspre(string text) => Swap(text, toDspre: true);

        public static string ToCheckout(string text) => Swap(text, toDspre: false);

        private static string Swap(string text, bool toDspre)
        {
            if (string.IsNullOrEmpty(text)) return text;
            foreach (var (checkout, dspre) in Pairs())
                text = toDspre ? text.Replace(checkout, dspre, StringComparison.Ordinal) : text.Replace(dspre, checkout, StringComparison.Ordinal);
            return text;
        }

        private static List<(string, string)> Pairs()
        {
            string root = HgEngineProject.IsActive ? HgEngineProject.RepoRootWindows : null;
            if (root == _forRoot) return _pairs;
            var pairs = new List<(string, string)>();
            try
            {
                string rel = root == null ? null : HgEngineOwnedFiles.CharMapRelPath(root);
                string path = rel == null ? null : Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
                var map = path != null && File.Exists(path) ? CharMapManager.GetCurrentCharMap() : null;
                if (map != null)
                    foreach (string line in File.ReadLines(path))
                    {
                        Match m = CharLine.Match(line.TrimEnd('\r'));
                        if (!m.Success || !map.CharacterMap.TryGetValue(m.Groups[1].Value.ToUpperInvariant(), out var entry)) continue;
                        string symbol = m.Groups[2].Value;
                        if (symbol != entry.Character && entry.Aliases?.Contains(symbol) == true && entry.Character.StartsWith("["))
                            pairs.Add((symbol, entry.Character));
                    }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                AppLogger.Error("HgEngineGlyphs: " + ex.Message);
            }
            _pairs = pairs;
            _forRoot = root;
            return pairs;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// The species lists of data/PokedexSort.c (sPokedexSort_NationalNum, _Heaviest, _BodyTypeQuadruped and the rest),
    /// by name without the prefix and in file order. Each list holds the source text of its entries, so entries that
    /// aren't touched keep their spelling; a list that changes is laid out again eight to a line.
    /// </summary>
    internal sealed class HgEngineDexSortLists
    {
        public const string RelPath = "data/PokedexSort.c";
        private const string Prefix = "sPokedexSort_";

        public static string FilePath => Path.Combine(HgEngineProject.RepoPathUnc, RelPath.Replace('/', Path.DirectorySeparatorChar));

        public string Text { get; private set; }
        public List<string> Names { get; } = new();
        public Dictionary<string, List<string>> Lists { get; } = new();
        private readonly Dictionary<string, CDeclaration> _declarations = new();

        public static bool Exists => HgEngineProject.IsActive && File.Exists(FilePath);

        public static string TryParse(string text, out HgEngineDexSortLists lists)
        {
            lists = new HgEngineDexSortLists { Text = text };
            foreach (var decl in CSourceFile.For(text).Declarations)
            {
                if (!decl.Name.StartsWith(Prefix, StringComparison.Ordinal) || decl.Dimensions.Count != 1) continue;
                if (decl.Init.Items.Any(i => i.IsConditional || i.Designators.Count > 0 || i.List != null))
                {
                    // A list DSPRE can't lay out again is left out, so nothing writes it.
                    continue;
                }
                string name = decl.Name.Substring(Prefix.Length);
                lists.Names.Add(name);
                lists.Lists[name] = decl.Init.Items.Select(i => i.ValueText(text).Trim()).ToList();
                lists._declarations[name] = decl;
            }
            return lists.Names.Count == 0 ? $"{RelPath} has no sPokedexSort_ lists DSPRE can read." : null;
        }

        public static bool TryLoad(out HgEngineDexSortLists lists, out string error)
        {
            lists = null;
            if (!Exists) { error = $"{RelPath} is missing from the checkout."; return false; }
            error = TryParse(HgEngineFileCache.GetText(FilePath).Replace("\r\n", "\n"), out lists);
            return error == null;
        }

        /// <summary>Writes the named lists as they are now in <see cref="Lists"/>, checking the file reads back so.</summary>
        public bool TryWrite(ICollection<string> changed, out string error)
        {
            error = null;
            if (changed.Count == 0) return true;
            string text = Text;
            foreach (string name in changed.OrderByDescending(n => _declarations[n].Init.Open))
            {
                var init = _declarations[name].Init;
                string body = "\n" + string.Join("\n", Lists[name].Chunk(8).Select(row => "    " + string.Join(" ", row.Select(t => t + ",")))) + "\n";
                text = text.Substring(0, init.Open + 1) + body + text.Substring(init.Close);
            }
            var expected = changed.ToDictionary(n => n, n => Lists[n].ToList());
            return HgEngineVerifiedWrite.TryWrite(FilePath, RelPath, text, written =>
            {
                string problem = TryParse(written, out var back);
                if (problem != null) return problem;
                foreach (var (name, list) in expected)
                    if (!back.Lists.TryGetValue(name, out var reread) || !reread.SequenceEqual(list)) return $"{Prefix}{name} differs";
                return null;
            }, out error);
        }
    }
}

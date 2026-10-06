using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// Archives hg-engine builds from one PNG per member, as pokegra.mk and itemgra.mk lay out battle sprites, party
    /// icons and item icons, and from the PNGs, palettes and files narcs.mk's recipes convert or copy over a vanilla
    /// archive (bag, battle HUD, weather icons, footprints, Pokédex and opening graphics). A member's drawing is the
    /// PNG's pixels and its colours that PNG's palette, so a change to either is kept only once it is in that PNG.
    /// Everything is read from the Makefile and the fragments it includes: the narchive recipe names the build folder
    /// and the archive, the rules name each file's source, and the members are that folder's files in name order.
    /// Members the recipe extracts from the ROM and leaves alone have no source.
    /// </summary>
    public static class HgEngineBuiltPngs
    {
        public enum Part { Pixels, Colours, Copy, JascColours }

        /// <summary>What one member is built from. A Copy is a file copied in as it is.</summary>
        public sealed record Source(string Path, Part Part);

        private static readonly Regex Assignment = new(@"^[ \t]*(\w+)[ \t]*:?=[ \t]*(.*?)[ \t]*\r?$", RegexOptions.Multiline);
        private static readonly Regex Include = new(@"^[ \t]*include[ \t]+(\S+\.mk)[ \t]*\r?$", RegexOptions.Multiline);
        // A recipe can carry blank and comment lines between its commands, as the battle graphics one does.
        private static readonly Regex NarcRecipe = new(@"^\$\((\w+)_NARC\)[ \t]*:[^\n]*\n((?:(?:\t[^\n]*|#[^\n]*|[ \t]*\r?)(?:\n|$))+)", RegexOptions.Multiline);
        private static readonly Regex PngRule = new(@"^(\S+\.(NCGR|NCLR))[ \t]*:[ \t]*(\S+\.png)[ \t]*\r?$", RegexOptions.Multiline);
        private static readonly Regex Reference = new(@"\$\((\w+)\)");

        private static Dictionary<string, List<Source>> _members;
        private static string _cachedFor;
        private static DateTime _cachedStamp;
        private static readonly HashSet<string> _reportedMismatch = new(StringComparer.OrdinalIgnoreCase);

        public static void ClearCache() { _members = null; _cachedFor = null; }

        /// <summary>
        /// What member <paramref name="member"/> of <paramref name="archive"/> is built from, or null when it is not one
        /// of these. <paramref name="memberCount"/> is how many the archive holds; a checkout whose rules list a
        /// different number is not trusted for any member.
        /// </summary>
        public static Source For(string archive, int member, int memberCount)
        {
            if (!HgEngineProject.IsActive || archive == null) return null;
            Dictionary<string, List<Source>> all = Load();
            if (all == null || !all.TryGetValue(HgEngineOwnedFiles.Normalise(archive), out List<Source> list)) return null;
            if (list.Count != memberCount)
            {
                lock (_reportedMismatch)
                    if (_reportedMismatch.Add(archive))
                        AppLogger.Error($"HgEngineBuiltPngs: the build rules give {archive} {list.Count} files but it holds {memberCount}, so its PNGs are left alone.");
                return null;
            }
            if (member < 0 || member >= list.Count) return null;
            Source source = list[member];
            if (source == null) return null;
            return source.Part == Part.Colours ? new Source(BuiltFrom(source.Path), Part.Colours) : source;
        }

        /// <summary>pokegra.mk falls back to the female PNG's palette when the male one is empty.</summary>
        private static string BuiltFrom(string png)
        {
            try
            {
                if (new FileInfo(png).Length > 0) return png;
                string sep = Path.DirectorySeparatorChar.ToString();
                string female = png.Replace(sep + "male" + sep, sep + "female" + sep);
                return female != png && File.Exists(female) && new FileInfo(female).Length > 0 ? female : png;
            }
            catch (IOException) { return png; }
        }

        private static Dictionary<string, List<Source>> Load()
        {
            string root = HgEngineProject.RepoPathUnc;
            string makefile = Path.Combine(root, "Makefile");
            if (!File.Exists(makefile)) return null;
            Dictionary<string, List<Source>> members = new Dictionary<string, List<Source>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string top = HgEngineFileCache.GetText(makefile);
                List<string> includes = Include.Matches(top).Select(inc => Full(root, inc.Groups[1].Value)).Where(File.Exists).ToList();
                // Species added by script rewrite pokegra.mk, so any fragment changing reads them all again.
                DateTime stamp = includes.Append(makefile).Max(File.GetLastWriteTimeUtc);
                if (_members != null && _cachedFor == root && _cachedStamp == stamp) return _members;

                Dictionary<string, string> vars = Variables(top, new Dictionary<string, string>());
                foreach (string path in includes)
                {
                    string text = HgEngineFileCache.GetText(path);
                    if (!PngRule.IsMatch(text) && !NarcRecipe.IsMatch(text)) continue;
                    Read(root, text, Variables(text, new Dictionary<string, string>(vars)), members);
                }
                _cachedStamp = stamp;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                AppLogger.Error("HgEngineBuiltPngs: " + ex.Message);
                return null;
            }
            _members = members;
            _cachedFor = root;
            return members;
        }

        private static readonly Regex PatternRule = new(@"^(\S*%\S*\.(NCGR|NCLR))[ \t]*:[ \t]*(\S*%\S*\.png)[ \t]*\r?$", RegexOptions.Multiline);
        // Pokéwalker pictures: $(DIR)/%.lz from $(ART_DIR)/%.2bpp, which ENCODE_IMG makes from %.png.
        private static readonly Regex ChainRule = new(@"^(\S*%\S*\.(lz|2bpp))[ \t]*:[ \t]*(\S*%\S*\.(2bpp|png))[ \t]*\r?$", RegexOptions.Multiline);
        private static readonly Regex HyphenMember = new(@"^(.+)(-0\d)\.(NCGR|NCLR)(\.lz)?$");
        private static readonly Regex NumberedMember = new(@"^(.*_)(\d+)$");

        private static void Read(string root, string text, Dictionary<string, string> vars, Dictionary<string, List<Source>> into)
        {
            List<(string Target, Part Part, string Png)> pngs = new List<(string Target, Part Part, string Png)>();
            foreach (Match m in PngRule.Matches(text))
                if (!m.Groups[1].Value.Contains('%'))
                    pngs.Add((Expand(m.Groups[1].Value, vars), m.Groups[2].Value == "NCGR" ? Part.Pixels : Part.Colours, Expand(m.Groups[3].Value, vars)));
            List<(string Target, Part Part, string Png)> patterns = PatternRule.Matches(text).Cast<Match>()
                .Select(m => (Target: Expand(m.Groups[1].Value, vars), Part: m.Groups[2].Value == "NCGR" ? Part.Pixels : Part.Colours, Png: Expand(m.Groups[3].Value, vars)))
                .ToList();
            List<(string Target, string From)> chains = ChainRule.Matches(text).Cast<Match>()
                .Select(m => (Target: Expand(m.Groups[1].Value, vars), From: Expand(m.Groups[3].Value, vars)))
                .ToList();
            // A packed picture whose .2bpp is made from a PNG counts as drawn from that PNG.
            foreach ((string t, string from) in chains)
            {
                if (!t.EndsWith(".lz", StringComparison.Ordinal) || !from.EndsWith(".2bpp", StringComparison.Ordinal)) continue;
                (string Target, string From) art = chains.FirstOrDefault(c => c.Target == from && c.From.EndsWith(".png", StringComparison.Ordinal));
                if (art.From != null) patterns.Add((t, Part.Pixels, art.From));
            }

            foreach (Match recipe in NarcRecipe.Matches(text))
            {
                string body = recipe.Groups[2].Value;
                Match create = Regex.Match(body, @"\$\(NARCHIVE\)\s+create\s+\$@\s+\$\((\w+)\)");
                if (!create.Success || !vars.TryGetValue(recipe.Groups[1].Value + "_TARGET", out string target)) continue;
                string dirVar = create.Groups[1].Value;
                string buildDir = Expand("$(" + dirVar + ")", vars).TrimEnd('/');

                SortedDictionary<string, Source> byName = new SortedDictionary<string, Source>(StringComparer.Ordinal);
                foreach ((string t, Part part, string png) in pngs)
                    if (Path.GetDirectoryName(t)?.Replace('\\', '/') == buildDir)
                        byName[Path.GetFileName(t)] = new Source(Full(root, png), part);

                string to = Regex.Escape("$(" + dirVar + ")");
                foreach (Match cp in Regex.Matches(body, @"cp\s+-r\s+(\S+)/\.\s+" + to))
                {
                    string from = Full(root, Expand(cp.Groups[1].Value, vars));
                    if (Directory.Exists(from))
                        foreach (string f in Directory.GetFiles(from)) byName[Path.GetFileName(f)] = new Source(f, Part.Copy);
                }
                foreach (Match cp in Regex.Matches(body, @"cp\s+(\S+)\s+" + to + @"/(\S+)"))
                    if (!cp.Groups[1].Value.StartsWith("$$", StringComparison.Ordinal))
                        byName[cp.Groups[2].Value] = new Source(Full(root, Expand(cp.Groups[1].Value, vars)), Part.Copy);
                // One file converted straight into a member, as otherpoke's $(GFX) x.pal $(DIR)/4_212.NCLR.
                foreach (Match gfx in Regex.Matches(body, @"\$\(GFX\)\s+(\S+\.(pal|png))\s+" + to + @"/(\S+\.(NCLR|NCGR))\b"))
                    if (!gfx.Groups[1].Value.StartsWith("$$", StringComparison.Ordinal))
                        byName[gfx.Groups[3].Value] = new Source(Full(root, Expand(gfx.Groups[1].Value, vars)),
                            gfx.Groups[2].Value == "pal" ? Part.JascColours : gfx.Groups[4].Value == "NCGR" ? Part.Pixels : Part.Colours);

                // The archive is packed straight from a source folder, as footprints are: every member is a copy.
                bool fromSource = dirVar.EndsWith("_DEPENDENCIES_DIR", StringComparison.Ordinal);
                string built = Full(root, buildDir);
                List<string> names = Directory.Exists(built)
                    ? Directory.GetFiles(built).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToList() : null;
                if (fromSource && names != null)
                    foreach (string n in names) byName[n] = new Source(Path.Combine(built, n), Part.Copy);

                // Trainer sprites have their own editor writing their sources, so only it touches them.
                string narc = Expand("$(" + recipe.Groups[1].Value + "_NARC)", vars);
                bool ownedByEditor = HgEngineDomains.All.Any(d => d.Domain == HgEngineDomain.TrainerGraphics && d.MakeTargets.Contains(narc));
                if (names != null && !fromSource && !ownedByEditor)
                {
                    List<string> sourceDirs = SourceDirs(recipe.Value, vars, root);
                    foreach (string n in names)
                    {
                        if (byName.ContainsKey(n)) continue;
                        Source found = FromPattern(n, buildDir, patterns, root) ?? FromLoop(n, body, sourceDirs);
                        if (found != null) byName[n] = found;
                    }
                }

                if (byName.Count == 0) continue;
                // Built members are listed whole, sourced or not, so positions match the archive; without a build
                // only the sourced members are known, as before.
                into[HgEngineOwnedFiles.Normalise(target)] = names != null
                    ? names.Select(n => byName.TryGetValue(n, out Source src) ? src : null).ToList()
                    : byName.Values.ToList();
            }
        }

        // A pattern rule such as $(BAGGFX_DIR)/5_%.NCGR: $(BAGGFX_DEPENDENCIES_DIR)/%.png names the PNG by the member's stem.
        private static Source FromPattern(string name, string buildDir, List<(string Target, Part Part, string Png)> patterns, string root)
        {
            foreach ((string t, Part part, string png) in patterns)
            {
                if (Path.GetDirectoryName(t)?.Replace('\\', '/') != buildDir) continue;
                string file = Path.GetFileName(t);
                Match m = Regex.Match(name, "^" + Regex.Escape(file).Replace("%", "(.+)") + "$");
                if (!m.Success) continue;
                string path = Full(root, png.Replace("%", m.Groups[1].Value));
                if (File.Exists(path)) return new Source(path, part);
            }
            return null;
        }

        // Loops over a source folder, matched only by the naming the recipe itself shows.
        private static Source FromLoop(string name, string body, List<string> sourceDirs)
        {
            string Find(string file) => sourceDirs.Select(d => Path.Combine(d, file)).FirstOrDefault(File.Exists);

            // $(GFX) $$file $(DIR)/$$(basename $$file .png)-00.NCGR, -01.NCLR, and the -00.NCGR.lz the terrain gets.
            Match h = HyphenMember.Match(name);
            if (h.Success && body.Contains("$$(basename $$file .png)" + h.Groups[2].Value, StringComparison.Ordinal)
                && Find(h.Groups[1].Value + ".png") is string hyphenPng)
                return new Source(hyphenPng, h.Groups[3].Value == "NCGR" ? Part.Pixels : Part.Colours);

            Match n = NumberedMember.Match(name);
            if (!n.Success) return null;
            // mv $(...)/$$(basename $$file .png).NCGR.lz $(DIR)/$$(basename $$file .png): the drawing keeps the PNG's name.
            if (Regex.IsMatch(body, @"NCGR\.lz\s+\$\(\w+\)/\$\$\(basename \$\$file \.png\)") && Find(name + ".png") is string drawn)
                return new Source(drawn, Part.Pixels);
            // ... and its colours go to the member one after it.
            if (body.Contains("+1))", StringComparison.Ordinal) && body.Contains(".NCLR", StringComparison.Ordinal)
                && int.TryParse(n.Groups[2].Value, out int number) && number > 0
                && Find(n.Groups[1].Value + (number - 1).ToString("D" + n.Groups[2].Value.Length) + ".png") is string coloured)
                return new Source(coloured, Part.Colours);
            if (body.Contains("$$(basename $$file .pal)", StringComparison.Ordinal) && Find(name + ".pal") is string jasc)
                return new Source(jasc, Part.JascColours);
            if (Regex.IsMatch(body, @"cp \$\$file \$\(\w+\)/\$\$\(basename \$\$file \.NSCR\)") && Find(name + ".NSCR") is string screen)
                return new Source(screen, Part.Copy);
            return null;
        }

        // The source folders a recipe reads: every *_DEPENDENCIES_DIR its rule line and body reach through variables.
        private static List<string> SourceDirs(string recipe, Dictionary<string, string> vars, string root)
        {
            string expanded = Expand(recipe, vars);
            return vars.Where(kv => kv.Key.EndsWith("_DEPENDENCIES_DIR", StringComparison.Ordinal))
                .Select(kv => Expand(kv.Value, vars).TrimEnd('/'))
                .Where(dir => dir.Length > 0 && expanded.Contains(dir, StringComparison.Ordinal))
                .Distinct()
                .Select(dir => Full(root, dir))
                .Where(Directory.Exists)
                .ToList();
        }

        private static Dictionary<string, string> Variables(string text, Dictionary<string, string> into)
        {
            foreach (Match m in Assignment.Matches(text))
                into.TryAdd(m.Groups[1].Value, m.Groups[2].Value);
            return into;
        }

        // FILESYS stays as written: Normalise finds the archive below it.
        private static string Expand(string value, Dictionary<string, string> vars)
        {
            for (int depth = 0; depth < 8 && value.Contains("$(", StringComparison.Ordinal); depth++)
                value = Reference.Replace(value, m => m.Groups[1].Value != "FILESYS" && vars.TryGetValue(m.Groups[1].Value, out string v) ? v : m.Value);
            return value;
        }

        private static string Full(string root, string rel) => Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
    }
}

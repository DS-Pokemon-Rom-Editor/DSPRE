using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// Archives hg-engine builds from PNGs, read from the Makefile's recipes; an edit lasts only once it is in the
    /// PNG. Members copied from the ROM unchanged have no source.
    /// </summary>
    /// <remarks>The recipes are pokegra.mk, itemgra.mk and narcs.mk; the members are the build folder's files in name order.</remarks>
    public static class HgEngineBuiltPngs
    {
        public enum Part { Pixels, Colours, Copy, JascColours }

        /// <summary>What one member is built from. A Copy is a file copied in as it is.</summary>
        public sealed record Source(string Path, Part Part);

        private static Dictionary<string, List<Source>> _members;
        private static string _cachedFor;
        private static DateTime _cachedStamp;
        private static readonly HashSet<string> ReportedMismatch = new(StringComparer.OrdinalIgnoreCase);

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
                lock (ReportedMismatch)
                    if (ReportedMismatch.Add(archive))
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
                List<string> includes = HgEngineMakeFile.For(top).Includes.Where(inc => inc.EndsWith(".mk", StringComparison.Ordinal) && !inc.Any(char.IsWhiteSpace))
                    .Select(inc => Full(root, inc)).Where(File.Exists).ToList();
                // Species added by script rewrite pokegra.mk, so any fragment changing reads them all again.
                DateTime stamp = includes.Append(makefile).Max(File.GetLastWriteTimeUtc);
                if (_members != null && _cachedFor == root && _cachedStamp == stamp) return _members;

                Dictionary<string, string> vars = Variables(top, new Dictionary<string, string>());
                foreach (string path in includes)
                {
                    string text = HgEngineFileCache.GetText(path);
                    if (!HgEngineMakeFile.For(text).Rules.Any(r => IsPngRule(r) || NarcOf(r) != null)) continue;
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

        private static bool OneWord(string text) => text.Length > 0 && !text.Any(char.IsWhiteSpace);

        private static bool IsDrawn(string target) => target.EndsWith(".NCGR", StringComparison.Ordinal) || target.EndsWith(".NCLR", StringComparison.Ordinal);

        private static bool IsPngRule(HgEngineMakeFile.Rule rule) =>
            OneWord(rule.Target) && OneWord(rule.Prerequisites) && IsDrawn(rule.Target) && rule.Prerequisites.EndsWith(".png", StringComparison.Ordinal);

        // Pokéwalker pictures: $(DIR)/%.lz from $(ART_DIR)/%.2bpp, which ENCODE_IMG makes from %.png.
        private static bool IsChainRule(HgEngineMakeFile.Rule rule) =>
            OneWord(rule.Target) && OneWord(rule.Prerequisites) && rule.Target.Contains('%') && rule.Prerequisites.Contains('%')
            && (rule.Target.EndsWith(".lz", StringComparison.Ordinal) || rule.Target.EndsWith(".2bpp", StringComparison.Ordinal))
            && (rule.Prerequisites.EndsWith(".2bpp", StringComparison.Ordinal) || rule.Prerequisites.EndsWith(".png", StringComparison.Ordinal));

        // X for a rule on $(X_NARC), or null.
        private static string NarcOf(HgEngineMakeFile.Rule rule)
        {
            string name = HgEngineMakeFile.ReferenceName(rule.Target);
            return name != null && name.EndsWith("_NARC", StringComparison.Ordinal) ? name.Substring(0, name.Length - 5) : null;
        }

        private static Part PartOf(string target) => target.EndsWith(".NCGR", StringComparison.Ordinal) ? Part.Pixels : Part.Colours;

        private static void Read(string root, string text, Dictionary<string, string> vars, Dictionary<string, List<Source>> into)
        {
            List<HgEngineMakeFile.Rule> rules = HgEngineMakeFile.For(text).Rules;
            List<(string Target, Part Part, string Png)> pngs = rules.Where(r => IsPngRule(r) && !r.Target.Contains('%'))
                .Select(r => (Expand(r.Target, vars), PartOf(r.Target), Expand(r.Prerequisites, vars))).ToList();
            List<(string Target, Part Part, string Png)> patterns = rules.Where(r => IsPngRule(r) && r.Target.Contains('%') && r.Prerequisites.Contains('%'))
                .Select(r => (Target: Expand(r.Target, vars), Part: PartOf(r.Target), Png: Expand(r.Prerequisites, vars)))
                .ToList();
            List<(string Target, string From)> chains = rules.Where(IsChainRule)
                .Select(r => (Target: Expand(r.Target, vars), From: Expand(r.Prerequisites, vars)))
                .ToList();
            // A packed picture whose .2bpp is made from a PNG counts as drawn from that PNG.
            foreach ((string t, string from) in chains)
            {
                if (!t.EndsWith(".lz", StringComparison.Ordinal) || !from.EndsWith(".2bpp", StringComparison.Ordinal)) continue;
                (string Target, string From) art = chains.FirstOrDefault(c => c.Target == from && c.From.EndsWith(".png", StringComparison.Ordinal));
                if (art.From != null) patterns.Add((t, Part.Pixels, art.From));
            }

            foreach (HgEngineMakeFile.Rule recipe in rules)
            {
                string variable = NarcOf(recipe);
                if (variable == null || recipe.Body.Length == 0) continue;
                string body = recipe.Body;
                string[] words = HgEngineMakeFile.Words(body);
                int create = HgEngineMakeFile.RunStart(words, 0, "$(NARCHIVE)", "create", "$@", "$(");
                string dirVar = create < 0 ? null : HgEngineMakeFile.ReferenceName(words[create + 3]);
                if (dirVar == null || !vars.TryGetValue(variable + "_TARGET", out string target)) continue;
                string buildDir = Expand("$(" + dirVar + ")", vars).TrimEnd('/');

                SortedDictionary<string, Source> byName = new SortedDictionary<string, Source>(StringComparer.Ordinal);
                foreach ((string t, Part part, string png) in pngs)
                    if (Path.GetDirectoryName(t)?.Replace('\\', '/') == buildDir)
                        byName[Path.GetFileName(t)] = new Source(Full(root, png), part);

                string to = "$(" + dirVar + ")";
                for (int w = 0; w + 2 < words.Length; w++)
                {
                    string next = words[w + 1], after = words[w + 2];
                    if (words[w] == "cp" && next == "-r" && w + 3 < words.Length && after.EndsWith("/.", StringComparison.Ordinal)
                        && words[w + 3].StartsWith(to, StringComparison.Ordinal))
                    {
                        string from = Full(root, Expand(after.Substring(0, after.Length - 2), vars));
                        if (Directory.Exists(from))
                            foreach (string f in Directory.GetFiles(from)) byName[Path.GetFileName(f)] = new Source(f, Part.Copy);
                    }
                    if (next.StartsWith("$$", StringComparison.Ordinal) || !after.StartsWith(to + "/", StringComparison.Ordinal)) continue;
                    string file = after.Substring(to.Length + 1);
                    // A file name only, so scr_seq's "cp ... $(SCR_SEQ_DIR)/)" inside $(if ...) doesn't count.
                    if (words[w] == "cp" && file.Length > 0 && file.All(c => HgEngineMakeFile.IsNameChar(c) || c is '.' or '-'))
                        byName[file] = new Source(Full(root, Expand(next, vars)), Part.Copy);
                    // One file converted straight into a member, as otherpoke's $(GFX) x.pal $(DIR)/4_212.NCLR.
                    if (words[w] == "$(GFX)" && IsDrawn(file) && (next.EndsWith(".pal", StringComparison.Ordinal) || next.EndsWith(".png", StringComparison.Ordinal)))
                        byName[file] = new Source(Full(root, Expand(next, vars)),
                            next.EndsWith(".pal", StringComparison.Ordinal) ? Part.JascColours : PartOf(file));
                }

                // The archive is packed straight from a source folder, as footprints are: every member is a copy.
                bool fromSource = dirVar.EndsWith("_DEPENDENCIES_DIR", StringComparison.Ordinal);
                string built = Full(root, buildDir);
                List<string> names = Directory.Exists(built)
                    ? Directory.GetFiles(built).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToList() : null;
                if (fromSource && names != null)
                    foreach (string n in names) byName[n] = new Source(Path.Combine(built, n), Part.Copy);

                // Trainer sprites have their own editor writing their sources, so only it touches them.
                string narc = Expand("$(" + variable + "_NARC)", vars);
                bool ownedByEditor = HgEngineDomains.All.Any(d => d.Domain == HgEngineDomain.TrainerGraphics && d.MakeTargets.Contains(narc));
                if (names != null && !fromSource && !ownedByEditor)
                {
                    List<string> sourceDirs = SourceDirs(recipe.Text, vars, root);
                    foreach (string n in names)
                    {
                        if (byName.ContainsKey(n)) continue;
                        Source found = FromPattern(n, buildDir, patterns, root) ?? FromLoop(n, body, sourceDirs);
                        if (found != null) byName[n] = found;
                    }
                }

                if (byName.Count == 0) continue;
                // Built members are listed whole, sourced or not, so positions match the archive; without a build
                // only the sourced members are known.
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
                int stemAt = file.IndexOf('%');
                if (stemAt < 0) continue;
                string before = file.Substring(0, stemAt), after = file.Substring(stemAt + 1);
                if (name.Length <= before.Length + after.Length || !name.StartsWith(before, StringComparison.Ordinal) || !name.EndsWith(after, StringComparison.Ordinal)) continue;
                string path = Full(root, png.Replace("%", name.Substring(before.Length, name.Length - before.Length - after.Length)));
                if (File.Exists(path)) return new Source(path, part);
            }
            return null;
        }

        // Loops over a source folder, matched only by the naming the recipe itself shows.
        private static Source FromLoop(string name, string body, List<string> sourceDirs)
        {
            string Find(string file) => sourceDirs.Select(d => Path.Combine(d, file)).FirstOrDefault(File.Exists);

            // $(GFX) $$file $(DIR)/$$(basename $$file .png)-00.NCGR, -01.NCLR, and the -00.NCGR.lz the terrain gets.
            string plain = name.EndsWith(".lz", StringComparison.Ordinal) ? name.Substring(0, name.Length - 3) : name;
            string stem = plain.Length > 5 ? plain.Substring(0, plain.Length - 5) : "";
            if (IsDrawn(plain) && stem.Length > 3 && stem[^3] == '-' && stem[^2] == '0' && char.IsAsciiDigit(stem[^1]))
            {
                string hyphen = stem.Substring(stem.Length - 3), picture = stem.Substring(0, stem.Length - 3);
                if (body.Contains("$$(basename $$file .png)" + hyphen, StringComparison.Ordinal) && Find(picture + ".png") is string hyphenPng)
                    return new Source(hyphenPng, PartOf(plain));
            }

            int digits = name.Length;
            while (digits > 0 && char.IsAsciiDigit(name[digits - 1])) digits--;
            if (digits == name.Length || digits == 0 || name[digits - 1] != '_') return null;
            string head = name.Substring(0, digits), tail = name.Substring(digits);
            // mv $(...)/$$(basename $$file .png).NCGR.lz $(DIR)/$$(basename $$file .png): the drawing keeps the PNG's name.
            if (MovesInto(body, "NCGR.lz", "$$(basename $$file .png)") && Find(name + ".png") is string drawn)
                return new Source(drawn, Part.Pixels);
            // ... and its colours go to the member one after it.
            if (body.Contains("+1))", StringComparison.Ordinal) && body.Contains(".NCLR", StringComparison.Ordinal)
                && int.TryParse(tail, out int number) && number > 0
                && Find(head + (number - 1).ToString("D" + tail.Length) + ".png") is string coloured)
                return new Source(coloured, Part.Colours);
            if (body.Contains("$$(basename $$file .pal)", StringComparison.Ordinal) && Find(name + ".pal") is string jasc)
                return new Source(jasc, Part.JascColours);
            if (MovesInto(body, "cp $$file", "$$(basename $$file .NSCR)") && Find(name + ".NSCR") is string screen)
                return new Source(screen, Part.Copy);
            return null;
        }

        // Whether "<source> $(ANY_DIR)/<name>" appears in the recipe.
        private static bool MovesInto(string body, string source, string name)
        {
            for (int at = body.IndexOf(source, StringComparison.Ordinal); at >= 0; at = body.IndexOf(source, at + 1, StringComparison.Ordinal))
            {
                int i = at + source.Length;
                while (i < body.Length && char.IsWhiteSpace(body[i])) i++;
                int close = string.CompareOrdinal(body, i, "$(", 0, 2) == 0 ? body.IndexOf(')', i) : -1;
                if (close > 0 && string.CompareOrdinal(body, close + 1, "/" + name, 0, name.Length + 1) == 0) return true;
            }
            return false;
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
            foreach (HgEngineMakeFile.Assignment a in HgEngineMakeFile.For(text).Assignments)
                if (!a.Appends) into.TryAdd(a.Name, a.Value);
            return into;
        }

        // FILESYS stays as written: Normalise finds the archive below it.
        private static string Expand(string value, Dictionary<string, string> vars) =>
            HgEngineMakeFile.Expand(value, name => name != "FILESYS" && vars.TryGetValue(name, out string v) ? v : null);

        private static string Full(string root, string rel) => Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
    }
}

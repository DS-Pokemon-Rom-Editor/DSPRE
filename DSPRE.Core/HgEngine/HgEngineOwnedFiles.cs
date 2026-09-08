using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>What DSPRE may do with a ROM file hg-engine builds.</summary>
    public enum HgEngineOwnership
    {
        /// <summary>Its source is text DSPRE can put in front of you and write back.</summary>
        EditableSource,

        /// <summary>Built from another domain's source, so there is nothing here to edit.</summary>
        Generated,

        /// <summary>Built from images or binaries, which want the source-backed graphics path.</summary>
        Asset,
    }

    /// <summary>One hg-engine build rule, resolved against the linked checkout.</summary>
    public sealed class HgEngineRule
    {
        public string Variable { get; init; }
        public string Label { get; init; }
        public HgEngineOwnership Ownership { get; init; }

        /// <summary>Read from the checkout. Null when the rule builds from generated input.</summary>
        public string SourceDirRelPath { get; init; }

        /// <summary>The ROM file it produces, as the checkout names it, e.g. "a/0/2/7".</summary>
        public string TargetArchive { get; init; }

        /// <summary>
        /// True when the rule creates the archive outright. A rule that extracts the ROM's copy first
        /// only overwrites its own files, so everything else in that archive is still DSPRE's to edit.
        /// </summary>
        public bool ReplacesWholeArchive { get; init; }
    }

    /// <summary>One file inside an archive hg-engine builds.</summary>
    public sealed class HgEngineOwnedFile
    {
        public HgEngineRule Rule { get; init; }
        public int Id { get; init; }

        /// <summary>Null for anything not built from an editable source file.</summary>
        public string RelPath { get; init; }
        public string FullPath { get; init; }

        public HgEngineOwnership Ownership => Rule.Ownership;
    }

    /// <summary>
    /// Everything a linked checkout builds, resolved from its own narcs.mk: which directory feeds which
    /// ROM archive, whether the rule replaces that archive outright, and which files are inside. Only
    /// hg-engine's make-variable naming is assumed; every path, target and file list is read.
    /// </summary>
    public static class HgEngineOwnedFiles
    {
        public const string MakeFragmentRelPath = "narcs.mk";

        // hg-engine's own variable naming, and what DSPRE may do with each rule's output.
        private static readonly (string Variable, string Label, HgEngineOwnership Ownership)[] Known =
        {
            ("MSGDATA",       "text archives",           HgEngineOwnership.EditableSource),
            ("SCR_SEQ",       "field scripts",           HgEngineOwnership.EditableSource),
            ("MOVEANIM",      "move animations",         HgEngineOwnership.EditableSource),
            ("MOVESUBANIM",   "move sub-animations",     HgEngineOwnership.EditableSource),
            ("MOVE_SEQ",      "move battle scripts",     HgEngineOwnership.EditableSource),
            ("BATTLE_EFF",    "battle effect scripts",   HgEngineOwnership.EditableSource),
            ("BATTLE_SUB",    "battle subscripts",       HgEngineOwnership.EditableSource),

            ("TRAINERTEXT",   "trainer battle messages", HgEngineOwnership.Generated),

            ("BATTLEHUD",     "battle HUD graphics",     HgEngineOwnership.Asset),
            ("MOVEPARTICLES", "move particles",          HgEngineOwnership.Asset),
            ("OPENDEMO",      "opening demo graphics",   HgEngineOwnership.Asset),
            ("FOOTPRINTS",    "footprints",              HgEngineOwnership.Asset),
            ("BAGGFX",        "bag graphics",            HgEngineOwnership.Asset),
            ("OVERWORLDS",    "overworld sprites",       HgEngineOwnership.Asset),
            ("DEXGFX",        "Pokedex graphics",        HgEngineOwnership.Asset),
            ("BATTLEGFX",     "battle graphics",         HgEngineOwnership.Asset),
            ("OTHERPOKE",     "alternate-form sprites",  HgEngineOwnership.Asset),
            ("FONT",          "fonts",                   HgEngineOwnership.Asset),
            ("TEXTBOX",       "message box graphics",    HgEngineOwnership.Asset),
            ("BALL_SPA",      "ball particles",          HgEngineOwnership.Asset),
            ("PW_POKEGRA",    "Pokewalker sprites",      HgEngineOwnership.Asset),
            ("PW_POKEICON",   "Pokewalker icons",        HgEngineOwnership.Asset),
            ("TRAINER_GFX",   "trainer sprites",         HgEngineOwnership.Asset),
            ("SDAT",          "sound",                   HgEngineOwnership.Asset),
        };

        // A source file names the archive slot it fills: either the whole stem, or the first run of
        // digits after an underscore. That covers scr_seq_00953_trainerscript, move_script_0001_POUND
        // and the group_index form the asset directories use, like 6_06, 8_51 and 8_003.
        private static readonly Regex BareNumber = new(@"^\d+$");
        private static readonly Regex PrefixedNumber = new(@"_(\d+)");

        private static Dictionary<string, HgEngineRule> _rules;
        private static Dictionary<string, Dictionary<int, HgEngineOwnedFile>> _filesByArchive;
        private static string _cachedFor;

        public static void ClearCache()
        {
            _rules = null;
            _filesByArchive = null;
            _cachedFor = null;
        }

        public static IReadOnlyCollection<HgEngineRule> Rules => Load().rules.Values;

        /// <summary>The rule that builds a ROM archive, by the path the checkout names it, or null.</summary>
        public static HgEngineRule RuleForArchive(string archive)
        {
            var maps = Load();
            return archive != null && maps.rules.TryGetValue(Normalise(archive), out HgEngineRule rule)
                ? rule : null;
        }

        public static HgEngineOwnedFile Get(string archive, int id)
        {
            var maps = Load();
            return archive != null
                && maps.files.TryGetValue(Normalise(archive), out var byId)
                && byId.TryGetValue(id, out HgEngineOwnedFile file) ? file : null;
        }

        public static bool Owns(string archive, int id) => Get(archive, id) != null;

        /// <summary>
        /// The ROM archive a DirNames stands for, spelled the way a checkout's targets are. Null for the
        /// named-folder layouts of DP and Platinum, which hg-engine does not build.
        /// </summary>
        public static string ArchiveOf(RomInfo.DirNames dir)
        {
            if (RomInfo.gameDirs == null || !RomInfo.gameDirs.TryGetValue(dir, out var paths)) return null;
            return ArchiveOfPath(paths.packedDir);
        }

        /// <summary>The same, for anything resolved by path rather than through the directory table.</summary>
        public static string ArchiveOfPath(string packedPath)
        {
            string packed = (packedPath ?? "").Replace('\\', '/');

            Match m = Regex.Match(packed, @"(?:^|/)(a/\d+/\d+/\d+)$");
            if (m.Success) return m.Groups[1].Value;

            // Not everything lives under a/x/y/z. The sound archive and the named folders are written
            // out in full, so what identifies them is the part below the ROM's filesystem root.
            foreach (string root in new[] { "/files/", "/root/", "/data/" })
            {
                int at = packed.LastIndexOf(root, StringComparison.OrdinalIgnoreCase);
                if (at >= 0) return packed.Substring(at + root.Length).Trim('/');
            }
            return null;
        }

        /// <summary>
        /// Why DSPRE must not write an archive, or null when it may. Only rules that build the whole
        /// archive refuse outright; one that overwrites single files leaves the rest editable, which the
        /// text and script editors handle per file.
        /// </summary>
        public static string RefusalFor(RomInfo.DirNames dir)
        {
            // A domain already routes its reads and writes to source, so it is edited, not refused.
            if (HgEngineDomains.IsOwned(dir)) return null;

            HgEngineRule rule = RuleForArchive(ArchiveOf(dir));
            if (rule == null || !rule.ReplacesWholeArchive) return null;

            string from = rule.Ownership == HgEngineOwnership.Generated
                ? "its own data"
                : rule.SourceDirRelPath ?? "its own source";
            return $"hg-engine builds the {rule.Label} from {from} on every build, so anything changed " +
                "here is overwritten the next time you compile.";
        }

        /// <summary>Every file of one archive, empty when hg-engine does not build it.</summary>
        public static IReadOnlyDictionary<int, HgEngineOwnedFile> FilesIn(string archive)
        {
            var maps = Load();
            return archive != null && maps.files.TryGetValue(Normalise(archive), out var byId)
                ? byId : new Dictionary<int, HgEngineOwnedFile>();
        }

        /// <summary>Message per line, kept exactly as stored: blank and space-only lines are real messages.</summary>
        public static bool TryReadLines(HgEngineOwnedFile file, out List<string> lines, out string error)
        {
            lines = new List<string>();
            if (!TryReadText(file, out string text, out error)) return false;

            lines = text.Replace("\r\n", "\n").Split('\n').ToList();
            if (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
            return true;
        }

        public static bool TryReadText(HgEngineOwnedFile file, out string text, out string error)
        {
            text = null;
            error = null;
            if (file?.FullPath == null) { error = "This file has no hg-engine source to read."; return false; }

            try { text = File.ReadAllText(file.FullPath); return true; }
            catch (Exception ex)
            {
                error = $"{file.RelPath} couldn't be read: {ex.Message}";
                return false;
            }
        }

        public static bool TryWriteLines(HgEngineOwnedFile file, IEnumerable<string> lines, out string error)
            => TryWriteText(file, string.Join("\n", lines) + "\n", out error);

        public static bool TryWriteText(HgEngineOwnedFile file, string text, out string error)
        {
            error = null;
            if (file?.FullPath == null) { error = "This file has no hg-engine source to write."; return false; }

            try
            {
                // The toolchain reads these under POSIX, so they stay LF whatever the editor produced.
                File.WriteAllText(file.FullPath, text.Replace("\r\n", "\n"));
                HgEngineFileCache.ClearCache();
                return true;
            }
            catch (Exception ex)
            {
                error = $"{file.RelPath} couldn't be written: {ex.Message}";
                return false;
            }
        }

        public static string CharMapRelPath(string root) => MakeVariable(ReadMakeFragment(root), "CHARMAP");

        /// <summary>The text validator the checkout's own build runs, or null when it declares none.</summary>
        public static string TextValidatorRelPath(string root)
        {
            string fragment = ReadMakeFragment(root);
            if (fragment == null) return null;
            Match m = Regex.Match(fragment, @"(\S*validate_text_archive\.py)");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static (Dictionary<string, HgEngineRule> rules,
                        Dictionary<string, Dictionary<int, HgEngineOwnedFile>> files) Load()
        {
            string root = HgEngineProject.IsActive ? HgEngineProject.RepoRootWindows : null;
            if (_rules != null && _filesByArchive != null && _cachedFor == root) return (_rules, _filesByArchive);

            _cachedFor = root;
            (_rules, _filesByArchive) = ScanRoot(root);
            return (_rules, _filesByArchive);
        }

        internal static (Dictionary<string, HgEngineRule> rules,
                         Dictionary<string, Dictionary<int, HgEngineOwnedFile>> files) ScanRoot(string root)
        {
            var rules = new Dictionary<string, HgEngineRule>(StringComparer.OrdinalIgnoreCase);
            var files = new Dictionary<string, Dictionary<int, HgEngineOwnedFile>>(StringComparer.OrdinalIgnoreCase);
            string fragment = root == null ? null : ReadMakeFragment(root);
            if (fragment == null) return (rules, files);

            foreach ((string variable, string label, HgEngineOwnership ownership) in Known)
            {
                // A target is written against $(FILESYS), so it is read raw and the prefix stripped.
                string archive = Normalise(MakeVariableRaw(fragment, variable + "_TARGET") ?? "");
                if (archive.Length == 0 || archive.Contains("$(", StringComparison.Ordinal)) continue;

                var rule = new HgEngineRule
                {
                    Variable = variable,
                    Label = label,
                    Ownership = ownership,
                    SourceDirRelPath = MakeVariable(fragment, variable + "_DEPENDENCIES_DIR"),
                    TargetArchive = archive,
                    ReplacesWholeArchive = !Regex.IsMatch(fragment,
                        @"extract\s+\$\(" + Regex.Escape(variable) + @"_TARGET\)"),
                };

                rules[rule.TargetArchive] = rule;
                files[rule.TargetArchive] = ScanRule(root, rule, fragment);
            }

            return (rules, files);
        }

        private static Dictionary<int, HgEngineOwnedFile> ScanRule(string root, HgEngineRule rule, string fragment)
        {
            var into = new Dictionary<int, HgEngineOwnedFile>();

            if (rule.SourceDirRelPath != null)
            {
                string dir = Path.Combine(root, rule.SourceDirRelPath.Replace('/', Path.DirectorySeparatorChar));
                if (Directory.Exists(dir))
                {
                    try
                    {
                        foreach (string path in Directory.EnumerateFiles(dir))
                        {
                            int id = IdFromName(Path.GetFileNameWithoutExtension(path));
                            if (id < 0 || into.ContainsKey(id)) continue;

                            into[id] = new HgEngineOwnedFile
                            {
                                Rule = rule,
                                Id = id,
                                RelPath = rule.SourceDirRelPath + "/" + Path.GetFileName(path),
                                FullPath = path,
                            };
                        }
                    }
                    catch (Exception ex) { AppLogger.Error("HgEngineOwnedFiles.ScanRule: " + ex.Message); }
                }
            }

            // Text archives the build generates from other domains are encoded after the hand-edited
            // ones onto the same file, so they win and are nobody's to edit here.
            if (rule.Variable == "MSGDATA")
            {
                var generated = new HgEngineRule
                {
                    Variable = rule.Variable,
                    Label = rule.Label,
                    Ownership = HgEngineOwnership.Generated,
                    SourceDirRelPath = rule.SourceDirRelPath,
                    TargetArchive = rule.TargetArchive,
                    ReplacesWholeArchive = rule.ReplacesWholeArchive,
                };
                foreach (int id in GeneratedTextArchives(fragment))
                {
                    into[id] = new HgEngineOwnedFile { Rule = generated, Id = id };
                }
            }

            return into;
        }

        /// <summary>Archives the msgdata rule encodes from build output rather than from its source directory.</summary>
        internal static IEnumerable<int> GeneratedTextArchives(string fragment)
        {
            foreach (Match m in Regex.Matches(fragment ?? "",
                @"MSGDATA_COMPILETIME_DEPENDENCIES\s*\+?=\s*(?<list>.+)"))
            {
                foreach (Match file in Regex.Matches(m.Groups["list"].Value, @"rawtext/(\d+)\.txt"))
                {
                    if (int.TryParse(file.Groups[1].Value, out int id)) yield return id;
                }
            }
        }

        internal static int IdFromName(string stem)
        {
            if (BareNumber.IsMatch(stem)) return int.Parse(stem);
            Match m = PrefixedNumber.Match(stem);
            return m.Success && int.TryParse(m.Groups[1].Value, out int id) ? id : -1;
        }

        /// <summary>A make variable's literal value, whatever it holds.</summary>
        private static string MakeVariableRaw(string fragment, string name)
        {
            if (fragment == null) return null;
            Match m = Regex.Match(fragment, @"^\s*" + Regex.Escape(name) + @"\s*:?=\s*(\S+)\s*$",
                RegexOptions.Multiline);
            return m.Success ? m.Groups[1].Value : null;
        }

        /// <summary>A make variable's value as a path. Anything still holding a $(...) is not one.</summary>
        private static string MakeVariable(string fragment, string name)
        {
            string value = MakeVariableRaw(fragment, name);
            return value != null && !value.Contains("$(", StringComparison.Ordinal) ? value : null;
        }

        /// <summary>Targets are written against the filesystem root; what identifies the archive is the rest.</summary>
        internal static string Normalise(string target)
        {
            string path = target.Replace('\\', '/');
            int marker = path.IndexOf("(FILESYS)/", StringComparison.Ordinal);
            if (marker >= 0) path = path.Substring(marker + "(FILESYS)/".Length);
            return path.Trim('/');
        }

        private static string ReadMakeFragment(string root)
        {
            try
            {
                string path = Path.Combine(root, MakeFragmentRelPath);
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception ex)
            {
                AppLogger.Error("HgEngineOwnedFiles.ReadMakeFragment: " + ex.Message);
                return null;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Finds and edits the starter's give command in a project's decompiled script sources.
    ///
    /// This is the way round to do it when the project has them. The game names the thing itself:
    ///
    ///     GetPlayerStarterSpecies VAR_0x8000
    ///     GivePokemon VAR_0x8000, 5, ITEM_NONE, VAR_RESULT
    ///
    /// A GivePokemon reading the same variable a GetPlayerStarterSpecies just wrote is the starter, and
    /// there is exactly one in an untouched Platinum. Looking for "the species comes from a variable"
    /// instead matches two, and GetPlayerStarterSpecies on its own appears twenty times, nearly all of
    /// them writing VAR_RESULT to ask which starter you picked.
    ///
    /// Editing goes through the same two steps the Script Editor's Save does: write the text, then
    /// compile the project. Nothing bespoke, so a starter edit behaves exactly like someone opening the
    /// script, changing the line by hand and saving it.
    /// </summary>
    public static class StarterRotomSource
    {
        /// <summary>One give command in the sources, with enough to show it and to change it.</summary>
        public sealed class Match
        {
            public int FileId;
            public int LineNumber;          // 1-based, as an editor shows it
            public string Line;             // the GivePokemon line, as written
            public string SpeciesArgument;  // VAR_0x8000, SPECIES_EEVEE, ...
            public int Level;
            public string HeldItemArgument; // ITEM_NONE, ITEM_ORAN_BERRY, ...
            public bool NamedAsStarter;     // preceded by GetPlayerStarterSpecies on the same variable
            public string Container;        // the "script script_13" it sits inside, blank if none
            public int IndexInContainer;    // which give command it is within that script, from 0
            public int ContainerNumber = -1; // the "#13" on that script, which is how RomInfo names it

            /// <summary>True when the species comes from a variable, which is how a starter arrives.</summary>
            public bool SpeciesFromVariable => LooksLikeVariable(SpeciesArgument);

            public string Where => string.IsNullOrEmpty(Container)
                ? $"script file {FileId}, line {LineNumber}"
                : $"script file {FileId}, {Container}, line {LineNumber}";
            public string Summary => NamedAsStarter
                ? $"the starter's, species from {SpeciesArgument}, level {Level}"
                : $"species {SpeciesArgument}, level {Level}";
            // Keyed by where it sits in the script rather than by line number, so editing something
            // above it in the file does not lose the user's choice.
            public string Key => $"rotom:{FileId}:{Container}:{IndexInContainer}";
        }

        public static bool IsAvailable =>
            RomInfo.hasRotomProject && Directory.Exists(SourceDir());

        private static string SourceDir() =>
            string.IsNullOrEmpty(RomInfo.workDir)
                ? null : Path.Combine(RomInfo.workDir, "expanded", "scripts");

        public static string PathFor(int fileId) =>
            Path.Combine(SourceDir() ?? "", fileId.ToString("D4") + ".rotom");

        // GivePokemon <species>, <level>, <item>, <result>
        private static readonly Regex Give = new Regex(
            @"^\s*GivePokemon\s+([^,]+?)\s*,\s*([^,]+?)\s*,\s*([^,]+?)\s*,",
            RegexOptions.Compiled);

        // script script_13 #13:  /  action action_4 #4:
        private static readonly Regex Container = new Regex(
            @"^\s*(?:script|function|action)\s+(\S+?)\s*(?:#(\d+))?\s*:", RegexOptions.Compiled);

        private static readonly Regex Starter = new Regex(
            @"^\s*GetPlayerStarterSpecies\s+(\S+)\s*$", RegexOptions.Compiled);

        /// <summary>
        /// Every give command in the sources, the one the game names as the starter first. Reading 575
        /// text files is quicker than parsing the binaries and needs no command database.
        /// </summary>
        public static List<Match> FindAll()
        {
            var found = new List<Match>();
            string dir = SourceDir();
            if (dir == null || !Directory.Exists(dir)) return found;

            foreach (string path in Directory.GetFiles(dir, "*.rotom"))
                if (int.TryParse(Path.GetFileNameWithoutExtension(path), out int fileId))
                    found.AddRange(ReadFile(fileId));

            return found.OrderByDescending(f => f.NamedAsStarter).ThenBy(f => f.FileId).ToList();
        }

        /// <summary>
        /// The give commands in one script source. This is what the editor reads when it already knows
        /// where the starter is, which is every time until somebody moves it.
        /// </summary>
        /// <summary>
        /// How many script sources have been read. The editor is supposed to read one on a project
        /// nobody has changed, so this is how a test can tell the shortcut is still being taken.
        /// </summary>
        public static int FilesRead { get; private set; }
        public static void ForgetFilesRead() => FilesRead = 0;

        public static List<Match> ReadFile(int fileId)
        {
            var found = new List<Match>();
            string path = PathFor(fileId);
            if (!File.Exists(path)) return found;

            string[] lines;
            try { lines = File.ReadAllLines(path); } catch { return found; }
            FilesRead++;

            {
                string container = null;
                int number = -1;
                int inContainer = 0;
                for (int i = 0; i < lines.Length; i++)
                {
                    var header = Container.Match(lines[i]);
                    if (header.Success)
                    {
                        container = header.Groups[1].Value;
                        number = header.Groups[2].Success ? int.Parse(header.Groups[2].Value) : -1;
                        inContainer = 0;
                    }

                    var m = Give.Match(lines[i]);
                    if (!m.Success) continue;

                    string species = m.Groups[1].Value.Trim();
                    int.TryParse(m.Groups[2].Value.Trim(), out int level);

                    found.Add(new Match
                    {
                        FileId = fileId,
                        LineNumber = i + 1,
                        Line = lines[i],
                        SpeciesArgument = species,
                        Level = level,
                        HeldItemArgument = m.Groups[3].Value.Trim(),
                        NamedAsStarter = PrecededByStarterLookup(lines, i, species),
                        Container = container,
                        IndexInContainer = inContainer++,
                        ContainerNumber = number,
                    });
                }
            }

            return found;
        }

        /// <summary>
        /// Whether a GetPlayerStarterSpecies just above writes the variable this command reads. A couple
        /// of lines of slack, because a WaitFadeScreen or the like can sit between them.
        /// </summary>
        private static bool PrecededByStarterLookup(string[] lines, int at, string speciesArgument)
        {
            // A file that has been through the old single-file decompile has no names left in it:
            // VAR_0x8000 reads as 0x8000. Both spellings have to count, or the starter becomes
            // unfindable in exactly the projects that have already been damaged.
            if (!LooksLikeVariable(speciesArgument)) return false;
            for (int back = 1; back <= 3 && at - back >= 0; back++)
            {
                var m = Starter.Match(lines[at - back]);
                if (!m.Success) continue;
                return string.Equals(m.Groups[1].Value.Trim(), speciesArgument, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        /// <summary>
        /// Rewrites the level and the held item on that line and saves, which is what the Script
        /// Editor does: write the source, then compile the project. Only the three arguments we own
        /// are rebuilt, so a comment or the result variable on that line survives. Returns null when
        /// it worked, or what went wrong, with the source put back if the compile refused it.
        /// </summary>
        public static async Task<string> SaveAsync(Match m, int? level, string heldItemArgument)
        {
            if (m == null) return "Nothing to change.";
            string path = PathFor(m.FileId);
            if (!File.Exists(path)) return Path.GetFileName(path) + " is not there any more.";

            string[] lines;
            try { lines = File.ReadAllLines(path); }
            catch (Exception ex) { return "Could not read the script source: " + ex.Message; }

            if (m.LineNumber < 1 || m.LineNumber > lines.Length)
                return "That line is no longer in the file.";

            string original = lines[m.LineNumber - 1];
            var give = Give.Match(original);
            if (!give.Success) return "That line is no longer a GivePokemon, so nothing was changed.";

            string newLevel = level?.ToString() ?? give.Groups[2].Value.Trim();
            string newItem = heldItemArgument ?? give.Groups[3].Value.Trim();
            string indent = original.Substring(0, original.Length - original.TrimStart().Length);
            string tail = original.Substring(give.Groups[3].Index + give.Groups[3].Length);

            lines[m.LineNumber - 1] =
                $"{indent}GivePokemon {give.Groups[1].Value.Trim()}, {newLevel}, {newItem}{tail}";

            try { File.WriteAllLines(path, lines); }
            catch (Exception ex) { return "Could not write the script source: " + ex.Message; }

            string failure = await CompileProjectAsync();
            if (failure != null)
            {
                lines[m.LineNumber - 1] = original;
                try { File.WriteAllLines(path, lines); } catch { }
                return failure;
            }

            m.Line = lines[m.LineNumber - 1];
            if (level.HasValue) m.Level = level.Value;
            if (heldItemArgument != null) m.HeldItemArgument = heldItemArgument;
            return null;
        }

        /// <summary>
        /// The same call the Script Editor makes. Project mode, run from the project root, which is
        /// where rotom.toml is and how it finds its database and constants. It only rebuilds what
        /// changed, so this costs one file.
        /// </summary>
        private static async Task<string> CompileProjectAsync()
        {
            if (!RotomTool.IsAvailable) return "The rotom tool is not available, so nothing was saved.";
            try
            {
                var result = await RotomTool.RunAsync("compile", "--json");
                if (!result.Success)
                    return "The script did not compile, so it was put back as it was: "
                         + RotomTool.FormatResult(result);
            }
            catch (Exception ex)
            {
                return "The script did not compile, so it was put back as it was: " + ex.Message;
            }
            return null;
        }
        /// <summary>
        /// Saves with a readable item name where rotom accepts one, and the plain number where it does
        /// not. Item names are rotom built-ins, not something the project database lists, so there is
        /// no way to know in advance whether ITEM_SOMETHING exists: try it, and fall back on the number
        /// if the compile says no. The number always compiles.
        /// </summary>
        public static async Task<string> SaveAsync(Match m, int? level, string itemName, int itemNumber)
        {
            if (m == null) return "Nothing to change.";

            if (!string.IsNullOrWhiteSpace(itemName))
            {
                string failure = await SaveAsync(m, level, itemName);
                if (failure == null) return null;
            }
            return await SaveAsync(m, level, itemNumber.ToString());
        }

        /// <summary>The rotom spelling of an item, from the name DSPRE shows. Null when it cannot.</summary>
        public static string ItemToken(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return null;
            var sb = new System.Text.StringBuilder("ITEM_");
            bool lastWasBreak = true;
            foreach (char c in displayName.Trim())
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(char.ToUpperInvariant(c));
                    lastWasBreak = false;
                }
                else if (!lastWasBreak)
                {
                    sb.Append('_');
                    lastWasBreak = true;
                }
            }
            string token = sb.ToString().TrimEnd('_');
            return token.Length > "ITEM_".Length ? token : null;
        }
        /// <summary>A variable, written either as a name or as the raw number a flattened file has.</summary>
        private static bool LooksLikeVariable(string argument)
        {
            if (string.IsNullOrWhiteSpace(argument)) return false;
            if (argument.StartsWith("VAR_", StringComparison.OrdinalIgnoreCase)) return true;

            string text = argument.Trim();
            bool hex = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
            return int.TryParse(hex ? text.Substring(2) : text,
                                hex ? System.Globalization.NumberStyles.HexNumber
                                    : System.Globalization.NumberStyles.Integer,
                                System.Globalization.CultureInfo.InvariantCulture, out int value)
                && value >= 0x4000;
        }
        /// <summary>What checking a place the user pointed at came to.</summary>
        public enum Verdict
        {
            Usable,            // a give command taking its species from a variable, which we can edit
            SpeciesIsItsOwn,   // a give command with the species written in, so it picks its own
            NotAGiveCommand,   // nothing there we can work with
        }

        public sealed class Check
        {
            public Verdict Verdict;
            public Match Found;
            public string Message;
        }

        /// <summary>
        /// Looks for a give command in the script the user named, so a romhack that moved the starter
        /// somewhere the pair rule cannot see can still be pointed at by hand.
        /// </summary>
        public static Check Verify(int fileId, string container)
        {
            var inThere = FindAll()
                .Where(m => m.FileId == fileId)
                .Where(m => string.IsNullOrWhiteSpace(container)
                         || string.Equals(m.Container, container.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();

            var usable = inThere.FirstOrDefault(m => m.SpeciesFromVariable);
            if (usable != null)
                return new Check { Verdict = Verdict.Usable, Found = usable,
                                   Message = "Found it: " + usable.Summary + "." };

            if (inThere.Count > 0)
                return new Check { Verdict = Verdict.SpeciesIsItsOwn, Found = inThere[0],
                                   Message = "This script seems to manage species on its own, which we do not handle." };

            return new Check { Verdict = Verdict.NotAGiveCommand,
                               Message = "There is no give-a-Pokemon command there." };
        }

        /// <summary>The scripts in a file, so the picker can offer them rather than ask for a number.</summary>
        public static List<string> ContainersIn(int fileId)
        {
            var names = new List<string>();
            string path = PathFor(fileId);
            if (!File.Exists(path)) return names;
            try
            {
                foreach (string line in File.ReadAllLines(path))
                {
                    var m = Container.Match(line);
                    if (m.Success) names.Add(m.Groups[1].Value);
                }
            }
            catch { }
            return names;
        }

        /// <summary>
        /// The give command where an untouched game keeps its starter, read from that one file. Null when
        /// it does not hold what it should, which is the only case worth reading the other 574 files for.
        /// </summary>
        public static Match FindVanilla()
        {
            int file = RomInfo.starterHeldItemScriptFileID;
            int script = RomInfo.starterCommandScriptNumber;
            if (file < 0 || script < 0) return null;

            return ReadFile(file).FirstOrDefault(m => m.ContainerNumber == script && m.NamedAsStarter);
        }

        /// <summary>The command a remembered choice points at, again without reading the whole game.</summary>
        public static Match FindByKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            string[] bits = key.Split(':');
            if (bits.Length < 2 || !int.TryParse(bits[1], out int fileId)) return null;
            return ReadFile(fileId).FirstOrDefault(m => m.Key == key);
        }

        /// <summary>The one the game names as the starter, or null when the sources do not say.</summary>
        public static Match FindStarter() => FindAll().FirstOrDefault(f => f.NamedAsStarter);

    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// Adds a trainer class to an hg-engine checkout the way its "Adding New Trainer Classes" guide does: the
    /// TRAINERCLASS_ constant in trainerclass.h and armips' constants.s, the name in texts 730 and 731, a copy of
    /// another class's five trainer_gfx files, a gender row, a prize money row with bytereplacement's count raised,
    /// and optionally an eye-contact music row. Everything is written together or not at all.
    /// </summary>
    public static class HgEngineTrainerClassExpansion
    {
        private const string HeaderRelPath = "include/constants/trainerclass.h";
        private const string AsmRelPath = "armips/include/constants.s";
        private const string MoneyRelPath = "src/trainermoney.c";
        private const string ByteReplacementRelPath = "bytereplacement";
        private const string Prefix = "TRAINERCLASS_";
        private const string GenderSwitch = "EXPAND_TRAINER_GENDER_TABLE";
        private const string MoneySwitch = "EXPAND_TRAINER_PRIZE_MONEY";
        private static readonly string[] SpriteSuffixes = { ".png", "_anim.json", "_cell.json", "_enc.png", "_enc.png.key" };

        /// <summary>The id the next class gets: one past the class names, unless a constant already uses it.</summary>
        public static int NextId(out string error)
        {
            error = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout linked."; return -1; }
            HgEngineOwnedFile names = NameFile(RomInfo.trainerClassMessageNumber);
            if (names == null || !HgEngineOwnedFiles.TryReadLines(names, out List<string> lines, out error)) { error ??= "The trainer class names text isn't in the checkout."; return -1; }
            int next = lines.Count;
            HgEngineSymbolTable classes = HgEngineSymbolTable.Load(HeaderRelPath);
            if (classes == null) { error = $"{HeaderRelPath} is missing from the checkout."; return -1; }
            if (classes.TryGetNameWithPrefix(next, Prefix, out string taken))
            { error = $"{taken} already uses {next}, the id after the last class name. Make the names and constants agree first."; return -1; }
            if (!classes.TryGetNameWithPrefix(next - 1, Prefix, out _))
            { error = $"{HeaderRelPath} has no class {next - 1}, so the names and constants don't line up."; return -1; }
            return next;
        }

        /// <summary>Whether the new class's id already has sprite files, which the add then keeps.</summary>
        public static bool HasSprite(int id) => HgEngineProject.IsActive && File.Exists(HgEngineTrainerGraphicsSource.Stem(false, id) + ".png");

        /// <summary>Why a class can't be added now, or null; the name is checked when given.</summary>
        public static string AddRefusal(string name = null)
        {
            if (!HgEngineProject.IsActive) return "No hg-engine checkout linked.";
            if (name != null && string.IsNullOrWhiteSpace(name)) return "Give the class a name.";
            HashSet<string> defined = HgEngineConfigState.DefinedNames();
            if (!defined.Contains(GenderSwitch))
                return $"Turn on {GenderSwitch} in hg-engine Settings first. Without it the game reads a new class's gender from past the end of its own table.";
            if (!defined.Contains(MoneySwitch))
                return $"Turn on {MoneySwitch} in hg-engine Settings first. Without it the game has no prize money row for a new class.";
            foreach (int archive in new[] { RomInfo.trainerClassMessageNumber, RomInfo.trainerClassMessageNumber + 1 })
            {
                HgEngineOwnedFile file = NameFile(archive);
                if (file == null) return $"Text {archive} isn't in the checkout.";
                if (file.Ownership != HgEngineOwnership.EditableSource) return $"hg-engine generates text {archive} during its build, so a class name can't be added there.";
            }
            return NextId(out string error) < 0 ? error : null;
        }

        /// <summary>Adds the class. <paramref name="spriteFrom"/> is the class whose sprite files are copied, or -1 to
        /// keep files already at the new id. Music 0 adds no eye-contact music row.</summary>
        public static bool TryAdd(string name, string nameWithArticle, int gender, int prizeMultiplier, int spriteFrom, ushort music,
            out int id, out string error)
        {
            id = -1;
            error = AddRefusal(name ?? "");
            if (error != null) return false;
            id = NextId(out error);
            if (id < 0) return false;
            name = name.Trim();
            string symbol = UniqueSymbol(name);

            // Everything is planned and checked before the first write.
            string header = Read(HeaderRelPath, out string headerPath), asm = Read(AsmRelPath, out string asmPath);
            if (header == null || asm == null) { error = $"{HeaderRelPath} or {AsmRelPath} is missing from the checkout."; return false; }
            if (!TryInsertAfterValue(header, @"^(\s*#define\s+)(" + Prefix + @"\w+)(\s+)\(?(\d+)\)?", id - 1, symbol, id, out string newHeader))
            { error = $"{HeaderRelPath} has no #define line for class {id - 1} to add after."; return false; }
            if (!TryInsertAfterValue(asm, @"^(\s*\.equ\s+)(" + Prefix + @"\w+)(,\s*)(\d+)", id - 1, symbol, id, out string newAsm))
            { error = $"{AsmRelPath} has no .equ line for class {id - 1} to add after."; return false; }

            HgEngineOwnedFile nameFile = NameFile(RomInfo.trainerClassMessageNumber);
            HgEngineOwnedFile articleFile = NameFile(RomInfo.trainerClassMessageNumber + 1);
            if (!HgEngineOwnedFiles.TryReadLines(nameFile, out List<string> nameLines, out error)) return false;
            if (!HgEngineOwnedFiles.TryReadLines(articleFile, out List<string> articleLines, out error)) return false;
            if (nameLines.Count != id || articleLines.Count != id)
            { error = $"Texts {RomInfo.trainerClassMessageNumber} and {RomInfo.trainerClassMessageNumber + 1} should both have {id} lines; make them agree first."; return false; }

            string sprites = Path.GetDirectoryName(HgEngineTrainerGraphicsSource.Stem(false, id));
            List<(string From, string To)> copies = new List<(string From, string To)>();
            if (spriteFrom >= 0)
            {
                foreach (string suffix in SpriteSuffixes)
                {
                    string from = HgEngineTrainerGraphicsSource.Stem(false, spriteFrom) + suffix, to = HgEngineTrainerGraphicsSource.Stem(false, id) + suffix;
                    if (!File.Exists(from)) { error = $"Class {spriteFrom} has no {Path.GetFileName(from)} to copy."; return false; }
                    if (File.Exists(to)) { error = $"{Path.GetFileName(to)} is already in {sprites}; keep that sprite instead of copying one."; return false; }
                    copies.Add((from, to));
                }
            }
            else if (!HasSprite(id)) { error = $"There is no sprite at {id} to keep; pick a class to copy one from."; return false; }

            if (music != 0 && !HgEngineMusicTables.TablesInSource)
            { error = "This checkout doesn't build its music tables from source, so the class can't get eye-contact music here."; return false; }

            string bytes = Read(ByteReplacementRelPath, out string bytesPath);
            if (bytes == null) { error = $"{ByteReplacementRelPath} is missing from the checkout."; return false; }

            // Original text of every file touched, put back if any step fails.
            Dictionary<string, string> originals = new Dictionary<string, string>
            {
                [headerPath] = header, [asmPath] = asm, [bytesPath] = bytes,
            };
            foreach (string rel in new[] { MoneyRelPath, "src/pokemon.c", "src/music_tables.c" })
                if (Read(rel, out string p) is string t) originals[p] = t;
            List<string> created = new List<string>();
            try
            {
                HgEngineFileCache.WriteText(headerPath, newHeader);
                HgEngineFileCache.WriteText(asmPath, newAsm);
                HgEngineSymbolTable.ClearCache();

                if (!HgEngineOwnedFiles.TryWriteLines(nameFile, nameLines.Append(name).ToList(), out error)) throw new IOException(error);
                if (!HgEngineOwnedFiles.TryWriteLines(articleFile, articleLines.Append((nameWithArticle ?? "").Trim()).ToList(), out error))
                    throw new IOException(error);

                // A copy keeps its template's date, and make rebuilds secondary files only from newer sources.
                foreach ((string from, string to) in copies) { HgEngineOverworlds.CopyAsNew(from, to); created.Add(to); }

                if (!HgEngineTrainerClassTables.TrySetGender(id, gender, out error)) throw new IOException(error);
                if (!HgEngineTrainerClassTables.TrySetPrizeMultiplier(id, prizeMultiplier, out error)) throw new IOException(error);
                if (!TryRaisePrizeCount(bytesPath, out error)) throw new IOException(error);
                if (music != 0 && !HgEngineMusicTables.TrySetEncounterMusic(id, music, music, out error)) throw new IOException(error);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
            {
                foreach ((string path, string text) in originals)
                {
                    try { HgEngineFileCache.WriteText(path, text); } catch (Exception undo) { AppLogger.Error("HgEngineTrainerClassExpansion rollback: " + undo.Message); }
                }
                foreach ((HgEngineOwnedFile file, List<string> lines) in new[] { (nameFile, nameLines), (articleFile, articleLines) })
                    HgEngineOwnedFiles.TryWriteLines(file, lines, out _);
                foreach (string path in created)
                {
                    try { File.Delete(path); } catch (Exception undo) { AppLogger.Error("HgEngineTrainerClassExpansion rollback: " + undo.Message); }
                }
                HgEngineSymbolTable.ClearCache();
                error = $"The new class couldn't be written, so nothing was added: {ex.Message}";
                id = -1;
                return false;
            }
            return true;
        }

        private static HgEngineOwnedFile NameFile(int archive) =>
            HgEngineOwnedFiles.Get(HgEngineOwnedFiles.ArchiveOf(RomInfo.DirNames.textArchives), archive);

        // TRAINERCLASS_ plus the name in capitals, made unique with a number.
        private static string UniqueSymbol(string name)
        {
            string stem = Prefix + Regex.Replace(Regex.Replace(name.ToUpperInvariant(), @"[^A-Z0-9]+", "_"), "^_+|_+$", "");
            if (stem == Prefix) stem = Prefix + "NEW";
            HgEngineSymbolTable classes = HgEngineSymbolTable.Load(HeaderRelPath);
            string symbol = stem;
            for (int n = 2; classes != null && classes.TryGetValue(symbol, out _); n++) symbol = stem + "_" + n;
            return symbol;
        }

        // Adds "<symbol> <value>" on a new line after the one defining <after>, spaced like it.
        private static bool TryInsertAfterValue(string text, string linePattern, int after, string symbol, int value, out string updated)
        {
            updated = null;
            Match line = Regex.Matches(text, linePattern, RegexOptions.Multiline).Cast<Match>()
                .LastOrDefault(m => int.TryParse(m.Groups[4].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v == after);
            if (line == null) return false;
            int end = text.IndexOf('\n', line.Index);
            if (end < 0) end = text.Length;
            string newline = text.Contains("\r\n") ? "\r\n" : "\n";
            int column = line.Groups[1].Length + line.Groups[2].Length + line.Groups[3].Length;
            string head = line.Groups[1].Value + symbol;
            string gap = line.Groups[3].Value.Contains(',') ? "," + new string(' ', Math.Max(1, column - head.Length - 1)) : new string(' ', Math.Max(1, column - head.Length));
            string add = head + gap + value.ToString(CultureInfo.InvariantCulture);
            int insertAt = end < text.Length ? end + 1 : end;
            updated = text.Insert(insertAt, (end < text.Length ? "" : newline) + add + newline);
            return true;
        }

        // bytereplacement's EXPAND_TRAINER_PRIZE_MONEY branch tells the game how many PrizeMoney rows to search.
        private static bool TryRaisePrizeCount(string bytesPath, out string error)
        {
            error = null;
            string text = HgEngineFileCache.GetText(bytesPath);
            string money = Read(MoneyRelPath, out _);
            CDeclaration table = money == null ? null : CSourceFile.For(money).Find("PrizeMoney");
            if (table == null) { error = $"{MoneyRelPath} has no PrizeMoney table."; return false; }
            int rows = table.Init.Items.Count(i => i.List != null && HgEngineConfigState.Compiles(i.Conditions) != false);

            int branch = text.IndexOf("#ifdef " + MoneySwitch, StringComparison.Ordinal);
            int heading = branch < 0 ? -1 : text.IndexOf("# PrizeMoney table range", branch, StringComparison.Ordinal);
            int stop = branch < 0 ? -1 : text.IndexOf("#else", branch, StringComparison.Ordinal);
            if (heading < 0 || (stop >= 0 && heading > stop))
            { error = $"{ByteReplacementRelPath} has no PrizeMoney table range under #ifdef {MoneySwitch}."; return false; }

            StringBuilder sb = new StringBuilder(text);
            int at = text.IndexOf('\n', heading) + 1, changed = 0;
            Regex countLine = new Regex(@"^[ \t]*\w+[ \t]+[0-9A-Fa-f]{8}[ \t]+([0-9A-Fa-f]{2})[ \t]*\r?$");
            while (at > 0 && at < text.Length)
            {
                int end = text.IndexOf('\n', at);
                if (end < 0) end = text.Length;
                Match m = countLine.Match(text.Substring(at, end - at));
                if (!m.Success) break;
                int count = int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                if (count != rows - 1)
                { error = $"PrizeMoney had {rows - 1} rows but {ByteReplacementRelPath} searches {count}. Make them agree first."; return false; }
                sb.Remove(at + m.Groups[1].Index, 2).Insert(at + m.Groups[1].Index, rows.ToString("X2", CultureInfo.InvariantCulture));
                changed++;
                at = end + 1;
            }
            if (changed == 0) { error = $"{ByteReplacementRelPath}'s PrizeMoney table range has no count lines."; return false; }
            if (rows > 255) { error = "PrizeMoney can hold at most 255 rows."; return false; }
            HgEngineFileCache.WriteText(bytesPath, sb.ToString());
            return true;
        }

        private static string Read(string relPath, out string path)
        {
            path = Path.Combine(HgEngineProject.RepoPathUnc, relPath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? HgEngineFileCache.GetText(path) : null;
        }
    }
}

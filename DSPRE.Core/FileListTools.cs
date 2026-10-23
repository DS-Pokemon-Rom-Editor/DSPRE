using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DSPRE
{
    /// <summary>
    /// Renaming the files of a folder, by a list of names or by what each file is, and writing such name lists from a
    /// folder or a C enum. A list holds one name per line; empty lines and lines starting with # are skipped.
    /// </summary>
    public static class FileListTools
    {
        public const string IsolatedFolderName = "DSPRE_IsolatedFiles";

        /// <summary>The folder's files in the order these tools take them: by name, with numbers compared as numbers.</summary>
        public static FileInfo[] FilesInOrder(string dir)
        {
            FileInfo[] files = new DirectoryInfo(dir).GetFiles();
            // Numbers are padded to the longest one in any name, so 2 comes before 10.
            int width = files.SelectMany(f => Regex.Matches(f.Name, @"\d+").Select(m => m.Value.Length)).DefaultIfEmpty(1).Max();
            return files.OrderBy(f => Regex.Replace(f.Name, @"\d+", m => m.Value.PadLeft(width, '0')), StringComparer.Ordinal).ToArray();
        }

        /// <summary>The three comment lines a list file starts with.</summary>
        public static List<string> ListHeader(string about) => new List<string>
        {
            "#============================================================================",
            "# File enumeration definition " + about,
            "#============================================================================",
        };

        /// <summary>The names a list file gives, without its comments and empty lines.</summary>
        public static List<string> ReadList(IEnumerable<string> lines) =>
            lines.Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#", StringComparison.Ordinal)).ToList();

        /// <summary>A list naming the folder's files as they are now.</summary>
        public static List<string> FolderList(string dir)
        {
            List<string> list = ListHeader("for folder \"" + new DirectoryInfo(dir).Name + "\"");
            list.AddRange(FilesInOrder(dir).Select(f => f.Name));
            return list;
        }

        /// <summary>
        /// Renames the folder's files, in order, to the list's names, and optionally moves the files left over into
        /// a subfolder. A name already taken gets a number added. Returns how many were renamed and moved.
        /// </summary>
        public static (int Renamed, int Isolated) RenameByList(string dir, IReadOnlyList<string> names, bool isolateRest)
        {
            FileInfo[] files = FilesInOrder(dir);
            int count = Math.Min(files.Length, names.Count);
            int renamed = 0;
            for (int i = 0; i < count; i++)
            {
                string target = Path.Combine(dir, Path.GetFileName(names[i]));
                if (string.Equals(files[i].FullName, target, StringComparison.OrdinalIgnoreCase)) continue;
                PlainFiles.Move(files[i].FullName, UniqueName(target));
                renamed++;
            }

            int isolated = 0;
            if (isolateRest && files.Length > count)
            {
                string isolatedDir = Path.Combine(dir, IsolatedFolderName);
                Directory.CreateDirectory(isolatedDir);
                for (int i = count; i < files.Length; i++)
                {
                    PlainFiles.Move(files[i].FullName, UniqueName(Path.Combine(isolatedDir, files[i].Name)));
                    isolated++;
                }
            }
            return (renamed, isolated);
        }

        /// <summary>
        /// Gives each file a name from its contents: Nitro files (BMD0, BTX0, BCA0 and the like) their model name and
        /// an .ns* extension, other recognised files their magic as the extension. Returns the list of new names, after
        /// a header; files that could not be recognised keep their name and are left out.
        /// </summary>
        public static List<string> RenameByContent(string dir)
        {
            List<string> list = ListHeader("for folder \"" + new DirectoryInfo(dir).Name + "\"");
            foreach (FileInfo f in FilesInOrder(dir))
            {
                string name = NameFromContent(f.FullName);
                if (name == null) continue;
                string target = UniqueName(Path.Combine(dir, name), f.FullName);
                if (!string.Equals(f.FullName, target, StringComparison.OrdinalIgnoreCase)) PlainFiles.Move(f.FullName, target);
                list.Add(Path.GetFileName(target));
            }
            return list;
        }

        /// <summary>The name a file's contents suggest, or null when its contents are not recognised.</summary>
        public static string NameFromContent(string path)
        {
            byte[] head = ReadStart(path, 16);
            if (head == null || head.Length < 16) return null;
            string stem = Path.GetFileNameWithoutExtension(path);

            // B**0: a Nitro file. Its first block's first name is the model, animation or pack name.
            if (head[0] == 'B' && head[3] == '0')
            {
                string magic = Encoding.ASCII.GetString(head, 0, 3).ToLowerInvariant();
                if (!magic.All(char.IsLetterOrDigit)) return null;
                string named = stem;
                if (!(head[1] == 'T' && head[2] == 'X'))
                {
                    int nameOffset = 52 + 4 * (BitConverter.ToUInt16(head, 0xE) - 1);
                    byte[] raw = ReadStart(path, nameOffset + 16);
                    if (raw != null && raw.Length >= nameOffset + 16)
                    {
                        string read = Encoding.UTF8.GetString(raw, nameOffset, 16).TrimEnd('\0');
                        if (read.Length > 0 && read.IndexOfAny(Path.GetInvalidFileNameChars()) < 0) named = read;
                    }
                }
                return named + ".ns" + magic;
            }

            // Other Nintendo files carry a four-letter magic, some after a five-byte prefix.
            int at = head[5] == 'R' && head[8] == 'N' ? 5 : 0;
            string tag = Encoding.ASCII.GetString(head, at, 4).ToLowerInvariant();
            return tag.All(char.IsLetterOrDigit) ? stem + "." + tag : null;
        }

        /// <summary>
        /// A list from a C enum: one name per entry in value order, the prefix all entries share dropped and the last
        /// underscore turned into a dot, so FILE_NAME_NSBMD reads name.nsbmd. Throws FormatException with the reason.
        /// </summary>
        public static List<string> EnumList(IReadOnlyList<string> lines, string about)
        {
            string[] code = lines.Select(l => l.Trim()).ToArray();
            int start = Array.FindIndex(code, l => l.Replace(" ", "").Contains("enum{") || l.Replace(" ", "") == "enum");
            if (start < 0) throw new FormatException("There is no enum in the file.");
            if (!code[start].Contains("{")) start++;   // "enum" and "{" on separate lines
            int end = Array.FindIndex(code, start + 1, l => l.Replace(" ", "").StartsWith("}", StringComparison.Ordinal));
            if (end < 0) throw new FormatException("The enum never closes.");

            List<string> entries = new List<string>();
            for (int i = start + 1; i < end; i++)
            {
                string line = code[i];
                int comment = line.IndexOf("//", StringComparison.Ordinal);
                if (comment >= 0) line = line.Substring(0, comment);
                line = line.Trim().TrimEnd(',').Trim();
                if (line.Length > 0) entries.Add(line);
            }
            if (entries.Count < 2) throw new FormatException("The enum needs at least two entries.");

            string first = entries[0].Split('=')[0].Trim(), second = entries[1].Split('=')[0].Trim();
            int same = first.Zip(second, (a, b) => a == b).TakeWhile(x => x).Count();
            int prefix = first.Substring(0, same).LastIndexOf('_') + 1;

            SortedDictionary<int, string> byValue = new SortedDictionary<int, string>();
            int next = 0;
            foreach (string entry in entries)
            {
                string[] parts = entry.Split('=');
                string name = parts[0].Trim();
                if (parts.Length > 1)
                {
                    string value = parts[1].Trim();
                    if (!(value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                            ? int.TryParse(value.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out next)
                            : int.TryParse(value, out next)))
                        throw new FormatException($"\"{entry}\" has a value that isn't a plain number.");
                }
                name = name.Length > prefix ? name.Substring(prefix) : name;
                int underscore = name.LastIndexOf('_');
                if (underscore >= 0) name = name.Remove(underscore, 1).Insert(underscore, ".");
                byValue[next] = name;
                next++;
            }

            List<string> list = ListHeader("based on \"" + about + "\"");
            list.AddRange(byValue.Values);
            return list;
        }

        /// <summary>The path, or the path with (1), (2)... before its extension when that is taken by another file.</summary>
        public static string UniqueName(string path, string movingFrom = null)
        {
            string dir = Path.GetDirectoryName(path), stem = Path.GetFileNameWithoutExtension(path), ext = Path.GetExtension(path);
            string candidate = path;
            for (int n = 1; File.Exists(candidate) && !string.Equals(candidate, movingFrom, StringComparison.OrdinalIgnoreCase); n++)
                candidate = Path.Combine(dir, $"{stem}({n}){ext}");
            return candidate;
        }

        private static byte[] ReadStart(string path, int count)
        {
            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            byte[] buffer = new byte[Math.Min(count, (int)Math.Min(stream.Length, int.MaxValue))];
            int read = stream.Read(buffer, 0, buffer.Length);
            return read == buffer.Length ? buffer : buffer.Take(read).ToArray();
        }
    }
}

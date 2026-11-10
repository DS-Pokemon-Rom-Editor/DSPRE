using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>Which of hg-engine's hook lists an entry belongs to, and therefore its shape.</summary>
    public enum HgEngineHookKind
    {
        /// <summary>hooks: binary, symbol, address, and the register the branch goes through.</summary>
        Hook,

        /// <summary>armhooks: the same, assembled as ARM, not THUMB.</summary>
        ArmHook,

        /// <summary>bytereplacement: binary, address, and the bytes to write there.</summary>
        ByteReplacement,

        /// <summary>repoints: binary, symbol, address, writing a pointer.</summary>
        Repoint,
    }

    /// <summary>
    /// One line of a hook list. A line DSPRE does not understand keeps its text and is written back
    /// exactly as it came, so comments, includes and conditionals survive editing.
    /// </summary>
    public sealed class HgEngineHookEntry
    {
        public HgEngineHookKind Kind { get; set; }

        /// <summary>The raw text, written back as it is when the line isn't parsed.</summary>
        public string RawLine { get; set; }

        public bool Parsed { get; set; }

        /// <summary>-1 for arm9, otherwise the overlay number.</summary>
        public int OverlayNumber { get; set; } = -1;

        /// <summary>The routine being hooked in, or the table being repointed. Empty for a replacement.</summary>
        public string Symbol { get; set; } = "";

        /// <summary>As written in the list: a load address, or a file offset against 0x08000000.</summary>
        public long Address { get; set; }

        /// <summary>hooks only. -1 when the column is absent, which means the whole routine is replaced.</summary>
        public int Register { get; set; } = -1;

        /// <summary>bytereplacement only.</summary>
        public IReadOnlyList<byte> Bytes { get; set; } = Array.Empty<byte>();

        /// <summary>The #ifdef / #ifndef conditions the line sits under, as the list writes them, or empty.</summary>
        public string Condition { get; set; } = "";

        /// <summary>Each condition of <see cref="Condition"/>, outermost first: a name, or "not " and a name.</summary>
        public IReadOnlyList<string> Conditions { get; set; } = Array.Empty<string>();

        /// <summary>The "# text" comment line the entry's block starts with, without the '#'. A blank line or a
        /// directive ends the block.</summary>
        public string Heading { get; set; } = "";

        /// <summary>Whether the build applies it: false when the checkout's defines rule it out, null when that turns on a make flag.</summary>
        public bool? Built { get; set; } = true;

        /// <summary>Why <see cref="Built"/> is not true, in words.</summary>
        public string BuiltNote { get; set; } = "";

        public string BinaryName => OverlayNumber < 0 ? "arm9" : $"overlay {OverlayNumber}";

        /// <summary>No register, or the 255 that stands for one, replaces the routine outright.</summary>
        public bool ReplacesWholeRoutine => Register < 0 || Register == 0xFF;

        /// <summary>What this entry does, in the words the list itself uses.</summary>
        public string Describes => Kind switch
        {
            HgEngineHookKind.Hook when ReplacesWholeRoutine => $"replaces the routine at 0x{Address:X8} with {Symbol}",
            HgEngineHookKind.Hook => $"branches to {Symbol} through r{Register}",
            HgEngineHookKind.ArmHook => $"branches to {Symbol} through r{Register}, in ARM",
            HgEngineHookKind.Repoint => $"points at {Symbol}",
            _ => $"writes {Plural.Of(Bytes.Count, "byte")}",
        };

        internal string Render() => Kind switch
        {
            HgEngineHookKind.ByteReplacement =>
                $"{BinaryField} {Address:X8} {string.Join(" ", Bytes.Select(b => b.ToString("X2")))}",
            HgEngineHookKind.Repoint => $"{BinaryField} {Symbol} {Address:X8}",
            HgEngineHookKind.Hook when Register < 0 => $"{BinaryField} {Symbol} {Address:X8}",
            _ => $"{BinaryField} {Symbol} {Address:X8} {Register}",
        };

        private string BinaryField => OverlayNumber < 0 ? "arm9" : OverlayNumber.ToString("D4");

        /// <summary>How many bytes the build writes for this entry, and so claims.</summary>
        public int Length => Kind switch
        {
            HgEngineHookKind.ByteReplacement => Bytes.Count,
            HgEngineHookKind.Repoint => 4,
            HgEngineHookKind.ArmHook => 8 + 4,
            // A branch takes an extra halfword off a word boundary. A missing register column, or 255, replaces the
            // whole routine: 0x18 of code plus its address.
            _ => ReplacesWholeRoutine ? 0x18 + 4 : ((Address & ~1L) % 4 != 0 ? 6 + 4 : 4 + 4),
        };

        /// <summary>
        /// Where it lands in the file. An address is either a main-RAM one, relative to where the binary
        /// loads, or already a file offset written against 0x08000000.
        /// </summary>
        public long FileOffset(Func<int, long> overlayLoadAddress)
        {
            const long MainRam = 0x02000000;
            if ((Address & MainRam) == 0) return Address - 0x08000000;

            long loadAddress = OverlayNumber < 0 ? MainRam : overlayLoadAddress(OverlayNumber);
            return loadAddress == 0 ? -1 : Address - loadAddress;
        }
    }

    /// <summary>
    /// One of the checkout's hook lists, read so it can be shown and added to. Only lines matching the
    /// list's own shape are parsed; everything else is carried through untouched, so writing a file back
    /// changes nothing but the entries that were edited.
    /// </summary>
    public sealed class HgEngineHookList
    {
        public HgEngineHookKind Kind { get; }
        public string FileName { get; }
        public string FullPath { get; }

        // Kept so a save changes only the edited lines.
        private bool _endsWithNewline = true;
        public List<HgEngineHookEntry> Entries { get; } = new();

        private HgEngineHookList(HgEngineHookKind kind, string fileName, string fullPath)
        {
            Kind = kind;
            FileName = fileName;
            FullPath = fullPath;
        }

        public static readonly (HgEngineHookKind Kind, string FileName)[] Known =
        {
            (HgEngineHookKind.Hook, "hooks"),
            (HgEngineHookKind.ArmHook, "armhooks"),
            (HgEngineHookKind.ByteReplacement, "bytereplacement"),
            (HgEngineHookKind.Repoint, "repoints"),
        };

        /// <summary>Every list the linked checkout has.</summary>
        public static List<HgEngineHookList> ReadAll()
            => ReadAllAt(HgEngineProject.IsActive ? HgEngineProject.RepoRootWindows : null);

        /// <summary>The linked checkout's list of this kind, or null when it has none.</summary>
        public static HgEngineHookList Read(HgEngineHookKind kind)
        {
            if (!HgEngineProject.IsActive) return null;
            string name = Known.First(k => k.Kind == kind).FileName;
            string path = Path.Combine(HgEngineProject.RepoRootWindows, name);
            return File.Exists(path) ? Read(kind, name, path) : null;
        }

        internal static List<HgEngineHookList> ReadAllAt(string root)
        {
            List<HgEngineHookList> lists = new List<HgEngineHookList>();
            if (root == null) return lists;

            foreach ((HgEngineHookKind kind, string name) in Known)
            {
                string path = Path.Combine(root, name);
                if (File.Exists(path)) lists.Add(Read(kind, name, path));
            }
            return lists;
        }

        internal static HgEngineHookList Read(HgEngineHookKind kind, string fileName, string path)
        {
            HgEngineHookList list = new HgEngineHookList(kind, fileName, path);
            try
            {
                string text = File.ReadAllText(path);
                list._endsWithNewline = text.Length == 0 || text.EndsWith('\n');
                foreach (string raw in File.ReadAllLines(path))
                    list.Entries.Add(Parse(kind, raw));
                MarkConditions(list.Entries, Path.GetDirectoryName(path));
            }
            catch (Exception ex) { AppLogger.Error($"HgEngineHookList.Read({fileName}): " + ex.Message); }
            return list;
        }

        /// <summary>
        /// Settles each line's #ifdef / #ifndef the way make.py does: an #include "file" line adds every line of that file
        /// starting with "#define ", the -D flags make passes count as defined, and only #ifdef, #ifndef, #else and
        /// #endif are understood. Flags the Makefile passes only for some builds leave a line as depending on them.
        /// </summary>
        private static void MarkConditions(List<HgEngineHookEntry> entries, string root)
        {
            HashSet<string> defined = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> makeFlags = MakeFlags(root);
            List<(string Name, bool WhenDefined)> open = new List<(string, bool)>();
            string heading = "";
            foreach (HgEngineHookEntry entry in entries)
            {
                string raw = entry.RawLine ?? "";
                string line = raw.Trim();
                string upper = line.ToUpperInvariant();
                string[] words = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                // "# text" is a comment and "#word" a directive.
                if (line.Length == 0 || (line[0] == '#' && line.Length > 1 && !char.IsWhiteSpace(line[1]))) heading = "";
                else if (line[0] == '#') heading = line.Substring(1).Trim();
                entry.Heading = heading;
                if (raw.StartsWith("#include \""))
                {
                    string file = Path.Combine(root, raw.Split('"')[1].Trim().Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(file))
                        foreach (string define in File.ReadLines(file).Where(l => l.StartsWith("#define ")))
                        {
                            string[] parts = define.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 1) defined.Add(parts[1]);
                        }
                    continue;
                }
                if (upper.StartsWith("#IFDEF ") && words.Length > 1) { open.Insert(0, (words[1], true)); continue; }
                if (upper.StartsWith("#IFNDEF ") && words.Length > 1) { open.Insert(0, (words[1], false)); continue; }
                if (upper == "#ELSE" && open.Count > 0) { open[0] = (open[0].Name, !open[0].WhenDefined); continue; }
                if (upper == "#ENDIF" && open.Count > 0) { open.RemoveAt(0); continue; }
                if (open.Count == 0) continue;

                entry.Conditions = open.AsEnumerable().Reverse().Select(c => (c.WhenDefined ? "" : "not ") + c.Name).ToList();
                entry.Condition = string.Join(" and ", entry.Conditions);
                (string Name, bool WhenDefined) off = open.FirstOrDefault(c => !makeFlags.Contains(c.Name) && defined.Contains(c.Name) != c.WhenDefined);
                (string Name, bool WhenDefined) depends = open.FirstOrDefault(c => makeFlags.Contains(c.Name));
                if (off.Name != null)
                {
                    entry.Built = false;
                    entry.BuiltNote = off.WhenDefined ? $"Off: {off.Name} isn't defined." : $"Off: {off.Name} is defined.";
                }
                else if (depends.Name != null)
                {
                    entry.Built = null;
                    entry.BuiltNote = $"Depends on {depends.Name}, which make passes only for some builds.";
                }
            }
        }

        // -D flags in the Makefile; they sit under build options such as AUTO_TEST, so none is settled here.
        private static HashSet<string> MakeFlags(string root)
        {
            HashSet<string> flags = new HashSet<string>(StringComparer.Ordinal);
            string makefile = Path.Combine(root, "Makefile");
            if (!File.Exists(makefile)) return flags;
            foreach (string line in HgEngineMakeFile.For(HgEngineFileCache.GetText(makefile)).Assignments.Where(a => a.Name.Contains("CFLAGS", StringComparison.Ordinal)).Select(a => a.Value))
                for (int at = line.IndexOf("-D", StringComparison.Ordinal); at >= 0; at = line.IndexOf("-D", at + 2, StringComparison.Ordinal))
                {
                    int end = at + 2;
                    while (end < line.Length && HgEngineMakeFile.IsNameChar(line[end])) end++;
                    if (end > at + 2) flags.Add(line.Substring(at + 2, end - at - 2));
                }
            return flags;
        }

        internal static HgEngineHookEntry Parse(HgEngineHookKind kind, string raw)
        {
            HgEngineHookEntry entry = new HgEngineHookEntry { Kind = kind, RawLine = raw };

            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') return entry;

            string[] f = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (f.Length < 2) return entry;
            if (!HgEngineClaimedRanges.TryBinary(f[0], out int overlay)) return entry;

            entry.OverlayNumber = overlay;

            if (kind == HgEngineHookKind.ByteReplacement)
            {
                if (f.Length < 3 || !TryHex(f[1], out long at)) return entry;

                List<byte> bytes = new List<byte>();
                foreach (string token in f.Skip(2))
                {
                    if (!byte.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                        return entry;   // a define, not literal bytes; leave the line alone
                    bytes.Add(b);
                }
                if (bytes.Count == 0) return entry;

                entry.Address = at;
                entry.Bytes = bytes;
                entry.Parsed = true;
                return entry;
            }

            if (f.Length < 3 || !TryHex(f[2], out long address)) return entry;

            // make.py splits hooks into exactly 3 or 4 columns and armhooks into exactly 4, reading the
            // fourth with int(). Anything else is kept as text, so it can't become a different line.
            int register = -1;
            if (kind == HgEngineHookKind.Repoint) { if (f.Length != 3) return entry; }
            else if (f.Length > 4) return entry;
            else if (f.Length == 4)
            {
                if (!int.TryParse(f[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out register) || register < 0) return entry;
            }
            else if (kind == HgEngineHookKind.ArmHook) return entry;

            entry.Symbol = f[1];
            entry.Address = address;
            entry.Register = register;
            entry.Parsed = true;
            return entry;
        }

        private static bool TryHex(string token, out long value) =>
            long.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);

        /// <summary>
        /// Reads the register typed for a new hook. make.py branches a hook through r0 to r7, treats 255 as
        /// replacing the whole routine, and loads an ARM hook's address through r0 to r12.
        /// </summary>
        public static bool TryParseRegister(HgEngineHookKind kind, string text, out int register, out string error)
        {
            register = -1;
            error = null;
            if (kind is not (HgEngineHookKind.Hook or HgEngineHookKind.ArmHook)) return true;

            string trimmed = (text ?? "").Trim();
            bool isNumber = int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value);
            bool valid = kind == HgEngineHookKind.Hook
                ? isNumber && ((value >= 0 && value <= 7) || value == 0xFF)
                : isNumber && value >= 0 && value <= 12;
            if (!valid)
            {
                error = kind == HgEngineHookKind.Hook
                    ? "The register has to be 0 to 7, or 255 to replace the whole routine."
                    : "The register has to be 0 to 12.";
                return false;
            }
            register = value;
            return true;
        }

        /// <summary>Why this entry can't be written as a line make.py reads correctly, or null.</summary>
        public static string Problem(HgEngineHookEntry entry)
        {
            if (!entry.Parsed) return null;
            string where = $"{entry.BinaryName} 0x{entry.Address:X8}";
            switch (entry.Kind)
            {
                case HgEngineHookKind.Hook when entry.Register < -1 || entry.Register > 0xFF:
                    return $"The hook at {where} has register {entry.Register}; it has to be 0 to 7, or 255.";
                case HgEngineHookKind.ArmHook when entry.Register < 0 || entry.Register > 15:
                    return $"The ARM hook at {where} has no usable register.";
                case HgEngineHookKind.Hook or HgEngineHookKind.ArmHook or HgEngineHookKind.Repoint
                    when string.IsNullOrWhiteSpace(entry.Symbol) || entry.Symbol.Any(char.IsWhiteSpace):
                    return $"The entry at {where} needs a routine or table name with no spaces.";
                case HgEngineHookKind.ByteReplacement when entry.Bytes.Count == 0:
                    return $"The replacement at {where} has no bytes.";
                default:
                    return null;
            }
        }

        /// <summary>Adds an entry to the end, and returns it.</summary>
        public HgEngineHookEntry Add(int overlayNumber, string symbol, long address, int register,
            IReadOnlyList<byte> bytes)
        {
            HgEngineHookEntry entry = new HgEngineHookEntry
            {
                Kind = Kind,
                Parsed = true,
                OverlayNumber = overlayNumber,
                Symbol = symbol ?? "",
                Address = address,
                Register = register,
                Bytes = bytes ?? Array.Empty<byte>(),
            };
            entry.RawLine = entry.Render();
            Entries.Add(entry);
            return entry;
        }

        /// <summary>Rewrites an entry's fields and its line, keeping the line's indentation.</summary>
        public static void Change(HgEngineHookEntry entry, int overlayNumber, string symbol, long address, int register,
            IReadOnlyList<byte> bytes)
        {
            string raw = entry.RawLine ?? "";
            string indent = raw.Substring(0, raw.Length - raw.TrimStart().Length);
            entry.Parsed = true;
            entry.OverlayNumber = overlayNumber;
            entry.Symbol = symbol ?? "";
            entry.Address = address;
            entry.Register = register;
            entry.Bytes = bytes ?? Array.Empty<byte>();
            entry.RawLine = indent + entry.Render();
        }

        /// <summary>Sets a replacement's bytes and rewrites only their columns, so the line keeps its spacing.</summary>
        public static void ChangeBytes(HgEngineHookEntry entry, IReadOnlyList<byte> bytes)
        {
            string raw = entry.RawLine ?? "";
            List<(int Start, int End)> fields = new List<(int, int)>();
            for (int i = 0; i < raw.Length;)
            {
                while (i < raw.Length && char.IsWhiteSpace(raw[i])) i++;
                int start = i;
                while (i < raw.Length && !char.IsWhiteSpace(raw[i])) i++;
                if (i > start) fields.Add((start, i));
            }
            entry.Bytes = bytes;
            if (!entry.Parsed || entry.Kind != HgEngineHookKind.ByteReplacement || fields.Count - 2 != bytes.Count)
            {
                Change(entry, entry.OverlayNumber, entry.Symbol, entry.Address, entry.Register, bytes);
                return;
            }
            for (int b = bytes.Count - 1; b >= 0; b--)
                raw = raw.Substring(0, fields[b + 2].Start) + bytes[b].ToString("X2") + raw.Substring(fields[b + 2].End);
            entry.RawLine = raw;
        }

        /// <summary>Raised after a list was written, for editors that read the lists.</summary>
        public static event Action Saved;

        /// <summary>Why this list cannot be written, or null.</summary>
        public string FirstProblem => Entries.Select(Problem).FirstOrDefault(p => p != null);

        public bool Save(out string error)
        {
            error = FirstProblem;
            if (error != null) return false;
            try
            {
                // Written back line for line as they came; added and changed entries carry their new line.
                IEnumerable<string> lines = Entries.Select(e => e.RawLine);
                HgEngineFileCache.WriteText(FullPath, string.Join("\n", lines) + (_endsWithNewline ? "\n" : ""));
                HgEngineClaimedRanges.ClearCache();
                Saved?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                error = $"{FileName} couldn't be written: {ex.Message}";
                return false;
            }
        }
    }
}

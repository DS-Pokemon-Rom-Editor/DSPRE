using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>Which of hg-engine's patch lists an entry belongs to, and therefore its shape.</summary>
    public enum HgEnginePatchKind
    {
        /// <summary>hooks: binary, symbol, address, and the register the branch goes through.</summary>
        Hook,

        /// <summary>armhooks: the same, assembled as ARM rather than THUMB.</summary>
        ArmHook,

        /// <summary>bytereplacement: binary, address, and the bytes to write there.</summary>
        ByteReplacement,

        /// <summary>repoints: binary, symbol, address, writing a pointer.</summary>
        Repoint,
    }

    /// <summary>
    /// One line of a patch list. A line DSPRE does not understand keeps its text and is written back
    /// exactly as it came, so comments, includes and conditionals survive editing.
    /// </summary>
    public sealed class HgEnginePatchEntry
    {
        public HgEnginePatchKind Kind { get; set; }

        /// <summary>The raw text, which is what an unparsed line is written back as.</summary>
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

        public string BinaryName => OverlayNumber < 0 ? "arm9" : $"overlay {OverlayNumber}";

        /// <summary>No register, or the 255 that stands for one, replaces the routine outright.</summary>
        public bool ReplacesWholeRoutine => Register < 0 || Register == 0xFF;

        /// <summary>What this entry does, in the words the list itself uses.</summary>
        public string Describes => Kind switch
        {
            HgEnginePatchKind.Hook when ReplacesWholeRoutine => $"replaces the routine at 0x{Address:X8} with {Symbol}",
            HgEnginePatchKind.Hook => $"branches to {Symbol} through r{Register}",
            HgEnginePatchKind.ArmHook => $"branches to {Symbol} through r{Register}, in ARM",
            HgEnginePatchKind.Repoint => $"points at {Symbol}",
            _ => $"writes {Bytes.Count} byte(s)",
        };

        internal string Render() => Kind switch
        {
            HgEnginePatchKind.ByteReplacement =>
                $"{BinaryField} {Address:X8} {string.Join(" ", Bytes.Select(b => b.ToString("X2")))}",
            HgEnginePatchKind.Repoint => $"{BinaryField} {Symbol} {Address:X8}",
            HgEnginePatchKind.Hook when Register < 0 => $"{BinaryField} {Symbol} {Address:X8}",
            _ => $"{BinaryField} {Symbol} {Address:X8} {Register}",
        };

        private string BinaryField => OverlayNumber < 0 ? "arm9" : OverlayNumber.ToString("D4");

        /// <summary>How many bytes the build writes for this entry, which is what it claims.</summary>
        public int Length => Kind switch
        {
            HgEnginePatchKind.ByteReplacement => Bytes.Count,
            HgEnginePatchKind.Repoint => 4,
            HgEnginePatchKind.ArmHook => 8 + 4,
            // The branch goes in at an even address and takes an extra halfword when that lands off a
            // word boundary. Replacing the whole routine needs 0x18 of code plus its address, which is
            // what a missing register column means, and what 255 means when one is written out.
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
    /// One of the checkout's patch lists, read so it can be shown and added to. Only lines matching the
    /// list's own shape are parsed; everything else is carried through untouched, so writing a file back
    /// changes nothing but the entries that were edited.
    /// </summary>
    public sealed class HgEnginePatchList
    {
        public HgEnginePatchKind Kind { get; }
        public string FileName { get; }
        public string FullPath { get; }
        public List<HgEnginePatchEntry> Entries { get; } = new();

        private HgEnginePatchList(HgEnginePatchKind kind, string fileName, string fullPath)
        {
            Kind = kind;
            FileName = fileName;
            FullPath = fullPath;
        }

        public static readonly (HgEnginePatchKind Kind, string FileName)[] Known =
        {
            (HgEnginePatchKind.Hook, "hooks"),
            (HgEnginePatchKind.ArmHook, "armhooks"),
            (HgEnginePatchKind.ByteReplacement, "bytereplacement"),
            (HgEnginePatchKind.Repoint, "repoints"),
        };

        /// <summary>Every list the linked checkout has.</summary>
        public static List<HgEnginePatchList> ReadAll()
            => ReadAllAt(HgEngineProject.IsActive ? HgEngineProject.RepoRootWindows : null);

        internal static List<HgEnginePatchList> ReadAllAt(string root)
        {
            var lists = new List<HgEnginePatchList>();
            if (root == null) return lists;

            foreach ((HgEnginePatchKind kind, string name) in Known)
            {
                string path = Path.Combine(root, name);
                if (File.Exists(path)) lists.Add(Read(kind, name, path));
            }
            return lists;
        }

        internal static HgEnginePatchList Read(HgEnginePatchKind kind, string fileName, string path)
        {
            var list = new HgEnginePatchList(kind, fileName, path);
            try
            {
                foreach (string raw in File.ReadAllLines(path))
                    list.Entries.Add(Parse(kind, raw));
            }
            catch (Exception ex) { AppLogger.Error($"HgEnginePatchList.Read({fileName}): " + ex.Message); }
            return list;
        }

        internal static HgEnginePatchEntry Parse(HgEnginePatchKind kind, string raw)
        {
            var entry = new HgEnginePatchEntry { Kind = kind, RawLine = raw };

            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') return entry;

            string[] f = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (f.Length < 2) return entry;
            if (!HgEngineClaimedRanges.TryBinary(f[0], out int overlay)) return entry;

            entry.OverlayNumber = overlay;

            if (kind == HgEnginePatchKind.ByteReplacement)
            {
                if (f.Length < 3 || !TryHex(f[1], out long at)) return entry;

                var bytes = new List<byte>();
                foreach (string token in f.Skip(2))
                {
                    if (!byte.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                        return entry;   // a define rather than literal bytes; leave the line alone
                    bytes.Add(b);
                }
                if (bytes.Count == 0) return entry;

                entry.Address = at;
                entry.Bytes = bytes;
                entry.Parsed = true;
                return entry;
            }

            if (f.Length < 3 || !TryHex(f[2], out long address)) return entry;
            entry.Symbol = f[1];
            entry.Address = address;
            entry.Register = f.Length > 3
                && int.TryParse(f[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int reg) ? reg : -1;
            entry.Parsed = true;
            return entry;
        }

        private static bool TryHex(string token, out long value) =>
            long.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);

        /// <summary>Adds an entry to the end, and returns it.</summary>
        public HgEnginePatchEntry Add(int overlayNumber, string symbol, long address, int register,
            IReadOnlyList<byte> bytes)
        {
            var entry = new HgEnginePatchEntry
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

        public bool Save(out string error)
        {
            error = null;
            try
            {
                // Written back line for line: an entry that was never parsed keeps its own text, and one
                // that was edited is rendered fresh.
                var lines = Entries.Select(e => e.Parsed ? e.Render() : e.RawLine);
                File.WriteAllText(FullPath, string.Join("\n", lines) + "\n");
                HgEngineClaimedRanges.ClearCache();
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

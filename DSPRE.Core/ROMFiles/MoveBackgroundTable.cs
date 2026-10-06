using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The move-effect backgrounds (Surf, Fly, Psychic and the rest): per background id, the battle background archive
    /// members of its drawing, palette, screen, reversed screen (the effect seen from the other side) and contest screen.
    /// The game keeps the table in its battle animation overlay (<see cref="RomInfo.MoveBackgroundTableSite"/>), found
    /// from the routine that reads it; hg-engine replaces that routine and reads data/BackgroundGfx.c instead.
    /// </summary>
    public sealed class MoveBackgroundTable
    {
        public const int Columns = 5;
        public static readonly string[] ColumnNames = { "Drawing", "Palette", "Screen", "Reversed screen", "Contest screen" };

        public List<int[]> Rows { get; } = new();
        /// <summary>hg-engine: rows come from data/BackgroundGfx.c and can be added or removed.</summary>
        public bool FromSource { get; private set; }

        private int _overlay = -1, _offset;

        // movs r2,#0x14; muls r2,r0; ldr r0,=table; lsls r1,r1,#2; adds r0,r0,r2; ldr r0,[r1,r0]; bx lr
        private static int FindGetter(byte[] code)
        {
            int found = -1;
            for (int i = 0; i + 14 <= code.Length; i += 2)
            {
                if (code[i] != 0x14 || code[i + 1] != 0x22 || code[i + 2] != 0x42 || code[i + 3] != 0x43 || code[i + 5] != 0x48) continue;
                if (code[i + 6] != 0x89 || code[i + 7] != 0x00 || code[i + 8] != 0x80 || code[i + 9] != 0x18
                    || code[i + 10] != 0x08 || code[i + 11] != 0x58 || code[i + 12] != 0x70 || code[i + 13] != 0x47) continue;
                if (found >= 0) return -2;
                found = i;
            }
            return found;
        }

        private static MoveBackgroundTable _cache;
        private static (string Project, bool Source) _cacheFor;

        /// <summary>The loaded ROM's table, read once per project until <see cref="Invalidate"/>.</summary>
        public static MoveBackgroundTable Current
        {
            get
            {
                (string workDir, bool IsActive) key = (workDir, HgEngine.HgEngineProject.IsActive);
                if (_cache == null || _cacheFor != key)
                {
                    // An unreadable table is empty rather than read again on every use.
                    if (!TryLoad(out _cache, out _)) _cache = new MoveBackgroundTable();
                    _cacheFor = key;
                }
                return _cache;
            }
        }

        public static void Invalidate() => _cache = null;

        public static bool TryLoad(out MoveBackgroundTable table, out string error)
        {
            table = null;
            if (HgEngine.HgEngineProject.IsActive)
            {
                if (!HgEngine.HgEngineMoveBackgrounds.TryRead(out List<int[]> rows, out error)) return false;
                table = new MoveBackgroundTable { FromSource = true };
                table.Rows.AddRange(rows);
                return true;
            }

            (int overlay, int count) = MoveBackgroundTableSite;
            if (overlay < 0) { error = "This game has no move background table DSPRE knows."; return false; }
            try
            {
                if (OverlayUtils.IsCompressed(overlay)) OverlayUtils.Decompress(overlay);
                byte[] code = File.ReadAllBytes(OverlayUtils.GetPath(overlay));
                int at = FindGetter(code);
                if (at < 0) { error = at == -2 ? "The move background routine appears twice in its overlay." : "The move background routine isn't where the game keeps it; the overlay may have been patched."; return false; }
                int literal = ((at + 8) & ~3) + code[at + 4] * 4;
                uint ram = BitConverter.ToUInt32(code, literal), start = OverlayUtils.OverlayTable.GetRAMAddress(overlay);
                long offset = (long)ram - start;
                if (offset < 0 || offset + count * Columns * 4 > code.Length) { error = $"The move background table at 0x{ram:X8} is outside its overlay."; return false; }

                table = new MoveBackgroundTable { _overlay = overlay, _offset = (int)offset };
                for (int r = 0; r < count; r++)
                    table.Rows.Add(Enumerable.Range(0, Columns).Select(c => BitConverter.ToInt32(code, (int)offset + (r * Columns + c) * 4)).ToArray());
                error = null;
                return true;
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException)
            {
                error = e.Message;
                return false;
            }
        }

        /// <summary>Why the rows can't be saved, or null.</summary>
        public string WhyNot(int archiveMembers)
        {
            if (Rows.Count == 0) return "The table needs at least one background.";
            if (!FromSource && Rows.Count != MoveBackgroundTableSite.Rows) return $"The game's table holds exactly {MoveBackgroundTableSite.Rows} backgrounds.";
            for (int r = 0; r < Rows.Count; r++)
                for (int c = 0; c < Columns; c++)
                    if (Rows[r][c] < 0 || (archiveMembers > 0 && Rows[r][c] >= archiveMembers))
                        return $"Background {r}: {ColumnNames[c].ToLowerInvariant()} {Rows[r][c]} isn't a file of the battle background archive.";
            return null;
        }

        /// <summary>Writes the rows where they were read from; throws with the reason if they can't be.</summary>
        public void Save(int archiveMembers)
        {
            if (WhyNot(archiveMembers) is string why) throw new InvalidOperationException(why);
            if (FromSource)
            {
                if (!HgEngine.HgEngineMoveBackgrounds.TryWrite(Rows, out string error)) throw new InvalidOperationException(error);
            }
            else
            {
                string path = OverlayUtils.GetPath(_overlay);
                byte[] code = File.ReadAllBytes(path);
                for (int r = 0; r < Rows.Count; r++)
                    for (int c = 0; c < Columns; c++)
                        BitConverter.GetBytes(Rows[r][c]).CopyTo(code, _offset + (r * Columns + c) * 4);
                File.WriteAllBytes(path, code);
            }
            Invalidate();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using DSPRE.LibNDSFormats;
using static DSPRE.RomInfo;

namespace DSPRE
{
    /// <summary>
    /// Detects and reads/writes hzla's PlatPatches "overworld sprites" expansion patch
    /// (github.com/hzla/PlatPatches, <c>src/patches/overworld-sprites.js</c>). Platinum-only.
    /// DSPRE never applies this patch itself — it only detects an already-patched ROM and, once
    /// detected, lets the BTX/Overworld Editor add/delete custom entries within the capacity the
    /// patch pre-reserved.
    ///
    /// Layout inside <see cref="Filesystem.expArmPath"/> (the same "synthetic overlay" NARC
    /// member DSPRE's own patches already use) once patched:
    ///   0x00: 12-byte ASCII marker "OWTBLXPANDV1"
    ///   0x10: u32 version (1)
    ///   0x14: u32 capacity (256)
    ///   0x18: u32 usedCustomCount
    ///   0x1C: u32 reserved (0)
    ///   0x20: table 0 (renderer behaviour, 8B/row), table 1 (render properties, 8B/row),
    ///         table 2 (texture association, 8B/row — this is RomInfo.OverworldTable's source),
    ///         table 3 (animation metadata, 16B/row), each concatenated back-to-back. Every
    ///         table's slot is reserved at size (originalRowCount + capacity + 1) * entrySize so
    ///         re-patching with more custom entries never has to move a later table. Every row's
    ///         first 4 bytes are its OBJ/appearance-code key; a row of 0xFFFF in that field is
    ///         the table's terminator.
    /// </summary>
    public static class OverworldSpriteTableExpansion
    {
        private const string Marker = "OWTBLXPANDV1";
        private const int HeaderSize = 0x20;
        private static readonly int[] EntrySizes = { 8, 8, 8, 16 }; // renderer, renderProps, texture, animation
        private const int TextureTableIndex = 2;
        private const int RenderPropsTableIndex = 1;

        /// <summary>Byte distance from the vanilla render-properties table to the vanilla texture
        /// table in an *unpatched* ROM (259 render-properties rows) — derived from source-confirmed
        /// struct sizes (fieldobj_drawdata.c), not guessed. Verify against a real ROM.</summary>
        private const long VanillaRenderPropsToTextureDelta = 259 * 8;
        private const int VanillaRenderPropsRowCount = 259;

        public struct OwRenderState
        {
            public int DrawType;     // FLDOBJ_DRAWTYPE: 0=None 1=Billboard 2=3D model
            public int ShadowType;   // FLDOBJ_SHADOWTYPE: 0=None 1=On
            public int FootmarkType; // FLDOBJ_FOOTMARKTYPE: 0=None 1=Normal(2-leg) 2=Cycle(bike)
            public int ReflectType;  // FLDOBJ_REFLECTTYPE: 0=None 1=On(billboard reflection)
        }

        private struct TableLayout
        {
            public long Start;            // file offset of row 0
            public int EntrySize;
            public int OriginalRowCount;  // rows before the first custom row
        }

        private struct SimpleTable
        {
            public string Path;
            public long Start;
            public int EntrySize;
            public int RowCount;
        }

        private static bool _detected;
        private static string _path;
        private static long _markerOffset = -1;
        private static long _reservedEnd = -1;
        private static uint _capacity;
        private static uint _usedCount;
        private static TableLayout[] _tables;

        public static bool IsApplied => _detected;
        public static uint UsedCount => _usedCount;
        public static uint Capacity => _capacity;

        public static (long Start, long End)? GetReservedByteRange() =>
            _detected ? ((long Start, long End)?)(_markerOffset, _reservedEnd) : null;

        /// <summary>Re-scans the synthetic overlay for the expansion marker. Safe to call anytime
        /// after a ROM is loaded (no-op, returns false, for DP/HGSS).</summary>
        public static bool Detect()
        {
            _detected = false;
            _markerOffset = -1;
            _reservedEnd = -1;
            _tables = null;

            if (RomInfo.gameFamily != GameFamilies.Plat)
                return false;

            try
            {
                DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.synthOverlay });
                _path = Filesystem.expArmPath;
                if (!File.Exists(_path)) return false;

                byte[] data = File.ReadAllBytes(_path);
                long markerOffset = FindBytes(data, Encoding.ASCII.GetBytes(Marker));
                if (markerOffset < 0) return false;

                _markerOffset = markerOffset;
                _capacity = ReadU32(data, _markerOffset + 0x14);
                _usedCount = ReadU32(data, _markerOffset + 0x18);

                long cursor = _markerOffset + HeaderSize;
                var tables = new TableLayout[4];
                for (int i = 0; i < 4; i++)
                {
                    int entrySize = EntrySizes[i];
                    int sentinelIndex = ScanForSentinelRow(data, cursor, entrySize);
                    if (sentinelIndex < 0) return false;
                    int originalRows = sentinelIndex - (int)_usedCount;
                    if (originalRows < 0) return false;
                    long slotBytes = (long)(originalRows + (int)_capacity + 1) * entrySize;
                    tables[i] = new TableLayout { Start = cursor, EntrySize = entrySize, OriginalRowCount = originalRows };
                    cursor += slotBytes;
                }

                _tables = tables;
                _reservedEnd = cursor;
                _detected = true;
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Error("Overworld sprite expansion detection failed: " + ex.Message);
                _detected = false;
                return false;
            }
        }

        /// <summary>Texture-association table (appearanceId -&gt; mmodel NARC member), read from
        /// the expanded location. Format matches RomInfo.OverworldTable's tuple convention
        /// (properties dummy 0). Only valid after a successful <see cref="Detect"/>.</summary>
        public static SortedDictionary<uint, (uint spriteID, ushort properties)> ReadTextureTable()
        {
            var result = new SortedDictionary<uint, (uint spriteID, ushort properties)>();
            if (!_detected) return result;

            byte[] data = File.ReadAllBytes(_path);
            TableLayout layout = _tables[TextureTableIndex];
            int rowCount = layout.OriginalRowCount + (int)_usedCount;
            for (int r = 0; r < rowCount; r++)
            {
                long off = layout.Start + (long)r * layout.EntrySize;
                result[ReadU32(data, off)] = (ReadU32(data, off + 4), 0);
            }
            return result;
        }

        /// <summary>True if <paramref name="appearanceId"/> is one of the patch's custom rows
        /// (as opposed to an original vanilla entry). Only meaningful when <see cref="IsApplied"/>.</summary>
        public static bool IsCustomEntry(uint appearanceId)
        {
            if (!_detected) return false;
            byte[] data = File.ReadAllBytes(_path);
            int idx = FindRowIndex(data, _tables[TextureTableIndex], appearanceId);
            return idx >= _tables[TextureTableIndex].OriginalRowCount;
        }

        /// <summary>Raw bytes of an entry's row in the renderer-behaviour (index 0) or
        /// animation-metadata (index 3) table — opaque, read-only, for informational display only.
        /// Only available once the expansion patch is detected. Returns null if not found.</summary>
        public static byte[] ReadRawRow(int tableIndex, uint appearanceId)
        {
            if (!_detected || tableIndex < 0 || tableIndex > 3) return null;
            byte[] data = File.ReadAllBytes(_path);
            TableLayout layout = _tables[tableIndex];
            int idx = FindRowIndex(data, layout, appearanceId);
            if (idx < 0) return null;
            var row = new byte[layout.EntrySize];
            Array.Copy(data, layout.Start + (long)idx * layout.EntrySize, row, 0, layout.EntrySize);
            return row;
        }

        /// <summary>Reads a texture file's dimensions/color budget via BTX0.Read, without caring
        /// whether it's an OW slot at all — used to validate/label candidate texture slots (mmodel.narc
        /// mixes flat textures and 3D models; only the former decode here).</summary>
        public static bool TryReadTextureInfo(string path, out int width, out int height, out uint colorLimit)
        {
            width = height = 0; colorLimit = 0;
            try
            {
                using (Bitmap bmp = BTX0.Read(File.ReadAllBytes(path)))
                {
                    if (bmp == null) return false;
                    width = bmp.Width; height = bmp.Height; colorLimit = BTX0.ColorCount;
                    return true;
                }
            }
            catch { return false; }
        }

        /// <summary>Finds an mmodel NARC member number nobody's using yet, so a newly imported
        /// texture can get its own genuinely new file instead of overwriting one that an existing
        /// overworld entry (or another custom one) already points at. <c>Narc.FromFolder</c> packs
        /// every numbered file present in the unpacked directory as a member at save time (see
        /// Narc.cs), so simply writing a fresh highest-numbered file here is enough to grow the
        /// NARC by one slot; nothing needs to be pre-reserved the way the table capacity is.</summary>
        public static uint AllocateNewMmodelSlot()
        {
            string dir = RomInfo.gameDirs[DirNames.OWSprites].unpackedDir;
            long max = -1;
            if (Directory.Exists(dir))
            {
                foreach (string f in Directory.GetFiles(dir))
                {
                    uint id;
                    if (uint.TryParse(Path.GetFileName(f), out id) && id > max)
                        max = id;
                }
            }
            return (uint)(max + 1);
        }

        // ── Render-state (table 1) read/write — available on every Platinum ROM, patched or not ──

        public static bool TryReadRenderState(uint appearanceId, out OwRenderState state)
        {
            state = default(OwRenderState);
            if (RomInfo.gameFamily != GameFamilies.Plat) return false;

            SimpleTable t = GetRenderStateTable();
            if (t.Path == null || !File.Exists(t.Path)) return false;

            byte[] data = File.ReadAllBytes(t.Path);
            for (int r = 0; r < t.RowCount; r++)
            {
                long off = t.Start + (long)r * t.EntrySize;
                if (off + t.EntrySize > data.Length) break;
                if (ReadU32(data, off) != appearanceId) continue;
                state = UnpackRenderState(ReadU32(data, off + 4));
                return true;
            }
            return false;
        }

        public static bool TryWriteRenderState(uint appearanceId, OwRenderState state, out string error)
        {
            error = null;
            if (RomInfo.gameFamily != GameFamilies.Plat)
            {
                error = "Overworld render-state editing is only implemented for Platinum.";
                return false;
            }

            SimpleTable t = GetRenderStateTable();
            if (t.Path == null || !File.Exists(t.Path))
            {
                error = "The render-state table could not be located.";
                return false;
            }

            byte[] data = File.ReadAllBytes(t.Path);
            for (int r = 0; r < t.RowCount; r++)
            {
                long off = t.Start + (long)r * t.EntrySize;
                if (off + t.EntrySize > data.Length) break;
                if (ReadU32(data, off) != appearanceId) continue;

                DSUtils.WriteToFile(t.Path, BitConverter.GetBytes(PackRenderState(state)), (uint)(off + 4));
                return true;
            }

            error = string.Format("Appearance ID 0x{0:X} was not found in the render-state table.", appearanceId);
            return false;
        }

        private static SimpleTable GetRenderStateTable()
        {
            if (_detected)
            {
                TableLayout t = _tables[RenderPropsTableIndex];
                return new SimpleTable { Path = _path, Start = t.Start, EntrySize = t.EntrySize, RowCount = t.OriginalRowCount + (int)_usedCount };
            }

            return new SimpleTable
            {
                Path = RomInfo.OWtablePath,
                Start = RomInfo.OWTableOffset - VanillaRenderPropsToTextureDelta,
                EntrySize = 8,
                RowCount = VanillaRenderPropsRowCount,
            };
        }

        private static OwRenderState UnpackRenderState(uint bits)
        {
            return new OwRenderState
            {
                DrawType = (int)(bits & 0xF),
                ShadowType = (int)((bits >> 4) & 0x3),
                FootmarkType = (int)((bits >> 6) & 0xF),
                ReflectType = (int)((bits >> 10) & 0x3),
            };
        }

        private static uint PackRenderState(OwRenderState s)
        {
            return ((uint)s.DrawType & 0xF)
                | (((uint)s.ShadowType & 0x3) << 4)
                | (((uint)s.FootmarkType & 0xF) << 6)
                | (((uint)s.ReflectType & 0x3) << 10);
        }

        // ── Add / Delete custom entries (only once the expansion patch is detected) ─────────────

        public static bool AddEntry(uint appearanceId, uint mmodelMember, uint cloneFrom, out string error)
        {
            error = null;
            if (!_detected)
            {
                error = "The overworld sprite expansion patch is not applied to this ROM.";
                return false;
            }
            if (_usedCount >= _capacity)
            {
                error = string.Format("No free custom slots left ({0}/{1} used).", _usedCount, _capacity);
                return false;
            }
            if (!IsAllowedAppearanceId(appearanceId, out error)) return false;
            string cloneErr;
            if (!IsAllowedAppearanceId(cloneFrom, out cloneErr))
            {
                error = "Clone-source ID: " + cloneErr;
                return false;
            }

            byte[] data = File.ReadAllBytes(_path);

            for (int t = 0; t < 4; t++)
            {
                if (FindRowIndex(data, _tables[t], appearanceId) >= 0)
                {
                    error = string.Format("Appearance ID 0x{0:X} already exists.", appearanceId);
                    return false;
                }
            }

            // Tables 0/1/3 need an existing row to clone; table 2 (texture) always gets a fresh row.
            var cloneRowIndex = new int[4];
            foreach (int t in new[] { 0, 1, 3 })
            {
                int idx = FindRowIndex(data, _tables[t], cloneFrom);
                if (idx < 0)
                {
                    error = string.Format("Clone source 0x{0:X} was not found (table {1}).", cloneFrom, t);
                    return false;
                }
                cloneRowIndex[t] = idx;
            }

            for (int t = 0; t < 4; t++)
            {
                TableLayout layout = _tables[t];
                int insertRowIndex = layout.OriginalRowCount + (int)_usedCount; // where the sentinel currently sits
                long insertOffset = layout.Start + (long)insertRowIndex * layout.EntrySize;

                byte[] row;
                if (t == TextureTableIndex)
                {
                    row = new byte[8];
                    WriteU32(row, 0, appearanceId);
                    WriteU32(row, 4, mmodelMember);
                }
                else
                {
                    long cloneOffset = layout.Start + (long)cloneRowIndex[t] * layout.EntrySize;
                    row = DSUtils.ReadFromFile(_path, cloneOffset, layout.EntrySize);
                    WriteU32(row, 0, appearanceId);
                }

                var sentinelRow = new byte[layout.EntrySize];
                WriteU32(sentinelRow, 0, 0xFFFF);

                DSUtils.WriteToFile(_path, row, (uint)insertOffset);
                DSUtils.WriteToFile(_path, sentinelRow, (uint)(insertOffset + layout.EntrySize));
            }

            WriteHeaderUsedCount(_usedCount + 1);
            Detect();
            return true;
        }

        public static bool DeleteEntry(uint appearanceId, out string error)
        {
            error = null;
            if (!_detected)
            {
                error = "The overworld sprite expansion patch is not applied to this ROM.";
                return false;
            }

            byte[] data = File.ReadAllBytes(_path);
            int texRowIndex = FindRowIndex(data, _tables[TextureTableIndex], appearanceId);
            if (texRowIndex < 0)
            {
                error = string.Format("Appearance ID 0x{0:X} was not found.", appearanceId);
                return false;
            }
            if (texRowIndex < _tables[TextureTableIndex].OriginalRowCount)
            {
                error = "Cannot delete an original (non-custom) overworld entry.";
                return false;
            }

            for (int t = 0; t < 4; t++)
            {
                TableLayout layout = _tables[t];
                int rowIndex = FindRowIndex(data, layout, appearanceId);
                if (rowIndex < 0) continue; // tables should be consistent, but don't hard-fail if not

                int lastCustomRowIndex = layout.OriginalRowCount + (int)_usedCount - 1;
                // Shift every row after the deleted one (through the sentinel) back by one slot.
                for (int r = rowIndex; r <= lastCustomRowIndex + 1; r++)
                {
                    byte[] nextRow = DSUtils.ReadFromFile(_path, layout.Start + (long)(r + 1) * layout.EntrySize, layout.EntrySize);
                    DSUtils.WriteToFile(_path, nextRow, (uint)(layout.Start + (long)r * layout.EntrySize));
                }
            }

            WriteHeaderUsedCount(_usedCount - 1);
            Detect();
            return true;
        }

        public static bool IsAllowedAppearanceId(uint id, out string error)
        {
            error = null;
            if (id > 0xFFFF) { error = string.Format("0x{0:X} is outside the 16-bit appearance-ID range.", id); return false; }
            if (id == 0xFFFF) { error = "0xFFFF is the reserved table-terminator ID."; return false; }
            if (id == 0x64 || (id >= 0x65 && id <= 0x74)) { error = string.Format("0x{0:X} is reserved for field-graphics.", id); return false; }
            if (id >= 0x1000 && id <= 0x10C0) { error = string.Format("0x{0:X} is reserved for berry-growth resources.", id); return false; }
            if (id == 0x2000) { error = "0x2000 is reserved."; return false; }
            return true;
        }

        /// <summary>Finds a free appearance ID nobody's using yet, starting right after the
        /// highest one already in the table and scanning upward past it for the first one that
        /// isn't reserved or already taken. Returns null if the expansion patch isn't detected or
        /// the table is empty.</summary>
        public static uint? SuggestNewAppearanceId()
        {
            if (!_detected) return null;
            var used = new HashSet<uint>(ReadTextureTable().Keys);
            if (used.Count == 0) return null;

            uint start = used.Max() + 1;
            for (uint candidate = start; candidate < 0xFFFF; candidate++)
            {
                if (used.Contains(candidate)) continue;
                string ignored;
                if (!IsAllowedAppearanceId(candidate, out ignored)) continue;
                return candidate;
            }
            return null;
        }

        private static void WriteHeaderUsedCount(uint value)
        {
            DSUtils.WriteToFile(_path, BitConverter.GetBytes(value), (uint)(_markerOffset + 0x18));
        }

        private static int FindRowIndex(byte[] data, TableLayout layout, uint key)
        {
            int rowCount = layout.OriginalRowCount + (int)_usedCount + 1; // + sentinel
            for (int r = 0; r < rowCount; r++)
            {
                long off = layout.Start + (long)r * layout.EntrySize;
                if (off + 4 > data.Length) break;
                uint k = ReadU32(data, off);
                if (k == key) return r;
                if (k == 0xFFFF) break;
            }
            return -1;
        }

        private static int ScanForSentinelRow(byte[] data, long start, int entrySize)
        {
            for (int r = 0; r < 4096; r++)
            {
                long off = start + (long)r * entrySize;
                if (off + 4 > data.Length) return -1;
                if (ReadU32(data, off) == 0xFFFF) return r;
            }
            return -1;
        }

        private static long FindBytes(byte[] haystack, byte[] needle)
        {
            if (haystack == null || needle == null || needle.Length == 0 || needle.Length > haystack.Length) return -1;
            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                bool isMatch = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j]) { isMatch = false; break; }
                }
                if (isMatch) return i;
            }
            return -1;
        }

        private static uint ReadU32(byte[] data, long offset)
        {
            return BitConverter.ToUInt32(data, (int)offset);
        }

        private static void WriteU32(byte[] data, int offset, uint value)
        {
            Array.Copy(BitConverter.GetBytes(value), 0, data, offset, 4);
        }
    }
}

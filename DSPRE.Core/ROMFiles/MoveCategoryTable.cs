using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// A move list the battle code keeps as data and walks by count: the punching moves Iron Fist boosts and the
    /// sound moves Soundproof blocks. Each is a u16 table in the battle overlay, reached through one pointer literal
    /// and bounded by a <c>cmp rN, #count</c>, so a list moved into the expanded ARM9 area is followed through the
    /// pointer and grows by rewriting the count.
    /// </summary>
    public sealed class MoveCategoryTable
    {
        public enum Kind { Punching, Sound }

        /// <summary>Room a list gets in the expanded ARM9 area; the count is a byte immediate, so 255 is the ceiling.</summary>
        public const int ExpandedCapacity = 64;

        /// <summary>The size of the block the toolbox patch places.</summary>
        public const int ExpansionBlockLength = SyntheticOverlaySpace.HeaderSize + ExpandedCapacity * 2;

        /// <summary>The battle overlay file whose code points at the list.</summary>
        public string CodePath => _ovPath;

        public static string MarkerOf(Kind kind) => kind == Kind.Punching ? "PUNCHMOVESX1" : "SOUNDMOVESX1";
        public static string NameOf(Kind kind) => kind == Kind.Punching ? "punching" : "sound";
        public static GameTable TableOf(Kind kind) => kind == Kind.Punching ? GameTable.PunchingMoves : GameTable.SoundMoves;

        /// <summary>The DS Pokémon Hacking wiki's write-up of each list, which also covers the trainer AI side.</summary>
        public static string GuideUrl(Kind kind) => "https://ds-pokemon-hacking.github.io/docs/generation-iv/guides/editing_moves/"
            + (kind == Kind.Punching ? "#punching-moves" : "#sound-based-moves");

        public Kind Which { get; }
        public List<ushort> Moves { get; } = new List<ushort>();
        public int Capacity { get; private set; }
        public bool InExpansion { get; private set; }
        public string Where { get; private set; }

        private string _path, _ovPath;
        private int _offset;
        private MoveListSites _sites;

        private MoveCategoryTable(Kind kind) { Which = kind; }

        public static string WhyNot(Kind kind)
        {
            if (isHGE) return "hg-engine keeps these lists in its own source.";
            MoveListSites sites = MoveListSitesOf(TableOf(kind));
            if (sites == null) return "Only US HeartGold, Platinum (Rev 1) and Diamond are supported.";
            return GameTableFile.WhyNot(TableOf(kind), sites.VanillaCapacity * 2);
        }

        /// <summary>Whether the list already sits in a block DSPRE placed in the expanded ARM9 area.</summary>
        public static bool IsExpanded(Kind kind)
        {
            try { return WhyNot(kind) == null && Load(kind).InExpansion; }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is InvalidOperationException) { return false; }
        }

        public static MoveCategoryTable Load(Kind kind)
        {
            if (WhyNot(kind) is string why) throw new InvalidOperationException(why);
            MoveCategoryTable table = new MoveCategoryTable(kind);
            table.Locate();
            byte[] ov = File.ReadAllBytes(table._ovPath);
            byte imm = ov[table._sites.CountCompare], op = ov[table._sites.CountCompare + 1];
            if ((op & 0xF8) != 0x28)
                throw new InvalidDataException($"The battle code's count check for the {NameOf(kind)} move list doesn't look like the game's, so DSPRE won't edit it.");
            byte[] data = File.ReadAllBytes(table._path);
            if (table._offset + imm * 2 > data.Length)
                throw new InvalidDataException($"The {NameOf(kind)} move list runs past the end of its file.");
            for (int i = 0; i < imm; i++) table.Moves.Add(BitConverter.ToUInt16(data, table._offset + 2 * i));
            if (!table.InExpansion) table.Capacity = Math.Max(table.Capacity, imm);
            return table;
        }

        /// <summary>Follows the battle overlay's pointer, so a list a patch moved is edited where it now lives.</summary>
        private void Locate()
        {
            TableSpot spot = SpotOf(TableOf(Which)).Value;
            _sites = MoveListSitesOf(TableOf(Which));
            _ovPath = GameTableFile.PathOf(spot);
            if (OverlayUtils.IsCompressed(spot.Overlay)) OverlayUtils.Decompress(spot.Overlay);
            byte[] ov = File.ReadAllBytes(_ovPath);
            if (_sites.PointerSite + 4 > ov.Length || _sites.CountCompare + 2 > ov.Length)
                throw new InvalidDataException($"Overlay {spot.Overlay} is too short for the {NameOf(Which)} move list's code.");
            uint ovBase = OverlayUtils.OverlayTable.GetRAMAddress(spot.Overlay);
            uint ram = BitConverter.ToUInt32(ov, _sites.PointerSite);

            if (ram >= ovBase && ram < ovBase + ov.Length)
            {
                _path = _ovPath; _offset = (int)(ram - ovBase);
                bool vanilla = _offset == spot.Offset;
                Capacity = vanilla ? _sites.VanillaCapacity : 0;
                Where = vanilla ? $"in overlay {spot.Overlay}" : $"moved within overlay {spot.Overlay}";
            }
            else if (ram >= synthOverlayLoadAddress && File.Exists(Filesystem.expArmPath)
                     && ram - synthOverlayLoadAddress < (ulong)new FileInfo(Filesystem.expArmPath).Length)
            {
                _path = Filesystem.expArmPath; _offset = (int)(ram - synthOverlayLoadAddress);
                Where = "in the expanded ARM9 area";
                byte[] synth = File.ReadAllBytes(_path);
                (long Start, long End) block = SyntheticOverlaySpace.Blocks(synth, MarkerOf(Which))
                    .FirstOrDefault(b => _offset == b.Start + SyntheticOverlaySpace.HeaderSize);
                InExpansion = block.End > 0;
                Capacity = InExpansion ? (int)BitConverter.ToUInt32(synth, (int)block.Start + 0x14) : 0;
            }
            else throw new InvalidDataException($"The {NameOf(Which)} move list was moved to 0x{ram:X8}, which DSPRE can't follow.");
        }

        public bool Contains(int move) => Moves.Contains((ushort)move);

        /// <summary>Adds or removes a move; returns false when the list has no room for one more.</summary>
        public bool Set(int move, bool listed)
        {
            ushort id = (ushort)move;
            if (listed == Moves.Contains(id)) return true;
            if (!listed) { Moves.Remove(id); return true; }
            if (Moves.Count >= Capacity) return false;
            Moves.Add(id);
            return true;
        }

        public byte[] ToBytes()
        {
            byte[] data = new byte[Math.Max(Capacity, Moves.Count) * 2];
            for (int i = 0; i < Moves.Count; i++) BitConverter.GetBytes(Moves[i]).CopyTo(data, 2 * i);
            return data;
        }

        /// <summary>Writes the list and its count; a failed count write puts the list back, so neither changes.</summary>
        public void Save()
        {
            if (Moves.Count > Capacity)
                throw new InvalidOperationException($"The {NameOf(Which)} move list holds {Capacity} moves and this one has {Moves.Count}.");
            if (Moves.Count > byte.MaxValue)
                throw new InvalidOperationException($"The {NameOf(Which)} move list can't count past {byte.MaxValue} moves.");
            byte[] table = ToBytes();
            byte[] before = DSUtils.ReadFromFile(_path, _offset, table.Length);
            DSUtils.WriteToFile(_path, table, (uint)_offset);
            try { DSUtils.WriteToFile(_ovPath, new[] { (byte)Moves.Count }, (uint)_sites.CountCompare); }
            catch
            {
                DSUtils.WriteToFile(_path, before, (uint)_offset);
                throw;
            }
        }

        /// <summary>
        /// Moves the list into a marked block in the expanded ARM9 area with room for <see cref="ExpandedCapacity"/>
        /// moves and points the battle code at it. The list keeps its moves and count.
        /// </summary>
        public void MoveToExpansion()
        {
            if (InExpansion) return;
            if (!SyntheticOverlaySpace.Available())
                throw new InvalidOperationException("Apply the ARM9 expansion in the ROM Patch Toolbox first.");
            if (Moves.Count > ExpandedCapacity)
                throw new InvalidOperationException($"The {NameOf(Which)} move list already has more than {ExpandedCapacity} moves.");

            byte[] block = new byte[ExpansionBlockLength];
            System.Text.Encoding.ASCII.GetBytes(MarkerOf(Which)).CopyTo(block, 0);
            BitConverter.GetBytes(1u).CopyTo(block, 0x0C);
            BitConverter.GetBytes((uint)block.Length).CopyTo(block, 0x10);
            BitConverter.GetBytes((uint)ExpandedCapacity).CopyTo(block, 0x14);
            for (int i = 0; i < Moves.Count; i++) BitConverter.GetBytes(Moves[i]).CopyTo(block, SyntheticOverlaySpace.HeaderSize + 2 * i);

            byte[] synth = File.ReadAllBytes(Filesystem.expArmPath);
            int at = SyntheticOverlaySpace.Place(synth, block.Length, SyntheticOverlaySpace.Reserved(synth));
            if (at < 0) throw new InvalidOperationException($"No free space was found in the expanded ARM9 area for the {NameOf(Which)} move list.");
            byte[] ov = File.ReadAllBytes(_ovPath);
            byte[] ovBefore = (byte[])ov.Clone(), synthBefore = (byte[])synth.Clone();
            block.CopyTo(synth, at);
            uint ram = synthOverlayLoadAddress + (uint)(at + SyntheticOverlaySpace.HeaderSize);
            BitConverter.GetBytes(ram).CopyTo(ov, _sites.PointerSite);
            ov[_sites.CountCompare] = (byte)Moves.Count;
            try
            {
                File.WriteAllBytes(Filesystem.expArmPath, synth);
                File.WriteAllBytes(_ovPath, ov);
            }
            catch
            {
                File.WriteAllBytes(Filesystem.expArmPath, synthBefore);
                File.WriteAllBytes(_ovPath, ovBefore);
                throw;
            }
            _path = Filesystem.expArmPath;
            _offset = at + SyntheticOverlaySpace.HeaderSize;
            Capacity = ExpandedCapacity;
            InExpansion = true;
            Where = "in the expanded ARM9 area";
        }
    }
}

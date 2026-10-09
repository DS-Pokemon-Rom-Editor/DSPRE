using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The Battle Point exchange counters. Platinum has two item lists priced from one table, movable into the synthetic
    /// overlay; Diamond and Pearl split one fixed 41-row item/price table between the two counters.
    /// </summary>
    public sealed class BpShopData
    {
        public sealed class Entry
        {
            public ushort Item { get; set; }
            public ushort Price { get; set; }
        }

        public const string Marker = "BPSHOPEXPV1\0";
        public const int ExchangeRows = 41, VanillaLeft = 26, VanillaRight = 15, MaxListItems = 255;
        private const ushort ListEnd = 0xFFFF;
        private const int FirstTm = 328, LastHm = 427;

        public List<Entry> Left { get; } = new List<Entry>();
        public List<Entry> Right { get; } = new List<Entry>();
        public bool IsPlatinum { get; }

        /// <summary>Where the lists were found, for the window's status line.</summary>
        public string Where { get; private set; } = "";
        public bool InExpansion => _blockStart >= 0 && !_inPlace;
        /// <summary>Moved by some other patch; saving refuses rather than guess where to write.</summary>
        public bool MovedByPatch => IsPlatinum && !_inPlace && _blockStart < 0;
        public bool InPlace => _inPlace;

        private readonly BpShopSites _sites;
        // Platinum: the lists' block in the synthetic overlay, -1 when they are not there.
        private int _blockStart = -1, _blockLength;
        private bool _inPlace;

        private BpShopData(BpShopSites sites) { _sites = sites; IsPlatinum = sites.ListPointers >= 0; }

        public static string WhyNot()
        {
            if (gameFamily == GameFamilies.HGSS)
                return "HeartGold and SoulSilver build their Battle Point exchange menus in scripts. Use the Script and Text editors.";
            BpShopSites sites = BpShopCodeSites;
            if (sites == null) return "Only US Platinum (Rev 1) and Diamond are supported.";
            if (!IsDsRomProject && ARM9.CheckCompressionMark()) return "arm9 is still compressed. Convert this project to ds-rom format first.";
            if (!File.Exists(arm9Path)) return "arm9 is missing from this project.";
            if (sites.ListPointers >= 0 && !File.Exists(OverlayUtils.GetPath(7))) return "Overlay 7 is missing from this project.";
            return null;
        }

        public static BpShopData Load()
        {
            if (WhyNot() is string why) throw new InvalidOperationException(why);
            BpShopData shop = new BpShopData(BpShopCodeSites);
            if (shop.IsPlatinum) shop.LoadPlatinum(); else shop.LoadDiamond();
            return shop;
        }

        // ---------------------------------------------------------------- Platinum

        private byte[] ReadOverlay7()
        {
            if (OverlayUtils.IsCompressed(7)) OverlayUtils.Decompress(7);
            return File.ReadAllBytes(OverlayUtils.GetPath(7));
        }

        private static byte[] ReadSynth() => File.Exists(Filesystem.expArmPath) ? File.ReadAllBytes(Filesystem.expArmPath) : null;

        /// <summary>Resolves a RAM address to (bytes, offset, 0 arm9 / 1 overlay 7 / 2 synthetic overlay).</summary>
        private static (byte[] data, int offset, int where) Resolve(uint ram, int length, byte[] arm9, byte[] ov7, uint ov7Base, byte[] synth, string what)
        {
            if (synth != null && ram >= synthOverlayLoadAddress && ram - synthOverlayLoadAddress + (ulong)length <= (ulong)synth.Length)
                return (synth, (int)(ram - synthOverlayLoadAddress), 2);
            if (ov7 != null && ram >= ov7Base && ram - ov7Base + (ulong)length <= (ulong)ov7.Length)
                return (ov7, (int)(ram - ov7Base), 1);
            if (ram >= ARM9.address && ram - ARM9.address + (ulong)length <= (ulong)arm9.Length)
                return (arm9, (int)(ram - ARM9.address), 0);
            throw new InvalidDataException($"The {what} was moved to 0x{ram:X8}, which DSPRE can't follow.");
        }

        private static List<ushort> ReadList((byte[] data, int offset, int where) at, string what)
        {
            List<ushort> items = new List<ushort>();
            for (int o = at.offset; ; o += 2)
            {
                if (o + 2 > at.data.Length) throw new InvalidDataException($"The {what} has no end marker.");
                ushort v = BitConverter.ToUInt16(at.data, o);
                if (v == ListEnd) return items;
                if (items.Count == MaxListItems) throw new InvalidDataException($"The {what} has no end marker.");
                items.Add(v);
            }
        }

        private void LoadPlatinum()
        {
            byte[] arm9 = File.ReadAllBytes(arm9Path), ov7 = ReadOverlay7(), synth = ReadSynth();
            uint ov7Base = OverlayUtils.OverlayTable.GetRAMAddress(7);

            (byte[] data, int offset, int where) right = Resolve(BitConverter.ToUInt32(arm9, _sites.ListPointers), 2, arm9, null, 0, synth, "TM counter list");
            (byte[] data, int offset, int where) left = Resolve(BitConverter.ToUInt32(arm9, _sites.ListPointers + 4), 2, arm9, null, 0, synth, "item counter list");
            uint priceRam = BitConverter.ToUInt32(ov7, _sites.PriceLiteral);
            if (BitConverter.ToUInt32(ov7, _sites.PriceLiteral + 4) != priceRam + 2)
                throw new InvalidDataException("The Battle Point price code doesn't look like Platinum's; it may have been patched.");
            if (ov7[_sites.PriceCountCompare + 1] != 0x2A)
                throw new InvalidDataException("The Battle Point price lookup doesn't look like Platinum's; it may have been patched.");
            int rows = ov7[_sites.PriceCountCompare];
            (byte[] data, int offset, int where) prices = Resolve(priceRam, rows * 4, arm9, ov7, ov7Base, synth, "price table");

            Dictionary<ushort, ushort> priceOf = new Dictionary<ushort, ushort>();
            for (int r = 0; r < rows; r++)
            {
                ushort item = BitConverter.ToUInt16(prices.data, prices.offset + r * 4);
                if (!priceOf.ContainsKey(item)) priceOf[item] = BitConverter.ToUInt16(prices.data, prices.offset + r * 4 + 2);
            }
            foreach (ushort i in ReadList(left, "item counter list")) Left.Add(new Entry { Item = i, Price = priceOf.GetValueOrDefault(i) });
            foreach (ushort i in ReadList(right, "TM counter list")) Right.Add(new Entry { Item = i, Price = priceOf.GetValueOrDefault(i) });

            _inPlace = left.where == 0 && left.offset == SpotOf(GameTable.BpShopItems)?.Offset
                && right.where == 0 && right.offset == SpotOf(GameTable.BpShopTms)?.Offset
                && prices.where == 1 && prices.offset == SpotOf(GameTable.BpShopPrices)?.Offset;
            if (synth != null && left.where == 2)
            {
                (long Start, long End) block = SyntheticOverlaySpace.Blocks(synth, Marker).FirstOrDefault(b => left.offset >= b.Start && left.offset < b.End);
                if (block.End > 0) { _blockStart = (int)block.Start; _blockLength = (int)(block.End - block.Start); }
            }
            Where = _inPlace ? "where the game keeps them" : _blockStart >= 0 ? "moved to the expanded ARM9 area" : "moved by a patch";
        }

        /// <summary>One row per distinct item, left counter first; the game prices an item by its first row.</summary>
        private List<Entry> PriceRows()
        {
            List<Entry> rows = new List<Entry>();
            foreach (Entry e in Left.Concat(Right))
                if (!rows.Any(r => r.Item == e.Item)) rows.Add(e);
            return rows;
        }

        /// <summary>Whether entries can be added, removed or moved between counters.</summary>
        public bool CanResize => IsPlatinum;

        /// <summary>The most price rows the game looks through: the count is a byte immediate.</summary>
        public const int MaxPriceRows = 255;

        /// <summary>Whether the lists sit in a block with room for the game's limits, as the toolbox patch leaves them.</summary>
        public bool Expanded => InExpansion && _blockLength >= FixedBlockLength;

        /// <summary>The lists outgrew their room; the "Expand the Battle Point lists" toolbox patch makes more.</summary>
        public bool NeedsExpansion => IsPlatinum && !Expanded && !FitsCurrentRoom;

        // A block an older DSPRE sized to its lists still takes them while they fit.
        private bool FitsCurrentRoom => _inPlace ? FitsInPlace
            : _blockStart >= 0 && BuildBlock(Left, Right, PriceRows(), out _, out _, out _).Length <= _blockLength;

        public bool FitsInPlace => IsPlatinum
            ? Left.Count <= VanillaLeft && Right.Count <= VanillaRight && PriceRows().Count <= ExchangeRows
            : Left.Count + Right.Count <= ExchangeRows;

        private static void Put16(byte[] d, int o, int v) { d[o] = (byte)v; d[o + 1] = (byte)(v >> 8); }
        private static void Put32(byte[] d, int o, uint v) { for (int i = 0; i < 4; i++) d[o + i] = (byte)(v >> (8 * i)); }

        private static void WriteList(byte[] d, int o, List<Entry> list, int slots)
        {
            Array.Clear(d, o, slots * 2);
            for (int i = 0; i < list.Count; i++) Put16(d, o + i * 2, list[i].Item);
            Put16(d, o + list.Count * 2, ListEnd);
        }

        private static void WriteRows(byte[] d, int o, IReadOnlyList<Entry> rows, int slots)
        {
            Array.Clear(d, o, slots * 4);
            for (int i = 0; i < rows.Count; i++) { Put16(d, o + i * 4, rows[i].Item); Put16(d, o + i * 4 + 2, rows[i].Price); }
        }

        /// <summary>Builds the synthetic-overlay block: header, left list, right list, then the price rows.</summary>
        private static int FixedBlockLength => BuildBlock(new List<Entry>(), new List<Entry>(), new List<Entry>(), out _, out _, out _, fixedRoom: true).Length;

        /// <param name="fixedRoom">Lays the lists out at the game's limits, so they never move as they grow.</param>
        internal static byte[] BuildBlock(List<Entry> left, List<Entry> right, List<Entry> rows, out int leftAt, out int rightAt, out int pricesAt, bool fixedRoom = false)
        {
            leftAt = SyntheticOverlaySpace.HeaderSize;
            rightAt = leftAt + ((fixedRoom ? MaxListItems : left.Count) + 1) * 2;
            pricesAt = (rightAt + ((fixedRoom ? MaxListItems : right.Count) + 1) * 2 + 3) & ~3;
            byte[] block = new byte[pricesAt + (fixedRoom ? MaxPriceRows : rows.Count) * 4];
            Encoding.ASCII.GetBytes(Marker).CopyTo(block, 0);
            Put32(block, 0x0C, 1);
            Put32(block, 0x10, (uint)block.Length);
            Put32(block, 0x14, (uint)left.Count);
            Put32(block, 0x18, (uint)right.Count);
            Put32(block, 0x1C, (uint)rows.Count);
            WriteList(block, leftAt, left, left.Count + 1);
            WriteList(block, rightAt, right, right.Count + 1);
            WriteRows(block, pricesAt, rows, rows.Count);
            return block;
        }

        /// <summary>
        /// Moves the lists, as saved, into their own expanded ARM9 block laid out at the game's limits, and points the
        /// exchange and price code at it. The toolbox patch calls this; the editor never moves the lists itself.
        /// </summary>
        /// <summary>The size of the block the toolbox patch places.</summary>
        public int ExpansionBlockLength => BuildBlock(Left, Right, PriceRows(), out _, out _, out _, fixedRoom: true).Length;

        public void MoveToExpansion()
        {
            if (!IsPlatinum || Expanded) return;
            if (MovedByPatch) throw new InvalidOperationException("The Battle Point lists were moved by a patch DSPRE doesn't know, so it won't move them.");
            if (!SyntheticOverlaySpace.Available()) throw new InvalidOperationException("Apply the ARM9 expansion in the ROM Patch Toolbox first.");
            byte[] arm9 = File.ReadAllBytes(arm9Path), ov7 = ReadOverlay7(), synth = ReadSynth();
            byte[] arm9Before = (byte[])arm9.Clone(), ov7Before = (byte[])ov7.Clone(), synthBefore = (byte[])synth.Clone();
            List<Entry> rows = PriceRows();
            byte[] block = BuildBlock(Left, Right, rows, out int leftAt, out int rightAt, out int pricesAt, fixedRoom: true);
            // A block an older DSPRE sized to its lists is freed for the new one.
            if (_blockStart >= 0) Array.Clear(synth, _blockStart, _blockLength);
            int at = SyntheticOverlaySpace.Place(synth, block.Length, SyntheticOverlaySpace.Reserved(synth));
            if (at < 0) throw new InvalidOperationException("No free space was found in the expanded ARM9 area for the Battle Point lists.");
            block.CopyTo(synth, at);
            uint ram = synthOverlayLoadAddress + (uint)at;
            Put32(arm9, _sites.ListPointers, ram + (uint)rightAt);
            Put32(arm9, _sites.ListPointers + 4, ram + (uint)leftAt);
            Put32(ov7, _sites.PriceLiteral, ram + (uint)pricesAt);
            Put32(ov7, _sites.PriceLiteral + 4, ram + (uint)pricesAt + 2);
            ov7[_sites.PriceCountCompare] = (byte)rows.Count;
            try
            {
                File.WriteAllBytes(Filesystem.expArmPath, synth);
                File.WriteAllBytes(OverlayUtils.GetPath(7), ov7);
                File.WriteAllBytes(arm9Path, arm9);
            }
            catch
            {
                File.WriteAllBytes(Filesystem.expArmPath, synthBefore);
                File.WriteAllBytes(OverlayUtils.GetPath(7), ov7Before);
                File.WriteAllBytes(arm9Path, arm9Before);
                throw;
            }
            _inPlace = false;
            _blockStart = at; _blockLength = block.Length;
            Where = "moved to the expanded ARM9 area";
        }

        private void SavePlatinum()
        {
            byte[] arm9 = File.ReadAllBytes(arm9Path), ov7 = ReadOverlay7(), synth = ReadSynth();
            byte[] arm9Before = (byte[])arm9.Clone(), ov7Before = (byte[])ov7.Clone(), synthBefore = (byte[])synth?.Clone();
            List<Entry> rows = PriceRows();
            bool synthChanged = false;

            if (_inPlace && FitsInPlace)
            {
                WriteList(arm9, SpotOf(GameTable.BpShopItems).Value.Offset, Left, VanillaLeft + 1);
                WriteList(arm9, SpotOf(GameTable.BpShopTms).Value.Offset, Right, VanillaRight + 1);
                WriteRows(ov7, SpotOf(GameTable.BpShopPrices).Value.Offset, rows, ExchangeRows);
            }
            else if (_blockStart >= 0 && (Expanded || FitsCurrentRoom))
            {
                // Writes into the block the lists already have; moving them is the toolbox patch's job.
                byte[] block = BuildBlock(Left, Right, rows, out int leftAt, out int rightAt, out int pricesAt, fixedRoom: Expanded);
                int at = _blockStart;
                Array.Clear(synth, _blockStart, _blockLength);
                block.CopyTo(synth, at);
                synthChanged = true;
                uint ram = synthOverlayLoadAddress + (uint)at;
                Put32(arm9, _sites.ListPointers, ram + (uint)rightAt);
                Put32(arm9, _sites.ListPointers + 4, ram + (uint)leftAt);
                Put32(ov7, _sites.PriceLiteral, ram + (uint)pricesAt);
                Put32(ov7, _sites.PriceLiteral + 4, ram + (uint)pricesAt + 2);
                ov7[_sites.PriceCountCompare] = (byte)rows.Count;
                Where = "moved to the expanded ARM9 area";
            }
            else if (_inPlace || _blockStart >= 0)
                throw new InvalidOperationException("The Battle Point lists don't fit where they are; apply the \"Expand the Battle Point lists\" patch in the ROM Patch Toolbox.");
            else throw new InvalidOperationException("The Battle Point lists were moved by a patch DSPRE doesn't know, so it won't write them.");

            // The script command's copy is read by no retail script; keep it matching while it still fits.
            if (Left.Count + Right.Count <= ExchangeRows && SplitLooksVanilla(arm9)) WriteExchangeRows(arm9);

            try
            {
                if (synthChanged) File.WriteAllBytes(Filesystem.expArmPath, synth);
                File.WriteAllBytes(OverlayUtils.GetPath(7), ov7);
                File.WriteAllBytes(arm9Path, arm9);
            }
            catch
            {
                if (synthChanged) File.WriteAllBytes(Filesystem.expArmPath, synthBefore);
                File.WriteAllBytes(OverlayUtils.GetPath(7), ov7Before);
                File.WriteAllBytes(arm9Path, arm9Before);
                throw;
            }
        }

        // ---------------------------------------------------------------- both

        private void WriteExchangeRows(byte[] arm9)
        {
            WriteRows(arm9, _sites.ExchangeTable, Left.Concat(Right).ToList(), ExchangeRows);
            arm9[_sites.ExchangeSplit] = (byte)Left.Count;
        }

        private bool SplitLooksVanilla(byte[] arm9) => arm9[_sites.ExchangeSplit + 1] == 0x21;

        // ---------------------------------------------------------------- Diamond

        // The counters' sizes live in script 0356, so the split stays fixed and only items and prices change.
        private void LoadDiamond()
        {
            byte[] arm9 = File.ReadAllBytes(arm9Path);
            if (!SplitLooksVanilla(arm9)) throw new InvalidDataException("The Battle Point exchange code doesn't look like Diamond's; it may have been patched.");
            _split = arm9[_sites.ExchangeSplit];
            if (_split < 1 || _split >= ExchangeRows) throw new InvalidDataException("The Battle Point exchange split is outside its table.");
            for (int i = 0; i < ExchangeRows; i++) (i < _split ? Left : Right).Add(Row(arm9, i));
            Where = "where the game keeps them";
        }

        private int _split;

        private Entry Row(byte[] arm9, int row) => new Entry
        {
            Item = BitConverter.ToUInt16(arm9, _sites.ExchangeTable + row * 4),
            Price = BitConverter.ToUInt16(arm9, _sites.ExchangeTable + row * 4 + 2),
        };

        private void SaveDiamond()
        {
            byte[] arm9 = File.ReadAllBytes(arm9Path);
            if (!SplitLooksVanilla(arm9)) throw new InvalidOperationException("The Battle Point exchange code changed since the lists were read; reopen the editor.");
            WriteExchangeRows(arm9);
            File.WriteAllBytes(arm9Path, arm9);
        }

        // ---------------------------------------------------------------- checks

        public static bool IsTmOrHm(int item) => item >= FirstTm && item <= LastHm;

        /// <summary>Why the lists can't be saved, or null.</summary>
        public string Problem(int itemCount)
        {
            foreach ((List<Entry> list, string name) in new[] { (Left, "item counter"), (Right, "TM counter") })
            {
                if (list.Count == 0) return $"The {name} needs at least one item.";
                if (list.Any(e => e.Item == 0 || e.Item >= itemCount || e.Item == ListEnd)) return $"The {name} lists an item that doesn't exist.";
                if (list.Any(e => e.Price == 0)) return $"Every item on the {name} needs a price.";
            }
            if (IsPlatinum)
            {
                if (!_inPlace && _blockStart < 0) return "A patch moved these lists somewhere DSPRE doesn't follow, so they can't be saved here.";
                if (NeedsExpansion)
                    return $"The item counter holds {VanillaLeft} items and the TM counter {VanillaRight} where they are. The \"Expand the Battle Point lists\" patch in the ROM Patch Toolbox makes room for {MaxListItems} each.";
                if (Left.Count > MaxListItems || Right.Count > MaxListItems) return $"Each counter can show up to {MaxListItems} items.";
                // The price lookup's row count is a byte immediate.
                if (PriceRows().Count > 255) return "The game can price up to 255 different items across both counters.";
                IGrouping<ushort, Entry> twice = Left.Concat(Right).GroupBy(e => e.Item).FirstOrDefault(g => g.Select(e => e.Price).Distinct().Count() > 1);
                if (twice != null) return "The game keeps one price per item, but an item is listed twice with different prices.";
            }
            else
            {
                if (Left.Count != _split || Right.Count != ExchangeRows - _split)
                    return $"Diamond and Pearl keep {_split} items and {ExchangeRows - _split} TMs; change items rather than adding or removing them.";
            }
            return null;
        }

        /// <summary>Diamond names the TM's move when buying from the right counter, so it should only hold TMs and HMs.</summary>
        public bool RightHasNonTm => !IsPlatinum && Right.Any(e => !IsTmOrHm(e.Item));

        public void Save(int itemCount)
        {
            if (Problem(itemCount) is string p) throw new InvalidOperationException(p);
            if (IsPlatinum) SavePlatinum(); else SaveDiamond();
        }

        /// <summary>The lists as bytes, for change tracking.</summary>
        public byte[] Snapshot()
        {
            List<byte> b = new List<byte>();
            foreach (List<Entry> list in new[] { Left, Right })
            {
                b.AddRange(BitConverter.GetBytes(list.Count));
                foreach (Entry e in list) { b.AddRange(BitConverter.GetBytes(e.Item)); b.AddRange(BitConverter.GetBytes(e.Price)); }
            }
            return b.ToArray();
        }

        public void Restore(byte[] snapshot)
        {
            int o = 0;
            foreach (List<Entry> list in new[] { Left, Right })
            {
                list.Clear();
                int n = BitConverter.ToInt32(snapshot, o); o += 4;
                for (int i = 0; i < n; i++, o += 4)
                    list.Add(new Entry { Item = BitConverter.ToUInt16(snapshot, o), Price = BitConverter.ToUInt16(snapshot, o + 2) });
            }
        }
    }
}

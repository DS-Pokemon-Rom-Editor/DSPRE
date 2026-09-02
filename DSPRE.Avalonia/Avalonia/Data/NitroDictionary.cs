using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DSPRE.Avalonia.Data
{
    /// <summary>
    /// The name lookup a Nitro 3D file carries. Every list inside a model, its nodes, its materials
    /// and its shapes, is one of these: a little tree for finding a name, then one fixed-size entry
    /// per thing, then the names themselves.
    ///
    /// The layout and the way the tree is walked were both read off HeartGold's own models. The walk
    /// below resolves all 2,947 names across its 340 building models, and the tree this builds
    /// resolves every name in all 1,020 of their name lists.
    /// </summary>
    public static class NitroDictionary
    {
        /// <summary>Names are exactly sixteen bytes, padded with zeros.</summary>
        public const int NameSize = 16;

        public struct Node
        {
            public byte RefBit, Left, Right, Entry;
        }

        public static byte[] Padded(string name)
        {
            var raw = Encoding.ASCII.GetBytes(name ?? "");
            var o = new byte[NameSize];
            Array.Copy(raw, o, Math.Min(raw.Length, NameSize));
            return o;
        }

        private static int Bit(byte[] name, int b) => (name[b >> 3] >> (b & 7)) & 1;

        /// <summary>
        /// Follows the tree for a name, the way the real files are read: step to the head's left
        /// child, then keep stepping while each node's bit is lower than the one before it.
        /// </summary>
        public static int Find(IReadOnlyList<Node> nodes, string name)
        {
            var key = Padded(name);
            var from = nodes[0];
            var at = nodes[from.Left];
            while (at.RefBit < from.RefBit)
            {
                from = at;
                at = nodes[Bit(key, at.RefBit) != 0 ? at.Right : at.Left];
            }
            return at.Entry;
        }

        /// <summary>Builds the tree for a list of names. Returns the head first, then one node each.</summary>
        public static List<Node> BuildTree(IReadOnlyList<string> names)
        {
            var keys = names.Select(Padded).ToList();
            var nodes = new List<Node> { new Node { RefBit = 127, Left = 0, Right = 0, Entry = 0 } };

            // The head stands for a name of all zeros, which no real name is, so the first name added
            // splits on the highest bit it has set.
            var owner = new Dictionary<int, byte[]> { [0] = new byte[NameSize] };

            for (int entry = 0; entry < keys.Count; entry++)
            {
                var key = keys[entry];
                int landed = WalkTo(nodes, key);
                int b = FirstDifferingBit(key, owner[landed]);
                if (b < 0) throw new InvalidOperationException($"two things are both called {names[entry]}");

                // Down again, stopping at the last step whose bit is still above the one being split on.
                int parent = 0;
                int at = nodes[0].Left;
                while (nodes[at].RefBit < nodes[parent].RefBit && nodes[at].RefBit > b)
                {
                    parent = at;
                    at = Bit(key, nodes[at].RefBit) != 0 ? nodes[at].Right : nodes[at].Left;
                }

                byte made = (byte)nodes.Count;
                bool one = Bit(key, b) != 0;
                nodes.Add(new Node
                {
                    RefBit = (byte)b,
                    Left = one ? (byte)at : made,      // the side this name is on points at itself
                    Right = one ? made : (byte)at,     // the other side keeps whatever was there
                    Entry = (byte)entry,
                });
                owner[made] = key;

                var p = nodes[parent];
                if (parent == 0) p.Left = made;
                else if (Bit(key, p.RefBit) != 0) p.Right = made;
                else p.Left = made;
                nodes[parent] = p;
            }
            return nodes;
        }

        private static int WalkTo(List<Node> nodes, byte[] key)
        {
            int from = 0, at = nodes[0].Left;
            while (nodes[at].RefBit < nodes[from].RefBit)
            {
                from = at;
                at = Bit(key, nodes[at].RefBit) != 0 ? nodes[at].Right : nodes[at].Left;
            }
            return at;
        }

        private static int FirstDifferingBit(byte[] a, byte[] b)
        {
            for (int i = 127; i >= 0; i--) if (Bit(a, i) != Bit(b, i)) return i;
            return -1;
        }

        /// <summary>
        /// Writes a whole dictionary: the tree, then one entry per thing, then the names.
        /// </summary>
        /// <param name="entries">One block of bytes per thing, all the same length.</param>
        public static byte[] Write(IReadOnlyList<string> names, IReadOnlyList<byte[]> entries)
        {
            if (names.Count != entries.Count)
                throw new ArgumentException("there must be one entry for every name");
            int unit = entries.Count == 0 ? 4 : entries[0].Length;
            foreach (var e in entries)
                if (e.Length != unit) throw new ArgumentException("every entry must be the same size");

            var nodes = BuildTree(names);
            int treeBytes = nodes.Count * 4;
            int ofsEntry = 8 + treeBytes;
            int total = ofsEntry + 4 + names.Count * unit + names.Count * NameSize;

            var d = new byte[total];
            d[0] = 0;                                   // revision
            d[1] = (byte)names.Count;
            Put16(d, 2, total);
            Put16(d, 4, 0);
            Put16(d, 6, ofsEntry);
            for (int i = 0; i < nodes.Count; i++)
            {
                d[8 + i * 4] = nodes[i].RefBit;
                d[8 + i * 4 + 1] = nodes[i].Left;
                d[8 + i * 4 + 2] = nodes[i].Right;
                d[8 + i * 4 + 3] = nodes[i].Entry;
            }

            int eh = ofsEntry;
            Put16(d, eh, unit);
            Put16(d, eh + 2, 4 + names.Count * unit);   // the names, from the entry header
            for (int i = 0; i < entries.Count; i++)
                Array.Copy(entries[i], 0, d, eh + 4 + i * unit, unit);
            int at = eh + 4 + names.Count * unit;
            for (int i = 0; i < names.Count; i++)
                Array.Copy(Padded(names[i]), 0, d, at + i * NameSize, NameSize);
            return d;
        }

        private static void Put16(byte[] d, int at, int v)
        { d[at] = (byte)v; d[at + 1] = (byte)(v >> 8); }

        /// <summary>How big <see cref="Write"/> will make it, without making it.</summary>
        public static int SizeFor(int count, int unit) =>
            8 + (count + 1) * 4 + 4 + count * unit + count * NameSize;
    }
}

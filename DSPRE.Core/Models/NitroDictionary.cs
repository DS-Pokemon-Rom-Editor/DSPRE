using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DSPRE.Models
{
    /// <summary>Nitro 3D name dictionary.</summary>
    public static class NitroDictionary
    {
        public const int NameSize = 16;

        /// <summary>Truncates a name to the 16 characters a dictionary stores.</summary>
        public static string Fit(string name) => name == null ? null : name.Length > NameSize ? name.Substring(0, NameSize) : name;

        public struct Node
        {
            public byte RefBit, Left, Right, Entry;
        }

        public static byte[] Padded(string name)
        {
            byte[] raw = Encoding.ASCII.GetBytes(name ?? "");
            byte[] o = new byte[NameSize];
            Array.Copy(raw, o, Math.Min(raw.Length, NameSize));
            return o;
        }

        private static int Bit(byte[] name, int b) => (name[b >> 3] >> (b & 7)) & 1;

        public static int Find(IReadOnlyList<Node> nodes, string name)
        {
            byte[] key = Padded(name);
            Node from = nodes[0];
            Node at = nodes[from.Left];
            while (at.RefBit < from.RefBit)
            {
                from = at;
                at = nodes[Bit(key, at.RefBit) != 0 ? at.Right : at.Left];
            }
            return at.Entry;
        }

        public static List<Node> BuildTree(IReadOnlyList<string> names)
        {
            List<byte[]> keys = names.Select(Padded).ToList();
            List<Node> nodes = new List<Node> { new Node { RefBit = 127, Left = 0, Right = 0, Entry = 0 } };

            Dictionary<int, byte[]> owner = new Dictionary<int, byte[]> { [0] = new byte[NameSize] };

            for (int entry = 0; entry < keys.Count; entry++)
            {
                byte[] key = keys[entry];
                int landed = WalkTo(nodes, key);
                int b = FirstDifferingBit(key, owner[landed]);
                if (b < 0) throw new InvalidOperationException($"two things are both called {names[entry]}");

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
                    Left = one ? (byte)at : made,
                    Right = one ? made : (byte)at,
                    Entry = (byte)entry,
                });
                owner[made] = key;

                Node p = nodes[parent];
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

        public static byte[] Write(IReadOnlyList<string> names, IReadOnlyList<byte[]> entries)
        {
            if (names.Count != entries.Count)
                throw new ArgumentException("there must be one entry for every name");
            int unit = entries.Count == 0 ? 4 : entries[0].Length;
            foreach (byte[] e in entries)
                if (e.Length != unit) throw new ArgumentException("every entry must be the same size");

            List<Node> nodes = BuildTree(names);
            int treeBytes = nodes.Count * 4;
            int ofsEntry = 8 + treeBytes;
            int total = ofsEntry + 4 + names.Count * unit + names.Count * NameSize;

            byte[] d = new byte[total];
            d[0] = 0;
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
            Put16(d, eh + 2, 4 + names.Count * unit);
            for (int i = 0; i < entries.Count; i++)
                Array.Copy(entries[i], 0, d, eh + 4 + i * unit, unit);
            int at = eh + 4 + names.Count * unit;
            for (int i = 0; i < names.Count; i++)
                Array.Copy(Padded(names[i]), 0, d, at + i * NameSize, NameSize);
            return d;
        }

        private static void Put16(byte[] d, int at, int v)
        { d[at] = (byte)v; d[at + 1] = (byte)(v >> 8); }

        public static List<(string name, byte[] entry)> Read(byte[] d, int at)
        {
            int count = d[at + 1];
            int ofsEntry = d[at + 6] | (d[at + 7] << 8);
            int eh = at + ofsEntry;
            int unit = d[eh] | (d[eh + 1] << 8);
            List<(string, byte[])> read = new List<(string, byte[])>();
            int names = eh + 4 + count * unit;
            for (int i = 0; i < count; i++)
            {
                byte[] entry = new byte[unit];
                Array.Copy(d, eh + 4 + i * unit, entry, 0, unit);
                int n = 0;
                while (n < NameSize && d[names + i * NameSize + n] != 0) n++;
                read.Add((System.Text.Encoding.ASCII.GetString(d, names + i * NameSize, n), entry));
            }
            return read;
        }

        public static int SizeFor(int count, int unit) =>
            8 + (count + 1) * 4 + 4 + count * unit + count * NameSize;
    }
}

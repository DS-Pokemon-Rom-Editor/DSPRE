using System;
using System.Collections.Generic;
using System.IO;

namespace DSPRE.Tracking
{
    /// <summary>An xdelta (VCDIFF, RFC 3284) patch from one ROM to another, for players who don't use DSPRE.</summary>
    public static class Vcdiff
    {
        // Copies and adds only, standard code table, no secondary compression, so any decoder reads it.
        // A copy under MinCopy costs more than adding the bytes.
        private const int Block = 32, MinCopy = 24;
        // Decoders cap a window's size, so the target goes in windows of 4 MiB.
        private const int Window = 1 << 22;
        private const ulong Base = 0x100000001B3;
        private const byte Add = 1, Copy = 19;

        public static byte[] Encode(byte[] source, byte[] target)
        {
            Dictionary<ulong, int> blocks = new Dictionary<ulong, int>();
            for (int i = 0; i + Block <= source.Length; i += Block) blocks.TryAdd(Hash(source, i), i);
            ulong top = 1;
            for (int i = 1; i < Block; i++) top *= Base;

            using MemoryStream patch = new MemoryStream();
            patch.Write(new byte[] { 0xD6, 0xC3, 0xC4, 0x00, 0x00 });
            int start = 0;
            do
            {
                int end = Math.Min(target.Length, start + Window);
                WriteWindow(patch, source, target, start, end, blocks, top);
                start = end;
            }
            while (start < target.Length);
            return patch.ToArray();
        }

        private static void WriteWindow(Stream patch, byte[] source, byte[] target, int start, int end, Dictionary<ulong, int> blocks, ulong top)
        {
            using MemoryStream data = new MemoryStream(), instructions = new MemoryStream(), addresses = new MemoryStream();
            int literal = start, p = start;
            ulong hash = 0;
            int hashedAt = int.MinValue;
            while (p < end)
            {
                // The same place in the original first, since most of a ROM hack doesn't move; then any block that matches.
                int from = -1, length = 0;
                if (p < source.Length) (from, length) = (p, Match(source, p, target, p, end));
                if (length < MinCopy && p + Block <= end)
                {
                    if (hashedAt == p - 1) hash = (hash - target[p - 1] * top) * Base + target[p + Block - 1];
                    else hash = Hash(target, p);
                    hashedAt = p;
                    if (blocks.TryGetValue(hash, out int at))
                    {
                        int found = Match(source, at, target, p, end);
                        if (found > length) (from, length) = (at, found);
                    }
                }
                if (length < MinCopy)
                {
                    p++;
                    continue;
                }
                // Reach back into the bytes not yet written while they match too.
                while (p > literal && from > 0 && source[from - 1] == target[p - 1])
                {
                    p--;
                    from--;
                    length++;
                }
                if (p > literal)
                {
                    instructions.WriteByte(Add);
                    WriteInt(instructions, p - literal);
                    data.Write(target, literal, p - literal);
                }
                instructions.WriteByte(Copy);
                WriteInt(instructions, length);
                WriteInt(addresses, from);
                p += length;
                literal = p;
                hashedAt = int.MinValue;
            }
            if (end > literal)
            {
                instructions.WriteByte(Add);
                WriteInt(instructions, end - literal);
                data.Write(target, literal, end - literal);
            }

            using MemoryStream delta = new MemoryStream();
            WriteInt(delta, end - start);
            delta.WriteByte(0);
            WriteInt(delta, (int)data.Length);
            WriteInt(delta, (int)instructions.Length);
            WriteInt(delta, (int)addresses.Length);
            data.WriteTo(delta);
            instructions.WriteTo(delta);
            addresses.WriteTo(delta);

            // The whole original is the source segment of every window.
            patch.WriteByte(0x01);
            WriteInt(patch, source.Length);
            WriteInt(patch, 0);
            WriteInt(patch, (int)delta.Length);
            delta.WriteTo(patch);
        }

        private static int Match(byte[] source, int from, byte[] target, int at, int end)
        {
            int n = 0;
            while (from + n < source.Length && at + n < end && source[from + n] == target[at + n]) n++;
            return n;
        }

        private static ulong Hash(byte[] bytes, int at)
        {
            ulong h = 0;
            for (int i = 0; i < Block; i++) h = h * Base + bytes[at + i];
            return h;
        }

        // Base-128, most significant group first, every byte but the last with its top bit set.
        private static void WriteInt(Stream s, int value)
        {
            Span<byte> buffer = stackalloc byte[5];
            int n = 0;
            do
            {
                buffer[4 - n] = (byte)(value & 0x7F);
                value >>= 7;
                n++;
            }
            while (value > 0);
            for (int i = 5 - n; i < 5; i++) s.WriteByte(i < 4 ? (byte)(buffer[i] | 0x80) : buffer[i]);
        }
    }
}

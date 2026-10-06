using System;
using System.Collections.Generic;
using System.Text;

namespace DSPRE.Models
{
    /// <summary>Locates the blocks of an NSBMD so single shapes can be replaced in place.</summary>
    public sealed class NsbmdFile
    {
        private const uint MagicBmd0 = 0x30444D42;
        private const uint MagicMdl0 = 0x304C444D;

        public sealed class Shape
        {
            public string Name;

            public int RecordAt;

            public int DisplayListAt;

            public int DisplayListSize;

            public int SetEntryAt;
        }

        public sealed class Named
        {
            public string Name;
            public int NameAt;

            public List<int> Materials = new List<int>();
        }

        public IReadOnlyList<Named> Pictures => _pictures;
        public IReadOnlyList<Named> Colours => _colours;

        private readonly List<Named> _pictures = new List<Named>();
        private readonly List<Named> _colours = new List<Named>();

        public byte[] Bytes { get; private set; }

        public int ModelAt { get; private set; }

        public int ShapeSetAt { get; private set; }

        public int SaysCorners { get; private set; }
        public int SaysTriangles { get; private set; }
        public int SaysQuads { get; private set; }

        public float ModelScale { get; private set; }

        private int _mdl0At;
        private int _blockCount;
        private readonly List<Shape> _shapes = new List<Shape>();
        public IReadOnlyList<Shape> Shapes => _shapes;

        private NsbmdFile() { }

        public static NsbmdFile Read(byte[] bytes, out string whynot)
        {
            whynot = null;
            if (bytes == null || bytes.Length < 0x18) { whynot = "No model data."; return null; }

            NsbmdFile f = new NsbmdFile { Bytes = bytes };
            if (U32(bytes, 0) != MagicBmd0) { whynot = "Not an NSBMD file."; return null; }

            int said = (int)U32(bytes, 8);
            if (said != bytes.Length)
                { whynot = $"Header size {said} does not match file size {bytes.Length}."; return null; }

            f._blockCount = U16(bytes, 0x0E);
            if (f._blockCount < 1) { whynot = "No blocks."; return null; }

            int mdl0 = -1;
            for (int i = 0; i < f._blockCount; i++)
            {
                int at = (int)U32(bytes, 0x10 + i * 4);
                if (at < 0 || at + 8 > bytes.Length) { whynot = "Block offset out of range."; return null; }
                if (U32(bytes, at) == MagicMdl0) { mdl0 = at; break; }
            }
            if (mdl0 < 0) { whynot = "No MDL0 block."; return null; }
            f._mdl0At = mdl0;

            int models = bytes[mdl0 + 9];
            if (models != 1)
            {
                whynot = $"{models} models; only one is supported.";
                return null;
            }

            int modelAt = mdl0 + (int)U32(bytes, mdl0 + 0x18 + models * 4);
            if (modelAt + 0x40 > bytes.Length) { whynot = "Model offset out of range."; return null; }
            f.ModelAt = modelAt;

            int shapeSet = modelAt + (int)U32(bytes, modelAt + 12);
            if (shapeSet + 16 > bytes.Length) { whynot = "Shape set offset out of range."; return null; }
            f.ShapeSetAt = shapeSet;
            f.ModelScale = (int)U32(bytes, modelAt + 28) / 4096f;
            f.SaysCorners = U16(bytes, modelAt + 36);
            f.SaysTriangles = U16(bytes, modelAt + 40);
            f.SaysQuads = U16(bytes, modelAt + 42);

            int count = bytes[shapeSet + 1];
            int alsoSaid = bytes[modelAt + 25];
            if (count != alsoSaid)
            {
                whynot = $"Model header says {alsoSaid} shapes, shape set has {count}.";
                return null;
            }
            int positions = shapeSet + 16 + count * 4;
            int names = positions + count * 4;
            if (names + count * 16 > bytes.Length) { whynot = "Shape set runs past the end of the file."; return null; }

            int texPal = modelAt + (int)U32(bytes, modelAt + 8);
            if (texPal + 4 <= bytes.Length)
            {
                ReadNames(bytes, texPal, texPal + U16(bytes, texPal), f._pictures, whichIsNull: out _);
                ReadNames(bytes, texPal, texPal + U16(bytes, texPal + 2), f._colours, whichIsNull: out _);
            }

            for (int i = 0; i < count; i++)
            {
                int entry = positions + i * 4;
                int record = shapeSet + (int)U32(bytes, entry);
                if (record < 0 || record + 16 > bytes.Length)
                    { whynot = $"Shape {i} record out of range."; return null; }

                int dl = record + (int)U32(bytes, record + 8);
                int size = (int)U32(bytes, record + 12);
                if (dl < 0 || size < 0 || dl + size > bytes.Length)
                    { whynot = $"Shape {i} display list out of range."; return null; }

                f._shapes.Add(new Shape
                {
                    Name = Name(bytes, names + i * 16),
                    RecordAt = record,
                    DisplayListAt = dl,
                    DisplayListSize = size,
                    SetEntryAt = entry,
                });
            }

            return f;
        }

        public int MaterialCount
        {
            get
            {
                int dict = MaterialSetAt + 4;
                return MaterialSetAt < 0 || dict + 2 > Bytes.Length ? 0 : Bytes[dict + 1];
            }
        }

        private int MaterialSetAt
            => ModelAt + 12 <= Bytes.Length ? ModelAt + (int)U32(Bytes, ModelAt + 8) : -1;

        public byte[] MaterialRecord(int material)
        {
            int set = MaterialSetAt;
            int count = MaterialCount;
            if (material < 0 || material >= count) return null;

            int entries = set + 4 + 16 + count * 4;
            if (entries + (material + 1) * 4 > Bytes.Length) return null;
            int record = set + (int)U32(Bytes, entries + material * 4);
            if (record < 0 || record + 4 > Bytes.Length) return null;

            int size = U16(Bytes, record + 2);
            if (size < 44 || record + size > Bytes.Length) return null;

            byte[] copy = new byte[size];
            Array.Copy(Bytes, record, copy, 0, size);
            return copy;
        }

        public byte[] With(IReadOnlyDictionary<int, byte[]> replaced, out string whynot)
        {
            whynot = null;
            if (replaced == null || replaced.Count == 0) return (byte[])Bytes.Clone();

            foreach (KeyValuePair<int, byte[]> kv in replaced)
            {
                if (kv.Key < 0 || kv.Key >= _shapes.Count)
                    { whynot = $"No shape {kv.Key}."; return null; }
                if (kv.Value == null || kv.Value.Length == 0)
                    { whynot = $"Shape {kv.Key}: empty display list."; return null; }
                if (kv.Value.Length % 4 != 0)
                    { whynot = $"Shape {kv.Key}: {kv.Value.Length} bytes is not word aligned."; return null; }
            }

            int lastRecordEnd = 0;
            foreach (Shape s in _shapes) lastRecordEnd = Math.Max(lastRecordEnd, s.RecordAt + 16);
            int firstDl = int.MaxValue;
            foreach (Shape s in _shapes) firstDl = Math.Min(firstDl, s.DisplayListAt);
            if (_shapes.Count > 0 && firstDl < lastRecordEnd)
                { whynot = "Unsupported layout: display list before shape record."; return null; }

            List<int> order = new List<int>();
            for (int i = 0; i < _shapes.Count; i++) order.Add(i);
            order.Sort((a, b) => _shapes[a].DisplayListAt.CompareTo(_shapes[b].DisplayListAt));

            List<byte[]> taken = new List<byte[]>();
            foreach (int at in order)
                taken.Add(replaced.TryGetValue(at, out byte[] made)
                    ? made
                    : Slice(Bytes, _shapes[at].DisplayListAt, _shapes[at].DisplayListSize));

            int region = order.Count == 0 ? Bytes.Length : _shapes[order[0]].DisplayListAt;
            int wasEnd = 0;
            foreach (int at in order)
                wasEnd = Math.Max(wasEnd, _shapes[at].DisplayListAt + _shapes[at].DisplayListSize);

            int nowEnd = region;
            foreach (byte[] b in taken) nowEnd += b.Length;
            int delta = nowEnd - wasEnd;

            byte[] outBytes = new byte[Bytes.Length + delta];
            Array.Copy(Bytes, 0, outBytes, 0, region);

            int write = region;
            int[] placed = new int[order.Count];
            for (int i = 0; i < order.Count; i++)
            {
                placed[i] = write;
                Array.Copy(taken[i], 0, outBytes, write, taken[i].Length);
                write += taken[i].Length;
            }

            if (wasEnd < Bytes.Length)
                Array.Copy(Bytes, wasEnd, outBytes, nowEnd, Bytes.Length - wasEnd);

            for (int i = 0; i < order.Count; i++)
            {
                Shape s = _shapes[order[i]];
                Put32(outBytes, s.RecordAt + 8, (uint)(placed[i] - s.RecordAt));
                Put32(outBytes, s.RecordAt + 12, (uint)taken[i].Length);
            }

            if (delta != 0)
            {
                Bump(outBytes, ModelAt + 0, delta);
                Bump(outBytes, ModelAt + 16, delta);
                Bump(outBytes, _mdl0At + 4, delta);
                Put32(outBytes, 8, (uint)outBytes.Length);

                for (int i = 0; i < _blockCount; i++)
                {
                    int at = 0x10 + i * 4;
                    if ((int)U32(outBytes, at) > _mdl0At) Bump(outBytes, at, delta);
                }
            }

            return outBytes;
        }

        private static void ReadNames(byte[] bytes, int texPal, int at, List<Named> into, out string whichIsNull)
        {
            whichIsNull = null;
            if (at < 0 || at + 2 > bytes.Length) return;

            int count = bytes[at + 1];
            int entries = at + 16 + count * 4;
            int names = entries + count * 4;
            if (names + count * 16 > bytes.Length) return;

            for (int i = 0; i < count; i++)
            {
                Named named = new Named { NameAt = names + i * 16 };
                named.Name = Name(bytes, named.NameAt);

                uint flags = U32(bytes, entries + i * 4);
                int pairs = (int)((flags >> 16) & 0xf);
                int list = texPal + (int)(flags & 0xffff);
                for (int k = 0; k < pairs && list + k < bytes.Length; k++)
                    named.Materials.Add(bytes[list + k]);

                into.Add(named);
            }
        }

        public byte[] WithNames(IReadOnlyDictionary<int, string> pictures,
                                IReadOnlyDictionary<int, string> colours, out string whynot)
        {
            byte[] made = (byte[])Bytes.Clone();
            return ApplyNamesTo(made, pictures, colours, out whynot) ? made : null;
        }

        public bool ApplyNamesTo(byte[] into, IReadOnlyDictionary<int, string> pictures,
                                 IReadOnlyDictionary<int, string> colours, out string whynot)
        {
            whynot = null;
            if (into == null) { whynot = "No model data."; return false; }
            if (!Rename(into, _pictures, pictures, "picture", ref whynot)) return false;
            if (!Rename(into, _colours, colours, "colour set", ref whynot)) return false;
            return true;
        }

        private static bool Rename(byte[] into, List<Named> have,
                                   IReadOnlyDictionary<int, string> want, string what, ref string whynot)
        {
            if (want == null) return true;
            foreach (KeyValuePair<int, string> kv in want)
            {
                if (kv.Key < 0 || kv.Key >= have.Count)
                { whynot = $"No {what} {kv.Key}."; return false; }

                byte[] raw = Encoding.ASCII.GetBytes(kv.Value ?? "");
                if (raw.Length > 16)
                { whynot = $"\"{kv.Value}\" is {raw.Length} letters and a name holds sixteen."; return false; }

                int at = have[kv.Key].NameAt;
                for (int i = 0; i < 16; i++) into[at + i] = i < raw.Length ? raw[i] : (byte)0;
            }
            return true;
        }

        public byte[] DisplayList(int shape)
            => Slice(Bytes, _shapes[shape].DisplayListAt, _shapes[shape].DisplayListSize);

        private static byte[] Slice(byte[] from, int at, int count)
        {
            byte[] b = new byte[count];
            Array.Copy(from, at, b, 0, count);
            return b;
        }

        private static string Name(byte[] b, int at)
        {
            int n = 0;
            while (n < 16 && b[at + n] != 0) n++;
            return Encoding.ASCII.GetString(b, at, n);
        }

        private static uint U32(byte[] b, int at) => BitConverter.ToUInt32(b, at);
        private static ushort U16(byte[] b, int at) => BitConverter.ToUInt16(b, at);
        private static void Put32(byte[] b, int at, uint v) => BitConverter.GetBytes(v).CopyTo(b, at);
        private static void Bump(byte[] b, int at, int by) => Put32(b, at, (uint)((int)U32(b, at) + by));
    }
}

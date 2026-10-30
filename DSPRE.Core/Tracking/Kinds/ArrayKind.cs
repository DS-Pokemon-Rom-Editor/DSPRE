using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using static DSPRE.RomInfo;

namespace DSPRE.Tracking.Kinds
{
    /// <summary>
    /// How one archive member is laid out: values of one size, either a fixed table or a list with an end marker. With
    /// <see cref="Fields"/>, each <see cref="Size"/> bytes are a row of named fields.
    /// </summary>
    public sealed record ArrayShape(string Label, int Size, bool Signed = false, int? End = null, Func<TrackingNames, Func<int, string>> Values = null,
        Func<int, int, string> Key = null, int Species = -1, IReadOnlyList<ArrayField> Fields = null, bool Insertable = false)
    {
        public static readonly ArrayShape Raw = new ArrayShape(null, 0);
    }

    /// <summary>A named value inside a row; a negative size is that many raw bytes, and <see cref="Bits"/> narrows it to bits from <see cref="Shift"/>.</summary>
    public sealed record ArrayField(string Name, int Offset, int Size, bool Signed = false, Func<TrackingNames, Func<int, string>> Values = null,
        double Scale = 1, int Decimals = 0, int Shift = 0, int Bits = 0, Func<TrackingNames, IReadOnlyList<string>> Flags = null);

    /// <summary>
    /// A data archive whose members are plain arrays: each value is a field keyed by its index, so two edits to
    /// different entries merge. Lists with an end marker are kept whole.
    /// </summary>
    public sealed class ArrayKind : FieldKind
    {
        private readonly DirNames _dir;
        private readonly string _title;
        private readonly Func<int, ArrayShape> _shape;
        private readonly DirNames? _derivedDir;
        private readonly Func<byte[], byte[], byte[]> _derive;
        private readonly (DirNames Dir, string Key)? _twin;

        /// <param name="derivedDir">An archive whose same-numbered member is worked out from this one, like an index into
        /// it: it belongs to the same record, isn't diffed, and is rebuilt by <paramref name="derive"/> (rows, old) on apply.</param>
        /// <param name="twin">A second copy of the archive the game reads instead in some cases: one record covers both, and
        /// only the values where the copy differs are written, under the key given.</param>
        public ArrayKind(string name, string folder, DirNames dir, string title, Func<int, ArrayShape> shape,
            DirNames? derivedDir = null, Func<byte[], byte[], byte[]> derive = null, (DirNames Dir, string Key)? twin = null)
        {
            _twin = twin;
            _derivedDir = derivedDir;
            _derive = derive;
            Name = name;
            Folder = folder;
            _dir = dir;
            _title = title;
            _shape = shape;
        }

        public override string Name { get; }
        public override string Folder { get; }

        public override string Title(string id, ChangeContext ctx)
        {
            string label = Label(Number(id), ctx);
            return _title + " " + id + (string.IsNullOrEmpty(label) ? "" : " " + label);
        }

        protected override string SlugName(string id, ChangeContext ctx) => Label(Number(id), ctx) is string s && s.Length > 0 ? s : null;

        /// <summary>The Pokémon a record belongs to, or -1 when it isn't one Pokémon's.</summary>
        public int SpeciesOf(string id) => _shape(Number(id)).Species;

        /// <summary>The record's own name, like "routes and towns, day", without its Pokémon.</summary>
        public string PartOf(string id) => _shape(Number(id)).Label;

        private string Label(int member, ChangeContext ctx)
        {
            ArrayShape shape = _shape(member);
            string species = shape.Species >= 0 ? ctx.Names.Species(shape.Species) : null;
            return string.Join(" ", new[] { species, shape.Label }.Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        public override bool Owns(TrackedFile file, out string id)
        {
            id = file.TryMember(out DirNames dir, out int member) && (dir == _dir || dir == _derivedDir || dir == _twin?.Dir) ? Id(member) : null;
            return id != null;
        }

        public override IReadOnlyList<TrackedFile> Files(string id, ChangeContext ctx)
        {
            List<TrackedFile> files = new List<TrackedFile> { TrackedFile.Member(_dir, Number(id)) };
            if (_derivedDir is DirNames derived) files.Add(TrackedFile.Member(derived, Number(id)));
            if (_twin is (DirNames twin, _)) files.Add(TrackedFile.Member(twin, Number(id)));
            return files;
        }

        // A list that can grow is stored as row hunks, so one inserted row isn't a change to every row after it.
        public override RecordFiles Export(string id, ChangeContext ctx)
        {
            int member = Number(id);
            ArrayShape shape = _shape(member);
            if (!shape.Insertable) return base.Export(id, ctx);
            TrackedFile file = Files(id, ctx)[0];
            byte[] was = ctx.Base.Read(file), now = ctx.Project.Read(file);
            if (Same(was, now)) return null;
            if (was == null || now == null || was.Length % shape.Size != 0 || now.Length % shape.Size != 0) return base.Export(id, ctx);
            List<string> from = Rows(member, was, ctx), to = Rows(member, now, ctx);
            if (!Same(Build(member, to, ctx), now)) return base.Export(id, ctx);
            JsonObject json = Envelope(id, Title(id, ctx), Files(id, ctx), "edit");
            json["changes"] = new JsonObject { ["rows"] = ObjectDiff.AsEntries(LineDiff.Hunks(from, to)) };
            return new RecordFiles { Json = json };
        }

        public override Dictionary<TrackedFile, byte[]> Apply(string id, RecordFiles change, Func<TrackedFile, byte[]> start, ChangeContext ctx, Func<Conflict, bool> resolve)
        {
            if (change.Json["changes"]?["rows"] is not JsonArray hunks) return base.Apply(id, change, start, ctx, resolve);
            int member = Number(id);
            TrackedFile file = Files(id, ctx)[0];
            List<string> rows = Rows(member, start(file) ?? Array.Empty<byte>(), ctx);
            List<string> patched = LineDiff.Apply(rows, ObjectDiff.AsLines(hunks), Title(id, ctx), resolve, ObjectDiff.Canonical);
            byte[] built = Build(member, patched, ctx);
            Dictionary<TrackedFile, byte[]> result = new Dictionary<TrackedFile, byte[]> { [file] = built };
            if (_derivedDir is DirNames derived)
            {
                TrackedFile other = TrackedFile.Member(derived, member);
                result[other] = _derive(built, start(other) ?? Array.Empty<byte>());
            }
            return result;
        }

        private List<string> Rows(int member, byte[] bytes, ChangeContext ctx) =>
            ((JsonObject)Fields.Write(f => Bind(f, member, bytes, ctx))["values"]).Select(r => r.Value.ToJsonString()).ToList();

        private byte[] Build(int member, IReadOnlyList<string> rows, ChangeContext ctx)
        {
            byte[] bytes = new byte[rows.Count * _shape(member).Size];
            JsonObject values = new JsonObject();
            for (int i = 0; i < rows.Count; i++) values[i.ToString(CultureInfo.InvariantCulture)] = JsonNode.Parse(rows[i]);
            Fields.Read(new JsonObject { ["values"] = values }, f => Bind(f, member, bytes, ctx));
            return bytes;
        }

        protected override JsonObject Decode(string id, Func<TrackedFile, byte[]> read, ChangeContext ctx)
        {
            byte[] bytes = read(Files(id, ctx)[0]);
            if (bytes == null) return null;
            JsonObject json = Fields.Write(f => Bind(f, Number(id), bytes, ctx));
            if (_twin is (DirNames twin, string key) && read(TrackedFile.Member(twin, Number(id))) is byte[] copy)
            {
                JsonNode mine = json["values"], theirs = Fields.Write(f => Bind(f, Number(id), copy, ctx))["values"];
                if (mine is JsonObject main && theirs is JsonObject other)
                {
                    JsonObject differs = new JsonObject();
                    foreach (KeyValuePair<string, JsonNode> v in other)
                        if (!main.TryGetPropertyValue(v.Key, out JsonNode same) || !JsonNode.DeepEquals(same, v.Value)) differs[v.Key] = v.Value?.DeepClone();
                    if (differs.Count > 0 || other.Count != main.Count) json[key] = differs;
                    if (other.Count < main.Count) json[key + "_length"] = other.Count;
                }
                else if (!JsonNode.DeepEquals(mine, theirs)) json[key] = theirs?.DeepClone();
            }
            return json;
        }

        protected override Dictionary<TrackedFile, byte[]> Encode(string id, JsonObject record, Func<TrackedFile, byte[]> start, ChangeContext ctx)
        {
            Dictionary<TrackedFile, byte[]> result = EncodeOne(id, Files(id, ctx)[0], record, start, ctx);
            if (_twin is (DirNames twin, string key))
            {
                // The copy is the main values with its own differences laid over them.
                JsonObject copy = (JsonObject)record.DeepClone();
                copy.Remove(key);
                copy.Remove(key + "_length");
                if (record[key] is JsonObject differs && copy["values"] is JsonObject values)
                {
                    foreach (KeyValuePair<string, JsonNode> v in differs) values[v.Key] = v.Value?.DeepClone();
                    if (record[key + "_length"]?.GetValue<int>() is int length)
                        foreach (string extra in values.Select(v => v.Key).Where(k => int.TryParse(k, NumberStyles.None, CultureInfo.InvariantCulture, out int i) && i >= length).ToList())
                            values.Remove(extra);
                }
                else if (record[key] is JsonNode whole) copy["values"] = whole.DeepClone();
                TrackedFile other = TrackedFile.Member(twin, Number(id));
                if (start(other) != null)
                    foreach (KeyValuePair<TrackedFile, byte[]> w in EncodeOne(id, other, copy, start, ctx)) result[w.Key] = w.Value;
            }
            return result;
        }

        private Dictionary<TrackedFile, byte[]> EncodeOne(string id, TrackedFile file, JsonObject record, Func<TrackedFile, byte[]> start, ChangeContext ctx)
        {
            ArrayShape shape = _shape(Number(id));
            byte[] bytes = (byte[])(start(file) ?? Array.Empty<byte>()).Clone();
            if (shape.End != null || shape.Size == 0)
            {
                byte[] built = null;
                Fields.Read(record, f => Bind(f, Number(id), bytes, ctx, b => built = b));
                return new Dictionary<TrackedFile, byte[]> { [file] = built ?? bytes };
            }
            JsonObject values = record["values"] as JsonObject;
            int needed = values == null || shape.Key != null ? 0
                : values.Select(v => int.TryParse(v.Key, NumberStyles.None, CultureInfo.InvariantCulture, out int i) ? i + 1 : 0).DefaultIfEmpty(0).Max() * shape.Size;
            if (needed > bytes.Length) Array.Resize(ref bytes, needed);
            Fields.Read(record, f => Bind(f, Number(id), bytes, ctx));
            return new Dictionary<TrackedFile, byte[]> { [file] = bytes };
        }

        private void Bind(Fields f, int member, byte[] bytes, ChangeContext ctx, Action<byte[]> rebuilt = null)
        {
            ArrayShape shape = _shape(member);
            if (shape.Size == 0)
            {
                f.Bytes("content", () => bytes, b => rebuilt?.Invoke(b));
                return;
            }
            Func<int, string> name = shape.Values?.Invoke(ctx.Names);
            if (shape.End != null)
            {
                List<int> list = new List<int>();
                for (int at = 0; at + shape.Size <= bytes.Length; at += shape.Size)
                {
                    int v = Get(bytes, at, shape);
                    if (v == shape.End) break;
                    list.Add(v);
                }
                Action<int[]> set = items => rebuilt?.Invoke(items.Append(shape.End.Value).SelectMany(v => Put(v, shape.Size)).ToArray());
                if (name != null) f.Refs("values", () => list, set, name);
                else f.Ints("values", () => list, set);
                return;
            }
            f.Object("values", o =>
            {
                for (int i = 0; i * shape.Size + shape.Size <= bytes.Length; i++)
                {
                    int at = i * shape.Size;
                    string key = shape.Key?.Invoke(bytes.Length / shape.Size, i) ?? i.ToString(CultureInfo.InvariantCulture);
                    if (shape.Fields != null)
                    {
                        o.Object(key, row => BindRow(row, bytes, at, shape.Fields, ctx.Names));
                    }
                    else if (name != null) o.Ref(key, () => Get(bytes, at, shape), v => Put(v, shape.Size).CopyTo(bytes, at), name);
                    else o.Int(key, () => Get(bytes, at, shape), v => Put(v, shape.Size).CopyTo(bytes, at));
                }
            });
        }

        /// <summary>The named fields of the row at <paramref name="at"/>.</summary>
        internal static void BindRow(Fields row, byte[] bytes, int at, IReadOnlyList<ArrayField> fields, TrackingNames names)
        {
            foreach (ArrayField field in fields)
            {
                int fieldAt = at + field.Offset;
                if (field.Size < 0)
                {
                    int length = -field.Size;
                    row.Bytes(field.Name, () => bytes.AsSpan(fieldAt, length).ToArray(), b =>
                    {
                        if (b.Length != length) throw new InvalidOperationException($"{field.Name} must be {length} bytes.");
                        b.CopyTo(bytes, fieldAt);
                    });
                    continue;
                }
                if (field.Flags != null)
                {
                    // A bit mask of up to eight bytes, written as the names of the bits set.
                    int bytesLong = field.Size;
                    row.Flags(field.Name, () =>
                    {
                        long v = 0;
                        for (int b = bytesLong - 1; b >= 0; b--) v = v << 8 | bytes[fieldAt + b];
                        return v;
                    }, v =>
                    {
                        for (int b = 0; b < bytesLong; b++) bytes[fieldAt + b] = (byte)(v >> (8 * b));
                    }, field.Flags(names), numbered: true);
                    continue;
                }
                ArrayShape one = new ArrayShape(null, field.Size, field.Signed);
                Func<int, string> fieldName = field.Values?.Invoke(names);
                int mask = field.Bits is > 0 and < 32 ? (1 << field.Bits) - 1 : -1;
                int Read() => mask == -1 ? Get(bytes, fieldAt, one) : (int)((uint)Get(bytes, fieldAt, one) >> field.Shift) & mask;
                void Write(int v) => Put(mask == -1 ? v : Get(bytes, fieldAt, one) & ~(mask << field.Shift) | (v & mask) << field.Shift, field.Size).CopyTo(bytes, fieldAt);
                if (field.Scale != 1) row.Scaled(field.Name, () => Read(), v => Write((int)v), field.Scale, field.Decimals);
                else if (fieldName != null) row.Ref(field.Name, Read, Write, fieldName);
                else row.Int(field.Name, Read, Write);
            }
        }

        private static int Get(byte[] b, int at, ArrayShape s) => s.Size switch
        {
            1 => s.Signed ? (sbyte)b[at] : b[at],
            2 => s.Signed ? BitConverter.ToInt16(b, at) : BitConverter.ToUInt16(b, at),
            _ => BitConverter.ToInt32(b, at),
        };

        private static byte[] Put(int v, int size) => size switch
        {
            1 => new[] { (byte)v },
            2 => BitConverter.GetBytes((ushort)v),
            _ => BitConverter.GetBytes(v),
        };
    }
}

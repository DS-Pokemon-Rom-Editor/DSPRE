using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace DSPRE.Tracking.Kinds
{
    /// <summary>
    /// A data table inside arm9 or an overlay, as one record of named rows. Its bytes are left out of the code
    /// file's own record.
    /// </summary>
    public sealed class CodeTableKind : FieldKind
    {
        public override string Name => "code-table";
        public override string Folder => "code-tables";

        private static CodeTable TableOf(string id, ChangeContext ctx) => ctx.Project.Table(id) is CodeTable t && t.Fields != null ? t : null;

        public override string Title(string id, ChangeContext ctx) => TableOf(id, ctx)?.Title ?? id;

        public override string Stem(string id, ChangeContext ctx) => id;

        public override bool TryParseStem(string stem, out string id)
        {
            id = string.IsNullOrEmpty(stem) ? null : stem;
            return id != null;
        }

        public override bool Owns(TrackedFile file, out string id)
        {
            id = file.TryRow(out string table, out _) && table != "MapHeaders" && table != "MapNames" ? table : null;
            return id != null;
        }

        public override IReadOnlyList<TrackedFile> Files(string id, ChangeContext ctx) =>
            TableOf(id, ctx) is CodeTable t ? Enumerable.Range(0, t.Count).Select(i => TrackedFile.Row(id, i)).ToArray() : Array.Empty<TrackedFile>();

        // A list in a fixed room is stored as row hunks, so one inserted entry isn't a change to every entry after it.
        public override RecordFiles Export(string id, ChangeContext ctx)
        {
            CodeTable table = TableOf(id, ctx);
            if (table == null || !table.Insertable) return base.Export(id, ctx);
            List<string> from = Rows(table, ctx.Base.Read, ctx), to = Rows(table, ctx.Project.Read, ctx);
            if (from == null || to == null) return base.Export(id, ctx);
            if (from.SequenceEqual(to, StringComparer.Ordinal)) return null;
            JsonObject json = Envelope(id, Title(id, ctx), Files(id, ctx), "edit");
            json["changes"] = new JsonObject { ["rows"] = ObjectDiff.AsEntries(LineDiff.Hunks(from, to)) };
            return new RecordFiles { Json = json };
        }

        public override Dictionary<TrackedFile, byte[]> Apply(string id, RecordFiles change, Func<TrackedFile, byte[]> start, ChangeContext ctx, Func<Conflict, bool> resolve)
        {
            if (change.Json["changes"]?["rows"] is not JsonArray hunks) return base.Apply(id, change, start, ctx, resolve);
            CodeTable table = TableOf(id, ctx) ?? throw new InvalidOperationException($"This project has no {id} table.");
            List<string> rows = Rows(table, start, ctx) ?? throw new InvalidOperationException($"{table.Title} can't be read.");
            List<string> patched = LineDiff.Apply(rows, ObjectDiff.AsLines(hunks), Title(id, ctx), resolve, ObjectDiff.Canonical);
            if (patched.Count > table.Count) throw new InvalidOperationException($"{table.Title} has room for {table.Count} entries.");
            byte[] bytes = new byte[table.RowSize * table.Count];
            for (int i = 0; i < table.Count; i++) start(TrackedFile.Row(id, i))?.CopyTo(bytes, i * table.RowSize);
            JsonObject values = new JsonObject();
            for (int i = 0; i < patched.Count; i++) values[Key(table, i)] = JsonNode.Parse(patched[i]);
            Fields.Read(new JsonObject { ["rows"] = values }, f => Bind(f, table, bytes, ctx, patched.Count));
            Dictionary<TrackedFile, byte[]> result = new Dictionary<TrackedFile, byte[]>();
            for (int i = 0; i < table.Count; i++) result[TrackedFile.Row(id, i)] = bytes.AsSpan(i * table.RowSize, table.RowSize).ToArray();
            return result;
        }

        private List<string> Rows(CodeTable table, Func<TrackedFile, byte[]> read, ChangeContext ctx)
        {
            JsonObject record = Decode(table.Name, read, ctx);
            return (record?["rows"] as JsonObject)?.Select(r => r.Value.ToJsonString()).ToList();
        }

        private static string Key(CodeTable table, int i) => table.RowKey?.Invoke(i) ?? i.ToString(CultureInfo.InvariantCulture);

        protected override JsonObject Decode(string id, Func<TrackedFile, byte[]> read, ChangeContext ctx)
        {
            CodeTable table = TableOf(id, ctx);
            if (table == null) return null;
            byte[] bytes = new byte[table.RowSize * table.Count];
            for (int i = 0; i < table.Count; i++)
            {
                byte[] row = read(TrackedFile.Row(id, i));
                if (row == null) return null;
                row.CopyTo(bytes, i * table.RowSize);
            }
            return Fields.Write(f => Bind(f, table, bytes, ctx));
        }

        protected override Dictionary<TrackedFile, byte[]> Encode(string id, JsonObject record, Func<TrackedFile, byte[]> start, ChangeContext ctx)
        {
            CodeTable table = TableOf(id, ctx) ?? throw new InvalidOperationException($"This project has no {id} table.");
            byte[] bytes = new byte[table.RowSize * table.Count];
            for (int i = 0; i < table.Count; i++)
                (start(TrackedFile.Row(id, i)) ?? throw new InvalidOperationException($"{table.Title} row {i} can't be read.")).CopyTo(bytes, i * table.RowSize);
            Fields.Read(record, f => Bind(f, table, bytes, ctx));
            Dictionary<TrackedFile, byte[]> result = new Dictionary<TrackedFile, byte[]>();
            for (int i = 0; i < table.Count; i++) result[TrackedFile.Row(id, i)] = bytes.AsSpan(i * table.RowSize, table.RowSize).ToArray();
            return result;
        }

        private static void Bind(Fields f, CodeTable table, byte[] bytes, ChangeContext ctx, int count = -1) =>
            f.Object("rows", o =>
            {
                for (int i = 0; i < (count < 0 ? table.Count : count); i++)
                {
                    int at = i * table.RowSize;
                    o.Object(Key(table, i), row => ArrayKind.BindRow(row, bytes, at, table.Fields, ctx.Names));
                }
            });
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Tracking.Kinds
{
    /// <summary>A map header, from the arm9 table or, with the dynamic headers patch, its own archive member.</summary>
    public sealed class HeaderKind : FieldKind
    {
        private const string Table = "MapHeaders", Names = "MapNames";

        public override string Name => "header";
        public override string Folder => "maps/headers";

        public override string Title(string id, ChangeContext ctx)
        {
            string name = HeaderName(id, ctx);
            return "Header " + id + (string.IsNullOrWhiteSpace(name) ? "" : " " + name);
        }

        protected override string SlugName(string id, ChangeContext ctx) => HeaderName(id, ctx);

        private static string HeaderName(string id, ChangeContext ctx)
        {
            byte[] bytes = ctx.Project.Read(File(id, ctx)) ?? ctx.Base.Read(File(id, ctx));
            if (bytes == null || bytes.Length < MapHeader.Length) return null;
            MapHeader header = MapHeader.LoadFromByteArray(bytes, (ushort)Number(id));
            int location = header switch
            {
                HeaderPt pt => pt.LocationName,
                HeaderHGSS hg => hg.LocationName,
                HeaderDP dp => dp.LocationName,
                _ => -1,
            };
            return location < 0 ? null : ctx.Names.Location(location);
        }

        public override bool Owns(TrackedFile file, out string id)
        {
            id = null;
            if (file.TryRow(out string table, out int row) && (table == Table || table == Names)) id = Id(row);
            else if (file.TryMember(out DirNames dir, out int member) && dir == DirNames.DynamicHeaders) id = Id(member);
            return id != null;
        }

        public override IReadOnlyList<TrackedFile> Files(string id, ChangeContext ctx) =>
            ctx.Project.Table(Names) != null ? new[] { File(id, ctx), TrackedFile.Row(Names, Number(id)) } : new[] { File(id, ctx) };

        // The header editor's map settings, bit by bit; the last three are HGSS's Pokégear settings.
        private static IReadOnlyList<string> FlagNames => GameFamily == GameFamilies.HGSS
            ? new[] { "bicycle", "run", "escape_rope", "fly", "outgoing_calls", "incoming_calls", "radio" }
            : new[] { "bicycle", "run", "escape_rope", "fly" };

        private static TrackedFile File(string id, ChangeContext ctx) =>
            ctx.Project.Table(Table) != null ? TrackedFile.Row(Table, Number(id)) : TrackedFile.Member(DirNames.DynamicHeaders, Number(id));

        protected override JsonObject Decode(string id, Func<TrackedFile, byte[]> read, ChangeContext ctx)
        {
            byte[] bytes = read(File(id, ctx));
            if (bytes == null || bytes.Length < MapHeader.Length) return null;
            MapHeader header = MapHeader.LoadFromByteArray(bytes, (ushort)Number(id));
            JsonObject json = ReflectedFields.Write(header, Refs(ctx), Skip);
            if (json["flags"] is JsonValue flags)
                json["flags"] = Fields.Write(f => f.Flags("flags", () => flags.GetValue<long>(), _ => { }, FlagNames))["flags"]?.DeepClone();
            if (ctx.Project.Table(Names) != null && read(TrackedFile.Row(Names, Number(id))) is byte[] name)
                json["internal_name"] = System.Text.Encoding.ASCII.GetString(name).TrimEnd('\0');
            return json;
        }

        protected override Dictionary<TrackedFile, byte[]> Encode(string id, JsonObject record, Func<TrackedFile, byte[]> start, ChangeContext ctx)
        {
            TrackedFile file = File(id, ctx);
            byte[] bytes = start(file) ?? throw new InvalidOperationException("A new header needs a file to build from.");
            MapHeader header = MapHeader.LoadFromByteArray(bytes, (ushort)Number(id));
            if (record["flags"] is JsonArray named)
            {
                long value = 0;
                Fields.Read(new JsonObject { ["flags"] = named.DeepClone() }, f => f.Flags("flags", () => 0, v => value = v, FlagNames));
                record = (JsonObject)record.DeepClone();
                record["flags"] = value;
            }
            ReflectedFields.Read(record, header, Refs(ctx), Skip);
            byte[] built = header.ToByteArray();
            if (built.Length < bytes.Length)
            {
                byte[] padded = (byte[])bytes.Clone();
                built.CopyTo(padded, 0);
                built = padded;
            }
            Dictionary<TrackedFile, byte[]> result = new Dictionary<TrackedFile, byte[]> { [file] = built };
            if (ctx.Project.Table(Names) is CodeTable names && record["internal_name"]?.GetValue<string>() is string internalName)
            {
                byte[] row = new byte[names.RowSize];
                byte[] text = System.Text.Encoding.ASCII.GetBytes(internalName);
                if (text.Length > row.Length) throw new InvalidDataException($"\"{internalName}\" is longer than {row.Length} letters.");
                text.CopyTo(row, 0);
                result[TrackedFile.Row(Names, Number(id))] = row;
            }
            return result;
        }

        private static readonly HashSet<string> Skip = new HashSet<string>(StringComparer.Ordinal) { "ID" };

        private static ReflectedFields.RefLookup Refs(ChangeContext ctx) => member => member == "LocationName" ? ctx.Names.Location : null;
    }
}

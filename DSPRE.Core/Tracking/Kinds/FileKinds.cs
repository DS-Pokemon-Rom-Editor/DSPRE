using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;

namespace DSPRE.Tracking.Kinds
{
    /// <summary>A record whose change is always the whole file, kept next to its JSON.</summary>
    public abstract class WholeFileKind : ChangeKind
    {
        protected virtual string Extension(TrackedFile file) => ".bin";

        public override RecordFiles Export(string id, ChangeContext ctx)
        {
            IReadOnlyList<TrackedFile> files = Files(id, ctx);
            RecordFiles result = new RecordFiles();
            JsonObject changes = new JsonObject();
            if (!RawChanges.Export(changes, result, files, ctx.Base.Read, f => WithoutTables(f, ctx), Extension)) return null;
            JsonObject json = Envelope(id, Title(id, ctx), files, "edit");
            json["changes"] = changes;
            return new RecordFiles { Json = json }.WithSidecars(result);
        }

        /// <summary>The file with every table that has its own record put back to the original bytes.</summary>
        protected static byte[] WithoutTables(TrackedFile file, ChangeContext ctx)
        {
            byte[] now = ctx.Project.Read(file), before = ctx.Base.Read(file);
            if (now == null || before == null) return now;
            byte[] masked = null;
            foreach (CodeTable table in ctx.Project.Tables.Where(t => t.TreePath == file.Key))
            {
                masked ??= (byte[])now.Clone();
                foreach (long i in table.Bytes()) if (i < masked.Length && i < before.Length) masked[i] = before[i];
            }
            return masked ?? now;
        }

        public override Dictionary<TrackedFile, byte[]> Apply(string id, RecordFiles change, Func<TrackedFile, byte[]> start, ChangeContext ctx, Func<Conflict, bool> resolve)
        {
            Dictionary<TrackedFile, byte[]> result = new Dictionary<TrackedFile, byte[]>();
            string title = Title(id, ctx);
            if (change.Json["changes"]?[RawChanges.Key] is JsonObject raw) RawChanges.Apply(raw, change, start, title, c => resolve(c with { Record = title }), result);
            return result;
        }
    }

    /// <summary>ROM files outside the archives, by their path in the project. Text files are line hunks, others whole files.</summary>
    public sealed class TreeFileKind : WholeFileKind
    {
        private static readonly string[] TextExtensions = { ".yaml", ".yml", ".txt", ".json" };

        public override string Name => "file";
        public override string Folder => "rom";
        public override string Title(string id, ChangeContext ctx) => id;
        protected override string Extension(TrackedFile file) => "";

        public override bool Owns(TrackedFile file, out string id)
        {
            id = file.TreePath;
            return id != null;
        }

        public override IReadOnlyList<TrackedFile> Files(string id, ChangeContext ctx) => new[] { TrackedFile.Tree(id) };

        public override string Stem(string id, ChangeContext ctx) => id;

        public override bool TryParseStem(string stem, out string id)
        {
            id = stem;
            return !string.IsNullOrEmpty(stem);
        }

        private static bool IsText(string id) => TextExtensions.Contains(Path.GetExtension(id).ToLowerInvariant());

        public override RecordFiles Export(string id, ChangeContext ctx)
        {
            TrackedFile file = Files(id, ctx)[0];
            byte[] before = ctx.Base.Read(file), now = ctx.Project.Read(file);
            if (!IsText(id) || before == null || now == null || !IsUtf8(before) || !IsUtf8(now)) return base.Export(id, ctx);
            if (before.AsSpan().SequenceEqual(now)) return null;
            string beforeText = Encoding.UTF8.GetString(before), nowText = Encoding.UTF8.GetString(now);
            if (Eol(beforeText) == null || Eol(nowText) == null || !nowText.EndsWith("\n", StringComparison.Ordinal)) return base.Export(id, ctx);
            JsonObject json = Envelope(id, Title(id, ctx), new[] { file }, "edit");
            json["changes"] = new JsonObject { ["hunks"] = LineDiff.Hunks(Lines(beforeText), Lines(nowText)) };
            return new RecordFiles { Json = json };
        }

        public override Dictionary<TrackedFile, byte[]> Apply(string id, RecordFiles change, Func<TrackedFile, byte[]> start, ChangeContext ctx, Func<Conflict, bool> resolve)
        {
            if (change.Json["changes"]?["hunks"] is not JsonArray hunks) return base.Apply(id, change, start, ctx, resolve);
            TrackedFile file = Files(id, ctx)[0];
            byte[] current = start(file) ?? throw new InvalidOperationException($"{id} isn't in this ROM.");
            string text = Encoding.UTF8.GetString(current);
            string eol = Eol(text) ?? "\n";
            List<string> patched = LineDiff.Apply(Lines(text), hunks, Title(id, ctx), resolve);
            return new Dictionary<TrackedFile, byte[]> { [file] = Encoding.UTF8.GetBytes(string.Join(eol, patched) + eol) };
        }

        // Hunks hold lines without their ending, so a file keeps its own line endings when they're applied.
        private static List<string> Lines(string text) => text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n').ToList();

        /// <summary>The file's one line ending, or null when it mixes them or has a lone carriage return.</summary>
        private static string Eol(string text)
        {
            int crlf = text.Split("\r\n").Length - 1, cr = text.Count(c => c == '\r'), lf = text.Count(c => c == '\n');
            if (cr != crlf) return null;
            return crlf == 0 ? "\n" : crlf == lf ? "\r\n" : null;
        }

        private static bool IsUtf8(byte[] bytes)
        {
            try
            {
                new UTF8Encoding(false, true).GetString(bytes);
                return Array.IndexOf(bytes, (byte)0) < 0;
            }
            catch (DecoderFallbackException) { return false; }
        }
    }
}

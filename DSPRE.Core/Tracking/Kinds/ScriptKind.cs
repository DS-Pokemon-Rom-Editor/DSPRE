using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using static DSPRE.RomInfo;

namespace DSPRE.Tracking.Kinds
{
    /// <summary>
    /// A script file as line hunks between rotom's text of the base and of the project. Both sides are decompiled by
    /// the same rotom, so only real changes show; a script that doesn't rebuild exactly is kept whole.
    /// </summary>
    public sealed class ScriptKind : ChangeKind
    {
        private readonly DirNames _dir;
        private readonly string _folder, _title;

        public ScriptKind(DirNames dir = DirNames.Scripts, string folder = "scripts", string title = "Script file")
        {
            _dir = dir;
            _folder = folder;
            _title = title;
        }

        public override string Name => "script";
        public override string Folder => _folder;
        public override string Title(string id, ChangeContext ctx) => _title + " " + id;

        public override bool Owns(TrackedFile file, out string id)
        {
            id = file.TryMember(out DirNames dir, out int member) && dir == _dir ? Id(member) : null;
            return id != null;
        }

        public override IReadOnlyList<TrackedFile> Files(string id, ChangeContext ctx) => new[] { Member(id) };

        private TrackedFile Member(string id) => TrackedFile.Member(_dir, Number(id));

        public override RecordFiles Export(string id, ChangeContext ctx)
        {
            TrackedFile member = Member(id);
            byte[] before = ctx.Base.Read(member), now = ctx.Project.Read(member);
            if (before == null ? now == null : now != null && before.AsSpan().SequenceEqual(now)) return null;

            RecordFiles result = new RecordFiles();
            JsonObject changes = null;
            string op = before == null ? "add" : now == null ? "delete" : "edit";
            string format = null;
            try
            {
                string f1 = null, f2 = null;
                string beforeText = before == null ? "" : Decompile(before, out f1);
                string nowText = now == null ? "" : Decompile(now, out f2);
                format = f2 ?? f1;
                if (now == null || Same(Compile(nowText, format), now))
                    changes = new JsonObject { ["hunks"] = LineDiff.Hunks(Lines(beforeText), Lines(nowText)) };
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is UnauthorizedAccessException)
            {
                AppLogger.Warn($"Track changes: {Title(id, ctx)} is kept whole: {ex.Message}");
            }
            if (changes == null)
            {
                op = "edit";
                changes = new JsonObject();
                if (!RawChanges.Export(changes, result, Files(id, ctx), ctx.Base.Read, ctx.Project.Read, _ => ".bin")) return null;
            }
            JsonObject json = Envelope(id, Title(id, ctx), Files(id, ctx), op);
            if (format != null && changes.ContainsKey("hunks")) json["format"] = format;
            json["changes"] = changes;
            return new RecordFiles { Json = json }.WithSidecars(result);
        }

        public override Dictionary<TrackedFile, byte[]> Apply(string id, RecordFiles change, Func<TrackedFile, byte[]> start, ChangeContext ctx, Func<Conflict, bool> resolve)
        {
            string title = Title(id, ctx);
            Dictionary<TrackedFile, byte[]> result = new Dictionary<TrackedFile, byte[]>();
            JsonObject changes = change.Json["changes"] as JsonObject ?? new JsonObject();
            if (changes[RawChanges.Key] is JsonObject raw)
            {
                RawChanges.Apply(raw, change, start, title, c => resolve(c with { Record = title }), result);
                return result;
            }
            TrackedFile member = Member(id);
            if (change.Json["op"]?.GetValue<string>() == "delete")
            {
                result[member] = null;
                return result;
            }
            string format = change.Json["format"]?.GetValue<string>() ?? "rotom";
            byte[] current = start(member);
            string text = current == null ? "" : Decompile(current, out _);
            List<string> patched = LineDiff.Apply(Lines(text), (JsonArray)changes["hunks"], title, resolve);
            string joined = string.Join("\n", patched);
            result[member] = Compile(joined, format);
            return result;
        }

        private static List<string> Lines(string text) => text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n').ToList();

        /// <summary>rotom's text for a script binary; level scripts come out as JSON.</summary>
        private static string Decompile(byte[] binary, out string format)
        {
            string dir = TempDir();
            try
            {
                string input = Path.Combine(dir, "script"), output = Path.Combine(dir, "script.rotom");
                PlainFiles.WriteBytes(input, binary);
                Run("decompile", "-i", input, "-o", output);
                string text = File.ReadAllText(output, Encoding.UTF8).Replace("\r\n", "\n");
                format = text.TrimStart().StartsWith("{", StringComparison.Ordinal) ? "json" : "rotom";
                return text;
            }
            finally { Directory.Delete(dir, true); }
        }

        private static byte[] Compile(string text, string format)
        {
            string dir = TempDir();
            try
            {
                string input = Path.Combine(dir, "script." + format), output = Path.Combine(dir, "script.bin");
                PlainFiles.WriteText(input, text, new UTF8Encoding(false));
                Run("compile", "-i", input, "-o", output);
                return File.ReadAllBytes(output);
            }
            finally { Directory.Delete(dir, true); }
        }

        private static void Run(params string[] args)
        {
            string database = RotomTool.ProjectDatabaseFile();
            if (database == null || !File.Exists(database)) throw new InvalidOperationException("The project has no rotom command database.");
            List<string> all = new List<string> { args[0], "-d", database };
            all.AddRange(args.Skip(1));
            RotomTool.Result result = RotomTool.RunInAsync(Path.GetTempPath(), all.ToArray()).GetAwaiter().GetResult();
            if (!result.Success) throw new InvalidOperationException("rotom " + args[0] + " failed: " + RotomTool.FormatResult(result));
        }

        private static string TempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "dspre_track_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}

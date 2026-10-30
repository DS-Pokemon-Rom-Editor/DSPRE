using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Tracking.Kinds
{
    /// <summary>
    /// A battle sequence or move animation script, one command a line: its words, then the command name as a comment.
    /// Changes are line hunks; a script that doesn't read back exactly is kept whole.
    /// </summary>
    public sealed class BattleScriptKind : ChangeKind
    {
        private readonly DirNames _dir;
        private readonly string _title;
        private readonly bool _animation;

        public BattleScriptKind(string name, string folder, DirNames dir, string title, bool animation)
        {
            Name = name;
            Folder = folder;
            _dir = dir;
            _title = title;
            _animation = animation;
        }

        public override string Name { get; }
        public override string Folder { get; }

        // Move scripts and move animations are numbered by move.
        public override string Title(string id, ChangeContext ctx) =>
            _dir is DirNames.WazaSeq or DirNames.WazaEffectScripts && ctx?.Names?.Move(Number(id)) is string move ? _title + " " + id + " " + move : _title + " " + id;

        public override bool Owns(TrackedFile file, out string id)
        {
            id = file.TryMember(out DirNames dir, out int member) && dir == _dir ? Id(member) : null;
            return id != null;
        }

        public override IReadOnlyList<TrackedFile> Files(string id, ChangeContext ctx) => new[] { TrackedFile.Member(_dir, Number(id)) };

        private static WazaSeqVersion Version => GameFamily switch
        {
            GameFamilies.HGSS => WazaSeqVersion.HGSS,
            GameFamilies.Plat => WazaSeqVersion.Plat,
            _ => WazaSeqVersion.DP,
        };

        public override RecordFiles Export(string id, ChangeContext ctx)
        {
            TrackedFile file = Files(id, ctx)[0];
            byte[] before = ctx.Base.Read(file), now = ctx.Project.Read(file);
            if (Same(before, now)) return null;
            JsonObject changes = new JsonObject();
            RecordFiles result = new RecordFiles();
            List<string> from = before == null ? new List<string>() : Lines(before), to = now == null ? null : Lines(now);
            if (to != null && Same(Bytes(to), now) && (before == null || Same(Bytes(from), before)))
                changes["hunks"] = LineDiff.Hunks(from, to);
            else if (!RawChanges.Export(changes, result, new[] { file }, ctx.Base.Read, ctx.Project.Read, _ => ".bin")) return null;
            JsonObject json = Envelope(id, Title(id, ctx), new[] { file }, before == null ? "add" : now == null ? "delete" : "edit");
            json["changes"] = changes;
            return new RecordFiles { Json = json }.WithSidecars(result);
        }

        public override Dictionary<TrackedFile, byte[]> Apply(string id, RecordFiles change, Func<TrackedFile, byte[]> start, ChangeContext ctx, Func<Conflict, bool> resolve)
        {
            TrackedFile file = Files(id, ctx)[0];
            Dictionary<TrackedFile, byte[]> result = new Dictionary<TrackedFile, byte[]>();
            string title = Title(id, ctx);
            if (change.Json["changes"]?[RawChanges.Key] is JsonObject raw)
            {
                RawChanges.Apply(raw, change, start, title, c => resolve(c with { Record = title }), result);
                return result;
            }
            if (change.Json["changes"]?["hunks"] is not JsonArray hunks) return result;
            byte[] current = start(file);
            List<string> patched = LineDiff.Apply(current == null ? new List<string>() : Lines(current), hunks, title, resolve, Words);
            result[file] = Bytes(patched);
            return result;
        }

        private List<string> Lines(byte[] bytes)
        {
            List<WazaSeqCommand> commands = _animation ? BattleAnimScript.Parse(bytes, Version) : WazaSeqScript.Parse(bytes, Version);
            List<string> lines = new List<string>();
            foreach (WazaSeqCommand c in commands)
            {
                if (!c.OnlyTail)
                {
                    string words = string.Join(" ", new[] { c.OpId }.Concat(c.Args).Select(w => w.ToString(CultureInfo.InvariantCulture)));
                    string name = c.Raw ? "not a known command" : _animation ? BattleAnimCommands.Name(Version, c.OpId) : WazaSeqOpcodes.Name(Version, c.OpId);
                    lines.Add(name == null ? words : words + "  // " + name);
                }
                if (c.Tail != null) lines.Add("tail " + ChangesJson.Hex(c.Tail));
            }
            return lines;
        }

        // A line's words, without its comment: what two lines are compared by when hunks are applied.
        private static string Words(string line)
        {
            int comment = line.IndexOf("//", StringComparison.Ordinal);
            return (comment < 0 ? line : line.Substring(0, comment)).Trim();
        }

        private static byte[] Bytes(IEnumerable<string> lines)
        {
            List<byte> bytes = new List<byte>();
            foreach (string line in lines.Select(Words).Where(l => l.Length > 0))
            {
                if (line.StartsWith("tail ", StringComparison.Ordinal)) bytes.AddRange(ChangesJson.FromHex(line.Substring(5)));
                else
                    foreach (string word in line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        bytes.AddRange(BitConverter.GetBytes(int.Parse(word, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture)));
            }
            return bytes.ToArray();
        }
    }
}

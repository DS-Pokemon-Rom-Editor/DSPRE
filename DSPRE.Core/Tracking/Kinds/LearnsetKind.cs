using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Tracking.Kinds
{
    /// <summary>A species' level-up moves, in the order the game lists them.</summary>
    public sealed class LearnsetKind : FieldKind
    {
        public override string Name => "learnset";
        public override string Folder => "pokemon/learnsets";

        public override string Title(string id, ChangeContext ctx)
        {
            string name = ctx.Names.Species(Number(id));
            return "Learnset " + id + (string.IsNullOrWhiteSpace(name) ? "" : " " + name);
        }

        protected override string SlugName(string id, ChangeContext ctx) => ctx.Names.Species(Number(id));

        public override bool Owns(TrackedFile file, out string id)
        {
            id = file.TryMember(out DirNames dir, out int member) && dir == DirNames.Learnsets ? Id(member) : null;
            return id != null;
        }

        public override IReadOnlyList<TrackedFile> Files(string id, ChangeContext ctx) => new[] { TrackedFile.Member(DirNames.Learnsets, Number(id)) };

        protected override JsonObject Decode(string id, Func<TrackedFile, byte[]> read, ChangeContext ctx)
        {
            byte[] bytes = read(Files(id, ctx)[0]);
            if (bytes == null) return null;
            LearnsetData data = new LearnsetData(new MemoryStream(bytes));
            List<(byte level, ushort move)> moves = new List<(byte, ushort)>();
            foreach ((byte level, ushort move) entry in data.List) moves.Add(entry);
            return Fields.Write(f => Bind(f, moves, ctx));
        }

        protected override Dictionary<TrackedFile, byte[]> Encode(string id, JsonObject record, Func<TrackedFile, byte[]> start, ChangeContext ctx)
        {
            TrackedFile file = Files(id, ctx)[0];
            byte[] bytes = start(file) ?? throw new InvalidOperationException("A new learnset needs a file to build from.");
            LearnsetData data = new LearnsetData(new MemoryStream(bytes));
            List<(byte level, ushort move)> moves = new List<(byte, ushort)>();
            Fields.Read(record, f => Bind(f, moves, ctx));
            data.List.Clear();
            foreach ((byte level, ushort move) entry in moves) data.List.Add(entry);
            return new Dictionary<TrackedFile, byte[]> { [file] = data.ToByteArray() };
        }

        private sealed class Entry
        {
            public int Level;
            public int Move;
        }

        private static void Bind(Fields f, List<(byte level, ushort move)> moves, ChangeContext ctx)
        {
            List<Entry> entries = moves.Select(m => new Entry { Level = m.level, Move = m.move }).ToList();
            f.List("moves", entries, () => new Entry(), (e, entry) =>
            {
                e.Int("level", () => entry.Level, v => entry.Level = v);
                e.Ref("move", () => entry.Move, v => entry.Move = v, ctx.Names.Move);
            });
            if (!f.Reading) return;
            moves.Clear();
            moves.AddRange(entries.Select(e => ((byte)e.Level, (ushort)e.Move)));
        }
    }
}

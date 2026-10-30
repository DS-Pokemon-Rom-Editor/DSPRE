using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Tracking.Kinds
{
    /// <summary>A Pokémon's Pokéathlon stats and the sprite placement the Pokéathlon reads.</summary>
    public sealed class PokeathlonKind : FieldKind
    {
        public override string Name => "pokeathlon";
        public override string Folder => "pokemon/pokeathlon";

        public override string Title(string id, ChangeContext ctx)
        {
            string name = ctx.Names.Species(Number(id));
            return "Pokéathlon " + id + (string.IsNullOrWhiteSpace(name) ? "" : " " + name);
        }

        protected override string SlugName(string id, ChangeContext ctx) => ctx.Names.Species(Number(id));

        public override bool Owns(TrackedFile file, out string id)
        {
            id = file.TryMember(out DirNames dir, out int member) && dir == DirNames.PokeathlonPerformance ? Id(member) : null;
            return id != null;
        }

        public override IReadOnlyList<TrackedFile> Files(string id, ChangeContext ctx) => new[] { TrackedFile.Member(DirNames.PokeathlonPerformance, Number(id)) };

        protected override JsonObject Decode(string id, Func<TrackedFile, byte[]> read, ChangeContext ctx)
        {
            byte[] bytes = read(Files(id, ctx)[0]);
            if (bytes == null || bytes.Length < PokeathlonPerformance.RecordSize) return null;
            PokeathlonPerformance p = new PokeathlonPerformance(Number(id), bytes);
            return Fields.Write(f => Bind(f, p, bytes));
        }

        protected override Dictionary<TrackedFile, byte[]> Encode(string id, JsonObject record, Func<TrackedFile, byte[]> start, ChangeContext ctx)
        {
            TrackedFile file = Files(id, ctx)[0];
            byte[] bytes = (byte[])(start(file) ?? throw new InvalidOperationException("A new record needs a file to build from.")).Clone();
            PokeathlonPerformance p = new PokeathlonPerformance(Number(id), bytes);
            Fields.Read(record, f => Bind(f, p, bytes));
            byte[] built = p.ToBytes();
            for (int i = PokeathlonPerformance.RecordSize - 1; i < built.Length && i < bytes.Length; i++) built[i] = bytes[i];
            return new Dictionary<TrackedFile, byte[]> { [file] = built };
        }

        private static void Bind(Fields f, PokeathlonPerformance p, byte[] bytes)
        {
            f.Object("stats", s =>
            {
                for (int stat = 0; stat < PokeathlonPerformance.StatCount; stat++)
                {
                    int i = stat;
                    s.Object(PokeathlonPerformance.Stats[i].ToLowerInvariant(), o =>
                    {
                        o.Int("base", () => p.Base(i), v => p.SetBase(i, (byte)v));
                        o.Int("min", () => p.Min(i), v => p.SetMin(i, (byte)v));
                        o.Int("max", () => p.Max(i), v => p.SetMax(i, (byte)v));
                    });
                }
            });
            f.Int("raised", () => p.Raised, v => p.Raised = (byte)v);
            f.Int("hitbox", () => p.Hitbox, v => p.Hitbox = (byte)v);
            f.Int("lift", () => p.Lift, v => p.Lift = (byte)v);
            f.Int("offset", () => p.Offset, v => p.Offset = (byte)v);
            f.Int("unknown_0x13", () => bytes[PokeathlonPerformance.RecordSize - 1], v => bytes[PokeathlonPerformance.RecordSize - 1] = (byte)v);
        }
    }
}

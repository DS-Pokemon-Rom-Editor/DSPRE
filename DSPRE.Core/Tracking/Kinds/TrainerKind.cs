using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Tracking.Kinds
{
    /// <summary>A trainer: its properties and its party, as one record.</summary>
    public sealed class TrainerKind : FieldKind
    {
        private const int MaxParty = 6;
        private static readonly string[] AiFlags =
        {
            "Basic", "Evaluate attack", "Expert", "Setup", "Risky",
            "Prioritize extremes", "Baton Pass", "Tag strategy", "Check HP", "Weather", "Harassment",
        };

        public override string Name => "trainer";
        public override string Folder => "trainers";

        public override string Title(string id, ChangeContext ctx)
        {
            string name = ctx.Names.Trainer(Number(id));
            return "Trainer " + id + (string.IsNullOrWhiteSpace(name) ? "" : " " + name);
        }

        protected override string SlugName(string id, ChangeContext ctx)
        {
            int number = Number(id);
            return ctx.Names.TrainerClass(ClassOf(number, ctx)) + " " + ctx.Names.Trainer(number);
        }

        private static int ClassOf(int id, ChangeContext ctx)
        {
            byte[] props = ctx.Project.Read(TrackedFile.Member(DirNames.TrainerProperties, id));
            return props != null && props.Length > 1 ? props[1] : 0;
        }

        public override bool Owns(TrackedFile file, out string id)
        {
            id = file.TryMember(out DirNames dir, out int member) && (dir == DirNames.TrainerProperties || dir == DirNames.TrainerParty) ? Id(member) : null;
            return id != null;
        }

        public override IReadOnlyList<TrackedFile> Files(string id, ChangeContext ctx) =>
            new[] { TrackedFile.Member(DirNames.TrainerProperties, Number(id)), TrackedFile.Member(DirNames.TrainerParty, Number(id)) };

        protected override JsonObject Decode(string id, Func<TrackedFile, byte[]> read, ChangeContext ctx)
        {
            byte[] props = read(TrackedFile.Member(DirNames.TrainerProperties, Number(id)));
            byte[] party = read(TrackedFile.Member(DirNames.TrainerParty, Number(id)));
            if (props == null || party == null) return null;
            (TrainerProperties trp, Party p) = Load(id, props, party);
            return Fields.Write(f => Bind(f, trp, p, ctx));
        }

        protected override Dictionary<TrackedFile, byte[]> Encode(string id, JsonObject record, Func<TrackedFile, byte[]> start, ChangeContext ctx)
        {
            TrackedFile propsFile = TrackedFile.Member(DirNames.TrainerProperties, Number(id)), partyFile = TrackedFile.Member(DirNames.TrainerParty, Number(id));
            byte[] props = start(propsFile), party = start(partyFile);
            if (props == null || party == null) throw new InvalidOperationException("A new trainer needs files to build from.");
            (TrainerProperties trp, Party p) = Load(id, props, party);
            Fields.Read(record, f => Bind(f, trp, p, ctx));
            return new Dictionary<TrackedFile, byte[]> { [propsFile] = trp.ToByteArray(), [partyFile] = p.ToByteArray() };
        }

        private static (TrainerProperties, Party) Load(string id, byte[] props, byte[] party)
        {
            TrainerProperties trp = new TrainerProperties((ushort)Number(id), new MemoryStream(props));
            Party p = new Party(false, MaxParty, new MemoryStream(party), trp);
            return (trp, p);
        }

        private static void Bind(Fields f, TrainerProperties trp, Party party, ChangeContext ctx)
        {
            f.Ref("class", () => trp.TrainerClass, v => trp.TrainerClass = (byte)v, ctx.Names.TrainerClass);
            f.Bool("choose_moves", () => trp.ChooseMoves, v => trp.ChooseMoves = v);
            f.Bool("choose_items", () => trp.ChooseItems, v => trp.ChooseItems = v);
            f.Refs("items", () => trp.TrainerItems.Select(i => (int)i).ToList(), v => trp.TrainerItems = v.Select(i => (ushort)i).ToArray(), ctx.Names.Item);
            f.Flags("ai", () => AiBits(trp.AI), v => trp.AI = new BitArray(BitConverter.GetBytes((uint)v)), AiFlags);
            f.Long("battle_type", () => trp.BattleType, v => trp.BattleType = (uint)v);
            f.Int("unknown_0x02", () => trp.TrDataUnknown, v => trp.TrDataUnknown = (byte)v);

            List<PartyPokemon> mons = Enumerable.Range(0, Math.Min((int)trp.PartyCount, MaxParty))
                .Select(i => party[i] ?? new PartyPokemon(trp.ChooseItems, trp.ChooseMoves)).ToList();
            f.List("party", mons, () => new PartyPokemon(trp.ChooseItems, trp.ChooseMoves), (m, mon) => BindMon(m, mon, trp, ctx));
            if (!f.Reading) return;
            if (mons.Count > MaxParty) throw new InvalidDataException("A party holds at most six Pokémon.");
            trp.PartyCount = (byte)mons.Count;
            for (int i = 0; i < MaxParty; i++) party[i] = i < mons.Count ? mons[i] : null;
        }

        private static void BindMon(Fields f, PartyPokemon mon, TrainerProperties trp, ChangeContext ctx)
        {
            f.Ref("species", () => mon.PokeID ?? 0, v => mon.PokeID = (ushort)v, ctx.Names.Species);
            f.Int("form", () => mon.FormID, v => mon.FormID = (ushort)v);
            f.Int("level", () => mon.Level, v => mon.Level = (ushort)v);
            f.Int("difficulty", () => mon.Difficulty, v => mon.Difficulty = (byte)v);
            f.Flags("gender_and_ability", () => (long)mon.GenderAndAbility, v => mon.GenderAndAbility = (PartyPokemon.GenderAndAbilityFlags)v,
                new[] { "Male", "Female", "", "", "Ability 1", "Ability 2", "Shiny" });
            f.Int("ball_seals", () => mon.BallSeals, v => mon.BallSeals = (ushort)v);
            if (!f.Reading || f.Json.ContainsKey("item"))
            {
                if (f.Reading || mon.HeldItem != null) f.Ref("item", () => mon.HeldItem ?? 0, v => mon.HeldItem = (ushort)v, ctx.Names.Item);
            }
            if (!f.Reading || f.Json.ContainsKey("moves"))
            {
                if (f.Reading || mon.Moves != null)
                    f.Refs("moves", () => (mon.Moves ?? new ushort[4]).Select(m => (int)m).ToList(), v => mon.Moves = v.Select(m => (ushort)m).ToArray(), ctx.Names.Move);
            }
            if (f.Reading)
            {
                if (trp.ChooseItems) mon.HeldItem ??= 0;
                else mon.HeldItem = null;
                if (trp.ChooseMoves) mon.Moves ??= new ushort[4];
                else mon.Moves = null;
            }
        }

        private static long AiBits(BitArray ai)
        {
            long bits = 0;
            for (int i = 0; i < ai.Length && i < 32; i++)
                if (ai[i]) bits |= 1L << i;
            return bits;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;

namespace DSPRE.Csv
{
    /// <summary>Personal data as one row per Pokémon. Names are the ones the editor's lists show.</summary>
    public sealed class PersonalDataCsv : CsvImporter
    {
        /// <summary>An accepted row's value.</summary>
        public sealed class Entry
        {
            public Entry(int id, PokemonPersonalData data) { Id = id; Data = data; }
            public int Id { get; }
            public PokemonPersonalData Data { get; }
        }

        public sealed class Lists
        {
            public string[] Pokemon, Types, Abilities, Items, GrowthCurves, EggGroups, DexColors;
        }

        private const int Id = 0, Name = 1, Type1 = 2, Type2 = 3, BaseHp = 4, EvHp = 10, Ability1 = 16, Ability2 = 17, Item1 = 18, Item2 = 19,
            CatchRate = 20, BaseExp = 21, Gender = 22, EggSteps = 23, Friendship = 24, Growth = 25, EggGroup1 = 26, EggGroup2 = 27,
            EscapeRate = 28, DexColor = 29, Flip = 30;

        private static readonly string[] ColumnNames =
        {
            "Pokemon ID", "Pokemon Name", "Type 1", "Type 2", "Base HP", "Base Atk", "Base Def", "Base SpAtk", "Base SpDef", "Base Speed",
            "EV HP", "EV Atk", "EV Def", "EV SpAtk", "EV SpDef", "EV Speed", "Ability 1", "Ability 2", "Item 1", "Item 2",
            "Catch Rate", "Base Exp", "Gender Ratio", "Egg Steps", "Base Friendship", "Growth Curve", "Egg Group 1", "Egg Group 2",
            "Escape Rate", "Dex Color", "Flip",
        };

        private readonly Lists _lists;
        private readonly CsvNames _pokemon, _types, _abilities, _items, _growth, _eggGroups, _colors;
        private readonly int _count;
        private readonly Func<int, (PokemonPersonalData Data, string Error)> _current;
        private readonly Dictionary<int, (PokemonPersonalData Data, string Error)> _bases = new Dictionary<int, (PokemonPersonalData, string)>();

        /// <param name="current">The Pokémon as the editor holds it now, staged edits included.</param>
        public PersonalDataCsv(Lists lists, int count, Func<int, (PokemonPersonalData Data, string Error)> current)
        {
            _lists = lists;
            _count = count;
            _current = current;
            _pokemon = new CsvNames(lists.Pokemon, "Pokémon");
            _types = new CsvNames(lists.Types, "type", Enum.GetValues<PokemonType>().Select(t => ((int)t, t.ToString())));
            _abilities = new CsvNames(lists.Abilities, "ability");
            _items = new CsvNames(lists.Items, "item");
            // Earlier exports wrote the code's own names for these.
            _growth = new CsvNames(lists.GrowthCurves, "growth curve", Enum.GetValues<PokemonGrowthCurve>().Select(g => ((int)g, g.ToString())));
            _eggGroups = new CsvNames(lists.EggGroups, "egg group", Enum.GetValues<PokemonEggGroup>().Select(g => ((int)g, g.ToString())));
            _colors = new CsvNames(lists.DexColors, "Pokédex colour", Enum.GetValues<PokemonDexColor>().Select(c => ((int)c, c.ToString())));
        }

        public override string Title => "Personal data";
        public override IReadOnlyList<string> Columns => ColumnNames;

        public static void Write(TextWriter writer, Lists lists, IEnumerable<(int Id, PokemonPersonalData Data)> rows)
        {
            writer.WriteLine(CsvText.Row(ColumnNames));
            foreach ((int id, PokemonPersonalData d) in rows)
                writer.WriteLine(CsvText.Row(Cells(lists, id, d)));
        }

        private static object[] Cells(Lists l, int id, PokemonPersonalData d) => new object[]
        {
            id, NameOf(l.Pokemon, id), NameOf(l.Types, (int)d.type1), NameOf(l.Types, (int)d.type2),
            d.baseHP, d.baseAtk, d.baseDef, d.baseSpAtk, d.baseSpDef, d.baseSpeed,
            d.evHP, d.evAtk, d.evDef, d.evSpAtk, d.evSpDef, d.evSpeed,
            NameOf(l.Abilities, d.firstAbility), NameOf(l.Abilities, d.secondAbility), NameOf(l.Items, d.item1), NameOf(l.Items, d.item2),
            d.catchRate, d.givenExp, d.genderVec, d.eggSteps, d.baseFriendship, NameOf(l.GrowthCurves, (int)d.growthCurve),
            NameOf(l.EggGroups, d.eggGroup1), NameOf(l.EggGroups, d.eggGroup2), d.escapeRate, NameOf(l.DexColors, (int)d.color), d.flip,
        };

        // A number with no name is written as the number, which the import reads back.
        private static string NameOf(string[] names, int index) => index >= 0 && index < names.Length && names[index].Trim().Length > 0 ? names[index] : index.ToString();

        private (PokemonPersonalData Data, string Error) Base(int id)
        {
            if (!_bases.TryGetValue(id, out (PokemonPersonalData Data, string Error) b)) _bases[id] = b = _current(id);
            return b;
        }

        public override void Check(CsvCheck c)
        {
            if (!c.NumberAndName(Id, Name, _pokemon, out int id)) return;
            c.Record.Label = $"#{id} {_pokemon[id]}";
            if (id >= _count)
            {
                c.Error(Id, $"There is no Pokémon {id}. The last one is {_count - 1}.");
                return;
            }
            (PokemonPersonalData baseData, string error) = Base(id);
            if (error != null) { c.Error(-1, error); return; }

            PokemonPersonalData d = new PokemonPersonalData(new MemoryStream(baseData.ToByteArray()));
            bool ok = true;
            int v;
            if (ok &= c.Name(Type1, _types, out v, (int)baseData.type1)) d.type1 = (PokemonType)v;
            if (ok &= c.Name(Type2, _types, out v, (int)baseData.type2)) d.type2 = (PokemonType)v;
            byte[] stats = new byte[6], evs = new byte[6];
            for (int s = 0; s < 6; s++)
            {
                if (ok &= c.Number(BaseHp + s, 0, 255, out v)) stats[s] = (byte)v;
                // Each yield has two bits in the file.
                if (ok &= c.Number(EvHp + s, 0, 3, out v)) evs[s] = (byte)v;
            }
            (d.baseHP, d.baseAtk, d.baseDef, d.baseSpAtk, d.baseSpDef, d.baseSpeed) = (stats[0], stats[1], stats[2], stats[3], stats[4], stats[5]);
            (d.evHP, d.evAtk, d.evDef, d.evSpAtk, d.evSpDef, d.evSpeed) = (evs[0], evs[1], evs[2], evs[3], evs[4], evs[5]);

            int abilityMax = RomInfo.isHGE ? ushort.MaxValue : byte.MaxValue;
            if (ok &= c.Name(Ability1, _abilities, out v, baseData.firstAbility) && InRange(c, Ability1, v, abilityMax)) d.firstAbility = (ushort)v;
            if (ok &= c.Name(Ability2, _abilities, out v, baseData.secondAbility) && InRange(c, Ability2, v, abilityMax)) d.secondAbility = (ushort)v;
            if (ok &= c.Name(Item1, _items, out v, baseData.item1)) d.item1 = (ushort)v;
            if (ok &= c.Name(Item2, _items, out v, baseData.item2)) d.item2 = (ushort)v;
            if (ok &= c.Number(CatchRate, 0, 255, out v)) d.catchRate = (byte)v;
            if (ok &= c.Number(BaseExp, 0, 255, out v)) d.givenExp = (byte)v;
            if (ok &= c.Number(Gender, 0, 255, out v)) d.genderVec = (byte)v;
            if (ok &= c.Number(EggSteps, 0, 255, out v)) d.eggSteps = (byte)v;
            if (ok &= c.Number(Friendship, 0, 255, out v)) d.baseFriendship = (byte)v;
            if (ok &= c.Name(Growth, _growth, out v, (int)baseData.growthCurve) && InRange(c, Growth, v, 255)) d.growthCurve = (PokemonGrowthCurve)v;
            if (ok &= c.Name(EggGroup1, _eggGroups, out v, baseData.eggGroup1) && InRange(c, EggGroup1, v, 255)) d.eggGroup1 = (byte)v;
            if (ok &= c.Name(EggGroup2, _eggGroups, out v, baseData.eggGroup2) && InRange(c, EggGroup2, v, 255)) d.eggGroup2 = (byte)v;
            if (ok &= c.Number(EscapeRate, 0, 255, out v)) d.escapeRate = (byte)v;
            // The colour shares its byte with the flip flag.
            if (ok &= c.Name(DexColor, _colors, out v, (int)baseData.color) && InRange(c, DexColor, v, 127)) d.color = (PokemonDexColor)v;
            if (ok &= c.Flag(Flip, out bool flip)) d.flip = flip;
            if (ok) c.Record.Value = new Entry(id, d);
        }

        private static bool InRange(CsvCheck c, int column, int value, int max)
        {
            if (value <= max) return true;
            c.Error(column, $"{c.ColumnName(column)} can't be higher than {max}.");
            return false;
        }

        public override void CheckTogether(IReadOnlyList<CsvRecord> records) => CsvRules.OneRowPerRecord(records, r => (r.Value as Entry)?.Id ?? -1, "Pokémon", On);

        public override IReadOnlyList<CsvChange> Changes(IReadOnlyList<CsvRecord> accepted)
        {
            List<CsvChange> changes = new List<CsvChange>();
            foreach (CsvRecord r in accepted)
            {
                Entry e = (Entry)r.Value;
                CsvRules.Compare(changes, r.Label, ColumnNames, Type1, Cells(_lists, e.Id, Base(e.Id).Data), Cells(_lists, e.Id, e.Data));
            }
            return changes;
        }
    }
}

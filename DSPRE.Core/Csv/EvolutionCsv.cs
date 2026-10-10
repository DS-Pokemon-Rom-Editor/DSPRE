using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.Csv
{
    /// <summary>Evolutions as one row per evolution, in slot order. A Pokémon with none has one row whose method
    /// is the empty one, so importing it clears its evolutions.</summary>
    public sealed class EvolutionCsv : CsvImporter
    {
        /// <summary>One slot as the Evolutions tab holds it: the method's place in its list, the parameter, the target
        /// and, on hg-engine, the target's form.</summary>
        public readonly record struct Slot(int Method, int Param, int Target, int Form);

        public sealed class Entry
        {
            public Entry(int id, List<Slot> slots) { Id = id; Slots = slots; }
            public int Id { get; }
            public List<Slot> Slots { get; }
        }

        public sealed class Setup
        {
            public string[] Pokemon, Methods, Items, Moves;
            /// <summary>Other names a method may go by, such as the code's own names in older exports.</summary>
            public IEnumerable<(int Index, string Name)> MethodAliases;
            /// <summary>The method that marks an empty slot.</summary>
            public int NoneMethod;
            /// <summary>The highest species an evolution may target.</summary>
            public int LastTarget;
            /// <summary>The first id that is an alternate form, whose evolutions the game never reads.</summary>
            public int FirstForm;
            public int SlotCount;
            public Func<int, EvolutionParamMeaning> Meaning;
            public Func<int, bool> NeedsTarget;
            /// <summary>The Pokémon's slots as the editor holds them now, staged edits included.</summary>
            public Func<int, (Slot[] Slots, string Error)> Current;
        }

        private const int Id = 0, Name = 1, Method = 2, Param = 3, Target = 4;
        private static readonly string[] ColumnNames = { "Pokemon ID", "Pokemon Name", "Method", "Parameter", "Target" };

        private readonly Setup _setup;
        private readonly CsvNames _pokemon, _methods, _items, _moves, _targets;
        private readonly Dictionary<int, (Slot[] Slots, string Error)> _bases = new Dictionary<int, (Slot[], string)>();

        public EvolutionCsv(Setup setup)
        {
            _setup = setup;
            _pokemon = new CsvNames(setup.Pokemon, "Pokémon");
            _methods = new CsvNames(setup.Methods, "evolution method", setup.MethodAliases);
            _items = new CsvNames(setup.Items, "item");
            _moves = new CsvNames(setup.Moves, "move");
            _targets = new CsvNames(setup.Pokemon.Take(setup.LastTarget + 1).ToArray(), "Pokémon");
        }

        public override string Title => "Evolutions";
        public override IReadOnlyList<string> Columns => ColumnNames;

        public static void Write(TextWriter writer, Setup setup, IEnumerable<(int Id, IReadOnlyList<Slot> Slots)> pokemon)
        {
            writer.WriteLine(CsvText.Row(ColumnNames));
            foreach ((int id, IReadOnlyList<Slot> slots) in pokemon)
            {
                List<Slot> used = slots.Where(s => s.Method != setup.NoneMethod).ToList();
                if (used.Count == 0) writer.WriteLine(CsvText.Row(id, Named(setup.Pokemon, id), Named(setup.Methods, setup.NoneMethod), "", ""));
                foreach (Slot s in used)
                    writer.WriteLine(CsvText.Row(id, Named(setup.Pokemon, id), Named(setup.Methods, s.Method), ParamText(setup, s),
                        setup.NeedsTarget(s.Method) ? Named(setup.Pokemon, s.Target) : ""));
            }
        }

        private static string Named(string[] names, int i) => i >= 0 && i < names.Length && names[i].Trim().Length > 0 ? names[i] : i.ToString();

        private static string ParamText(Setup setup, Slot s) => setup.Meaning(s.Method) switch
        {
            EvolutionParamMeaning.Ignored => "",
            EvolutionParamMeaning.ItemName => Named(setup.Items, s.Param),
            EvolutionParamMeaning.MoveName => Named(setup.Moves, s.Param),
            EvolutionParamMeaning.PokemonName => Named(setup.Pokemon, s.Param),
            _ => s.Param.ToString(),
        };

        private (Slot[] Slots, string Error) Base(int id)
        {
            if (!_bases.TryGetValue(id, out (Slot[] Slots, string Error) b)) _bases[id] = b = _setup.Current(id);
            return b;
        }

        public override void Check(CsvCheck c)
        {
            if (!c.NumberAndName(Id, Name, _pokemon, out int id)) return;
            c.Record.Label = $"#{id} {_pokemon[id]}";
            (Slot[] baseSlots, string error) = Base(id);
            if (error != null) { c.Error(-1, error); return; }
            if (!c.Name(Method, _methods, out int method)) return;
            if (method == _setup.NoneMethod)
            {
                c.Record.Value = new Entry(id, new List<Slot>());
                return;
            }
            if (id >= _setup.FirstForm)
                c.Warn(-1, "Alternate forms don't use their own evolutions. The game uses the base Pokémon's.");

            int param = 0;
            bool ok = true;
            switch (_setup.Meaning(method))
            {
                case EvolutionParamMeaning.Ignored:
                    if (!c.IsBlank(Param)) c.Warn(Param, "This method has no parameter. It will be ignored.", new CsvFix(Param, "", "Clear it"));
                    break;
                case EvolutionParamMeaning.FromLevel: ok = c.Number(Param, 1, 100, out param); break;
                case EvolutionParamMeaning.ItemName: ok = c.Name(Param, _items, out param); break;
                case EvolutionParamMeaning.MoveName: ok = c.Name(Param, _moves, out param); break;
                case EvolutionParamMeaning.PokemonName: ok = c.Name(Param, _pokemon, out param); break;
                case EvolutionParamMeaning.BeautyValue: ok = c.Number(Param, 0, 255, out param); break;
                default: ok = c.Number(Param, 0, short.MaxValue, out param); break;
            }

            int target = 0;
            if (_setup.NeedsTarget(method))
            {
                if (c.Name(Target, _targets, out target) && target == 0)
                {
                    c.Error(Target, "Pick a target Pokémon.");
                    ok = false;
                }
                else ok &= target > 0;
            }
            else if (!c.IsBlank(Target)) c.Warn(Target, "This method doesn't use a target. It will be ignored.", new CsvFix(Target, "", "Clear it"));

            if (ok) c.Record.Value = new Entry(id, new List<Slot> { new Slot(method, param, target, 0) });
        }

        public override void CheckTogether(IReadOnlyList<CsvRecord> records)
        {
            Dictionary<int, int> count = new Dictionary<int, int>();
            Dictionary<int, CsvRecord> emptied = new Dictionary<int, CsvRecord>();
            foreach (CsvRecord r in records)
            {
                if (r.Value is not Entry e) continue;
                if (e.Slots.Count == 0) { emptied[e.Id] = r; continue; }
                count[e.Id] = count.TryGetValue(e.Id, out int n) ? n + 1 : 1;
                if (count[e.Id] > _setup.SlotCount)
                    On(r).Error(-1, $"{r.Label} has more than {_setup.SlotCount} evolutions. Skip this row or add slots in the Patch Toolbox.");
            }
            foreach ((int id, CsvRecord r) in emptied)
                if (count.ContainsKey(id))
                    On(r).Warn(Method, "Other rows add evolutions for this Pokémon. This empty row does nothing.");
        }

        /// <summary>Each Pokémon's slots in row order, the form kept where the slot's target stays the same.</summary>
        public Dictionary<int, Slot[]> Result(IReadOnlyList<CsvRecord> accepted)
        {
            Dictionary<int, List<Slot>> lists = new Dictionary<int, List<Slot>>();
            foreach (CsvRecord r in accepted)
            {
                Entry e = (Entry)r.Value;
                if (!lists.TryGetValue(e.Id, out List<Slot> list)) lists[e.Id] = list = new List<Slot>();
                list.AddRange(e.Slots);
            }
            Dictionary<int, Slot[]> result = new Dictionary<int, Slot[]>();
            foreach ((int id, List<Slot> list) in lists)
            {
                Slot[] was = Base(id).Slots ?? Array.Empty<Slot>();
                Slot[] slots = new Slot[_setup.SlotCount];
                for (int i = 0; i < slots.Length; i++)
                {
                    if (i >= list.Count) { slots[i] = new Slot(_setup.NoneMethod, 0, 0, 0); continue; }
                    int form = i < was.Length && was[i].Target == list[i].Target ? was[i].Form : 0;
                    slots[i] = list[i] with { Form = form };
                }
                result[id] = slots;
            }
            return result;
        }

        public override IReadOnlyList<CsvChange> Changes(IReadOnlyList<CsvRecord> accepted)
        {
            List<CsvChange> changes = new List<CsvChange>();
            foreach ((int id, Slot[] slots) in Result(accepted).OrderBy(kv => kv.Key))
            {
                string was = Describe(Base(id).Slots), now = Describe(slots);
                if (was != now) changes.Add(new CsvChange($"#{id} {_pokemon[id]}", "Evolutions", was, now));
            }
            return changes;
        }

        private string Describe(IEnumerable<Slot> slots)
        {
            List<string> parts = new List<string>();
            foreach (Slot s in slots ?? Enumerable.Empty<Slot>())
            {
                if (s.Method == _setup.NoneMethod) continue;
                string param = ParamText(_setup, s);
                string text = Named(_setup.Methods, s.Method) + (param.Length > 0 ? " " + param : "");
                if (_setup.NeedsTarget(s.Method)) text += " to " + Named(_setup.Pokemon, s.Target);
                parts.Add(text);
            }
            return parts.Count == 0 ? "None" : string.Join("; ", parts);
        }
    }
}

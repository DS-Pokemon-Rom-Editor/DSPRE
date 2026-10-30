using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE.Tracking
{
    /// <summary>
    /// Turns records into subjects people know: a Pokémon, a trainer, a map. Each record kind names the subject a
    /// change belongs to, the part of it, and what its fields are called in the editors.
    /// </summary>
    internal sealed class ChangelogSubjects
    {
        private readonly ChangeContext _ctx;
        private readonly ChangeKinds _kinds;
        private readonly bool _pictures;
        private readonly Dictionary<string, ChangelogSubject> _subjects = new Dictionary<string, ChangelogSubject>(StringComparer.Ordinal);
        private readonly Dictionary<ChangelogArea, int> _order = new Dictionary<ChangelogArea, int>();
        private Dictionary<(string Use, int Id), SortedSet<string>> _maps;
        private Dictionary<int, (ChangelogSection Section, string Role)> _textRoles;

        public ChangelogSubjects(ChangeContext ctx, ChangeKinds kinds, bool pictures)
        {
            _ctx = ctx;
            _kinds = kinds;
            _pictures = pictures;
        }

        public List<ChangelogSubject> Result() => _subjects.Values
            .Where(s => s.Status != ChangelogStatus.Modified || s.Areas.Any(a => a.Lines.Count > 0 || a.Count > 0))
            .OrderBy(s => s.Section).ThenBy(s => s.SortKey, StringComparer.Ordinal).ToList();

        public void Add(ChangeKind kind, string id, string key, JsonObject record)
        {
            JsonNode changes = record["changes"];
            string op = record["op"]?.GetValue<string>() ?? "edit";
            ChangelogStatus status = op == "add" ? ChangelogStatus.New : op == "delete" ? ChangelogStatus.Removed : ChangelogStatus.Modified;
            string title = record["name"]?.GetValue<string>() ?? kind.Title(id, _ctx);
            int number = int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : -1;
            Current current = new Current(this, kind, id);
            // A whole record added or removed lists what it holds.
            if (status != ChangelogStatus.Modified && changes is JsonObject whole && (whole[ObjectDiff.Now] ?? whole[ObjectDiff.Was]) is JsonNode body)
                changes = Spread(body, status == ChangelogStatus.Removed);

            switch (kind.Name)
            {
                case "personal": Personal(number, key, changes); break;
                case "evolution": Evolution(number, key, changes, current); break;
                case "learnset": Learnset(number, key, changes); break;
                case "egg-moves": EggMoves(key, changes, current); break;
                case "pokedex-data": PokedexData(title, number, key, changes); break;
                case "move":
                    Fields(ChangelogSection.Moves, "move:" + number, Name(_ctx.Names.Move(number)), Pad(number), key, changes, "Move data editor",
                        p => p.Count > 0 && p[0].StartsWith("contest", StringComparison.Ordinal) ? "Contest" : "Battle", MoveLabels, status);
                    break;
                case "item": Item(number, key, changes, status); break;
                case "berry":
                    int berry = BerryData.FirstBerryItem + number;
                    Fields(ChangelogSection.Items, "item:" + berry, Name(_ctx.Names.Item(berry)), Pad(berry), key, changes, "Berry data", _ => "Berry", null, status);
                    break;
                case "trade": Trade(number, key, changes, current, status); break;
                case "trainer": Trainer(number, key, changes, current, status); break;
                case "text": Text(number, key, changes); break;
                case "header":
                    string map = Plain(title, "Header") ?? title;
                    Fields(ChangelogSection.Maps, "map:" + map, map, map, key, NamedHeader(changes), "Header editor", _ => "Map settings", HeaderLabels, status);
                    break;
                case "headbutt":
                    string trees = HeaderName(number) ?? Readable(title);
                    Fields(ChangelogSection.Wild, "wild-map:" + trees, trees, trees, key, changes, "Wild Pokémon editor", _ => "Headbutt trees", null, status);
                    break;
                case "rock-smash":
                    string rocks = HeaderName(number) ?? Readable(title);
                    Fields(ChangelogSection.Items, "rock-smash:" + rocks, rocks, "~" + rocks, key, changes, "Item tables (pickup, hidden, Rock Smash)", _ => "Rock Smash items", null, status);
                    break;
                case "events": Events(number, key, changes); break;
                case "encounters": Encounters(number, key, changes); break;
                case "safari-zone" or "special-encounters":
                    Fields(ChangelogSection.Wild, kind.Name + id, Readable(title), Readable(title), key, changes,
                        kind.Name == "special-encounters" ? "Special encounters editor" : "Wild Pokémon editor", _ => AreaOf(kind.Name), null, status);
                    break;
                case "graphic": Graphic(kind, id, number, key, changes); break;
                case "file" when title.Contains("banner", StringComparison.OrdinalIgnoreCase): Banner(key, changes); break;
                case "code-table": CodeTable(title, key, changes, status); break;
                case "battle-height": OfSpecies(kind, id, title, key, changes, "Battle position", "Pokémon editor", 15); break;
                case "battle-sprite" or "regional-dex-numbers": BySpecies(kind.Name, title, key, changes); break;
                case "pokedex-areas": OfSpecies(kind, id, title, key, changes, "Pokédex areas", "Pokédex lists", 13); break;
                case "growth" or "regional-dex-species" or "pokedex-search-steps" or "pokeathlon" or "battle-height-form":
                    string editor = kind.Name == "growth" ? "Growth curves" : kind.Name == "pokeathlon" ? "Pokémon editor" : "Pokédex lists";
                    Fields(ChangelogSection.Pokemon, kind.Name + id, Readable(title), "~" + Readable(title), key, changes, editor, _ => AreaOf(kind.Name), null, status);
                    break;
                case "trainer-messages" or "trainer-capsules":
                    Fields(ChangelogSection.Trainers, kind.Name + id, Readable(title), "~" + Readable(title), key, changes,
                        kind.Name == "trainer-capsules" ? "Ball capsules" : "Trainer editor", _ => AreaOf(kind.Name), null, status);
                    break;
                case string follower when follower.StartsWith("follower", StringComparison.Ordinal):
                    Fields(ChangelogSection.Followers, kind.Name + id, Readable(title), Readable(title), key, changes, "Follower talk", _ => AreaOf(kind.Name), null, status);
                    break;
                case string tower when tower.StartsWith("battle-tower", StringComparison.Ordinal):
                    Fields(ChangelogSection.Facilities, kind.Name + id, Readable(title), Readable(title), key, changes, "Battle Tower editor", _ => AreaOf(kind.Name), null, status);
                    break;
                case "sound-archive": Counted(ChangelogSection.Sound, "Sounds", key, "Audio editor", "sound", Count(changes), null); break;
                case "script": Counted(ChangelogSection.Technical, "Scripts", key, "Script editor", "script file", 1, Places("script", number)); break;
                case "code": Counted(ChangelogSection.Technical, "Game code", key, "ROM patch toolbox", "code file", 1, null); break;
                case "map-file": Counted(ChangelogSection.Technical, "Map data", key, "Map editor", "map file", 1, null); break;
                case "matrix": Counted(ChangelogSection.Technical, "Map data", key, "Matrix editor", "matrix", 1, null); break;
                case "area-data": Counted(ChangelogSection.Technical, "Map data", key, "Area data editor", "area data file", 1, null); break;
                case string building when building.StartsWith("building", StringComparison.Ordinal) || building.StartsWith("distortion", StringComparison.Ordinal):
                    Counted(ChangelogSection.Technical, "Map data", key, kind.Name.StartsWith("distortion", StringComparison.Ordinal) ? "Distortion World" : "Building editor", MapNouns.TryGetValue(kind.Name, out string noun) ? noun : "map file", 1, null);
                    break;
                case string battle when kind is Kinds.BattleScriptKind || battle.StartsWith("move-", StringComparison.Ordinal) || battle.EndsWith("sequence", StringComparison.Ordinal):
                    Counted(ChangelogSection.Technical, "Battle scripts", key, "Move animations & battle scripts", "battle script", 1, null);
                    break;
                case "trainer-class-metadata": Counted(ChangelogSection.Technical, "Trainer class data", key, "VS intro editor", "class", Count(changes), null); break;
                default: Counted(ChangelogSection.Technical, "Other files", key, "All graphics", "file", 1, null); break;
            }
            if (status == ChangelogStatus.Modified) return;
            foreach (ChangelogSubject subject in _subjects.Values.Where(s => s.Records.Contains(key))) subject.Status = status;
        }

        private static readonly Dictionary<string, string> MapNouns = new Dictionary<string, string>
        {
            ["building-animation"] = "building animation", ["building-texture-set"] = "building texture set",
            ["distortion-map"] = "Distortion World map", ["distortion-attributes"] = "Distortion World attribute file",
        };

        // ---- Pokémon ----

        private static readonly Dictionary<string, (string Area, string Label)> PersonalFields = new Dictionary<string, (string, string)>
        {
            ["basehp"] = ("Stats", "HP"), ["baseatk"] = ("Stats", "Attack"), ["basedef"] = ("Stats", "Defense"),
            ["basespeed"] = ("Stats", "Speed"), ["basespatk"] = ("Stats", "Sp. Atk"), ["basespdef"] = ("Stats", "Sp. Def"),
            ["type1"] = ("Types", "Type 1"), ["type2"] = ("Types", "Type 2"),
            ["firstability"] = ("Abilities", "Ability 1"), ["secondability"] = ("Abilities", "Ability 2"),
            ["item1"] = ("Held items", "Held item 1"), ["item2"] = ("Held items", "Held item 2"),
            ["evhp"] = ("EV yield", "HP"), ["evatk"] = ("EV yield", "Attack"), ["evdef"] = ("EV yield", "Defense"),
            ["evspeed"] = ("EV yield", "Speed"), ["evspatk"] = ("EV yield", "Sp. Atk"), ["evspdef"] = ("EV yield", "Sp. Def"),
            ["gendervec"] = ("Breeding", "Gender ratio"), ["eggsteps"] = ("Breeding", "Egg cycles"),
            ["egggroup1"] = ("Breeding", "Egg group 1"), ["egggroup2"] = ("Breeding", "Egg group 2"),
            ["catchrate"] = ("Other", "Catch rate"), ["givenexp"] = ("Other", "Base experience"), ["basefriendship"] = ("Other", "Base friendship"),
            ["growthcurve"] = ("Other", "Growth rate"), ["escaperate"] = ("Other", "Safari Zone flee rate"), ["color"] = ("Other", "Pokédex colour"),
            ["flip"] = ("Other", "Sprite flips"), ["machines"] = ("TM and HM moves", "TM and HM moves"),
        };

        private static readonly string[] PersonalAreaOrder = { "Stats", "Types", "Abilities", "Held items", "EV yield", "Breeding", "TM and HM moves", "Other" };

        private void Personal(int species, string key, JsonNode changes)
        {
            foreach ((List<string> path, JsonObject leaf, JsonObject hunk) in Walk(changes))
            {
                string field = path.Count > 0 ? path[0].Replace("_", "") : "";
                (string area, string label) = PersonalFields.TryGetValue(field, out (string, string) known) ? known : ("Other", Label(path));
                ChangelogArea a = Area(ChangelogSection.Pokemon, "species:" + species, Name(_ctx.Names.Species(species)), Pad(species), area, "Pokémon editor", key,
                    Array.IndexOf(PersonalAreaOrder, area));
                if (field == "machines" && leaf != null) a.Lines.AddRange(SetLines(leaf, Machine));
                else if (leaf != null) a.Lines.AddRange(ValueLines(label, leaf));
                else if (hunk != null) a.Lines.AddRange(HunkLines(label, hunk));
            }
        }

        private static string Machine(JsonNode node)
        {
            if (node is JsonValue v && v.TryGetValue(out int i))
                return i < PokemonPersonalData.TmsCount ? "TM" + (i + 1).ToString("D2", CultureInfo.InvariantCulture)
                    : "HM" + (i - PokemonPersonalData.TmsCount + 1).ToString("D2", CultureInfo.InvariantCulture);
            return Value(node);
        }

        private void Evolution(int species, string key, JsonNode changes, Current current)
        {
            JsonArray now = current.Project?["data"] as JsonArray, was = current.Original?["data"] as JsonArray;
            ChangelogArea a = Area(ChangelogSection.Pokemon, "species:" + species, Name(_ctx.Names.Species(species)), Pad(species), "Evolution", "Pokémon editor", key, 10);
            foreach ((List<string> path, JsonObject leaf, JsonObject _) in Walk(changes))
            {
                if (leaf == null || path.Count < 3 || !int.TryParse(path[1], out int slot)) continue;
                JsonObject nowSlot = now != null && slot < now.Count ? now[slot] as JsonObject : null;
                JsonObject wasSlot = was != null && slot < was.Count ? was[slot] as JsonObject : null;
                string into = nowSlot?["target"] is JsonNode target ? Value(target) : "";
                string label = "Evolution " + (slot + 1) + (into.Length > 0 && path[2] != "target" ? " (" + into + ")" : "");
                switch (path[2])
                {
                    case "target":
                        a.Lines.Add(Change(label + ", evolves into", Value(leaf[ObjectDiff.Was]), Value(leaf[ObjectDiff.Now])));
                        break;
                    case "method":
                        a.Lines.Add(Change(label + ", method", Value(leaf[ObjectDiff.Was]), Value(leaf[ObjectDiff.Now])));
                        break;
                    case "param":
                        (string wasLabel, string wasText) = Param(wasSlot?["method"], leaf[ObjectDiff.Was]);
                        (string nowLabel, string nowText) = Param(nowSlot?["method"], leaf[ObjectDiff.Now]);
                        a.Lines.Add(Change(label + ", " + (nowLabel ?? wasLabel ?? "condition").ToLowerInvariant(), wasText, nowText));
                        break;
                    default:
                        a.Lines.AddRange(ValueLines(label + ", " + Label(path.Skip(2).ToList()).ToLowerInvariant(), leaf));
                        break;
                }
            }
        }

        private (string Label, string Text) Param(JsonNode method, JsonNode value)
        {
            int number = value is JsonValue v && v.TryGetValue(out int i) ? i : -1;
            if (number < 0 || method is not JsonValue m || !m.TryGetValue(out string methodName) || !Enum.TryParse(methodName, out EvolutionMethod evo)
                || !EvolutionFile.EvoDescriptions.TryGetValue(evo, out EvolutionParamMeaning meaning))
                return (null, Value(value));
            return meaning switch
            {
                EvolutionParamMeaning.FromLevel => ("Level", number.ToString(CultureInfo.InvariantCulture)),
                EvolutionParamMeaning.ItemName => ("Item", Name(_ctx.Names.Item(number))),
                EvolutionParamMeaning.MoveName => ("Move", Name(_ctx.Names.Move(number))),
                EvolutionParamMeaning.PokemonName => ("Pokémon", Name(_ctx.Names.Species(number))),
                EvolutionParamMeaning.BeautyValue => ("Beauty", number.ToString(CultureInfo.InvariantCulture)),
                _ => (null, number.ToString(CultureInfo.InvariantCulture)),
            };
        }

        private void Learnset(int species, string key, JsonNode changes)
        {
            ChangelogArea a = Area(ChangelogSection.Pokemon, "species:" + species, Name(_ctx.Names.Species(species)), Pad(species), "Learnset", "Pokémon editor", key, 11);
            List<(string Move, string Level)> removed = new List<(string, string)>(), added = new List<(string, string)>();
            foreach ((List<string> _, JsonObject leaf, JsonObject hunk) in Walk(changes))
            {
                if (hunk != null)
                {
                    foreach (JsonNode e in Entries(hunk[ObjectDiff.Remove])) removed.Add((Value(e?["move"]), Value(e?["level"])));
                    foreach (JsonNode e in Entries(hunk[ObjectDiff.Add])) added.Add((Value(e?["move"]), Value(e?["level"])));
                }
                else if (leaf != null) a.Lines.AddRange(ValueLines("Learnset", leaf));
            }
            // A move kept but moved to another level reads as one change.
            foreach ((string move, string level) in removed.ToList())
            {
                int match = added.FindIndex(x => x.Move == move);
                if (match < 0) continue;
                a.Lines.Add(Change(move + ", level", level, added[match].Level));
                added.RemoveAt(match);
                removed.Remove((move, level));
            }
            foreach ((string move, string level) in added) a.Lines.Add(new ChangelogLine { Text = "Added " + move + " at level " + level });
            foreach ((string move, string level) in removed) a.Lines.Add(new ChangelogLine { Text = "Removed " + move + " at level " + level });
        }

        private void EggMoves(string key, JsonNode changes, Current current)
        {
            List<string> rows = (current.Project?["values"] as JsonObject)?.Select(e => e.Value is JsonValue rv && rv.TryGetValue(out string s) ? s : "").ToList()
                ?? new List<string>();
            foreach ((List<string> _, JsonObject _, JsonObject hunk) in Walk(changes))
            {
                if (hunk == null) continue;
                int at = hunk["at"] is JsonValue v && v.TryGetValue(out int i) ? i : 0;
                // Each species marker row is followed by its moves, so the nearest marker above the hunk owns it.
                int species = -1;
                for (int r = Math.Min(at, rows.Count) - 1; r >= 0 && species < 0; r--)
                {
                    int value = rows[r].Length > 0 && char.IsDigit(rows[r][0]) ? ChangesJson.ParseRef(rows[r]) : -1;
                    if (value >= EggMoveData.SpeciesConstant && value != 0xFFFF) species = value - EggMoveData.SpeciesConstant;
                }
                ChangelogArea a = species >= 0
                    ? Area(ChangelogSection.Pokemon, "species:" + species, Name(_ctx.Names.Species(species)), Pad(species), "Egg moves", "Egg move editor", key, 12)
                    : Area(ChangelogSection.Pokemon, "egg-moves", "Egg moves", "~egg", "Egg moves", "Egg move editor", key, 12);
                foreach (JsonNode e in Entries(hunk[ObjectDiff.Add])) a.Lines.Add(new ChangelogLine { Text = "Added " + Value(e) });
                foreach (JsonNode e in Entries(hunk[ObjectDiff.Remove])) a.Lines.Add(new ChangelogLine { Text = "Removed " + Value(e) });
            }
        }

        private void PokedexData(string title, int column, string key, JsonNode changes)
        {
            string name = Regex.Replace(title, @"^Pokédex data \d+\s*", "");
            bool order = name.Contains("order", StringComparison.OrdinalIgnoreCase) || name.Contains("sort", StringComparison.OrdinalIgnoreCase);
            Func<JsonNode, string> unit = name switch
            {
                "height" => node => node is JsonValue h && h.TryGetValue(out double d) ? (d / 10).ToString("0.0", CultureInfo.InvariantCulture) + " m" : Value(node),
                "weight" => node => node is JsonValue w && w.TryGetValue(out double d) ? (d / 10).ToString("0.0", CultureInfo.InvariantCulture) + " kg" : Value(node),
                _ => Value,
            };
            foreach ((List<string> path, JsonObject leaf, JsonObject _) in Walk(changes))
            {
                if (leaf == null || path.Count < 2 || !int.TryParse(path[1], out int index)) continue;
                if (order)
                {
                    ChangelogArea list = Area(ChangelogSection.Pokemon, "dex:" + column, "Pokédex " + name, "~dex " + Pad(column), "Order", "Pokédex lists", key, 0);
                    list.Lines.AddRange(ValueLines("Number " + (index + 1), leaf));
                    continue;
                }
                ChangelogArea a = Area(ChangelogSection.Pokemon, "species:" + index, Name(_ctx.Names.Species(index)), Pad(index), "Pokédex", "Pokémon editor", key, 13);
                a.Lines.Add(Change(Sentence(name), unit(leaf[ObjectDiff.Was]), unit(leaf[ObjectDiff.Now])));
            }
        }

        private static readonly Dictionary<string, string> Areas = new Dictionary<string, string>
        {
            ["growth"] = "Experience", ["battle-height-form"] = "Battle position", ["regional-dex-species"] = "Order", ["pokedex-search-steps"] = "Search order", ["pokeathlon"] = "Pokéathlon",
            ["trainer-messages"] = "Messages", ["trainer-capsules"] = "Ball capsules",
            ["follower-slots"] = "Talk slots", ["follower-reaction"] = "Reactions", ["follower-movement"] = "Movements",
            ["follower-groups"] = "Species groups", ["follower-model"] = "Models",
            ["battle-tower-pokemon"] = "Sets", ["battle-tower-trainer"] = "Trainers",
            ["headbutt"] = "Headbutt trees", ["rock-smash"] = "Rock Smash", ["safari-zone"] = "Safari Zone", ["special-encounters"] = "Special encounters",
        };

        private static string AreaOf(string kind) => Areas.TryGetValue(kind, out string area) ? area : Sentence(kind);

        /// <summary>A record that belongs to one Pokémon, like a battle height or a Pokédex area list, put under that Pokémon.</summary>
        private void OfSpecies(ChangeKind kind, string id, string title, string key, JsonNode changes, string area, string editor, int order)
        {
            int species = kind is Kinds.ArrayKind array ? array.SpeciesOf(id) : -1;
            string part = kind is Kinds.ArrayKind named && !string.IsNullOrWhiteSpace(named.PartOf(id)) ? Sentence(named.PartOf(id)) : area;
            ChangelogArea a = species >= 0
                ? Area(ChangelogSection.Pokemon, "species:" + species, Name(_ctx.Names.Species(species)), Pad(species), area, editor, key, order)
                : Area(ChangelogSection.Pokemon, kind.Name + ":" + id, Readable(title), "~" + title, area, editor, key, order);
            foreach ((List<string> _, JsonObject leaf, JsonObject hunk) in Walk(changes))
            {
                if (leaf != null) a.Lines.AddRange(ValueLines(part, leaf));
                else if (hunk != null) a.Lines.AddRange(HunkLines(part, hunk));
            }
        }

        /// <summary>A table with a row per species, each row put under its Pokémon.</summary>
        private void BySpecies(string kind, string title, string key, JsonNode changes)
        {
            (string area, string editor) = kind == "battle-sprite" ? ("Battle position", "Pokémon editor") : ("Pokédex", "Pokédex lists");
            foreach ((List<string> path, JsonObject leaf, JsonObject hunk) in Walk(changes))
            {
                int at = path.FindIndex(p => int.TryParse(p, out _));
                if (at < 0 || leaf == null)
                {
                    ChangelogArea whole = Area(ChangelogSection.Pokemon, kind, Readable(title), "~" + title, area, editor, key, 15);
                    if (leaf != null) whole.Lines.AddRange(ValueLines(Label(path), leaf));
                    else if (hunk != null) whole.Lines.AddRange(HunkLines(Label(path), hunk));
                    continue;
                }
                int species = int.Parse(path[at], CultureInfo.InvariantCulture);
                ChangelogArea a = Area(ChangelogSection.Pokemon, "species:" + species, Name(_ctx.Names.Species(species)), Pad(species), area, editor, key, 15);
                string field = path.Count > at + 1 ? Label(path.Skip(at + 1).ToList()) : kind == "regional-dex-numbers" ? "Regional number" : "Value";
                a.Lines.AddRange(ValueLines(field, leaf));
            }
        }

        // ---- Moves, items, trades ----

        private static readonly Dictionary<string, string> MoveLabels = new Dictionary<string, string>
        {
            ["battleeffect"] = "Effect", ["split"] = "Category", ["damage"] = "Power", ["movetype"] = "Type", ["accuracy"] = "Accuracy",
            ["pp"] = "PP", ["sideeffectprobability"] = "Effect chance", ["target"] = "Target", ["priority"] = "Priority",
            ["flagfield"] = "Flags", ["contestappeal"] = "Contest appeal", ["contestconditiontype"] = "Contest condition",
        };

        private static readonly Dictionary<string, string> ItemLabels = new Dictionary<string, string>
        {
            ["fullprice"] = "Price", ["holdeffect"] = "Hold effect", ["holdeffectparam"] = "Hold effect value", ["flingpower"] = "Fling power",
            ["flingeffect"] = "Fling effect", ["naturalgiftpower"] = "Natural Gift power", ["naturalgifttype"] = "Natural Gift type",
            ["preventtoss"] = "Can't be thrown away", ["selectable"] = "Can be registered", ["fieldpocket"] = "Bag pocket",
            ["battlepocket"] = "Battle bag pocket", ["fieldusefunc"] = "Use outside battle", ["battleusefunc"] = "Use in battle", ["pluckeffect"] = "Pluck effect",
        };

        private void Item(int item, string key, JsonNode changes, ChangelogStatus status)
        {
            // hg-engine splits a price into Price and PriceHigh. full_price already shows both, so they're dropped.
            if (changes is JsonObject o && o.ContainsKey("full_price"))
            {
                JsonObject trimmed = (JsonObject)o.DeepClone();
                trimmed.Remove("price");
                trimmed.Remove("price_high");
                changes = trimmed;
            }
            Fields(ChangelogSection.Items, "item:" + item, Name(_ctx.Names.Item(item)), Pad(item), key, changes, "Item editor", _ => "Item", ItemLabels, status);
        }

        private static readonly Dictionary<string, string> TradeLabels = new Dictionary<string, string>
        {
            ["species"] = "Gives", ["requestedspecies"] = "Asks for", ["helditem"] = "Held item", ["otid"] = "Trainer ID", ["otgender"] = "Trainer gender",
            ["hpiv"] = "HP IV", ["atkiv"] = "Attack IV", ["defiv"] = "Defense IV", ["speediv"] = "Speed IV", ["spatkiv"] = "Sp. Atk IV", ["spdefiv"] = "Sp. Def IV",
            ["pid"] = "Personality value",
        };

        private void Trade(int trade, string key, JsonNode changes, Current current, ChangelogStatus status) =>
            Fields(ChangelogSection.Trades, "trade:" + trade, TradeName(trade, current), Pad(trade), key, changes, "Trade editor", _ => "Trade", TradeLabels, status);

        /// <summary>"Trade 1 (Onix)", by the Pokémon the trade gives.</summary>
        private string TradeName(int trade, Current current = null)
        {
            ChangeKind kind = current == null ? _kinds.All.FirstOrDefault(k => k.Name == "trade") : null;
            current ??= kind != null ? new Current(this, kind, trade.ToString(CultureInfo.InvariantCulture)) : null;
            string gives = current?.Project?["species"] is JsonNode s ? Value(s) : "";
            return "Trade " + (trade + 1) + (gives.Length > 0 ? " (" + gives + ")" : "");
        }

        // ---- Trainers ----

        private static readonly Dictionary<string, string> TrainerLabels = new Dictionary<string, string>
        {
            ["class"] = "Class", ["choosemoves"] = "Sets moves", ["chooseitems"] = "Sets held items", ["items"] = "Battle items", ["ai"] = "AI",
            ["battletype"] = "Battle type",
        };

        private static readonly Dictionary<string, string> PartyLabels = new Dictionary<string, string>
        {
            ["level"] = "level", ["item"] = "held item", ["moves"] = "moves", ["difficulty"] = "difficulty", ["form"] = "form",
            ["genderandability"] = "gender and ability", ["ballseals"] = "ball seals",
        };

        private void Trainer(int trainer, string key, JsonNode changes, Current current, ChangelogStatus status)
        {
            JsonObject whole = current.Project ?? current.Original;
            string name = TrainerName(trainer, whole);
            JsonArray party = whole?["party"] as JsonArray;
            foreach ((List<string> path, JsonObject leaf, JsonObject hunk) in Walk(changes))
            {
                bool inParty = path.Count > 0 && path[0] == "party";
                ChangelogArea a = Area(ChangelogSection.Trainers, "trainer:" + trainer, name, Pad(trainer), inParty ? "Party" : "Trainer", "Trainer editor", key, inParty ? 1 : 0);
                if (inParty && hunk != null)
                {
                    foreach (JsonNode e in Entries(hunk[ObjectDiff.Add])) a.Lines.Add(new ChangelogLine { Text = "Added " + Mon(e) });
                    foreach (JsonNode e in Entries(hunk[ObjectDiff.Remove])) a.Lines.Add(new ChangelogLine { Text = "Removed " + Mon(e) });
                }
                else if (inParty && leaf != null && path.Count >= 3 && int.TryParse(path[1], out int slot))
                {
                    string field = path[2].Replace("_", "");
                    string mon = party != null && slot < party.Count && party[slot]?["species"] is JsonNode sp ? Value(sp) : "";
                    string label = field == "species" ? "Pokémon " + (slot + 1)
                        : "Pokémon " + (slot + 1) + (mon.Length > 0 ? " (" + mon + ")" : "") + ", "
                          + (PartyLabels.TryGetValue(field, out string l) ? l : Label(path.Skip(2).ToList()).ToLowerInvariant());
                    a.Lines.AddRange(ValueLines(label, leaf));
                }
                else if (leaf != null) a.Lines.AddRange(ValueLines(Known(path, TrainerLabels), leaf));
                else if (hunk != null) a.Lines.AddRange(HunkLines(Known(path, TrainerLabels), hunk));
            }
            if (status != ChangelogStatus.Modified) Area(ChangelogSection.Trainers, "trainer:" + trainer, name, Pad(trainer), "Trainer", "Trainer editor", key, 0);
        }

        private static string Mon(JsonNode entry) =>
            Value(entry?["species"]) + (entry?["level"] is JsonNode level ? " at level " + Value(level) : "");

        private string TrainerName(int trainer, JsonObject record)
        {
            string cls = record?["class"] is JsonNode c ? Value(c) : "";
            string name = _ctx.Names.Trainer(trainer) ?? "";
            string both = (cls + " " + name).Trim();
            return both.Length > 0 ? both : "Trainer " + trainer;
        }

        // ---- Text ----

        private void Text(int archive, string key, JsonNode changes)
        {
            (ChangelogSection section, string role) = TextRole(archive);
            SortedSet<string> places = role == null ? MapsUsing("text", archive) : null;
            foreach ((List<string> path, JsonObject leaf, JsonObject hunk) in Walk(changes))
            {
                if (leaf != null && path.Count >= 2 && int.TryParse(path[1], out int line))
                {
                    string was = Clean(leaf[ObjectDiff.Was]), now = Clean(leaf[ObjectDiff.Now]);
                    if (role != null && Folded(section, role, line, key, was, now)) continue;
                    TextArea(section, archive, key, places, role).Lines.Add(Change("Line " + (line + 1), was, now));
                }
                else if (hunk != null)
                {
                    int at = hunk["at"] is JsonValue v && v.TryGetValue(out int i) ? i : 0;
                    ChangelogArea a = TextArea(section, archive, key, places, role);
                    List<string> removed = Entries(hunk[ObjectDiff.Remove]).Select(Clean).ToList(), added = Entries(hunk[ObjectDiff.Add]).Select(Clean).ToList();
                    int paired = Math.Min(removed.Count, added.Count);
                    for (int k = 0; k < paired; k++) a.Lines.Add(Change("Line " + (at + k + 1), removed[k], added[k]));
                    for (int k = paired; k < added.Count; k++) a.Lines.Add(new ChangelogLine { Text = "Added line " + (at + k + 1) + ": " + added[k] });
                    for (int k = paired; k < removed.Count; k++) a.Lines.Add(new ChangelogLine { Text = "Removed line " + (at + k + 1) + ": " + removed[k] });
                }
            }
        }

        private ChangelogArea TextArea(ChangelogSection section, int archive, string key, SortedSet<string> places, string role)
        {
            if (role != null) return Area(section, "text:" + archive, role, "~" + role, "Lines", "Text editor", key, 30, isText: true);
            if (places != null && places.Count > 0)
            {
                string map = places.First();
                return Area(ChangelogSection.Maps, "map:" + map, map, map, "Dialogue", "Text editor", key, 5, isText: true);
            }
            return Area(ChangelogSection.Text, "text:" + archive, "Text archive " + archive, Pad(archive), "Lines", "Text editor", key, 0, isText: true);
        }

        /// <summary>A line of a names or descriptions archive, put under the thing it names.</summary>
        private bool Folded(ChangelogSection section, string role, int line, string key, string was, string now)
        {
            (string subjectKey, string name, string area) = role switch
            {
                "Pokémon names" => ("species:" + line, Name(_ctx.Names.Species(line)), "Name"),
                "Pokédex entries" => ("species:" + line, Name(_ctx.Names.Species(line)), "Pokédex entry"),
                "Pokédex categories" => ("species:" + line, Name(_ctx.Names.Species(line)), "Pokédex category"),
                "Trade names" when TradeData.GetTradeCount() > 0 => ("trade:" + line % TradeData.GetTradeCount(), TradeName(line % TradeData.GetTradeCount()),
                    line < TradeData.GetTradeCount() ? "Nickname" : "Trainer name"),
                "Move names" => ("move:" + line, Name(_ctx.Names.Move(line)), "Name"),
                "Move descriptions" => ("move:" + line, Name(_ctx.Names.Move(line)), "Description"),
                "Item names" => ("item:" + line, Name(_ctx.Names.Item(line)), "Name"),
                "Item descriptions" => ("item:" + line, Name(_ctx.Names.Item(line)), "Description"),
                "Trainer names" => ("trainer:" + line, null, "Name"),
                "Trainer class names" => ("class:" + line, Name(now) + " class", "Name"),
                _ => (null, null, null),
            };
            if (subjectKey == null) return false;
            if (role == "Trainer names")
            {
                ChangeKind trainerKind = _kinds.All.FirstOrDefault(k => k.Name == "trainer");
                Current trainer = trainerKind != null ? new Current(this, trainerKind, line.ToString("D4", CultureInfo.InvariantCulture)) : null;
                name = TrainerName(line, trainer?.Project ?? trainer?.Original);
            }
            string sort = !subjectKey.StartsWith("class:", StringComparison.Ordinal) && int.TryParse(subjectKey.Substring(subjectKey.IndexOf(':') + 1), out int number)
                ? Pad(number) : "~" + name;
            bool naming = area is "Nickname" or "Trainer name";
            ChangelogArea a = Area(section, subjectKey, name, sort, naming ? "Names" : area, "Text editor", key, area == "Name" || naming ? -1 : 20,
                isText: area is "Description" or "Pokédex entry");
            a.Lines.Add(Change(area, was, now));
            return true;
        }

        private (ChangelogSection, string) TextRole(int archive)
        {
            if (_textRoles == null)
            {
                _textRoles = new Dictionary<int, (ChangelogSection, string)>();
                void Role(int number, ChangelogSection section, string role)
                {
                    if (number >= 0) _textRoles.TryAdd(number, (section, role));
                }
                foreach (int n in PokemonNamesTextNumbers ?? Array.Empty<int>()) Role(n, ChangelogSection.Pokemon, "Pokémon names");
                Role(PokedexEntryTextNumber, ChangelogSection.Pokemon, "Pokédex entries");
                Role(PokedexCategoryTextNumber, ChangelogSection.Pokemon, "Pokédex categories");
                Role(TradeTextNumber, ChangelogSection.Trades, "Trade names");
                Role(AttackNamesTextNumber, ChangelogSection.Moves, "Move names");
                Role(MoveNamesTextNumbers, ChangelogSection.Moves, "Move names");
                Role(MoveDescriptionsTextNumbers, ChangelogSection.Moves, "Move descriptions");
                Role(ItemNamesTextNumber, ChangelogSection.Items, "Item names");
                Role(ItemDescriptionsTextNumber, ChangelogSection.Items, "Item descriptions");
                Role(TrainerNamesMessageNumber, ChangelogSection.Trainers, "Trainer names");
                Role(TrainerClassMessageNumber, ChangelogSection.Trainers, "Trainer class names");
                Role(TrainerMessageTextNumber, ChangelogSection.Trainers, "Trainer messages");
                Role(AbilityNamesTextNumber, ChangelogSection.Text, "Ability names");
                Role(LocationNamesTextNumber, ChangelogSection.Text, "Place names");
                Role(TypesTextNumber, ChangelogSection.Text, "Type names");
                Role(StarterScreenTextNumber, ChangelogSection.Pokemon, "Starter choice screen");
                Role(StarterPokedexSpeciesTextNumber, ChangelogSection.Pokemon, "Starter species text");
                Role(PokedexMessagesTextNumber, ChangelogSection.Pokemon, "Pokédex messages");
                Role(PokedexHeightTextNumber, ChangelogSection.Pokemon, "Pokédex heights");
                Role(PokedexWeightTextNumber, ChangelogSection.Pokemon, "Pokédex weights");
                foreach (int n in PokedexForeignEntryTextNumbers ?? Array.Empty<int>()) Role(n, ChangelogSection.Pokemon, "Pokédex entries in other languages");
                foreach (int n in PokedexForeignNameTextNumbers ?? Array.Empty<int>()) Role(n, ChangelogSection.Pokemon, "Pokémon names in other languages");
                foreach (int n in PokedexForeignCategoryTextNumbers ?? Array.Empty<int>()) Role(n, ChangelogSection.Pokemon, "Pokédex categories in other languages");
                Role(BattleStringsTextNumber, ChangelogSection.Text, "Battle messages");
                Role(MovesUsedTextNumber, ChangelogSection.Moves, "Move use messages");
                Role(PlayerCharacterNamesTextNumber, ChangelogSection.Text, "Player names");
                Role(TrainerClassWithArticleMessageNumber, ChangelogSection.Trainers, "Trainer class names with \"a\" or \"an\"");
                Role(BattleTowerTrainerNamesMessageNumber, ChangelogSection.Facilities, "Battle Tower trainer names");
                Role(SealNamesTextNumber, ChangelogSection.Items, "Seal names");
                Role(BugContestTextNumber, ChangelogSection.Text, "Bug-Catching Contest");
            }
            return _textRoles.TryGetValue(archive, out (ChangelogSection, string) role) ? role : (ChangelogSection.Text, null);
        }

        /// <summary>Game text as it reads in game: control codes dropped, line breaks as spaces.</summary>
        private static string Clean(JsonNode node)
        {
            if (node is not JsonValue v || !v.TryGetValue(out string s)) return node == null ? "none" : Value(node);
            s = Regex.Replace(s, @"\{[A-Z_]+:([^}]*)\}", "$1");
            s = Regex.Replace(s, @"\{[^}]*\}", "");
            s = Regex.Replace(s, @"\\[nrf]", " ");
            s = Regex.Replace(s, @"\s+", " ").Trim();
            return s.Length == 0 ? "(empty)" : s;
        }

        // ---- Maps ----

        private static readonly Dictionary<string, string> HeaderLabels = new Dictionary<string, string>
        {
            ["musicday"] = "Music (day)", ["musicnight"] = "Music (night)", ["musicdayid"] = "Music (day)", ["musicnightid"] = "Music (night)", ["weatherid"] = "Weather", ["weather"] = "Weather", ["cameraangleid"] = "Camera angle",
            ["battlebackground"] = "Battle background", ["internalname"] = "Internal name", ["locationname"] = "Place name", ["areaicon"] = "Area icon",
            ["maptype"] = "Map type",
        };

        private static readonly Dictionary<string, string> MapFlags = new Dictionary<string, string>
        {
            ["bicycle"] = "Bicycle", ["run"] = "Running", ["escape_rope"] = "Escape Rope", ["fly"] = "Fly",
            ["outgoing_calls"] = "Outgoing calls", ["incoming_calls"] = "Incoming calls", ["radio"] = "Radio",
        };

        private static readonly Dictionary<string, (string Area, string One, string Many)> EventGroups = new Dictionary<string, (string, string, string)>
        {
            ["overworlds"] = ("People and objects", "person or object", "people and objects"),
            ["warps"] = ("Warps", "warp", "warps"),
            ["triggers"] = ("Triggers", "trigger", "triggers"),
            ["spawnables"] = ("Signs and hidden items", "sign or hidden item", "signs and hidden items"),
        };

        private void Events(int file, string key, JsonNode changes)
        {
            SortedSet<string> places = MapsUsing("events", file);
            string subject = places.Count > 0 ? places.First() : "Event file " + file;
            foreach ((List<string> path, JsonObject leaf, JsonObject hunk) in Walk(changes))
            {
                string group = path.Count > 0 ? path[0] : "";
                (string area, string one, string many) = EventGroups.TryGetValue(group, out (string, string, string) g) ? g : (Sentence(group), "entry", "entries");
                ChangelogArea a = Area(ChangelogSection.Maps, "map:" + subject, subject, subject, area, "Event editor", key, 3);
                if (hunk != null)
                {
                    int add = Entries(hunk[ObjectDiff.Add]).Count(), remove = Entries(hunk[ObjectDiff.Remove]).Count();
                    if (add > 0) a.Lines.Add(new ChangelogLine { Text = "Added " + add + " " + (add == 1 ? one : many) });
                    if (remove > 0) a.Lines.Add(new ChangelogLine { Text = "Removed " + remove + " " + (remove == 1 ? one : many) });
                }
                else if (leaf != null && path.Count >= 3 && int.TryParse(path[1], out int index))
                    a.Lines.AddRange(ValueLines(Sentence(one) + " " + (index + 1) + ", " + Label(path.Skip(2).ToList()).ToLowerInvariant(), leaf));
                else if (leaf != null) a.Lines.AddRange(ValueLines(Label(path), leaf));
            }
        }

        private void Encounters(int file, string key, JsonNode changes)
        {
            SortedSet<string> places = MapsUsing("encounters", file);
            string subject = places.Count > 0 ? string.Join(", ", places) : "Encounter file " + file;
            foreach ((List<string> path, JsonObject leaf, JsonObject hunk) in Walk(changes))
            {
                string field = path.Count > 0 ? path[0] : "";
                string area = field switch
                {
                    string f when f.StartsWith("surf", StringComparison.Ordinal) => "Surfing",
                    string f when f.Contains("rod", StringComparison.Ordinal) => "Fishing",
                    string f when f.StartsWith("rock_smash", StringComparison.Ordinal) => "Rock Smash",
                    string f when f.Contains("swarm", StringComparison.Ordinal) => "Swarms",
                    string f when f.Contains("music", StringComparison.Ordinal) || f.Contains("radar", StringComparison.Ordinal) => "Radio and Poké Radar",
                    string f when f.StartsWith("morning", StringComparison.Ordinal) || f.StartsWith("day", StringComparison.Ordinal) || f.StartsWith("night", StringComparison.Ordinal)
                        || f.StartsWith("walking", StringComparison.Ordinal) || f.StartsWith("grass", StringComparison.Ordinal) || f.Contains("level", StringComparison.Ordinal) => "Grass",
                    _ => "Other",
                };
                ChangelogArea a = Area(ChangelogSection.Wild, "wild:" + file, subject, places.Count > 0 ? places.First() : "~" + Pad(file), area, "Wild Pokémon editor", key, 0);
                if (leaf != null) a.Lines.AddRange(ValueLines(Label(path), leaf, "slot"));
                else if (hunk != null) a.Lines.AddRange(HunkLines(Label(path), hunk));
            }
        }

        /// <summary>The places whose header uses a file, by what it's used as.</summary>
        private SortedSet<string> MapsUsing(string use, int id)
        {
            if (_maps == null)
            {
                _maps = new Dictionary<(string, int), SortedSet<string>>();
                ChangeKind headers = _kinds.All.FirstOrDefault(k => k.Name == "header");
                int count;
                try { count = headers == null ? 0 : GetHeaderCount(); }
                catch (Exception ex) when (ex is System.IO.IOException || ex is ArgumentException || ex is NullReferenceException) { count = 0; }
                for (int h = 0; h < count; h++)
                {
                    // Read through the project tree, which keeps the header table in memory.
                    byte[] bytes = _ctx.Project.Read(headers.Files(h.ToString(CultureInfo.InvariantCulture), _ctx)[0]);
                    if (bytes == null || bytes.Length < MapHeader.Length) continue;
                    MapHeader header = MapHeader.LoadFromByteArray(bytes, (ushort)h);
                    if (header == null) continue;
                    string name = Name(DSPRE.HeaderLabels.LocationNameOf(header));
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    void Use(string what, int file)
                    {
                        if (!_maps.TryGetValue((what, file), out SortedSet<string> set)) _maps[(what, file)] = set = new SortedSet<string>(StringComparer.Ordinal);
                        set.Add(name);
                    }
                    Use("text", header.TextArchiveID);
                    Use("events", header.EventFileID);
                    Use("script", header.ScriptFileID);
                    Use("script", header.LevelScriptID);
                    if (header.WildPokemon != 0xFFFF) Use("encounters", header.WildPokemon);
                }
            }
            return _maps.TryGetValue((use, id), out SortedSet<string> places) ? places : new SortedSet<string>(StringComparer.Ordinal);
        }

        private SortedSet<string> Places(string use, int id) => id < 0 ? null : MapsUsing(use, id);

        /// <summary>A map's place name by its header number: Headbutt and Rock Smash files are numbered by header.</summary>
        private string HeaderName(int header)
        {
            ChangeKind headers = _kinds.All.FirstOrDefault(k => k.Name == "header");
            if (headers == null || header < 0) return null;
            string name = Plain(headers.Title(header.ToString(CultureInfo.InvariantCulture), _ctx), "Header");
            return string.IsNullOrWhiteSpace(name) ? null : Name(name);
        }

        /// <summary>Header numbers as the header editor names them: weather, camera and music.</summary>
        private static JsonNode NamedHeader(JsonNode changes)
        {
            if (changes is not JsonObject o) return changes;
            JsonObject named = (JsonObject)o.DeepClone();
            foreach (string key in named.Select(e => e.Key).ToList())
            {
                Func<int, string> names = key.Replace("_", "") switch
                {
                    "weatherid" => n => Lookup(GameFamily switch
                    {
                        GameFamilies.DP => Resources.PokeDatabase.Weather.DPWeatherDict,
                        GameFamilies.Plat => Resources.PokeDatabase.Weather.PtWeatherDict,
                        _ => Resources.PokeDatabase.Weather.HGSSWeatherDict,
                    }, n),
                    "cameraangleid" => n => Lookup(GameFamily switch
                    {
                        GameFamilies.DP => Resources.PokeDatabase.CameraAngles.DPPtCameraDict,
                        GameFamilies.Plat => Resources.PokeDatabase.CameraAngles.PtCameraDict,
                        _ => Resources.PokeDatabase.CameraAngles.HGSSCameraDict,
                    }, n),
                    "musicdayid" or "musicnightid" => n => (GameFamily switch
                    {
                        GameFamilies.DP => Resources.PokeDatabase.MusicDB.DPMusicDict,
                        GameFamilies.Plat => Resources.PokeDatabase.MusicDB.PtMusicDict,
                        _ => Resources.PokeDatabase.MusicDB.HGSSMusicDict,
                    }).TryGetValue((ushort)n, out string music) ? music : null,
                    _ => null,
                };
                if (names == null || named[key] is not JsonObject leaf || !ObjectDiff.IsLeaf(leaf)) continue;
                foreach (string side in new[] { ObjectDiff.Was, ObjectDiff.Now })
                    if (leaf[side] is JsonValue v && v.TryGetValue(out int n) && names(n) is string text) leaf[side] = n + " " + text;
            }
            return named;
        }

        private static string Lookup(Dictionary<int, string> names, int n) => names.TryGetValue(n, out string name) ? name : null;

        // ---- Graphics ----

        private static readonly Dictionary<string, string> Folders = new Dictionary<string, string>
        {
            [nameof(DirNames.BattleBg)] = "Battle backgrounds", [nameof(DirNames.BattleObj)] = "Battle objects", [nameof(DirNames.MonIcons)] = "Pokémon icons",
            [nameof(DirNames.ItemIcons)] = "Item icons", [nameof(DirNames.PokewalkerIcons)] = "Pokéwalker icons", [nameof(DirNames.PokewalkerSprites)] = "Pokéwalker sprites",
            [nameof(DirNames.TrainerCardGraphics)] = "Trainer card", [nameof(DirNames.TitleScreenGraphics)] = "Title screen", [nameof(DirNames.Fonts)] = "Fonts",
            [nameof(DirNames.WindowFrames)] = "Window frames", [nameof(DirNames.SummaryGraphics)] = "Summary screen", [nameof(DirNames.BagGraphics)] = "Bag",
            [nameof(DirNames.PartyMenuGraphics)] = "Party screen", [nameof(DirNames.PokedexGraphics)] = "Pokédex", [nameof(DirNames.PokegearGraphics)] = "Pokégear",
            [nameof(DirNames.WazaEffectChar)] = "Move animation drawings", [nameof(DirNames.WazaEffectPltt)] = "Move animation colours",
            [nameof(DirNames.FootprintGraphics)] = "Footprints", [nameof(DirNames.SealGraphics)] = "Seals",
        };


        private void Graphic(ChangeKind kind, string id, int member, string key, JsonNode changes)
        {
            string dir = kind.Folder.Substring(kind.Folder.IndexOf('/') + 1);
            bool trainer = (dir == nameof(DirNames.TrainerGraphics) || dir == nameof(DirNames.TrainerBackGraphics)) && TrainerGraphicsLayout.Stride == Kinds.GraphicKind.ClassParts.Length;
            ChangelogArea a;
            string part;
            if (dir == nameof(DirNames.PokemonBattleSprites))
            {
                int species = member / 6;
                part = Sentence(Kinds.GraphicKind.SpeciesParts[member % 6]);
                a = Area(ChangelogSection.Pokemon, "species:" + species, Name(_ctx.Names.Species(species)), Pad(species), "Sprites", "Pokémon editor", key, 14);
            }
            else if (trainer)
            {
                int cls = TrainerGraphicsLayout.ClassOf(member);
                part = (dir == nameof(DirNames.TrainerBackGraphics) ? "Back " : "") + Kinds.GraphicKind.ClassParts[member % Kinds.GraphicKind.ClassParts.Length].Replace("plain copy", "flat sprite");
                part = char.ToUpperInvariant(part[0]) + part.Substring(1);
                string name = Name(_ctx.Names.TrainerClass(cls)) + " class";
                a = Area(ChangelogSection.Trainers, "class:" + cls, name, "~" + name, "Sprite", "Trainer sprite editor", key, 2);
            }
            else
            {
                part = "File " + member;
                string folder = Folders.TryGetValue(dir, out string known) ? known : Sentence(dir);
                a = Area(ChangelogSection.Graphics, "graphics:" + dir, folder, folder, "Files", "All graphics", key, 0);
            }
            string picture = ChangesJson.Slug(a.Editor == "All graphics" ? dir + " " + member : SubjectOf(a) + " " + part);

            JsonObject o = changes as JsonObject;
            if (o?["colors"] is JsonObject colours)
            {
                a.Lines.Add(new ChangelogLine
                {
                    Text = part + ": " + colours.Count + (colours.Count == 1 ? " colour changed" : " colours changed"),
                    Picture = !_pictures ? null : trainer && TrainerGraphicsLayout.HasCells ? TrainerSprite(dir, member, picture) : Recoloured(kind, member, dir, trainer, picture),
                });
                foreach (KeyValuePair<string, JsonNode> c in colours)
                    if (ObjectDiff.IsLeaf(c.Value))
                        a.Lines.Add(Change(part + ", colour " + c.Key, Value(c.Value[ObjectDiff.Was]).ToUpperInvariant(), Value(c.Value[ObjectDiff.Now]).ToUpperInvariant()));
                return;
            }
            ChangelogPicture shown = null;
            // The plain copy is a flat picture already; the other parts are drawn through the class's cells.
            bool plainCopy = member % Kinds.GraphicKind.ClassParts.Length == Kinds.GraphicKind.ClassParts.Length - 1;
            if (_pictures && trainer && TrainerGraphicsLayout.HasCells && !plainCopy) shown = TrainerSprite(dir, member, picture);
            else if (_pictures && o?["image"] != null && kind is FieldKind field) shown = Picture(field, id, picture);
            a.Lines.Add(new ChangelogLine { Text = part + " changed", Picture = shown });
        }

        /// <summary>Draws a trainer class sprite from its cells, before and after, each from its own files.</summary>
        private ChangelogPicture TrainerSprite(string dir, int member, string name)
        {
            if (!Enum.TryParse(dir, out DirNames archive)) return null;
            int cls = TrainerGraphicsLayout.ClassOf(member);
            int drawing = TrainerGraphicsLayout.DrawingEntry(cls);
            byte[] Draw(RomTree tree)
            {
                byte[] Read(int m) => m < 0 ? null : NitroBgCodec.Inflate(tree?.Read(TrackedFile.Member(archive, m)));
                byte[] ncgr = Read(drawing), nclr = Read(TrainerGraphicsLayout.ColoursEntry(cls)), ncer = Read(TrainerGraphicsLayout.CellsEntry(cls));
                if (ncgr == null || nclr == null || ncer == null) return null;
                NcerFile cells = NcerFile.Read(ncer);
                ushort[] colours = NitroBgCodec.ReadColours(nclr);
                if (cells == null || colours == null) return null;
                // The first cell that draws anything is the standing pose; some classes start on a blank cell.
                for (int c = 0; c < cells.Cells.Count; c++)
                {
                    RawImage image = Cropped(CellRenderer.Draw(cells, c, ncgr, colours, 256, 256));
                    if (image != null) return GameBanner.EncodePng(Doubled(image));
                }
                return null;
            }
            byte[] before = Draw(_ctx.Base), after = Draw(_ctx.Project);
            return before == null && after == null ? null : new ChangelogPicture { Name = name, Before = Doubled(before), After = Doubled(after) };
        }

        // DS sprites are small; under 128 pixels they're shown at twice the size, pixel for pixel.
        private const int SmallPicture = 128;

        private static RawImage Doubled(RawImage image)
        {
            if (image.Width >= SmallPicture || image.Height >= SmallPicture) return image;
            int w = image.Width * 2, h = image.Height * 2;
            byte[] bgra = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    Array.Copy(image.Bgra, ((y / 2) * image.Width + x / 2) * 4, bgra, (y * w + x) * 4, 4);
            return new RawImage(w, h, bgra);
        }

        private static byte[] Doubled(byte[] png)
        {
            if (png == null || !IndexedPng.TryRead(png, out byte[] indices, out uint[] palette, out int w, out int h) || w >= SmallPicture || h >= SmallPicture) return png;
            byte[] big = new byte[w * h * 4];
            for (int y = 0; y < h * 2; y++)
                for (int x = 0; x < w * 2; x++)
                    big[y * w * 2 + x] = indices[(y / 2) * w + x / 2];
            return IndexedPng.Write(big, palette, w * 2, h * 2, palette.Length > 16 ? 8 : 4);
        }

        /// <summary>The picture trimmed to what it draws, or null when it draws nothing.</summary>
        private static RawImage Cropped(RawImage image)
        {
            int left = image.Width, top = image.Height, right = -1, bottom = -1;
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    if (image.Bgra[(y * image.Width + x) * 4 + 3] == 0) continue;
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
            if (right < 0) return null;
            int w = right - left + 1, h = bottom - top + 1;
            byte[] bgra = new byte[w * h * 4];
            for (int y = 0; y < h; y++) Array.Copy(image.Bgra, ((top + y) * image.Width + left) * 4, bgra, y * w * 4, w * 4);
            return new RawImage(w, h, bgra);
        }

        private string SubjectOf(ChangelogArea area) => _subjects.Values.First(s => s.Areas.Contains(area)).Name;

        /// <summary>The drawing before and after, as the record's own PNGs from the original ROM and the project.</summary>
        private ChangelogPicture Picture(FieldKind field, string id, string name)
        {
            byte[] before = Png(field, id, true), after = Png(field, id, false);
            return before == null && after == null ? null : new ChangelogPicture { Name = name, Before = Doubled(before), After = Doubled(after) };
        }

        private byte[] Png(FieldKind field, string id, bool original)
        {
            JsonObject json = field.Current(id, _ctx, original, out IReadOnlyDictionary<string, byte[]> parts);
            return json?["image"] is JsonValue v && v.TryGetValue(out string sha) && parts.TryGetValue(sha, out byte[] png) ? png : null;
        }

        /// <summary>A colour change shown on the drawing those colours paint.</summary>
        private ChangelogPicture Recoloured(ChangeKind kind, int member, string dir, bool trainer, string name)
        {
            int drawing = dir == nameof(DirNames.PokemonBattleSprites) ? member - member % 6 + 3
                : trainer ? member - member % Kinds.GraphicKind.ClassParts.Length
                : -1;
            if (drawing < 0 || kind is not FieldKind palettes || !Enum.TryParse(dir, out DirNames archive)) return null;
            if (!_kinds.TryOwner(TrackedFile.Member(archive, drawing), out ChangeKind drawingKind, out string drawingId) || drawingKind is not FieldKind drawings) return null;
            string id = member.ToString("D4", CultureInfo.InvariantCulture);
            uint[] Colours(bool original)
            {
                JsonObject json = palettes.Current(id, _ctx, original, out _);
                if (json?["colors"] is not JsonObject c) return null;
                return c.OrderBy(e => int.Parse(e.Key, CultureInfo.InvariantCulture)).Select((e, i) =>
                {
                    ushort bgr = ChangesJson.ParseColour(e.Value.GetValue<string>());
                    uint r = (uint)(bgr & 0x1F) << 3, g = (uint)(bgr >> 5 & 0x1F) << 3, b = (uint)(bgr >> 10 & 0x1F) << 3;
                    return (i == 0 ? 0u : 0xFF000000u) | r << 16 | g << 8 | b;
                }).ToArray();
            }
            // The drawing comes from the project for both sides, so only the colours differ.
            byte[] art = Png(drawings, drawingId, false) ?? Png(drawings, drawingId, true);
            if (art == null || !IndexedPng.TryRead(art, out byte[] indices, out uint[] palette, out int w, out int h)) return null;
            byte[] Paint(uint[] colours)
            {
                if (colours == null) return null;
                uint[] merged = palette.Select((p, i) => i < colours.Length ? colours[i] : p).ToArray();
                return IndexedPng.Write(indices, merged, w, h, merged.Length > 16 ? 8 : 4);
            }
            return new ChangelogPicture { Name = name, Before = Doubled(Paint(Colours(true))), After = Doubled(Paint(Colours(false))) };
        }

        // ---- Game, code tables ----

        private void Banner(string key, JsonNode changes)
        {
            ChangelogArea a = Area(ChangelogSection.Game, "game", "Game title", "game", "Banner", "Game icon & banner", key, 0);
            foreach ((List<string> _, JsonObject _, JsonObject hunk) in Walk(changes))
            {
                if (hunk == null) continue;
                List<string> was = Entries(hunk[ObjectDiff.Remove]).Select(Value).Select(s => s.Trim()).ToList();
                List<string> now = Entries(hunk[ObjectDiff.Add]).Select(Value).Select(s => s.Trim()).ToList();
                for (int i = 0; i < Math.Max(was.Count, now.Count); i++)
                    a.Lines.Add(Change("Title", i < was.Count ? was[i] : "none", i < now.Count ? now[i] : "none"));
            }
        }

        private void CodeTable(string title, string key, JsonNode changes, ChangelogStatus status)
        {
            string lower = title.ToLowerInvariant();
            (ChangelogSection section, string editor) = lower switch
            {
                string t when t.Contains("mart") || t.Contains("shop") => (ChangelogSection.Items, "Mart editor"),
                string t when t.Contains("starter") => (ChangelogSection.Pokemon, "Starter Pokémon editor"),
                string t when t.Contains("tutor") => (ChangelogSection.Moves, "Move tutors"),
                string t when t.Contains("tm ") || t.Contains("machine") => (ChangelogSection.Moves, "TM / HM editor"),
                string t when t.Contains("rematch") || t.Contains("phone") => (ChangelogSection.Trainers, "Pokégear rematch editor"),
                string t when t.Contains("pickup") || t.Contains("hidden item") => (ChangelogSection.Items, "Item tables (pickup, hidden, Rock Smash)"),
                _ => (ChangelogSection.Technical, "ROM patch toolbox"),
            };
            if (section == ChangelogSection.Technical)
            {
                Counted(section, "Code tables", key, editor, "table", 1, new SortedSet<string>(StringComparer.Ordinal) { Readable(title) });
                return;
            }
            Fields(section, "table:" + title, Readable(title), "~" + Readable(title), key, changes, editor, _ => "Table", null, status);
        }

        private void Counted(ChangelogSection section, string subject, string key, string editor, string noun, int count, SortedSet<string> places)
        {
            // One counted area per subject, whichever editor wrote each file.
            ChangelogArea a = _subjects.TryGetValue(section + "|count:" + subject, out ChangelogSubject s) && s.Areas.Count > 0 ? s.Areas[0]
                : Area(section, "count:" + subject, subject, "~" + subject, subject, editor, key, 0);
            s?.Records.Add(key);
            a.Counts[noun] = (a.Counts.TryGetValue(noun, out int had) ? had : 0) + Math.Max(1, count);
            if (places != null) a.Places.UnionWith(places);
        }

        // ---- Shared ----

        /// <summary>A record's fields as lines, under the area <paramref name="area"/> picks from each field's path.</summary>
        private void Fields(ChangelogSection section, string subjectKey, string subject, string sortKey, string key, JsonNode changes, string editor,
            Func<List<string>, string> area, Dictionary<string, string> labels, ChangelogStatus status)
        {
            foreach ((List<string> path, JsonObject leaf, JsonObject hunk) in Walk(changes))
            {
                ChangelogArea a = Area(section, subjectKey, subject, sortKey, area(path), editor, key, 0);
                string label = Known(path, labels);
                if (leaf != null && leaf[ObjectDiff.Was] is JsonArray && leaf[ObjectDiff.Now] is JsonArray && path.Count > 0 && path[0] == "flags")
                    a.Lines.AddRange(SetLines(leaf, v => MapFlags.TryGetValue(Value(v), out string f) ? f : Sentence(Value(v)), " now allowed", " no longer allowed"));
                else if (leaf != null) a.Lines.AddRange(ValueLines(label, leaf));
                else if (hunk != null) a.Lines.AddRange(HunkLines(label, hunk));
            }
            if (status != ChangelogStatus.Modified) Area(section, subjectKey, subject, sortKey, area(new List<string>()), editor, key, 0);
        }

        private ChangelogArea Area(ChangelogSection section, string subjectKey, string subject, string sortKey, string area, string editor, string key, int order,
            bool isText = false)
        {
            string full = section + "|" + subjectKey;
            if (!_subjects.TryGetValue(full, out ChangelogSubject s))
                _subjects[full] = s = new ChangelogSubject { Section = section, Name = string.IsNullOrWhiteSpace(subject) ? subjectKey : subject, SortKey = sortKey ?? subject ?? subjectKey };
            s.Records.Add(key);
            ChangelogArea a = s.Areas.FirstOrDefault(x => x.Name == area && x.Editor == editor);
            if (a != null) return a;
            a = new ChangelogArea { Name = area, Editor = editor, IsText = isText };
            int at = 0;
            while (at < s.Areas.Count && _order[s.Areas[at]] <= order) at++;
            s.Areas.Insert(at, a);
            _order[a] = order;
            return a;
        }

        /// <summary>Every changed value and every inserted or removed run of entries, with its path.</summary>
        private static IEnumerable<(List<string> Path, JsonObject Leaf, JsonObject Hunk)> Walk(JsonNode node, List<string> path = null)
        {
            path ??= new List<string>();
            if (node == null) yield break;
            if (ObjectDiff.IsLeaf(node))
            {
                yield return (path, (JsonObject)node, null);
                yield break;
            }
            if (node is JsonObject hunk && hunk.ContainsKey("at") && (hunk.ContainsKey(ObjectDiff.Add) || hunk.ContainsKey(ObjectDiff.Remove)))
            {
                yield return (path, null, hunk);
                yield break;
            }
            if (node is JsonObject o)
            {
                foreach (KeyValuePair<string, JsonNode> e in o)
                {
                    if (e.Key == "file") continue;
                    foreach ((List<string>, JsonObject, JsonObject) r in Walk(e.Value, new List<string>(path) { e.Key })) yield return r;
                }
            }
            else if (node is JsonArray a && a.All(i => i is JsonObject))
            {
                foreach (JsonNode item in a)
                    foreach ((List<string>, JsonObject, JsonObject) r in Walk(item, path)) yield return r;
            }
            else yield return (path, ObjectDiff.Leaf(null, node), null);
        }

        /// <summary>A whole added or removed record as one change per field.</summary>
        private static JsonNode Spread(JsonNode body, bool removed)
        {
            if (body is not JsonObject o) return ObjectDiff.Leaf(removed ? body : null, removed ? null : body);
            JsonObject spread = new JsonObject();
            foreach (KeyValuePair<string, JsonNode> e in o) spread[e.Key] = ObjectDiff.Leaf(removed ? e.Value : null, removed ? null : e.Value);
            return spread;
        }

        private static IEnumerable<JsonNode> Entries(JsonNode node)
        {
            if (node is not JsonArray a) yield break;
            foreach (JsonNode item in a)
            {
                // Inserted rows are kept as lines of JSON.
                if (item is JsonValue v && v.TryGetValue(out string text) && text.StartsWith('{'))
                {
                    JsonNode parsed;
                    try { parsed = JsonNode.Parse(text); }
                    catch (System.Text.Json.JsonException) { parsed = item; }
                    yield return parsed;
                }
                else yield return item;
            }
        }

        private static int Count(JsonNode changes) => Math.Max(1, Walk(changes).Count());

        private static IEnumerable<ChangelogLine> ValueLines(string label, JsonObject leaf, string slotNoun = null)
        {
            JsonNode was = leaf[ObjectDiff.Was], now = leaf[ObjectDiff.Now];
            if (was is JsonArray wa && now is JsonArray na && wa.Count == na.Count && wa.All(x => x is JsonValue) && na.All(x => x is JsonValue))
            {
                for (int i = 0; i < wa.Count; i++)
                    if (ObjectDiff.Canonical(wa[i]) != ObjectDiff.Canonical(na[i]))
                        yield return Change(label + ", " + (slotNoun ?? "entry") + " " + (i + 1), Value(wa[i]), Value(na[i]));
                yield break;
            }
            if (was is JsonArray || now is JsonArray)
            {
                foreach (ChangelogLine l in SetLines(leaf, Value)) yield return new ChangelogLine { Text = label + ": " + Lower(l.Text) };
                yield break;
            }
            if (was == null && now is JsonObject added)
            {
                yield return new ChangelogLine { Text = label + ": " + Describe(added) };
                yield break;
            }
            if (now == null && was is JsonObject gone)
            {
                yield return new ChangelogLine { Text = label + " removed: " + Describe(gone) };
                yield break;
            }
            if (Value(was) == "a file" || Value(now) == "a file")
            {
                yield return new ChangelogLine { Text = label + " changed" };
                yield break;
            }
            string from = Value(was), to = Value(now);
            if (from == to)
            {
                // Same name, different record (two trainers called Jose): show the raw values with their numbers. Empty and "None" are no change.
                if (was is not JsonValue wv || now is not JsonValue nv || !wv.TryGetValue(out string rawWas) || !nv.TryGetValue(out string rawNow)) yield break;
                (from, to) = (rawWas, rawNow);
            }
            yield return Change(label, from, to);
        }

        private static string Lower(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

        private static IEnumerable<ChangelogLine> SetLines(JsonObject leaf, Func<JsonNode, string> name, string added = null, string removed = null)
        {
            List<string> was = (leaf[ObjectDiff.Was] as JsonArray)?.Select(name).ToList() ?? new List<string>();
            List<string> now = (leaf[ObjectDiff.Now] as JsonArray)?.Select(name).ToList() ?? new List<string>();
            foreach (string s in now.Except(was)) yield return new ChangelogLine { Text = added != null ? s + added : "Added " + s };
            foreach (string s in was.Except(now)) yield return new ChangelogLine { Text = removed != null ? s + removed : "Removed " + s };
        }

        private static IEnumerable<ChangelogLine> HunkLines(string label, JsonObject hunk)
        {
            foreach (JsonNode e in Entries(hunk[ObjectDiff.Add])) yield return new ChangelogLine { Text = label + ": added " + (e is JsonObject o ? Describe(o) : Value(e)) };
            foreach (JsonNode e in Entries(hunk[ObjectDiff.Remove])) yield return new ChangelogLine { Text = label + ": removed " + (e is JsonObject o ? Describe(o) : Value(e)) };
        }

        private static string Describe(JsonObject o) =>
            string.Join(", ", o.Where(e => e.Value is JsonValue).Take(6).Select(e => Sentence(e.Key).ToLowerInvariant() + " " + Value(e.Value)));

        private static ChangelogLine Change(string label, string was, string now) => new ChangelogLine { Label = label, Was = was, Now = now };

        private static string Known(List<string> path, Dictionary<string, string> labels)
        {
            if (labels == null || path.Count == 0 || !labels.TryGetValue(path[^1].Replace("_", ""), out string l)) return Label(path);
            return path.Count > 1 ? Label(path.Take(path.Count - 1).ToList()) + ", " + l.ToLowerInvariant() : l;
        }

        /// <summary>A field's path as words: "party.0.level" is Party, 1, level.</summary>
        private static string Label(List<string> path)
        {
            List<string> words = new List<string>();
            foreach (string part in path)
            {
                if (part == "rows" || part == "values" || part == "data") continue;
                words.Add(int.TryParse(part, out int i) ? (words.Count == 0 ? "entry " : "") + (i + 1).ToString(CultureInfo.InvariantCulture) : Sentence(part).ToLowerInvariant());
            }
            string text = Regex.Replace(string.Join(", ", words), @"\b(hp|pp|evs?|ivs?|id|ai|ot|(tm|hm)\d*)\b", m => m.Value.EndsWith('s') ? m.Value.ToUpperInvariant()[..^1] + "s" : m.Value.ToUpperInvariant());
            text = Regex.Replace(text, @"\bpokemon\b", "Pokémon");
            return text.Length == 0 ? "Value" : char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        /// <summary>"base_sp_atk" or "BaseSpAtk" as Base sp atk.</summary>
        internal static string Sentence(string key)
        {
            if (string.IsNullOrEmpty(key)) return key ?? "";
            string spaced = Regex.Replace(key.Replace('_', ' ').Replace('-', ' '), @"(?<=[a-z0-9])(?=[A-Z])", " ").Trim().ToLowerInvariant();
            return spaced.Length == 0 ? spaced : char.ToUpperInvariant(spaced[0]) + spaced.Substring(1);
        }

        /// <summary>A record or folder name with game names in normal case and run-together words split.</summary>
        private static string Readable(string title)
        {
            string text = Regex.Replace(title ?? "", @"(?<=[a-z])(?=[A-Z])", " ");
            return Regex.Replace(text, @"\b[A-Z][A-Z.'\-]{2,}\b", m => Name(m.Value));
        }

        private static string Plain(string title, string prefix)
        {
            Match m = Regex.Match(title ?? "", "^" + Regex.Escape(prefix) + @" \d+\s+(.+)$");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static string Pad(int n) => n.ToString("D5", CultureInfo.InvariantCulture);

        /// <summary>A value in plain words: references by name, enums as words, Yes and No.</summary>
        internal static string Value(JsonNode node)
        {
            switch (node)
            {
                case null: return "none";
                case JsonValue v when v.TryGetValue(out bool b): return b ? "Yes" : "No";
                case JsonValue v when v.TryGetValue(out string s):
                    if (s.StartsWith("sha1:", StringComparison.Ordinal)) return "a file";
                    Match reference = Regex.Match(s, @"^-?\d+ (.+)$");
                    if (reference.Success) return Name(reference.Groups[1].Value);
                    if (Regex.IsMatch(s, @"^[A-Z][a-z]+(?:[A-Z0-9][a-z0-9]*)+$")) return Sentence(s);
                    return s;
                case JsonValue v: return v.ToJsonString();
                case JsonArray a when a.All(x => x is JsonValue): return a.Count == 0 ? "none" : string.Join(", ", a.Select(Value));
                case JsonObject o: return Describe(o);
                default: return node.ToJsonString();
            }
        }

        /// <summary>A game name in normal case: PIKACHU is Pikachu, MR. MIME is Mr. Mime. Names with lower case stay as they are.</summary>
        internal static string Name(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            name = name.Trim();
            // The games name an empty slot "-----", "-" or "None".
            if (name.All(c => c == '-') || name.Equals("none", StringComparison.OrdinalIgnoreCase)) return "none";
            if (name.Any(char.IsLower) || !name.Any(char.IsLetter) || Regex.IsMatch(name, @"^[A-Z]{1,3}\d+$")) return name;
            StringBuilder sb = new StringBuilder(name.Length);
            bool start = true;
            foreach (char c in name)
            {
                sb.Append(start ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
                start = c == ' ' || c == '-' || c == '.' || c == '(';
            }
            return sb.ToString();
        }

        /// <summary>The whole record now and in the original game, read once when a change needs it.</summary>
        private sealed class Current
        {
            private readonly ChangelogSubjects _owner;
            private readonly ChangeKind _kind;
            private readonly string _id;
            private JsonObject _project, _original;
            private bool _readProject, _readOriginal;

            public Current(ChangelogSubjects owner, ChangeKind kind, string id)
            {
                _owner = owner;
                _kind = kind;
                _id = id;
            }

            public JsonObject Project
            {
                get
                {
                    if (!_readProject) _project = Read(false);
                    _readProject = true;
                    return _project;
                }
            }

            public JsonObject Original
            {
                get
                {
                    if (!_readOriginal) _original = Read(true);
                    _readOriginal = true;
                    return _original;
                }
            }

            private JsonObject Read(bool original) => _kind is FieldKind field ? field.Current(_id, _owner._ctx, original, out _) : null;
        }
    }
}

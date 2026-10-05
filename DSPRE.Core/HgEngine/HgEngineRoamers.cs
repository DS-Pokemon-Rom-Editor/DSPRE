using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// hg-engine's roamers (EXPAND_ROAMERS), src/field_roamer.c: Save_CreateRoamerByID sets each slot's species and
    /// level in a switch, SpeciesToRoamerIdx maps a battled species back to its slot, and sRoamerLocations lists the
    /// maps a roamer is first placed on. Upstream, the game's own location lookup and next-route move still read the
    /// vanilla tables in arm9, so routes are shown read-only. A checkout that hooks GetRoamMapByLocationIdx and
    /// RoamerLocationUpdateEx to its own sRoamerAdjacencyTable reads every route from source, and then they are edited.
    /// </summary>
    public static class HgEngineRoamers
    {
        public const string RelPath = "src/field_roamer.c";
        private const string RoamerH = "include/constants/roamer.h";
        private const string SpeciesH = "include/constants/species.h";
        private const string MapsH = "include/constants/maps.h";

        private static string FilePath => Path.Combine(HgEngineProject.RepoPathUnc, RelPath.Replace('/', Path.DirectorySeparatorChar));

        public sealed record Slot(int Species, int Level);

        private sealed class Span { public int Start, End; public string Text; }

        private sealed class Parsed
        {
            public string Text;
            public Dictionary<int, (Span Species, Span Level)> Create = new();
            public Dictionary<int, Span> Reverse = new();   // slot -> the species case label that returns it
            public List<int> Routes = new();
            public List<CInitItem> RouteItems = new();
            public List<List<int>> Adjacency;      // null when the file has no sRoamerAdjacencyTable
            public List<CInitItem> AdjacencyItems = new();
        }

        // The tokens of the function's body, or null.
        private static (int Open, int Close)? Body(CSourceFile file, string name)
        {
            var t = file.Tokens;
            for (int i = 0; i + 1 < t.Count; i++)
            {
                if (t[i].Kind != CTokenKind.Identifier || !t[i].Is(file.Text, name) || !t[i + 1].Is(file.Text, "(")) continue;
                int close = file.Match(i + 1);
                if (close < 0 || close + 1 >= t.Count || !t[close + 1].Is(file.Text, "{")) continue;
                int end = file.Match(close + 1);
                if (end > 0) return (close + 1, end);
            }
            return null;
        }

        // Each "case X:" in [open, close) with the tokens up to the next case, default or the end.
        private static IEnumerable<(Span Label, int From, int To)> Cases(CSourceFile file, int open, int close)
        {
            var t = file.Tokens;
            var starts = new List<int>();
            for (int i = open + 1; i < close; i++)
                if (t[i].Kind == CTokenKind.Identifier && (t[i].Is(file.Text, "case") || t[i].Is(file.Text, "default"))) starts.Add(i);
            for (int k = 0; k < starts.Count; k++)
            {
                int s = starts[k];
                if (!t[s].Is(file.Text, "case")) continue;
                int colon = s + 1;
                while (colon < close && !t[colon].Is(file.Text, ":")) colon++;
                if (colon <= s + 1 || colon >= close) continue;
                var label = new Span { Start = t[s + 1].Start, End = t[colon - 1].End };
                label.Text = file.Text.Substring(label.Start, label.End - label.Start);
                yield return (label, colon + 1, k + 1 < starts.Count ? starts[k + 1] : close);
            }
        }

        // The value assigned to name (name = value;) within [from, to).
        private static Span Assigned(CSourceFile file, int from, int to, string name)
        {
            var t = file.Tokens;
            for (int i = from; i + 2 < to; i++)
            {
                if (t[i].Kind != CTokenKind.Identifier || !t[i].Is(file.Text, name) || !t[i + 1].Is(file.Text, "=")) continue;
                int end = i + 2;
                while (end < to && !t[end].Is(file.Text, ";")) end++;
                if (end == i + 2) return null;
                var span = new Span { Start = t[i + 2].Start, End = t[end - 1].End };
                span.Text = file.Text.Substring(span.Start, span.End - span.Start);
                return span;
            }
            return null;
        }

        private static string Parse(string text, out Parsed p)
        {
            p = new Parsed { Text = text };
            var roamers = HgEngineSymbolTable.Load(RoamerH);
            var species = HgEngineSymbolTable.Load(SpeciesH);
            var maps = HgEngineSymbolTable.Load(MapsH);
            if (roamers == null || species == null) return $"{RoamerH} or {SpeciesH} could not be read.";
            int? Eval(string expr, HgEngineSymbolTable table) =>
                HgEngineSourceExpression.TryEvaluate(expr, n => table != null && table.TryGetValue(n, out int v) ? v : null, out int r) ? r : null;

            var file = CSourceFile.For(text);
            var create = Body(file, "Save_CreateRoamerByID");
            var reverse = Body(file, "SpeciesToRoamerIdx");
            if (create == null || reverse == null) return $"{RelPath} has no Save_CreateRoamerByID or SpeciesToRoamerIdx.";

            foreach (var (label, from, to) in Cases(file, create.Value.Open, create.Value.Close))
            {
                if (Eval(label.Text, roamers) is not int slot) return $"{RelPath}: case {label.Text} isn't a roamer.";
                var sp = Assigned(file, from, to, "species");
                var lv = Assigned(file, from, to, "level");
                if (sp == null || lv == null) return $"{RelPath}: roamer {label.Text} doesn't set species and level the way DSPRE reads.";
                p.Create[slot] = (sp, lv);
            }
            var t = file.Tokens;
            foreach (var (label, from, to) in Cases(file, reverse.Value.Open, reverse.Value.Close))
            {
                if (from >= to || !t[from].Is(text, "return")) continue;
                int end = from + 1;
                while (end < to && !t[end].Is(text, ";")) end++;
                string value = text.Substring(t[from + 1].Start, t[end - 1].End - t[from + 1].Start);
                if (Eval(value, roamers) is int slot) p.Reverse[slot] = label;
            }

            var locations = file.Find("sRoamerLocations");
            if (locations != null)
                foreach (var item in locations.Init.Items)
                {
                    p.Routes.Add(Eval(item.ValueText(text), maps) ?? -1);
                    p.RouteItems.Add(item);
                }

            var adjacency = file.Find("sRoamerAdjacencyTable");
            if (adjacency != null)
            {
                p.Adjacency = new List<List<int>>();
                foreach (var row in adjacency.Init.Items)
                {
                    var cells = row.List?.Items;
                    if (cells == null || cells.Count < 2 || cells[1].List == null || Eval(cells[0].ValueText(text), null) is not int count)
                        return $"{RelPath}: sRoamerAdjacencyTable has a row DSPRE can't read.";
                    var next = cells[1].List.Items.Take(count).Select(n => Eval(n.ValueText(text), roamers) ?? -1).ToList();
                    p.Adjacency.Add(next);
                    p.AdjacencyItems.Add(row);
                }
            }

            foreach (var (slot, (sp, lv)) in p.Create)
            {
                if (Eval(sp.Text, species) == null) return $"{RelPath}: roamer {slot}'s species {sp.Text} can't be read.";
                if (Eval(lv.Text, null) == null) return $"{RelPath}: roamer {slot}'s level {lv.Text} can't be read.";
            }
            return null;
        }

        private static bool TryLoad(out Parsed p, out string error)
        {
            p = null;
            if (!HgEngineProject.IsActive) { error = "No hg-engine checkout is linked."; return false; }
            if (!File.Exists(FilePath)) { error = $"{RelPath} is missing from the checkout."; return false; }
            if (!HgEngineConfigState.DefinedNames().Contains("EXPAND_ROAMERS"))
            { error = "EXPAND_ROAMERS is off in include/config.h, so hg-engine uses the game's own roamer code."; return false; }
            error = Parse(HgEngineFileCache.GetText(FilePath).Replace("\r\n", "\n"), out p);
            return error == null;
        }

        /// <summary>Why the routes are shown but not edited in this checkout, or null when they can be edited.</summary>
        public static string RoutesReadOnlyReason()
        {
            if (!TryLoad(out var p, out string error)) return error;
            var hooks = HgEnginePatchList.ReadAll().FirstOrDefault(l => l.Kind == HgEnginePatchKind.Hook);
            bool hooked = hooks != null && new[] { "GetRoamMapByLocationIdx", "RoamerLocationUpdateEx" }
                .All(n => hooks.Entries.Any(e => e.Parsed && e.Symbol == n));
            if (!hooked || p.Adjacency == null)
                return "hg-engine places roamers on these routes from its own list, but encounters and the move to a next route still read the game's own table, so they aren't edited here.";
            if (p.Adjacency.Count != p.Routes.Count) return $"{RelPath}: sRoamerAdjacencyTable and sRoamerLocations have different lengths.";
            return null;
        }

        /// <summary>The routes' next routes, by index into the route list, or null when the checkout has no table.</summary>
        public static List<List<int>> ReadAdjacency() => TryLoad(out var p, out _) ? p.Adjacency : null;

        /// <summary>Writes the route list and the next routes. Only for a checkout where <see cref="RoutesReadOnlyReason"/> is null.</summary>
        public static bool TryWriteRoutes(IReadOnlyList<int> routes, IReadOnlyList<IReadOnlyList<int>> adjacency, out string error)
        {
            error = RoutesReadOnlyReason();
            if (error != null) return false;
            if (!TryLoad(out var p, out error)) return false;
            if (routes.Count != p.Routes.Count || adjacency.Count != p.Adjacency.Count)
            { error = "The number of routes is set by hg-engine's code; DSPRE edits the existing ones."; return false; }
            var maps = HgEngineSymbolTable.Load(MapsH);
            var roamers = HgEngineSymbolTable.Load(RoamerH);
            string MapName(int id) => maps != null && maps.TryGetNameWithPrefix(id, "MAP_", out string n) ? n : id.ToString();
            string LocName(int loc) => roamers != null && roamers.TryGetNameWithPrefix(loc, "ROAMER_LOC_", out string n) ? n : loc.ToString();

            var edits = new List<(int Start, int End, string Text)>();
            for (int r = 0; r < routes.Count; r++)
                if (routes[r] != p.Routes[r]) edits.Add((p.RouteItems[r].ValueStart, p.RouteItems[r].ValueEnd, MapName(routes[r])));
            for (int r = 0; r < adjacency.Count; r++)
            {
                var next = adjacency[r];
                if (next.Count < 1 || next.Count > 6 || next.Any(n => n < 0 || n >= routes.Count))
                { error = $"Route {r + 1} needs 1 to 6 next routes from the list."; return false; }
                if (next.SequenceEqual(p.Adjacency[r])) continue;
                string cells = string.Join(", ", next.Select(LocName).Concat(Enumerable.Repeat("-1", 6 - next.Count)));
                edits.Add((p.AdjacencyItems[r].ValueStart, p.AdjacencyItems[r].ValueEnd, $"{{ {next.Count}, {{ {cells} }} }}"));
            }
            if (edits.Count == 0) return true;
            string text = p.Text;
            foreach (var e in edits.OrderByDescending(e => e.Start)) text = text.Substring(0, e.Start) + e.Text + text.Substring(e.End);

            return HgEngineVerifiedWrite.TryWrite(FilePath, RelPath, text, written =>
            {
                string problem = Parse(written, out var back);
                if (problem != null) return problem;
                if (!back.Routes.SequenceEqual(routes)) return "the routes read back differently";
                for (int r = 0; r < adjacency.Count; r++)
                    if (!back.Adjacency[r].SequenceEqual(adjacency[r])) return $"route {r + 1}'s next routes read back differently";
                return null;
            }, out error);
        }

        public static bool TryRead(out List<Slot> slots, out List<int> routes, out string error)
        {
            slots = new List<Slot>();
            routes = new List<int>();
            if (!TryLoad(out var p, out error)) return false;
            var species = HgEngineSymbolTable.Load(SpeciesH);
            for (int s = 0; s <= (p.Create.Count == 0 ? -1 : p.Create.Keys.Max()); s++)
            {
                if (!p.Create.TryGetValue(s, out var c)) { error = $"{RelPath} has no case for roamer {s}."; return false; }
                HgEngineSourceExpression.TryEvaluate(c.Species.Text, n => species.TryGetValue(n, out int v) ? v : null, out int sp);
                HgEngineSourceExpression.TryEvaluate(c.Level.Text, _ => null, out int lv);
                slots.Add(new Slot(sp, lv));
            }
            routes = p.Routes;
            return true;
        }

        /// <summary>Writes each slot's species and level, and moves its SpeciesToRoamerIdx case to the new species.</summary>
        public static bool TryWrite(IReadOnlyList<Slot> slots, out string error)
        {
            if (!TryRead(out var current, out _, out error)) return false;
            if (!TryLoad(out var p, out error)) return false;
            if (slots.Count != current.Count) { error = "hg-engine's roamer count is set by its code; DSPRE edits the existing ones."; return false; }
            if (slots.GroupBy(s => s.Species).Any(g => g.Count() > 1)) { error = "Two roamers can't be the same species: the game finds the one you battled by species."; return false; }
            var species = HgEngineSymbolTable.Load(SpeciesH);
            string Name(int sp) => species.TryGetNameWithPrefix(sp, "SPECIES_", out string n) ? n : sp.ToString();

            var edits = new List<(int Start, int End, string Text)>();
            for (int s = 0; s < slots.Count; s++)
            {
                var (sp, lv) = p.Create[s];
                if (slots[s].Species != current[s].Species)
                {
                    edits.Add((sp.Start, sp.End, Name(slots[s].Species)));
                    if (!p.Reverse.TryGetValue(s, out var label)) { error = $"SpeciesToRoamerIdx has no case returning roamer {s}."; return false; }
                    edits.Add((label.Start, label.End, Name(slots[s].Species)));
                }
                if (slots[s].Level != current[s].Level) edits.Add((lv.Start, lv.End, slots[s].Level.ToString()));
            }
            if (edits.Count == 0) return true;
            string text = p.Text;
            foreach (var e in edits.OrderByDescending(e => e.Start)) text = text.Substring(0, e.Start) + e.Text + text.Substring(e.End);

            return HgEngineVerifiedWrite.TryWrite(FilePath, RelPath, text, written =>
            {
                string problem = Parse(written, out var back);
                if (problem != null) return problem;
                for (int s = 0; s < slots.Count; s++)
                {
                    HgEngineSourceExpression.TryEvaluate(back.Create[s].Species.Text, n => species.TryGetValue(n, out int v) ? v : null, out int sp);
                    HgEngineSourceExpression.TryEvaluate(back.Create[s].Level.Text, _ => null, out int lv);
                    if (sp != slots[s].Species || lv != slots[s].Level) return $"roamer {s} reads back differently";
                    if (!back.Reverse.TryGetValue(s, out var label) || !HgEngineSourceExpression.TryEvaluate(label.Text, n => species.TryGetValue(n, out int v) ? v : null, out int rs) || rs != slots[s].Species)
                        return $"SpeciesToRoamerIdx doesn't map roamer {s}'s species back";
                }
                return null;
            }, out error);
        }
    }
}

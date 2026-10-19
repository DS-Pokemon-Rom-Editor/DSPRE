using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DSPRE.HgEngine
{
    /// <summary>
    /// A battle test's TestBattleScenario initializer, read where each value sits in the text so the form can change
    /// one value without touching the rest. Values stay as the C text they are (constant names, numbers, OR'd flags).
    /// </summary>
    public sealed class HgEngineBattleScenario
    {
        public const int PartySize = 6, Moves = 4, ScriptBattlers = 2, ScriptLength = 8;

        public sealed class Mon
        {
            public string Species = "", Level = "", Form = "", Ability = "", Item = "", Hp = "", Status = "", Condition2 = "", MoveEffectFlags = "";
            public string[] Moves = { "", "", "", "" };
        }

        public sealed class Action
        {
            public string Do = "", Target = "";
        }

        public sealed class Expectation
        {
            public string Type = "", Battler = "", Field = "", Value = "";
        }

        public string BattleType = "", Weather = "", FieldCondition = "", Terrain = "", PassCount = "", KnownFailing = "";
        public Mon[] Player = NewParty(), Enemy = NewParty();
        public Action[][] PlayerScript = NewScript(), EnemyScript = NewScript();
        public List<Expectation> Expectations = new List<Expectation>();

        private static Mon[] NewParty() => Enumerable.Range(0, PartySize).Select(_ => new Mon()).ToArray();
        private static Action[][] NewScript() =>
            Enumerable.Range(0, ScriptBattlers).Select(_ => Enumerable.Range(0, ScriptLength).Select(_ => new Action()).ToArray()).ToArray();

        // ── Reading ──────────────────────────────────────────────────────────

        private sealed class Node
        {
            public int Open, Close;   // offsets of { and }
            public List<Item> Items = new List<Item>();
        }

        private sealed class Item
        {
            public string Designator;   // ".species", ".expectationValue.message", or null when positional
            public int ValueStart, ValueEnd;
            public Node Child;
            public int End;             // where the item ends, before its comma
        }

        private string _text;
        private Node _root;

        /// <summary>Reads the scenario in a test's text, or returns null with why it can't.</summary>
        public static HgEngineBattleScenario Read(string text, out string error)
        {
            error = null;
            string visible = HgEngineBattleTests.Visible(text);
            int open = visible.IndexOf('{');
            if (open < 0) { error = "There is no { ... } scenario in this test."; return null; }
            int pos = open;
            Node root = ParseNode(visible, ref pos, out error);
            if (root == null) return null;

            HgEngineBattleScenario s = new HgEngineBattleScenario { _text = text, _root = root };
            s.Load(text);
            return s;
        }

        private static Node ParseNode(string v, ref int pos, out string error)
        {
            error = null;
            Node node = new Node { Open = pos };
            pos++;
            while (true)
            {
                SkipSpace(v, ref pos);
                if (pos >= v.Length) { error = "A { is never closed."; return null; }
                if (v[pos] == '}') { node.Close = pos; pos++; return node; }

                Item item = new Item();
                Match d = Regex.Match(v.Substring(pos, Math.Min(200, v.Length - pos)), @"^((?:\.\w+(?:\[\d+\])?)+)\s*=\s*");
                if (d.Success) { item.Designator = d.Groups[1].Value; pos += d.Length; }
                SkipSpace(v, ref pos);
                item.ValueStart = pos;
                if (pos < v.Length && v[pos] == '{')
                {
                    item.Child = ParseNode(v, ref pos, out error);
                    if (item.Child == null) return null;
                    item.ValueEnd = pos;
                }
                else
                {
                    while (pos < v.Length && v[pos] != ',' && v[pos] != '}')
                    {
                        if (v[pos] == '"') SkipString(v, ref pos);
                        else if (v[pos] == '(') SkipParens(v, ref pos);
                        else pos++;
                    }
                    item.ValueEnd = pos;
                    while (item.ValueEnd > item.ValueStart && char.IsWhiteSpace(v[item.ValueEnd - 1])) item.ValueEnd--;
                }
                item.End = item.ValueEnd;
                node.Items.Add(item);
                SkipSpace(v, ref pos);
                if (pos < v.Length && v[pos] == ',') pos++;
            }
        }

        private static void SkipSpace(string v, ref int pos) { while (pos < v.Length && char.IsWhiteSpace(v[pos])) pos++; }
        private static void SkipString(string v, ref int pos)
        {
            pos++;
            while (pos < v.Length && v[pos] != '"') pos += v[pos] == '\\' ? 2 : 1;
            pos++;
        }
        private static void SkipParens(string v, ref int pos)
        {
            int depth = 0;
            do
            {
                if (v[pos] == '(') depth++;
                else if (v[pos] == ')') depth--;
                pos++;
            } while (pos < v.Length && depth > 0);
        }

        private string ValueOf(Item i) => i == null || i.Child != null ? "" : _text.Substring(i.ValueStart, i.ValueEnd - i.ValueStart);
        private static Item Find(Node n, string designator) => n?.Items.FirstOrDefault(i => i.Designator == designator);

        private void Load(string text)
        {
            BattleType = ValueOf(Find(_root, ".battleType"));
            Weather = ValueOf(Find(_root, ".weather"));
            FieldCondition = ValueOf(Find(_root, ".fieldCondition"));
            Terrain = ValueOf(Find(_root, ".terrain"));
            PassCount = ValueOf(Find(_root, ".expectationPassCount"));
            KnownFailing = ValueOf(Find(_root, ".knownFailing"));
            LoadParty(Find(_root, ".playerParty")?.Child, Player);
            LoadParty(Find(_root, ".enemyParty")?.Child, Enemy);
            LoadScript(Find(_root, ".playerScript")?.Child, PlayerScript);
            LoadScript(Find(_root, ".enemyScript")?.Child, EnemyScript);
            Node exp = Find(_root, ".expectations")?.Child;
            if (exp != null)
                foreach (Item e in exp.Items.Where(i => i.Child != null))
                {
                    Item value = e.Child.Items.FirstOrDefault(i => i.Designator != null && i.Designator.StartsWith(".expectationValue"));
                    Expectations.Add(new Expectation
                    {
                        Type = ValueOf(Find(e.Child, ".expectationType")),
                        Battler = ValueOf(Find(e.Child, ".battlerIDOrPartySlot")),
                        Field = value?.Designator?.Split('.').Last() ?? "",
                        Value = value == null ? "" : value.Child != null
                            ? string.Join(", ", value.Child.Items.Select(ValueOf))
                            : ValueOf(value),
                    });
                }
        }

        private void LoadParty(Node party, Mon[] into)
        {
            if (party == null) return;
            for (int slot = 0; slot < Math.Min(PartySize, party.Items.Count); slot++)
            {
                Node m = party.Items[slot].Child;
                if (m == null) continue;
                Mon mon = into[slot];
                mon.Species = ValueOf(Find(m, ".species"));
                mon.Level = ValueOf(Find(m, ".level"));
                mon.Form = ValueOf(Find(m, ".form"));
                mon.Ability = ValueOf(Find(m, ".ability"));
                mon.Item = ValueOf(Find(m, ".item"));
                mon.Hp = ValueOf(Find(m, ".hp"));
                mon.Status = ValueOf(Find(m, ".status"));
                mon.Condition2 = ValueOf(Find(m, ".condition2"));
                mon.MoveEffectFlags = ValueOf(Find(m, ".moveEffectFlags"));
                Node moves = Find(m, ".moves")?.Child;
                if (moves != null)
                    for (int k = 0; k < Math.Min(Moves, moves.Items.Count); k++) mon.Moves[k] = ValueOf(moves.Items[k]);
            }
        }

        private void LoadScript(Node script, Action[][] into)
        {
            if (script == null) return;
            for (int b = 0; b < Math.Min(ScriptBattlers, script.Items.Count); b++)
            {
                Node turns = script.Items[b].Child;
                if (turns == null) continue;
                for (int t = 0; t < Math.Min(ScriptLength, turns.Items.Count); t++)
                {
                    Node a = turns.Items[t].Child;
                    if (a == null || a.Items.Count < 2) continue;
                    into[b][t].Do = ValueOf(a.Items[0]);
                    into[b][t].Target = ValueOf(a.Items[1]);
                }
            }
        }

        // ── Writing ──────────────────────────────────────────────────────────

        /// <summary>
        /// The test's text with this scenario's values. A value that changed is replaced where it stands; a field the
        /// test doesn't have yet is added before its block's closing brace; everything else is left as it was.
        /// </summary>
        public string Write()
        {
            HgEngineBattleScenario was = Read(_text, out _);
            List<(int At, int Length, string With)> edits = new List<(int, int, string)>();

            Set(edits, _root, ".battleType", was.BattleType, BattleType);
            Set(edits, _root, ".weather", was.Weather, Weather);
            Set(edits, _root, ".fieldCondition", was.FieldCondition, FieldCondition);
            Set(edits, _root, ".terrain", was.Terrain, Terrain);
            Set(edits, _root, ".expectationPassCount", was.PassCount, PassCount);
            Set(edits, _root, ".knownFailing", was.KnownFailing, KnownFailing);
            WriteParty(edits, ".playerParty", was.Player, Player);
            WriteParty(edits, ".enemyParty", was.Enemy, Enemy);
            WriteScript(edits, ".playerScript", was.PlayerScript, PlayerScript);
            WriteScript(edits, ".enemyScript", was.EnemyScript, EnemyScript);
            WriteExpectations(edits, was.Expectations);

            StringBuilder sb = new StringBuilder(_text);
            foreach ((int at, int length, string with) in edits.OrderByDescending(e => e.At))
            {
                sb.Remove(at, length);
                sb.Insert(at, with);
            }
            return sb.ToString();
        }

        private void Set(List<(int, int, string)> edits, Node node, string designator, string before, string now)
        {
            now = (now ?? "").Trim();
            if (node == null || now == (before ?? "").Trim()) return;
            Item item = Find(node, designator);
            if (item != null && item.Child == null) { edits.Add((item.ValueStart, item.ValueEnd - item.ValueStart, now)); return; }
            if (item == null && now.Length > 0) edits.Add(InsertField(node, $"{designator} = {now}"));
        }

        // A new field goes after the block's last item, on its own line with the siblings' indent.
        private (int, int, string) InsertField(Node node, string field)
        {
            if (node.Items.Count == 0) return (node.Close, 0, " " + field + " ");
            Item last = node.Items[^1];
            string indent = IndentAt(last.ValueStart, last.Designator);
            int after = last.ValueEnd;
            bool comma = after < _text.Length && NextNonSpace(after) == ',';
            int at = comma ? _text.IndexOf(',', after) + 1 : after;
            return (at, 0, (comma ? "" : ",") + "\n" + indent + field + ",");
        }

        private char NextNonSpace(int from)
        {
            while (from < _text.Length && char.IsWhiteSpace(_text[from])) from++;
            return from < _text.Length ? _text[from] : '\0';
        }

        private string IndentAt(int valueStart, string designator)
        {
            int lineStart = _text.LastIndexOf('\n', Math.Max(0, valueStart - 1)) + 1;
            string lead = Regex.Match(_text.Substring(lineStart), @"^[ \t]*").Value;
            return lead;
        }

        private void WriteParty(List<(int, int, string)> edits, string designator, Mon[] before, Mon[] now)
        {
            Item partyItem = Find(_root, designator);
            for (int slot = 0; slot < PartySize; slot++)
            {
                Mon b = before[slot], n = now[slot];
                Node m = partyItem?.Child != null && slot < partyItem.Child.Items.Count ? partyItem.Child.Items[slot].Child : null;
                if (m == null)
                {
                    if (string.IsNullOrWhiteSpace(n.Species) || n.Species == b.Species) continue;
                    // The party is written out in full only when the test has no party block at all.
                    if (partyItem?.Child == null) { edits.Add(InsertField(_root, $"{designator} = {{ {MonText(n)} }}")); return; }
                    edits.Add(InsertElement(partyItem.Child, $"{{ {MonText(n)} }}"));
                    continue;
                }
                Set(edits, m, ".species", b.Species, n.Species);
                Set(edits, m, ".level", b.Level, n.Level);
                Set(edits, m, ".form", b.Form, n.Form);
                Set(edits, m, ".ability", b.Ability, n.Ability);
                Set(edits, m, ".item", b.Item, n.Item);
                Set(edits, m, ".hp", b.Hp, n.Hp);
                Set(edits, m, ".status", b.Status, n.Status);
                Set(edits, m, ".condition2", b.Condition2, n.Condition2);
                Set(edits, m, ".moveEffectFlags", b.MoveEffectFlags, n.MoveEffectFlags);
                if (!b.Moves.SequenceEqual(n.Moves))
                {
                    Item moves = Find(m, ".moves");
                    string text = "{ " + string.Join(", ", n.Moves.Select(x => string.IsNullOrWhiteSpace(x) ? "MOVE_NONE" : x.Trim())) + " }";
                    if (moves != null) edits.Add((moves.ValueStart, moves.ValueEnd - moves.ValueStart, text));
                    else edits.Add(InsertField(m, ".moves = " + text));
                }
            }
        }

        private static string MonText(Mon m)
        {
            List<string> f = new List<string> { $".species = {m.Species.Trim()}" };
            void Add(string name, string v) { if (!string.IsNullOrWhiteSpace(v)) f.Add($".{name} = {v.Trim()}"); }
            Add("level", m.Level); Add("form", m.Form); Add("ability", m.Ability); Add("item", m.Item);
            if (m.Moves.Any(x => !string.IsNullOrWhiteSpace(x)))
                f.Add(".moves = { " + string.Join(", ", m.Moves.Select(x => string.IsNullOrWhiteSpace(x) ? "MOVE_NONE" : x.Trim())) + " }");
            Add("hp", m.Hp); Add("status", m.Status); Add("condition2", m.Condition2); Add("moveEffectFlags", m.MoveEffectFlags);
            return string.Join(", ", f);
        }

        private (int, int, string) InsertElement(Node array, string element)
        {
            if (array.Items.Count == 0) return (array.Close, 0, " " + element + " ");
            Item last = array.Items[^1];
            string indent = IndentAt(last.ValueStart, null);
            int after = last.ValueEnd;
            bool comma = NextNonSpace(after) == ',';
            int at = comma ? _text.IndexOf(',', after) + 1 : after;
            return (at, 0, (comma ? "" : ",") + "\n" + indent + element + ",");
        }

        private void WriteScript(List<(int, int, string)> edits, string designator, Action[][] before, Action[][] now)
        {
            bool changed = Enumerable.Range(0, ScriptBattlers).Any(b => Enumerable.Range(0, ScriptLength)
                .Any(t => before[b][t].Do.Trim() != now[b][t].Do.Trim() || before[b][t].Target.Trim() != now[b][t].Target.Trim()));
            if (!changed) return;
            Item script = Find(_root, designator);
            if (script?.Child != null)
            {
                for (int b = 0; b < ScriptBattlers; b++)
                {
                    Node turns = b < script.Child.Items.Count ? script.Child.Items[b].Child : null;
                    for (int t = 0; t < ScriptLength; t++)
                    {
                        Action was = before[b][t], act = now[b][t];
                        if (was.Do.Trim() == act.Do.Trim() && was.Target.Trim() == act.Target.Trim()) continue;
                        Node a = turns != null && t < turns.Items.Count ? turns.Items[t].Child : null;
                        string text = $"{{ {Or(act.Do, "ACTION_NONE")}, {Or(act.Target, "0")} }}";
                        if (a != null) edits.Add((turns.Items[t].ValueStart, turns.Items[t].ValueEnd - turns.Items[t].ValueStart, text));
                        else if (turns != null) edits.Add(InsertElement(turns, text));
                    }
                }
                return;
            }
            string all = "{ " + string.Join(", ", now.Select(side => "{ " + string.Join(", ",
                side.Select(a => $"{{ {Or(a.Do, "ACTION_NONE")}, {Or(a.Target, "0")} }}")) + " }")) + " }";
            edits.Add(InsertField(_root, $"{designator} = {all}"));
        }

        private static string Or(string v, string fallback) => string.IsNullOrWhiteSpace(v) ? fallback : v.Trim();

        private void WriteExpectations(List<(int, int, string)> edits, List<Expectation> before)
        {
            Item list = Find(_root, ".expectations");
            List<Item> rows = list?.Child?.Items.Where(i => i.Child != null).ToList() ?? new List<Item>();
            for (int k = 0; k < Expectations.Count; k++)
            {
                Expectation n = Expectations[k], b = k < before.Count ? before[k] : null;
                if (b != null && b.Type == n.Type && b.Battler == n.Battler && b.Field == n.Field && b.Value == n.Value) continue;
                string text = ExpectationText(n);
                if (k < rows.Count) edits.Add((rows[k].ValueStart, rows[k].ValueEnd - rows[k].ValueStart, text));
                else if (list?.Child != null) edits.Add(InsertElement(list.Child, text));
                else { edits.Add(InsertField(_root, ".expectations = { " + string.Join(", ", Expectations.Select(ExpectationText)) + " }")); return; }
            }
            // Rows taken off the end go in one cut, from the end of the last kept row to the end of the last row.
            if (Expectations.Count < rows.Count)
            {
                int from = Expectations.Count == 0 ? rows[0].ValueStart : rows[Expectations.Count - 1].ValueEnd;
                int to = rows[^1].ValueEnd;
                if (Expectations.Count == 0 && NextNonSpace(to) == ',') to = _text.IndexOf(',', to) + 1;
                edits.Add((from, to - from, ""));
            }
        }

        private static string ExpectationText(Expectation e)
        {
            List<string> f = new List<string> { $".expectationType = {Or(e.Type, "EXPECTATION_TYPE_MESSAGE")}" };
            if (!string.IsNullOrWhiteSpace(e.Battler)) f.Add($".battlerIDOrPartySlot = {e.Battler.Trim()}");
            if (!string.IsNullOrWhiteSpace(e.Field))
            {
                string field = e.Field.Trim();
                bool list = field == "hpTaken" || field == "hpRecovered" || field == "types";
                f.Add($".expectationValue.{field} = " + (list ? "{ " + e.Value.Trim() + " }" : e.Value.Trim()));
            }
            return "{ " + string.Join(", ", f) + " }";
        }
    }
}

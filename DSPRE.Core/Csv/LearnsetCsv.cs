using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;

namespace DSPRE.Csv
{
    /// <summary>Level-up moves as one row per move, in learnset order. The file for every Pokémon names the Pokémon on
    /// each row; the file for one Pokémon leaves those columns out. A Pokémon in the file gets exactly its rows.</summary>
    public sealed class LearnsetCsv : CsvImporter
    {
        public readonly record struct Move(int Level, int MoveId);

        public sealed class Entry
        {
            public Entry(int id, Move move) { Id = id; Move = move; }
            public int Id { get; }
            public Move Move { get; }
        }

        private static readonly string[] AllColumns = { "Pokemon ID", "Pokemon Name", "Level", "Move ID", "Move Name" };
        private static readonly string[] OneColumns = { "Level", "Move ID", "Move Name" };

        private readonly string[] _columns;
        private readonly int _single, _offset;
        private readonly CsvNames _pokemon, _moves;
        private readonly int _count, _lastMove;
        private readonly Func<int, (List<Move> Moves, string Error)> _current;
        private readonly Dictionary<int, (List<Move> Moves, string Error)> _bases = new Dictionary<int, (List<Move>, string)>();

        /// <param name="single">The one Pokémon the file is for, or -1 for a file that names each row's Pokémon.</param>
        /// <param name="count">How many Pokémon have a learnset.</param>
        /// <param name="current">The Pokémon's learnset as the editor holds it now.</param>
        public LearnsetCsv(string[] pokemon, string[] moves, int count, int single, Func<int, (List<Move> Moves, string Error)> current)
        {
            _single = single;
            _columns = single >= 0 ? OneColumns : AllColumns;
            _offset = single >= 0 ? -2 : 0;
            _pokemon = new CsvNames(pokemon, "Pokémon");
            _moves = new CsvNames(moves, "move");
            _count = count;
            // The game's entries hold the move in 9 bits; hg-engine's hold 16.
            _lastMove = Math.Min(moves.Length - 1, LearnsetData.UsesWideEntries ? 0xFFFE : (1 << LearnsetData.bitsMove) - 1);
            _current = current;
        }

        public override string Title => _single >= 0 ? "Learnset" : "Learnsets";
        public override IReadOnlyList<string> Columns => _columns;

        public static void Write(TextWriter writer, string[] pokemon, string[] moves, IEnumerable<(int Id, IReadOnlyList<Move> Moves)> learnsets, bool single)
        {
            writer.WriteLine(CsvText.Row(single ? OneColumns : AllColumns));
            foreach ((int id, IReadOnlyList<Move> list) in learnsets)
                foreach (Move m in list)
                {
                    string move = m.MoveId >= 0 && m.MoveId < moves.Length ? moves[m.MoveId] : "";
                    writer.WriteLine(single ? CsvText.Row(m.Level, m.MoveId, move)
                        : CsvText.Row(id, id >= 0 && id < pokemon.Length ? pokemon[id] : "", m.Level, m.MoveId, move));
                }
        }

        private (List<Move> Moves, string Error) Base(int id)
        {
            if (!_bases.TryGetValue(id, out (List<Move> Moves, string Error) b)) _bases[id] = b = _current(id);
            return b;
        }

        public override void Check(CsvCheck c)
        {
            int id = _single;
            if (_single < 0)
            {
                if (!c.NumberAndName(0, 1, _pokemon, out id)) return;
                if (id >= _count) { c.Error(0, $"Pokémon {id} has no learnset. The last one is {_count - 1}."); return; }
            }
            c.Record.Label = $"#{id} {_pokemon[id]}";
            string error = Base(id).Error;
            if (error != null) { c.Error(-1, error); return; }

            bool ok = c.Number(2 + _offset, 0, 100, out int level);
            if (c.NumberAndName(3 + _offset, 4 + _offset, _moves, out int move))
            {
                if (move == 0) { c.Error(4 + _offset, "Pick a move."); ok = false; }
                else if (move > _lastMove) { c.Error(3 + _offset, $"Learnsets can't hold moves above {_lastMove}."); ok = false; }
            }
            else ok = false;
            if (ok) c.Record.Value = new Entry(id, new Move(level, move));
        }

        public override void CheckTogether(IReadOnlyList<CsvRecord> records)
        {
            Dictionary<(int, Move), CsvRecord> seen = new Dictionary<(int, Move), CsvRecord>();
            Dictionary<int, int> count = new Dictionary<int, int>();
            foreach (CsvRecord r in records)
            {
                if (r.Value is not Entry e) continue;
                if (seen.TryGetValue((e.Id, e.Move), out CsvRecord first))
                {
                    On(r).Warn(-1, $"Duplicate of line {first.Line}. Only one is kept.");
                    continue;
                }
                seen[(e.Id, e.Move)] = r;
                count[e.Id] = count.TryGetValue(e.Id, out int n) ? n + 1 : 1;
                if (!LearnsetData.UsesWideEntries && count[e.Id] == LearnsetData.VanillaLimit + 1)
                    On(r).Warn(-1, $"{r.Label} has more than {LearnsetData.VanillaLimit} level-up moves, over the vanilla limit.");
            }
        }

        /// <summary>Each Pokémon's moves in row order, repeats kept once.</summary>
        public Dictionary<int, List<Move>> Result(IReadOnlyList<CsvRecord> accepted)
        {
            Dictionary<int, List<Move>> result = new Dictionary<int, List<Move>>();
            foreach (CsvRecord r in accepted)
            {
                Entry e = (Entry)r.Value;
                if (!result.TryGetValue(e.Id, out List<Move> list)) result[e.Id] = list = new List<Move>();
                if (!list.Contains(e.Move)) list.Add(e.Move);
            }
            return result;
        }

        public override IReadOnlyList<CsvChange> Changes(IReadOnlyList<CsvRecord> accepted)
        {
            List<CsvChange> changes = new List<CsvChange>();
            foreach ((int id, List<Move> now) in Result(accepted).OrderBy(kv => kv.Key))
            {
                List<Move> was = Base(id).Moves ?? new List<Move>();
                if (was.SequenceEqual(now)) continue;
                List<string> lost = was.Except(now).Select(Describe).ToList(), gained = now.Except(was).Select(Describe).ToList();
                string label = $"#{id} {_pokemon[id]}";
                if (lost.Count == 0 && gained.Count == 0) changes.Add(new CsvChange(label, "Order", "", "Moves reordered"));
                if (lost.Count > 0) changes.Add(new CsvChange(label, "Removed", string.Join("; ", lost), ""));
                if (gained.Count > 0) changes.Add(new CsvChange(label, "Added", "", string.Join("; ", gained)));
            }
            return changes;
        }

        private string Describe(Move m) => $"Lv. {m.Level} {_moves[m.MoveId]}";
    }
}

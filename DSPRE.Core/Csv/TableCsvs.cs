using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DSPRE.ROMFiles;
using static DSPRE.MoveData;

namespace DSPRE.Csv
{
    /// <summary>Move data as one row per move. The name column is only checked; move names are text.</summary>
    public sealed class MoveDataCsv : CsvImporter
    {
        public sealed class Entry
        {
            public Entry(MoveDataImportEntry move) { Move = move; }
            public MoveDataImportEntry Move { get; }
        }

        private const int Id = 0, Name = 1, Type = 2, Split = 3, Power = 4, Accuracy = 5, Priority = 6, Effect = 7, Pp = 8, Range = 9;
        private static readonly string[] ColumnNames = { "Move ID", "Move Name", "Move Type", "Move Split", "Power", "Accuracy", "Priority", "Side Effect Probability", "PP", "Range" };
        private static readonly string[] SplitNames = Enum.GetNames<MoveSplit>();
        private static readonly string[] RangeNames = AttackRangeDescriptions.Select(r => r.name).ToArray();

        private readonly string[] _typeNames;
        private readonly CsvNames _moves, _types, _splits, _ranges;
        private readonly Func<int, (MoveData Move, string Error)> _current;
        private readonly Dictionary<int, (MoveData Move, string Error)> _bases = new Dictionary<int, (MoveData, string)>();

        /// <param name="current">The move as the editor holds it now, staged imports included.</param>
        public MoveDataCsv(string[] moveNames, string[] typeNames, Func<int, (MoveData Move, string Error)> current)
        {
            _typeNames = typeNames;
            _moves = new CsvNames(moveNames, "move");
            _types = new CsvNames(typeNames, "type");
            _splits = new CsvNames(SplitNames, "split");
            _ranges = new CsvNames(RangeNames, "range");
            _current = current;
        }

        public override string Title => "Move data";
        public override IReadOnlyList<string> Columns => ColumnNames;

        public static void Write(TextWriter writer, string[] moveNames, string[] typeNames, IEnumerable<(int Id, MoveData Move)> moves)
        {
            writer.WriteLine(CsvText.Row(ColumnNames));
            foreach ((int id, MoveData m) in moves)
                writer.WriteLine(CsvText.Row(Cells(moveNames, typeNames, id, m)));
        }

        private static object[] Cells(string[] moveNames, string[] typeNames, int id, MoveData m) => new object[]
        {
            id, id < moveNames.Length ? moveNames[id] : "", (int)m.movetype < typeNames.Length ? typeNames[(int)m.movetype] : ((int)m.movetype).ToString(),
            m.split, m.damage, m.accuracy, m.priority, m.sideEffectProbability, m.pp, GetAttackRangeName(m.target),
        };

        private (MoveData Move, string Error) Base(int id)
        {
            if (!_bases.TryGetValue(id, out (MoveData Move, string Error) b)) _bases[id] = b = _current(id);
            return b;
        }

        public override void Check(CsvCheck c)
        {
            if (!c.NumberAndName(Id, Name, _moves, out int id)) return;
            c.Record.Label = $"#{id} {_moves[id]}";
            string error = Base(id).Error;
            if (error != null) { c.Error(-1, error); return; }

            bool ok = c.Name(Type, _types, out int type) & c.Name(Split, _splits, out int split)
                & c.Number(Power, 0, 255, out int power) & c.Number(Accuracy, 0, 255, out int accuracy)
                & c.Number(Priority, sbyte.MinValue, sbyte.MaxValue, out int priority) & c.Number(Effect, 0, 255, out int effect)
                & c.Number(Pp, 0, 255, out int pp) & c.Name(Range, _ranges, out int range);
            if (!ok) return;
            c.Record.Value = new Entry(new MoveDataImportEntry
            {
                MoveID = id, MoveName = _moves[id], MoveType = (PokemonType)type, Split = (MoveSplit)split, Power = (byte)power,
                Accuracy = (byte)accuracy, Priority = (sbyte)priority, SideEffectProbability = (byte)effect, PP = (byte)pp,
                Range = AttackRangeDescriptions[range].value,
            });
        }

        public override void CheckTogether(IReadOnlyList<CsvRecord> records) => CsvRules.OneRowPerRecord(records, r => (r.Value as Entry)?.Move.MoveID ?? -1, "move", On);

        public override IReadOnlyList<CsvChange> Changes(IReadOnlyList<CsvRecord> accepted)
        {
            List<CsvChange> changes = new List<CsvChange>();
            string[] names = Enumerable.Range(0, _moves.Count).Select(i => _moves[i]).ToArray();
            foreach (CsvRecord r in accepted)
            {
                MoveDataImportEntry e = ((Entry)r.Value).Move;
                MoveData was = Base(e.MoveID).Move;
                MoveData now = new MoveData(new MemoryStream(was.ToByteArray()))
                {
                    movetype = e.MoveType, split = e.Split, damage = e.Power, accuracy = e.Accuracy, priority = e.Priority,
                    sideEffectProbability = e.SideEffectProbability, pp = e.PP, target = e.Range,
                };
                CsvRules.Compare(changes, r.Label, ColumnNames, Type, Cells(names, _typeNames, e.MoveID, was), Cells(names, _typeNames, e.MoveID, now));
            }
            return changes;
        }
    }

    /// <summary>TM and HM moves and disc colours as one row per machine.</summary>
    public sealed class MachineCsv : CsvImporter
    {
        public sealed class Entry
        {
            public Entry(int machine, int move, int palette) { Machine = machine; Move = move; Palette = palette; }
            public int Machine { get; }
            public int Move { get; }
            public int Palette { get; }
        }

        private const int Machine = 0, MoveId = 1, MoveName = 2, Palette = 3;
        private static readonly string[] ColumnNames = { "Machine", "Move ID", "Move Name", "Palette ID" };

        private readonly CsvNames _machines, _moves;
        private readonly int[] _moveNow, _paletteNow;
        private readonly bool _palettesKnown;
        private readonly Func<int, bool> _isDiscPalette;

        /// <param name="labels">Each machine's label, such as TM01.</param>
        /// <param name="isDiscPalette">Whether a palette is one of the type colours.</param>
        public MachineCsv(string[] labels, string[] moveNames, int[] moves, int[] palettes, bool palettesKnown, Func<int, bool> isDiscPalette)
        {
            // Machines go by their labels; a bare 5 would otherwise mean the sixth one.
            _machines = new CsvNames(labels, "machine", numbers: false);
            _moves = new CsvNames(moveNames, "move");
            _moveNow = moves;
            _paletteNow = palettes;
            _palettesKnown = palettesKnown;
            _isDiscPalette = isDiscPalette;
        }

        public override string Title => "TM and HM";
        public override IReadOnlyList<string> Columns => ColumnNames;
        public override bool IsOptional(int column) => column == Palette;

        public static void Write(TextWriter writer, string[] labels, string[] moveNames, int[] moves, int[] palettes)
        {
            writer.WriteLine(CsvText.Row(ColumnNames));
            for (int i = 0; i < moves.Length; i++)
                writer.WriteLine(CsvText.Row(labels[i], moves[i], moves[i] >= 0 && moves[i] < moveNames.Length ? moveNames[moves[i]] : "", palettes[i]));
        }

        public override void Check(CsvCheck c)
        {
            // A label copied from the machine list carries its move after a dash.
            string label = c.Text(Machine).Split('-')[0].Trim();
            if (!_machines.TryFind(label, -1, out int machine, out _))
            {
                c.Error(Machine, $"This ROM has no machine {c.Text(Machine)}.",
                    _machines.Closest(label).Select(i => new CsvFix(Machine, _machines[i], $"Use {_machines[i]}")).ToArray());
                return;
            }
            c.Record.Label = _machines[machine];
            bool ok = c.NumberAndName(MoveId, MoveName, _moves, out int move);
            int palette = _paletteNow[machine];
            if (!c.IsBlank(Palette))
            {
                if (!_palettesKnown) c.Warn(Palette, "Palettes can't be changed: a machine has no item row.");
                else if (ok &= c.Number(Palette, 0, ushort.MaxValue, out palette))
                {
                    if (!_isDiscPalette(palette)) c.Warn(Palette, $"Palette {palette} is not one of the type colours.");
                }
            }
            if (ok) c.Record.Value = new Entry(machine, move, _palettesKnown ? palette : _paletteNow[machine]);
        }

        public override void CheckTogether(IReadOnlyList<CsvRecord> records) => CsvRules.OneRowPerRecord(records, r => (r.Value as Entry)?.Machine ?? -1, "machine", On);

        public override IReadOnlyList<CsvChange> Changes(IReadOnlyList<CsvRecord> accepted)
        {
            List<CsvChange> changes = new List<CsvChange>();
            foreach (CsvRecord r in accepted)
            {
                Entry e = (Entry)r.Value;
                if (e.Move != _moveNow[e.Machine]) changes.Add(new CsvChange(r.Label, "Move", _moves[_moveNow[e.Machine]], _moves[e.Move]));
                if (e.Palette != _paletteNow[e.Machine]) changes.Add(new CsvChange(r.Label, "Palette", _paletteNow[e.Machine].ToString(), e.Palette.ToString()));
            }
            return changes;
        }
    }

    /// <summary>Egg moves as one row per move. The file becomes the whole table, Pokémon in the order they first
    /// appear, so a Pokémon left out loses its egg moves. A repeated move stays: retail HGSS repeats Swinub's Mud Shot.</summary>
    public sealed class EggMoveTableCsv : CsvImporter
    {
        public sealed class Entry
        {
            public Entry(int species, int move) { Species = species; Move = move; }
            public int Species { get; }
            public int Move { get; }
        }

        private const int SpeciesId = 0, SpeciesName = 1, MoveId = 2, MoveName = 3;
        private static readonly string[] ColumnNames = { "Species ID", "Species Name", "Move ID", "Move Name" };
        // The table marks each Pokémon with 20000 plus its number, so moves stay below that.
        private const int SpeciesMarker = 20000;

        private readonly CsvNames _pokemon, _moves;
        private readonly List<EggMoveEntry> _now;

        public EggMoveTableCsv(string[] pokemon, string[] moves, List<EggMoveEntry> now)
        {
            _pokemon = new CsvNames(pokemon, "Pokémon");
            _moves = new CsvNames(moves, "move");
            _now = now;
        }

        public override string Title => "Egg moves";
        public override IReadOnlyList<string> Columns => ColumnNames;

        public static void Write(TextWriter writer, string[] pokemon, string[] moves, IEnumerable<EggMoveEntry> table)
        {
            writer.WriteLine(CsvText.Row(ColumnNames));
            foreach (EggMoveEntry e in table)
                foreach (ushort move in e.moveIDs)
                    writer.WriteLine(CsvText.Row(e.speciesID, e.speciesID < pokemon.Length ? pokemon[e.speciesID] : "", move, move < moves.Length ? moves[move] : ""));
        }

        public override void Check(CsvCheck c)
        {
            if (!c.NumberAndName(SpeciesId, SpeciesName, _pokemon, out int species)) return;
            c.Record.Label = $"#{species} {_pokemon[species]}";
            if (!c.NumberAndName(MoveId, MoveName, _moves, out int move)) return;
            if (move == 0) { c.Error(MoveName, "Pick a move."); return; }
            if (move >= SpeciesMarker) { c.Error(MoveId, $"Egg moves can't be above move {SpeciesMarker - 1}."); return; }
            c.Record.Value = new Entry(species, move);
        }

        public List<EggMoveEntry> Result(IReadOnlyList<CsvRecord> accepted)
        {
            List<EggMoveEntry> table = new List<EggMoveEntry>();
            foreach (CsvRecord r in accepted)
            {
                Entry e = (Entry)r.Value;
                int at = table.FindIndex(t => t.speciesID == e.Species);
                if (at < 0) { table.Add(new EggMoveEntry(e.Species, new List<ushort>())); at = table.Count - 1; }
                table[at].moveIDs.Add((ushort)e.Move);
            }
            return table;
        }

        public override IReadOnlyList<CsvChange> Changes(IReadOnlyList<CsvRecord> accepted)
        {
            List<CsvChange> changes = new List<CsvChange>();
            List<EggMoveEntry> next = Result(accepted);
            foreach (EggMoveEntry was in _now)
                if (!next.Any(n => n.speciesID == was.speciesID))
                    changes.Add(new CsvChange($"#{was.speciesID} {_pokemon[was.speciesID]}", "Egg moves", Describe(was.moveIDs), "None, not in the file"));
            foreach (EggMoveEntry now in next)
            {
                List<ushort> was = _now.FirstOrDefault(n => n.speciesID == now.speciesID).moveIDs ?? new List<ushort>();
                if (!was.SequenceEqual(now.moveIDs))
                    changes.Add(new CsvChange($"#{now.speciesID} {_pokemon[now.speciesID]}", "Egg moves", Describe(was), Describe(now.moveIDs)));
            }
            return changes;
        }

        private string Describe(IEnumerable<ushort> moves) => moves.Any() ? string.Join("; ", moves.Select(m => _moves[m])) : "None";
    }

    /// <summary>The dungeon cut-in table, one row per table row. The table has a fixed size, so a row number says which
    /// row a line replaces; without one, lines fill the rows in order.</summary>
    public sealed class DungeonCutinCsv : CsvImporter
    {
        public sealed class Entry
        {
            public Entry(int row, int[] values) { Row = row; Values = values; }
            public int Row { get; }
            public int[] Values { get; }
        }

        private const int RowColumn = 0, Zone = 1;
        private static readonly string[] ColumnNames =
        {
            "Row", "ZoneID", "WipeType",
            "MorningPalette", "MorningTiles", "MorningScreen",
            "NoonPalette", "NoonTiles", "NoonScreen",
            "EveningPalette", "EveningTiles", "EveningScreen",
            "NightPalette", "NightTiles", "NightScreen",
            "NameMessageId",
        };

        private readonly int[][] _now;
        private readonly CsvNames _zones;

        /// <param name="zones">The header names a zone number picks.</param>
        public DungeonCutinCsv(int[][] now, string[] zones)
        {
            _now = now;
            _zones = new CsvNames(zones, "header");
        }

        public override string Title => "Dungeon cut-in";
        public override IReadOnlyList<string> Columns => ColumnNames;
        public override bool IsOptional(int column) => column == RowColumn;

        public static void Write(TextWriter writer, int[][] rows)
        {
            writer.WriteLine(CsvText.Row(ColumnNames));
            for (int i = 0; i < rows.Length; i++)
                writer.WriteLine(CsvText.Row(new object[] { i + 1 }.Concat(rows[i].Cast<object>()).ToArray()));
        }

        public override void Check(CsvCheck c)
        {
            int row = c.Record.Index + 1;
            if (!c.IsBlank(RowColumn) && !c.Number(RowColumn, 1, _now.Length, out row)) return;
            c.Record.Label = $"Row {row}";
            if (row > _now.Length)
            {
                c.Error(-1, $"The table only has {_now.Length} rows.");
                return;
            }
            int[] values = new int[ColumnNames.Length - 1];
            bool ok = true;
            for (int col = Zone; col < ColumnNames.Length; col++)
                ok &= c.Number(col, int.MinValue, int.MaxValue, out values[col - 1]);
            if (ok && (values[0] < 0 || values[0] >= _zones.Count))
                c.Warn(Zone, $"There is no header {values[0]}.");
            if (ok) c.Record.Value = new Entry(row - 1, values);
        }

        public override void CheckTogether(IReadOnlyList<CsvRecord> records) => CsvRules.OneRowPerRecord(records, r => (r.Value as Entry)?.Row ?? -1, "table row", On);

        public override IReadOnlyList<CsvChange> Changes(IReadOnlyList<CsvRecord> accepted)
        {
            List<CsvChange> changes = new List<CsvChange>();
            foreach (CsvRecord r in accepted)
            {
                Entry e = (Entry)r.Value;
                for (int i = 0; i < e.Values.Length; i++)
                    if (e.Values[i] != _now[e.Row][i])
                        changes.Add(new CsvChange(r.Label, ColumnNames[i + 1], _now[e.Row][i].ToString(CultureInfo.InvariantCulture), e.Values[i].ToString(CultureInfo.InvariantCulture)));
            }
            return changes;
        }
    }
}

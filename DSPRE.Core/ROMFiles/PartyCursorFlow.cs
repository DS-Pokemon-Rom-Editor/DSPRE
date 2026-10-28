using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Where the arrow keys take the party menu cursor from each stop, and the slots tried first when it leaves the buttons.
    /// Stop positions belong to <see cref="PartyPanelLayout"/>; only directions and back rows are written here.
    /// </summary>
    public sealed class PartyCursorFlow
    {
        public const int Stops = 8, Pokemon = 6, Confirm = 6, Cancel = 7, Stay = 0xFF;
        public const int BackRows = 4, BackLength = 6;

        public enum Set { Normal, MultiBattle, BattleEntry, EggChoice }

        public static readonly Set[] Sets = { Set.Normal, Set.MultiBattle, Set.BattleEntry, Set.EggChoice };

        // pokeplatinum main.c and pokeheartgold party_menu.c: 8 bytes per stop, the four directions last.
        private const int Entry = 8, DirectionsAt = 4, SetLength = Stops * Entry;
        private const int FlowLength = Stops * 4, BackSize = BackRows * BackLength;

        private readonly ScreenTables _files;
        // Each set's whole table, read once for the button positions.
        private readonly byte[] _tables;
        // The editor changes each set's directions, then the back rows.
        private readonly byte[] _data;

        private PartyCursorFlow(RomInfo.PartyCursorSites sites, ScreenTables files)
        {
            _files = files;
            byte[] arm9 = files.FileOf(ScreenTables.InArm9);
            _tables = sites.Sets.SelectMany(s => arm9.Skip((int)s).Take(SetLength)).ToArray();
            foreach (uint set in sites.Sets)
                for (int stop = 0; stop < Stops; stop++) files.Take(ScreenTables.InArm9, (int)set + stop * Entry + DirectionsAt, 4);
            files.Take(ScreenTables.InArm9, (int)sites.Back, BackSize);
            _data = files.Finish();
        }

        /// <summary>Null when this ROM's tables aren't known or hold values the game's tables never do.</summary>
        public static PartyCursorFlow Read()
        {
            RomInfo.PartyCursorSites sites = RomLocator.PartyCursorSites;
            if (sites == null) return null;
            ScreenTables files = new ScreenTables();
            byte[] arm9 = files.FileOf(ScreenTables.InArm9);
            if (arm9 == null || sites.Sets.Any(s => s + SetLength > arm9.Length) || sites.Back + BackSize > arm9.Length) return null;
            PartyCursorFlow flow = new PartyCursorFlow(sites, files);
            return !files.Missing && flow.Sane() ? flow : null;
        }

        private bool Sane()
        {
            for (int i = 0; i < 4 * FlowLength; i++)
                if (_data[i] >= Stops && _data[i] != Stay) return false;
            return _data.Skip(4 * FlowLength).All(b => b < Pokemon);
        }

        public static string StopName(int stop) => stop < Pokemon ? $"Pokémon {stop + 1}" : stop == Confirm ? "Confirm" : "Cancel";

        public static string SetName(Set set) => set switch
        {
            Set.Normal => "Normal",
            Set.MultiBattle => "Multi battle",
            Set.BattleEntry => "Battle entry",
            _ => RomInfo.GameFamily == RomInfo.GameFamilies.HGSS ? "Spin Trade" : "Egg choice",
        };

        /// <summary>The cursor's centre in pixels, as the ROM's table has it.</summary>
        public (int X, int Y) Position(Set set, int stop)
        {
            int at = (int)set * SetLength + stop * Entry;
            return (_tables[at], _tables[at + 1]);
        }

        /// <summary>Unused stops sit at 0, 0: Confirm outside battle entry, both buttons in the egg choice.</summary>
        public bool Used(Set set, int stop) => stop < Pokemon || Position(set, stop) != (0, 0);

        private static int At(Set set, int stop, CursorDirection direction) => (int)set * FlowLength + stop * 4 + (int)direction;

        /// <summary>A stop, or <see cref="Stay"/>.</summary>
        public int Target(Set set, int stop, CursorDirection direction) => _data[At(set, stop, direction)];

        public void SetTarget(Set set, int stop, CursorDirection direction, int target) => _data[At(set, stop, direction)] = (byte)target;

        /// <summary>Rows 0 and 1 are Down from the buttons, rows 2 and 3 Up; the even row follows a left-column slot.</summary>
        public int Back(int row, int i) => _data[4 * FlowLength + row * BackLength + i];

        public void SetBack(int row, int i, int slot) => _data[4 * FlowLength + row * BackLength + i] = (byte)slot;

        public byte[] ToBytes() => _files.Copy();
        public void FromBytes(byte[] bytes) => _files.Load(bytes);

        /// <summary>The bytes arm9 holds now, in <see cref="ToBytes"/> order.</summary>
        public byte[] FileBytes() => _files.FileBytes();

        /// <summary>Null when the game can run this flow; otherwise the first problem it didn't already have.</summary>
        public string WhyNot() => _files.NewProblem(Problems, _data);

        private IEnumerable<string> Problems(byte[] data)
        {
            foreach (Set set in Sets)
                for (int stop = 0; stop < Stops; stop++)
                {
                    if (!Used(set, stop)) continue;
                    foreach (CursorDirection d in CursorDirections.All)
                    {
                        int target = data[At(set, stop, d)];
                        string from = $"{SetName(set)}: {d} from {StopName(stop)}";
                        if (target == Stay) continue;
                        if (target >= Stops) yield return $"{from} goes to a stop that doesn't exist.";
                        else if (!Used(set, target)) yield return $"{from} goes to {StopName(target)}, but this menu doesn't show it.";
                        else if (Loops(data, set, stop, target, d)) yield return $"{from} can loop through empty slots and freeze the game.";
                    }
                }
            for (int i = 4 * FlowLength; i < data.Length; i++)
                if (data[i] >= Pokemon) yield return "Leaving the buttons can only go to Pokémon 1 to 6.";
        }

        // The game keeps going the same way while it lands on an empty slot; slot 1 and the slot it left are never empty.
        private static bool Loops(byte[] data, Set set, int from, int first, CursorDirection d)
        {
            HashSet<int> seen = new HashSet<int>();
            int at = first;
            while (at > 0 && at < Pokemon && at != from)
            {
                if (!seen.Add(at)) return true;
                at = data[At(set, at, d)];
            }
            return false;
        }

        /// <summary>Null when the bytes can be written.</summary>
        public string WhyNotWrite(byte[] bytes) =>
            bytes == null || bytes.Length != _data.Length ? "The cursor tables don't match this ROM." : _files.NewProblem(Problems, bytes);

        public void WriteFile(byte[] bytes)
        {
            if (WhyNotWrite(bytes) is string why) throw new InvalidOperationException(why);
            _files.Write(bytes);
        }
    }
}

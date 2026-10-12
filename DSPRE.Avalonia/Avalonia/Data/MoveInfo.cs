using System;
using System.Collections.Generic;

namespace DSPRE.Avalonia.Data
{
    /// <summary>A move's type, category and battle numbers as the ROM has them, read once per move.</summary>
    internal static class MoveInfo
    {
        public readonly record struct Facts(int Type, int Category, int Power, int Accuracy, int Pp);

        private static readonly Dictionary<int, Facts?> Cache = new();
        private static string _cachedFor;

        /// <summary>Raised after a move's data is saved, so shown icons and numbers can be read again.</summary>
        public static event EventHandler Changed;

        public static Facts? Of(int move)
        {
            if (move <= 0) return null;
            string rom = RomInfo.workDir;
            if (_cachedFor != rom)
            {
                Cache.Clear(); _cachedFor = rom;
                try { DSUtils.TryUnpackNarcs(new List<RomInfo.DirNames> { RomInfo.DirNames.moveData }); } catch { }
            }
            if (Cache.TryGetValue(move, out Facts? known)) return known;
            Facts? facts = null;
            try
            {
                MoveData data = new MoveData(move);
                facts = new Facts((int)data.movetype, (int)data.split, data.damage, data.accuracy, data.pp);
            }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException || e is ArgumentException || e is KeyNotFoundException) { }
            Cache[move] = facts;
            return facts;
        }

        private static Dictionary<int, int> _taughtBy;
        private static string _taughtFor;

        /// <summary>The move a TM or HM item teaches, or 0 for any other item.</summary>
        public static int TaughtByItem(int item)
        {
            string rom = RomInfo.workDir;
            if (_taughtBy == null || _taughtFor != rom)
            {
                Dictionary<int, int> map = new Dictionary<int, int>();
                try
                {
                    int[] moves = TMEditor.ReadMachineMoves();
                    for (int m = 0; m < moves.Length; m++) map[TMEditor.MachineItemId(m)] = moves[m];
                }
                catch (Exception e) when (e is System.IO.IOException || e is InvalidOperationException || e is IndexOutOfRangeException) { }
                _taughtBy = map; _taughtFor = rom;
            }
            return _taughtBy.TryGetValue(item, out int move) ? move : 0;
        }

        public static void Forget()
        {
            Cache.Clear();
            _taughtBy = null;
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }
}

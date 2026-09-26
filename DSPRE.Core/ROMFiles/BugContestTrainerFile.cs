using System;
using System.Collections.Generic;
using System.IO;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// The Bug-Catching Contest opponents in data/mushi/mushi_trainer.bin (HGSS): ten opponents with eight
    /// rows each. The game draws five opponents, picks one of each one's rows allowed that day, and scores it
    /// nominal + (random % (2 * variation)) - variation.
    /// </summary>
    public class BugContestTrainerFile
    {
        public const int Opponents = 10, RowsPerOpponent = 8, RowSize = 8;
        public const int Size = Opponents * RowsPerOpponent * RowSize;

        /// <summary>A day value at or above this is allowed every day.</summary>
        public const byte AnyDay = 7;

        /// <summary>The results screen prints scores with three digits.</summary>
        public const int HighestShownScore = 999;

        public class Row
        {
            /// <summary>Nonzero: only offered once the player has the National Dex.</summary>
            public byte NationalDex;
            /// <summary>0-6 is Sunday to Saturday; 7 and above is any day.</summary>
            public byte Day;
            public ushort Species;
            public ushort Score;
            public ushort Variation;

            public bool AllowedOn(int day, bool hasNationalDex) =>
                (NationalDex == 0 || hasNationalDex) && (Day >= AnyDay || Day == day);

            public int LowestScore => Score - Variation;
            public int HighestScore => Score + Variation - 1;
        }

        public Row[,] Rows { get; } = new Row[Opponents, RowsPerOpponent];

        // Anything past the 80 rows the game reads is kept as it was.
        private readonly byte[] _tail;

        public BugContestTrainerFile(byte[] data)
        {
            if (data == null || data.Length < Size)
                throw new InvalidDataException($"The opponent file is {data?.Length ?? 0} bytes; it needs {Size}.");

            for (int o = 0; o < Opponents; o++)
                for (int r = 0; r < RowsPerOpponent; r++)
                {
                    int at = (o * RowsPerOpponent + r) * RowSize;
                    Rows[o, r] = new Row
                    {
                        NationalDex = data[at],
                        Day = data[at + 1],
                        Species = BitConverter.ToUInt16(data, at + 2),
                        Score = BitConverter.ToUInt16(data, at + 4),
                        Variation = BitConverter.ToUInt16(data, at + 6),
                    };
                }
            _tail = data.AsSpan(Size).ToArray();
        }

        public static BugContestTrainerFile Load(string path) => new BugContestTrainerFile(File.ReadAllBytes(path));

        public byte[] ToBytes()
        {
            var data = new byte[Size + _tail.Length];
            for (int o = 0; o < Opponents; o++)
                for (int r = 0; r < RowsPerOpponent; r++)
                {
                    int at = (o * RowsPerOpponent + r) * RowSize;
                    var row = Rows[o, r];
                    data[at] = row.NationalDex;
                    data[at + 1] = row.Day;
                    BitConverter.GetBytes(row.Species).CopyTo(data, at + 2);
                    BitConverter.GetBytes(row.Score).CopyTo(data, at + 4);
                    BitConverter.GetBytes(row.Variation).CopyTo(data, at + 6);
                }
            _tail.CopyTo(data, Size);
            return data;
        }

        public void Save(string path) => File.WriteAllBytes(path, ToBytes());

        /// <summary>The opponents' names from the contest text, or numbered labels where that text is unknown.</summary>
        public static string[] OpponentNames()
        {
            var names = new string[Opponents];
            List<string> lines = null;
            try { if (RomInfo.BugContestTextNumber >= 0) lines = new TextArchive(RomInfo.BugContestTextNumber).messages; }
            catch (Exception e) when (e is IOException || e is InvalidDataException) { }

            for (int o = 0; o < Opponents; o++)
            {
                string line = lines != null && FirstNameLine + o < lines.Count ? lines[FirstNameLine + o] : null;
                var match = line == null ? null : System.Text.RegularExpressions.Regex.Match(line, @"^\{TRAINER_NAME:(.+)\}$");
                names[o] = match != null && match.Success ? match.Groups[1].Value : $"Opponent {o + 1}";
            }
            return names;
        }

        private const int FirstNameLine = 78;

        public static readonly string[] DayNames = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

        /// <summary>What would make the game divide by zero: a variation of 0, or an opponent with no row allowed
        /// on some day. <paramref name="name"/> labels an opponent by index.</summary>
        public List<string> Problems(Func<int, string> name)
        {
            var problems = new List<string>();
            for (int o = 0; o < Opponents; o++)
            {
                for (int r = 0; r < RowsPerOpponent; r++)
                    if (Rows[o, r].Variation == 0)
                        problems.Add($"{name(o)}, row {r + 1}: variation must be at least 1.");

                foreach (bool hasNationalDex in new[] { false, true })
                {
                    var missing = new List<string>();
                    for (int day = 0; day < DayNames.Length; day++)
                    {
                        bool any = false;
                        for (int r = 0; r < RowsPerOpponent && !any; r++) any = Rows[o, r].AllowedOn(day, hasNationalDex);
                        if (!any) missing.Add(DayNames[day]);
                    }
                    if (missing.Count > 0)
                        problems.Add($"{name(o)} has no row for {string.Join(", ", missing)} " +
                                     (hasNationalDex ? "after the National Dex." : "before the National Dex."));
                }
            }
            return problems;
        }

        /// <summary>Rows whose score can fall below 0 or above what the results screen shows.</summary>
        public List<string> Warnings(Func<int, string> name)
        {
            var warnings = new List<string>();
            for (int o = 0; o < Opponents; o++)
                for (int r = 0; r < RowsPerOpponent; r++)
                {
                    var row = Rows[o, r];
                    if (row.Variation == 0) continue;
                    if (row.LowestScore < 0 || row.HighestScore > HighestShownScore)
                        warnings.Add($"{name(o)}, row {r + 1}: scores {row.LowestScore} to {row.HighestScore} go outside 0 to {HighestShownScore}.");
                }
            return warnings;
        }
    }
}

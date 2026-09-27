using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Move tutors and who can learn them; the counts are compiled in, so moves can be changed but not added.
    /// Pt: overlay 5, 38 moves of 12 bytes, 5-byte mask per Pokémon. HGSS: overlay 1, 52 moves of 4 bytes, 8-byte masks in
    /// waza_oshie.bin. Bit n of a mask is pool move n; rows follow the personal files, species 1-493 then 12 form files.
    /// </summary>
    public class MoveTutorData
    {
        public const int Rows = 505;

        public sealed class Tutor
        {
            public ushort Move;
            public byte[] Costs;   // Platinum: red, blue, yellow, green shards; HGSS: BP
            public int Where;      // Platinum: location; HGSS: which tutor
            internal byte[] Raw;
        }

        public bool Platinum { get; }
        public int PoolSize => Platinum ? 38 : 52;
        private int RecordSize => Platinum ? 12 : 4;
        private int MaskSize => Platinum ? 5 : 8;

        public List<Tutor> Pool { get; } = new List<Tutor>();
        public ulong[] Masks { get; } = new ulong[Rows];

        public static readonly string[] PlatinumPlaces = { "Route 212", "Survival Area", "Snowpoint City" };
        public static readonly string[] HeartGoldTutors = { "Frontier, top left", "Frontier, top right", "Frontier, bottom right", "Headbutt tutor" };
        public string[] Places => Platinum ? PlatinumPlaces : HeartGoldTutors;
        public string[] CostNames => Platinum ? new[] { "Red", "Blue", "Yellow", "Green" } : new[] { "BP" };

        private MoveTutorData(bool platinum) { Platinum = platinum; }

        private static string HgMaskPath => Path.Combine(dataPath, "fielddata", "wazaoshie", "waza_oshie.bin");

        public static string WhyNot()
        {
            if (gameFamily == GameFamilies.DP) return "Diamond and Pearl have no move tutors.";
            bool pt = gameFamily == GameFamilies.Plat;
            if (GameTableFile.WhyNot(GameTable.TutorPool, (pt ? 38 * 12 : 52 * 4)) is string why) return why;
            if (pt) return GameTableFile.WhyNot(GameTable.TutorCompatibility, Rows * 5);
            if (!File.Exists(HgMaskPath)) return "waza_oshie.bin is missing from this project.";
            return new FileInfo(HgMaskPath).Length == Rows * 8 ? null : "waza_oshie.bin isn't the size the game expects.";
        }

        public static MoveTutorData Load()
        {
            if (WhyNot() is string why) throw new InvalidOperationException(why);
            var data = new MoveTutorData(gameFamily == GameFamilies.Plat);
            byte[] pool = GameTableFile.Read(GameTable.TutorPool, data.PoolSize * data.RecordSize);
            byte[] masks = data.Platinum ? GameTableFile.Read(GameTable.TutorCompatibility, Rows * 5) : File.ReadAllBytes(HgMaskPath);
            data.Restore(pool, masks);
            return data;
        }

        /// <summary>Loads bytes <see cref="PoolBytes"/> and <see cref="MaskBytes"/> produced.</summary>
        public void Restore(byte[] pool, byte[] masks)
        {
            Pool.Clear();
            for (int i = 0; i < PoolSize; i++)
            {
                var raw = pool.AsSpan(i * RecordSize, RecordSize).ToArray();
                Pool.Add(Platinum
                    ? new Tutor { Move = BitConverter.ToUInt16(raw, 0), Costs = raw[2..6], Where = (int)BitConverter.ToUInt32(raw, 8), Raw = raw }
                    : new Tutor { Move = BitConverter.ToUInt16(raw, 0), Costs = new[] { raw[2] }, Where = raw[3], Raw = raw });
            }
            for (int r = 0; r < Rows; r++)
            {
                ulong m = 0;
                for (int b = 0; b < MaskSize; b++) m |= (ulong)masks[r * MaskSize + b] << (8 * b);
                Masks[r] = m;
            }
        }

        /// <summary>The mask row for a personal file id, or -1 (eggs have none).</summary>
        public static int RowOf(int personalId) =>
            personalId >= 1 && personalId <= 493 ? personalId - 1
            : personalId >= 496 && personalId < 496 + 12 ? personalId - 3
            : -1;

        public bool Learns(int row, int tutor) => (Masks[row] >> tutor & 1) != 0;

        public void SetLearns(int row, int tutor, bool learns)
        {
            if (learns) Masks[row] |= 1UL << tutor; else Masks[row] &= ~(1UL << tutor);
        }

        public byte[] PoolBytes()
        {
            var data = new byte[PoolSize * RecordSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var t = Pool[i];
                var raw = (byte[])t.Raw.Clone();
                BitConverter.GetBytes(t.Move).CopyTo(raw, 0);
                if (Platinum) { t.Costs.CopyTo(raw, 2); BitConverter.GetBytes((uint)t.Where).CopyTo(raw, 8); }
                else { raw[2] = t.Costs[0]; raw[3] = (byte)t.Where; }
                raw.CopyTo(data, i * RecordSize);
            }
            return data;
        }

        public byte[] MaskBytes()
        {
            var data = new byte[Rows * MaskSize];
            for (int r = 0; r < Rows; r++)
                for (int b = 0; b < MaskSize; b++) data[r * MaskSize + b] = (byte)(Masks[r] >> (8 * b));
            return data;
        }

        /// <summary>Why the tutors can't be saved, or null.</summary>
        public string Problem(int moveCount)
        {
            for (int i = 0; i < PoolSize; i++)
            {
                var t = Pool[i];
                if (t.Move == 0 || t.Move >= moveCount) return $"Tutor move {i + 1}: pick a move.";
                if (t.Where < 0 || t.Where >= Places.Length) return $"Tutor move {i + 1}: pick where it's taught.";
                // The game looks prices up by move and takes the first match.
                int first = Pool.FindIndex(x => x.Move == t.Move);
                if (first != i) return $"Tutor move {i + 1} repeats tutor move {first + 1}; the game only uses the first.";
            }
            return null;
        }

        public void Save(int moveCount)
        {
            if (Problem(moveCount) is string p) throw new InvalidOperationException(p);
            GameTableFile.Write(GameTable.TutorPool, PoolBytes());
            if (Platinum) GameTableFile.Write(GameTable.TutorCompatibility, MaskBytes());
            else File.WriteAllBytes(HgMaskPath, MaskBytes());
        }
    }
}

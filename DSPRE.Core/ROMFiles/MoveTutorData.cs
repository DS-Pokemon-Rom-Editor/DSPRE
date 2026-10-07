using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPRE.HgEngine;
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
        /// <summary>hg-engine: the pool is src/field/move_tutor.c and each species' row its learnsets.json TutorMoves.</summary>
        public bool FromSource { get; private set; }
        private int _sourcePool;
        public int PoolSize => FromSource ? _sourcePool : Platinum ? 38 : 52;
        private int RecordSize => Platinum ? 12 : 4;
        private int MaskSize => Platinum ? 5 : 8;

        public List<Tutor> Pool { get; } = new List<Tutor>();
        public ulong[] Masks { get; private set; } = new ulong[Rows];

        public static readonly string[] PlatinumPlaces = { "Route 212", "Survival Area", "Snowpoint City" };
        public static readonly string[] HeartGoldTutors = { "Frontier, top left", "Frontier, top right", "Frontier, bottom right", "Headbutt tutor" };
        public string[] Places => Platinum ? PlatinumPlaces : HeartGoldTutors;
        public string[] CostNames => Platinum ? new[] { "Red", "Blue", "Yellow", "Green" } : new[] { "BP" };

        private MoveTutorData(bool platinum) { Platinum = platinum; }

        private static string HgMaskPath => Path.Combine(dataPath, "fielddata", "wazaoshie", "waza_oshie.bin");

        private static bool UsesSource => HgEngine.HgEngineProject.IsActive;

        public static string WhyNot()
        {
            if (UsesSource) return null;
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
            if (UsesSource) return LoadSource();
            MoveTutorData data = new MoveTutorData(gameFamily == GameFamilies.Plat);
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
                byte[] raw = pool.AsSpan(i * RecordSize, RecordSize).ToArray();
                Pool.Add(Platinum
                    ? new Tutor { Move = BitConverter.ToUInt16(raw, 0), Costs = raw[2..6], Where = (int)BitConverter.ToUInt32(raw, 8), Raw = raw }
                    : new Tutor { Move = BitConverter.ToUInt16(raw, 0), Costs = new[] { raw[2] }, Where = raw[3], Raw = raw });
            }
            for (int r = 0; r < Masks.Length && (r + 1) * MaskSize <= masks.Length; r++)
            {
                ulong m = 0;
                for (int b = 0; b < MaskSize; b++) m |= (ulong)masks[r * MaskSize + b] << (8 * b);
                Masks[r] = m;
            }
        }

        /// <summary>The mask row for a personal file id, or -1 (eggs have none). hg-engine has a row per species id.</summary>
        public static int RowOf(int personalId) =>
            UsesSource ? (personalId >= 1 ? personalId : -1)
            : personalId >= 1 && personalId <= 493 ? personalId - 1
            : personalId >= 496 && personalId < 496 + 12 ? personalId - 3
            : -1;

        public bool Learns(int row, int tutor) => (Masks[row] >> tutor & 1) != 0;

        public void SetLearns(int row, int tutor, bool learns)
        {
            if (learns) Masks[row] |= 1UL << tutor; else Masks[row] &= ~(1UL << tutor);
        }

        public byte[] PoolBytes()
        {
            byte[] data = new byte[PoolSize * RecordSize];
            for (int i = 0; i < PoolSize; i++)
            {
                Tutor t = Pool[i];
                byte[] raw = (byte[])t.Raw.Clone();
                BitConverter.GetBytes(t.Move).CopyTo(raw, 0);
                if (Platinum) { t.Costs.CopyTo(raw, 2); BitConverter.GetBytes((uint)t.Where).CopyTo(raw, 8); }
                else { raw[2] = t.Costs[0]; raw[3] = (byte)t.Where; }
                raw.CopyTo(data, i * RecordSize);
            }
            return data;
        }

        public byte[] MaskBytes()
        {
            byte[] data = new byte[Masks.Length * MaskSize];
            for (int r = 0; r < Masks.Length; r++)
                for (int b = 0; b < MaskSize; b++) data[r * MaskSize + b] = (byte)(Masks[r] >> (8 * b));
            return data;
        }

        /// <summary>Why the tutors can't be saved, or null.</summary>
        public string Problem(int moveCount)
        {
            for (int i = 0; i < PoolSize; i++)
            {
                Tutor t = Pool[i];
                if (t.Move == 0 || t.Move >= moveCount) return $"Tutor move {i + 1}: pick a move.";
                if (t.Where < 0 || t.Where >= Places.Length) return $"Tutor move {i + 1}: pick where it's taught.";
                // The game looks prices up by move and takes the first match.
                int first = Pool.FindIndex(x => x.Move == t.Move);
                if (first != i) return $"Tutor move {i + 1} repeats tutor move {first + 1}; the game only uses the first.";
            }
            return null;
        }

        // ── hg-engine ────────────────────────────────────────────────────────
        private List<List<int>> _sourceOwn;
        private ulong[] _sourceMasks;

        // A form with no list of its own uses its base species' one, as the build does.
        private static MoveTutorData LoadSource()
        {
            if (!HgEngine.HgEngineMoveTutors.TryRead(out List<HgEngineMoveTutors.Tutor> tutors, out string error)
                || !HgEngine.HgEngineLearnsets.TryGetAllMoveNames(HgEngine.HgEngineLearnsets.TutorMovesField, out Dictionary<int, List<int>> lists, out error))
                throw new InvalidDataException(error);
            if (tutors.Count > 64) throw new InvalidDataException($"{HgEngine.HgEngineMoveTutors.SourceRelPath} has {tutors.Count} tutor moves; DSPRE edits up to 64.");
            MoveTutorData data = new MoveTutorData(false) { FromSource = true, _sourcePool = tutors.Count };
            foreach (HgEngineMoveTutors.Tutor t in tutors)
                data.Pool.Add(new Tutor { Move = (ushort)t.Move, Costs = new[] { (byte)t.Cost }, Where = t.Npc, Raw = new byte[4] });
            Dictionary<int, int> bases = HgEngine.HgEngineLearnsets.FormBases();
            int species = GetPokemonNames().Length;
            data.Masks = new ulong[species];
            data._sourceOwn = new List<List<int>>(species);
            for (int s = 0; s < species; s++)
            {
                List<int> own = lists.TryGetValue(s, out List<int> l) ? l : new List<int>();
                data._sourceOwn.Add(own);
                List<int> effective = own.Count > 0 || !bases.TryGetValue(s, out int b) || !lists.TryGetValue(b, out List<int> inherited) ? own : inherited;
                for (int j = 0; j < tutors.Count; j++)
                    if (effective.Contains(tutors[j].Move)) data.Masks[s] |= 1UL << j;
            }
            data._sourceMasks = (ulong[])data.Masks.Clone();
            return data;
        }

        private void SaveSource()
        {
            List<HgEngineMoveTutors.Tutor> tutors = Pool.Select(t => new HgEngine.HgEngineMoveTutors.Tutor(t.Move, t.Costs[0], t.Where)).ToList();
            if (!HgEngine.HgEngineMoveTutors.TryWrite(tutors, out string error)) throw new IOException(error);
            Dictionary<int, IReadOnlyList<int>> changes = new Dictionary<int, IReadOnlyList<int>>();
            HashSet<int> poolMoves = Pool.Select(t => (int)t.Move).ToHashSet();
            for (int s = 0; s < Masks.Length; s++)
            {
                if (Masks[s] == _sourceMasks[s]) continue;
                // Moves keep their place; a tutor's move goes when unticked and joins the end when ticked.
                HashSet<int> learned = Enumerable.Range(0, Pool.Count).Where(j => Learns(s, j)).Select(j => (int)Pool[j].Move).ToHashSet();
                List<int> list = _sourceOwn[s].Where(m => !poolMoves.Contains(m) || learned.Contains(m)).ToList();
                for (int j = 0; j < Pool.Count; j++)
                    if (Learns(s, j) && !list.Contains(Pool[j].Move)) list.Add(Pool[j].Move);
                changes[s] = list;
            }
            if (changes.Count > 0 && !HgEngine.HgEngineLearnsets.TrySaveMoveNames(HgEngine.HgEngineLearnsets.TutorMovesField, changes, out error))
                throw new IOException(error);
            foreach ((int s, IReadOnlyList<int> list) in changes) _sourceOwn[s] = list.ToList();
            _sourceMasks = (ulong[])Masks.Clone();
        }

        public void Save(int moveCount)
        {
            if (Problem(moveCount) is string p) throw new InvalidOperationException(p);
            if (FromSource) { SaveSource(); return; }
            byte[] pool = PoolBytes();
            // A failed compatibility write puts the pool back, so neither file changes.
            byte[] before = GameTableFile.Read(GameTable.TutorPool, pool.Length);
            GameTableFile.Write(GameTable.TutorPool, pool);
            try
            {
                if (Platinum) GameTableFile.Write(GameTable.TutorCompatibility, MaskBytes());
                else File.WriteAllBytes(HgMaskPath, MaskBytes());
            }
            catch
            {
                GameTableFile.Write(GameTable.TutorPool, before);
                throw;
            }
        }
    }
}

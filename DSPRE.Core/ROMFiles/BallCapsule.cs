using System;
using System.Collections.Generic;
using System.IO;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>Eight {u8 seal, u8 x, u8 y} slots on the capsule board; seal 0 is an empty slot.</summary>
    public sealed class BallCapsule
    {
        public const int Slots = 8, Bytes = Slots * 3;

        /// <summary>The board is a circle on the lower screen; a seal dropped outside it goes back into the case.</summary>
        public const int BoardCentreX = 190, BoardCentreY = 70, BoardRadius = 60;

        public sealed class Placed
        {
            public int Seal { get; set; }
            public int X { get; set; } = BoardCentreX;
            public int Y { get; set; } = BoardCentreY;
        }

        public Placed[] Seals { get; } = new Placed[Slots];

        public BallCapsule()
        {
            for (int i = 0; i < Slots; i++) Seals[i] = new Placed();
        }

        public static bool OnBoard(int x, int y)
        {
            int dx = x - BoardCentreX, dy = y - BoardCentreY;
            return dx * dx + dy * dy <= BoardRadius * BoardRadius;
        }

        public static BallCapsule Read(byte[] data, int at)
        {
            var capsule = new BallCapsule();
            for (int i = 0; i < Slots; i++)
            {
                capsule.Seals[i].Seal = data[at + i * 3];
                capsule.Seals[i].X = data[at + i * 3 + 1];
                capsule.Seals[i].Y = data[at + i * 3 + 2];
            }
            return capsule;
        }

        public void Write(byte[] data, int at)
        {
            for (int i = 0; i < Slots; i++)
            {
                data[at + i * 3] = (byte)Math.Clamp(Seals[i].Seal, 0, 255);
                data[at + i * 3 + 1] = (byte)Math.Clamp(Seals[i].X, 0, 255);
                data[at + i * 3 + 2] = (byte)Math.Clamp(Seals[i].Y, 0, 255);
            }
        }

        public bool IsEmpty => Array.TrueForAll(Seals, s => s.Seal == 0);
    }

    /// <summary>One file of 128 trainer capsules, named by a 1-based number in a party entry. Diamond and Pearl have none.</summary>
    public static class TrainerCapsules
    {
        public const int Count = 128;

        public static bool Available => gameDirs != null && gameDirs.ContainsKey(DirNames.trainerCapsules);

        private static string FilePath()
        {
            DSUtils.TryUnpackNarcs(new List<DirNames> { DirNames.trainerCapsules });
            return Path.Combine(gameDirs[DirNames.trainerCapsules].unpackedDir, "0000");
        }

        public static BallCapsule[] ReadAll()
        {
            var capsules = new BallCapsule[Count];
            byte[] data = Available && File.Exists(FilePath()) ? File.ReadAllBytes(FilePath()) : Array.Empty<byte>();
            for (int i = 0; i < Count; i++)
                capsules[i] = (i + 1) * BallCapsule.Bytes <= data.Length ? BallCapsule.Read(data, i * BallCapsule.Bytes) : new BallCapsule();
            return capsules;
        }

        public static void WriteAll(IReadOnlyList<BallCapsule> capsules)
        {
            if (!Available) throw new InvalidOperationException("This game has no trainer capsules.");
            string path = FilePath();
            byte[] data = File.Exists(path) ? File.ReadAllBytes(path) : new byte[Count * BallCapsule.Bytes];
            if (data.Length < Count * BallCapsule.Bytes) Array.Resize(ref data, Count * BallCapsule.Bytes);
            for (int i = 0; i < Count && i < capsules.Count; i++) capsules[i].Write(data, i * BallCapsule.Bytes);
            File.WriteAllBytes(path, data);
        }
    }
}

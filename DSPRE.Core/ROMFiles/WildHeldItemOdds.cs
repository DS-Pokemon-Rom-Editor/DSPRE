using System;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// Wild held-item odds: a 0-99 roll below <see cref="Row.NoneBelow"/> gives nothing, below <see cref="Row.RareFrom"/>
    /// the common item, else the rare one. Row 2 is for a Compound Eyes lead; a species listing one item twice always holds it.
    /// </summary>
    public class WildHeldItemOdds
    {
        public const int Size = 8;

        public class Row
        {
            public int NoneBelow;
            public int RareFrom;

            public int NonePercent => Math.Clamp(NoneBelow, 0, 100);
            public int CommonPercent => Math.Clamp(RareFrom, 0, 100) - NonePercent;
            public int RarePercent => 100 - Math.Clamp(RareFrom, 0, 100);
        }

        public Row Normal { get; } = new Row();
        public Row CompoundEyes { get; } = new Row();

        public WildHeldItemOdds(byte[] data)
        {
            if (data == null || data.Length < Size) throw new ArgumentException($"The held item odds are {Size} bytes.");
            Normal.NoneBelow = BitConverter.ToUInt16(data, 0);
            Normal.RareFrom = BitConverter.ToUInt16(data, 2);
            CompoundEyes.NoneBelow = BitConverter.ToUInt16(data, 4);
            CompoundEyes.RareFrom = BitConverter.ToUInt16(data, 6);
        }

        public byte[] ToBytes()
        {
            var data = new byte[Size];
            BitConverter.GetBytes((ushort)Normal.NoneBelow).CopyTo(data, 0);
            BitConverter.GetBytes((ushort)Normal.RareFrom).CopyTo(data, 2);
            BitConverter.GetBytes((ushort)CompoundEyes.NoneBelow).CopyTo(data, 4);
            BitConverter.GetBytes((ushort)CompoundEyes.RareFrom).CopyTo(data, 6);
            return data;
        }

        /// <summary>Why the odds can't be saved, or null.</summary>
        public string Problem()
        {
            foreach (var (name, row) in new[] { ("Normal", Normal), ("Compound Eyes", CompoundEyes) })
                if (row.NoneBelow < 0 || row.NoneBelow > row.RareFrom || row.RareFrom > 100)
                    return $"{name}: the three chances must be 0 or more and add up to 100.";
            return null;
        }

        public static string WhyNot() => GameTableFile.WhyNot(RomInfo.GameTable.WildHeldItemOdds, Size);

        public static WildHeldItemOdds Load() => new WildHeldItemOdds(GameTableFile.Read(RomInfo.GameTable.WildHeldItemOdds, Size));

        public void Save()
        {
            if (Problem() is string p) throw new InvalidOperationException(p);
            GameTableFile.Write(RomInfo.GameTable.WildHeldItemOdds, ToBytes());
        }
    }
}

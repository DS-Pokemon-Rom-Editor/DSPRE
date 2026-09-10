namespace DSPRE.ROMFiles
{
    /// <summary>One step of a battle sprite's frame run: which half of the two-frame sheet to show, for how
    /// many ticks, and its pixel shift. A frameNo of -1 ends the run; below -1 it is a counted jump.</summary>
    public readonly struct SpriteFrameSlot
    {
        public int FrameNo { get; }
        public int Duration { get; }
        public int HorizontalShift { get; }
        public int VerticalShift { get; }
        public SpriteFrameSlot(int frameNo, int duration, int horizontalShift, int verticalShift)
        {
            FrameNo = frameNo;
            Duration = duration;
            HorizontalShift = horizontalShift;
            VerticalShift = verticalShift;
        }
    }

    /// <summary>Platinum and HGSS per-species battle sprite record: animation, delays, frame runs and offsets.
    /// The games read this, not pokeanm.</summary>
    public sealed class SpeciesSpriteData
    {
        public const int Size = 89;
        public const int FrameCount = 10;
        private const int FaceSize = 3 + FrameCount * 4;

        public sealed class Face
        {
            public int CryDelay { get; set; }
            public int Animation { get; set; }
            public int StartDelay { get; set; }
            public SpriteFrameSlot[] Frames { get; } = new SpriteFrameSlot[FrameCount];
        }

        public Face Front { get; } = new Face();
        public Face Back { get; } = new Face();
        public int YOffset { get; set; }
        public int ShadowXOffset { get; set; }
        public int ShadowSize { get; set; }

        public static SpeciesSpriteData Parse(byte[] data)
        {
            if (data == null || data.Length < Size) return null;
            var rec = new SpeciesSpriteData();
            ReadFace(data, 0, rec.Front);
            ReadFace(data, FaceSize, rec.Back);
            rec.YOffset = (sbyte)data[FaceSize * 2];
            rec.ShadowXOffset = (sbyte)data[FaceSize * 2 + 1];
            rec.ShadowSize = data[FaceSize * 2 + 2];
            return rec;
        }

        public byte[] ToBytes()
        {
            var data = new byte[Size];
            WriteFace(data, 0, Front);
            WriteFace(data, FaceSize, Back);
            data[FaceSize * 2] = (byte)(sbyte)YOffset;
            data[FaceSize * 2 + 1] = (byte)(sbyte)ShadowXOffset;
            data[FaceSize * 2 + 2] = (byte)ShadowSize;
            return data;
        }

        private static void ReadFace(byte[] data, int at, Face face)
        {
            face.CryDelay = data[at];
            face.Animation = data[at + 1];
            face.StartDelay = data[at + 2];
            for (int i = 0; i < FrameCount; i++)
            {
                int o = at + 3 + i * 4;
                face.Frames[i] = new SpriteFrameSlot((sbyte)data[o], data[o + 1], (sbyte)data[o + 2], (sbyte)data[o + 3]);
            }
        }

        private static void WriteFace(byte[] data, int at, Face face)
        {
            data[at] = (byte)face.CryDelay;
            data[at + 1] = (byte)face.Animation;
            data[at + 2] = (byte)face.StartDelay;
            for (int i = 0; i < FrameCount; i++)
            {
                int o = at + 3 + i * 4;
                var s = face.Frames[i];
                data[o] = (byte)(sbyte)s.FrameNo;
                data[o + 1] = (byte)s.Duration;
                data[o + 2] = (byte)(sbyte)s.HorizontalShift;
                data[o + 3] = (byte)(sbyte)s.VerticalShift;
            }
        }
    }
}

using System;

namespace DSPRE.Models
{
    /// <summary>A material record, kept whole so rebuilt maps keep lighting, alpha and culling.</summary>
    public sealed class MaterialLook
    {
        public const int PlainSize = 44;

        public byte[] Record { get; }

        private MaterialLook(byte[] record) { Record = record; }

        public static MaterialLook FromRecord(byte[] record)
            => record == null || record.Length < PlainSize ? null : new MaterialLook((byte[])record.Clone());

        public int DiffuseAmbient => BitConverter.ToInt32(Record, 4);
        public int SpecularEmission => BitConverter.ToInt32(Record, 8);
        public int PolygonAttr => BitConverter.ToInt32(Record, 12);
        public int ImageParam => BitConverter.ToInt32(Record, 20);

        public int Lights => PolygonAttr & 0xf;

        public int Alpha => (PolygonAttr >> 16) & 31;

        public bool BothSides => (PolygonAttr & 0xc0) == 0xc0;
        public bool Fog => (PolygonAttr & 0x8000) != 0;

        public int CornerColour => (DiffuseAmbient & 0x8000) != 0 ? DiffuseAmbient & 0x7fff : -1;

        public string Key => Convert.ToHexString(Record);

        public MaterialLook With(int? alpha = null, bool? bothSides = null, bool? fog = null, int? lights = null)
        {
            var r = (byte[])Record.Clone();
            int poly = BitConverter.ToInt32(r, 12);
            if (alpha is int a)
            {
                a = Math.Clamp(a, 0, 31);
                poly = (poly & ~(31 << 16)) | (a << 16);
                int id = (poly >> 24) & 63;
                if (id != 8) poly = (poly & ~(63 << 24)) | ((a < 31 ? 3 : 0) << 24);
            }
            if (bothSides is bool both) poly = (poly & ~0xc0) | (both ? 0xc0 : 0x80);
            if (fog is bool f) poly = f ? poly | 0x8000 : poly & ~0x8000;
            if (lights is int l) poly = (poly & ~0xf) | (l & 0xf);
            Put32(r, 12, poly);
            return new MaterialLook(r);
        }

        public MaterialLook WithPictureSize(int wide, int tall)
        {
            var r = (byte[])Record.Clone();
            Put16(r, 32, wide);
            Put16(r, 34, tall);
            return new MaterialLook(r);
        }

        public static MaterialLook Plain => Made(new[] { true, false, false, false }, false, 31, true, false,
            Tiling.Repeat, Tiling.Repeat, 0, (25, 25, 25), (31, 31, 31), (0, 0, 0), (0, 0, 0));

        public enum Tiling { Repeat = 0, Clamp = 1, Flip = 2 }

        public static MaterialLook Made(bool[] lights, bool bothSides, int alpha, bool fog, bool outlined,
                                        Tiling across, Tiling down, int placesFrom,
                                        (int r, int g, int b) diffuse, (int r, int g, int b) ambient,
                                        (int r, int g, int b) specular, (int r, int g, int b) emission,
                                        int pictureWide = 0, int pictureTall = 0)
        {
            alpha = Math.Clamp(alpha, 0, 31);
            int lightMask = 0;
            for (int i = 0; i < 4 && lights != null && i < lights.Length; i++)
                if (lights[i]) lightMask |= 1 << i;

            int polygonId = outlined ? 8 : alpha < 31 ? 3 : 0;
            int poly = lightMask | (bothSides ? 0xc0 : 0x80) | (fog ? 0x8000 : 0)
                     | (alpha << 16) | (polygonId << 24);

            int image = RepeatBits(across, 16, 18) | RepeatBits(down, 17, 19) | ((placesFrom & 3) << 30);

            var r = new byte[PlainSize];
            Put16(r, 0, 0);
            Put16(r, 2, PlainSize);
            // Bit 15: diffuse is used as the vertex colour.
            Put32(r, 4, Colour(diffuse) | 0x8000 | (Colour(ambient) << 16));
            Put32(r, 8, Colour(specular) | (Colour(emission) << 16));
            Put32(r, 12, poly);
            Put32(r, 16, 0x3F1FF8FF);
            Put32(r, 20, image);
            Put32(r, 24, unchecked((int)0xFFFFFFFF));
            Put16(r, 28, 0);
            Put16(r, 30, 0x1FCE);
            Put16(r, 32, pictureWide);
            Put16(r, 34, pictureTall);
            Put32(r, 36, 4096);
            Put32(r, 40, 4096);
            return new MaterialLook(r);
        }

        private static int RepeatBits(Tiling t, int repeat, int flip) => t switch
        {
            Tiling.Repeat => 1 << repeat,
            Tiling.Flip => (1 << repeat) | (1 << flip),
            _ => 0,
        };

        private static int Colour((int r, int g, int b) c)
            => Math.Clamp(c.r, 0, 31) | (Math.Clamp(c.g, 0, 31) << 5) | (Math.Clamp(c.b, 0, 31) << 10);

        private static void Put16(byte[] d, int at, int v) { d[at] = (byte)v; d[at + 1] = (byte)(v >> 8); }

        private static void Put32(byte[] d, int at, int v)
        {
            d[at] = (byte)v; d[at + 1] = (byte)(v >> 8); d[at + 2] = (byte)(v >> 16); d[at + 3] = (byte)(v >> 24);
        }
    }
}

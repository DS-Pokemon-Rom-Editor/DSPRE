using System;
using System.Collections.Generic;
using System.Drawing;

namespace MKDS_Course_Editor
{
    // From the Tinke and MKDS Course Editor palette editor; only its colour helpers are kept.
    public class nclr_e
    {
        int _r = 255;
        int _g = 255;
        int _b = 255;
        List<Color> _colors = new List<Color>();
        bool _bpp8 = false;
        uint _unknown1;
        public nclr_e(Color[] c, bool bpp8, uint unknown)
        {
            _colors.AddRange(c);
            _bpp8 = bpp8;
            _unknown1 = unknown;
        }
        public nclr_e()
        {

        }
        public int DecodeColor(int value, CColorFormat format)
        {
            int[] res = format.GetResolution();
            int rgb = Convert.ToInt32(0xff000000);
            int shift = 0;
            int length = res.Length / 2;
            for (int i = 0; i < length; i++)
            {
                int mode = res[length - i - 1];
                int nshift = res[length * 2 - i - 1];
                int mult = ShiftList[nshift][1];
                int and = ShiftList[nshift][0];
                int n = (value >> shift & and) * mult;
                switch (mode)
                {
                    case 0: // '\0'
                        rgb |= n << 24;
                        break;

                    case 1: // '\001'
                        rgb |= n << 16;
                        break;

                    case 2: // '\002'
                        rgb |= n << 8;
                        break;

                    case 3: // '\003'
                        rgb |= n;
                        break;

                    case 4: // '\004'
                        rgb = n << 16 | n << 8 | n;
                        break;
                }
                shift += nshift;
            }

            return rgb;
        }
        public CColorFormat BGR555 = new CColorFormat("BGR555", 10, 16, new int[] {
        3, 2, 1, 5, 5, 5
    });

        public static int EncodeColor(int value, CColorFormat format)
        {
            int[] res = format.GetResolution();
            int rgb = 0;
            int shift = 0;
            int length = res.Length / 2;
            for (int i = 0; i < length; i++)
            {
                int mode = res[length - i - 1];
                int nshift = res[length * 2 - i - 1];
                int mult = ShiftList[nshift][1];
                int and = ShiftList[nshift][0];
                int n = 0;
                switch (mode)
                {
                    case 0: // '\0'
                        n = value >> 24 & 0xff;
                        break;

                    case 1: // '\001'
                        n = value >> 16 & 0xff;
                        break;

                    case 2: // '\002'
                        n = value >> 8 & 0xff;
                        break;

                    case 3: // '\003'
                        n = value & 0xff;
                        break;

                    case 4: // '\004'
                        n = value & 0xff;
                        break;
                }
                rgb |= (n / mult & and) << shift;
                shift += nshift;
            }

            return rgb;
        }

        public static int[][] ShiftList = new int[][] {
            new int[] { 0, 0 },
            new int[] { 1, 255 },
            new int[] { 3, 85 },
            new int[] { 7, 36 },
            new int[] { 15, 17 },
            new int[] { 31, 8 },
            new int[] { 63, 4 },
            new int[] { 127, 2 },
            new int[] { 255, 1 }
        };

        public class CColorFormat
        {

            public CColorFormat(String name, int id, int depth, int[] res)
            {
                this._name = name;
                this._id = id;
                this._depth = depth;
                this._res = res;
            }

            public override string ToString()
            {
                return GetName();
            }

            public string GetName()
            {
                return _name;
            }

            public int GetId()
            {
                return _id;
            }

            public int GetDepth()
            {
                return _depth;
            }

            public int[] GetResolution()
            {
                return _res;
            }

            public int GetWidth(int field)
            {
                for (int i = 0; i < _res.Length / 2; i++)
                    if (_res[i] == field)
                        return _res[i + _res.Length / 2];

                return 0;
            }

            private String _name;
            private int _id;
            private int _depth;
            private int[] _res;
        }

    }
}

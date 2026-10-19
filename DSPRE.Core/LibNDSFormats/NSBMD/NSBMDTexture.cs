using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LibNDSFormats.NSBMD
{
    public class NSBMDTexture
    {
        public int Format;
        public int Height;
        public byte Repeat;
        public byte[] Spdata;
        public byte[] Texdata;
        public List<uint> Texmatid = new List<uint>();
        public string Texname = String.Empty;
        public UInt32 Texoffset;
        public UInt32 Texsize;
        public int Width;
        public int Color0;
    }
}

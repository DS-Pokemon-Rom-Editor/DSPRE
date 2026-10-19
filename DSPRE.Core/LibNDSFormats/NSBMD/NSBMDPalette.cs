using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LibNDSFormats.NSBMD
{
    public class NSBMDPalette
    {
        public int Color0;
        public RGBA[] Paldata;
        public List<uint> Palmatid = new List<uint>();
        public string Palname = String.Empty;
        public UInt32 Paloffset;
        public UInt32 Palsize;
    }
}

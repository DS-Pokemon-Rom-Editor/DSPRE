using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LibNDSFormats.NSBMD
{
    /// <summary>
    /// Type for NSBCA objects.
    /// </summary>
    public class NSBMDAnimation
    {
        public int Dataoffset = 0;
        public int Flag = 0;
        public float[] MTrans = new float[3];
        public float A, B = 0;
        public float[] MScale = new float[3];
        public int Frame = 0;
        public int Framelen = 0;
        public List<Int16> Animdata = new List<short>();
    }
}

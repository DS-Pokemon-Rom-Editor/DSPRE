// Material type for NSBMD models.
// Code adapted from kiwi.ds' NSBMD Model Viewer.

using System;
using System.Drawing;
using System.Collections.Generic;

namespace LibNDSFormats.NSBMD
{
    public class NSBMDMaterial
    {
        #region Fields (16) 

        public int Color0;
        public int Format;
        public int Height;
        public RGBA[] Paldata;
        public uint Palmatid;
        public string Palname = String.Empty;
        public UInt32 Paloffset;
        public UInt32 Palsize;
        public byte Repeat;
        public byte[] Spdata;
        public byte[] Texdata;
        public List<uint> Texmatid = new List<uint>();
        public bool IsEnvironmentMap = false;
        public string Texname = String.Empty;
        public UInt32 Texoffset;
        public UInt32 Texsize;
        public int Width;
        public string MaterialName;
        public int RepeatS;
        public int RepeatT;
        public int FlipS;
        public int FlipT;
        public float Rot;      // sine of the texture rotation
        public float RotCos = 1f;
        public float ScaleT;
        public float ScaleS;
        public float TransT;
        public float TransS;
        public Color DiffuseColor;
        public Color AmbientColor;
        public Color SpecularColor;
        public Color EmissionColor;
        public uint PolyAttrib;
        /// <summary>Which bits of <see cref="PolyAttrib"/> this material actually sets. A bit that is
        /// not in here was never specified, so reading it would be reading nothing.</summary>
        public uint PolyAttribMask;
        public int Alpha;
        public bool HasDiffuseColor;
        public bool Shine;
        public bool MissingExternalTexture;
        public float[] Mtx = null;

        #endregion Fields 

        #region Methods (1) 

        // Public Methods (1) 

        /// <summary>
        /// Copy data to other NSBMD material
        /// </summary>
        /// <param name="other">Other NSBMD material.</param>
        public NSBMDMaterial Clone() => (NSBMDMaterial)MemberwiseClone();

        public NSBMDMaterial CopyTo(NSBMDMaterial other1)
        {
            NSBMDMaterial other = other1;
            other.Texname = Texname;
            other.Texoffset = Texoffset;
            other.Texsize = Texsize;
            other.Format = Format;
            other.Color0 = Color0;
            other.Width = Width;
            other.Height = Height;
            other.Texdata = Texdata;
            other.Spdata = Spdata;
            other.MaterialName = MaterialName;
            return other;
        }

        #endregion Methods 
    }
}

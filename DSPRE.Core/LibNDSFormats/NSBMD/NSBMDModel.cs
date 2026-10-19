// Model definition for NSBMD.
// Code adapted from kiwi.ds' NSBMD Model Viewer.

using System.Collections.Generic;

namespace LibNDSFormats.NSBMD
{
    /// <summary>
    /// NSBMD model type.
    /// </summary>
    public class NSBMDModel
    {
        #region Fields (3) 
        /// <summary>
        /// NSBMD materials.
        /// </summary>
        public readonly List<NSBMDMaterial> Materials = new List<NSBMDMaterial>();
        /// <summary>
        /// NSBMD materials.
        /// </summary>
        public readonly List<NSBMDTexture> Textures = new List<NSBMDTexture>();
        /// <summary>
        /// NSBMD materials.
        /// </summary>
        public readonly List<NSBMDPalette> Palettes = new List<NSBMDPalette>();
        /// <summary>
        /// NSBMD objects.
        /// </summary>
        public readonly List<NSBMDObject> Objects = new List<NSBMDObject>();
        /// <summary>
        /// NSBMD polygons.
        /// </summary>
        public readonly List<NSBMDPolygon> Polygons = new List<NSBMDPolygon>();
        /// <summary>Node ids in the order the render commands set their matrices.</summary>
        public readonly List<int> NodeOrder = new List<int>();

        #endregion Fields 

        #region Properties (1) 

        /// <summary>
        /// Model name.
        /// </summary>
        public string Name { get; set; }
        public List<int> TexMat = new List<int>();
        public List<int> PalMat = new List<int>();
        public float BoundXmin;
        public float BoundYmin;
        public float BoundZmin;
        public float BoundXmax;
        public float BoundYmax;
        public float BoundZmax;
        public float BoundScale;
        public float ModelScale;

        /// <summary>How the model's texture matrices are built, NNSG3dResMdlInfo.texMtxMode: 0 Maya, 1 SoftImage 3D,
        /// 2 3ds Max, 3 XSI. The field models are Maya.</summary>
        public byte TexMtxMode;
        public int Laststackid;
        #endregion Properties 
    }
}
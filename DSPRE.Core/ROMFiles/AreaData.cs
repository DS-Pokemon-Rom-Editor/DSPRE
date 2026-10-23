using System.IO;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /// <summary>
    /// An area's texture packs, terrain animation, area type and light.
    /// </summary>
    public class AreaData : RomFile
    {
        internal static readonly byte TYPE_INDOOR = 0;
        internal static readonly byte TYPE_OUTDOOR = 1;

        #region Fields (2)
        public ushort BuildingsTileset;
        public ushort MapTileset;
        // Third field of the area record. HGSS stores the terrain animation index here
        // (0xFFFF for none); DP/Pt stores a model set index that nothing in the game ever reads.
        public ushort GroundAnimation;
        public ushort MovingModelSet;
        public byte AreaType = TYPE_OUTDOOR; //HGSS ONLY
        public ushort LightType; //using an overabundant size. HGSS only needs a byte
        public bool IsIndoor => RomInfo.GameFamily == GameFamilies.HGSS && AreaType == TYPE_INDOOR;
        #endregion

        #region Constructors (1)
        public AreaData(Stream data)
        {
            using (BinaryReader reader = new BinaryReader(data))
            {
                BuildingsTileset = reader.ReadUInt16();
                MapTileset = reader.ReadUInt16();

                if (RomInfo.GameFamily == GameFamilies.HGSS)
                {
                    GroundAnimation = reader.ReadUInt16();
                    AreaType = reader.ReadByte();
                    LightType = reader.ReadByte();
                }
                else
                {
                    MovingModelSet = reader.ReadUInt16();
                    LightType = reader.ReadUInt16();
                }
            }
        }
        public AreaData (byte ID) : this(new FileStream(Path.Combine(RomInfo.GameDirs[DirNames.AreaData].unpackedDir, ID.ToString("D4")), FileMode.Open, FileAccess.Read)) {}
        #endregion

        #region Methods (1)
        public override byte[] ToByteArray()
        {
            MemoryStream newData = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(newData))
            {
                writer.Write(BuildingsTileset);
                writer.Write(MapTileset);

                if (RomInfo.GameFamily == GameFamilies.HGSS)
                {
                    writer.Write(GroundAnimation);
                    writer.Write(AreaType);
                    writer.Write((byte)LightType);
                }
                else
                {
                    writer.Write(MovingModelSet);
                    writer.Write((ushort)LightType);
                }
            }
            return newData.ToArray();
        }

        public void SaveToFileDefaultDir(int IDtoReplace, bool showSuccessMessage = true)
        {
            SaveToFileDefaultDir(DirNames.AreaData, IDtoReplace, showSuccessMessage);
        }

        public void SaveToFileExplorePath(string suggestedFileName, bool showSuccessMessage = true)
        {
            SaveToFileExplorePath("Area data file", "bin", suggestedFileName, showSuccessMessage);
        }
        #endregion
    }
}

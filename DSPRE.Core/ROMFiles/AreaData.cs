using System.IO;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles {
    /// <summary>
    /// Class to store area data in Pokémon NDS games
    /// </summary>
    public class AreaData : RomFile {
        internal static readonly byte TYPE_INDOOR = 0;
        internal static readonly byte TYPE_OUTDOOR = 1;

        #region Fields (2)
        public ushort buildingsTileset;
        public ushort mapTileset;
        // Third field of the area's RESOURCE_PARAM. HGSS stores the terrain animation index here
        // (0xFFFF for none); DP/Pt stores a model set index that nothing in the game ever reads.
        public ushort groundAnimation;
        public ushort movingModelSet;
        public byte areaType = TYPE_OUTDOOR; //HGSS ONLY
        public ushort lightType; //using an overabundant size. HGSS only needs a byte
        public bool IsIndoor => RomInfo.gameFamily == GameFamilies.HGSS && areaType == TYPE_INDOOR;
        #endregion

        #region Constructors (1)
        public AreaData(Stream data) {
            using (BinaryReader reader = new BinaryReader(data)) {
                buildingsTileset = reader.ReadUInt16();
                mapTileset = reader.ReadUInt16();

                if (RomInfo.gameFamily == GameFamilies.HGSS) {
                    groundAnimation = reader.ReadUInt16();
                    areaType = reader.ReadByte();
                    lightType = reader.ReadByte();
                } else {
                    movingModelSet = reader.ReadUInt16();
                    lightType = reader.ReadUInt16();
                }
            }
        }
        public AreaData (byte ID) : this(new FileStream(Path.Combine(RomInfo.gameDirs[DirNames.areaData].unpackedDir, ID.ToString("D4")), FileMode.Open)) {}
        #endregion

        #region Methods (1)
        public override byte[] ToByteArray() {
            MemoryStream newData = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(newData)) {
                writer.Write(buildingsTileset);
                writer.Write(mapTileset);

                if (RomInfo.gameFamily == GameFamilies.HGSS) {
                    writer.Write(groundAnimation);
                    writer.Write(areaType);
                    writer.Write((byte)lightType);
                } else {
                    writer.Write(movingModelSet);
                    writer.Write((ushort)lightType);
                }
            }
            return newData.ToArray();
        }

        public void SaveToFileDefaultDir(int IDtoReplace, bool showSuccessMessage = true) {
            SaveToFileDefaultDir(DirNames.areaData, IDtoReplace, showSuccessMessage);
        }

        public void SaveToFileExplorePath(string suggestedFileName, bool showSuccessMessage = true) {
            SaveToFileExplorePath("Gen IV Area Data File", "bin", suggestedFileName, showSuccessMessage);
        }
        #endregion
    }
}

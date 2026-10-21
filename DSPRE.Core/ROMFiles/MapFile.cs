using System.IO;
using System.Collections.Generic;
using LibNDSFormats.NSBMD;
using static DSPRE.RomInfo;
using System;
using System.Drawing;

namespace DSPRE.ROMFiles
{
    /* ----------------------- MAP FILE DATA STRUCTURE (DPPtHGSS):--------------------------

    0x0  //  uint:       Length of permissions section (always 2048)
    0x4  //  uint:       Length of buildings section
    0x8  //  uint:       Length of nsbmd model section
    0xC  //  uint:       Length of BDHC section
    0x10 //  

    ************* Permissions section (1024 byte - byte pairs):
        
    0x0  //  byte:       Type value of tile (e.g. normal, grass, surfable water)
    0x1  //  byte:       Collision value of tile (00 for walkable, 80 for blocked)   

    ************* Buildings section (# of buildings equal to section length divided by 48):
    
    BUILDING FORMAT:
    0x0  //  uint:      Model ID
    0x4  //  ushort:    65535ths of X coordinate
    0x6  //  ushort:    X coordinate
    0x8  //  ushort:    65535ths of Z coordinate
    0xA  //  ushort:    Z coordinate
    0xC  //  ushort:    65535ths of Y coordinate
    0xE  //  ushort:    Y coordinate
    -------- 0x10 - 0x1C FILLER ZEROES
    0x1D //  uint:      Model width scale (usually 16)
    0x21 //  uint:      Model height scale (usually 16)
    0x25 //  uint:      Model length scale (usually 16)
    -------- 0x2A - 0x2F FILLER ZEROES

   ************* NSBMD model section
   ************* BDHC section
   
   -------------------------------------------------------------------------------------- */


    /// <summary>
    /// Class to store map data in Pokémon NDS games
    /// </summary>
    public class MapFile : RomFile
    {
        #region Fields

        public static readonly string NSBMDFilter = "NSBMD File (*.nsbmd)|*.nsbmd";

        public bool CorrectnessFlag = true;
        public static readonly byte MapSize = 32;
        public static readonly byte BuildingHeaderSize = 48;
        public static readonly byte[] BlankBGS = new byte[] { 0x34, 0x12, 0x00, 0x00 };

        public List<Building> Buildings;
        /// <summary>Bytes after the last whole building record, written back unchanged.</summary>
        public byte[] BuildingsTail = Array.Empty<byte>();
        public NSBMD MapModel;
        public byte[,] Collisions = new byte[MapSize, MapSize];
        public byte[,] Types = new byte[MapSize, MapSize];
        public byte[] MapModelData;
        public byte[] Bdhc;

        /// <summary>Terrain plates the user made or edited while this map is open; rebuilds keep them.</summary>
        public List<Models.BdhcBuild.Piece> KeptPlates = new List<Models.BdhcBuild.Piece>();
        public byte[] Bgs = BlankBGS;
        #endregion

        #region Constructors (1)
        public MapFile(string path, GameFamilies gFamily, bool discardMoveperms = false, bool showMessages = true) : this (new FileStream(path, FileMode.Open), gFamily, discardMoveperms, showMessages) {}
        public MapFile(int mapNumber, GameFamilies gFamily, bool discardMoveperms = false, bool showMessages = true) : this(Path.Combine(RomInfo.GameDirs[DirNames.Maps].unpackedDir, mapNumber.ToString("D4")), gFamily, discardMoveperms, showMessages) { }
        public MapFile(Stream data, GameFamilies gFamily, bool discardMoveperms = false, bool showMessages = true)
        {
            using (BinaryReader reader = new BinaryReader(data))
            {
                /* Read sections lengths */
                int permissionsSectionLength = reader.ReadInt32();
                int buildingsSectionLength = reader.ReadInt32();
                int nsbmdSectionLength = reader.ReadInt32();
                int bdhcSectionLength = reader.ReadInt32();

                /* Read background sounds section */
                if (gFamily == GameFamilies.HGSS)
                { //Map must be loaded as HGSS
                    ushort bgsSignature = reader.ReadUInt16();
                    if (bgsSignature == 0x1234)
                    {
                        ushort bgsDataLength = reader.ReadUInt16();

                        reader.BaseStream.Position -= 4; //go back so that the signature "0x1234" + size can be read and stored
                        ImportSoundPlates(reader.ReadBytes(bgsDataLength + 4));
                    }
                    else
                    {
                        CorrectnessFlag = false;
                        if (showMessages)
                        {
                            AppMessages.Error("The header section of this map's BackGround Sound data is corrupted.",
                            "BGS Error");
                        }
                    }
                }

                /* Read permission data */
                if (discardMoveperms)
                {
                    reader.BaseStream.Position += permissionsSectionLength;
                }
                else
                {
                    ImportPermissions(reader.ReadBytes(permissionsSectionLength));
                }

                /* Read buildings data */
                ImportBuildings(reader.ReadBytes(buildingsSectionLength));

                /* Read nsbmd model */
                if ( !LoadMapModel(reader.ReadBytes(nsbmdSectionLength), showMessages) )
                { //Assign result to flag
                    CorrectnessFlag = false;
                    MapModel = null;
                };

                /* Read bdhc data */
                ImportTerrain(reader.ReadBytes(bdhcSectionLength));
            }
        }
        #endregion

        #region Methods
        public byte[] BuildingsToByteArray()
        {
            MemoryStream newData = new MemoryStream(BuildingHeaderSize * Buildings.Count + BuildingsTail.Length);
            using (BinaryWriter writer = new BinaryWriter(newData))
            {
                for (int i = 0; i < Buildings.Count; i++)
                {
                    writer.Write(Buildings[i].ToRecord());
                }
                writer.Write(BuildingsTail);
            }
            return newData.ToArray();
        }
        public byte[] CollisionsToByteArray()
        {
            MemoryStream newData = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(newData))
            {
                for (int i = 0; i < MapSize; i++)
                {
                    for (int j = 0; j < MapSize; j++)
                    {
                        writer.Write(Types[i, j]);
                        writer.Write(Collisions[i, j]);
                    }
                }
            }
            return newData.ToArray();
        }
        public void ImportBuildings(byte[] newData)
        {
            Buildings = new List<Building>();
            using (BinaryReader reader = new BinaryReader(new MemoryStream(newData)))
            {
                for (int i = 0; i < newData.Length / BuildingHeaderSize; i++)
                {
                    Buildings.Add(new Building(new MemoryStream(reader.ReadBytes(BuildingHeaderSize))));
                }
                BuildingsTail = reader.ReadBytes(newData.Length % BuildingHeaderSize);
            }
        }

        /// <summary>
        /// Per-map model and terrain buffers (pokeplatinum MAP_MODEL_FILE_SIZE, BDHC_BUFFER_SIZE; HGSS matches).
        /// A larger file overruns the next map's buffer.
        /// </summary>
        public const int ModelRoom = 0xF000, TerrainRoom = 0x9000;

        /// <summary>
        /// Reads a map BIN for <paramref name="family"/>, falling back to the other layout (HGSS maps carry a sound plate
        /// section DP and Pt maps lack), which is converted on save. Null when the file reads as neither.
        /// </summary>
        public static MapFile ReadForGame(byte[] data, GameFamilies family, out bool converted)
        {
            converted = false;
            MapFile own = TryRead(data, family);
            if (own != null) return own;
            MapFile other = TryRead(data, family == GameFamilies.HGSS ? GameFamilies.Plat : GameFamilies.HGSS);
            converted = other != null;
            return other;
        }

        /// <summary>
        /// Reads a file brought in as a whole map or a model, by its contents, so a renamed file or one with no
        /// extension still works. Exactly one of <paramref name="map"/> and <paramref name="model"/> is set on success.
        /// </summary>
        /// <returns>Why the file can't be used, or null.</returns>
        public static string ReadImport(byte[] data, GameFamilies family, out MapFile map, out byte[] model, out bool converted)
        {
            map = null; model = null; converted = false;
            if (data != null && data.Length >= 4 && BitConverter.ToUInt32(data, 0) == NSBMD.NdsTypeBmd0)
            {
                // A map's textures come from its area's pack, so any carried in the file are left out, as on load.
                byte[] bare = data.Length > 0x10 && BitConverter.ToInt16(data, 0xE) > 1 ? NSBUtils.GetModelWithoutTextures(data) : data;
                string whynot = CannotUseModel(bare);
                if (whynot == null) model = bare;
                return whynot;
            }
            map = data == null ? null : ReadForGame(data, family, out converted);
            if (map == null) return "That file is neither a map nor an NSBMD model.";
            string tooBig = TooBigForTheGame(map.MapModelData.Length, map.Bdhc?.Length ?? 0);
            if (tooBig != null) map = null;
            return tooBig;
        }

        /// <summary>
        /// Puts another map's model, permissions, buildings, terrain and sound plates in this one, so everything holding
        /// this map sees them. Sound plates exist only in HGSS maps.
        /// </summary>
        public void TakeContentsOf(MapFile other, GameFamilies family)
        {
            LoadMapModel(other.MapModelData, showMessages: false);
            ImportPermissions(other.CollisionsToByteArray());
            ImportBuildings(other.BuildingsToByteArray());
            ImportTerrain(other.Bdhc);
            KeptPlates.Clear();
            Bgs = family == GameFamilies.HGSS ? other.Bgs : BlankBGS;
        }

        /// <summary>Why a model file can't be this map's model, or null when it can.</summary>
        public static string CannotUseModel(byte[] model)
        {
            if (model == null || model.Length < 4 || BitConverter.ToUInt32(model, 0) != NSBMD.NdsTypeBmd0) return "That file is not an NSBMD model.";
            return TooBigForTheGame(model.Length, 0);
        }

        private static MapFile TryRead(byte[] data, GameFamilies family)
        {
            try
            {
                MapFile map = new MapFile(new MemoryStream(data), family, discardMoveperms: false, showMessages: false);
                return map.CorrectnessFlag && map.MapModelData != null ? map : null;
            }
            catch (Exception ex) when (ex is EndOfStreamException || ex is ArgumentException || ex is IOException || ex is OverflowException)
            {
                return null;
            }
        }

        /// <summary>Why the game could not load this map's model or terrain, or null when both fit.</summary>
        public static string TooBigForTheGame(int modelBytes, int terrainBytes) =>
            modelBytes > ModelRoom
                ? $"The model is {modelBytes:n0} bytes; the game loads at most {ModelRoom:n0} per map, so the maps around it would fail to load."
            : terrainBytes > TerrainRoom
                ? $"The terrain is {terrainBytes:n0} bytes; the game loads at most {TerrainRoom:n0} per map."
            : null;

        public bool LoadMapModel(byte[] newData, bool showMessages = true)
        {
            using (BinaryReader modelReader = new BinaryReader(new MemoryStream(newData)))
            {

                if (modelReader.ReadUInt32() != NSBMD.NdsTypeBmd0)
                {
                    if (showMessages)
                    {
                        AppMessages.Error("Please select an NSBMD file.", "Invalid File");
                    }
                    return false;
                }

                modelReader.BaseStream.Position = 0xE;
                if (modelReader.ReadInt16() > 1)
                { // If NSBMD contains more than one segment, it means there are embedded textures we must remove
                    MapModelData = NSBUtils.GetModelWithoutTextures(newData);
                }
                else
                {
                    modelReader.BaseStream.Position = 0x0;
                    MapModelData = modelReader.ReadBytes((int)modelReader.BaseStream.Length);
                }

                MapModel = NSBMDLoader.LoadNSBMD(new MemoryStream(MapModelData));
            }
            return true;
        }

        public void ImportPermissions(byte[] newData)
        {
            using (BinaryReader reader = new BinaryReader(new MemoryStream(newData)))
            {
                for (int i = 0; i < 32; i++)
                {
                    for (int j = 0; j < 32; j++)
                    {
                        Types[i, j] = reader.ReadByte(); // Read permission type (e.g. surfing water, grass, sand etc.)
                        Collisions[i, j] = reader.ReadByte(); // Read walkability (00 for walkable and 80 for blocked)                        
                    }
                }
            }
        }
        public void ImportSoundPlates(byte[] newData)
        {
            using (BinaryReader reader = new BinaryReader(new MemoryStream(newData)))
            {
                Bgs = reader.ReadBytes((int)newData.Length);
            }
        }
        public void ImportTerrain(byte[] newData)
        {
            using (BinaryReader reader = new BinaryReader(new MemoryStream(newData)))
            {
                Bdhc = reader.ReadBytes((int)newData.Length);
            }
        }
        public override byte[] ToByteArray()
        {
            MemoryStream newData = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(newData))
            {
                /* Write section lengths */
                writer.Write(Collisions.Length + Types.Length);

                writer.Write(Buildings.Count * BuildingHeaderSize + BuildingsTail.Length);
                writer.Write(MapModelData.Length);
                writer.Write(Bdhc.Length);

                /* Write soundplate section for HG/SS */
                if (RomInfo.GameFamily == GameFamilies.HGSS)
                {
                    writer.Write(Bgs);
                }

                /* Write sections */
                writer.Write(CollisionsToByteArray());
                writer.Write(BuildingsToByteArray());
                writer.Write(MapModelData);
                writer.Write(Bdhc);
            }
            return newData.ToArray();
        }

        public SortedSet<byte> GetUsedTypes()
        {
            SortedSet<byte> sortedBytes = new SortedSet<byte>();

            for (int i = 0; i < MapSize; i++)
            {
                for (int j = 0; j < MapSize; j++)
                {
                    sortedBytes.Add(Types[i, j]);
                }
            }

            return sortedBytes;
        }

        public void SaveToFileDefaultDir(int IDtoReplace, bool showSuccessMessage = true)
        {
            SaveToFileDefaultDir(DirNames.Maps, IDtoReplace, showSuccessMessage);
        }
        public void SaveToFileExplorePath(string suggestedFileName, bool showSuccessMessage = true)
        {
            SaveToFileExplorePath("Gen IV Map Bin", "bin", suggestedFileName, showSuccessMessage);
        }
        #endregion
    }

    /// <summary>
    /// Class to store building data from Pokémon NDS games
    /// </summary>
    public class Building
    {
        #region Fields (11)
        public NSBMD NSBMDFile;
        public uint ModelID { get; set; }
        public short XPosition { get; set; }
        public short YPosition { get; set; }
        public short ZPosition { get; set; }
        public ushort XFraction { get; set; }
        public ushort YFraction { get; set; }
        public ushort ZFraction { get; set; }
        public ushort XRotation { get; set; }
        public ushort YRotation { get; set; }
        public ushort ZRotation { get; set; }
        public uint Width { get; set; }
        public uint Height { get; set; }
        public uint Length { get; set; }

        // The record as read, so bytes DSPRE does not edit (the scales' fractions and the trailing
        // pokeplatinum MapPropFile.dummy28 words) are written back unchanged.
        private byte[] _record;
        #endregion Fields

        #region Constructors (2)
        public Building(Stream data)
        {
            using (BinaryReader source = new BinaryReader(data))
            {
                _record = source.ReadBytes(MapFile.BuildingHeaderSize);
            }
            if (_record.Length < MapFile.BuildingHeaderSize)
            {
                Array.Resize(ref _record, MapFile.BuildingHeaderSize);
            }
            using (BinaryReader reader = new BinaryReader(new MemoryStream(_record)))
            {
                ModelID = reader.ReadUInt32();

                XFraction = reader.ReadUInt16();
                XPosition = reader.ReadInt16();
                YFraction = reader.ReadUInt16();
                YPosition = reader.ReadInt16();
                ZFraction = reader.ReadUInt16();
                ZPosition = reader.ReadInt16();
                
                XRotation = reader.ReadUInt16();
                reader.BaseStream.Position += 0x2;
                YRotation = reader.ReadUInt16();
                reader.BaseStream.Position += 0x2;
                ZRotation = reader.ReadUInt16();
                reader.BaseStream.Position += 0x2;

                reader.BaseStream.Position += 0x1;

                Width = reader.ReadUInt16();
                reader.BaseStream.Position += 0x2;
                Height = reader.ReadUInt16();
                reader.BaseStream.Position += 0x2;
                Length = reader.ReadUInt16();
                
                //reader.BaseStream.Position += 0x2;
            }
        }
        public Building()
        {
            ModelID = 0;
            XFraction = 0;
            XPosition = 0;
            YFraction = 0;
            YPosition = 1;
            ZFraction = 0;
            ZPosition = 0;

            XRotation = YRotation = ZRotation = 0;
            Width = 16;
            Height = 16;
            Length = 16;
        }

        public Building(Building toCopy)
        {
            ModelID = toCopy.ModelID;
            XFraction = toCopy.XFraction;
            XPosition = toCopy.XPosition;
            YFraction = toCopy.YFraction;
            YPosition = (short)(toCopy.YPosition + 1);
            ZFraction = toCopy.ZFraction;
            ZPosition = toCopy.ZPosition;

            XRotation = toCopy.XRotation;
            YRotation = toCopy.YRotation;
            ZRotation = toCopy.ZRotation;

            Width = toCopy.Width;
            Height = toCopy.Height;
            Length = toCopy.Length;
            _record = (byte[])toCopy._record?.Clone();
        }
        #endregion Constructors

        /// <summary>The 48-byte record: the edited fields laid over the bytes it was read from.</summary>
        public byte[] ToRecord()
        {
            byte[] bytes = _record != null ? (byte[])_record.Clone() : new byte[MapFile.BuildingHeaderSize];
            using (BinaryWriter writer = new BinaryWriter(new MemoryStream(bytes)))
            {
                writer.Write(ModelID);
                writer.Write(XFraction);
                writer.Write(XPosition);
                writer.Write(YFraction);
                writer.Write(YPosition);
                writer.Write(ZFraction);
                writer.Write(ZPosition);

                writer.Write((int)XRotation);
                writer.Write((int)YRotation);
                writer.Write((int)ZRotation);

                // Each scale is read as the whole part of its fx32, one byte into the word.
                writer.BaseStream.Position = 0x1D;
                writer.Write((ushort)Width);
                writer.BaseStream.Position = 0x21;
                writer.Write((ushort)Height);
                writer.BaseStream.Position = 0x25;
                writer.Write((ushort)Length);
            }
            return bytes;
        }
        public static ushort DegToU16(float deg)
        {
            return (ushort)(deg * 65536 / 360);
        }
        public static float U16ToDeg(ushort u16)
        {
            return (float)u16 * 360 / 65536;
        }
        public void LoadModelData(string dir)
        {
            LoadModelDataFromID((int)ModelID, dir);
        }

        public void LoadModelData(bool interior)
        {
            string modelPath = Filesystem.GetBuildingModelPath(interior, (int)ModelID);

            if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
            {
                AppMessages.Error("Building " + ModelID + " could not be found in\n" + '"' + Path.GetDirectoryName(modelPath) + '"', "Building not found");
                return;
            }

            try
            {
                using (Stream fs = new FileStream(modelPath, FileMode.Open))
                {
                    this.NSBMDFile = NSBMDLoader.LoadNSBMD(fs);
                }
            }
            catch (FileNotFoundException)
            {
                AppMessages.Error("Building " + ModelID + " could not be found in\n" + '"' + Path.GetDirectoryName(modelPath) + '"', "Building not found");
            }
        }

        public void LoadModelDataFromID(int modelID, string bmDir)
        {
            string modelPath = Path.Combine(bmDir, modelID.ToString("D4"));

            try
            {
                using (Stream fs = new FileStream(modelPath, FileMode.Open))
                {
                    this.NSBMDFile = NSBMDLoader.LoadNSBMD(fs);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"Error loading building model ID {modelID} from path \"{modelPath}\": {ex.Message}");
                AppMessages.Error($"Building \"{modelID}\" could not be found at path \"{modelPath}\".\n" +
                    $"Map may not display correctly.", "Building not found");
            }
        }
    }
}

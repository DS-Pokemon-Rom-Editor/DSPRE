using System.Collections.Generic;
using System.IO;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /* ---------------------- HEADER DATA STRUCTURE (DPPt):----------------------------
        
       0x0  //  byte:       Area data value
       0x1  //  byte:       Unknown value
       0x2  //  ushort:     Matrix number
       0x4  //  ushort:     Script file number
       0x6  //  ushort:     Level script file number
       0x8  //  ushort:     Text Archive number
       0xA  //  ushort:     Day music track number
       0xC  //  ushort:     Night music track number
       0xE  //  ushort:     Wild Pok�mon file number
       0x10 //  ushort:     Event file number

       * D/P:
       0x12 //  ushort:     Index of map name in Text Archive #382 (US version)   
       
       * Plat:
       0x12 //  byte:       Index of map name in Text Archive #382 (US version)
       0x13 //  byte:       Map name textbox type value

       0x14 //  byte:       Weather value
       0x15 //  byte:       Camera value
       0x16 //  byte:       Boolean flag: show name when entering map
       0x17 //  byte:       Bitwise permission flags:

       -----------------    1: Allow Fly
       -----------------    2: ?
       -----------------    3: ?
       -----------------    4: Allow Bike usage
       -----------------    5: ?
       -----------------    6: ?
       -----------------    7: Esc. Rope
       -----------------    8: ?

    /* ---------------------- HEADER DATA STRUCTURE (HGSS):----------------------------
        
       0x0  //  byte:       Wild Pok�mon file number
       0x1  //  byte:       Area data value
       0x2  //  byte:       ?
       0x3  //  byte:       ?
       0x4  //  ushort:     Matrix number
       0x6  //  ushort:     Script file number
       0x8  //  ushort:     Level script file
       0xA  //  ushort:     Text Archive number
       0xC  //  ushort:     Day music track number
       0xE  //  ushort:     Night music track number
       0x10 //  ushort:     Event file number
       0x12 //  byte:       Index of map name in Text Archive #382 (US version)
       0x13 //  byte:       Map name textbox type value
       0x14 //  byte:       Weather value
       0x15 //  byte:       Camera value
       0x16 //  byte:       Follow mode (for the Pok�mon following hero)
       0x17 //  byte:       Bitwise permission flags:

        DPPT
       -----------------    1: Allow Fly
       -----------------    2: Allow Esc. Rope
       -----------------    3: Allow Running
       -----------------    4: Allow Bike 
       -----------------    5: Battle BG b4
       -----------------    6: Battle BG b3
       -----------------    7: Battle BG b2
       -----------------    8: Battle BG b1

        HGSS
       -----------------    1: Allow Bicycle
       -----------------    2: Allow Running (unused)
       -----------------    3: Allow Esc. Rope
       -----------------    4: Allow Fly
       -----------------    5: Allow outgoing Pokegear calls
       -----------------    6: Allow incoming Pokegear calls
       -----------------    7: Allow Pokegear radio signal

    ----------------------------------------------------------------------------------*/

    /// <summary>
    /// General class to store common map header data across all Gen IV Pok�mon NDS games
    /// </summary>
    public abstract class MapHeader : RomFile
    {
        /*System*/
        public static readonly string DefaultFilter = "DSPRE Header File (*.dsh; *.bin)|*.dsh;*.bin";
        public ushort ID { get; set; }
        public static readonly byte Length = 24;
        public static readonly string NameSeparator = " -   ";
        public const int HgssNullEncounterFileId = 0xff;

        /// <summary>Header 0 is the catch-all, not a place: matrix cells no real header claims carry it.</summary>
        public const ushort Everywhere = 0;
        public const int DpptNullEncounterFileId = 0xffff;

        public enum SearchableFields: byte
        {
            AreaDataID,
            CameraAngleID,
            EventFileID,
            InternalName,
            LevelScriptID,
            MatrixID,
            MusicDayID,
            //MusicDayName,
            MusicNightID,
            //MusicNightName,
            ScriptFileID,
            TextArchiveID,
            WeatherID,
        };



        #region Fields (10)
        public byte AreaDataID { get; set; }
        public byte CameraAngleID { get; set; }
        public ushort EventFileID { get; set; }
        public ushort LevelScriptID { get; set; }
        public ushort MatrixID { get; set; }
        public ushort ScriptFileID { get; set; }
        public ushort MusicDayID { get; set; }
        public ushort MusicNightID { get; set; }
        public byte LocationSpecifier { get; set; }
        public byte BattleBackground { get; set; }
        public ushort TextArchiveID { get; set; }
        public byte WeatherID { get; set; }
        public byte Flags { get; set; }
        public ushort WildPokemon { get; set; }
        #endregion Fields

        #region Methods (1)
        public static string BuildName(int headerID, string name)
        {
            return headerID.ToString("D3") + MapHeader.NameSeparator + name;
        }

        /// <summary>
        /// Reads just the location-name index (offset 0x12) of one header without building a full
        /// <see cref="MapHeader"/>, for the Header editor's location-grouped sidebar. Pass the
        /// already-known dynamic-headers flag so this doesn't re-probe the patch state per call.
        /// DP stores it as a ushort; Plat/HGSS as a byte.
        /// </summary>
        public static bool TryReadLocationNameIndex(ushort headerId, bool dynamicHeaders, out int locationNameIndex)
        {
            locationNameIndex = 0;
            byte[] data;
            try
            {
                if (dynamicHeaders)
                {
                    string path = Path.Combine(RomInfo.GameDirs[RomInfo.DirNames.DynamicHeaders].unpackedDir, headerId.ToString("D4"));
                    if (!File.Exists(path)) return false;
                    data = File.ReadAllBytes(path);
                }
                else
                {
                    long headerOffset = RomInfo.HeaderTableOffset + MapHeader.Length * headerId;
                    data = DSUtils.ReadFromFile(RomInfo.Arm9Path, headerOffset, MapHeader.Length);
                }
            }
            catch (System.Exception ex)
            {
                AppLogger.WarnOnce("MapHeader.TryReadLocationNameIndex", ex);
                return false;
            }

            if (data == null || data.Length < 0x14) return false;

            switch (RomInfo.GameFamily)
            {
                case RomInfo.GameFamilies.DP:
                    locationNameIndex = System.BitConverter.ToUInt16(data, 0x12);
                    return true;
                case RomInfo.GameFamilies.Plat:
                case RomInfo.GameFamilies.HGSS:
                    locationNameIndex = data[0x12];
                    return true;
                default:
                    return false;
            }
        }

        public static MapHeader LoadFromByteArray(byte[] headerData, ushort headerNumber, RomInfo.GameFamilies gameFamily = RomInfo.GameFamilies.NULL)
        {
            /* Encapsulate header data into the class appropriate for the gameVersion */
            if (headerData.Length < MapHeader.Length)
            {
                AppMessages.Error("File of header " + headerNumber + " is too small and can't store header data.", "Header file too small");
                return null;
            }

            if (gameFamily == RomInfo.GameFamilies.NULL)
            {
                gameFamily = RomInfo.GameFamily;
            }

            switch (gameFamily)
            {
                case RomInfo.GameFamilies.DP:
                    return new HeaderDP(headerNumber, new MemoryStream(headerData));
                case RomInfo.GameFamilies.Plat:
                    return new HeaderPt(headerNumber, new MemoryStream(headerData));
                default:
                    return new HeaderHGSS(headerNumber, new MemoryStream(headerData));
            }
        }

        public static MapHeader LoadFromFile(string filename, ushort headerNumber, long offsetInFile, RomInfo.GameFamilies gameFamily = RomInfo.GameFamilies.NULL)
        {
            /* Calculate header offset and load data */
            byte[] headerData = DSUtils.ReadFromFile(filename, offsetInFile, MapHeader.Length);
            return LoadFromByteArray(headerData, headerNumber, gameFamily);
        }

        public static MapHeader LoadFromARM9(ushort headerNumber, RomInfo.GameFamilies gameFamily = RomInfo.GameFamilies.NULL)
        {
            long headerOffset = RomInfo.HeaderTableOffset + MapHeader.Length * headerNumber;
            return LoadFromFile(RomInfo.Arm9Path, headerNumber, headerOffset, gameFamily);
        }

        public static MapHeader GetMapHeader(ushort headerNumber)
        {
            // Callers routinely probe a header number that comes from ROM data (e.g. a warp's
            // destination-header field) rather than from a UI control already clamped to a valid
            // range, so an out-of-range value here is an expected "no such header" case, not a
            // corrupt-file situation. Without this guard, LoadFromFile reads past the end of the
            // header table, LoadFromByteArray sees a too-short buffer and pops an "Header file too
            // small" error dialog for what is really just "there is no header this large", return
            // null quietly instead, matching what every caller (e.g. EventEditorViewModel.TestWarp)
            // already expects a not-found lookup to look like.
            if (headerNumber >= GetHeaderCount())
            {
                return null;
            }

            MapHeader mapHeader;

            if (UsesDynamicHeaders)
            {
                string path = Filesystem.GetDynamicHeaderPath(headerNumber);
                mapHeader = MapHeader.LoadFromFile(path, headerNumber, 0);
            }
            else
            {
                mapHeader = MapHeader.LoadFromARM9(headerNumber);
            }

            return mapHeader;
        }

        /// <summary>
        /// Whether headers live in the dynamic-headers archive instead of the ARM9 table. The flag is set from the
        /// ARM9 when a project loads and when the patch is applied; Diamond and Pearl have no such patch.
        /// </summary>
        public static bool UsesDynamicHeaders =>
            RomInfo.GameFamily != RomInfo.GameFamilies.DP && RomPatchState.FlagDynamicHeadersPatchApplied;

        /// <summary>Writes a header where the game reads it: the dynamic-headers archive or the ARM9 table.</summary>
        public static void Save(MapHeader header)
        {
            byte[] bytes = header.ToByteArray();
            if (UsesDynamicHeaders)
                DSUtils.WriteToFile(Filesystem.GetDynamicHeaderPath(header.ID), bytes, 0, 0, fmode: FileMode.Create);
            else
                ARM9.WriteBytes(bytes, (uint)(RomInfo.HeaderTableOffset + Length * header.ID));
        }

        /// <summary>A header's internal name as the names file stores it: ASCII, cut or zero-padded to its fixed length.</summary>
        public static byte[] InternalNameBytes(string name)
        {
            name ??= "";
            name = name.Substring(0, System.Math.Min(name.Length, RomInfo.InternalNameLength));
            return System.Text.Encoding.ASCII.GetBytes(name.PadRight(RomInfo.InternalNameLength, '\0'));
        }

        public static void WriteInternalName(int id, string name) =>
            DSUtils.WriteToFile(RomInfo.InternalNamesPath, InternalNameBytes(name), (uint)(id * RomInfo.InternalNameLength));

        /// <summary>Appends a header copied from header 0 with this internal name; dynamic headers only. Returns its id.</summary>
        /// <summary>Adds a header at the end, a copy of header <paramref name="copyFrom"/>; dynamic headers only.</summary>
        public static int AddDynamicHeader(string internalName, int copyFrom = 0)
        {
            int id = GetHeaderCount();
            if (copyFrom < 0 || copyFrom >= id) copyFrom = 0;
            ArchiveMembers.Write(Filesystem.GetDynamicHeaderPath(id), File.ReadAllBytes(Filesystem.GetDynamicHeaderPath(copyFrom)));
            WriteInternalName(id, internalName);
            return id;
        }

        /// <summary>Puts back a removed last header from its saved bytes; dynamic headers only.</summary>
        public static int RestoreDynamicHeader(byte[] data, string internalName)
        {
            int id = GetHeaderCount();
            ArchiveMembers.Put(RomInfo.DirNames.DynamicHeaders, id, data);
            WriteInternalName(id, internalName);
            return id;
        }

        /// <summary>Removes the last header and its internal name; dynamic headers only.</summary>
        public static void RemoveLastDynamicHeader()
        {
            int last = GetHeaderCount() - 1;
            ArchiveMembers.Delete(Filesystem.GetDynamicHeaderPath(last));
            using (DSUtils.EasyWriter writer = new DSUtils.EasyWriter(RomInfo.InternalNamesPath)) writer.EditSize(-RomInfo.InternalNameLength);
        }

        public static int GetHeaderCount()
        {
            int headerCount;
            if (UsesDynamicHeaders)
            {
                headerCount = Filesystem.GetDynamicHeadersCount();
            }
            else
            {
                headerCount = RomInfo.GetHeaderCount();
            }

            return headerCount;
        }

        public void SaveFile()
        {
            /* Check if dynamic headers patch has been applied, and save header to arm9 or a/0/5/0 accordingly */
            if (UsesDynamicHeaders)
            {
                string path = Filesystem.GetDynamicHeaderPath(ID);
                DSUtils.WriteToFile(path, this.ToByteArray(), 0, 0, fmode: FileMode.Create);
            }
            else
            {
                uint headerOffset = (uint)(RomInfo.HeaderTableOffset + MapHeader.Length * this.ID);
                ARM9.WriteBytes(this.ToByteArray(), headerOffset);
            }
        }


        #endregion
    }

    /// <summary>
    /// Class to store map header data from Pok�mon D and P
    /// </summary>
    public class HeaderDP : MapHeader
    {
        #region Fields (5)
        public byte Unknown1 { get; set; }
        public ushort LocationName { get; set; }
        #endregion Fields

        #region Constructors (1)
        public HeaderDP(ushort headerNumber, Stream data)
        {
            this.ID = headerNumber;
            using (BinaryReader reader = new BinaryReader(data))
            {
                AreaDataID = reader.ReadByte();
                Unknown1 = reader.ReadByte();
                MatrixID = reader.ReadUInt16();
                ScriptFileID = reader.ReadUInt16();
                LevelScriptID = reader.ReadUInt16();
                TextArchiveID = reader.ReadUInt16();
                MusicDayID = reader.ReadUInt16();
                MusicNightID = reader.ReadUInt16();
                WildPokemon = reader.ReadUInt16();
                EventFileID = reader.ReadUInt16();
                LocationName = reader.ReadUInt16();
                WeatherID = reader.ReadByte();
                CameraAngleID = reader.ReadByte();
                LocationSpecifier = reader.ReadByte();

                byte mapSettings = reader.ReadByte();
                BattleBackground = (byte)(mapSettings & 0b_1111);
                Flags = (byte)(mapSettings >> 4 & 0b_1111);
            }
        }
        #endregion Constructors

        #region Methods (1)
        public override byte[] ToByteArray()
        {
            MemoryStream newData = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(newData))
            {
                writer.Write(AreaDataID);
                writer.Write(Unknown1);
                writer.Write(MatrixID);
                writer.Write(ScriptFileID);
                writer.Write(LevelScriptID);
                writer.Write(TextArchiveID);
                writer.Write(MusicDayID);
                writer.Write(MusicNightID);
                writer.Write(WildPokemon);
                writer.Write(EventFileID);
                writer.Write(LocationName);
                writer.Write(WeatherID);
                writer.Write(CameraAngleID);
                writer.Write(LocationSpecifier);

                byte mapSettings = (byte)((BattleBackground & 0b_1111) + ((Flags & 0b_1111) << 4));
                writer.Write(mapSettings);
            }
            return newData.ToArray();
        }
        #endregion
    }

    /// <summary>
    /// Class to store map header data from Pok�mon Plat
    /// </summary>
    public class HeaderPt : MapHeader
    {
        #region Fields (5)
        public byte AreaIcon { get; set; }
        public byte LocationName { get; set; }
        public byte Unknown1 { get; set; }
        #endregion Fields

        #region Constructors (1)
        public HeaderPt(ushort headerNumber, Stream data)
        {
            this.ID = headerNumber;
            using (BinaryReader reader = new BinaryReader(data))
            {
                try
                {
                    AreaDataID = reader.ReadByte();
                    Unknown1 = reader.ReadByte();
                    MatrixID = reader.ReadUInt16();
                    ScriptFileID = reader.ReadUInt16();
                    LevelScriptID = reader.ReadUInt16();
                    TextArchiveID = reader.ReadUInt16();
                    MusicDayID = reader.ReadUInt16();
                    MusicNightID = reader.ReadUInt16();
                    WildPokemon = reader.ReadUInt16();
                    EventFileID = reader.ReadUInt16();
                    LocationName = reader.ReadByte();
                    AreaIcon = reader.ReadByte();
                    WeatherID = reader.ReadByte();
                    CameraAngleID = reader.ReadByte();

                    ushort mapSettings = reader.ReadUInt16();
                    LocationSpecifier = (byte)(mapSettings & 0b_1111_111);
                    BattleBackground = (byte)(mapSettings >> 7 & 0b_1111_1);
                    Flags = (byte)(mapSettings >> 12 & 0b_1111);

                }
                catch (EndOfStreamException)
                {
                    AppMessages.Error("Error loading header " + ID + '.', "Unexpected EOF");
                }
            }
        }
        #endregion Constructors

        #region Methods(1)
        public override byte[] ToByteArray()
        {
            MemoryStream newData = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(newData))
            {
                writer.Write(AreaDataID);
                writer.Write(Unknown1);
                writer.Write(MatrixID);
                writer.Write(ScriptFileID);
                writer.Write(LevelScriptID);
                writer.Write(TextArchiveID);
                writer.Write(MusicDayID);
                writer.Write(MusicNightID);
                writer.Write(WildPokemon);
                writer.Write(EventFileID);
                writer.Write(LocationName);
                writer.Write(AreaIcon);
                writer.Write(WeatherID);
                writer.Write(CameraAngleID);

                ushort mapSettings = (ushort)((LocationSpecifier & 0b_1111_111) + ((BattleBackground & 0b_1111_1) << 7) + ((Flags & 0b_1111) << 12));
                writer.Write(mapSettings);
            }
            return newData.ToArray();
        }
        #endregion
    }

    /// <summary>
    /// Class to store map header data from Pok�mon HG and SS
    /// </summary>
    public class HeaderHGSS : MapHeader
    {
        #region Fields (7)
        public byte AreaIcon { get; set; }
        public byte FollowMode { get; set; }
        public byte LocationName { get; set; }
        public byte LocationType { get; set; }  //4 bits only
        public byte Unknown0 { get; set; } //4 bits only
        public byte MomCallIntroParam { get; set; } //4 bits only, offset into Mom's Pokegear call intro message
        public byte WorldmapX { get; set; } //6 bits only
        public byte WorldmapY { get; set; } //6 bits only
        public bool KantoFlag { get; set; }
        #endregion

        #region Constructors (1)
        public HeaderHGSS(ushort headerNumber, Stream data)
        {
            this.ID = headerNumber;
            using (BinaryReader reader = new BinaryReader(data))
            {
                try
                {
                    WildPokemon = reader.ReadByte();
                    AreaDataID = reader.ReadByte();

                    ushort coords = reader.ReadUInt16();
                    Unknown0 = (byte)(coords & 0b_1111); //get 4 bits
                    WorldmapX = (byte)((coords >> 4) & 0b_1111_11); //get 6 bits after the first 4
                    WorldmapY = (byte)((coords >> 10) & 0b_1111_11); //get 6 bits after the first 10

                    MatrixID = reader.ReadUInt16();
                    ScriptFileID = reader.ReadUInt16();
                    LevelScriptID = reader.ReadUInt16();
                    TextArchiveID = reader.ReadUInt16();
                    MusicDayID = reader.ReadUInt16();
                    MusicNightID = reader.ReadUInt16();
                    EventFileID = reader.ReadUInt16();
                    LocationName = reader.ReadByte();
                    
                    byte areaProperties = reader.ReadByte();
                    AreaIcon = (byte)(areaProperties & 0b_1111); //get 4 bits
                    MomCallIntroParam = (byte)((areaProperties >> 4) & 0b_1111); //get 4 bits after the first 4

                    uint last32 = reader.ReadUInt32();
                    KantoFlag = (last32 & 0b_1) == 1; //get 1 bit
                    WeatherID = (byte)((last32 >> 1) & 0b_1111_111); //get 7 bits after the first one

                    LocationType = (byte)((last32 >> 8) & 0b_1111); //get 4 bits after the first 8
                    CameraAngleID = (byte)((last32 >> 12) & 0b_1111_11); //get 6 bits after the first 12
                    FollowMode = (byte)((last32 >> 18) & 0b_11); //get 2 bits after the first 17
                    BattleBackground = (byte)((last32 >> 20) & 0b_1111_1); //get 5 bits after the first 19
                    Flags = (byte)(last32 >> 25 & 0b_1111_111); //get 7 bits after the first 24

                }
                catch (EndOfStreamException)
                {
                    AppMessages.Error("Error loading header " + ID + '.', "Unexpected EOF");
                    ID = ushort.MaxValue;
                }
            }
        }
        #endregion Constructors

        #region Methods(1)
        public override byte[] ToByteArray()
        {
            MemoryStream newData = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(newData))
            {
                writer.Write((byte)WildPokemon);
                writer.Write(AreaDataID);

                ushort worldMapCoordinates = (ushort)((Unknown0 & 0b_1111) + ((WorldmapX & 0b_1111_11) << 4) + ((WorldmapY & 0b_1111_11) << 10));
                writer.Write(worldMapCoordinates);

                writer.Write(MatrixID);
                writer.Write(ScriptFileID);
                writer.Write(LevelScriptID);
                writer.Write(TextArchiveID);
                writer.Write(MusicDayID);
                writer.Write(MusicNightID);
                writer.Write(EventFileID);
                writer.Write(LocationName);

                byte areaProperties = (byte)((AreaIcon & 0b_1111) + ((MomCallIntroParam & 0b_1111) << 4));
                writer.Write(areaProperties);

                uint last32 = (uint)(((WeatherID & 0b_1111_111) << 1) +
                    ((LocationType & 0b_1111) << 8) +
                    ((CameraAngleID & 0b_1111_11) << 12) +
                    ((FollowMode & 0b_11) << 18) +
                    ((BattleBackground & 0b_1111_1) << 20) +
                    ((Flags & 0b_1111_111) << 25));

                if (KantoFlag)
                {
                    last32++;
                }

                writer.Write(last32);
            }
            return newData.ToArray();
        }
        #endregion
    }
}

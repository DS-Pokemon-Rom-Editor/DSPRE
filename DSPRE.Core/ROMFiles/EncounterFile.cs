using System;
using System.Collections.Generic;
using System.IO;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    /* ---------------------- MAP HEADER STRUCTURE (DPPt):----------------------------
        
       0x0  //  byte:       Walking encounter rate
       0x4  //  byte:       Level of
       0x2  //  ushort:     Matrix number
       0x4  //  ushort:     Script file number
       0x6  //  ushort:     Level script file number
       0x8  //  ushort:     Text Archive number
       0xA  //  ushort:     Day music track number
       0xC  //  ushort:     Night music track number
       0xE  //  ushort:     Wild Pokemon file number
       0x10 //  ushort:     Event file number

       * Diamond/Pearl:
       0x12 //  ushort:     Index of map name in Text Archive #382 (US version)   
       
       * Platinum:
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

    /* ---------------------- MAP HEADER STRUCTURE (HGSS):----------------------------
        
       0x0  //  byte:       Wild Pokemon file number
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
       0x16 //  byte:       Follow mode (for the Pokemon following hero)
       0x17 //  byte:       Bitwise permission flags:

       -----------------    1: Allow Fly
       -----------------    2: ?
       -----------------    3: ?
       -----------------    4: Allow Bike usage
       -----------------    5: ?
       -----------------    6: ?
       -----------------    7: Esc. Rope
       -----------------    8: ?

    ----------------------------------------------------------------------------------*/

    /// <summary>
    /// The wild Pokemon a map holds, across all four Gen IV games.
    ///
    /// The block above documents the map header, which is a different file; it is kept because the header
    /// is what points at the wild file. The wild file itself is 196 bytes in HeartGold and 424 bytes in
    /// Diamond, Pearl and Platinum, and the
    /// two are laid out quite differently: HeartGold writes each fishing entry as min level, max level,
    /// species, while Platinum writes max level, min level, species.
    /// </summary>
    public abstract class EncounterFile : RomFile
    {
        public const string MsgFixed = " (already fixed)";
        public const string Extension = "wld";
        #region Fields (19)

        /* Encounter rates */
        public byte GoodRodRate { get; set; }
        public byte OldRodRate { get; set; }
        public byte SuperRodRate { get; set; }
        public byte SurfRate { get; set; }
        public byte WalkingRate { get; set; }

        /// <summary>Whether any kind of encounter here can happen at all.</summary>
        public virtual bool HasEncounters => WalkingRate > 0 || SurfRate > 0 || OldRodRate > 0 || GoodRodRate > 0 || SuperRodRate > 0;

        /* Levels */
        public byte[] GoodRodMaxLevels = new byte[5];
        public byte[] GoodRodMinLevels = new byte[5];
        public byte[] OldRodMaxLevels = new byte[5];
        public byte[] OldRodMinLevels = new byte[5];
        public byte[] WalkingLevels = new byte[12];
        public byte[] SuperRodMaxLevels = new byte[5];
        public byte[] SuperRodMinLevels = new byte[5];
        public byte[] SurfMaxLevels = new byte[5];
        public byte[] SurfMinLevels = new byte[5];

        /* Encounters */
        public ushort[] GoodRodPokemon = new ushort[5];
        public ushort[] OldRodPokemon = new ushort[5];
        public ushort[] SuperRodPokemon = new ushort[5];
        public ushort[] SurfPokemon = new ushort[5];
        public ushort[] SwarmPokemon { get; set; }  //2 for DPPt, 4 for HGSS
        #endregion

        #region Methods (1)
        public void SaveToFileDefaultDir(int IDtoReplace, bool showSuccessMessage = true)
        {
            SaveToFileDefaultDir(DirNames.Encounters, IDtoReplace, showSuccessMessage);
        }

        public void ReportErrors(List<string> errorList)
        {
            string fullError = "The following sections of this encounter file couldn't be read correctly: " + Environment.NewLine;

            string errorSections = "";
            foreach (string elem in errorList)
            {
                errorSections += "- " + elem + Environment.NewLine;
            }
            fullError += errorSections;

            fullError += Environment.NewLine + "It is recommended that you check them before resaving.";
            
            if (errorSections.Contains(MsgFixed))
            {
                fullError += Environment.NewLine + "Fields marked as " + '\'' + MsgFixed + '\'' + " have been repaired with a value of 0.";
            }

            AppMessages.Error(fullError, "Encounter File error");
        }
        #endregion
    }

    /// <summary>
    /// Class to store wild Pokemon data from Pokemon Diamond, Pearl and Platinum
    /// </summary>
    public class EncounterFileDPPt : EncounterFile
    {
        #region Fields (9)       
        /* Field encounters */
        public uint[] RadarPokemon = new uint[4];
        public uint[] WalkingPokemon = new uint[12];

        /* Time-specific encounters */
        public uint[] DayPokemon = new uint[2];
        public uint[] NightPokemon = new uint[2];

        /* Dual slot exclusives */
        public uint[] RubyPokemon = new uint[2];
        public uint[] SapphirePokemon = new uint[2];
        public uint[] EmeraldPokemon = new uint[2];
        public uint[] FireRedPokemon = new uint[2];
        public uint[] LeafGreenPokemon = new uint[2];

        /* Form Data */
        public uint[] FormRates = new uint[5];
        public uint UnownTable = 0;

        // The writer starts from these, so bytes it does not model (an unused table, slot padding) survive a save.
        private readonly byte[] _original;

        #endregion

        #region Constructors (1)
        public EncounterFileDPPt(Stream data)
        {
            using (MemoryStream copy = new MemoryStream())
            {
                using (data) data.CopyTo(copy);
                _original = copy.ToArray();
            }
            using (BinaryReader reader = new BinaryReader(new MemoryStream(_original)))
            {
                List<string> fieldsWithErrors = new List<string>();

                /* Walking encounters */
                try
                {
                    WalkingRate = (byte)reader.ReadInt32();
                    for (int i = 0; i < 12; i++)
                    {
                        WalkingLevels[i] = (byte)reader.ReadUInt32();
                        WalkingPokemon[i] = reader.ReadUInt32();
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.WarnOnce("EncounterFileDPPt.ctor", ex);
                    fieldsWithErrors.Add("Regular encounters");
                }

                /* Swarms */
                SwarmPokemon = new ushort[2];
                for (int i = 0; i < 2; i++)
                {
                    try
                    {
                        SwarmPokemon[i] = (ushort)reader.ReadUInt32();
                    }
                    catch (EndOfStreamException)
                    {
                        SwarmPokemon[i] = 0x00;
                        fieldsWithErrors.Add("Swarms" + ' ' + '[' + i + ']' + MsgFixed);
                    }
                }

                /* Time-specific encounters */
                for (int i = 0; i < 2; i++)
                {
                    try
                    {
                        DayPokemon[i] = reader.ReadUInt32();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileDPPt.ctor", ex);
                        DayPokemon[i] = 0x00;
                        fieldsWithErrors.Add("Day encounters" + ' ' + '[' + i + ']' + MsgFixed);
                    }
                }

                for (int i = 0; i < 2; i++)
                {
                    try
                    {
                        NightPokemon[i] = reader.ReadUInt32();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileDPPt.ctor", ex);
                        NightPokemon[i] = 0x00;
                        fieldsWithErrors.Add("Night encounters" + ' ' + '[' + i + ']' + MsgFixed);
                    }
                }

                /* Poké-Radar encounters */
                for (int i = 0; i < 4; i++)
                {
                    try
                    {
                        RadarPokemon[i] = reader.ReadUInt32();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileDPPt.ctor", ex);
                        RadarPokemon[i] = 0x00;
                        fieldsWithErrors.Add("PokéRadar" + ' ' + '[' + i + ']' + MsgFixed);
                    }
                }

                /* Form data */
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        FormRates[i] = reader.ReadUInt32();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileDPPt.ctor", ex);
                        FormRates[i] = 0x00;
                        fieldsWithErrors.Add("Form data" + ' ' + '[' + i + ']' + MsgFixed);
                    }
                }

                try
                {
                    UnownTable = reader.ReadUInt32();
                }
                catch (Exception ex)
                {
                    AppLogger.WarnOnce("EncounterFileDPPt.ctor", ex);
                    UnownTable = 0x00;
                    fieldsWithErrors.Add("Unknown table" + MsgFixed);
                }

                /* Dual-slot encounters */
                for (int i = 0; i < 2; i++)
                {
                    try
                    {
                        RubyPokemon[i] = reader.ReadUInt32();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileDPPt.ctor", ex);
                        RubyPokemon[i] = 0x00;
                        fieldsWithErrors.Add("Dual-Slot Ruby" + ' ' + '[' + i + ']' + MsgFixed);
                    }
                }

                for (int i = 0; i < 2; i++)
                {
                    try
                    {
                        SapphirePokemon[i] = reader.ReadUInt32();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileDPPt.ctor", ex);
                        SapphirePokemon[i] = 0x00;
                        fieldsWithErrors.Add("Dual-Slot Sapphire" + ' ' + '[' + i + ']' + MsgFixed);
                    }
                }

                for (int i = 0; i < 2; i++)
                {
                    try
                    {
                        EmeraldPokemon[i] = reader.ReadUInt32();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileDPPt.ctor", ex);
                        EmeraldPokemon[i] = 0x00;
                        fieldsWithErrors.Add("Dual-Slot Emerald" + ' ' + '[' + i + ']' + MsgFixed);
                    }
                }

                for (int i = 0; i < 2; i++)
                {
                    try
                    {
                        FireRedPokemon[i] = reader.ReadUInt32();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileDPPt.ctor", ex);
                        FireRedPokemon[i] = 0x00;
                        fieldsWithErrors.Add("Dual-Slot FireRed" + ' ' + '[' + i + ']' + MsgFixed);
                    }
                }

                for (int i = 0; i < 2; i++)
                {
                    try
                    {
                        LeafGreenPokemon[i] = reader.ReadUInt32();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileDPPt.ctor", ex);
                        LeafGreenPokemon[i] = 0x00;
                        fieldsWithErrors.Add("Dual-Slot LeafGreen" + ' ' + '[' + i + ']' + MsgFixed);
                    }
                }

                /* Surf encounters */
                try
                {
                    SurfRate = (byte)reader.ReadInt32();
                    for (int i = 0; i < 5; i++)
                    {
                        SurfMaxLevels[i] = reader.ReadByte();
                        SurfMinLevels[i] = reader.ReadByte();
                        reader.BaseStream.Position += 0x2;
                        SurfPokemon[i] = (ushort)reader.ReadUInt32();
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.WarnOnce("EncounterFileDPPt.ctor", ex);
                    fieldsWithErrors.Add("Surf");
                }

                reader.BaseStream.Position = 0x124;

                /* Old Rod encounters */
                try
                {
                    OldRodRate = (byte)reader.ReadInt32();
                    for (int i = 0; i < 5; i++)
                    {
                        OldRodMaxLevels[i] = reader.ReadByte();
                        OldRodMinLevels[i] = reader.ReadByte();

                        reader.BaseStream.Position += 0x2;
                        OldRodPokemon[i] = (ushort)reader.ReadUInt32();
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.WarnOnce("EncounterFileDPPt.ctor", ex);
                    fieldsWithErrors.Add("Old Rod");
                }

                /* Good Rod encounters */
                try
                {
                    GoodRodRate = (byte)reader.ReadInt32();
                    for (int i = 0; i < 5; i++)
                    {
                        GoodRodMaxLevels[i] = reader.ReadByte();
                        GoodRodMinLevels[i] = reader.ReadByte();

                        reader.BaseStream.Position += 0x2;
                        GoodRodPokemon[i] = (ushort)reader.ReadUInt32();
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.WarnOnce("EncounterFileDPPt.ctor", ex);
                    fieldsWithErrors.Add("Good Rod");
                }

                /* Super Rod encounters */
                try
                {
                    SuperRodRate = (byte)reader.ReadInt32();
                    for (int i = 0; i < 5; i++)
                    {
                        SuperRodMaxLevels[i] = reader.ReadByte();
                        SuperRodMinLevels[i] = reader.ReadByte();

                        reader.BaseStream.Position += 0x2;
                        SuperRodPokemon[i] = (ushort)reader.ReadUInt32();
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.WarnOnce("EncounterFileDPPt.ctor", ex);
                    fieldsWithErrors.Add("Super Rod");
                }

                if (fieldsWithErrors.Count > 0)
                {
                    ReportErrors(fieldsWithErrors);
                }
            }
        }

        public EncounterFileDPPt(int ID) : this(new FileStream(Path.Combine(RomInfo.GameDirs[DirNames.Encounters].unpackedDir, ID.ToString("D4")), FileMode.Open, FileAccess.Read)) { }
        public EncounterFileDPPt()
        {
            SwarmPokemon = new ushort[2];
        }
        #endregion Constructors

        #region Methods (1)
        public override byte[] ToByteArray()
        {
            MemoryStream newData = new MemoryStream();
            if (_original != null) { newData.Write(_original, 0, _original.Length); newData.Position = 0; }
            using (BinaryWriter writer = new BinaryWriter(newData))
            {
                writer.Write((uint)WalkingRate);

                /* Walking encounters */
                for (int i = 0; i < 12; i++)
                {
                    writer.Write((uint)WalkingLevels[i]);
                    writer.Write(WalkingPokemon[i]);
                }

                /* Swarms */
                for (int i = 0; i < 2; i++)
                {
                    writer.Write((uint)SwarmPokemon[i]);
                }

                /* Time-specific encounters */
                for (int i = 0; i < 2; i++)
                {
                    writer.Write(DayPokemon[i]);
                }

                for (int i = 0; i < 2; i++)
                {
                    writer.Write(NightPokemon[i]);
                }

                /* Poké-Radar encounters */
                for (int i = 0; i < 4; i++)
                {
                    writer.Write(RadarPokemon[i]);
                }

                /* Form data */
                for (int i = 0; i < 5; i++)
                {
                    writer.Write(FormRates[i]);
                }

                /* Unknown table */
                writer.Write(UnownTable);

                /* Dual-slot encounters */
                for (int i = 0; i < 2; i++)
                {
                    writer.Write(RubyPokemon[i]);
                }

                for (int i = 0; i < 2; i++)
                {
                    writer.Write(SapphirePokemon[i]);
                }

                for (int i = 0; i < 2; i++)
                {
                    writer.Write(EmeraldPokemon[i]);
                }

                for (int i = 0; i < 2; i++)
                {
                    writer.Write(FireRedPokemon[i]);
                }

                for (int i = 0; i < 2; i++)
                {
                    writer.Write(LeafGreenPokemon[i]);
                }

                /* Surf encounters */
                writer.Write((uint)SurfRate);
                for (int i = 0; i < 5; i++)
                {
                    writer.Write(SurfMaxLevels[i]);
                    writer.Write(SurfMinLevels[i]);
                    writer.BaseStream.Position += 0x2;
                    writer.Write((uint)SurfPokemon[i]);
                }

                writer.BaseStream.Position = 0x124;

                /* Old Rod encounters */
                writer.Write((uint)OldRodRate);
                for (int i = 0; i < 5; i++)
                {
                    writer.Write(OldRodMaxLevels[i]);
                    writer.Write(OldRodMinLevels[i]);
                    writer.BaseStream.Position += 0x2;
                    writer.Write((uint)OldRodPokemon[i]);
                }

                /* Good Rod encounters */
                writer.Write((uint)GoodRodRate);
                for (int i = 0; i < 5; i++)
                {
                    writer.Write(GoodRodMaxLevels[i]);
                    writer.Write(GoodRodMinLevels[i]);
                    writer.BaseStream.Position += 0x2;
                    writer.Write((uint)GoodRodPokemon[i]);
                }

                /* Super Rod encounters */
                writer.Write((uint)SuperRodRate);
                for (int i = 0; i < 5; i++)
                {
                    writer.Write(SuperRodMaxLevels[i]);
                    writer.Write(SuperRodMinLevels[i]);
                    writer.BaseStream.Position += 0x2;
                    writer.Write((uint)SuperRodPokemon[i]);
                }
            }
            return newData.ToArray();
        }
        public void SaveToFileExplorePath(string suggestedFileName, bool showSuccessMessage = true)
        {
            SaveToFileExplorePath("DPPt Encounter File", EncounterFile.Extension, suggestedFileName, showSuccessMessage);
        }
        #endregion
    }

    /// <summary>
    /// Class to store wild Pokemon data from Pokemon HeartGold and SoulSilver
    /// </summary>
    public class EncounterFileHGSS : EncounterFile
    {
        #region Fields (9)
        public byte RockSmashRate;

        public override bool HasEncounters => base.HasEncounters || RockSmashRate > 0;
        public ushort[] MorningPokemon = new ushort[12];
        public ushort[] DayPokemon = new ushort[12];
        public ushort[] NightPokemon = new ushort[12];
        public ushort[] HoennMusicPokemon = new ushort[2];
        public ushort[] SinnohMusicPokemon = new ushort[2];
        public ushort[] RockSmashPokemon = new ushort[2];
        public byte[] RockSmashMinLevels = new byte[2];
        public byte[] RockSmashMaxLevels = new byte[2];
        #endregion

        #region Constructors
        public EncounterFileHGSS(Stream data)
        {
            using (BinaryReader reader = new BinaryReader(data))
            {
                List<string> fieldsWithErrors = new List<string>();

                /* Encounter rates */
                try
                {
                    WalkingRate = reader.ReadByte();
                }
                catch (Exception ex)
                {
                    AppLogger.WarnOnce("EncounterFileHGSS.ctor", ex);
                    WalkingRate = 0x00;
                    fieldsWithErrors.Add("Regular Encounters rate" + MsgFixed);
                }

                try
                {
                    SurfRate = reader.ReadByte();
                }
                catch (Exception ex)
                {
                    AppLogger.WarnOnce("EncounterFileHGSS.ctor", ex);
                    SurfRate = 0x00;
                    fieldsWithErrors.Add("Surf rate" + MsgFixed);
                }

                try
                { 
                    RockSmashRate = reader.ReadByte();
                }
                catch (Exception ex)
                {
                    AppLogger.WarnOnce("EncounterFileHGSS.ctor", ex);
                    RockSmashRate = 0x00;
                    fieldsWithErrors.Add("Rock Smash rate" + MsgFixed);
                }

                try
                { 
                    OldRodRate = reader.ReadByte();
                }
                catch (Exception ex)
                {
                    AppLogger.WarnOnce("EncounterFileHGSS.ctor", ex);
                    OldRodRate = 0x00;
                    fieldsWithErrors.Add("Old Rod rate" + MsgFixed);
                }

                try
                { 
                    GoodRodRate = reader.ReadByte();
                }
                catch (Exception ex)
                {
                    AppLogger.WarnOnce("EncounterFileHGSS.ctor", ex);
                    GoodRodRate = 0x00;
                    fieldsWithErrors.Add("Good Rod rate" + MsgFixed);
                }

                try
                { 
                    SuperRodRate = reader.ReadByte();
                }
                catch (Exception ex)
                {
                    AppLogger.WarnOnce("EncounterFileHGSS.ctor", ex);
                    SuperRodRate = 0x00;
                    fieldsWithErrors.Add("Super Rod rate" + MsgFixed);
                }

                reader.BaseStream.Position += 0x2;

                /* Walking encounters levels */
                for (int i = 0; i < 12; i++)
                {
                    try
                    { 
                        WalkingLevels[i] = reader.ReadByte();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileHGSS.ctor", ex);
                        WalkingLevels[i] = 0x00;
                        fieldsWithErrors.Add("Regular Encounters" + ' ' + '[' + i + ']' + MsgFixed);
                    }
                }

                /* Morning walking encounters */
                for (int i = 0; i < 12; i++)
                {
                    try
                    {
                        MorningPokemon[i] = reader.ReadUInt16();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileHGSS.ctor", ex);
                        MorningPokemon[i] = 0x00;
                        fieldsWithErrors.Add("Morning Encounters" + ' ' + '[' + i + ']' + MsgFixed);
                    }
                }

                /* Day walking encounters */
                for (int i = 0; i < 12; i++)
                {
                    try
                    { 
                        DayPokemon[i] = reader.ReadUInt16();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileHGSS.ctor", ex);
                        DayPokemon[i] = 0x00;
                        fieldsWithErrors.Add("Day Encounters" + ' ' + '[' + i + ']' + MsgFixed);
                    }
                }

                /* Night walking encounters */
                for (int i = 0; i < 12; i++)
                {
                    try
                    { 
                        NightPokemon[i] = reader.ReadUInt16();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileHGSS.ctor", ex);
                        NightPokemon[i] = 0x00;
                        fieldsWithErrors.Add("Night Encounters" + ' ' + '[' + i + ']' + MsgFixed);
                    }
                }

                /* PokéGear music encounters */
                for (int i = 0; i < 2; i++)
                {
                    try
                    {
                        HoennMusicPokemon[i] = reader.ReadUInt16();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileHGSS.ctor", ex);
                        HoennMusicPokemon[i] = 0x00;
                        fieldsWithErrors.Add("Hoenn Music Encounters" + ' ' + '[' + i + ']' + MsgFixed);
                    }
                }

                for (int i = 0; i < 2; i++)
                {
                    try
                    {  
                        SinnohMusicPokemon[i] = reader.ReadUInt16();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileHGSS.ctor", ex);
                        SinnohMusicPokemon[i] = 0x00;
                        fieldsWithErrors.Add("Sinnoh Music Encounters" + ' ' + '[' + i + ']' + MsgFixed);
                    }
                }

                /* Surf encounters */
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        SurfMinLevels[i] = reader.ReadByte();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileHGSS.ctor", ex);
                        SurfMinLevels[i] = 0x01;
                        fieldsWithErrors.Add("Surf Encounters" + ' ' + '[' + i + ']' + " min. level" + MsgFixed);
                    }

                    try
                    {
                        SurfMaxLevels[i] = reader.ReadByte();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileHGSS.ctor", ex);
                        SurfMaxLevels[i] = 0x01;
                        fieldsWithErrors.Add("Surf Encounters" + ' ' + '[' + i + ']' + " max. level" + MsgFixed);
                    }

                    try
                    {
                        SurfPokemon[i] = reader.ReadUInt16();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileHGSS.ctor", ex);
                        SurfPokemon[i] = 0x00;
                        fieldsWithErrors.Add("Surf Encounters" + ' ' + '[' + i + ']' + " Pokémon" + MsgFixed);
                    }
                }

                /* Rock Smash encounters */
                for (int i = 0; i < 2; i++)
                {
                    try
                    {
                        RockSmashMinLevels[i] = reader.ReadByte();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileHGSS.ctor", ex);
                        RockSmashMinLevels[i] = 0x01;
                        fieldsWithErrors.Add("Rock Smash Encounters" + ' ' + '[' + i + ']' + " min. level" + MsgFixed);
                    }

                    try
                    {
                        RockSmashMaxLevels[i] = reader.ReadByte();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileHGSS.ctor", ex);
                        RockSmashMaxLevels[i] = 0x01;
                        fieldsWithErrors.Add("Rock Smash Encounters" + ' ' + '[' + i + ']' + " max. level" + MsgFixed);
                    }

                    try
                    {
                        RockSmashPokemon[i] = reader.ReadUInt16();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileHGSS.ctor", ex);
                        RockSmashPokemon[i] = 0x00;
                        fieldsWithErrors.Add("Rock Smash Encounters" + ' ' + '[' + i + ']' + " Pokémon" + MsgFixed);
                    }
                }

                /* Old Rod encounters */
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        OldRodMinLevels[i] = reader.ReadByte();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileHGSS.ctor", ex);
                        OldRodMinLevels[i] = 0x01;
                        fieldsWithErrors.Add("Old Rod Encounters" + ' ' + '[' + i + ']' + " min. level" + MsgFixed);
                    }

                    try
                    {
                        OldRodMaxLevels[i] = reader.ReadByte();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileHGSS.ctor", ex);
                        OldRodMaxLevels[i] = 0x01;
                        fieldsWithErrors.Add("Old Rod Encounters" + ' ' + '[' + i + ']' + " max. level" + MsgFixed);
                    }

                    try
                    {
                        OldRodPokemon[i] = reader.ReadUInt16();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileHGSS.ctor", ex);
                        OldRodPokemon[i] = 0x00;
                        fieldsWithErrors.Add("Old Rod Encounters" + ' ' + '[' + i + ']' + " Pokémon" + MsgFixed);
                    }
                }

                /* Good Rod encounters */
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        GoodRodMinLevels[i] = reader.ReadByte();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileHGSS.ctor", ex);
                        GoodRodMinLevels[i] = 0x01;
                        fieldsWithErrors.Add("Good Rod Encounters" + ' ' + '[' + i + ']' + " min. level" + MsgFixed);
                    }

                    try
                    {
                        GoodRodMaxLevels[i] = reader.ReadByte();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileHGSS.ctor", ex);
                        GoodRodMaxLevels[i] = 0x01;
                        fieldsWithErrors.Add("Good Rod Encounters" + ' ' + '[' + i + ']' + " max. level" + MsgFixed);
                    }

                    try
                    {
                        GoodRodPokemon[i] = reader.ReadUInt16();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileHGSS.ctor", ex);
                        GoodRodPokemon[i] = 0x00;
                        fieldsWithErrors.Add("Good Rod Encounters" + ' ' + '[' + i + ']' + " Pokémon" + MsgFixed);
                    }
                }

                /* Super Rod encounters */
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        SuperRodMinLevels[i] = reader.ReadByte();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileHGSS.ctor", ex);
                        SuperRodMinLevels[i] = 0x01;
                        fieldsWithErrors.Add("Super Rod Encounters" + ' ' + '[' + i + ']' + " min. level" + MsgFixed);
                    }

                    try
                    {
                        SuperRodMaxLevels[i] = reader.ReadByte();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileHGSS.ctor", ex);
                        SuperRodMaxLevels[i] = 0x01;
                        fieldsWithErrors.Add("Super Rod Encounters" + ' ' + '[' + i + ']' + " max. level" + MsgFixed);
                    }

                    try
                    {
                        SuperRodPokemon[i] = reader.ReadUInt16();
                    }
                    catch (Exception ex)
                    {
                        AppLogger.WarnOnce("EncounterFileHGSS.ctor", ex);
                        SuperRodPokemon[i] = 0x00;
                        fieldsWithErrors.Add("Super Rod Encounters" + ' ' + '[' + i + ']' + " Pokémon" + MsgFixed);
                    }
                }

                /* Swarm encounters */
                SwarmPokemon = new ushort[4];
                for (int i = 0; i < 4; i++)
                {
                    try
                    {
                        SwarmPokemon[i] = reader.ReadUInt16();
                    }
                    catch (EndOfStreamException)
                    {
                        SwarmPokemon[i] = 0x00;
                        fieldsWithErrors.Add("Swarms" + '[' + i + ']' + MsgFixed);
                    }
                }

                if (fieldsWithErrors.Count > 0)
                {
                    ReportErrors(fieldsWithErrors);
                }
            }
        }
        public EncounterFileHGSS(int ID) : this(new FileStream(Path.Combine(RomInfo.GameDirs[DirNames.Encounters].unpackedDir, ID.ToString("D4")), FileMode.Open, FileAccess.Read)) { }
        public EncounterFileHGSS()
        {
            SwarmPokemon = new ushort[4];
        }
        #endregion

        #region Methods(1)
        public override byte[] ToByteArray()
        {
            MemoryStream newData = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(newData))
            {
                /* Encounter rates */
                writer.Write(WalkingRate);
                writer.Write(SurfRate);
                writer.Write(RockSmashRate);
                writer.Write(OldRodRate);
                writer.Write(GoodRodRate);
                writer.Write(SuperRodRate);

                writer.BaseStream.Position += 0x2;

                /* Walking encounters levels */
                for (int i = 0; i < 12; i++)
                {
                    writer.Write(WalkingLevels[i]);
                }

                /* Morning walking encounters */
                for (int i = 0; i < 12; i++)
                { 
                    writer.Write(MorningPokemon[i]);
                }

                /* Day walking encounters */
                for (int i = 0; i < 12; i++)
                {
                    writer.Write(DayPokemon[i]);
                }

                /* Night walking encounters */
                for (int i = 0; i < 12; i++)
                {
                    writer.Write(NightPokemon[i]);
                }

                /* PokéGear music encounters */
                for (int i = 0; i < 2; i++)
                {
                    writer.Write(HoennMusicPokemon[i]);
                }

                for (int i = 0; i < 2; i++)
                {
                    writer.Write(SinnohMusicPokemon[i]);
                }

                /* Surf encounters */
                for (int i = 0; i < 5; i++)
                {
                    writer.Write(SurfMinLevels[i]);
                    writer.Write(SurfMaxLevels[i]);
                    writer.Write(SurfPokemon[i]);
                }

                /* Rock Smash encounters */
                for (int i = 0; i < 2; i++)
                {
                    writer.Write(RockSmashMinLevels[i]);
                    writer.Write(RockSmashMaxLevels[i]);
                    writer.Write(RockSmashPokemon[i]);
                }

                /* Old Rod encounters */
                for (int i = 0; i < 5; i++)
                {
                    writer.Write(OldRodMinLevels[i]);
                    writer.Write(OldRodMaxLevels[i]);
                    writer.Write(OldRodPokemon[i]);
                }

                /* Good Rod encounters */
                for (int i = 0; i < 5; i++)
                {
                    writer.Write(GoodRodMinLevels[i]);
                    writer.Write(GoodRodMaxLevels[i]);
                    writer.Write(GoodRodPokemon[i]);
                }

                /* Super Rod encounters */
                for (int i = 0; i < 5; i++)
                {
                    writer.Write(SuperRodMinLevels[i]);
                    writer.Write(SuperRodMaxLevels[i]);
                    writer.Write(SuperRodPokemon[i]);
                }

                /* Swarm encounters */
                for (int i = 0; i < 4; i++)
                {
                    writer.Write(SwarmPokemon[i]);
                }
            }
            return newData.ToArray();
        }

        public void SaveToFileExplorePath(string suggestedFileName, bool showSuccessMessage = true)
        {
            SaveToFileExplorePath("HGSS Encounter File", EncounterFile.Extension, suggestedFileName, showSuccessMessage);
        }
        #endregion
    }
}

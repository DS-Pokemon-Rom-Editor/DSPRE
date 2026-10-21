﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    internal class TradeData : RomFile
    {

        public int Id;

        // TradeData structure in narc
        public int Species;
        public int HpIV;
        public int AtkIV;
        public int DefIV;
        public int SpeedIV;
        public int SpAtkIV;
        public int SpDefIV;
        public int Ability; // unused
        public int OtID;
        public int Cool;
        public int Beauty;
        public int Cute;
        public int Smart;
        public int Tough;
        public int Pid;
        public int HeldItem;
        public int OtGender;
        public int Sheen;
        public int Language;
        public int RequestedSpecies;
        public int Unknown; // unused?

        public TradeData(int id, Stream stream)
        {
            this.Id = id;
            using (BinaryReader br = new BinaryReader(stream))
            {
                Species = br.ReadInt32();
                HpIV = br.ReadInt32();
                AtkIV = br.ReadInt32();
                DefIV = br.ReadInt32();
                SpeedIV = br.ReadInt32();
                SpAtkIV = br.ReadInt32();
                SpDefIV = br.ReadInt32();
                Ability = br.ReadInt32(); // appears unused
                OtID = br.ReadInt32();
                Cool = br.ReadInt32();
                Beauty = br.ReadInt32();
                Cute = br.ReadInt32();
                Smart = br.ReadInt32();
                Tough = br.ReadInt32();
                Pid = br.ReadInt32();
                HeldItem = br.ReadInt32();
                OtGender = br.ReadInt32();
                Sheen = br.ReadInt32();
                Language = br.ReadInt32();
                RequestedSpecies = br.ReadInt32();

                if (RomInfo.GameFamily == GameFamilies.HGSS)
                {
                    Unknown = br.ReadInt32(); // unused?
                }
                    
            }
        }

        public TradeData(int id) : this(id, GetStream(id)) { }

        private static Stream GetStream(int id)
        {

            if (!File.Exists(Path.Combine(RomInfo.GameDirs[DirNames.TradeData].unpackedDir, id.ToString("D4"))))
            {
                // If the file does not exist, create it with default values
                FileStream fileStream = new FileStream(Path.Combine(RomInfo.GameDirs[DirNames.TradeData].unpackedDir, id.ToString("D4")), FileMode.Create);
                fileStream.Write(new byte[0x50], 0, 0x50); // create an empty file
                if (RomInfo.GameFamily == GameFamilies.HGSS)
                {
                    fileStream.Write(new byte[0x04], 0, 0x04); // HGSS only
                }
                fileStream.Seek(0, SeekOrigin.Begin); // Reset the position to the start of the file
                return fileStream;
            }
            
            return new FileStream(Path.Combine(RomInfo.GameDirs[DirNames.TradeData].unpackedDir, id.ToString("D4")), FileMode.Open, FileAccess.Read);
            
        }

        public override byte[] ToByteArray()
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                bw.Write(Species);
                bw.Write(HpIV);
                bw.Write(AtkIV);
                bw.Write(DefIV);
                bw.Write(SpeedIV);
                bw.Write(SpAtkIV);
                bw.Write(SpDefIV);
                bw.Write(Ability); // unused
                bw.Write(OtID);
                bw.Write(Cool);
                bw.Write(Beauty);
                bw.Write(Cute);
                bw.Write(Smart);
                bw.Write(Tough);
                bw.Write(Pid);
                bw.Write(HeldItem);
                bw.Write(OtGender);
                bw.Write(Sheen);
                bw.Write(Language);
                bw.Write(RequestedSpecies);

                if (RomInfo.GameFamily == GameFamilies.HGSS)
                {
                    bw.Write(Unknown); // HGSS only 
                }
                    
                return ms.ToArray();
            }
        }

        public void SaveToFileDefaultDir(int IDtoReplace, bool showSuccessMessage = true)
        {
            SaveToFileDefaultDir(DirNames.TradeData, IDtoReplace, showSuccessMessage);
        }
        public void SaveToFileExplorePath(string suggestedFileName, bool showSuccessMessage = true)
        {
            SaveToFileExplorePath("Gen IV Trade data", "bin", suggestedFileName, showSuccessMessage);
        }

        /// <summary>The text bank holding each trade's nickname, then each trade's OT name.</summary>
        public static int TextBank => RomInfo.TradeTextNumber;

        public static int GetTradeCount()
        {
            return NarcAPI.Narc.MemberCount(RomInfo.GameDirs[DirNames.TradeData].unpackedDir);
        }

    }
}

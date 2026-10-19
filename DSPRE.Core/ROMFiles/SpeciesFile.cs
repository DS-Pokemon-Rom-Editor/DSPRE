using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;


namespace DSPRE.ROMFiles
{
    public class SpeciesFile
    {
        public byte Ability1;
        public byte Ability2;
        public byte GenderRatioMaleToFemale;

        public const int GenderRatioByteOffset = 16;
        public const int Ability1ByteOffset = 22;

        public const int GenderRatioMale = 0;
        public const int GenderRatioFemale = 254;
        public const int GenderRatioGenderless = 255;

        public const int PichuIdNum = 172;
        public const int UnownIdNum = 201;
        public const int CastformIdNum = 351;
        public const int DeoxysIdNum = 386;
        public const int BurmyIdNum = 412;
        public const int WormadamIdNum = 413;
        public const int ShellosIdNum = 422;
        public const int GastrodonIdNum = 423;
        public const int RotomIdNum = 479;
        public const int GiratinaIdNum = 487;
        public const int ShayminIdNum = 492;

        public SpeciesFile(FileStream pokeData)
        {
            BinaryReader pokeDataReader = new BinaryReader(pokeData);
            pokeDataReader.BaseStream.Position = GenderRatioByteOffset;
            GenderRatioMaleToFemale = pokeDataReader.ReadByte();

            pokeDataReader.BaseStream.Position = Ability1ByteOffset;
            Ability1 = pokeDataReader.ReadByte();
            Ability2 = pokeDataReader.ReadByte();

            pokeDataReader.Close();
        }

    }
}


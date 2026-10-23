﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static DSPRE.RomInfo;

namespace DSPRE.ROMFiles
{
    public enum PokemonGender
    {
        Male = 0,
        Female = 254,
        Unknown = 255
    }
    public enum PokemonType
    {
        Normal = 0,
        Fighting,
        Flying,
        Poison,
        Ground,
        Rock,
        Bug,
        Ghost,
        Steel,
        Unknown,
        Fire,
        Water,
        Grass,
        Electric,
        Psychic,
        Ice,
        Dragon,
        Dark
    }
    public enum PokemonGrowthCurve
    {
        MediumFast = 0,
        Erratic,
        Fluctuating,
        MediumSlow,
        Fast,
        Slow
    }

    public enum PokemonEggGroup
    {
        Unassigned = 0,
        Monster,
        Water1,
        Bug,
        Flying,
        Field,
        Fairy,
        Grass,
        Humanoid,
        Water3,
        Mineral,
        Amorphous,
        Water2,
        Ditto,
        Dragon,
        NoBreed,
    };
    public enum PokemonDexColor
    {
        Red = 0,
        Blue, 
        Yellow,
        Green,
        Black,
        Brown,
        Purple,
        Gray,
        White,
        Pink,
        Unspecified
    }

    public class PokemonPersonalData : RomFile
    {
        public static readonly int TmsCount = 92;
        public static readonly int HmsCount = 8;

        public byte BaseHP;   
        public byte BaseAtk;
        public byte BaseDef;
        public byte BaseSpeed;
        public byte BaseSpAtk;
        public byte BaseSpDef;

        public PokemonType Type1;
        public PokemonType Type2;

        public byte CatchRate;
        public byte GivenExp;

        //Part of a u16 bitfield, 2 bits each.
        public byte EvHP;   
        public byte EvAtk;
        public byte EvDef;
        public byte EvSpeed;
        public byte EvSpAtk;
        public byte EvSpDef;

        public ushort Item1;             // First item that the mon may hold when caught
        public ushort Item2;             // Second item that the mon may hold when caught

        public byte GenderVec;        
        public byte EggSteps;      
        public byte BaseFriendship;
        public PokemonGrowthCurve GrowthCurve;

        public byte EggGroup1;    
        public byte EggGroup2;    
        public ushort FirstAbility;
        public ushort SecondAbility;

        public byte EscapeRate;
        public PokemonDexColor Color;// : 7;           // Color (used in Pokedex)
        public bool Flip;// : 1;         // Flip Flag

        public SortedSet<byte> Machines;            

        public PokemonPersonalData(Stream stream)
        {
            using (BinaryReader reader = new BinaryReader(stream))
            {
                BaseHP = reader.ReadByte();
                BaseAtk = reader.ReadByte();
                BaseDef = reader.ReadByte();
                BaseSpeed = reader.ReadByte();
                BaseSpAtk = reader.ReadByte();
                BaseSpDef = reader.ReadByte();
                Type1 = (PokemonType)reader.ReadByte();
                Type2 = (PokemonType)reader.ReadByte();
                CatchRate = reader.ReadByte();
                GivenExp = reader.ReadByte();
                ushort evData = reader.ReadUInt16();
                EvHP = (byte)(evData & 0b11);
                EvAtk = (byte)((evData >> 2) & 0b11);
                EvDef = (byte)((evData >> 4) & 0b11);
                EvSpeed = (byte)((evData >> 6) & 0b11);
                EvSpAtk = (byte)((evData >> 8) & 0b11);
                EvSpDef = (byte)((evData >> 10) & 0b11);
                Item1 = reader.ReadUInt16();
                Item2 = reader.ReadUInt16();
                GenderVec = reader.ReadByte();
                EggSteps = reader.ReadByte();
                BaseFriendship = reader.ReadByte();
                GrowthCurve = (PokemonGrowthCurve)reader.ReadByte();
                EggGroup1 = reader.ReadByte();
                EggGroup2 = reader.ReadByte();
                // hg-engine widens both abilities to u16 and moves the second past the flee rate and colour.
                FirstAbility = RomInfo.IsHGE ? reader.ReadUInt16() : reader.ReadByte();
                if (!RomInfo.IsHGE) SecondAbility = reader.ReadByte();
                EscapeRate = reader.ReadByte();

                byte colorAndFlip = reader.ReadByte();
                Color = (PokemonDexColor)(colorAndFlip & 0b01111111);
                Flip = ((colorAndFlip >> 7) & 0b00000001) == 1;

                if (RomInfo.IsHGE) SecondAbility = reader.ReadUInt16();
                else reader.BaseStream.Position += 2; //Alignment

                uint tm1 = reader.ReadUInt32();
                uint tm2 = reader.ReadUInt32();
                uint tm3 = reader.ReadUInt32();
                uint tm4 = reader.ReadUInt32();
                Machines = BitFieldToSet(new uint[4] { tm1, tm2, tm3, tm4 });
            }
        }

        public PokemonPersonalData(int ID) : this(new FileStream(Path.Combine(RomInfo.GameDirs[DirNames.PersonalPokeData].unpackedDir, ID.ToString("D4")), FileMode.Open, FileAccess.Read)) { }

        public override byte[] ToByteArray()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(BaseHP);
                    writer.Write(BaseAtk);
                    writer.Write(BaseDef);
                    writer.Write(BaseSpeed);
                    writer.Write(BaseSpAtk);
                    writer.Write(BaseSpDef);
                    writer.Write((byte)Type1);
                    writer.Write((byte)Type2);
                    writer.Write(CatchRate);
                    writer.Write(GivenExp);

                    ushort evData = (ushort)((EvHP & 0b11) |
                                             ((EvAtk & 0b11) << 2) |
                                             ((EvDef & 0b11) << 4) |
                                             ((EvSpeed & 0b11) << 6) |
                                             ((EvSpAtk & 0b11) << 8) |
                                             ((EvSpDef & 0b11) << 10));
                    writer.Write(evData);

                    writer.Write(Item1);
                    writer.Write(Item2);
                    writer.Write(GenderVec);
                    writer.Write(EggSteps);
                    writer.Write(BaseFriendship);
                    writer.Write((byte)GrowthCurve);
                    writer.Write(EggGroup1);
                    writer.Write(EggGroup2);
                    if (RomInfo.IsHGE) writer.Write(FirstAbility);
                    else { writer.Write((byte)FirstAbility); writer.Write((byte)SecondAbility); }
                    writer.Write(EscapeRate);
                    byte colorAndFlipflag = (byte)(((byte)Color & 0b01111111) |
                                                  (((Flip ? 1 : 0) & 0b00000001) << 7));
                    writer.Write(colorAndFlipflag);

                    if (RomInfo.IsHGE) writer.Write(SecondAbility);
                    else writer.BaseStream.Position += 2; //Alignment

                    uint[] bfs = SetToBitField(Machines);
                    int l = Math.Min(bfs.Length, 4);
                    int i;
                    for (i = 0; i < l; i++)
                    {
                        writer.Write(bfs[i]);
                    }
                    while (i < 4)
                    {
                        writer.Write((uint)0);
                        i++;
                    }
                }
                return stream.ToArray();
            }
        }

        public SortedSet<byte> BitFieldToSet(uint[] bitfield)
        {
            SortedSet<byte> result = new SortedSet<byte>();

            for (uint i = 0; i < bitfield.Length; i++)
            {
                uint currentBitfield = bitfield[i];

                for (int j = 0; j < 32; j++)
                {
                    if ((currentBitfield & (1 << j)) != 0)
                    {
                        result.Add((byte)(i * 32 + j));
                    }
                }
            }

            return result;
        }

        public uint[] SetToBitField(SortedSet<byte> set)
        {
            if (set == null)
            {
                return null;
            } 
            if (set.Count == 0)
            {
                return new uint[0];
            }

            int maxBit = set.Max();

            uint[] bitfield = new uint[(maxBit / 32) + 1];

            foreach (byte bit in set)
            {
                int index = bit / 32;
                int offset = bit % 32;
                bitfield[index] |= (uint)(1 << offset);
            }

            return bitfield;
        }
        public void SaveToFileDefaultDir(int IDtoReplace, bool showSuccessMessage = true)
        {
            // The Pokédex type search lists are rebuilt only when asked, in Pokédex lists.
            SaveToFileDefaultDir(DirNames.PersonalPokeData, IDtoReplace, showSuccessMessage);
        }
        public void SaveToFileExplorePath(string suggestedFileName, bool showSuccessMessage = true)
        {
            SaveToFileExplorePath("Pokémon data", "bin", suggestedFileName, showSuccessMessage);
        }

    }
}

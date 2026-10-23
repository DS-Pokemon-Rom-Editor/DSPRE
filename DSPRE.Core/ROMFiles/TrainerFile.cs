using System;
using System.Collections;
using System.IO;
using static DSPRE.ROMFiles.PartyPokemon;

namespace DSPRE.ROMFiles
{
    public class PartyPokemon : RomFile
    {
        public const int MonNumberBitsize = 10;
        public const int MonNumberBitmask = (1 << MonNumberBitsize) - 1;

        public const int MonFormBitsize = 6; //16-MON_NUMBER_BITSIZE
        public const int MonFormBitmask = ((1 << MonFormBitsize) - 1) << MonNumberBitsize;

        #region Fields
        public ushort? PokeID = null;
        // Which form the Pokemon takes.
        public ushort FormID = 0;
        public ushort Level = 0;
        public byte Difficulty = 0;
        // Gender and ability in HGSS and the AI backport; elsewhere the high byte of the u16 difficulty, kept as read.
        public GenderAndAbilityFlags GenderAndAbility;
        public ushort BallSeals = 0;

        public ushort? HeldItem = null;
        public ushort[] Moves = null;

        public enum GenderAndAbilityFlags
        {
            NO_FLAGS = 0,
            FORCE_MALE = 0x1,
            FORCE_FEMALE = 0x2,
            ABILITY_SLOT1 = 0x10,
            ABILITY_SLOT2 = 0x20,
            // Read only by the external trainer shiny patch (TrainerShinyPatch); US HeartGold's own trainers leave it clear.
            FORCE_SHINY = 0x40
        }

        public bool ForceShiny
        {
            get => GenderAndAbility.HasFlag(GenderAndAbilityFlags.FORCE_SHINY);
            set => GenderAndAbility = value
                ? GenderAndAbility | GenderAndAbilityFlags.FORCE_SHINY
                : GenderAndAbility & ~GenderAndAbilityFlags.FORCE_SHINY;
        }
        #endregion

        #region Constructor
        public PartyPokemon(bool chooseItems = false, bool chooseMoves = false)
        {
            UpdateItemsAndMoves(chooseItems, chooseMoves);
        }

        public PartyPokemon(byte difficulty, GenderAndAbilityFlags genderAndAbilityFlags, ushort Level, ushort pokeNum, ushort ballSealConfig, ushort? heldItem = null, ushort[] moves = null)
        {
            PokeID = pokeNum;
            this.Level = Level;
            this.Difficulty = difficulty;
            this.GenderAndAbility = genderAndAbilityFlags;
            BallSeals = ballSealConfig;
            this.HeldItem = heldItem;
            this.Moves = moves;
        }

        public PartyPokemon(byte difficulty, GenderAndAbilityFlags genderAndAbilityFlags, ushort Level, ushort pokeNum, ushort formNum, ushort ballSealConfig, ushort? heldItem = null, ushort[] moves = null) :
            this(difficulty, genderAndAbilityFlags, Level, pokeNum, ballSealConfig, heldItem, moves)
        {

            FormID = formNum;
        }
        public override byte[] ToByteArray()
        {
            MemoryStream newData = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(newData))
            {
                writer.Write(Difficulty);
                writer.Write((byte)GenderAndAbility);
                writer.Write(Level);
                writer.Write((ushort)((PokeID ?? 0) | FormID << MonNumberBitsize));

                if (HeldItem != null)
                {
                    writer.Write((ushort)HeldItem);
                }

                if (Moves != null)
                {
                    foreach (ushort move in Moves)
                    {
                        writer.Write(move);
                    }
                }
                if (RomInfo.GameFamily == RomInfo.GameFamilies.HGSS || RomInfo.GameFamily == RomInfo.GameFamilies.Plat)
                    writer.Write(BallSeals); // Diamond and Pearl party records have no ball capsule field
            }
            return newData.ToArray();
        }
        public void UpdateItemsAndMoves(bool chooseItems = false, bool chooseMoves = false)
        {
            if (chooseItems)
            {
                this.HeldItem = 0;
            }
            if (chooseMoves)
            {
                this.Moves = new ushort[4];
            }
        }

        public override string ToString()
        {
            return CheckEmpty() ? "Empty" : this.PokeID + " Lv. " + this.Level;
        }
        public bool CheckEmpty()
        {
            return this is null || PokeID is null || Level <= 0;
        }
        #endregion
    }

    public class TrainerProperties : RomFile
    {
        public const int AiCount = 11;
        public const int TrainerItemCount = 4;
        // Youngster, the same id in all three games.
        public const byte NewTrainerClass = 2;

        #region Fields
        public ushort TrainerID;
        public byte TrDataUnknown;

        public byte TrainerClass = 0;
        public byte PartyCount = 0;

        // The game ORs the whole value into the battle type, so bits other than 2 are kept as read.
        public uint BattleType;
        public bool DoubleBattle
        {
            get => (BattleType & 2) != 0;
            set => BattleType = value ? BattleType | 2u : BattleType & ~2u;
        }
        public bool ChooseMoves = false;
        public bool ChooseItems = false;

        public ushort[] TrainerItems = new ushort[TrainerItemCount];
        public BitArray AI;
        #endregion

        #region Constructor
        public TrainerProperties(ushort ID, byte partyCount = 0)
        {
            TrainerID = ID;
            this.PartyCount = partyCount;
            TrainerItems = new ushort[TrainerItemCount];
            AI = new BitArray(new bool[AiCount] { true, false, false, false, false, false, false, false, false, false, false });
            TrDataUnknown = 0;
        }
        public TrainerProperties(ushort ID, Stream trainerPropertiesStream)
        {
            TrainerID = ID;
            using (BinaryReader reader = new BinaryReader(trainerPropertiesStream))
            {
                byte flags = reader.ReadByte();
                ChooseMoves = (flags & 1) != 0;
                ChooseItems = (flags & 2) != 0;

                TrainerClass = reader.ReadByte();
                TrDataUnknown = reader.ReadByte();
                PartyCount = reader.ReadByte();

                for (int i = 0; i < TrainerItems.Length; i++)
                {
                    TrainerItems[i] = reader.ReadUInt16();
                }

                AI = new BitArray(BitConverter.GetBytes(reader.ReadUInt32()));
                BattleType = reader.ReadUInt32();
            }
        }
        #endregion

        #region Methods
        public override byte[] ToByteArray()
        {
            MemoryStream newData = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(newData))
            {
                byte flags = 0;
                flags |= (byte)(ChooseMoves ? 1 : 0);
                flags |= (byte)(ChooseItems ? 2 : 0);

                writer.Write(flags);
                writer.Write(TrainerClass);
                writer.Write(TrDataUnknown);
                writer.Write(PartyCount);

                foreach (ushort trItem in TrainerItems)
                {
                    writer.Write(trItem);
                }

                uint AIflags = 0;
                for (int i = 0; i < AI.Length; i++)
                {
                    if (AI[i])
                    {
                        AIflags |= (uint)1 << i;
                    }
                }

                writer.Write(AIflags);
                writer.Write(BattleType);
            }
            return newData.ToArray();
        }

        public void SaveToFileExplorePath(string suggestedFileName, bool showSuccessMessage = true)
        {
            SaveToFileExplorePath("Trainer properties", "trp", suggestedFileName, showSuccessMessage);
        }
        #endregion

    }

    public class Party : RomFile
    {
        private PartyPokemon[] _content;
        private TrainerProperties _trp;
        public bool ExportCondensedData;

        public const int MovesPerPoke = 4;
        public Party(int POKE_IN_PARTY, bool init, TrainerProperties trp)
        {
            this._trp = trp;
            this._content = new PartyPokemon[POKE_IN_PARTY];

            if (init)
            {
                for (int i = 0; i < _content.Length; i++)
                {
                    this._content[i] = new PartyPokemon();
                }
            }
        }

        public Party(bool readFirstByte, int maxPoke, Stream partyData, TrainerProperties traipr)
        {
            this._trp = traipr;
            this._content = new PartyPokemon[maxPoke];
            using (BinaryReader reader = new BinaryReader(partyData))
            {
                try
                {
                    if (readFirstByte)
                    {
                        byte flags = reader.ReadByte();

                        _trp.ChooseMoves = (flags & 1) != 0;
                        _trp.ChooseItems = (flags & 2) != 0;
                        _trp.PartyCount = (byte)((flags & 28) >> 2);
                    }

                    bool hasBallSeals = RomInfo.GameFamily == RomInfo.GameFamilies.HGSS || RomInfo.GameFamily == RomInfo.GameFamilies.Plat;
                    int recordSize = RecordSize();

                    long recordCount = (partyData.Length - partyData.Position) / recordSize;
                    int endval = (int)Math.Min(Math.Min(recordCount, _trp.PartyCount), maxPoke);
                    for (int i = 0; i < endval; i++)
                    {
                        byte difficulty = reader.ReadByte();
                        GenderAndAbilityFlags genderAndAbilityFlags = (GenderAndAbilityFlags)reader.ReadByte();
                        ushort level = reader.ReadUInt16();

                        ushort monFull = reader.ReadUInt16();
                        ushort pokemon = (ushort)(monFull & PartyPokemon.MonNumberBitmask);
                        ushort form_no = (ushort)((monFull & PartyPokemon.MonFormBitmask) >> PartyPokemon.MonNumberBitsize);

                        ushort? heldItem = null;
                        ushort[] moves = null;

                        if (_trp.ChooseItems)
                        {
                            heldItem = reader.ReadUInt16();
                        }
                        if (_trp.ChooseMoves)
                        {
                            moves = new ushort[MovesPerPoke];
                            for (int m = 0; m < moves.Length; m++)
                            {
                                ushort val = reader.ReadUInt16();
                                moves[m] = (ushort)(val == ushort.MaxValue ? 0 : val);
                            }
                        }


                        // Diamond and Pearl records have no ball seal field.
                        ushort ballSeals = hasBallSeals ? reader.ReadUInt16() : (ushort)0;
                        _content[i] = new PartyPokemon(difficulty, genderAndAbilityFlags, level, pokemon, form_no, ballSeals, heldItem, moves);
                    }
                }
                catch (EndOfStreamException)
                {
                    AppMessages.Error("This trainer's party couldn't be read.", "Party not read");
                }
            }
            for (int i = 0; i < _content.Length; i++)
            {
                _content[i] ??= new PartyPokemon(_trp.ChooseItems, _trp.ChooseMoves);
            }
        }

        public PartyPokemon this[int index]
        {
            get
            {
                return _content[index];
            }
            set
            {
                _content[index] = value;
            }
        }
        public override string ToString()
        {
            if (this._content == null)
            {
                return "Empty";
            }
            else
            {
                string buffer = "";
                byte nonEmptyCtr = CountNonEmptyMons();
                buffer += nonEmptyCtr + " Poke ";
                if (this._trp.ChooseMoves)
                {
                    buffer += ", moves ";
                }
                if (this._trp.ChooseItems)
                {
                    buffer += ", items ";
                }
                return buffer;
            }
        }

        public byte CountNonEmptyMons()
        {
            byte nonEmptyCtr = 0;
            foreach (PartyPokemon p in this._content)
            {
                if (!p.CheckEmpty())
                {
                    nonEmptyCtr++;
                }
            }

            return nonEmptyCtr;
        }

        // The game reads exactly partyCount records, so slots past it are not written.
        public override byte[] ToByteArray() => ToByteArray(_trp?.PartyCount);

        /// <summary>Writes the first recordCount slots, empty ones included; null writes every non-empty slot.</summary>
        public byte[] ToByteArray(int? recordCount)
        {
            MemoryStream newData = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(newData))
            {
                if (this.ExportCondensedData && _trp != null)
                {
                    byte condensedTrData = (byte)(((_trp.ChooseMoves ? 1 : 0) & 0b_1) + (((_trp.ChooseItems ? 1 : 0) & 0b_1) << 1) + ((_trp.PartyCount & 0b_1111_11) << 2));
                    writer.Write(condensedTrData);
                }

                if (recordCount is int count)
                {
                    for (int i = 0; i < count && i < this._content.Length; i++)
                    {
                        PartyPokemon poke = this._content[i] ?? new PartyPokemon(_trp?.ChooseItems ?? false, _trp?.ChooseMoves ?? false);
                        writer.Write(poke.ToByteArray());
                    }
                    // A trainer with no party still has one zeroed record in the game's own data.
                    if (count == 0 && !this.ExportCondensedData)
                    {
                        writer.Write(new byte[RecordSize()]);
                    }
                }
                else
                {
                    foreach (PartyPokemon poke in this._content)
                    {
                        if (!poke.CheckEmpty())
                        {
                            writer.Write(poke.ToByteArray());
                        }
                    }
                }
                // The game's party files end on a 4-byte boundary; keeping it leaves unedited parties byte for byte.
                if (!this.ExportCondensedData)
                {
                    while (newData.Length % 4 != 0) writer.Write((byte)0);
                }
            }
            return newData.ToArray();
        }
        private int RecordSize()
        {
            bool hasBallSeals = RomInfo.GameFamily == RomInfo.GameFamilies.HGSS || RomInfo.GameFamily == RomInfo.GameFamilies.Plat;
            return 6 + (hasBallSeals ? sizeof(ushort) : 0)
                + (_trp != null && _trp.ChooseMoves ? MovesPerPoke * sizeof(ushort) : 0)
                + (_trp != null && _trp.ChooseItems ? sizeof(ushort) : 0);
        }

        public void SaveToFileExplorePath(string suggestedFileName, bool showSuccessMessage = true)
        {
            SaveToFileExplorePath("Party data", "pdat", suggestedFileName, showSuccessMessage);
        }
    }

    public static class TrainerRecords
    {
        /// <summary>Writes a trainer's properties and party as record <paramref name="id"/> of the unpacked archives.</summary>
        public static void Save(int id, TrainerProperties trp, Party party)
        {
            SaveProperties(id, trp);
            ArchiveMembers.Put(RomInfo.DirNames.TrainerParty, id, party.ToByteArray());
        }

        /// <summary>Writes only a trainer's properties record, for edits that leave the party alone.</summary>
        public static void SaveProperties(int id, TrainerProperties trp) =>
            ArchiveMembers.Put(RomInfo.DirNames.TrainerProperties, id, trp.ToByteArray());

        /// <summary>Reads record <paramref name="id"/> of the unpacked archives, the way the editor opens a trainer.</summary>
        public static TrainerFile Load(int id, string name = "")
        {
            string file = id.ToString("D4");
            using FileStream prop = File.OpenRead(Path.Combine(RomInfo.GameDirs[RomInfo.DirNames.TrainerProperties].unpackedDir, file));
            using FileStream party = File.OpenRead(Path.Combine(RomInfo.GameDirs[RomInfo.DirNames.TrainerParty].unpackedDir, file));
            return new TrainerFile(new TrainerProperties((ushort)id, prop), party, name);
        }
    }
    public class TrainerFile : RomFile
    {
        public const int DefaultNameLen = 7; // battle copies the name into an 8-character buffer with its end mark

        /// <summary>How many letters of a compressed trainer name fit in a buffer this many units long.</summary>
        public static int CompressedNameLen(int bufferUnits) => Math.Max(0, (bufferUnits - 2) * 15 / 9);
        public const int PokeInParty = 6;
        public static readonly string NAME_NOT_FOUND = "(name not found)";

        #region Fields
        public string Name;
        public TrainerProperties Trp;
        public Party Party;
        #endregion

        #region Constructor
        public TrainerFile(TrainerProperties trp, string name = "")
        {
            this.Name = name;
            this.Trp = trp;
            trp.PartyCount = 1;
            this.Party = new Party(1, init: true, trp);
        }
        public TrainerFile(TrainerProperties trp, Stream partyData, string name = "")
        {
            this.Name = name;
            this.Trp = trp;
            Party = new Party(readFirstByte: false, PokeInParty, partyData, this.Trp);
        }
        #endregion

        #region Methods
        public override byte[] ToByteArray()
        {
            MemoryStream newData = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(newData))
            {
                writer.Write(Name);

                byte[] trDat = Trp.ToByteArray();
                writer.Write((byte)trDat.Length);
                writer.Write(trDat);

                byte[] pDat = Party.ToByteArray();
                writer.Write((byte)pDat.Length);
                writer.Write(pDat);
            }
            return newData.ToArray();
        }

        public void SaveToFileExplorePath(string suggestedFileName, bool showSuccessMessage = true)
        {
            SaveToFileExplorePath("Trainer file", "trf", suggestedFileName, showSuccessMessage);
        }
        #endregion

    }

}

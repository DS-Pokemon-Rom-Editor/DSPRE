using System;
using System.Collections;
using System.IO;
using static DSPRE.ROMFiles.PartyPokemon;

namespace DSPRE.ROMFiles {
    public class PartyPokemon : RomFile {
        public const int MON_NUMBER_BITSIZE = 10;
        public const int MON_NUMBER_BITMASK = (1 << MON_NUMBER_BITSIZE) - 1;

        public const int MON_FORM_BITSIZE = 6; //16-MON_NUMBER_BITSIZE
        public const int MON_FORM_BITMASK = ((1 << MON_FORM_BITSIZE) - 1) << MON_NUMBER_BITSIZE;

        #region Fields
        public ushort? pokeID = null;
        // Which form the Pokemon takes.
        public ushort formID = 0;
        public ushort level = 0;
        public byte difficulty = 0;
        // Gender and ability in HGSS and the AI backport; elsewhere the high byte of the u16 difficulty, kept as read.
        public GenderAndAbilityFlags genderAndAbilityFlags;
        public ushort ballSeals = 0;

        public ushort? heldItem = null;
        public ushort[] moves = null;

        public enum GenderAndAbilityFlags {
            NO_FLAGS = 0,
            FORCE_MALE = 0x1,
            FORCE_FEMALE = 0x2,
            ABILITY_SLOT1 = 0x10,
            ABILITY_SLOT2 = 0x20,
            // Read only by the external trainer shiny patch (TrainerShinyPatch); US HeartGold's own trainers leave it clear.
            FORCE_SHINY = 0x40
        }

        public bool ForceShiny {
            get => genderAndAbilityFlags.HasFlag(GenderAndAbilityFlags.FORCE_SHINY);
            set => genderAndAbilityFlags = value
                ? genderAndAbilityFlags | GenderAndAbilityFlags.FORCE_SHINY
                : genderAndAbilityFlags & ~GenderAndAbilityFlags.FORCE_SHINY;
        }
        #endregion

        #region Constructor
        public PartyPokemon(bool chooseItems = false, bool chooseMoves = false) {
            UpdateItemsAndMoves(chooseItems, chooseMoves);
        }

        public PartyPokemon(byte difficulty, GenderAndAbilityFlags genderAndAbilityFlags, ushort Level, ushort pokeNum, ushort ballSealConfig, ushort? heldItem = null, ushort[] moves = null) {
            pokeID = pokeNum;
            level = Level;
            this.difficulty = difficulty;
            this.genderAndAbilityFlags = genderAndAbilityFlags;
            ballSeals = ballSealConfig;
            this.heldItem = heldItem;
            this.moves = moves;
        }

        public PartyPokemon(byte difficulty, GenderAndAbilityFlags genderAndAbilityFlags, ushort Level, ushort pokeNum, ushort formNum, ushort ballSealConfig, ushort? heldItem = null, ushort[] moves = null) :
            this(difficulty, genderAndAbilityFlags, Level, pokeNum, ballSealConfig, heldItem, moves) {

            formID = formNum;
        }
        public override byte[] ToByteArray() {
            MemoryStream newData = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(newData)) {
                writer.Write(difficulty);
                writer.Write((byte)genderAndAbilityFlags);
                writer.Write(level);
                writer.Write((ushort)((pokeID ?? 0) | formID << MON_NUMBER_BITSIZE));

                if (heldItem != null) {
                    writer.Write((ushort)heldItem);
                }

                if (moves != null) {
                    foreach (ushort move in moves) {
                        writer.Write(move);
                    }
                }
                if (RomInfo.gameFamily == RomInfo.GameFamilies.HGSS || RomInfo.gameFamily == RomInfo.GameFamilies.Plat)
                    writer.Write(ballSeals); // Diamond and Pearl apparently dont save ball capsule data in enemy trainer pokedata!!!
            }
            return newData.ToArray();
        }
        public void UpdateItemsAndMoves(bool chooseItems = false, bool chooseMoves = false) {
            if (chooseItems) {
                this.heldItem = 0;
            }
            if (chooseMoves) {
                this.moves = new ushort[4];
            }
        }

        public override string ToString() {
            return CheckEmpty() ? "Empty" : this.pokeID + " Lv. " + this.level;
        }
        public bool CheckEmpty() {
            return this is null || pokeID is null || level <= 0;
        }
        #endregion
    }

    public class TrainerProperties : RomFile {
        public const int AI_COUNT = 11;
        public const int TRAINER_ITEMS = 4;
        // Youngster, the same id in all three games.
        public const byte NewTrainerClass = 2;

        #region Fields
        public ushort trainerID;
        public byte trDataUnknown;

        public byte trainerClass = 0;
        public byte partyCount = 0;

        // The game ORs the whole value into the battle type, so bits other than 2 are kept as read.
        public uint battleType;
        public bool doubleBattle {
            get => (battleType & 2) != 0;
            set => battleType = value ? battleType | 2u : battleType & ~2u;
        }
        public bool chooseMoves = false;
        public bool chooseItems = false;

        public ushort[] trainerItems = new ushort[TRAINER_ITEMS];
        public BitArray AI;
        #endregion

        #region Constructor
        public TrainerProperties(ushort ID, byte partyCount = 0) {
            trainerID = ID;
            this.partyCount = partyCount;
            trainerItems = new ushort[TRAINER_ITEMS];
            AI = new BitArray(new bool[AI_COUNT] { true, false, false, false, false, false, false, false, false, false, false });
            trDataUnknown = 0;
        }
        public TrainerProperties(ushort ID, Stream trainerPropertiesStream) {
            trainerID = ID;
            using (BinaryReader reader = new BinaryReader(trainerPropertiesStream)) {
                byte flags = reader.ReadByte();
                chooseMoves = (flags & 1) != 0;
                chooseItems = (flags & 2) != 0;

                trainerClass = reader.ReadByte();
                trDataUnknown = reader.ReadByte();
                partyCount = reader.ReadByte();

                for (int i = 0; i < trainerItems.Length; i++) {
                    trainerItems[i] = reader.ReadUInt16();
                }

                AI = new BitArray(BitConverter.GetBytes(reader.ReadUInt32()));
                battleType = reader.ReadUInt32();
            }
        }
        #endregion

        #region Methods
        public override byte[] ToByteArray() {
            MemoryStream newData = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(newData)) {
                byte flags = 0;
                flags |= (byte)(chooseMoves ? 1 : 0);
                flags |= (byte)(chooseItems ? 2 : 0);

                writer.Write(flags);
                writer.Write(trainerClass);
                writer.Write(trDataUnknown);
                writer.Write(partyCount);

                foreach (ushort trItem in trainerItems) {
                    writer.Write(trItem);
                }

                uint AIflags = 0;
                for (int i = 0; i < AI.Length; i++) {
                    if (AI[i]) {
                        AIflags |= (uint)1 << i;
                    }
                }

                writer.Write(AIflags);
                writer.Write(battleType);
            }
            return newData.ToArray();
        }

        public void SaveToFileExplorePath(string suggestedFileName, bool showSuccessMessage = true) {
            SaveToFileExplorePath("Gen IV Trainer Properties", "trp", suggestedFileName, showSuccessMessage);
        }
        #endregion

    }

    public class Party : RomFile {
        private PartyPokemon[] content;
        private TrainerProperties trp;
        public bool exportCondensedData;

        public const int MOVES_PER_POKE = 4;
        public Party(int POKE_IN_PARTY, bool init, TrainerProperties trp) {
            this.trp = trp;
            this.content = new PartyPokemon[POKE_IN_PARTY];

            if (init) {
                for (int i = 0; i < content.Length; i++) {
                    this.content[i] = new PartyPokemon();
                }
            }
        }

        public Party(bool readFirstByte, int maxPoke, Stream partyData, TrainerProperties traipr) {
            this.trp = traipr;
            this.content = new PartyPokemon[maxPoke];
            using (BinaryReader reader = new BinaryReader(partyData)) {
                try {
                    if (readFirstByte) {
                        byte flags = reader.ReadByte();

                        trp.chooseMoves = (flags & 1) != 0;
                        trp.chooseItems = (flags & 2) != 0;
                        trp.partyCount = (byte)((flags & 28) >> 2);
                    }

                    bool hasBallSeals = RomInfo.gameFamily == RomInfo.GameFamilies.HGSS || RomInfo.gameFamily == RomInfo.GameFamilies.Plat;
                    int recordSize = RecordSize();

                    long recordCount = (partyData.Length - partyData.Position) / recordSize;
                    int endval = (int)Math.Min(Math.Min(recordCount, trp.partyCount), maxPoke);
                    for (int i = 0; i < endval; i++) {
                        byte difficulty = reader.ReadByte();
                        GenderAndAbilityFlags genderAndAbilityFlags = (GenderAndAbilityFlags)reader.ReadByte();
                        ushort level = reader.ReadUInt16();

                        ushort monFull = reader.ReadUInt16();
                        ushort pokemon = (ushort)(monFull & PartyPokemon.MON_NUMBER_BITMASK);
                        ushort form_no = (ushort)((monFull & PartyPokemon.MON_FORM_BITMASK) >> PartyPokemon.MON_NUMBER_BITSIZE);

                        ushort? heldItem = null;
                        ushort[] moves = null;

                        if (trp.chooseItems) {
                            heldItem = reader.ReadUInt16();
                        }
                        if (trp.chooseMoves) {
                            moves = new ushort[MOVES_PER_POKE];
                            for (int m = 0; m < moves.Length; m++) {
                                ushort val = reader.ReadUInt16();
                                moves[m] = (ushort)(val == ushort.MaxValue ? 0 : val);
                            }
                        }


                        // Diamond and Pearl records have no ball seal field.
                        ushort ballSeals = hasBallSeals ? reader.ReadUInt16() : (ushort)0;
                        content[i] = new PartyPokemon(difficulty, genderAndAbilityFlags, level, pokemon, form_no, ballSeals, heldItem, moves);
                    }
                } catch (EndOfStreamException) {
                    AppMessages.Error("There was a problem reading the party data of this " + this.GetType().Name + ".", "Read Error");
                }
            }
            for (int i = 0; i < content.Length; i++) {
                content[i] ??= new PartyPokemon(trp.chooseItems, trp.chooseMoves);
            }
        }

        public PartyPokemon this[int index] {
            get {
                return content[index];
            }
            set {
                content[index] = value;
            }
        }
        public override string ToString() {
            if (this.content == null) {
                return "Empty";
            } else {
                string buffer = "";
                byte nonEmptyCtr = CountNonEmptyMons();
                buffer += nonEmptyCtr + " Poke ";
                if (this.trp.chooseMoves) {
                    buffer += ", moves ";
                }
                if (this.trp.chooseItems) {
                    buffer += ", items ";
                }
                return buffer;
            }
        }

        public byte CountNonEmptyMons() {
            byte nonEmptyCtr = 0;
            foreach (PartyPokemon p in this.content) {
                if (!p.CheckEmpty()) {
                    nonEmptyCtr++;
                }
            }

            return nonEmptyCtr;
        }

        // The game reads exactly partyCount records, so slots past it are not written.
        public override byte[] ToByteArray() => ToByteArray(trp?.partyCount);

        /// <summary>Writes the first recordCount slots, empty ones included; null writes every non-empty slot.</summary>
        public byte[] ToByteArray(int? recordCount) {
            MemoryStream newData = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(newData)) {
                if (this.exportCondensedData && trp != null) {
                    byte condensedTrData = (byte)(((trp.chooseMoves ? 1 : 0) & 0b_1) + (((trp.chooseItems ? 1 : 0) & 0b_1) << 1) + ((trp.partyCount & 0b_1111_11) << 2));
                    writer.Write(condensedTrData);
                }

                if (recordCount is int count) {
                    for (int i = 0; i < count && i < this.content.Length; i++) {
                        PartyPokemon poke = this.content[i] ?? new PartyPokemon(trp?.chooseItems ?? false, trp?.chooseMoves ?? false);
                        writer.Write(poke.ToByteArray());
                    }
                    // A trainer with no party still has one zeroed record in the game's own data.
                    if (count == 0 && !this.exportCondensedData) {
                        writer.Write(new byte[RecordSize()]);
                    }
                } else {
                    foreach (PartyPokemon poke in this.content) {
                        if (!poke.CheckEmpty()) {
                            writer.Write(poke.ToByteArray());
                        }
                    }
                }
                // The game's party files end on a 4-byte boundary; keeping it leaves unedited parties byte for byte.
                if (!this.exportCondensedData) {
                    while (newData.Length % 4 != 0) writer.Write((byte)0);
                }
            }
            return newData.ToArray();
        }
        private int RecordSize() {
            bool hasBallSeals = RomInfo.gameFamily == RomInfo.GameFamilies.HGSS || RomInfo.gameFamily == RomInfo.GameFamilies.Plat;
            return 6 + (hasBallSeals ? sizeof(ushort) : 0)
                + (trp != null && trp.chooseMoves ? MOVES_PER_POKE * sizeof(ushort) : 0)
                + (trp != null && trp.chooseItems ? sizeof(ushort) : 0);
        }

        public void SaveToFileExplorePath(string suggestedFileName, bool showSuccessMessage = true) {
            SaveToFileExplorePath("Gen IV Party Data", "pdat", suggestedFileName, showSuccessMessage);
        }
    }
    public class TrainerFile : RomFile {
        public const int defaultNameLen = 7; // battle copies the name into an 8-character buffer with its end mark
        public const int POKE_IN_PARTY = 6;
        public static readonly string NAME_NOT_FOUND = "NAME READ ERROR";

        #region Fields
        public string name;
        public TrainerProperties trp;
        public Party party;
        #endregion

        #region Constructor
        public TrainerFile(TrainerProperties trp, string name = "") {
            this.name = name;
            this.trp = trp;
            trp.partyCount = 1;
            this.party = new Party(1, init: true, trp);
        }
        public TrainerFile(TrainerProperties trp, Stream partyData, string name = "") {
            this.name = name;
            this.trp = trp;
            party = new Party(readFirstByte: false, POKE_IN_PARTY, partyData, this.trp);
        }
        #endregion

        #region Methods
        public override byte[] ToByteArray() {
            MemoryStream newData = new MemoryStream();
            using (BinaryWriter writer = new BinaryWriter(newData)) {
                writer.Write(name);

                byte[] trDat = trp.ToByteArray();
                writer.Write((byte)trDat.Length);
                writer.Write(trDat);

                byte[] pDat = party.ToByteArray();
                writer.Write((byte)pDat.Length);
                writer.Write(pDat);
            }
            return newData.ToArray();
        }

        public void SaveToFileExplorePath(string suggestedFileName, bool showSuccessMessage = true) {
            SaveToFileExplorePath("Gen IV Trainer File", "trf", suggestedFileName, showSuccessMessage);
        }
        #endregion

    }

}

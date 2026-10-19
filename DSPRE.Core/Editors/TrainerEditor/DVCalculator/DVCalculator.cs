using System;
using System.Collections.Generic;
using System.IO;
using DSPRE.ROMFiles;
using static DSPRE.RomInfo;

namespace DSPRE
{
    // Putting this class here is bad practice, however it's just a really small utility class
    public class DVIVNatureTriplet
    {
        public int DV { get; set; }
        public int IV { get; set; }
        public string Nature { get; set; }

        public DVIVNatureTriplet(int DV, int IV, string Nature)
        {
            this.DV = DV;
            this.IV = IV;
            this.Nature = Nature;
        }

    }

    public static class DVCalculator
    {
        private static uint _state;
        private static uint _genderMod;
        public static void SetSeed(uint seed)
        {

            DVCalculator._state = seed;

        }

        // this code is partially lifted from turtleisaac's Pokeditor
        // See https://github.com/turtleisaac/PokEditor-v2/blob/72ca6ab641f616b8be9a87624b81896baa45f947/src/com/turtleisaac/pokeditor/utilities/TrainerPersonalityCalculator.java
        // and the pokeplatinum / pokeheartgold projects See: https://github.com/pret
        public static uint GetNextRandom()
        {
            _state = 1103515245 * _state + 24691;

            // Upper 16 bit are random value
            return _state >> 16;
        }

        public static void ResetGenderMod(bool maleOrMulti)
        {            
             _genderMod = maleOrMulti ? 136u : 120u;   
        }

        public static void FilterHighestDV(ref List<DVIVNatureTriplet> natures)
        {
            Dictionary<string, DVIVNatureTriplet> result = new Dictionary<string, DVIVNatureTriplet>();
            foreach (DVIVNatureTriplet triplet in natures)
            {
                if (!result.ContainsKey(triplet.Nature) || triplet.DV > result[triplet.Nature].DV)
                {
                    result[triplet.Nature] = triplet;
                }
            }
            natures = new List<DVIVNatureTriplet>(result.Values);
        }

        public static uint GeneratePID(uint trainerIdx, uint trainerClassIdx, uint pokeIdx, byte pokeLevel, byte baseGenderRatio, int genderOverride, int abilityOverride, byte difficultyValue)
        {
            uint newSeed = (uint)(trainerIdx + pokeIdx + pokeLevel + difficultyValue);

            SetSeed(newSeed);

            // A class with no rolls shifts the seed itself into the personality.
            uint random = newSeed;

            while (trainerClassIdx > 0)
            {
                trainerClassIdx--;
                random = GetNextRandom();
            }
            if (RomInfo.GameFamily == GameFamilies.HGSS || RomInfo.AIBackportEnabled)
                UpdateGenderMod((ushort)pokeIdx, baseGenderRatio, genderOverride, abilityOverride);

            uint PID = (random << 8) + _genderMod;

            return (uint)PID;
        }

        public static int GetNatureFromPID(uint PID)
        {
            return (int) (PID % 100) % 25;
        }

        public static void UpdateGenderMod(ushort pokeId, byte baseGenderRatio, int genderOverride, int abilityOverride)
        {
            if (genderOverride > 0 || abilityOverride > 0)
            {
                if (RomInfo.OutdatedAIBackportEnabled)
                    _genderMod = pokeId;

                // Code from here on is HGSS exclusive
                if (genderOverride == 1)
                {
                    _genderMod = baseGenderRatio + 2u;
                }

                else if (genderOverride == 2)
                {
                    _genderMod = baseGenderRatio - 2u;
                }

                // Force Ability 1 --> Force lowest bit to 0
                if (abilityOverride == 1)
                {
                    _genderMod = (uint)(_genderMod & ~1);
                }

                // Force Ability 2 --> Force lowest bit to 1
                else if (abilityOverride == 2)
                {
                    _genderMod = (uint)(_genderMod | 1);
                }
            }
        }

        public static List<DVIVNatureTriplet> GetAllNatures(uint trainerIdx, uint trainerClassIdx, uint pokeIdx, byte pokeLevel, byte baseGenderRatio, int genderOverride, int abilityOverride)
        {
            List<DVIVNatureTriplet> natureDict = new List<DVIVNatureTriplet>();

            int DV;
            int natureIdx;
            uint genderModLocal = _genderMod;

            for (DV = 255; DV >= 0; DV--)
            {
                natureIdx = GetNatureFromPID(GeneratePID(trainerIdx, trainerClassIdx, pokeIdx, pokeLevel, baseGenderRatio, genderOverride, abilityOverride, (byte) DV));

                _genderMod = genderModLocal;

                natureDict.Add(new DVIVNatureTriplet(DV, DV * 31 / 255, Natures[natureIdx]));

            }

            return natureDict;
        }

        /// <summary>One party member as the personality roll reads it; Gender and Ability are the override flags.</summary>
        public readonly record struct PartyMon(int Species, int Level, int Gender, int Ability, int Dv);

        /// <summary>Each party member's personality in party order; on HGSS a forced gender or ability carries to the next.</summary>
        public static uint[] PartyPids(uint trainer, uint trainerClass, bool maleTrainer, IReadOnlyList<PartyMon> party)
        {
            ResetGenderMod(maleTrainer);
            uint[] pids = new uint[party.Count];
            for (int i = 0; i < party.Count; i++)
            {
                PartyMon p = party[i];
                pids[i] = GeneratePID(trainer, trainerClass, (uint)p.Species, (byte)p.Level, new PokemonPersonalData(p.Species).GenderVec, p.Gender, p.Ability, (byte)p.Dv);
            }
            return pids;
        }

        /// <summary>Every DV of one party member with the IV and nature it gives, optionally only the highest DV per nature.</summary>
        public static List<DVIVNatureTriplet> SlotOptions(uint trainer, uint trainerClass, bool maleTrainer, IReadOnlyList<PartyMon> party, int index, bool highestOnly)
        {
            ResetGenderMod(maleTrainer);
            if (RomInfo.GameFamily == GameFamilies.HGSS || RomInfo.AIBackportEnabled)
                for (int i = 0; i < index && i < party.Count; i++)
                    UpdateGenderMod((ushort)party[i].Species, new PokemonPersonalData(party[i].Species).GenderVec, party[i].Gender, party[i].Ability);
            PartyMon p = party[index];
            List<DVIVNatureTriplet> triplets = GetAllNatures(trainer, trainerClass, (uint)p.Species, (byte)p.Level,
                new PokemonPersonalData(p.Species).GenderVec, p.Gender, p.Ability);
            if (highestOnly) FilterHighestDV(ref triplets);
            return triplets;
        }

        public static readonly List<string> Natures = new List<string>
            {
            "Hardy: Neutral",
            "Lonely: +Atk, -Def",
            "Brave: +Atk, -Spe",
            "Adamant: +Atk, -SpA",
            "Naughty: +Atk, -SpD",
            "Bold: +Def, -Atk",
            "Docile: Neutral",
            "Relaxed: +Def, -Spe",
            "Impish: +Def, -SpA",
            "Lax: +Def, -SpD",
            "Timid: +Spe, -Atk",
            "Hasty: +Spe, -Def",
            "Serious: Neutral",
            "Jolly: +Spe, -SpA",
            "Naive: +Spe, -SpD",
            "Modest: +SpA, -Atk",
            "Mild: +SpA, -Def",
            "Quiet: +SpA, -Spe",
            "Bashful: Neutral",
            "Rash: +SpA, -SpD",
            "Calm: +SpD, -Atk",
            "Gentle: +SpD, -Def",
            "Sassy: +SpD, -Spe",
            "Careful: +SpD, -SpA",
            "Quirky: Neutral"
            };

        public static class TrainerClassGender
        {

            // true represents male, false represents female
            private static List<bool> _trainerClassGenders = new List<bool>();

            private static bool _tableLoaded = false;

            /// <summary>Forgets the table read from the previous ROM.</summary>
            public static void Invalidate()
            {
                _trainerClassGenders.Clear();
                _tableLoaded = false;
            }

            public static bool GetTrainerClassGender(int trainerClassID)
            {
                // The trainer class metadata patch (PR #272) reads the gender from its own record instead.
                TrainerClassMetadataDetectionState metadata = ROMFiles.TrainerClassMetadataStore.DetectCurrentRom(out string metadataDetail);
                if (metadata == ROMFiles.TrainerClassMetadataDetectionState.SchemaV1
                    && ROMFiles.TrainerClassMetadataStore.TryReadCommonFields(trainerClassID, out TrainerClassMetadataCommonFields fields, out metadataDetail))
                    return fields.Gender != 1;
                if (metadata != ROMFiles.TrainerClassMetadataDetectionState.Stock)
                {
                    AppLogger.Warn($"Trainer class {trainerClassID} gender read as male: {metadataDetail}");
                    return true;
                }

                if (TrainerClassTableExpansion.TryReadGender(trainerClassID, out byte gender, out _))
                {
                    return gender != 1;
                }

                if (!_tableLoaded)
                {
                    ReadTrainerClassGenderTable();
                }
                return _trainerClassGenders[trainerClassID];
            }
                
            public static void ReadTrainerClassGenderTable()
            {
                uint offset = RomInfo.TrainerClassGenderTableOffset;
                uint length = (uint)RomInfo.VanillaTrainerClassCount;
                if (offset == 0 || length == 0)
                {
                    AppMessages.Error("Couldn't load trainer class gender table from arm9." +
                        "\nTrainers will default to male when calculating natures.", "Error");
                    _tableLoaded = true;
                    return;
                }

                byte[] table = ARM9.ReadBytes(offset, length);

                for (int i = 0; i < table.Length; i++)
                {
                    _trainerClassGenders.Add(table[i] == 1 ? false : true);
                }
                _tableLoaded = true;

            }
        }         

    }
}
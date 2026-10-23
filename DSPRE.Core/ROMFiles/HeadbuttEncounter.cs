using System.IO;

namespace DSPRE.ROMFiles
{
    public class HeadbuttEncounter
    {
        public ushort PokemonID;
        public byte MinLevel;
        public byte MaxLevel;

        public HeadbuttEncounter()
        {
            MaxLevel = 0;
            MinLevel = 0;
            PokemonID = 0;
        }

        public HeadbuttEncounter(BinaryReader br)
        {
            this.PokemonID = br.ReadUInt16();
            this.MinLevel = br.ReadByte();
            this.MaxLevel = br.ReadByte();
        }

        public override string ToString()
        {
            string[] pokemonNames = RomInfo.GetPokemonNames();
            string pokemon = pokemonNames[PokemonID];
            return $"({PokemonID,4}) {pokemon,10}: Lv {MinLevel,3} to {MaxLevel,3}";
        }
    }
}

using System;
using System.IO;

namespace DSPRE.ROMFiles
{
  public class SafariZoneEncounter
  {
    public ushort pokemonID;
    public byte level;
    public SafariZoneEncounter() {
      level = 1;
      pokemonID = 0;
    }

    public SafariZoneEncounter(BinaryReader br) {
      readEncounter(br);
    }

    public void readEncounter(BinaryReader br) {
      this.pokemonID = br.ReadUInt16();
      this.level = br.ReadByte();
    }

    public void writeEncounter(BinaryWriter bw) {
      bw.Write((UInt16)pokemonID);
      bw.Write((byte)level);
    }

    public override string ToString() {
      string[] pokemonNames = RomInfo.GetPokemonNames();
      // hg-engine keeps a form above the species, which the base games never set.
      int species = pokemonID & HgEngine.HgEngineTrainerSource.SpeciesMask, form = pokemonID >> HgEngine.HgEngineTrainerSource.FormShift;
      string pokemon = species < pokemonNames.Length ? pokemonNames[species] : "???";
      if (form > 0) pokemon += $" (form {form})";
      return $"{species,4} {pokemon,10}: {level,3}";
    }
  }
}

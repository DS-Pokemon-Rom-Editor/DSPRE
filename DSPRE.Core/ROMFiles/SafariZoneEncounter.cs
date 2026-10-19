using System;
using System.IO;

namespace DSPRE.ROMFiles
{
  public class SafariZoneEncounter
  {
    public ushort PokemonID;
    public byte Level;
    public SafariZoneEncounter()
    {
      Level = 1;
      PokemonID = 0;
    }

    public SafariZoneEncounter(BinaryReader br)
    {
      ReadEncounter(br);
    }

    public void ReadEncounter(BinaryReader br)
    {
      this.PokemonID = br.ReadUInt16();
      this.Level = br.ReadByte();
    }

    public void WriteEncounter(BinaryWriter bw)
    {
      bw.Write((UInt16)PokemonID);
      bw.Write((byte)Level);
    }

    public override string ToString()
    {
      string[] pokemonNames = RomInfo.GetPokemonNames();
      // hg-engine keeps a form above the species, which the base games never set.
      int species = PokemonID & HgEngine.HgEngineTrainerSource.SpeciesMask, form = PokemonID >> HgEngine.HgEngineTrainerSource.FormShift;
      string pokemon = species < pokemonNames.Length ? pokemonNames[species] : "???";
      if (form > 0) pokemon += $" (form {form})";
      return $"{species,4} {pokemon,10}: {Level,3}";
    }
  }
}
